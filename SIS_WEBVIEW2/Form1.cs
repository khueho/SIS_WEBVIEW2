using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace SIS_WEBVIEW2
{
    public partial class Form1 : Form
    {
        // === CẤU HÌNH WEB PORTAL ===
        private readonly string loginUrl   = "https://portal.sisvietnam.vn/vi/home";
        private readonly string myUsername = "cnud";
        private readonly string myPassword = "Qts@2026";

        // === CẤU HÌNH API SIS ===
        private readonly string _apiBaseUrl = "https://dhs.sisvietnam.vn";
        private readonly string _apiTenantId = "3a0f747c-57b6-753b-ba21-3987e8e93b9a";
        private readonly string _apiClientId = "DigitalHealthSolutions_HospitalIntegration";
        private string _apiClientSecret = ""; // Đọc từ file api_secret.txt nếu có
        private readonly string _apiScope = "HospitalIntegration";
        private SisApiService? _apiService;

        // === CẤU HÌNH HỆ THỐNG & DB ===
        private readonly string _downloadFolder = @"D:\SIS_SIEMENS_PDF";
        private readonly string _sqlConnectionString = @"Server=.;Database=SisPatientDb;Trusted_Connection=True;TrustServerCertificate=True;";

        // === TIMER & HÀNG ĐỢI ===
        private System.Windows.Forms.Timer? _autoTimer;
        private const int POLLING_INTERVAL_SECONDS = 15 * 60; // 15 phút = 900 giây
        private int _countdownSeconds = POLLING_INTERVAL_SECONDS;
        private bool _isProcessingQueue = false;

        // === TRẠNG THÁI CHẠY ===
        private bool _isLoggedIn = false;
        private CancellationTokenSource? _cts;
        private string? _currentPatientId;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            InitializeApiAndTimer();

            await webView21.EnsureCoreWebView2Async(null);

            // Bỏ qua lỗi chứng chỉ SSL
            await webView21.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Security.setIgnoreCertificateErrors", "{\"ignore\": true}");

            // Tự động tải file vào D:\SIS_SIEMENS_PDF
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }

            webView21.CoreWebView2.DownloadStarting += (s, downloadArgs) =>
            {
                string originalFileName = Path.GetFileName(downloadArgs.ResultFilePath);
                if (string.IsNullOrEmpty(originalFileName)) originalFileName = "document.pdf";

                string fileName = string.IsNullOrEmpty(_currentPatientId)
                    ? originalFileName
                    : $"{_currentPatientId}_{originalFileName}";

                string targetFilePath = Path.Combine(_downloadFolder, fileName);
                downloadArgs.ResultFilePath = targetFilePath;
                downloadArgs.Handled = true; // Tự động tải ngầm, không hiện popup hỏi của trình duyệt

                // Lắng nghe khi tải file PDF xong -> Đọc PDF và lưu vào SQL DB
                var downloadOp = downloadArgs.DownloadOperation;
                downloadOp.StateChanged += async (senderOp, eOp) =>
                {
                    if (downloadOp.State == CoreWebView2DownloadState.Completed)
                    {
                        try
                        {
                            var dto = await PatientAccessService.ProcessAndSavePdfAsync(targetFilePath, _sqlConnectionString);
                            await PatientAccessService.UpdatePatientStatusAsync(dto.PatientId, "ACTIVATED", _sqlConnectionString);
                            SetStatus($"💾 [Đã lưu DB] ID: {dto.PatientId} - {dto.PatientName} (Pass: {dto.TemporaryPassword})", Color.LightGreen);
                            await RefreshStatusSummaryAsync();
                        }
                        catch (Exception ex)
                        {
                            SetStatus($"⚠️ [Lỗi lưu DB] {ex.Message}", Color.OrangeRed);
                        }
                    }
                };
            };

            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            webView21.CoreWebView2.Navigate(loginUrl);
        }

        /// <summary>
        /// Khởi tạo cấu hình API Service và đồng hồ đếm ngược 15 phút
        /// </summary>
        private void InitializeApiAndTimer()
        {
            // Tự động nạp secret từ file api_secret.txt nếu có
            string baseDirSecret = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api_secret.txt");
            string rootSecret = Path.Combine(Directory.GetCurrentDirectory(), "api_secret.txt");
            if (File.Exists(baseDirSecret))
            {
                _apiClientSecret = File.ReadAllText(baseDirSecret).Trim();
            }
            else if (File.Exists(rootSecret))
            {
                _apiClientSecret = File.ReadAllText(rootSecret).Trim();
            }

            _apiService = new SisApiService
            {
                BaseUrl = _apiBaseUrl,
                TenantId = _apiTenantId,
                ClientId = _apiClientId,
                ClientSecret = _apiClientSecret,
                Scope = _apiScope
            };

            _autoTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000 // Tick mỗi giây để cập nhật đồng hồ đếm ngược
            };
            _autoTimer.Tick += AutoTimer_Tick;
            _autoTimer.Start();

            _ = RefreshStatusSummaryAsync();
        }

        private async void AutoTimer_Tick(object? sender, EventArgs e)
        {
            if (!chkAuto15m.Checked)
            {
                lblTimer.Text = "⏳ Tự động quét: TẮT";
                return;
            }

            _countdownSeconds--;
            if (_countdownSeconds <= 0)
            {
                _countdownSeconds = POLLING_INTERVAL_SECONDS;
                lblTimer.Text = "⏳ Đang quét API...";
                await FetchApiDataAsync(triggerProcessIfIdle: true);
            }
            else
            {
                int mm = _countdownSeconds / 60;
                int ss = _countdownSeconds % 60;
                lblTimer.Text = $"⏳ Tiếp theo: {mm:D2}:{ss:D2}";
            }
        }

        /// <summary>
        /// Gọi API lấy danh sách bệnh nhân và lưu vào bảng DiagnosticImagingPatients
        /// </summary>
        public async Task FetchApiDataAsync(bool triggerProcessIfIdle = true)
        {
            if (_apiService == null) return;

            if (string.IsNullOrWhiteSpace(_apiService.ClientSecret))
            {
                SetStatus("⚠️ Chưa có ClientSecret API! Hãy tạo file api_secret.txt chứa secret.", Color.Orange);
                return;
            }

            try
            {
                SetStatus("🌐 Đang gọi API lấy danh sách bệnh nhân chẩn đoán hình ảnh hôm nay...", Color.Cyan);
                var patients = await _apiService.GetTodayDiagnosticImagingPatientsAsync();

                var (total, newInserted, updated) = await PatientAccessService.SaveApiPatientsAsync(patients, _sqlConnectionString);
                SetStatus($"✅ API trả về {total} BN (Mới: +{newInserted}, Cập nhật: {updated}).", Color.LightGreen);

                await RefreshStatusSummaryAsync();

                // Nếu đang rảnh và đã đăng nhập web thì tự động kích hoạt các ca pending
                if (triggerProcessIfIdle && _isLoggedIn && !_isProcessingQueue)
                {
                    _ = StartAutoProcess();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"❌ Lỗi gọi API: {ex.Message}", Color.Tomato);
            }
        }

        /// <summary>
        /// Cập nhật hiển thị số lượng thống kê trên giao diện
        /// </summary>
        public async Task RefreshStatusSummaryAsync()
        {
            try
            {
                var s = await PatientAccessService.GetStatusSummaryAsync(_sqlConnectionString);
                if (lblSummary.InvokeRequired)
                {
                    lblSummary.Invoke(() => lblSummary.Text = $"📊 Chờ: {s.Pending} | Đã active: {s.Activated + s.AlreadyActive} | Lỗi: {s.Failed} | Tổng: {s.Total}");
                }
                else
                {
                    lblSummary.Text = $"📊 Chờ: {s.Pending} | Đã active: {s.Activated + s.AlreadyActive} | Lỗi: {s.Failed} | Tổng: {s.Total}";
                }
            }
            catch
            {
                // Bỏ qua lỗi kết nối tạm thời khi tải summary
            }
        }

        private void ChkAuto15m_CheckedChanged(object? sender, EventArgs e)
        {
            if (chkAuto15m.Checked)
            {
                _countdownSeconds = POLLING_INTERVAL_SECONDS;
            }
        }
    }
}

