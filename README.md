# SIS WebView2 — Auto PatientID Checker

**Dự án:** Windows Forms (.NET 8) + WebView2  
**Mục tiêu:** Tự động hóa quy trình kiểm tra/xử lý trạng thái PatientID trên portal SIS Vietnam.

---

## Mục Tiêu Tổng Quan

Tự động hóa các bước thủ công trên `https://portal.sisvietnam.vn`:

1. Tự động đăng nhập vào portal
2. Tuần tự xử lý danh sách PatientID (hiện hardcode, tương lai đọc từ file/API)
3. Với mỗi ID: điền vào input → click Apply → đọc kết quả
4. Phân loại kết quả: **Đã active** / **Chưa active** / **Không có kết quả**
5. Hiển thị thống kê tổng kết cuối cùng

---

## Cấu Trúc File

```
SIS_WEBVIEW2/
├── SIS_WEBVIEW2.sln
└── SIS_WEBVIEW2/
    ├── Form1.cs              ← Config, constructor, Form_Load, xử lý Download ngầm
    ├── Form1.Designer.cs     ← Toàn bộ UI (panel, button, textbox...)
    ├── Form1.Login.cs        ← Logic auto-login (NavigationCompleted, AutoLogin, IsLoginPage)
    ├── Form1.PatientId.cs    ← Điều phối tuần tự PatientID (StartAutoProcess, ProcessPatientIds, ShowSummary)
    ├── Form1.Activation.cs   ← Quy trình chi tiết 6 bước kích hoạt bệnh nhân (ActivatePatient)
    ├── Form1.DomActions.cs   ← Tập hợp toàn bộ JavaScript DOM inject & ExtJS selectors
    ├── EmailService.cs       ← Dịch vụ gửi email thông tin tài khoản qua MailKit (HTML responsive + đính kèm PDF)
    ├── email_config.json     ← File cấu hình SMTP server, tài khoản gửi và mẫu email
    ├── Program.cs
    └── SIS_WEBVIEW2.csproj   ← net8.0-windows, Microsoft.Web.WebView2 v1.0.4191.47, MailKit v4.18.1
```

---

## Tech Stack

| Thành phần | Chi tiết |
|---|---|
| Framework | .NET 8 Windows (WinForms) |
| Browser engine | Microsoft WebView2 v1.0.4191.47 |
| Target URL | `https://portal.sisvietnam.vn/vi/home` |
| Build | `dotnet build` / Visual Studio 2022 |
| Publish | Single-file self-contained `win-x86` |

---

## Cấu Hình Quan Trọng

### Thông tin đăng nhập (Form1.cs)
```csharp
private readonly string loginUrl   = "https://portal.sisvietnam.vn/vi/home";
private readonly string myUsername = "cnud";
private readonly string myPassword = "Qts@2026";
```

### Danh sách PatientID (Form1.PatientId.cs)
```csharp
// Hiện đang hardcode — TODO: đọc từ file/TextBox/API
private static readonly List<string> _hardcodedPatientIds = new List<string>
{
    "26767120",
    // Thêm ID vào đây
};
```

### JavaScript Selectors (Form1.PatientId.cs)
```javascript
// Input PatientID — selector dựa vào ExtJS dynamic ID
input[id*="ext-modern-element-123" i]

// Nút Apply/Search
[id*="ext-modern-element-140" i]

// Kiểm tra trạng thái kết quả
[aria-label="Edit"]      // → ACTIVATED (đã active)
[aria-label="Activate"]  // → NOT_ACTIVATED (chưa active)
```

> ⚠️ **CẢNH BÁO:** ExtJS sinh ID động (`ext-modern-element-XXX`), số thứ tự có thể thay đổi
> nếu trang thêm/xóa component. Nếu app không tìm thấy input, cần inspect lại selector.

---

## Luồng Xử Lý Chính

```
App khởi động
  → Navigate đến portal.sisvietnam.vn
  → NavigationCompleted: phát hiện trang login
  → AutoLogin() — điền email + password + click submit (JS)
  → NavigationCompleted: URL đổi → đăng nhập thành công
  → SetStatus "✅ Đăng nhập thành công"
  → Tự động bắt đầu StartAutoProcess() sau 2 giây

Tự động xử lý tuần tự từng PatientID trong _hardcodedPatientIds:
      1. FillPatientId(id)    → JS điền input + dispatch events
      2. await Task.Delay(300ms)
      3. ClickApply()         → JS click nút Apply
      4. await Task.Delay(3000ms)  ← delay cố định 3 giây chờ kết quả tải
      5. CheckActiveStatus()  → JS kiểm tra aria-label
      6. Nếu NOT_ACTIVATED    → ActivatePatient(): Activate → Email → Save → Switch Printed & signed → Download về D:\SIS_SIEMENS_PDF
      7. Cập nhật lblStatus realtime
  → ShowSummary() popup tổng kết
```

---

## UI Components (Form1.Designer.cs)

| Control | Mô tả |
|---|---|
| `panelControl` | Panel màu tối ở phía trên, cao 50px |
| `lblStatus` | Hiển thị trạng thái realtime toàn bộ quy trình tự động (Dock Fill panel) |
| `webView21` | WebView2 chiếm toàn bộ phần dưới, DockStyle.Fill |

---

## Trạng Thái Tiến Độ (cập nhật: 2026-09-19)

