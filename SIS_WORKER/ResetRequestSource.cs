using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>Nguồn trả về danh sách mã bệnh nhân cần reset mật khẩu (sau này thay bằng API thật).</summary>
public interface IResetRequestSource
{
    Task<IReadOnlyList<string>> GetPatientCodesAsync(CancellationToken ct);
}

/// <summary>API giả lập: trả về danh sách cố định lấy từ cấu hình ResetApi:MockPatientCodes.</summary>
public sealed class MockResetRequestSource : IResetRequestSource
{
    private readonly ResetApiOptions _opt;

    public MockResetRequestSource(IOptions<ResetApiOptions> opt) => _opt = opt.Value;

    public Task<IReadOnlyList<string>> GetPatientCodesAsync(CancellationToken ct)
    {
        IReadOnlyList<string> list = _opt.MockPatientCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(list);
    }
}
