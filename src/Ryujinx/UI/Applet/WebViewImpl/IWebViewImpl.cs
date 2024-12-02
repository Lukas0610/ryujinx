using Avalonia.Controls;
using Ryujinx.HLE.HOS.Applets.Browser;

namespace Ryujinx.UI.Applet.WebViewImpl
{

    interface IWebViewImpl
    {

        void PresentWebView(ContentControl container);

        void ShowDeveloperTools();

        bool Navigate(BrowserUIArgs args);

    }

}
