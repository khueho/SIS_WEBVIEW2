using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace SIS_WEBVIEW2
{
    public partial class Form1 : Form
    {
        // === CẤU HÌNH ===
        private readonly string loginUrl   = "https://portal.sisvietnam.vn/vi/home";
        private readonly string myUsername = "cnud";
        private readonly string myPassword = "Qts@2026";

        // === TRẠNG THÁI ===
        private bool _isLoggedIn = false;
        private CancellationTokenSource? _cts;
        private string? _currentPatientId;
        private readonly string _downloadFolder = @"D:\SIS_SIEMENS_PDF";
        private readonly string _sqlConnectionString = @"Server=.;Database=SisPatientDb;Trusted_Connection=True;TrustServerCertificate=True;";

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await webView21.EnsureCoreWebView2Async(null);

            // Bỏ qua lỗi chứng chỉ SSL
            await webView21.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Security.setIgnoreCertificateErrors", "{\"ignore\": true}");

            // Tự động tải file vào D:\SIS_SIEMENS_PDF
            if (!System.IO.Directory.Exists(_downloadFolder))
            {
                System.IO.Directory.CreateDirectory(_downloadFolder);
            }

            webView21.CoreWebView2.DownloadStarting += (s, downloadArgs) =>
            {
                string originalFileName = System.IO.Path.GetFileName(downloadArgs.ResultFilePath);
                if (string.IsNullOrEmpty(originalFileName)) originalFileName = "document.pdf";

                string fileName = string.IsNullOrEmpty(_currentPatientId)
                    ? originalFileName
                    : $"{_currentPatientId}_{originalFileName}";

                string targetFilePath = System.IO.Path.Combine(_downloadFolder, fileName);
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
                            SetStatus($"💾 [Đã lưu DB] ID: {dto.PatientId} - {dto.PatientName} (Pass: {dto.TemporaryPassword})", System.Drawing.Color.LightGreen);
                        }
                        catch (Exception ex)
                        {
                            SetStatus($"⚠️ [Lỗi lưu DB] {ex.Message}", System.Drawing.Color.OrangeRed);
                        }
                    }
                };
            };

            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            webView21.CoreWebView2.Navigate(loginUrl);
        }
    }
}
