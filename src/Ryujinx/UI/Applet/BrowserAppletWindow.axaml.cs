using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.HOS.Applets.Browser;
using System;
using System.IO;
using System.Text;

namespace Ryujinx.Ava.UI.Applet
{
    internal partial class BrowserAppletWindow : StyleableWindow
    {

        private readonly AppHost _appHost;

        public BrowserAppletWindow(AppHost appHost)
        {
            _appHost = appHost;

            DataContext = this;
            InitializeComponent();
        }

        public void ShowWebViewDevTools()
        {
            WebView.ShowDeveloperTools();
        }

        public bool Navigate(BrowserUIArgs args)
        {
            RegisterJavaScriptObject("nx", new Nx());

            // @TODO: Allows JS execution to continue (in Mario Kart 8 info-page),
            //        but causes loads of other JS errors (seemingly unrelated)
            // RegisterJavaScriptObject("nx.playReport", new Nx.PlayReport());

            if (args.DocumentPath.StartsWith("https://") || args.DocumentPath.StartsWith("http://"))
            {
                WebView.LoadUrl(args.DocumentPath);
            }
            else if (args.DocumentKind == DocumentKind.OfflineHtmlPage)
            {
                string fullDocumentPath = $"local:///html-document/{args.DocumentPath}";

                WebView.BeforeResourceLoad += HandleBeforeResourceLoadForOfflineHtmlPage;

                WebView.LoadUrl(fullDocumentPath);
            }

            return true;
        }

        private void HandleBeforeResourceLoadForOfflineHtmlPage(WebViewControl.ResourceHandler resourceHandler)
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
            WebView.RegisterJavascriptObject(alias, obj);

            // properly assign alias to target object
            WebView.ExecuteScript($"window.{protoName} = window.{alias}");
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
