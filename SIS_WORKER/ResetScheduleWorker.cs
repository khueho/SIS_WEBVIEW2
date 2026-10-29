using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>
/// Định kỳ hỏi nguồn (API giả lập/thật) danh sách mã cần reset mật khẩu, đặt ResetDueAt = ngay bây giờ.
/// PasswordResetWorker sẽ nhặt và reset. Mỗi mã chỉ được đặt lịch 1 lần/ngày (chống reset lặp mỗi chu kỳ).
/// </summary>
public sealed class ResetScheduleWorker : BackgroundService
{
    private readonly ResetApiOptions _api;
    private readonly PasswordResetOptions _reset;
    private readonly IResetRequestSource _source;
    private readonly ILogger<ResetScheduleWorker> _logger;
    private readonly string _conn = DatabaseConfigService.GetConnectionString();

    public ResetScheduleWorker(
        IOptions<ResetApiOptions> api,
        IOptions<PasswordResetOptions> reset,
        IResetRequestSource source,
        ILogger<ResetScheduleWorker> logger)
    {
        _api = api.Value;
        _reset = reset.Value;
        _source = source;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_api.Enabled || !_reset.Enabled)
        {
            _logger.LogInformation("ResetApi:Enabled={Api}, PasswordReset:Enabled={Reset} → không lấy danh sách reset từ API.",
                _api.Enabled, _reset.Enabled);
            return;
        }

        _logger.LogInformation("ResetScheduleWorker khởi động (nguồn: {Src}). Chu kỳ {Min} phút.",
            _source.GetType().Name, _api.PollIntervalMinutes);

        try
        {
            await PatientAccessService.EnsureDatabaseSchemaAsync(_conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kiểm tra/khởi tạo được schema DB (tiếp tục chạy).");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _api.PollIntervalMinutes)));
        try
        {
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Service đang dừng
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            var codes = await _source.GetPatientCodesAsync(ct);
            int scheduled = 0, skipped = 0;
            foreach (var code in codes)
            {
                if (await PatientAccessService.ScheduleResetFromApiAsync(code, _conn)) scheduled++;
                else skipped++; // không có trong DB hoặc hôm nay đã đặt lịch rồi
            }

            if (scheduled > 0)
                _logger.LogInformation("API reset: nhận {Total} mã, đặt lịch reset {Sch} mã, bỏ qua {Skip} (không có trong DB / hôm nay đã đặt lịch).",
                    codes.Count, scheduled, skipped);
            else
                _logger.LogDebug("API reset: nhận {Total} mã, không có mã mới cần đặt lịch.", codes.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy/đặt lịch danh sách reset từ API (sẽ thử lại ở chu kỳ sau).");
        }
    }
}
