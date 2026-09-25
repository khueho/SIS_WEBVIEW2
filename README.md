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
    ├── Form1.cs              ← Config, constructor, Form_Load
    ├── Form1.Designer.cs     ← Toàn bộ UI (panel, button, textbox...)
    ├── Form1.Login.cs        ← Logic auto-login (NavigationCompleted, AutoLogin, IsLoginPage)
    ├── Form1.PatientId.cs    ← Logic tuần tự: FillPatientId, ClickApply, CheckActiveStatus, SetStatus
    ├── Program.cs
    └── SIS_WEBVIEW2.csproj   ← net8.0-windows, Microsoft.Web.WebView2 v1.0.4191.47
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
      4. await Task.Delay(numDelay.Value ms)  ← user cấu hình (200-10000ms, mặc định 1000ms)
      5. CheckActiveStatus()  → JS kiểm tra aria-label
      6. Nếu NOT_ACTIVATED    → ActivatePatient(): Activate → Email → Save → Switch Printed & signed → Download về D:\SIS_SIEMENS_PDF
      7. Cập nhật lblStatus realtime
  → ShowSummary() popup tổng kết
```

---

## UI Components (Form1.Designer.cs)

| Control | Mô tả |
|---|---|
| `panelControl` | Panel màu tối ở phía trên, cao 65px |
| `lblDelay` + `numDelay` | Cài delay giữa mỗi ID (200–10000ms, mặc định 1000ms) |
| `lblStatus` | Hiển thị trạng thái realtime toàn bộ quy trình tự động |
| `webView21` | WebView2 chiếm toàn bộ phần dưới, DockStyle.Fill |

---

## Trạng Thái Tiến Độ (cập nhật: 2026-09-19)

### ✅ Đã hoàn thành
- [x] Auto-login vào portal (JS inject username/password/submit)
- [x] Bỏ qua lỗi SSL certificate
- [x] Tách code thành partial class (Form1.cs / Login.cs / PatientId.cs)
- [x] UI panel điều khiển (Start/Stop/Delay/Status)
- [x] Nhập tuần tự PatientID vào input
- [x] Click Apply sau khi điền
- [x] Kiểm tra trạng thái ACTIVATED / NOT_ACTIVATED / NO_RESULT
- [x] CancellationToken để dừng giữa chừng
- [x] Popup tổng kết sau khi xử lý xong
- [x] Flow kích hoạt hoàn chỉnh: Click Activate → Điền email → Click Save → Bật switch Printed and signed → Click Download → Tự động quay về ban đầu
- [x] Tự động tải ngầm file PDF về thư mục `D:\SIS_SIEMENS_PDF` và đổi tên theo `[PatientID]_[FileName]`
- [x] Đóng gói publish single-file self-contained win-x86

### 🔴 Vấn đề đang tồn tại
- [ ] **Selector không ổn định:** `ext-modern-element-123/140` là ExtJS dynamic ID,
  có thể thay đổi nếu trang cập nhật. Cần tìm selector bền vững hơn (aria-label, placeholder, v.v.)
- [ ] **Danh sách ID hardcode:** Hiện chỉ có 1 ID test (`26767120`). Cần cơ chế
  để user nhập danh sách (TextBox, file .txt, file Excel)

### 📋 TODO tiếp theo (ưu tiên cao → thấp)
1. **[HIGH]** Test flow đầy đủ với nhiều ID — xác nhận selector JS còn đúng không
2. **[HIGH]** Nếu selector sai → dùng DevTools của WebView2 để inspect lại
   và cập nhật `FillPatientId()` + `ClickApply()` trong `Form1.PatientId.cs`
3. **[MED]** Cho phép user nhập danh sách ID qua TextBox (thay vì hardcode)
4. **[MED]** Xuất kết quả ra file CSV/Excel sau khi xử lý
5. **[LOW]** Đổi tên app thành "SIS Auto" (đổi `Text` trong Form1.Designer.cs)
6. **[LOW]** Đóng gói lại sau khi có thay đổi mới

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
