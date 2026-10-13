using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>
/// Vòng lặp nền thay Timer 2 phút của Form1: gọi API SIS lấy bệnh nhân → lưu DB → xử lý các ca PENDING.
/// </summary>
public sealed class ActivationWorker : BackgroundService
{
    private readonly WorkerOptions _opt;
    private readonly SisApiOptions _sis;
    private readonly ActivationProcessor _processor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ActivationWorker> _logger;
    private readonly string _conn = DatabaseConfigService.GetConnectionString();
    private readonly SisApiService _api;

    public ActivationWorker(
        IOptions<WorkerOptions> opt,
        IOptions<SisApiOptions> sis,
        ActivationProcessor processor,
        IHostApplicationLifetime lifetime,
        ILogger<ActivationWorker> logger)
    {
        _opt = opt.Value;
        _sis = sis.Value;
        _processor = processor;
        _lifetime = lifetime;
        _logger = logger;
        _api = new SisApiService
        {
            BaseUrl = _sis.BaseUrl,
            TenantId = _sis.TenantId,
            ClientId = _sis.ClientId,
            ClientSecret = _sis.ClientSecret,
            Scope = _sis.Scope
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SIS_WORKER khởi động. Chu kỳ {Sec}s, DryRun={Dry}, RunOnce={Once}, Limit={Limit}, PDF={Folder}",
            _opt.PollingIntervalSeconds, _opt.DryRun, _opt.RunOnce, _opt.PatientLimit, _opt.DownloadFolder);
        if (_opt.DryRun)
            _logger.LogWarning("⚠ Đang ở chế độ DRY-RUN: sẽ KHÔNG kích hoạt/ghi PDF/gửi email/đổi trạng thái DB.");

        try
        {
            await PatientAccessService.EnsureDatabaseSchemaAsync(_conn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không kiểm tra/khởi tạo được schema DB (tiếp tục chạy, sẽ thử lại ở chu kỳ sau).");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, _opt.PollingIntervalSeconds)));
        try
        {
            do
            {
                await RunCycleAsync(stoppingToken);
                if (_opt.RunOnce) break;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Service đang dừng
        }

        if (_opt.RunOnce)
        {
            _logger.LogInformation("RunOnce hoàn tất → thoát.");
            _lifetime.StopApplication();
        }
        else
        {
            _logger.LogInformation("SIS_WORKER đã dừng.");
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // 1) API SIS → DB
            if (_opt.FetchFromApi)
            {
                if (string.IsNullOrWhiteSpace(_sis.ClientSecret))
                {
                    _logger.LogWarning("Chưa cấu hình SisApi:ClientSecret → bỏ qua bước gọi API.");
                }
                else
                {
                    var raw = await _api.GetTodayDiagnosticImagingPatientsAsync();
                    var patients = _opt.PatientLimit > 0 ? raw.Take(_opt.PatientLimit).ToList() : raw;
                    var (total, added, updated) = await PatientAccessService.SaveApiPatientsAsync(patients, _conn);
                    _logger.LogInformation("API SIS: {Count} ca (mới +{New}, cập nhật {Upd}).", patients.Count, added, updated);
                }
            }

            // 2) Xử lý hàng đợi PENDING
            var pending = await PatientAccessService.GetPendingPatientsAsync(_conn);
            if (_opt.MaxPatientsPerCycle > 0 && pending.Count > _opt.MaxPatientsPerCycle)
            {
                _logger.LogWarning("Giới hạn MaxPatientsPerCycle={Max}: chỉ xử lý {Max}/{Total} ca PENDING.",
                    _opt.MaxPatientsPerCycle, _opt.MaxPatientsPerCycle, pending.Count);
                pending = pending.Take(_opt.MaxPatientsPerCycle).ToList();
            }
            if (pending.Count == 0)
            {
                _logger.LogInformation("Không có ca PENDING.");
                return;
            }

            _logger.LogInformation("Có {Count} ca PENDING cần xử lý.", pending.Count);
            var counts = new Dictionary<ProcessOutcome, int>();
            foreach (var item in pending)
            {
                ct.ThrowIfCancellationRequested();
                var outcome = await _processor.ProcessAsync(item, ct);
                counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
                await Task.Delay(_opt.DelayBetweenPatientsMs, ct);
            }

            _logger.LogInformation("Kết quả chu kỳ: {Summary}",
                string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong chu kỳ xử lý (sẽ thử lại ở chu kỳ sau).");
        }
    }
}
