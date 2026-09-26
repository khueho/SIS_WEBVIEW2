using System;
using System.Threading.Tasks;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        // ─── JavaScript DOM Helpers & ExtJS Selectors ────────────────────────

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
    }
}
