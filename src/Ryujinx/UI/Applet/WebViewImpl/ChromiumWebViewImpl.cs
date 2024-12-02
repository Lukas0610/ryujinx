#if ENABLE_WEBVIEW_APPLET
using Avalonia.Controls;
using Ryujinx.Ava;
using Ryujinx.HLE.HOS.Applets.Browser;
using System;
using System.IO;
using WebViewControl;

namespace Ryujinx.UI.Applet.WebViewImpl
{

    sealed class ChromiumWebViewImpl : IWebViewImpl
    {

        private readonly AppHost _appHost;
        private readonly WebView _webView;

        public ChromiumWebViewImpl(AppHost appHost)
        {
            _appHost = appHost;

            _webView = new WebView
            {
                Focusable = true,
                AllowDeveloperTools = true,
                DisableBuiltinContextMenus = true,
                DisableFileDialogs = true
            };
        }

        public void PresentWebView(ContentControl container)
        {
            container.Content = _webView;
        }

        public void ShowDeveloperTools()
        {
            _webView.ShowDeveloperTools();
        }

        public bool Navigate(BrowserUIArgs args)
        {
            RegisterJavaScriptObject("nx", new Nx());

            // @TODO: Allows JS execution to continue (in Mario Kart 8 info-page),
            //        but causes loads of other JS errors (seemingly unrelated)
            // RegisterJavaScriptObject("nx.playReport", new Nx.PlayReport());

            if (args.DocumentPath.StartsWith("https://") || args.DocumentPath.StartsWith("http://"))
            {
                _webView.LoadUrl(args.DocumentPath);
            }
            else if (args.DocumentKind == DocumentKind.OfflineHtmlPage)
            {
                string fullDocumentPath = $"local:///html-document/{args.DocumentPath}";

                _webView.BeforeResourceLoad += HandleBeforeResourceLoadForOfflineHtmlPage;

                _webView.LoadUrl(fullDocumentPath);
            }

            return true;
        }

        private void HandleBeforeResourceLoadForOfflineHtmlPage(ResourceHandler resourceHandler)
        {
            Uri url = new Uri(resourceHandler.Url);
            string fullResourcePath = $"/html-document/{url.AbsolutePath.TrimStart('/')}";

            if (_appHost.Device.ApplicationDocumentRegistry.TryGetHtmlDocumentFileData(fullResourcePath, out var resourceData))
            {
                MemoryStream resourceStream = new(resourceData, false);
                string extension = Path.GetExtension(fullResourcePath).ToLowerInvariant();

                resourceHandler.RespondWith(resourceStream, extension);
            }
        }

        private void RegisterJavaScriptObject(string name, object obj)
        {
            string[] path = name.Split('.');

            string alias = string.Join('_', path);
            string protoName = string.Join(".__proto__.", path);

            // register object under alias
            _webView.RegisterJavascriptObject(alias, obj);

            // properly assign alias to target object
            _webView.ExecuteScript($"window.{protoName} = window.{alias}");
        }

        class Nx
        {

            public void playSystemSe(string eventName) { }

            public class PlayReport
            {

                public void setCounterSetIdentifier(long value) { }

                public void incrementCounter(long value) { }

            }

        }

    }

}
#endif
