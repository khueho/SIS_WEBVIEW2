using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        private async void BtnStart_Click(object sender, System.EventArgs e)
        {
            List<string> ids = txtPatientIds.Lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (ids.Count == 0)
            {
                MessageBox.Show("Vui lòng nhập ít nhất một Patient ID!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _cts = new CancellationTokenSource();

            btnStart.Enabled      = false;
            btnStop.Enabled       = true;
            txtPatientIds.ReadOnly = true;

            await ProcessPatientIds(ids, _cts.Token);

            btnStart.Enabled      = true;
            btnStop.Enabled       = false;
            txtPatientIds.ReadOnly = false;
        }

        private void BtnStop_Click(object sender, System.EventArgs e)
        {
            _cts?.Cancel();
            SetStatus("⛔ Đã dừng!", Color.OrangeRed);
        }

        private async Task ProcessPatientIds(List<string> ids, CancellationToken token)
        {
            int delay = (int)numDelay.Value;
            int total = ids.Count;

            for (int i = 0; i < total; i++)
            {
                if (token.IsCancellationRequested) break;

                string id = ids[i];
                SetStatus($"⏳ Đang nhập [{i + 1}/{total}]: {id}", Color.Yellow);

                bool ok = await FillPatientId(id);

                if (!ok)
                    SetStatus($"⚠️ [{i + 1}/{total}] Không tìm thấy input patientID! ID: {id}", Color.Orange);

                try   { await Task.Delay(delay, token); }
                catch (TaskCanceledException) { break; }
            }

            if (!token.IsCancellationRequested)
                SetStatus($"✅ Hoàn thành! Đã nhập {total} ID.", Color.LightGreen);
        }

        /// <summary>
        /// Tìm input patientID trên trang và điền giá trị vào
        /// </summary>
        private async Task<bool> FillPatientId(string patientId)
        {
            string script = $$"""
                (function() {
                    let input = document.querySelector('input[id*="ext-modern-element-123" i]')

                    if (!input) return 'NOT_FOUND';

                    input.value = '{{patientId}}';
                    input.dispatchEvent(new Event('input',  { bubbles: true }));
                    input.dispatchEvent(new Event('change', { bubbles: true }));
                    input.focus();

                    return 'OK';
                })();
            """;

            string result = await webView21.ExecuteScriptAsync(script);
            return result.Contains("OK");
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
