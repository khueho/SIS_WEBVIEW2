using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        // TODO: Thay thế bằng dữ liệu lấy từ API hoặc input UI
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
        //     "26768648",
        //     "22110758",
        //     "26763296",
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
            "23023028",
            "26768648",
            "22110758",
            "26763296"
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
            const int delay = 3000; // Delay cố định 3 giây (3000ms) chờ web phản hồi
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
                        // Bước 4: Tự động kích hoạt (Quy trình chi tiết trong Form1.Activation.cs)
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
