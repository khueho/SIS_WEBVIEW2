using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace SIS_WEBVIEW2
{
    public partial class Form1
    {
        private async void WebView21_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            string currentUrl = webView21.CoreWebView2.Source;

            if (!_isLoggedIn && IsLoginPage(currentUrl))
            {
                // Còn ở trang login → thực hiện điền form
                await AutoLogin(myUsername, myPassword);
            }
            else if (!_isLoggedIn && !IsLoginPage(currentUrl))
            {
                // Đã redirect ra ngoài → đăng nhập thành công
                _isLoggedIn = true;
                webView21.NavigationCompleted -= WebView21_NavigationCompleted;
                SetStatus("✅ Đăng nhập thành công! Nhập danh sách ID rồi nhấn Bắt đầu.", System.Drawing.Color.LightGreen);
            }
        }

        private bool IsLoginPage(string url)
        {
            return url.Contains("login") || url.Contains("sign-in") || url == loginUrl;
        }

        private async Task AutoLogin(string username, string password)
        {
            string script = $$"""
                (function() {
                    let userField = Array.from(document.querySelectorAll('input')).find(i => {
                        return i.outerHTML.toLowerCase().includes('email');
                    });

                    let passField  = document.querySelector('input[type="password"]');
                    let submitBtn  = document.querySelector('button[type="submit"], input[type="submit"]');

                    if (userField && passField) {
                        userField.value = '{{username}}';
                        userField.dispatchEvent(new Event('input',  { bubbles: true }));
                        userField.dispatchEvent(new Event('change', { bubbles: true }));

                        passField.value = '{{password}}';
                        passField.dispatchEvent(new Event('input',  { bubbles: true }));
                        passField.dispatchEvent(new Event('change', { bubbles: true }));

                        setTimeout(() => {
                            if (submitBtn)        submitBtn.click();
                            else if (userField.form) userField.form.submit();
                        }, 300);

                        return 'SUCCESS';
                    }
                    return 'NOT_FOUND';
                })();
            """;

            await webView21.ExecuteScriptAsync(script);
        }
    }
}
