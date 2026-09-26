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
                @"(?<id>\d{6,10})\s*(?<name>.*?)\s*(?<url>https?://[^\s\d]+)\s*(?<user>\d{6,10})\s*(?<pass>.+?)(?=(?:system@|csckh@|\S+@dotquy\.vn|$))",
                RegexOptions.Singleline | RegexOptions.IgnoreCase
            );

            // Cắt bỏ phần email liên hệ dính ở đuôi mật khẩu (do trong PDF không có dấu cách ngăn cách)
            string pass = match.Groups["pass"].Value.Trim();
            pass = Regex.Replace(pass, @"(?:system@|csckh@|[a-zA-Z0-9._%+-]+@).*$", "", RegexOptions.IgnoreCase).Trim();

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
    }
}
