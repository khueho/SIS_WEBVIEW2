using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>
/// Xử lý reset mật khẩu mặc định cho 1 tài khoản đã active (giống nút "Tạo mật khẩu mới" trên portal):
/// queryUsers → resolveFullPatient(userId) → generatePassword → update (portal trả PDF mới)
/// → lưu PDF → đọc PDF lưu DB (TemporaryPassword mới) → xoá PDF cũ của cùng mã.
/// KHÔNG gửi email, KHÔNG gửi SMS.
/// </summary>
public sealed class PasswordResetProcessor : IDisposable
{
    // Phiên portal RIÊNG (login riêng) → chạy song song được với luồng kích hoạt, không dùng chung cookie/p_auth.
    private readonly PortalClient _portal;
    private readonly WorkerOptions _opt;
    private readonly PasswordResetOptions _reset;
    private readonly ILogger<PasswordResetProcessor> _logger;
    private readonly string _conn = DatabaseConfigService.GetConnectionString();

    public PasswordResetProcessor(
        IOptions<PortalOptions> portalOptions,
        ILogger<PortalClient> portalLogger,
        IOptions<WorkerOptions> options,
        IOptions<PasswordResetOptions> resetOptions,
        ILogger<PasswordResetProcessor> logger)
    {
        _portal = new PortalClient(portalOptions, portalLogger);
        _opt = options.Value;
        _reset = resetOptions.Value;
        _logger = logger;
    }

    public void Dispose() => _portal.Dispose();

    /// <summary>Trả true nếu reset thành công.</summary>
    public async Task<bool> ProcessAsync(DueResetItem item, CancellationToken ct)
    {
        string code = item.PatientCode;

        try
        {
            PortalPatient? patient = await _portal.FindPatientAsync(code, ct);
            if (patient == null)
                throw new InvalidOperationException("Không tìm thấy trên portal.");
            if (!patient.IsUser || string.IsNullOrWhiteSpace(patient.UserId))
                throw new InvalidOperationException("Chưa phải user trên portal (chưa active) → không reset được.");

            _logger.LogInformation("[{Code}] {Name}: đang reset mật khẩu (userId {UserId})...", code, patient.FullName, patient.UserId);

            System.Text.Json.Nodes.JsonNode full = await _portal.ResolveFullPatientAsync(patient, ct, forExistingUser: true);
            await _portal.GeneratePasswordAsync(ct);
            ActivationResult result = await _portal.ResetPasswordAsync(patient, full, code, ct);

            // Lưu PDF mới với tên có dấu thời gian để không ghi đè PDF kích hoạt
            Directory.CreateDirectory(_opt.DownloadFolder);
            string safeName = string.Concat(Path.GetFileName(result.FileName)
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string pdfPath = Path.Combine(_opt.DownloadFolder, $"{code}_reset_{DateTime.Now:yyyyMMddHHmmss}_{safeName}");
            await File.WriteAllBytesAsync(pdfPath, result.PdfBytes, ct);
            _logger.LogInformation("[{Code}] Đã lưu PDF reset: {Path}", code, pdfPath);

            // Đọc PDF → cập nhật DB (TemporaryPassword mới). Không gửi email.
            PatientAccessDto dto = await PatientAccessService.ProcessAndSavePdfAsync(pdfPath, _conn, item.PhoneNumber);
            if (string.IsNullOrWhiteSpace(dto.PatientId))
                throw new InvalidOperationException($"Không trích xuất được thông tin từ PDF reset ({pdfPath}).");
            if (!string.Equals(dto.PatientId, code, StringComparison.OrdinalIgnoreCase))
                _logger.LogWarning("[{Code}] PatientId trong PDF ({PdfId}) khác mã đang reset.", code, dto.PatientId);

            await PatientAccessService.MarkResetDoneAsync(code, _conn);
            _logger.LogInformation("[{Code}] Reset mật khẩu thành công (Pass mới: {Pass}).", code, dto.TemporaryPassword);

            DeleteOldPdfs(code, pdfPath);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            bool gaveUp = false;
            try { gaveUp = await PatientAccessService.MarkResetFailedAsync(code, _reset.MaxAttempts, _conn); }
            catch (Exception dbEx) { _logger.LogWarning(dbEx, "[{Code}] Không ghi được trạng thái lỗi reset.", code); }

            if (gaveUp)
                _logger.LogError(ex, "[{Code}] Reset mật khẩu thất bại, đã quá {Max} lần → BỎ CUỘC (xoá lịch reset).", code, _reset.MaxAttempts);
            else
                _logger.LogError(ex, "[{Code}] Reset mật khẩu thất bại (sẽ thử lại ở chu kỳ sau).", code);
            return false;
        }
    }

    /// <summary>
    /// Xoá các PDF cũ của đúng mã này (mẫu {code}_*.pdf) trừ file vừa tạo. Mẫu có dấu "_" nên không khớp mã khác
    /// chỉ trùng tiền tố (vd 123_ không khớp 1234_). Lỗi xoá chỉ log cảnh báo.
    /// </summary>
    private void DeleteOldPdfs(string code, string keepPath)
    {
        try
        {
            string keep = Path.GetFullPath(keepPath);
            foreach (string file in Directory.GetFiles(_opt.DownloadFolder, $"{code}_*.pdf", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFullPath(file), keep, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("[{Code}] Đã xoá PDF cũ: {File}", code, file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{Code}] Không xoá được PDF cũ {File}.", code, file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Code}] Lỗi khi dọn PDF cũ.", code);
        }
    }
}
