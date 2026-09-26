using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        /// <summary>
        /// Quy trình kích hoạt patient:
        ///   1. Click nút Activate
        ///   2. Chờ form email hiện ra
        ///   3. Điền [patientId]@dummy.com
        ///   4. Click Save
        ///   5. Chờ UI tài liệu tải & Bật switch 'Printed and signed'
        ///   6. Click Download → UI tự quay về ban đầu
        /// </summary>
        private async Task<bool> ActivatePatient(string patientId, CancellationToken token)
        {
            // Bước 1: Click nút Activate
            SetStatus("🖱️ [1/6] Đang click nút Activate...", Color.Gold);
            if (!await ClickActivateButton()) return false;

            // Chờ form email hiện ra
            SetStatus("⏳ Chờ form email hiện ra (2s)...", Color.Gold);
            try { await Task.Delay(2000, token); } catch (TaskCanceledException) { return false; }

            // Bước 2: Điền email
            string email = $"{patientId}@dummy.com";
            SetStatus($"✉️ [2/6] Đang điền email: {email}", Color.Gold);
            if (!await FillEmailField(email)) return false;

            // Dừng 1.5s để nhìn thấy email đã được điền
            SetStatus("⏳ Đã điền email, chờ 1.5s...", Color.Gold);
            try { await Task.Delay(1500, token); } catch (TaskCanceledException) { return false; }

            // Bước 3: Click Save
            SetStatus("💾 [3/6] Đang click Save...", Color.Gold);
            if (!await ClickSave()) return false;

            // Chờ trang tài liệu tải hoàn tất (3s)
            SetStatus("⏳ [4/6] Đang chờ trang tài liệu tải (3s)...", Color.Gold);
            try { await Task.Delay(3000, token); } catch (TaskCanceledException) { return false; }

            // Bước 4: Bật switch "Printed and signed"
            SetStatus("🔘 [4/6] Đang bật switch 'Printed and signed'...", Color.Gold);
            bool toggled = false;
            for (int retry = 0; retry < 5; retry++)
            {
                if (await TogglePrintedAndSigned())
                {
                    toggled = true;
                    break;
                }
                try { await Task.Delay(1000, token); } catch (TaskCanceledException) { return false; }
            }
            if (!toggled) return false;

            // Dừng 2 giây để quan sát rõ switch đã chuyển sang màu cam trước khi bấm Download
            SetStatus("⏳ Switch đã bật! Chờ 2s để kiểm tra...", Color.Gold);
            try { await Task.Delay(2000, token); } catch (TaskCanceledException) { return false; }

            // Bước 5: Click Download
            SetStatus("📥 [5/6] Đang click Download...", Color.Gold);
            if (!await ClickDownload()) return false;

            // Bước 6: Chờ UI tự quay về hiển thị ban đầu
            SetStatus("⏳ [6/6] Đang chờ quay về trang kết quả (3s)...", Color.Gold);
            try { await Task.Delay(3000, token); } catch (TaskCanceledException) { return false; }

            return true;
        }
    }
}
