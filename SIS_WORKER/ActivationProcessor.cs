using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

public enum ProcessOutcome
{
    NotFound,
    AlreadyActive,
    Activated,
    Failed,
    DryRun
}

/// <summary>
/// Xử lý 1 bệnh nhân PENDING: tra cứu portal → (nếu chưa active) kích hoạt qua saveUser
/// → lưu PDF vào DownloadFolder → trích xuất PDF lưu DB → gửi email. Thay thế Form1.PatientId/Activation/DomActions.
/// </summary>
public sealed class ActivationProcessor
{
    private readonly PortalClient _portal;
    private readonly WorkerOptions _opt;
    private readonly PasswordResetOptions _reset;
    private readonly ILogger<ActivationProcessor> _logger;
    private readonly string _conn = DatabaseConfigService.GetConnectionString();

    public ActivationProcessor(PortalClient portal, IOptions<WorkerOptions> options, IOptions<PasswordResetOptions> resetOptions, ILogger<ActivationProcessor> logger)
    {
        _portal = portal;
        _opt = options.Value;
        _reset = resetOptions.Value;
        _logger = logger;
    }

    public async Task<ProcessOutcome> ProcessAsync(PendingPatientItem item, CancellationToken ct)
    {
        string code = item.PatientCode;
        try
        {
            // Bước 1: Tra cứu bệnh nhân trên portal
            PortalPatient? patient = await _portal.FindPatientAsync(code, ct);
            if (patient == null)
            {
                _logger.LogWarning("[{Code}] Không tìm thấy trên portal (NOT_FOUND).", code);
                if (!_opt.DryRun) await PatientAccessService.UpdatePatientStatusAsync(code, "NOT_FOUND", _conn);
                return ProcessOutcome.NotFound;
            }

            // Bước 2: Đã có tài khoản user → coi như đã active sẵn
            if (patient.IsUser)
            {
                _logger.LogInformation("[{Code}] {Name}: đã active sẵn.", code, patient.FullName);
                if (!_opt.DryRun) await PatientAccessService.UpdatePatientStatusAsync(code, "ALREADY_ACTIVE", _conn);
                return ProcessOutcome.AlreadyActive;
            }

            if (_opt.DryRun)
            {
                _logger.LogInformation("[{Code}] {Name}: CHƯA active → [DRY-RUN] bỏ qua kích hoạt.", code, patient.FullName);
                return ProcessOutcome.DryRun;
            }

            // Bước 3: Chọn email (email thật nếu có, nếu không thì {code}@gmail.com — giống Form1.Activation)
            string email = ChooseEmail(item.Email, code);
            JsonNode full = await _portal.ResolveFullPatientAsync(patient, ct);

            if (!await _portal.IsEmailUniqueAsync(full, code, email, ct))
            {
                string fallback = $"{code}@gmail.com";
                if (!string.Equals(email, fallback, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[{Code}] Email {Email} đã tồn tại → dùng email tạm {Fallback}.", code, email, fallback);
                    email = fallback;
                }
                if (!await _portal.IsEmailUniqueAsync(full, code, email, ct))
                    throw new InvalidOperationException($"Email {email} không hợp lệ/đã tồn tại trên portal.");
            }

            // Bước 4: Kích hoạt → nhận PDF
            _logger.LogInformation("[{Code}] {Name}: đang kích hoạt (email {Email})...", code, patient.FullName, email);
            ActivationResult result = await _portal.ActivateAsync(full, code, email, ct);

            // Bước 5: Lưu PDF
            Directory.CreateDirectory(_opt.DownloadFolder);
            string safeName = string.Concat(Path.GetFileName(result.FileName)
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string pdfPath = Path.Combine(_opt.DownloadFolder, $"{code}_{safeName}");
            await File.WriteAllBytesAsync(pdfPath, result.PdfBytes, ct);
            _logger.LogInformation("[{Code}] Đã lưu PDF: {Path}", code, pdfPath);

            // Đã kích hoạt trên portal → đánh dấu ACTIVATED ngay (kể cả khi bước đọc PDF bên dưới lỗi)
            await PatientAccessService.UpdatePatientStatusAsync(code, "ACTIVATED", _conn);

            // Bước 6: Trích xuất PDF → DB → gửi email
            try
            {
                PatientAccessDto dto = await PatientAccessService.ProcessAndSavePdfAsync(pdfPath, _conn, item.PhoneNumber);
                if (string.IsNullOrWhiteSpace(dto.PatientId))
                {
                    _logger.LogWarning("[{Code}] Không trích xuất được thông tin từ PDF (kiểm tra lại định dạng).", code);
                }
                else
                {
                    _logger.LogInformation("[{Code}] Đã lưu DB: {Name} (Pass: {Pass})", dto.PatientId, dto.PatientName, dto.TemporaryPassword);

                    // Đặt lịch reset mật khẩu mặc định (1 lần, sau DelayHours giờ kể từ lúc kích hoạt)
                    if (_reset.Enabled)
                    {
                        try
                        {
                            bool scheduled = await PatientAccessService.ScheduleResetAsync(dto.PatientId, _reset.DelayHours, _conn);
                            if (scheduled)
                                _logger.LogInformation("[{Code}] Đã đặt lịch reset mật khẩu sau {Hours}h.", dto.PatientId, _reset.DelayHours);
                            else
                                _logger.LogWarning("[{Code}] Không đặt được lịch reset (chưa có dòng PatientAccessInfo).", dto.PatientId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[{Code}] Lỗi đặt lịch reset mật khẩu (bỏ qua, không ảnh hưởng kích hoạt).", dto.PatientId);
                        }
                    }
                    await SendEmailAsync(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Code}] Lỗi khi đọc PDF/lưu DB/gửi email (portal đã kích hoạt, PDF vẫn nằm ở {Path}).", code, pdfPath);
            }

            return ProcessOutcome.Activated;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Code}] Kích hoạt thất bại.", code);
            if (!_opt.DryRun)
            {
                try { await PatientAccessService.UpdatePatientStatusAsync(code, "FAILED", _conn); } catch { /* bỏ qua */ }
            }
            return ProcessOutcome.Failed;
        }
    }

    private static string ChooseEmail(string? raw, string code)
    {
        bool isReal = !string.IsNullOrWhiteSpace(raw)
                      && raw.Contains('@')
                      && !PatientAccessService.IsFakeEmail(raw, code);
        return isReal
            ? raw!.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim()
            : $"{code}@gmail.com";
    }

    /// <summary>Gửi email thông tin tài khoản — giữ nguyên logic cũ ở Form1.cs (DownloadStarting).</summary>
    private async Task SendEmailAsync(PatientAccessDto dto)
    {
        try
        {
            var emailConfig = EmailService.LoadConfig();
            string? realEmail = await PatientAccessService.GetPatientRealEmailAsync(dto.PatientId, _conn);

            // Nếu có OverrideRecipientEmail thì gửi về email giả lập để test
            string? target = !string.IsNullOrWhiteSpace(emailConfig.OverrideRecipientEmail)
                ? emailConfig.OverrideRecipientEmail.Trim()
                : realEmail;

            if (string.IsNullOrWhiteSpace(target))
            {
                await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "NO_EMAIL", null, _conn);
                return;
            }

            _logger.LogInformation("[{Id}] Đang gửi thông tin tài khoản đến {Email}...", dto.PatientId, target);
            var (ok, err) = await EmailService.SendAccountInfoEmailAsync(dto, target);
            if (ok)
            {
                await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "SENT", null, _conn);
                _logger.LogInformation("[{Id}] Gửi email thành công đến {Email}.", dto.PatientId, target);
            }
            else
            {
                await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "FAILED", err, _conn);
                _logger.LogWarning("[{Id}] Gửi email thất bại: {Err}", dto.PatientId, err);
            }
        }
        catch (Exception ex)
        {
            await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "FAILED", ex.Message, _conn);
            _logger.LogWarning(ex, "[{Id}] Lỗi gửi email.", dto.PatientId);
        }
    }

}
