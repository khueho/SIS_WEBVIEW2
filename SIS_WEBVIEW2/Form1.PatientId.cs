using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        // TODO: Thay thế bằng dữ liệu lấy từ API
        // "26767100",
        //     "24049600",
        //     "26766162",
        //     "25105778",
        //     "26765358",
        //     "25105778",
        //     "25045371,
        //     "22922488",
        //     "22115470",
        //     "26765254",
        //     "20000"
        private static readonly List<string> _hardcodedPatientIds = new List<string>
        {
            "26767100",
            "24049600",
            "26766162",
            "25105778",
            "26765358",
            "25105778",
            "25045371",
            "22922488",
            "22115470",
            "26765254",
            "23903909",
            "23023028"
        };

        /// <summary>
        /// Bắt đầu xử lý tự động tuần tự danh sách Patient ID
        /// </summary>
        public async Task StartAutoProcess()
        {
            List<string> ids = _hardcodedPatientIds;

            if (ids.Count == 0)
            {
                SetStatus("⚠️ Danh sách Patient ID trống!", Color.Orange);
                return;
            }

            _cts = new CancellationTokenSource();
            await ProcessPatientIds(ids, _cts.Token);
        }

        private async Task ProcessPatientIds(List<string> ids, CancellationToken token)
        {
            int delay = (int)numDelay.Value;
            int total = ids.Count;

            int countActivated     = 0; // nút Edit  → đã active sẵn
            int countJustActivated = 0; // vừa được activate trong phiên này
            int countFailed        = 0; // activate thất bại
            int countNoResult      = 0; // không tìm được kết quả

            for (int i = 0; i < total; i++)
            {
                if (token.IsCancellationRequested) break;

                string id = ids[i];
                _currentPatientId = id;
                SetStatus($"⏳ [{i + 1}/{total}] Đang nhập: {id}", Color.Yellow);

                // Bước 1: Điền PatientID
                bool filled = await FillPatientId(id);
                if (!filled)
                {
                    SetStatus($"⚠️ [{i + 1}/{total}] Không tìm thấy input PatientID! ID: {id}", Color.Orange);
                    countNoResult++;
                    try { await Task.Delay(delay, token); } catch (TaskCanceledException) { break; }
                    continue;
                }

                try { await Task.Delay(300, token); } catch (TaskCanceledException) { break; }

                // Bước 2: Click Apply
                SetStatus($"🔍 [{i + 1}/{total}] Đang click Apply: {id}", Color.Cyan);
                bool clicked = await ClickApply();
                if (!clicked)
                {
                    SetStatus($"⚠️ [{i + 1}/{total}] Không tìm thấy nút Apply! ID: {id}", Color.Orange);
                    countNoResult++;
                    try { await Task.Delay(delay, token); } catch (TaskCanceledException) { break; }
                    continue;
                }

                // Chờ kết quả tải xong
                try { await Task.Delay(delay, token); } catch (TaskCanceledException) { break; }

                // Bước 3: Kiểm tra trạng thái
                SetStatus($"🔎 [{i + 1}/{total}] Kiểm tra trạng thái: {id}", Color.LightBlue);
                string status = await CheckActiveStatus();

                switch (status)
                {
                    case "ACTIVATED":
                        SetStatus($"✅ [{i + 1}/{total}] Đã active: {id}", Color.LightGreen);
                        countActivated++;
                        break;

                    case "NOT_ACTIVATED":
                        // Bước 4: Tự động kích hoạt
                        SetStatus($"🔄 [{i + 1}/{total}] Chưa active, đang kích hoạt: {id}", Color.Gold);
                        bool activateOk = await ActivatePatient(id, token);

                        if (activateOk)
                        {
                            SetStatus($"🎉 [{i + 1}/{total}] Đã kích hoạt xong: {id}", Color.LightGreen);
                            countJustActivated++;
                        }
                        else
                        {
                            SetStatus($"❌ [{i + 1}/{total}] Kích hoạt thất bại: {id}", Color.Tomato);
                            countFailed++;
                        }
                        break;

                    default:
                        SetStatus($"❓ [{i + 1}/{total}] Không có kết quả: {id}", Color.Gray);
                        countNoResult++;
                        break;
                }

                try { await Task.Delay(500, token); } catch (TaskCanceledException) { break; }
            }
            _currentPatientId = null;

            if (!token.IsCancellationRequested)
            {
                SetStatus($"✅ Hoàn thành! Đã xử lý {total} ID.", Color.LightGreen);
                ShowSummary(total, countActivated, countJustActivated, countFailed, countNoResult);
            }
        }

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

        // ─── JavaScript helpers ───────────────────────────────────────────────

        /// <summary>Điền patientID vào input tìm kiếm (ExtJS dynamic ID)</summary>
        private async Task<bool> FillPatientId(string patientId)
        {
            string script = "(function() { " +
                "let input = document.querySelector('input[id*=\"ext-modern-element-123\" i]'); " +
                "if (!input) return 'NOT_FOUND'; " +
                "input.value = '" + patientId + "'; " +
                "input.dispatchEvent(new Event('input',  { bubbles: true })); " +
                "input.dispatchEvent(new Event('change', { bubbles: true })); " +
                "input.focus(); " +
                "return 'OK'; " +
                "})();";

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>Click nút Apply/Search (ExtJS dynamic ID)</summary>
        private async Task<bool> ClickApply()
        {
            string script = "(function() { " +
                "let btn = document.querySelector('[id*=\"ext-modern-element-140\" i]'); " +
                "if (!btn) return 'NOT_FOUND'; " +
                "btn.click(); " +
                "return 'OK'; " +
                "})();";

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>
        /// Kiểm tra trạng thái kết quả:
        ///   - Nút aria-label="Edit"     → ACTIVATED
        ///   - Nút aria-label="Activate" → NOT_ACTIVATED
        /// </summary>
        private async Task<string> CheckActiveStatus()
        {
            string script = """
                (function() {
                    let editBtn     = document.querySelector('[aria-label="Edit"]');
                    let activateBtn = document.querySelector('[aria-label="Activate"]');
                    if (editBtn)     return 'ACTIVATED';
                    if (activateBtn) return 'NOT_ACTIVATED';
                    return 'NO_RESULT';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            if (result.Contains("NOT_ACTIVATED")) return "NOT_ACTIVATED";
            if (result.Contains("ACTIVATED"))     return "ACTIVATED";
            return "NO_RESULT";
        }

        /// <summary>Click nút Activate (aria-label="Activate")</summary>
        private async Task<bool> ClickActivateButton()
        {
            string script = """
                (function() {
                    let btn = document.querySelector('[aria-label="Activate"]');
                    if (!btn) return 'NOT_FOUND';
                    btn.click();
                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>
        /// Tìm input email VISIBLE trên form activate và điền giá trị
        /// </summary>
        private async Task<bool> FillEmailField(string email)
        {
            // Chỉ lấy input visible (offsetParent !== null) — tránh hidden input
            string script = "(function() { " +
                "let all = Array.from(document.querySelectorAll('input')).filter(i => i.offsetParent !== null); " +
                "let input = all.find(i => i.type === 'email') || " +
                "            all.find(i => (i.placeholder||'').toLowerCase().includes('email')) || " +
                "            all.find(i => (i.name||'').toLowerCase().includes('email')) || " +
                "            all.find(i => (i.id||'').toLowerCase().includes('email')); " +
                "if (!input) return 'NOT_FOUND'; " +
                "input.value = '" + email + "'; " +
                "input.dispatchEvent(new Event('input',  { bubbles: true })); " +
                "input.dispatchEvent(new Event('change', { bubbles: true })); " +
                "input.focus(); " +
                "return 'OK'; " +
                "})();";

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>Click nút Cancel đang hiển thị trên form</summary>
        private async Task<bool> ClickCancel()
        {
            string script = """
                (function() {
                    let btn = Array.from(document.querySelectorAll('[aria-label="Cancel"], button')).find(b =>
                        b.offsetParent !== null &&
                        (b.getAttribute('aria-label') === 'Cancel' || b.textContent.trim().toLowerCase().includes('cancel'))
                    );
                    if (!btn) return 'NOT_FOUND';

                    btn.click();
                    let child = btn.querySelector('.x-button-el') || btn.firstElementChild;
                    if (child) child.click();
                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>Click nút Save trên form activate (chỉ click 1 lần duy nhất)</summary>
        private async Task<bool> ClickSave()
        {
            string script = """
                (function() {
                    let btn = Array.from(document.querySelectorAll('[aria-label="Save"], button, [role="button"], div.x-button')).find(b =>
                        b.offsetParent !== null &&
                        ((b.getAttribute('aria-label') || '').toLowerCase() === 'save' || 
                         (b.innerText || b.textContent || '').trim().toLowerCase() === 'save')
                    );
                    if (!btn) return 'NOT_FOUND';

                    // Chỉ click đúng 1 lần duy nhất
                    btn.click();
                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>Bật switch 'Printed and signed'</summary>
        private async Task<bool> TogglePrintedAndSigned()
        {
            string script = """
                (function() {
                    let label = Array.from(document.querySelectorAll('*')).find(e =>
                        e.offsetParent !== null && e.children.length === 0 &&
                        (e.textContent || '').toLowerCase().includes('printed and signed')
                    );
                    if (!label) return 'NOT_FOUND';

                    let knob = label.previousElementSibling || label.parentElement?.querySelector('.x-toggle, .x-slider, input') || label;
                    knob.click();
                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        /// <summary>Click nút Download</summary>
        private async Task<bool> ClickDownload()
        {
            string script = """
                (function() {
                    let btn = Array.from(document.querySelectorAll('[aria-label*="Download" i], button, [role="button"], div.x-button')).find(b =>
                        b.offsetParent !== null &&
                        ((b.getAttribute('aria-label') || '').toLowerCase() === 'download' || 
                         (b.innerText || b.textContent || '').trim().toLowerCase() === 'download')
                    );
                    if (!btn) return 'NOT_FOUND';

                    btn.click();
                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
        }

        private void ShowSummary(int total, int activated, int justActivated, int failed, int noResult)
        {
            string msg =
                $"📊 Tổng kết xử lý {total} Patient ID:\n\n" +
                $"  ✅ Đã active sẵn       : {activated}\n" +
                $"  🎉 Vừa được kích hoạt  : {justActivated}\n" +
                $"  ❌ Kích hoạt thất bại  : {failed}\n" +
                $"  ❓ Không có kết quả    : {noResult}";

            MessageBox.Show(msg, "Thống kê kết quả",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetStatus(string message, Color color)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(() => SetStatus(message, color));
                return;
            }
            lblStatus.ForeColor = color;
            lblStatus.Text = message;
        }
    }
}
