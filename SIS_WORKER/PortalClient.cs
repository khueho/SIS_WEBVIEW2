using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIS_WORKER;

/// <summary>Phiên đăng nhập hết hạn / token p_auth không còn hợp lệ → cần login lại.</summary>
public class PortalSessionExpiredException : Exception
{
    public PortalSessionExpiredException(string message) : base(message) { }
}

/// <summary>Portal trả success=false hoặc cấu trúc không như mong đợi.</summary>
public class PortalApiException : Exception
{
    public PortalApiException(string message) : base(message) { }
}

/// <summary>Kết quả tra cứu bệnh nhân (operation=queryUsers).</summary>
public sealed record PortalPatient(string Code, bool IsUser, string FullName, JsonNode PatientId, string? UserId = null);

/// <summary>Kết quả kích hoạt (operation=saveUser): file PDF thông tin truy cập.</summary>
public sealed record ActivationResult(byte[] PdfBytes, string FileName);

/// <summary>
/// Thay thế toàn bộ thao tác UI WebView2 bằng HTTP trực tiếp tới portal Liferay + ExtJS.
/// Luồng (bắt từ HAR): login (2 request) → lấy p_auth/INSTANCE → queryUsers → resolveFullPatient
/// → isEmailUnique → saveUser (trả PDF base64).
/// </summary>
public sealed class PortalClient : IDisposable
{
    private const string PortletName = "PatientOnBoardingPortlet_WAR_PatientOnBoardingPortlet";

    private readonly PortalOptions _opt;
    private readonly ILogger<PortalClient> _logger;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;

    private string? _pAuth;
    private string? _instance;
    private string? _tabId;

    public PortalClient(IOptions<PortalOptions> options, ILogger<PortalClient> logger)
    {
        _opt = options.Value;
        _logger = logger;

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };
        if (_opt.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri(_opt.BaseUrl.TrimEnd('/') + "/");
    }

    // ───────────────────────── Đăng nhập & token ─────────────────────────

