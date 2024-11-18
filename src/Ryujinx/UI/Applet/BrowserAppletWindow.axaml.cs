using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.HOS.Applets.Browser;

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

        public bool Navigate(BrowserUIArgs args)
        {
            // Info-Page path for Mario Kart 8: "htmlcontents.htdocs/html/USen/index.html"
            return true;
        }

    }
}
