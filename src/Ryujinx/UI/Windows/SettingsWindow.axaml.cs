using Avalonia.Controls;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class SettingsWindow : StyleableWindow
    {
        internal SettingsViewModel ViewModel { get; set; }

        public SettingsWindow(ConfigurationState config, GameConfigurationState gameConfig, bool ingame, VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            Title = $"Ryujinx {Program.Version} - {LocaleManager.Instance[LocaleKeys.Settings]}";

            ViewModel = new SettingsViewModel(config, gameConfig, ingame, virtualFileSystem, contentManager);
            DataContext = ViewModel;

            ViewModel.CloseWindow += Close;
            ViewModel.SaveSettingsEvent += SaveSettings;

            InitializeComponent();
            Load();
        }

        public SettingsWindow()
        {
            ConfigurationState config = ConfigurationState.Instance;

            ViewModel = new SettingsViewModel(config, config.Game, false);
            DataContext = ViewModel;

            InitializeComponent();
            Load();
        }

        public void SaveSettings()
        {
            InputPage.InputView?.SaveCurrentProfile();

            if (Owner is MainWindow window && ViewModel.DirectoryChanged)
            {
                window.LoadApplications();
            }
        }

        private void Load()
        {
            InputPage.Initialize(ViewModel.GameConfig);

            Pages.Children.Clear();
            NavPanel.SelectionChanged += NavPanelOnSelectionChanged;

            IEnumerable<NavigationViewItem> menuItems = NavPanel.MenuItems.Cast<NavigationViewItem>();

            if (ViewModel.IsGameConfig)
            {
                NavPanel.SelectedItem = menuItems.First(x => x.Tag?.ToString() == "SystemPage");
            }
            else
            {
                NavPanel.SelectedItem = menuItems.First(x => x.Tag?.ToString() == "UiPage");
            }
        }

        private void NavPanelOnSelectionChanged(object sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem navItem && navItem.Tag is not null)
            {
                switch (navItem.Tag.ToString())
                {
                    case "UiPage":
                        UiPage.ViewModel = ViewModel;
                        NavPanel.Content = UiPage;
                        break;
                    case "InputPage":
                        NavPanel.Content = InputPage;
                        break;
                    case "HotkeysPage":
                        NavPanel.Content = HotkeysPage;
                        break;
                    case "SystemPage":
                        SystemPage.ViewModel = ViewModel;
                        NavPanel.Content = SystemPage;
                        break;
                    case "CpuPage":
                        NavPanel.Content = CpuPage;
                        break;
                    case "GraphicsPage":
                        NavPanel.Content = GraphicsPage;
                        break;
                    case "AudioPage":
                        NavPanel.Content = AudioPage;
                        break;
                    case "NetworkPage":
                        NavPanel.Content = NetworkPage;
                        break;
                    case "LoggingPage":
                        NavPanel.Content = LoggingPage;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                NavPanel.Content = null;
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            HotkeysPage.Dispose();
            InputPage.Dispose();
            base.OnClosing(e);
        }
    }
}
