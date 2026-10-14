# SIS_WORKER — Tự động kích hoạt tài khoản bệnh nhân (Windows Service)

**Dự án:** .NET 8 Worker Service (chạy ngầm, không giao diện).
**Mục tiêu:** Tự động quét danh sách bệnh nhân chẩn đoán hình ảnh trong ngày từ API SIS, kích hoạt tài khoản
trên `https://portal.sisvietnam.vn` bằng HTTP trực tiếp, lưu PDF thông tin truy cập, ghi DB và gửi email cho bệnh nhân.

> 🔙 **Quay lại phiên bản WebView2/WinForms (tự động hoá bằng trình duyệt):**
> ```powershell
> git checkout webview2-final      # tag trỏ vào commit cuối cùng còn bản WebView2
> ```
> Từ commit này trở đi, project WebView2 đã bị xoá để gọn nhẹ (chỉ còn 1 project `SIS_WORKER`).

---

## Luồng xử lý

```
Mỗi 2 phút (PollingIntervalSeconds):
  1. API SIS (dhs.sisvietnam.vn)  → lấy bệnh nhân CĐHA hôm nay → lưu DB (PENDING)
  2. Lấy các ca PENDING từ DB, với mỗi ca:
       a. Portal: queryUsers theo PatientID
            - không có kết quả   → NOT_FOUND
            - user=true          → ALREADY_ACTIVE
       b. resolveFullPatient → isEmailUnique → saveUser (trả PDF base64)
            (email thật nếu có, nếu không dùng {PatientID}@gmail.com)
       c. Ghi PDF vào D:\SIS_SIEMENS_PDF\[PatientID]_[FileName].pdf
       d. Đọc PDF (PdfPig) → lưu PatientAccessInfo → trạng thái ACTIVATED
       e. Gửi email thông tin tài khoản (MailKit)
  Lỗi bất kỳ ở 1 ca → FAILED, tiếp tục ca sau. Hết phiên portal → tự đăng nhập lại.
```

Các request portal được dựng lại từ HAR (Liferay + ExtJS): login 2 bước → lấy `p_auth` + `INSTANCE` từ HTML →
các operation `queryUsers`, `resolveFullPatient`, `isEmailUnique`, `saveUser`.

## Cấu trúc

```
SIS_WORKER.sln
└── SIS_WORKER/
    ├── Program.cs              ← Host: Windows Service, Serilog (log file xoay ngày), bind config
    ├── ActivationWorker.cs     ← BackgroundService: vòng lặp 2 phút (API → DB → hàng đợi)
    ├── ActivationProcessor.cs  ← Xử lý 1 bệnh nhân (tra cứu → kích hoạt → PDF → DB → email)
    ├── PortalClient.cs         ← HTTP client cho portal (login, queryUsers, saveUser, relogin)
    ├── Options.cs              ← WorkerOptions / PortalOptions / SisApiOptions
    ├── PatientAccessService.cs ← Đọc PDF, truy vấn PostgreSQL, trạng thái
    ├── SisApiService.cs        ← OAuth2 + API lấy bệnh nhân CĐHA trong ngày
    ├── EmailService.cs         ← Gửi email (MailKit), cấu hình trong email_config.json
    ├── DatabaseConfig.cs       ← Đọc db_config.json
    ├── appsettings.json        ← Cấu hình mặc định (DryRun=true)
    ├── appsettings.local.json  ← Secret cục bộ (gitignored): mật khẩu portal, client secret
    ├── db_config.json / email_config.json
scripts/
    ├── install-service.ps1 / uninstall-service.ps1
```

## Cấu hình (`appsettings.json`, ghi đè bằng `appsettings.local.json`)

