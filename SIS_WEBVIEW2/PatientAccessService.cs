using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
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

    public class PendingPatientItem
    {
        public string PatientCode { get; set; } = string.Empty;
        public string? Email { get; set; }
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
                @"(?<id>\d{6,10})\s*(?<name>.*?)\s*(?<url>https?://[^\s\d]+)\s*(?<user>\k<id>|\d{6,10}?)\s*(?<pass>.+?)(?=(?:system@|csckh@|support@|admin@|info@|$))",
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
        /// Lưu hoặc cập nhật thông tin bệnh nhân vào PostgreSQL (tránh trùng khóa qua ON CONFLICT)
        /// </summary>
        public static async Task SaveToSqlAsync(PatientAccessDto dto, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(dto.PatientId)) return;

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                INSERT INTO PatientAccessInfo (PatientId, PatientName, Username, TemporaryPassword, PortalUrl, SavedAt, EmailSentStatus)
                VALUES (@Id, @Name, @User, @Pass, @Url, NOW(), 'PENDING')
                ON CONFLICT (PatientId) DO UPDATE
                SET PatientName = EXCLUDED.PatientName,
                    Username = EXCLUDED.Username,
                    TemporaryPassword = EXCLUDED.TemporaryPassword,
                    PortalUrl = EXCLUDED.PortalUrl,
                    SavedAt = NOW();";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("@Id", NpgsqlDbType.Varchar) { Value = dto.PatientId });
            cmd.Parameters.Add(new NpgsqlParameter("@Name", NpgsqlDbType.Varchar) { Value = dto.PatientName });
            cmd.Parameters.Add(new NpgsqlParameter("@User", NpgsqlDbType.Varchar) { Value = dto.Username });
            cmd.Parameters.Add(new NpgsqlParameter("@Pass", NpgsqlDbType.Varchar) { Value = dto.TemporaryPassword });
            cmd.Parameters.Add(new NpgsqlParameter("@Url", NpgsqlDbType.Text) { Value = dto.PortalUrl });

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
        /// Kiểm tra xem email có phải là email giả lập do API/hệ thống tự sinh ({PatientCode}@gmail.com) hay không.
        /// </summary>
        public static bool IsFakeEmail(string? email, string? patientCode)
        {
            if (string.IsNullOrWhiteSpace(email)) return true;
            string cleanEmail = email.Trim();
            if (!string.IsNullOrWhiteSpace(patientCode))
            {
                string cleanCode = patientCode.Trim();
                if (cleanEmail.Equals($"{cleanCode}@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Lưu danh sách bệnh nhân lấy từ API vào bảng DiagnosticImagingPatients trên PostgreSQL.
        /// Chống trùng: Dùng ON CONFLICT (PatientCode) DO UPDATE.
        /// </summary>
        public static async Task<(int Total, int NewInserted, int Updated)> SaveApiPatientsAsync(
            IEnumerable<DiagnosticImagingPatientDto> patients, string connectionString)
        {
            int total = 0;
            int inserted = 0;
            int updated = 0;

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                INSERT INTO DiagnosticImagingPatients 
                    (PatientCode, FullName, Address, PhoneNumber, Email, SentDate, ProcessStatus, FetchedAt)
                VALUES 
                    (@PatientCode, @FullName, @Address, @PhoneNumber, @Email, @SentDate, 'PENDING', NOW())
                ON CONFLICT (PatientCode) DO UPDATE
                SET FullName = EXCLUDED.FullName,
                    Address = EXCLUDED.Address,
                    PhoneNumber = EXCLUDED.PhoneNumber,
                    Email = EXCLUDED.Email,
                    SentDate = EXCLUDED.SentDate
                RETURNING (xmax = 0) AS is_inserted;";

            foreach (var p in patients)
            {
                if (string.IsNullOrWhiteSpace(p.PatientCode)) continue;
                total++;

                // Nếu email là dạng giả lập {patientId}@gmail.com từ API thì lưu NULL để biết BN chưa có email thật
                string? realEmail = IsFakeEmail(p.Email, p.PatientCode) ? null : p.Email?.Trim();

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.Add(new NpgsqlParameter("@PatientCode", NpgsqlDbType.Varchar) { Value = p.PatientCode });
                cmd.Parameters.Add(new NpgsqlParameter("@FullName", NpgsqlDbType.Varchar) { Value = (object?)p.FullName ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@Address", NpgsqlDbType.Text) { Value = (object?)p.Address ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@PhoneNumber", NpgsqlDbType.Varchar) { Value = (object?)p.PhoneNumber ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@Email", NpgsqlDbType.Varchar) { Value = (object?)realEmail ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@SentDate", NpgsqlDbType.Text) { Value = (object?)p.SentDate ?? DBNull.Value });

                var res = await cmd.ExecuteScalarAsync();
                if (res is bool isInserted && isInserted)
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
        /// Lấy danh sách bệnh nhân đang PENDING (kèm Email nếu có) và chưa có trong PatientAccessInfo
        /// </summary>
        public static async Task<List<PendingPatientItem>> GetPendingPatientsAsync(string connectionString)
        {
            var result = new List<PendingPatientItem>();

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT PatientCode, Email 
                FROM DiagnosticImagingPatients
                WHERE ProcessStatus = 'PENDING'
                  AND PatientCode NOT IN (SELECT PatientId FROM PatientAccessInfo)
                ORDER BY FetchedAt ASC;";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string code = reader.GetString(0);
                string? email = reader.IsDBNull(1) ? null : reader.GetString(1);

                // Loại trừ nếu trong DB còn sót email giả lập
                if (IsFakeEmail(email, code))
                {
                    email = null;
                }

                if (!string.IsNullOrWhiteSpace(code))
                {
                    result.Add(new PendingPatientItem
                    {
                        PatientCode = code,
                        Email = email
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Lấy danh sách PatientCode đang PENDING và chưa có trong PatientAccessInfo
        /// </summary>
        public static async Task<List<string>> GetPendingPatientCodesAsync(string connectionString)
        {
            var patients = await GetPendingPatientsAsync(connectionString);
            return patients.Select(p => p.PatientCode).ToList();
        }

        /// <summary>
        /// Cập nhật trạng thái xử lý của bệnh nhân trong DiagnosticImagingPatients
        /// </summary>
        public static async Task UpdatePatientStatusAsync(string patientCode, string status, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(patientCode)) return;

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                UPDATE DiagnosticImagingPatients 
                SET ProcessStatus = @Status,
                    ProcessedAt = NOW()
                WHERE PatientCode = @PatientCode;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("@Status", NpgsqlDbType.Varchar) { Value = status });
            cmd.Parameters.Add(new NpgsqlParameter("@PatientCode", NpgsqlDbType.Varchar) { Value = patientCode });

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Thống kê số lượng bệnh nhân theo các trạng thái
        /// </summary>
        public static async Task<(int Total, int Pending, int Activated, int AlreadyActive, int Failed, int NotFound)> GetStatusSummaryAsync(string connectionString)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT 
                    COUNT(*) AS Total,
                    COALESCE(SUM(CASE WHEN ProcessStatus = 'PENDING' THEN 1 ELSE 0 END), 0) AS PendingCount,
                    COALESCE(SUM(CASE WHEN ProcessStatus = 'ACTIVATED' THEN 1 ELSE 0 END), 0) AS ActivatedCount,
                    COALESCE(SUM(CASE WHEN ProcessStatus = 'ALREADY_ACTIVE' THEN 1 ELSE 0 END), 0) AS AlreadyActiveCount,
                    COALESCE(SUM(CASE WHEN ProcessStatus = 'FAILED' THEN 1 ELSE 0 END), 0) AS FailedCount,
                    COALESCE(SUM(CASE WHEN ProcessStatus = 'NOT_FOUND' THEN 1 ELSE 0 END), 0) AS NotFoundCount
                FROM DiagnosticImagingPatients;";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int total = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                int pending = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
                int activated = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                int alreadyActive = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
                int failed = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
                int notFound = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5));
                return (total, pending, activated, alreadyActive, failed, notFound);
            }

            return (0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Tự động kiểm tra và khởi tạo database & schema bảng PostgreSQL nếu chưa tồn tại
        /// </summary>
        public static async Task EnsureDatabaseSchemaAsync(string connectionString)
        {
            try
            {
                // Bước 1: Đảm bảo Database tồn tại (kết nối tạm qua database mặc định 'postgres' để kiểm tra & tạo nếu thiếu)
                try
                {
                    var csb = new NpgsqlConnectionStringBuilder(connectionString);
                    string targetDb = string.IsNullOrWhiteSpace(csb.Database) ? "sis_patient_db" : csb.Database;

                    var adminCsb = new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        Database = "postgres"
                    };

                    using (var adminConn = new NpgsqlConnection(adminCsb.ConnectionString))
                    {
                        await adminConn.OpenAsync();
                        using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @db;", adminConn);
                        checkCmd.Parameters.AddWithValue("@db", targetDb);
                        var exists = await checkCmd.ExecuteScalarAsync();

                        if (exists == null)
                        {
                            string safeDbName = targetDb.Replace("\"", "\"\"");
                            using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{safeDbName}\";", adminConn);
                            await createCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch
                {
                    // Bỏ qua nếu user không có quyền trên database 'postgres' hoặc database đã sẵn sàng
                }

                // Bước 2: Tạo các bảng & cột cần thiết nếu chưa có
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = @"
                    CREATE TABLE IF NOT EXISTS DiagnosticImagingPatients (
                        PatientCode VARCHAR(50) PRIMARY KEY,
                        FullName VARCHAR(255),
                        Address TEXT,
                        PhoneNumber VARCHAR(50),
                        Email VARCHAR(255),
                        SentDate TEXT,
                        ProcessStatus VARCHAR(50) DEFAULT 'PENDING',
                        FetchedAt TIMESTAMP DEFAULT NOW(),
                        ProcessedAt TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS PatientAccessInfo (
                        PatientId VARCHAR(50) PRIMARY KEY,
                        PatientName VARCHAR(255),
                        Username VARCHAR(100),
                        TemporaryPassword VARCHAR(100),
                        PortalUrl TEXT,
                        SavedAt TIMESTAMP DEFAULT NOW(),
                        EmailSentStatus VARCHAR(50) DEFAULT 'PENDING',
                        EmailSentAt TIMESTAMP,
                        EmailError TEXT
                    );

                    ALTER TABLE PatientAccessInfo ADD COLUMN IF NOT EXISTS EmailSentStatus VARCHAR(50) DEFAULT 'PENDING';
                    ALTER TABLE PatientAccessInfo ADD COLUMN IF NOT EXISTS EmailSentAt TIMESTAMP;
                    ALTER TABLE PatientAccessInfo ADD COLUMN IF NOT EXISTS EmailError TEXT;";

                using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Bỏ qua lỗi nếu không đủ quyền DDL, app vẫn hoạt động bình thường
            }
        }

        /// <summary>
        /// Truy vấn email thật của bệnh nhân từ bảng DiagnosticImagingPatients (loại bỏ email giả lập)
        /// </summary>
        public static async Task<string?> GetPatientRealEmailAsync(string patientId, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return null;

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"SELECT Email FROM DiagnosticImagingPatients WHERE PatientCode = @PatientCode LIMIT 1;";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("@PatientCode", NpgsqlDbType.Varchar) { Value = patientId });

            var obj = await cmd.ExecuteScalarAsync();
            if (obj != null && obj != DBNull.Value)
            {
                string email = obj.ToString()!.Trim();
                if (!IsFakeEmail(email, patientId) && email.Contains("@"))
                {
                    return email;
                }
            }

            return null;
        }

        /// <summary>
        /// Cập nhật trạng thái gửi email cho bệnh nhân trong PatientAccessInfo
        /// </summary>
        public static async Task UpdateEmailSentStatusAsync(string patientId, string status, string? error, string connectionString)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return;

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
                UPDATE PatientAccessInfo
                SET EmailSentStatus = @Status,
                    EmailSentAt = CASE WHEN @Status = 'SENT' THEN NOW() ELSE EmailSentAt END,
                    EmailError = @Error
                WHERE PatientId = @PatientId;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new NpgsqlParameter("@PatientId", NpgsqlDbType.Varchar) { Value = patientId });
            cmd.Parameters.Add(new NpgsqlParameter("@Status", NpgsqlDbType.Varchar) { Value = status });
            cmd.Parameters.Add(new NpgsqlParameter("@Error", NpgsqlDbType.Text) { Value = (object?)error ?? DBNull.Value });

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
