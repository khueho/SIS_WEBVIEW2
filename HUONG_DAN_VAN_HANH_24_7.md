# Hướng Dẫn Vận Hành & Chạy Thực Tế 24/7 (SIS_WORKER)

Tài liệu này hướng dẫn chi tiết quy trình đưa ứng dụng **SIS_WORKER** lên môi trường chạy thực tế 24/7 dưới dạng **Windows Service**.

---

## 1. Chuẩn Bị File Cấu Hình Cho Chạy Thật

Trước khi cài đặt service, cần kiểm tra và cập nhật 2 file cấu hình:

### 1.1. Cấu hình `SIS_WORKER/appsettings.local.json`
Đảm bảo chuyển sang chế độ chạy thật và kiểm tra mật khẩu:

```json
{
  "Worker": {
    "DryRun": false,             // BẬT KÍCH HOẠT THẬT (false = kích hoạt thật trên portal)
    "RunOnce": false,            // Chạy vòng lặp vô tận định kỳ 2 phút
    "PatientLimit": 0,           // 0: Lấy toàn bộ danh sách bệnh nhân từ API
    "MaxPatientsPerCycle": 0     // 0: Xử lý hết tất cả các ca PENDING trong hàng đợi
  },
  "Portal": {
    "Username": "cnud",
    "Password": "..."           // Mật khẩu portal thật
  },
  "SisApi": {
    "ClientSecret": "..."       // Secret API thật của SIS
  }
}
```

### 1.2. Cấu hình `SIS_WORKER/email_config.json`
* **QUAN TRỌNG:** Xóa giá trị trong mục `"OverrideRecipientEmail"` (đặt thành `""` hoặc `null`).
* Nếu còn để email test (ví dụ `hanho11a3@gmail.com`), hệ thống sẽ gửi toàn bộ email của mọi bệnh nhân về hòm thư này. Khi xóa đi, email sẽ được gửi trực tiếp đến địa chỉ email thật của từng bệnh nhân.

---

## 2. Các Bước Cài Đặt Windows Service

### Bước 1: Mở PowerShell quyền Administrator
* Bấm phím **Windows**, gõ **PowerShell**.
* Chuột phải vào **Windows PowerShell** ➔ Chọn **Run as administrator**.

### Bước 2: Chạy lệnh cài đặt tự động (Không cần cd)
Mở PowerShell Admin ở bất kỳ đâu và dán lệnh sau:

```powershell
powershell -ExecutionPolicy Bypass -File "d:\repo\SIS\SIS_WEBVIEW2\scripts\install-service.ps1" -InstallDir "D:\SIS_WORKER"
```

### Script sẽ tự động thực hiện:
1. Build bản Release tối ưu và publish toàn bộ ứng dụng sang thư mục đích `D:\SIS_WORKER`.
2. Tự động sao chép các file cấu hình (`appsettings.json`, `appsettings.local.json`, `db_config.json`, `email_config.json`).
3. Đăng ký dịch vụ Windows Service tên **`SIS_WORKER`** (Tên hiển thị: *SIS Patient Activation Worker*).
4. Thiết lập **StartupType = Automatic** (Tự động khởi động khi bật máy/khởi động lại server mà không cần ai đăng nhập màn hình).
5. Thiết lập **Auto-Recovery**: Tự động thử khởi động lại sau 1 phút nếu bị tắt do sự cố mạng.
6. Bật service chạy ngay lập tức.

---

## 3. Cơ Chế Hoạt Động Ngầm 24/7

* **Chạy độc lập (Session 0):** Hoạt động ngầm liên tục 24/7 trên server mà không phụ thuộc vào việc có tài khoản người dùng đăng nhập hay không.
* **Chu kỳ quét 2 phút/lần:**
  1. Gọi API `dhs.sisvietnam.vn` kéo danh sách bệnh nhân CĐHA mới trong ngày ➔ lưu DB PostgreSQL (`status = PENDING`).
  2. Lấy danh sách các ca `PENDING`, tra cứu trạng thái trên Portal.
  3. Kích hoạt tài khoản, nhận PDF base64 và lưu vào `D:\SIS_SIEMENS_PDF`.
  4. Trích xuất mật khẩu tạm từ PDF, cập nhật DB sang `ACTIVATED`.
  5. Gửi email thông tin tài khoản (Username, Mật khẩu, Link tra cứu) cho bệnh nhân.
* **Tự làm mới phiên Portal:** Nếu cookie/session của Portal hết hạn, worker sẽ tự động login lại và lấy token mới mà không bị gián đoạn tiến trình.

---

## 4. Quản Lý & Giám Sát Khi Vận Hành

### 4.1. Xem nhật ký hoạt động (Log file)
Toàn bộ hành động, trạng thái thành công và lỗi (nếu có) được ghi nhận chi tiết tại:
📁 `D:\SIS_WORKER\logs\sis_worker-YYYYMMDD.log`
* Mỗi ngày một file riêng biệt.
* Tự động lưu trữ và giữ lại log trong vòng 30 ngày gần nhất.

### 4.2. Quản lý Service qua giao diện Windows Services
* Nhấn tổ hợp phím `Windows + R`, gõ `services.msc` rồi nhấn `Enter`.
* Cuộn xuống tìm dịch vụ tên: **`SIS Patient Activation Worker`** (hoặc `SIS_WORKER`).
* Bạn có thể thực hiện:
  * **Start**: Khởi động service.
  * **Stop**: Tạm dừng service.
  * **Restart**: Khởi động lại service khi có thay đổi file cấu hình.

### 4.3. Quản lý Service bằng lệnh PowerShell (Admin)
```powershell
# Kiểm tra trạng thái
Get-Service SIS_WORKER

# Khởi động lại service
Restart-Service SIS_WORKER

# Dừng service
Stop-Service SIS_WORKER
```

---

## 5. Quy Trình Cập Nhật / Gỡ Bỏ Service

### Khi cần cập nhật code/cấu hình mới:
Chỉ cần chạy lại lệnh cài đặt, script sẽ tự động dừng service cũ, build đè bản mới và khởi động lại:
```powershell
powershell -ExecutionPolicy Bypass -File "d:\repo\SIS\SIS_WEBVIEW2\scripts\install-service.ps1" -InstallDir "D:\SIS_WORKER"
```

### Khi muốn gỡ bỏ hoàn toàn Service khỏi Windows:
Mở PowerShell Admin và chạy:
```powershell
powershell -ExecutionPolicy Bypass -File "d:\repo\SIS\SIS_WEBVIEW2\scripts\uninstall-service.ps1"
```
