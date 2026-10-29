using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>
/// Vòng lặp nền reset mật khẩu mặc định: mỗi PollIntervalMinutes phút nhặt các ca có ResetDueAt đã đến hạn
/// (được đặt lúc kích hoạt, hoặc bởi luồng API sau này) và reset tuần tự. Reset xong thì xoá lịch → chỉ reset 1 lần.
/// </summary>
public sealed class PasswordResetWorker : BackgroundService
{
    private readonly PasswordResetOptions _reset;
    private readonly WorkerOptions _opt;
    private readonly PasswordResetProcessor _processor;
    private readonly ILogger<PasswordResetWorker> _logger;
    private readonly string _conn = DatabaseConfigService.GetConnectionString();

    public PasswordResetWorker(
        IOptions<PasswordResetOptions> reset,
        IOptions<WorkerOptions> opt,
        PasswordResetProcessor processor,
        ILogger<PasswordResetWorker> logger)
    {
        _reset = reset.Value;
        _opt = opt.Value;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_reset.Enabled)
        {
            _logger.LogInformation("PasswordReset:Enabled=false → worker reset mật khẩu không chạy.");
            return;
        }

        _logger.LogInformation(
            "PasswordResetWorker khởi động. Chu kỳ {Min} phút, tối đa {Max} ca/lần (<=0: không giới hạn), thử lại tối đa {Attempts} lần, DryRun={Dry}",
            _reset.PollIntervalMinutes, _reset.MaxPerCycle, _reset.MaxAttempts, _opt.DryRun);

        try
        {
            await PatientAccessService.EnsureDatabaseSchemaAsync(_conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kiểm tra/khởi tạo được schema DB (tiếp tục chạy, sẽ thử lại ở chu kỳ sau).");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _reset.PollIntervalMinutes)));
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

        _logger.LogInformation("PasswordResetWorker đã dừng.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            int limit = _reset.MaxPerCycle; // <= 0: không giới hạn số ca mỗi chu kỳ
            const int BatchSize = 20;

            if (_opt.DryRun)
            {
                var preview = await PatientAccessService.PreviewDueResetsAsync(limit > 0 ? limit : int.MaxValue, _conn);
                _logger.LogInformation("[DRY-RUN] Có {Count} ca đến hạn reset mật khẩu: {Codes}",
                    preview.Count, string.Join(", ", preview.Select(p => p.PatientCode)));
                return;
            }

            // Nhặt theo lô nhỏ (khoá 15 phút/lô) cho đến khi hết ca đến hạn, tránh khoá cả đống ca xếp hàng quá lâu.
            // Ca vừa lỗi trong chu kỳ này sẽ không bị xử lý lại ngay (tránh đốt hết số lần thử).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int ok = 0, fail = 0;
            while (limit <= 0 || ok + fail < limit)
            {
                int take = limit > 0 ? Math.Min(BatchSize, limit - (ok + fail)) : BatchSize;
                var due = await PatientAccessService.ClaimDueResetsAsync(take, _conn);
                var fresh = due.Where(d => seen.Add(d.PatientCode)).ToList();
                if (fresh.Count == 0) break;

                if (ok + fail == 0)
                    _logger.LogInformation("Có ca đến hạn reset mật khẩu (lô đầu {Count} ca).", fresh.Count);

                foreach (var item in fresh)
                {
                    ct.ThrowIfCancellationRequested();
                    if (await _processor.ProcessAsync(item, ct)) ok++; else fail++;
                    await Task.Delay(_opt.DelayBetweenPatientsMs, ct);
                }
            }

            if (ok + fail == 0)
                _logger.LogDebug("Không có ca đến hạn reset mật khẩu.");
            else
                _logger.LogInformation("Kết quả reset mật khẩu: thành công={Ok}, lỗi={Fail}", ok, fail);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong chu kỳ reset mật khẩu (sẽ thử lại ở chu kỳ sau).");
        }
    }
}
