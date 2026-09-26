# Context & Rules cho Agent — SIS_WEBVIEW2

> File này được tự động load bởi Antigravity IDE mỗi phiên làm việc.
> Đọc README.md để có thông tin đầy đủ hơn.

## Dự Án Là Gì

**SIS_WEBVIEW2** — WinForms .NET 8 + WebView2, tự động hóa việc kiểm tra
trạng thái PatientID trên `https://portal.sisvietnam.vn`.

App mở browser (WebView2), tự đăng nhập, rồi tuần tự nhập từng PatientID
vào input → click Apply → đọc kết quả (Đã active / Chưa active / Không có kết quả).

---

## Cấu Trúc File Quan Trọng

| File | Vai trò |
|---|---|
| `SIS_WEBVIEW2/Form1.cs` | Config đăng nhập, constructor, tải file ngầm |
| `SIS_WEBVIEW2/Form1.Login.cs` | Logic auto-login qua JS inject |
| `SIS_WEBVIEW2/Form1.PatientId.cs` | Điều phối vòng lặp PatientID, thống kê |
| `SIS_WEBVIEW2/Form1.Activation.cs` | Quy trình chi tiết 6 bước kích hoạt bệnh nhân |
| `SIS_WEBVIEW2/Form1.DomActions.cs` | Toàn bộ JS DOM helper & selectors ExtJS |
| `SIS_WEBVIEW2/Form1.Designer.cs` | UI (panel/button/label layout) |

---

## Điểm Cần Chú Ý Ngay

1. **Selectors JS không ổn định** — `ext-modern-element-123` và `ext-modern-element-140`
   là ExtJS dynamic ID. Nếu user báo app không tìm thấy input → inspect lại trước tiên.

2. **Danh sách PatientID hardcode** trong `Form1.PatientId.cs`:
   ```csharp
   private static readonly List<string> _hardcodedPatientIds = new List<string> { "26767120" };
   ```
   Khi user muốn thêm/sửa ID → chỉnh mảng này.

3. **Thông tin đăng nhập** ở `Form1.cs` dòng 11-13 (username: `cnud`).

---

## TODO Hiện Tại (ưu tiên)

1. 🔴 Test selector JS còn đúng không — nếu sai → dùng DevTools inspect lại
2. 🟡 Cho nhập danh sách ID qua UI (TextBox) thay vì hardcode
3. 🟡 Xuất kết quả ra CSV sau khi xử lý
4. 🟢 Đổi tên app thành "SIS Auto"

---

## Quy Tắc Khi Làm Việc

- Luôn đọc `README.md` để nắm tiến độ mới nhất trước khi bắt đầu
- Sau mỗi thay đổi quan trọng, cập nhật mục **Trạng Thái Tiến Độ** trong `README.md`
- Không xóa comments tiếng Việt trong code — chúng là context quan trọng
- Khi sửa selector JS, test bằng cách chạy trong Console của DevTools WebView2 trước
- Khi tạo các file test hay script debug, tạo trực tiếp trong thư mục project (workspace) để dễ dàng theo dõi và quản lý, tránh tạo ở các thư mục tạm bên ngoài. Xóa bỏ hoặc dọn dẹp các file tạm này khi kết thúc thử nghiệm.
