using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.HOS.Applets.Browser;
using Ryujinx.UI.Applet.WebViewImpl;

namespace Ryujinx.Ava.UI.Applet
{
    internal partial class BrowserAppletWindow : StyleableWindow
    {

        private readonly IWebViewImpl _webViewImpl;

        public BrowserAppletWindow(AppHost appHost)
        {
#if ENABLE_WEBVIEW_APPLET
            _webViewImpl = new ChromiumWebViewImpl(appHost);
#else
            _webViewImpl = new DummyWebViewImpl(appHost);
#endif

            DataContext = this;

            InitializeComponent();
            _webViewImpl?.PresentWebView(WebViewContainer);
        }

        public void ShowWebViewDeveloperTools()
        {
            _webViewImpl?.ShowDeveloperTools();
        }

        public bool Navigate(BrowserUIArgs args)
        {
            return _webViewImpl?.Navigate(args) == true;
        }

    }
}
