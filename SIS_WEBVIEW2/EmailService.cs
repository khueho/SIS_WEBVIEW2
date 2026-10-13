using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SIS_WEBVIEW2
{
    public class EmailConfig
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = false;
        public bool UseStartTls { get; set; } = true;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
        public string SenderDisplayName { get; set; } = "Bệnh viện Đa Khoa Quốc Tế S.I.S Cần Thơ";
        public string Subject { get; set; } = "[S.I.S Vietnam] Thông tin tài khoản tra cứu hồ sơ bệnh án điện tử";
        public string? OverrideRecipientEmail { get; set; } = null;
    }


    public static class EmailService
    {
        private static EmailConfig? _cachedConfig;
        private static readonly string ConfigFileName = "email_config.json";

        /// <summary>
        /// Tải cấu hình từ email_config.json
        /// </summary>
        public static EmailConfig LoadConfig(bool forceReload = false)
        {
            if (!forceReload && _cachedConfig != null) return _cachedConfig;

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (!File.Exists(configPath))
            {
                // Tìm ngược lên thư mục chạy của project nếu đang debug
                string fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
                if (File.Exists(fallbackPath)) configPath = fallbackPath;
            }

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var cfg = JsonSerializer.Deserialize<EmailConfig>(json);
                    if (cfg != null)
                    {
                        _cachedConfig = cfg;
                        return _cachedConfig;
                    }
                }
                catch
                {
                    // Fallback cấu hình mặc định nếu lỗi parse JSON
                }
            }

            _cachedConfig = new EmailConfig();
            return _cachedConfig;
        }

        /// <summary>
        /// Gửi thông tin tài khoản bệnh nhân qua email bằng MailKit
        /// </summary>
        public static async Task<(bool Success, string? ErrorMessage)> SendAccountInfoEmailAsync(
            PatientAccessDto accountInfo,
            string recipientEmail)
        {
            var config = LoadConfig();

            // Nếu có cấu hình OverrideRecipientEmail, ưu tiên chuyển hướng người nhận sang email này để test
            string targetRecipient = !string.IsNullOrWhiteSpace(config.OverrideRecipientEmail)
                ? config.OverrideRecipientEmail.Trim()
                : recipientEmail;

            if (string.IsNullOrWhiteSpace(targetRecipient))
            {
                return (false, "Địa chỉ email người nhận trống.");
            }

            if (string.IsNullOrWhiteSpace(config.SenderEmail) || string.IsNullOrWhiteSpace(config.SenderPassword))
            {
                return (false, "Chưa cấu hình SenderEmail hoặc SenderPassword trong email_config.json!");
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(config.SenderDisplayName, config.SenderEmail));
                message.To.Add(new MailboxAddress(accountInfo.PatientName, targetRecipient));

                // Nếu đang dùng email giả lập để test, ghi rõ vào tiêu đề để người test nhận biết
                if (!string.IsNullOrWhiteSpace(config.OverrideRecipientEmail))
                {
                    message.Subject = $"[TEST GIẢ LẬP -> {recipientEmail}] {config.Subject}";
                }
                else
                {
                    message.Subject = config.Subject;
                }


                var builder = new BodyBuilder
                {
                    HtmlBody = BuildHtmlBody(accountInfo)
                };

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();

                // Bỏ qua lỗi SSL Certificate nếu dùng Mail Server nội bộ viện tự ký
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                var secureSocketOptions = config.UseSsl
                    ? SecureSocketOptions.SslOnConnect
                    : (config.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

                await client.ConnectAsync(config.SmtpHost, config.SmtpPort, secureSocketOptions);
                await client.AuthenticateAsync(config.SenderEmail, config.SenderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Xây dựng nội dung email dạng HTML chuyên nghiệp, thân thiện, bảo mật
        /// </summary>
        private static string BuildHtmlBody(PatientAccessDto accountInfo)
        {
            string patientName = string.IsNullOrWhiteSpace(accountInfo.PatientName) ? "Quý khách" : accountInfo.PatientName;
            string portalUrl = string.IsNullOrWhiteSpace(accountInfo.PortalUrl) ? "https://portal.sisvietnam.vn" : accountInfo.PortalUrl;

            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333333;
            background-color: #f4f6f9;
            margin: 0;
            padding: 20px;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 12px rgba(0,0,0,0.08);
            border: 1px solid #e1e8ed;
        }}
        .header {{
            background: linear-gradient(135deg, #0052cc 0%, #0747a6 100%);
            color: #ffffff;
            padding: 24px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 20px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }}
        .header p {{
            margin: 6px 0 0 0;
            font-size: 13px;
            opacity: 0.9;
        }}
        .content {{
            padding: 24px;
        }}
        .greeting {{
            font-size: 15px;
            margin-bottom: 16px;
        }}
        .card {{
            background-color: #f8fafc;
            border: 1px solid #e2e8f0;
            border-left: 4px solid #0052cc;
            border-radius: 6px;
            padding: 18px;
            margin: 20px 0;
        }}
        .row {{
            margin-bottom: 12px;
            display: flex;
        }}
        .row:last-child {{
            margin-bottom: 0;
        }}
        .label {{
            font-weight: 600;
            color: #475569;
            width: 140px;
            display: inline-block;
        }}
        .value {{
            color: #0f172a;
            font-weight: 500;
        }}
        .highlight {{
            background: #e0f2fe;
            color: #0369a1;
            padding: 3px 8px;
            border-radius: 4px;
            font-family: Consolas, 'Courier New', monospace;
            font-size: 16px;
            font-weight: 700;
            letter-spacing: 0.5px;
        }}
        .btn {{
            display: inline-block;
            background-color: #0052cc;
            color: #ffffff !important;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            margin-top: 15px;
            text-align: center;
        }}
        .notice {{
            font-size: 13px;
            color: #d97706;
            background: #fffbeb;
            border: 1px solid #fef3c7;
            padding: 12px;
            border-radius: 6px;
            margin-top: 18px;
        }}
        .footer {{
            background-color: #f1f5f9;
            padding: 16px 24px;
            text-align: center;
            font-size: 12px;
            color: #64748b;
            border-top: 1px solid #e2e8f0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>BỆNH VIỆN ĐA KHOA QUỐC TẾ S.I.S CẦN THƠ</h1>
            <p>Hệ thống Tra cứu Hồ sơ & Kết quả Chẩn đoán hình ảnh</p>
        </div>
        <div class=""content"">
            <div class=""greeting"">
                Kính gửi Quý bệnh nhân: <strong>{patientName}</strong>,
            </div>
            <p>Tài khoản truy cập Cổng thông tin hồ sơ y tế điện tử của Quý khách đã được kích hoạt thành công. Quý khách có thể đăng nhập để tra cứu hình ảnh và kết quả cận lâm sàng theo thông tin bên dưới:</p>
            
            <div class=""card"">
                <div class=""row"">
                    <span class=""label"">Mã bệnh nhân:</span>
                    <span class=""value""><strong>{accountInfo.PatientId}</strong></span>
                </div>
                <div class=""row"">
                    <span class=""label"">Cổng đăng nhập:</span>
                    <span class=""value""><a href=""{portalUrl}"" target=""_blank"">{portalUrl}</a></span>
                </div>
                <div class=""row"">
                    <span class=""label"">Tên đăng nhập:</span>
                    <span class=""value highlight"">{accountInfo.Username}</span>
                </div>
                <div class=""row"">
                    <span class=""label"">Mật khẩu tạm thời:</span>
                    <span class=""value highlight"">{accountInfo.TemporaryPassword}</span>
                </div>
            </div>

            <div style=""text-align: center;"">
                <a href=""{portalUrl}"" class=""btn"" target=""_blank"">Truy cập Cổng tra cứu ngay &rarr;</a>
            </div>

            <div class=""notice"">
                ⚠️ <strong>Lưu ý bảo mật:</strong> Để đảm bảo an toàn thông tin cá nhân và hồ sơ bệnh án, Quý khách vui lòng đổi mật khẩu ngay trong lần đăng nhập đầu tiên.
            </div>
        </div>
        <div class=""footer"">
            <p>Bệnh viện Đa Khoa Quốc Tế S.I.S Cần Thơ | Hotline CSKH: 1800 1115</p>
            <p>Email này được gửi tự động từ hệ thống. Quý khách vui lòng không phản hồi trực tiếp vào email này.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
