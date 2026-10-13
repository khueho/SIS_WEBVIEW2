namespace SIS_WORKER;

public class WorkerOptions
{
    /// <summary>Chu kỳ quét API + xử lý hàng đợi (giây). Mặc định 2 phút như app WinForms.</summary>
    public int PollingIntervalSeconds { get; set; } = 120;

    /// <summary>Số bệnh nhân tối đa lấy từ API mỗi lần (&lt;= 0: lấy toàn bộ).</summary>
    public int PatientLimit { get; set; } = 0;

    /// <summary>Số ca PENDING tối đa xử lý mỗi chu kỳ (&lt;= 0: không giới hạn). Dùng khi test thật.</summary>
    public int MaxPatientsPerCycle { get; set; } = 0;

    /// <summary>true: chỉ tra cứu portal, KHÔNG gọi saveUser, KHÔNG ghi PDF/DB trạng thái/email.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>true: chạy đúng 1 chu kỳ rồi thoát (dùng khi test bằng console).</summary>
    public bool RunOnce { get; set; } = false;

    /// <summary>true: gọi API SIS lấy danh sách bệnh nhân mỗi chu kỳ.</summary>
    public bool FetchFromApi { get; set; } = true;

    public string DownloadFolder { get; set; } = @"D:\SIS_SIEMENS_PDF";

    public int DelayBetweenPatientsMs { get; set; } = 500;
}

public class PortalOptions
{
    public string BaseUrl { get; set; } = "https://portal.sisvietnam.vn";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string TabId { get; set; } = "";
    public string Fingerprint { get; set; } = "2aecfcf843753a6bbaeed33e6927b9ac";
    public bool IgnoreSslErrors { get; set; } = true;
}

public class SisApiOptions
{
    public string BaseUrl { get; set; } = "https://dhs.sisvietnam.vn";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "HospitalIntegration";
}
