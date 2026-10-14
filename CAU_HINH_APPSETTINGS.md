# Tài Liệu Cấu Hình SIS_WORKER (`appsettings.json`)

Tài liệu này giải thích chi tiết ý nghĩa, vai trò và tính chất cố định/thay đổi của từng tham số cấu hình trong file `appsettings.json` (và file ghi đè bí mật `appsettings.local.json`).

---

## 1. Nhóm cấu hình `Worker` (Tiến trình ngầm)

| Tham số | Giá trị mặc định | Giải thích chi tiết |
| :--- | :--- | :--- |
| `PollingIntervalSeconds` | `120` | Chu kỳ quét định kỳ tính theo **giây** (120s = 2 phút). Cứ sau khoảng thời gian này, worker sẽ kích hoạt chu kỳ: gọi API SIS lấy danh sách mới và xử lý hàng đợi các ca `PENDING`. |
| `PatientLimit` | `0` | Giới hạn số lượng bệnh nhân lấy từ API SIS mỗi lần quét. Đặt `<= 0` nghĩa là **lấy toàn bộ danh sách** trong ngày. |
| `MaxPatientsPerCycle` | `0` | Giới hạn số lượng ca `PENDING` được xử lý kích hoạt trong 1 chu kỳ. Đặt `<= 0` nghĩa là **xử lý hết tất cả các ca đang chờ**. (Dùng khi test: ví dụ đặt bằng `1` để chỉ kích hoạt đúng 1 ca). |
| `DryRun` | `true` | **Chế độ chạy thử / An toàn:**<br>- `true`: Chỉ gọi API và tra cứu portal, **tuyệt đối KHÔNG** bấm kích hoạt (`saveUser`), không tạo PDF, không đổi trạng thái DB và không gửi email.<br>- `false`: Chạy thật, kích hoạt tài khoản thật trên portal. |
| `RunOnce` | `false` | - `true`: Chạy đúng 1 chu kỳ rồi tự động tắt tiến trình (dùng khi chạy test bằng lệnh console `dotnet run`).<br>- `false`: Chạy vô tận theo chu kỳ (dành cho Windows Service). |
| `FetchFromApi` | `true` | Có gọi API SIS để kéo dữ liệu bệnh nhân mới vào DB mỗi chu kỳ hay không. |
| `DownloadFolder` | `D:\SIS_SIEMENS_PDF` | Đường dẫn thư mục lưu trữ file PDF thông tin tài khoản được trích xuất từ portal. |
| `DelayBetweenPatientsMs` | `500` | Thời gian nghỉ (mili-giây) giữa 2 lượt kích hoạt bệnh nhân liên tiếp để tránh gửi request dồn dập làm nghẽn portal. |

---

## 2. Nhóm cấu hình `Portal` (Kết nối Portal SIS Vietnam)

| Tham số | Giá trị mẫu | Giải thích chi tiết & Tính chất cố định |
| :--- | :--- | :--- |
| `BaseUrl` | `https://portal.sisvietnam.vn` | Địa chỉ gốc của hệ thống Portal bệnh nhân. |
| `Username` | `""` | Tên đăng nhập tài khoản portal (được lưu tại `appsettings.local.json`). |
| `Password` | `""` | Mật khẩu tài khoản portal (được lưu tại `appsettings.local.json`). |
| **`TabId`** | `"5f0dcc66-5b43-48ba-9dc6-a10f8d7dfa0f"` | **GUID của phân hệ tạo tài khoản bệnh nhân:**<br>- **Ý nghĩa:** Định danh tab chức năng `create-patient-user` bên trong giao diện web Liferay/ExtJS của Siemens.<br>- **Tính cố định:** Gần như cố định vĩnh viễn theo menu của portal.<br>- **Cơ chế tự động:** Trong code [PortalClient.cs](file:///d:/repo/SIS/SIS_WEBVIEW2/SIS_WORKER/PortalClient.cs), hệ thống đã có sẵn cơ chế tự động đọc `tabId` từ URL redirect hoặc HTML sau khi login. Giá trị trong file này chỉ đóng vai trò là **giá trị fallback (dự phòng)** nên người dùng **không cần phải thay đổi hay bận tâm**. |
| **`Fingerprint`** | `"2aecfcf843753a6bbaeed33e6927b9ac"` | **Mã nhận diện thiết bị (Device Fingerprint):**<br>- **Ý nghĩa:** Chuỗi hash MD5 32 ký tự đại diện cho dấu vân tay trình duyệt/thiết bị mà script frontend của Liferay tạo ra.<br>- **Tính cố định:** **Cố định dùng mãi mãi.** Backend portal chỉ dùng chuỗi này để ghi nhận thiết bị quen thuộc và bỏ qua bước hỏi mã xác thực OTP/2FA. Giữ nguyên chuỗi này worker vẫn đăng nhập bình thường. |
| `IgnoreSslErrors` | `true` | Bỏ qua kiểm tra chứng chỉ SSL (tương đương `Security.setIgnoreCertificateErrors` trước đây trên WebView2). |

---

## 3. Nhóm cấu hình `SisApi` (API Hệ Thống Bệnh Viện)

| Tham số | Giá trị mặc định | Giải thích chi tiết |
| :--- | :--- | :--- |
| `BaseUrl` | `https://dhs.sisvietnam.vn` | Server API tích hợp bệnh viện (DHS). |
| `TenantId` | `"3a0f747c-57b6-753b-ba21-3987e8e93b9a"` | Mã định danh đơn vị / bệnh viện. Cố định. |
| `ClientId` | `"DigitalHealthSolutions_HospitalIntegration"` | Client ID OAuth 2.0 cấp riêng cho tích hợp. Cố định. |
| `ClientSecret` | `""` | Secret key bảo mật OAuth 2.0 (lưu tại `appsettings.local.json`). |
| `Scope` | `"HospitalIntegration"` | Phạm vi quyền truy cập API. Cố định. |

---

## 4. Quản lý Secret & Môi Trường Chạy

* **File `appsettings.json`**: Chỉ chứa các cấu hình khung, giá trị mặc định an toàn (`DryRun: true`). File này được commit lên Git.
* **File `appsettings.local.json`**: Chứa mật khẩu thật (`Portal:Username`, `Portal:Password`, `SisApi:ClientSecret`). File này **đã được thêm vào `.gitignore`**, không sợ lộ thông tin bảo mật lên repository.