    public async Task LoginAsync(CancellationToken ct)
    {
        _logger.LogInformation("Đăng nhập portal với user {User}...", _opt.Username);

        // Xoá cookie cũ
        foreach (Cookie c in _cookies.GetAllCookies()) c.Expired = true;
        _pAuth = _instance = _tabId = null;

        // Bước 0: lấy cookie khởi tạo (JSESSIONID, GUEST_LANGUAGE_ID...)
        using (var home = await _http.GetAsync("vi/home", ct)) { }

        // Bước 1: LoginServlet (multipart)
        using (var mp = new MultipartFormDataContent("----WebKitFormBoundary" + Guid.NewGuid().ToString("N")[..16]))
        {
            mp.Add(PlainPart(_opt.Username), "user");
            mp.Add(PlainPart(_opt.Password), "password");
            mp.Add(PlainPart(_opt.Fingerprint), "fingerprint");

            using var req = new HttpRequestMessage(HttpMethod.Post, "delegate/LoginServlet?operation=authenticateUser") { Content = mp };
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Headers.Accept.ParseAdd("application/json, text/plain, */*");

            using var resp = await _http.SendAsync(req, ct);
            string text = await resp.Content.ReadAsStringAsync(ct);
            JsonNode? json = TryParse(text);
            if (json?["success"]?.GetValue<bool>() != true)
            {
                throw new PortalApiException($"LoginServlet thất bại: {Truncate(text)}");
            }
        }

        // Bước 2: Liferay login form
        string loginUrl =
            "vi/web/guest/home?p_p_id=com_liferay_login_web_portlet_LoginPortlet&p_p_lifecycle=1&p_p_state=normal&p_p_mode=view" +
            "&_com_liferay_login_web_portlet_LoginPortlet_javax.portlet.action=%2Flogin%2Flogin" +
            "&_com_liferay_login_web_portlet_LoginPortlet_mvcRenderCommandName=%2Flogin%2Flogin";
        const string P = "_com_liferay_login_web_portlet_LoginPortlet_";
        var form = new Dictionary<string, string>
        {
            [P + "formDate"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            [P + "saveLastPath"] = "false",
            [P + "redirect"] = "",
            [P + "doActionAfterLogin"] = "false",
            [P + "login"] = _opt.Username,
            [P + "password"] = _opt.Password,
            [P + "otp"] = "",
            [P + "device-fingerprint"] = _opt.Fingerprint,
            [P + "trust-device"] = "false",
            [P + "company"] = "",
            [P + "companyId"] = ""
        };

        string html;
        Uri? finalUri;
        using (var resp = await _http.PostAsync(loginUrl, FormContent(form), ct))
        {
            finalUri = resp.RequestMessage?.RequestUri;
            html = await resp.Content.ReadAsStringAsync(ct);
        }

        // Nếu chưa tới trang create-patient-user thì tự vào (dùng TabId trong config)
        if (finalUri == null || !finalUri.AbsolutePath.Contains("create-patient-user", StringComparison.OrdinalIgnoreCase))
        {
            string tab = string.IsNullOrWhiteSpace(_opt.TabId) ? "" : $"?tabId={_opt.TabId}";
            using var resp = await _http.GetAsync("group/sense-portal/create-patient-user" + tab, ct);
            finalUri = resp.RequestMessage?.RequestUri;
            html = await resp.Content.ReadAsStringAsync(ct);
        }

        // Lấy p_auth, INSTANCE id, tabId
        var mAuth = Regex.Match(html, @"p_auth=([A-Za-z0-9_\-]+)");
        var mInst = Regex.Match(html, PortletName + @"_INSTANCE_([A-Za-z0-9]+)");
        if (!mAuth.Success || !mInst.Success)
        {
            throw new PortalApiException("Không lấy được p_auth/INSTANCE sau đăng nhập (sai tài khoản hoặc portal đổi cấu trúc).");
        }

        _pAuth = mAuth.Groups[1].Value;
        _instance = mInst.Groups[1].Value;

        var mTab = Regex.Match(finalUri?.Query ?? "", @"tabId=([0-9a-fA-F\-]+)");
        _tabId = mTab.Success ? mTab.Groups[1].Value : _opt.TabId;
        if (string.IsNullOrWhiteSpace(_tabId))
        {
            var mTabHtml = Regex.Match(html, @"_INSTANCE_" + _instance + @"_tabId=([0-9a-fA-F\-]+)");
            if (mTabHtml.Success) _tabId = mTabHtml.Groups[1].Value;
        }

        _logger.LogInformation("Đăng nhập portal thành công (instance {Instance}).", _instance);
    }

    // ───────────────────────── API nghiệp vụ ─────────────────────────

    /// <summary>Tra cứu bệnh nhân theo PatientID (operation=queryUsers). Trả null nếu không có kết quả.</summary>
    public Task<PortalPatient?> FindPatientAsync(string patientCode, CancellationToken ct) =>
        WithSessionAsync(async () =>
        {
            string filter = new JsonObject
            {
                ["localPatientsOnly"] = null,
                ["zpiSearch"] = null,
                ["firstName"] = null,
                ["lastName"] = null,
                ["motherMaidenName"] = null,
                ["gender"] = null,
                ["dateOfBirth"] = null,
                ["email"] = null,
                ["institution"] = "",
                ["id"] = patientCode
            }.ToJsonString();

            string url = PortletUrl() +
                         $"&_dc={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" +
                         $"&filter={Uri.EscapeDataString(filter)}&operation=queryUsers&page=1&start=0&limit=15";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            JsonNode root = await SendJsonAsync(req, ct);

            var rows = root["data"]?[0]?["pageResult"]?.AsArray();
            if (rows == null || rows.Count == 0) return null;

            JsonNode? hit = rows.FirstOrDefault(r =>
                                string.Equals(r?["ssn"]?.GetValue<string>(), patientCode, StringComparison.OrdinalIgnoreCase))
                            ?? rows[0];
            if (hit == null) return null;

            string full = $"{hit["lastName"]?.GetValue<string>()} {hit["firstName"]?.GetValue<string>()}".Trim();
            bool isUser = hit["user"]?.GetValue<bool>() ?? false;
            JsonNode? pid = hit["patientId"]?.DeepClone();
            if (pid == null) throw new PortalApiException("queryUsers không trả patientId.");

            string? userId = hit["userId"]?.ToString();

            return new PortalPatient(patientCode, isUser, full, pid, userId);
        }, ct);

    /// <summary>
    /// Lấy thông tin đầy đủ (operation=resolveFullPatient) → node "patient".
    /// forExistingUser=true: bệnh nhân đã là user (luồng reset mật khẩu) → gửi kèm userId, prepareForUpgrade=false như UI.
    /// </summary>
    public Task<JsonNode> ResolveFullPatientAsync(PortalPatient p, CancellationToken ct, bool forExistingUser = false) =>
        WithSessionAsync(async () =>
        {
            var fields = new Dictionary<string, string>
            {
                ["userId"] = forExistingUser ? (p.UserId ?? "") : "",
                ["patientId"] = p.PatientId.ToJsonString(),
                ["forceReload"] = "true",
                ["generateIdentifier"] = "false",
                ["prepareForUpgrade"] = forExistingUser ? "false" : "",
                ["operation"] = "resolveFullPatient",
                ["componentInstanceID"] = ""
            };
            JsonNode root = await PostPortletAsync(fields, ct);
            return root["data"]?[0]?["patient"] ?? throw new PortalApiException("resolveFullPatient không trả patient.");
        }, ct);

    /// <summary>Kiểm tra email có dùng được không (operation=isEmailUnique).</summary>
    public Task<bool> IsEmailUniqueAsync(JsonNode patient, string patientCode, string email, CancellationToken ct) =>
        WithSessionAsync(async () =>
        {
            var fields = new Dictionary<string, string>
            {
                ["patient"] = BuildUserJson(patient, patientCode, email, forSave: false).ToJsonString(),
                ["userId"] = "",
                ["operation"] = "isEmailUnique",
                ["componentInstanceID"] = ""
            };
            JsonNode root = await PostPortletAsync(fields, ct);
            return root["data"]?[0]?.GetValue<bool>() ?? false;
        }, ct);

    /// <summary>Kích hoạt tài khoản (operation=saveUser). Portal trả luôn PDF thông tin truy cập (base64).</summary>
    public Task<ActivationResult> ActivateAsync(JsonNode patient, string patientCode, string email, CancellationToken ct) =>
        WithSessionAsync(async () =>
        {
            string svnr = patient["svnr"]?.GetValue<string>() ?? patientCode;
            var fields = new Dictionary<string, string>
            {
                ["user"] = BuildUserJson(patient, patientCode, email, forSave: true).ToJsonString(),
                ["updatePatient"] = "true",
                ["notificationData"] = "{\"notificationData\":null}",
                ["userLanguage"] = "en-US",
                ["username"] = svnr,
                ["triggerPatientSelfOnboarding"] = "false",
                ["operation"] = "saveUser",
                ["componentInstanceID"] = ""
            };
            JsonNode root = await PostPortletAsync(fields, ct);
            return ParsePdfResult(root, patientCode, "saveUser");
        }, ct);

    /// <summary>
    /// Nhấn nút "Tạo mật khẩu mới" bước 1 (operation=generatePassword). Portal sinh mật khẩu và (nhiều khả năng) giữ theo phiên
    /// để bước update dùng → PHẢI gọi ngay trước ResetPasswordAsync, trong cùng phiên (PortalClient riêng của luồng reset).
    /// </summary>
    public Task GeneratePasswordAsync(CancellationToken ct) =>
        WithSessionAsync(async () =>
        {
            var fields = new Dictionary<string, string>
            {
                ["operation"] = "generatePassword",
                ["componentInstanceID"] = ""
            };
            await PostPortletAsync(fields, ct);
            return true;
        }, ct);

    /// <summary>
    /// Reset mật khẩu bệnh nhân đã active (operation=update, passwordResetRequired=true). Portal tự sinh mật khẩu mới
    /// và trả PDF thông tin truy cập (giống lúc kích hoạt). Giữ nguyên email hiện có của user.
    /// </summary>
    public Task<ActivationResult> ResetPasswordAsync(PortalPatient p, JsonNode patient, string patientCode, CancellationToken ct) =>
        WithSessionAsync(async () =>
        {
            string svnr = patient["svnr"]?.GetValue<string>() ?? patientCode;
            string email = patient["email"]?.ToString()
                           ?? patient["telecomAddresses"]?[0]?["value"]?.ToString()
                           ?? $"{patientCode}@gmail.com";

            JsonObject user = BuildUserJson(patient, patientCode, email, forSave: true);
            // UI gửi birthdate dạng ISO đầy đủ (yyyy-MM-ddT00:00:00.000Z) khi update
            string? bd = user["birthdate"]?.ToString();
            if (bd is { Length: 10 }) user["birthdate"] = bd + "T00:00:00.000Z";

            var fields = new Dictionary<string, string>
            {
                ["patient"] = user.ToJsonString(),
                ["passwordResetRequired"] = "true",
                ["userId"] = p.UserId ?? "",
                ["userLanguage"] = "en-US",
                ["username"] = svnr,
                ["triggerPatientSelfOnboarding"] = "false",
                ["operation"] = "update",
                ["componentInstanceID"] = ""
            };
            JsonNode root = await PostPortletAsync(fields, ct);
            return ParsePdfResult(root, patientCode, "update");
        }, ct);

    /// <summary>Tách PDF base64 (data[0].content + fileName) từ response saveUser / update.</summary>
    private static ActivationResult ParsePdfResult(JsonNode root, string patientCode, string operation)
    {
        string? b64 = root["data"]?[0]?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(b64)) throw new PortalApiException($"{operation} không trả PDF (content rỗng).");

        string fileName = root["data"]?[0]?["fileName"]?.GetValue<string>() ?? $"Access_Information_{patientCode}.pdf";
        return new ActivationResult(Convert.FromBase64String(b64), fileName);
    }