### ✅ Đã hoàn thành
- [x] Auto-login vào portal (JS inject username/password/submit)
- [x] Bỏ qua lỗi SSL certificate
- [x] Tách code thành các partial class dễ bảo trì:
  - `Form1.cs`: Khởi tạo & tự động tải file ngầm
  - `Form1.Login.cs`: Tự động đăng nhập
  - `Form1.PatientId.cs`: Điều phối vòng lặp duyệt ID & thống kê
  - `Form1.Activation.cs`: Quy trình 6 bước kích hoạt chi tiết
  - `Form1.DomActions.cs`: Toàn bộ JavaScript helpers & ExtJS selectors
- [x] UI panel điều khiển (Start/Stop/Delay/Status)
- [x] Nhập tuần tự PatientID vào input
- [x] Click Apply sau khi điền
- [x] Kiểm tra trạng thái ACTIVATED / NOT_ACTIVATED / NO_RESULT
- [x] CancellationToken để dừng giữa chừng
- [x] Popup tổng kết sau khi xử lý xong
- [x] Flow kích hoạt hoàn chỉnh: Click Activate → Điền email → Click Save → Bật switch Printed and signed → Click Download → Tự động quay về ban đầu
- [x] Tự động tải ngầm file PDF về thư mục `D:\SIS_SIEMENS_PDF` và đổi tên theo `[PatientID]_[FileName]`
- [x] Sửa lỗi Regex trích xuất TemporaryPassword từ PDF:
  - Khắc phục lỗi pattern lookahead gây ngắt chuỗi trước đây.
  - Khắc phục lỗi `(?<user>\d{6,10})` tham lam (greedy) vô tình nuốt mất chữ số đầu tiên của mật khẩu khi mật khẩu bắt đầu bằng chữ số (dùng `\k<id>` để map khớp chính xác mã bệnh nhân, đảm bảo mật khẩu như `3S6@xcs7!vk8`, `2xCh6!CaySX6` giữ nguyên vẹn 12 ký tự).
- [x] **Bảng Database riêng biệt `DiagnosticImagingPatients`**: Lưu dữ liệu nhân thân từ API và theo dõi trạng thái (`PENDING`, `ACTIVATED`, `ALREADY_ACTIVE`, `FAILED`, `NOT_FOUND`)
- [x] **Truyền email thật từ API khi kích hoạt**: Tự động lấy email thật của bệnh nhân từ database API để điền vào popup kích hoạt trên Portal. Nếu bệnh nhân không có email thì tự động fallback về `{patientId}@gmail.com`.
- [x] **Tự động gửi Email thông tin tài khoản (MailKit)**: Sau khi kích hoạt và bóc tách PDF thành công, tự động kiểm tra nếu bệnh nhân có email thật thì gửi email HTML chuyên nghiệp chứa Username, Mật khẩu tạm và PortalUrl. Cập nhật trạng thái gửi vào database (`EmailSentStatus`, `EmailSentAt`, `EmailError`).
- [x] **Cơ chế chống trùng lặp tự động**: Bỏ qua bệnh nhân đã kích hoạt/đã có mật khẩu trong `PatientAccessInfo`, không chạy lại quy trình vô ích
- [x] **Tự động quét định kỳ 2 phút**: Background timer tự đếm ngược 02:00, tự động gọi API, nạp các ca mới phát sinh vào hàng đợi
- [x] **Nâng cấp UI SIS Auto**: Thêm thanh công cụ điều khiển API, nút Quét ngay, nút Dừng/Chạy, Checkbox 2 phút, đồng hồ đếm ngược và thanh thống kê số lượng trực tiếp
- [x] Đổi tên app thành "SIS Auto - Quản Lý & Kích Hoạt Bệnh Nhân"
- [x] Đóng gói publish single-file self-contained win-x86

### 🔴 Vấn đề đang tồn tại
- [ ] **Selector không ổn định:** `ext-modern-element-123/140` là ExtJS dynamic ID,
  có thể thay đổi nếu trang cập nhật. Cần tìm selector bền vững hơn (aria-label, placeholder, v.v.)

### 📋 TODO tiếp theo (ưu tiên cao → thấp)
1. **[HIGH]** Cung cấp `client_secret` thật vào file `api_secret.txt` và chạy thử luồng API thực tế
2. **[HIGH]** Test flow đầy đủ với danh sách ID từ API — xác nhận selector JS còn đúng không
3. **[HIGH]** Nếu selector sai → dùng DevTools của WebView2 để inspect lại
   và cập nhật `FillPatientId()` + `ClickApply()` trong `Form1.PatientId.cs`
4. **[MED]** Xuất kết quả ra file CSV/Excel sau khi xử lý
5. **[LOW]** Đóng gói lại sau khi kiểm thử ổn định


---

## Cách Debug Selector JS

Khi app báo "Không tìm thấy input patientID":

1. Chạy app, đợi login xong
2. Mở **DevTools** của WebView2: nhấn `F12` trong cửa sổ app (nếu được bật)
   hoặc vào `webView21.CoreWebView2.OpenDevToolsWindow()`
3. Vào tab **Console**, chạy:
   ```javascript
   // Tìm tất cả input visible trên trang
   Array.from(document.querySelectorAll('input')).filter(i => i.offsetParent).map(i => i.outerHTML)
   ```
4. Cập nhật selector trong `FillPatientId()` ở `Form1.PatientId.cs`

---

## Build & Publish

```powershell
# Build debug
dotnet build SIS_WEBVIEW2\SIS_WEBVIEW2.csproj -c Debug

# Publish single-file self-contained win-x86 (người dùng không cần cài .NET)
dotnet publish SIS_WEBVIEW2\SIS_WEBVIEW2.csproj `
  -c Release -r win-x86 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish\win-x86

# Output: SIS_WEBVIEW2\bin\Release\net8.0-windows\publish\win-x86\SIS_WEBVIEW2.exe (~140MB)
```
