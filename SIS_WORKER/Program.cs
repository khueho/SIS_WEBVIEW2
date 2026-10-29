using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SIS_WORKER;

// Content root = thư mục chứa exe (service mặc định chạy ở System32)
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Secrets cục bộ (không commit)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

builder.Services.AddWindowsService(o => o.ServiceName = "SIS_WORKER");

// Log ra file (xoay theo ngày, giữ 30 ngày) + console
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "sis_worker-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection("Portal"));
builder.Services.Configure<SisApiOptions>(builder.Configuration.GetSection("SisApi"));
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.Configure<ResetApiOptions>(builder.Configuration.GetSection("ResetApi"));

builder.Services.AddSingleton<PortalClient>();
builder.Services.AddSingleton<ActivationProcessor>();
builder.Services.AddSingleton<PasswordResetProcessor>();
builder.Services.AddHostedService<ActivationWorker>();
builder.Services.AddHostedService<PasswordResetWorker>();
// Nguồn danh sách reset: hiện là API giả lập; khi có API thật thì thay bằng class khác implement IResetRequestSource
builder.Services.AddSingleton<IResetRequestSource, MockResetRequestSource>();
builder.Services.AddHostedService<ResetScheduleWorker>();

await builder.Build().RunAsync();