| Khoá | Ý nghĩa |
|---|---|
| `Worker:DryRun` | `true` (mặc định): chỉ tra cứu portal, **không** kích hoạt/ghi PDF/gửi email/đổi trạng thái DB |
| `Worker:PollingIntervalSeconds` | Chu kỳ quét (mặc định 120) |
| `Worker:PatientLimit` | Giới hạn số ca lấy từ API (0 = toàn bộ) |
| `Worker:MaxPatientsPerCycle` | Giới hạn số ca PENDING xử lý mỗi chu kỳ (0 = không giới hạn) |
| `Worker:RunOnce` | Chạy 1 chu kỳ rồi thoát (test console) |
| `Worker:FetchFromApi` | Có gọi API SIS mỗi chu kỳ không |
| `Worker:DownloadFolder` | Thư mục lưu PDF (`D:\SIS_SIEMENS_PDF`) |
| `Portal:*` | `BaseUrl`, `Username`, `Password`, `TabId`, `Fingerprint`, `IgnoreSslErrors` |
| `SisApi:*` | `BaseUrl`, `TenantId`, `ClientId`, `ClientSecret`, `Scope` |

Mọi khoá đều ghi đè được bằng biến môi trường dạng `Worker__DryRun=false`.
`email_config.json` có `OverrideRecipientEmail`: nếu có giá trị, **mọi email gửi về hòm thư test** thay vì bệnh nhân — xoá khi chạy thật.

## Chạy & cài đặt

```powershell
# Test console: 1 chu kỳ, chỉ tra cứu (an toàn)
$env:Worker__RunOnce='true'; dotnet run --project SIS_WORKER

# Test kích hoạt thật đúng 1 ca
$env:Worker__RunOnce='true'; $env:Worker__DryRun='false'; $env:Worker__MaxPatientsPerCycle='1'; dotnet run --project SIS_WORKER

# Cài Windows Service (PowerShell Admin; nhớ đặt Worker:DryRun=false trong appsettings.local.json)
.\scripts\install-service.ps1          # publish ra D:\SIS_WORKER + tạo service tự khởi động
.\scripts\uninstall-service.ps1
```
Log: `<thư mục cài>\logs\sis_worker-YYYYMMDD.log` (giữ 30 ngày).

> 📖 **Xem tài liệu chi tiết:**
> - [Báo cáo đánh giá năng lực & hiệu năng xử lý (.md)](file:///d:/repo/SIS/SIS_WEBVIEW2/BAO_CAO_HIEU_NANG_XU_LY.md) | [Bản Word (.docx)](file:///d:/repo/SIS/SIS_WEBVIEW2/BAO_CAO_HIEU_NANG_XU_LY.docx)
> - [Hướng dẫn chạy thực tế 24/7](file:///d:/repo/SIS/SIS_WEBVIEW2/HUONG_DAN_VAN_HANH_24_7.md)
> - [Giải thích chi tiết cấu hình appsettings.json](file:///d:/repo/SIS/SIS_WEBVIEW2/CAU_HINH_APPSETTINGS.md)

## Lưu ý vận hành
- Portal đổi API/phiên bản → service sẽ ghi lỗi `Portal báo lỗi...` / `Không lấy được p_auth...` trong log.
- `saveUser` có payload với các key `"0".."7"` (ký tự của PatientID) — do UI ExtJS gửi như vậy, được giữ nguyên.
- Thông tin truy cập portal & SMTP đang là secret dạng plain text trong file config; không commit `appsettings.local.json`.

## Trạng thái tiến độ (cập nhật: 2026-10-13)

- [x] Chuyển từ WebView2/WinForms sang Worker Service gọi HTTP (tag `webview2-final` giữ bản cũ)
- [x] Test DryRun + test kích hoạt thật 1 ca (`saveUser` → PDF → DB → email test) thành công
- [x] Script cài/gỡ Windows Service, log file xoay theo ngày
- [x] PostgreSQL (Npgsql), trạng thái `PENDING/ACTIVATED/ALREADY_ACTIVE/FAILED/NOT_FOUND`, theo dõi gửi email
- [ ] Cài service thật trên máy chủ và chạy với `DryRun=false` (xoá `OverrideRecipientEmail`)
- [ ] Xuất báo cáo CSV sau mỗi chu kỳ (tuỳ chọn)
