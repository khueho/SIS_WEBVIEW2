using System;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly string _apiClientSecret = "XPWFParRQldtZopqdDFLPYblIzauxuWT";
        private readonly string _apiScope = "HospitalIntegration";
        private SisApiService? _apiService;

        // Cấu hình số lượng bệnh nhân lấy từ API để test (Đặt <= 0 nếu muốn lấy TOÀN BỘ danh sách)
        private int _testLimitPatientCount = 4;

        // === CẤU HÌNH HỆ THỐNG & DB ===
        private readonly string _downloadFolder = @"D:\SIS_SIEMENS_PDF";
        private readonly string _sqlConnectionString = @"Server=.;Database=SisPatientDb;Trusted_Connection=True;TrustServerCertificate=True;";

        // === TIMER & HÀNG ĐỢI ===
        private System.Windows.Forms.Timer? _autoTimer;
        private const int POLLING_INTERVAL_SECONDS = 2 * 60; // 2 phút = 120 giây
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

            // Đảm bảo schema DB có đầy đủ các cột cần thiết (bao gồm theo dõi gửi email)
            _ = PatientAccessService.EnsureDatabaseSchemaAsync(_sqlConnectionString);

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

                            // Tự động gửi email thông tin tài khoản nếu bệnh nhân có email thật
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    string? realEmail = await PatientAccessService.GetPatientRealEmailAsync(dto.PatientId, _sqlConnectionString);
                                    if (!string.IsNullOrWhiteSpace(realEmail))
                                    {
                                        SetStatus($"✉️ Đang gửi thông tin tài khoản đến email: {realEmail}...", Color.Cyan);
                                        var (sendOk, sendErr) = await EmailService.SendAccountInfoEmailAsync(dto, realEmail);
                                        if (sendOk)
                                        {
                                            await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "SENT", null, _sqlConnectionString);
                                            SetStatus($"📧 [Đã gửi Email] Thành công đến {realEmail} (BN: {dto.PatientName})", Color.LightGreen);
                                        }
                                        else
                                        {
                                            await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "FAILED", sendErr, _sqlConnectionString);
                                            SetStatus($"⚠️ [Gửi Email thất bại] {sendErr}", Color.Orange);
                                        }
                                    }
                                    else
                                    {
                                        await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "NO_EMAIL", null, _sqlConnectionString);
                                    }
                                }
                                catch (Exception exEmail)
                                {
                                    await PatientAccessService.UpdateEmailSentStatusAsync(dto.PatientId, "FAILED", exEmail.Message, _sqlConnectionString);
                                    SetStatus($"⚠️ [Lỗi gửi email] {exEmail.Message}", Color.Orange);
                                }
                            });
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
        /// Khởi tạo cấu hình API Service và đồng hồ đếm ngược 2 phút
        /// </summary>
        private void InitializeApiAndTimer()
        {
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
                SetStatus("⚠️ Chưa cấu hình ClientSecret cho SIS API!", Color.Orange);
                return;
            }

            try
            {
                SetStatus("🌐 Đang gọi API lấy danh sách bệnh nhân chẩn đoán hình ảnh hôm nay...", Color.Cyan);
                var rawPatients = await _apiService.GetTodayDiagnosticImagingPatientsAsync();

                // Lấy danh sách bệnh nhân theo số lượng cấu hình _testLimitPatientCount
                var patients = _testLimitPatientCount > 0
                    ? rawPatients.Take(_testLimitPatientCount).ToList()
                    : rawPatients;

                var (total, newInserted, updated) = await PatientAccessService.SaveApiPatientsAsync(patients, _sqlConnectionString);
                string testInfo = patients.Count > 0 
                    ? string.Join(", ", patients.Select(p => $"{p.PatientCode} - {p.FullName}"))
                    : "Không có dữ liệu";
                string tag = _testLimitPatientCount > 0 ? $"TEST {_testLimitPatientCount} BN" : "TOÀN BỘ";
                SetStatus($"✅ [{tag}] Đã lấy {patients.Count} ca: {testInfo} (Mới: +{newInserted}, Cập nhật: {updated}).", Color.LightGreen);

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

