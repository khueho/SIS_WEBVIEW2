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

            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            webView21.CoreWebView2.Navigate(loginUrl);
        }
    }
}