    // ───────────────────────── Helper nội bộ ─────────────────────────

    /// <summary>
    /// Dựng JSON "user" y hệt UI ExtJS gửi lên. Lưu ý: UI có các key "0".."7" = từng ký tự của svnr
    /// (do JS spread chuỗi) — giữ nguyên để portal chấp nhận như thao tác thật.
    /// </summary>
    private static JsonObject BuildUserJson(JsonNode patient, string patientCode, string email, bool forSave)
    {
        string svnr = patient["svnr"]?.GetValue<string>() ?? patientCode;
        JsonNode? name = patient["name"] ?? patient["names"]?[0];

        JsonObject NameObj() => new()
        {
            ["givenName"] = name?["givenName"]?.GetValue<string>() ?? "",
            ["familyName"] = name?["familyName"]?.GetValue<string>() ?? "",
            ["motherMaidenName"] = "",
            ["prefix"] = "",
            ["suffix"] = ""
        };

        var obj = new JsonObject();
        for (int i = 0; i < svnr.Length; i++) obj[i.ToString()] = svnr[i].ToString();

        obj["names"] = new JsonArray(NameObj());
        obj["name"] = NameObj();
        obj["gender"] = patient["gender"]?.GetValue<string>();
        obj["birthdate"] = patient["birthdate"]?.GetValue<string>();
        obj["nationality"] = null;
        obj["citizenships"] = new JsonArray((JsonNode?)null);
        obj["language"] = null;
        obj["religion"] = "";
        obj["profileImage"] = null;
        obj["userLanguage"] = "en-US";
        obj["birthplace"] = "";
        obj["birthcountry"] = null;
        obj["username"] = svnr;
        obj["addresses"] = new JsonArray();
        obj["telecomAddresses"] = new JsonArray(new JsonObject
        {
            ["value"] = email,
            ["type"] = "Internet",
            ["typeOfUse"] = "PRN"
        });
        obj["email"] = email;

        JsonNode? pidNode = patient["patientID"];
        if (forSave)
        {
            obj["identifiers"] = patient["identifiers"]?.DeepClone() ?? new JsonArray();
            obj["svnr"] = svnr;
            obj["patientID"] = pidNode?.DeepClone();
        }
        else
        {
            obj["identifiers"] = new JsonArray(new JsonObject
            {
                ["idNumber"] = pidNode?["idNumber"]?.GetValue<string>()
            });
            obj["svnr"] = svnr;
        }

        return obj;
    }

