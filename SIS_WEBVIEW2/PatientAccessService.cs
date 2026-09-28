using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using UglyToad.PdfPig;

namespace SIS_WEBVIEW2
{
    public class PatientAccessDto
    {
        public string PatientId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
        public string PortalUrl { get; set; } = "https://portal.sisvietnam.vn";
    }

    public static class PatientAccessService
    {
        /// <summary>
        /// Đọc PDF và trích xuất dữ liệu tài khoản bệnh nhân
        /// </summary>
        public static PatientAccessDto ExtractFromPdf(string pdfPath)
        {
            using var document = PdfDocument.Open(pdfPath);
            string text = string.Join(" ", document.GetPages().Select(p => p.Text)).Trim();

            // Cấu trúc text stream của SIS BIRT: [PatientId][Name]https://portal...[User][Password]system@...
            var match = Regex.Match(
                text,
                @"(?<id>\d{6,10})\s*(?<name>.*?)\s*(?<url>https?://[^\s\d]+)\s*(?<user>\d{6,10})\s*(?<pass>.+?)(?=(?:system@|csckh@|support@|admin@|info@|$))",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );

            // Cắt bỏ phần email liên hệ dính ở đuôi mật khẩu (do trong PDF không có dấu cách ngăn cách)
            string pass = match.Groups["pass"].Value.Trim();
            pass = Regex.Replace(pass, @"(?:system@|csckh@|support@|admin@|info@).*$", "", RegexOptions.IgnoreCase).Trim();

            // Fallback lấy tên từ tên file nếu PDF thiếu
            string name = match.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(name))
            {
                var fileMatch = Regex.Match(Path.GetFileNameWithoutExtension(pdfPath), @"(?:-_|-)\s*(.+)$");
                if (fileMatch.Success) name = fileMatch.Groups[1].Value.Trim();
            }

            return new PatientAccessDto
            {
                PatientId = match.Groups["id"].Value.Trim(),
                PatientName = name,
                PortalUrl = match.Groups["url"].Success ? match.Groups["url"].Value.Trim() : "https://portal.sisvietnam.vn",
                Username = match.Groups["user"].Success ? match.Groups["user"].Value.Trim() : match.Groups["id"].Value.Trim(),
                TemporaryPassword = pass
            };
        }

