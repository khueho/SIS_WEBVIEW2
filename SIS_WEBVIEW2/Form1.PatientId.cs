using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        /// <summary>
        /// Bắt đầu xử lý tự động tuần tự danh sách Patient ID lấy từ Database (Status = PENDING)
        /// </summary>
        public async Task StartAutoProcess()
        {
            if (_isProcessingQueue)
            {
                SetStatus("ℹ️ Tiến trình xử lý đang chạy...", Color.Yellow);
                return;
            }

            List<PendingPatientItem> pendingPatients = await PatientAccessService.GetPendingPatientsAsync(_sqlConnectionString);

            if (pendingPatients.Count == 0)
            {
                SetStatus("✅ Không có Patient ID nào đang chờ kích hoạt trong Database!", Color.LightGreen);
                await RefreshStatusSummaryAsync();
                return;
            }

            _isProcessingQueue = true;
            _cts = new CancellationTokenSource();

            try
            {
                await ProcessPatientList(pendingPatients, _cts.Token);
            }
            finally
            {
                _isProcessingQueue = false;
                await RefreshStatusSummaryAsync();
            }
        }

        private async Task ProcessPatientIds(List<string> ids, CancellationToken token)
        {
            var list = ids.Select(id => new PendingPatientItem { PatientCode = id }).ToList();
            await ProcessPatientList(list, token);
        }

        private async Task ProcessPatientList(List<PendingPatientItem> patients, CancellationToken token)
        {
            const int delay = 3000; // Delay 3s chờ web phản hồi
            int total = patients.Count;

            int countActivated     = 0; // nút Edit  → đã active sẵn
            int countJustActivated = 0; // vừa được activate trong phiên này
            int countFailed        = 0; // activate thất bại
            int countNoResult      = 0; // không tìm được kết quả

            for (int i = 0; i < total; i++)
            {
                if (token.IsCancellationRequested) break;

                var patient = patients[i];
                string id = patient.PatientCode;
                string? email = patient.Email;
                _currentPatientId = id;
                SetStatus($"⏳ [{i + 1}/{total}] Đang nhập: {id}", Color.Yellow);

                // Bước 1: Điền PatientID
                bool filled = await FillPatientId(id);
                if (!filled)
                {
                    SetStatus($"⚠️ [{i + 1}/{total}] Không tìm thấy input PatientID! ID: {id}", Color.Orange);
                    await PatientAccessService.UpdatePatientStatusAsync(id, "NOT_FOUND", _sqlConnectionString);
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
                    await PatientAccessService.UpdatePatientStatusAsync(id, "FAILED", _sqlConnectionString);
                    countFailed++;
                    try { await Task.Delay(delay, token); } catch (TaskCanceledException) { break; }
                    continue;
                }

                // Chờ kết quả tải xong
                try { await Task.Delay(delay, token); } catch (TaskCanceledException) { break; }

                // Bước 3: Kiểm tra trạng thái trên Portal
                SetStatus($"🔎 [{i + 1}/{total}] Kiểm tra trạng thái: {id}", Color.LightBlue);
                string status = await CheckActiveStatus();

                switch (status)
                {
                    case "ACTIVATED":
                        SetStatus($"✅ [{i + 1}/{total}] Đã active sẵn: {id}", Color.LightGreen);
                        await PatientAccessService.UpdatePatientStatusAsync(id, "ALREADY_ACTIVE", _sqlConnectionString);
                        countActivated++;
                        break;

                    case "NOT_ACTIVATED":
                        // Bước 4: Tự động kích hoạt (Quy trình chi tiết trong Form1.Activation.cs)
                        SetStatus($"🔄 [{i + 1}/{total}] Chưa active, đang kích hoạt: {id}", Color.Gold);
                        bool activateOk = await ActivatePatient(id, email, token);

                        if (activateOk)
                        {
                            SetStatus($"🎉 [{i + 1}/{total}] Đã kích hoạt xong: {id}", Color.LightGreen);
                            await PatientAccessService.UpdatePatientStatusAsync(id, "ACTIVATED", _sqlConnectionString);
                            countJustActivated++;
                        }
                        else
                        {
                            SetStatus($"❌ [{i + 1}/{total}] Kích hoạt thất bại: {id}", Color.Tomato);
                            await PatientAccessService.UpdatePatientStatusAsync(id, "FAILED", _sqlConnectionString);
                            countFailed++;
                        }
                        break;

                    default:
                        SetStatus($"❓ [{i + 1}/{total}] Không có kết quả: {id}", Color.Gray);
                        await PatientAccessService.UpdatePatientStatusAsync(id, "NOT_FOUND", _sqlConnectionString);
                        countNoResult++;
                        break;
                }

                _ = RefreshStatusSummaryAsync();
                try { await Task.Delay(500, token); } catch (TaskCanceledException) { break; }
            }
            _currentPatientId = null;

            if (!token.IsCancellationRequested)
            {
                SetStatus($"✅ Hoàn thành lượt xử lý {total} ID.", Color.LightGreen);
                ShowSummary(total, countActivated, countJustActivated, countFailed, countNoResult);
            }
            else
            {
                SetStatus("⏹ Tiến trình xử lý đã tạm dừng theo yêu cầu người dùng.", Color.Orange);
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
