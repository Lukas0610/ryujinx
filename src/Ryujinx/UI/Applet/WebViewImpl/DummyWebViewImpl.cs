using Avalonia.Controls;
using Ryujinx.Ava;
using Ryujinx.HLE.HOS.Applets.Browser;
using System;

namespace Ryujinx.UI.Applet.WebViewImpl
{

    sealed class DummyWebViewImpl : IWebViewImpl
    {

        public DummyWebViewImpl(AppHost appHost)
        {
            ArgumentNullException.ThrowIfNull(appHost);
        }

        public void PresentWebView(ContentControl container) { }

        public void ShowDeveloperTools() { }

        public bool Navigate(BrowserUIArgs args)
        {
            return true;
        }

    }

}
