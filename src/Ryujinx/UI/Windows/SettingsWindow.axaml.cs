using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class SettingsWindow : StyleableWindow
    {

        private IObjectObserver[] _gameBootTimeConfigurationObservers;

        internal SettingsViewModel ViewModel { get; set; }

        public SettingsWindow(ConfigurationState config, GameConfigurationState gameConfig, bool ingame, VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            Title = $"Ryujinx {Program.Version} - {LocaleManager.Instance[LocaleKeys.Settings]}";

            ViewModel = new SettingsViewModel(config, gameConfig, ingame, virtualFileSystem, contentManager);
            DataContext = ViewModel;

            ViewModel.CloseWindow += Close;
            ViewModel.SaveSettingsEvent += SaveSettings;

            InitializeComponent();

            CreateGameBootTimeConfigurationObservers(gameConfig);
            Load();
        }

        public SettingsWindow()
        {
            ConfigurationState config = ConfigurationState.Instance;

            ViewModel = new SettingsViewModel(config, config.Game, false);
            DataContext = ViewModel;

            InitializeComponent();

            CreateGameBootTimeConfigurationObservers(config.Game);
            Load();
        }

        public async void SaveSettings()
        {
            if (ViewModel.IsIngame)
            {
                bool hasAnyGameBootTimeConfigurationChanged = _gameBootTimeConfigurationObservers.Any(x => x.HasChanged);

                if (hasAnyGameBootTimeConfigurationChanged)
                {
                    await ContentDialogHelper.CreateInfoDialog(
                        LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogGameBootTimeSettingsChangedWhileIngame),
                        LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogGameBootTimeSettingsChangedWhileIngameMessage),
                        LocaleManager.Instance[LocaleKeys.InputDialogOk],
                        "",
                        LocaleManager.Instance[LocaleKeys.RyujinxInfo]);
                }
            }

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

            Array.ForEach(_gameBootTimeConfigurationObservers, static x => x.Destroy());

            base.OnClosing(e);
        }

        private void CreateGameBootTimeConfigurationObservers(GameConfigurationState gameConfig)
        {
            _gameBootTimeConfigurationObservers = new IObjectObserver[]
            {
                ReactiveObjectObserver.Create(gameConfig.Graphics.GraphicsBackend),
                ReactiveObjectObserver.Create(gameConfig.Graphics.BackendThreading),
                ReactiveObjectObserver.Create(gameConfig.System.AudioBackend),
                ReactiveObjectObserver.Create(gameConfig.System.MemoryConfiguration),
                ReactiveObjectObserver.Create(gameConfig.System.Language),
                ReactiveObjectObserver.Create(gameConfig.System.Region),
                ReactiveObjectObserver.Create(gameConfig.Graphics.EnableVsync),
                ReactiveObjectObserver.Create(gameConfig.System.EnablePtc),
                ReactiveObjectObserver.Create(gameConfig.System.UseStreamingPtc),
                ReactiveObjectObserver.Create(gameConfig.System.EnableInternetAccess),
                ReactiveObjectObserver.Create(gameConfig.System.EnableFsIntegrityChecks),
                ReactiveObjectObserver.Create(gameConfig.System.EnableHostFsBuffering),
                ReactiveObjectObserver.Create(gameConfig.System.EnableHostFsBufferingPrefetch),
                ReactiveObjectObserver.Create(gameConfig.System.HostFsBufferingMaxCacheSize),
                // ReactiveObjectObserver.Create(gameConfig.System.SystemTimeOffset), -- ignore tz offset as this keeps changing
                ReactiveObjectObserver.Create(gameConfig.System.TimeZone),
                ReactiveObjectObserver.Create(gameConfig.System.MemoryManagerMode),
                ReactiveObjectObserver.Create(gameConfig.System.UseHypervisor),
                ReactiveObjectObserver.Create(gameConfig.System.UseSparseAddressTable),
                ReactiveObjectObserver.Create(gameConfig.System.HleKernelThreadsCPUSet),
                ReactiveObjectObserver.Create(gameConfig.System.HleKernelThreadsCPUSetStaticCore),
                ReactiveObjectObserver.Create(gameConfig.System.PtcBackgroundThreadsCPUSet),
                ReactiveObjectObserver.Create(gameConfig.System.PtcBackgroundThreadCount),
            };
        }

    }
}