        /// <summary>
        /// Lưu hoặc cập nhật thông tin bệnh nhân vào SQL Server (tránh trùng khóa)
        /// </summary>
        public static async Task SaveToSqlAsync(PatientAccessDto dto, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(dto.PatientId)) return;

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM PatientAccessInfo WHERE PatientId = @Id)
                    INSERT INTO PatientAccessInfo (PatientId, PatientName, Username, TemporaryPassword, PortalUrl, SavedAt)
                    VALUES (@Id, @Name, @User, @Pass, @Url, GETDATE());
                ELSE
                    UPDATE PatientAccessInfo 
                    SET PatientName = @Name, Username = @User, TemporaryPassword = @Pass, PortalUrl = @Url, SavedAt = GETDATE()
                    WHERE PatientId = @Id;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", dto.PatientId);
            cmd.Parameters.AddWithValue("@Name", dto.PatientName);
            cmd.Parameters.AddWithValue("@User", dto.Username);
            cmd.Parameters.AddWithValue("@Pass", dto.TemporaryPassword);
            cmd.Parameters.AddWithValue("@Url", dto.PortalUrl);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Đọc và lưu 1 file PDF vào database
        /// </summary>
        public static async Task<PatientAccessDto> ProcessAndSavePdfAsync(string pdfPath, string connectionString)
        {
            var dto = ExtractFromPdf(pdfPath);
            await SaveToSqlAsync(dto, connectionString);
            return dto;
        }

        /// <summary>
        /// Lưu danh sách bệnh nhân lấy từ API vào bảng DiagnosticImagingPatients.
        /// Chống trùng: Không bao giờ đè trạng thái đã hoàn thành (ACTIVATED, ALREADY_ACTIVE).
        /// </summary>
        public static async Task<(int Total, int NewInserted, int Updated)> SaveApiPatientsAsync(
            IEnumerable<DiagnosticImagingPatientDto> patients, string connectionString)
        {
            int total = 0;
            int inserted = 0;
            int updated = 0;

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM DiagnosticImagingPatients WHERE PatientCode = @PatientCode)
                BEGIN
                    INSERT INTO DiagnosticImagingPatients 
                        (PatientCode, FullName, Address, PhoneNumber, Email, SentDate, ProcessStatus, FetchedAt)
                    VALUES 
                        (@PatientCode, @FullName, @Address, @PhoneNumber, @Email, @SentDate, 'PENDING', GETDATE());
                    SELECT 1 AS Result; -- Đã thêm mới
                END
                ELSE
                BEGIN
                    UPDATE DiagnosticImagingPatients
                    SET FullName = @FullName,
                        Address = @Address,
                        PhoneNumber = @PhoneNumber,
                        Email = @Email,
                        SentDate = @SentDate
                    WHERE PatientCode = @PatientCode;
                    SELECT 2 AS Result; -- Đã cập nhật
                END";

            foreach (var p in patients)
            {
                if (string.IsNullOrWhiteSpace(p.PatientCode)) continue;
                total++;

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@PatientCode", p.PatientCode);
                cmd.Parameters.AddWithValue("@FullName", (object?)p.FullName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Address", (object?)p.Address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PhoneNumber", (object?)p.PhoneNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", (object?)p.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SentDate", (object?)p.SentDate ?? DBNull.Value);

                var res = await cmd.ExecuteScalarAsync();
                if (res != null && Convert.ToInt32(res) == 1)
                {
                    inserted++;
                }
                else
                {
                    updated++;
                }
            }

            return (total, inserted, updated);
        }

        /// <summary>
        /// Lấy danh sách PatientCode đang PENDING và chưa có trong PatientAccessInfo
        /// </summary>
        public static async Task<List<string>> GetPendingPatientCodesAsync(string connectionString)
        {
            var result = new List<string>();

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT PatientCode 
                FROM DiagnosticImagingPatients
                WHERE ProcessStatus = 'PENDING'
                  AND PatientCode NOT IN (SELECT PatientId FROM PatientAccessInfo)
                ORDER BY FetchedAt ASC;";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string code = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    result.Add(code);
                }
            }

            return result;
        }

        /// <summary>
        /// Cập nhật trạng thái xử lý của bệnh nhân trong DiagnosticImagingPatients
        /// </summary>
        public static async Task UpdatePatientStatusAsync(string patientCode, string status, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(patientCode)) return;

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                UPDATE DiagnosticImagingPatients 
                SET ProcessStatus = @Status,
                    ProcessedAt = GETDATE()
                WHERE PatientCode = @PatientCode;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@PatientCode", patientCode);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Thống kê số lượng bệnh nhân theo các trạng thái
        /// </summary>
        public static async Task<(int Total, int Pending, int Activated, int AlreadyActive, int Failed, int NotFound)> GetStatusSummaryAsync(string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT 
                    COUNT(*) AS Total,
                    SUM(CASE WHEN ProcessStatus = 'PENDING' THEN 1 ELSE 0 END) AS PendingCount,
                    SUM(CASE WHEN ProcessStatus = 'ACTIVATED' THEN 1 ELSE 0 END) AS ActivatedCount,
                    SUM(CASE WHEN ProcessStatus = 'ALREADY_ACTIVE' THEN 1 ELSE 0 END) AS AlreadyActiveCount,
                    SUM(CASE WHEN ProcessStatus = 'FAILED' THEN 1 ELSE 0 END) AS FailedCount,
                    SUM(CASE WHEN ProcessStatus = 'NOT_FOUND' THEN 1 ELSE 0 END) AS NotFoundCount
                FROM DiagnosticImagingPatients;";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                int pending = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                int activated = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                int alreadyActive = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                int failed = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                int notFound = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                return (total, pending, activated, alreadyActive, failed, notFound);
            }

            return (0, 0, 0, 0, 0, 0);
        }
    }
}

