# Context & Rules cho Agent — SIS_WEBVIEW2 (SIS_WORKER)

> File này được tự động load bởi Antigravity IDE mỗi phiên làm việc.
> Đọc README.md để có thông tin đầy đủ hơn.

## Dự Án Là Gì

**SIS_WORKER** — .NET 8 Worker Service (Windows Service), tự động kích hoạt tài khoản bệnh nhân
trên `https://portal.sisvietnam.vn` bằng **HTTP trực tiếp** (không còn WebView2/WinForms).

Mỗi 2 phút: gọi API SIS lấy bệnh nhân CĐHA hôm nay → lưu DB (PENDING) → với mỗi ca PENDING:
queryUsers → (chưa active) resolveFullPatient → isEmailUnique → saveUser (trả PDF base64)
→ lưu `D:\SIS_SIEMENS_PDF` → đọc PDF lưu DB → gửi email.

> Bản WebView2/WinForms cũ nằm ở branch **`legacy-webview2`** hoặc tag **`webview2-final`** (`git checkout legacy-webview2`).

---

## Cấu Trúc File Quan Trọng

| File | Vai trò |
|---|---|
| `SIS_WORKER/Program.cs` | Host, Windows Service, Serilog, bind config |
| `SIS_WORKER/ActivationWorker.cs` | BackgroundService: vòng lặp API → DB → hàng đợi |
| `SIS_WORKER/ActivationProcessor.cs` | Xử lý 1 bệnh nhân (tra cứu → kích hoạt → PDF → DB → email) |
| `SIS_WORKER/PortalClient.cs` | HTTP client portal Liferay/ExtJS (login, token, queryUsers, saveUser) |
| `SIS_WORKER/Options.cs` | WorkerOptions / PortalOptions / SisApiOptions |
| `SIS_WORKER/PatientAccessService.cs` | Đọc PDF, truy vấn PostgreSQL |
| `SIS_WORKER/appsettings.json` | Cấu hình mặc định (`DryRun=true`); secret ở `appsettings.local.json` (gitignored) |

---

## Điểm Cần Chú Ý Ngay

1. **API portal có thể đổi** — nếu log báo `Không lấy được p_auth/INSTANCE` hoặc `Portal báo lỗi`,
   bắt lại HAR từ Chrome DevTools (Network → Save all as HAR) rồi đối chiếu với `PortalClient.cs`.
   Không commit file HAR (chứa mật khẩu/cookie).
2. **Luôn thử `Worker:DryRun=true` trước**; test kích hoạt thật với `Worker:MaxPatientsPerCycle=1`.
3. `email_config.json` có `OverrideRecipientEmail` (hòm thư test) — xoá khi chạy thật.
4. Không chạy đồng thời 2 bản worker (xử lý trùng ca).

---

## Quy Tắc Khi Làm Việc

- Luôn đọc `README.md` để nắm tiến độ mới nhất trước khi bắt đầu
- Sau mỗi thay đổi quan trọng, cập nhật mục **Trạng Thái Tiến Độ** trong `README.md`
- Không xóa comments tiếng Việt trong code — chúng là context quan trọng
- Khi tạo các file test hay script debug, tạo trực tiếp trong thư mục project (workspace) để dễ dàng theo dõi và quản lý, tránh tạo ở các thư mục tạm bên ngoài. Xóa bỏ hoặc dọn dẹp các file tạm này khi kết thúc thử nghiệm.