    private string PortletUrl()
    {
        if (_pAuth == null || _instance == null) throw new PortalSessionExpiredException("Chưa đăng nhập.");
        string inst = $"{PortletName}_INSTANCE_{_instance}";
        return "group/sense-portal/create-patient-user" +
               $"?p_p_id={inst}&p_p_lifecycle=2&p_p_state=normal&p_p_mode=view&p_p_cacheability=cacheLevelPage" +
               $"&p_auth={_pAuth}&_{inst}_tabId={_tabId}";
    }

    private async Task<JsonNode> PostPortletAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, PortletUrl()) { Content = FormContent(fields) };
        return await SendJsonAsync(req, ct);
    }

    private async Task<JsonNode> SendJsonAsync(HttpRequestMessage req, CancellationToken ct)
    {
        req.Headers.Add("X-Requested-With", "XMLHttpRequest");
        req.Headers.Accept.ParseAdd("*/*");

        using var resp = await _http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new PortalSessionExpiredException($"HTTP {(int)resp.StatusCode}");

        JsonNode? json = TryParse(text);
        if (json == null)
            throw new PortalSessionExpiredException("Portal trả HTML thay vì JSON (phiên hết hạn?).");

        if (!resp.IsSuccessStatusCode)
            throw new PortalApiException($"HTTP {(int)resp.StatusCode}: {Truncate(text)}");

        if (json["success"]?.GetValue<bool>() != true)
            throw new PortalApiException($"Portal báo lỗi: {Truncate(text)}");

        return json;
    }

    /// <summary>Chạy action; nếu phiên hết hạn thì đăng nhập lại và thử lại đúng 1 lần.</summary>
    private async Task<T> WithSessionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (_pAuth == null) await LoginAsync(ct);
        try
        {
            return await action();
        }
        catch (PortalSessionExpiredException ex)
        {
            _logger.LogWarning("Phiên portal hết hạn ({Reason}) → đăng nhập lại.", ex.Message);
            await LoginAsync(ct);
            return await action();
        }
    }

    private static JsonNode? TryParse(string text)
    {
        string t = text.TrimStart();
        if (!t.StartsWith('{') && !t.StartsWith('[')) return null;
        try { return JsonNode.Parse(t); }
        catch { return null; }
    }

    private static HttpContent PlainPart(string value)
    {
        var c = new StringContent(value);
        c.Headers.ContentType = null; // Chrome không gửi Content-Type cho từng phần form-data
        return c;
    }

    private static FormUrlEncodedContent FormContent(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var c = new FormUrlEncodedContent(fields);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") { CharSet = "UTF-8" };
        return c;
    }

    private static string Truncate(string s, int max = 300) => s.Length <= max ? s : s[..max] + "...";

    public void Dispose() => _http.Dispose();
}
