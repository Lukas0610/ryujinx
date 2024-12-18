using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibHac.Ncm;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Utilities;
using Ryujinx.Media;
using Ryujinx.Modules;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using Ryujinx.UI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Main
{
    public partial class MainMenuBarView : UserControl
    {
        public MainWindow Window { get; private set; }
        public MainWindowViewModel ViewModel { get; private set; }

        public MainMenuBarView()
        {
            InitializeComponent();

            ToggleFileTypesMenuItem.ItemsSource = GenerateToggleFileTypeItems();
            ChangeLanguageMenuItem.ItemsSource = GenerateLanguageMenuItems();
        }

        private CheckBox[] GenerateToggleFileTypeItems()
        {
            List<CheckBox> checkBoxes = new();

            foreach (var item in Enum.GetValues(typeof(FileTypes)))
            {
                string fileName = Enum.GetName(typeof(FileTypes), item);
                checkBoxes.Add(new CheckBox
                {
                    Content = $".{fileName}",
                    IsChecked = ((FileTypes)item).GetConfigValue(ConfigurationState.Instance.UI.ShownFileTypes),
                    Command = MiniCommand.Create(() => Window.ToggleFileType(fileName)),
                });
            }

            return checkBoxes.ToArray();
        }

        private static MenuItem[] GenerateLanguageMenuItems()
        {
            List<MenuItem> menuItems = new();

            string localePath = "Ryujinx/Assets/Locales";
            string localeExt = ".json";

            string[] localesPath = EmbeddedResources.GetAllAvailableResources(localePath, localeExt);

            Array.Sort(localesPath);

            foreach (string locale in localesPath)
            {
                string languageCode = Path.GetFileNameWithoutExtension(locale).Split('.').Last();
                string languageJson = EmbeddedResources.ReadAllText($"{localePath}/{languageCode}{localeExt}");
                var strings = JsonHelper.Deserialize(languageJson, CommonJsonContext.Default.StringDictionary);

                if (!strings.TryGetValue("Language", out string languageName))
                {
                    languageName = languageCode;
                }

                MenuItem menuItem = new()
                {
                    Header = languageName,
                    Command = MiniCommand.Create(() =>
                    {
                        MainWindowViewModel.ChangeLanguage(languageCode);
                    }),
                };

                menuItems.Add(menuItem);
            }

            return menuItems.ToArray();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow window)
            {
                Window = window;
            }

            ViewModel = Window.ViewModel;
            DataContext = ViewModel;
        }

        private async void OpenApplicationSettings_Click(object sender, RoutedEventArgs e)
        {
            GameConfigurationState gameConfig = Window.ViewModel.AppHost?.GameConfig;
            GameConfigurationState appGameConfig = Window.ViewModel.AppHost?.ApplicationGameConfig;

            // As `gameConfig` may be the global game-config provided by  `ConfigurationState.Instance.Game`,
            // we should not let the user modify the global configuration by pressing on a button called
            // "Open Game-Settings", which obviously is going to be confusing.
            //
            // For that reason, the actual app-config was also provided to AppHost on creation, allowing us
            // to modify the actual per-game configuration if the active game-config is the global config.
            if (gameConfig != null && !gameConfig.IsGlobalState)
            {
                await Window.ViewModel.OpenApplicationSettings(gameConfig, true, false);
            }
            else if (appGameConfig != null && !appGameConfig.IsGlobalState)
            {
                await Window.ViewModel.OpenApplicationSettings(appGameConfig, true, true);
            }
        }

        private async void StopEmulation_Click(object sender, RoutedEventArgs e)
        {
            await Window.ViewModel.AppHost?.ShowExitPrompt();
        }

        private void PauseEmulation_Click(object sender, RoutedEventArgs e)
        {
            Window.ViewModel.AppHost?.Pause();
        }

        private void ResumeEmulation_Click(object sender, RoutedEventArgs e)
        {
            Window.ViewModel.AppHost?.Resume();
        }

        public async void OpenSettings(object sender, RoutedEventArgs e)
        {
            ConfigurationState config = ConfigurationState.Instance;
            GameConfigurationState gameConfig = config.Game;
            bool ingame = ViewModel.IsGameLoading || ViewModel.IsGameRunning;

            Window.SettingsWindow = new(config, gameConfig, ingame, false, Window.VirtualFileSystem, Window.ContentManager);

            await Window.SettingsWindow.ShowDialog(Window);

            Window.SettingsWindow = null;
        }

        public async void OpenMiiApplet(object sender, RoutedEventArgs e)
        {
            string contentPath = ViewModel.ContentManager.GetInstalledContentPath(0x0100000000001009, StorageId.BuiltInSystem, NcaContentType.Program);

            if (!string.IsNullOrEmpty(contentPath))
            {
                ApplicationData applicationData = new()
                {
                    Name = "miiEdit",
                    Id = 0x0100000000001009,
                    Path = contentPath,
                };

                await ViewModel.LoadApplication(applicationData, ViewModel.IsFullScreen || ViewModel.StartGamesInFullscreen);
            }
        }

        public async void OpenAmiiboWindow(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsAmiiboRequested)
            {
                return;
            }

            if (ViewModel.AppHost.Device.System.SearchingForAmiibo(out int deviceId))
            {
                string titleId = ViewModel.AppHost.Device.Processes.ActiveApplication.ProgramIdText.ToUpper();
                AmiiboWindow window = new(ViewModel.ShowAll, ViewModel.LastScannedAmiiboId, titleId);

                await window.ShowDialog(Window);

                if (window.IsScanned)
                {
                    ViewModel.ShowAll = window.ViewModel.ShowAllAmiibo;
                    ViewModel.LastScannedAmiiboId = window.ScannedAmiibo.GetId();

                    ViewModel.AppHost.Device.System.ScanAmiibo(deviceId, ViewModel.LastScannedAmiiboId, window.ViewModel.UseRandomUuid);
                }
            }
        }

        public async void OpenCheatManagerForCurrentApp(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsGameRunning)
            {
                return;
            }

            string name = ViewModel.AppHost.Device.Processes.ActiveApplication.ApplicationControlProperties.Title[(int)ViewModel.AppHost.Device.System.State.DesiredTitleLanguage].NameString.ToString();

            await new CheatWindow(
                Window.VirtualFileSystem,
                Window.HostFileSystem,
                Window.ViewModel.SelectedApplication.GameConfig,
                ViewModel.AppHost.Device.Processes.ActiveApplication.ProgramIdText,
                name,
                Window.ViewModel.SelectedApplication.Path).ShowDialog(Window);

            ViewModel.AppHost.Device.EnableCheats();
        }

        private void ScanAmiiboMenuItem_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is MenuItem)
            {
                ViewModel.IsAmiiboRequested = Window.ViewModel.AppHost.Device.System.SearchingForAmiibo(out _);
            }
        }

        private async void InstallFileTypes_Click(object sender, RoutedEventArgs e)
        {
            if (FileAssociationHelper.Install())
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogInstallFileTypesSuccessMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogInstallFileTypesErrorMessage]);
            }
        }

        private async void UninstallFileTypes_Click(object sender, RoutedEventArgs e)
        {
            if (FileAssociationHelper.Uninstall())
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogUninstallFileTypesSuccessMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogUninstallFileTypesErrorMessage]);
            }
        }

        private async void ShowFFmpegInformations_Click(object sender, RoutedEventArgs e)
        {
            if (!FFmpegModule.IsInitialized)
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogFFmpegNotInstalledMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
                return;
            }

            string infoText = FFmpegModule.BuildInfoText(FFmpegModuleInfo.Library | FFmpegModuleInfo.Codecs | FFmpegModuleInfo.Formats);
            await ContentDialogHelper.CreateDebugOutputDialog(infoText, LocaleManager.Instance[LocaleKeys.DialogShowFFmpegInformationsTitle]);
        }

        private async void DownloadFFmpegLibraries_Click(object sender, RoutedEventArgs e)
        {
            if (FFmpegModule.IsInitialized)
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesAlreadyInstalledMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
                return;
            }

            if (!FFmpegDownloader.CanDownloadOnCurrentPlatform())
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesPlatformNotSupportedMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
                return;
            }

            FFmpegDownloader downloader = new FFmpegDownloader();

            downloader.RunBackground(FFmpegDownloader.Source.Release);

            await ContentDialogHelper.ShowProgressDialog(
                LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesTitle],
                LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesTitle],
                downloader.ProgressReporter);

            if (downloader.WaitForBackgroundRun())
            {
                if (FFmpegModule.Initialize(Program.AppDataNativeRuntimesDirectory))
                {
                    ViewModel.RefreshFFmpegModuleState();
                    await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesSuccessMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
                }
                else
                {
                    await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesLoadingErrorMessage], string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
                }
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogDownloadFFmpegLibrariesDownloadErrorMessage]);
            }
        }

        private void OpenRuntimesDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(Program.AppDataNativeRuntimesDirectory))
            {
                Directory.CreateDirectory(Program.AppDataNativeRuntimesDirectory);
            }

            OpenHelper.OpenFolder(Program.AppDataNativeRuntimesDirectory);
        }

        private async void ChangeWindowSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                int height;
                int width;

                switch (item.Tag)
                {
                    case "720":
                        height = 720;
                        width = 1280;
                        break;

                    case "1080":
                        height = 1080;
                        width = 1920;
                        break;

                    default:
                        throw new ArgumentNullException($"Invalid Tag for {item}");
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ViewModel.WindowState = WindowState.Normal;

                    height += (int)Window.StatusBarHeight + (int)Window.MenuBarHeight;

                    Window.Arrange(new Rect(Window.Position.X, Window.Position.Y, width, height));
                });
            }
        }

        public async void CheckForUpdates(object sender, RoutedEventArgs e)
        {
            if (Updater.CanUpdate(true))
            {
                await Updater.BeginParse(Window, true);
            }
        }

        public async void OpenAboutWindow(object sender, RoutedEventArgs e)
        {
            await AboutWindow.Show();
        }

        public void CloseWindow(object sender, RoutedEventArgs e)
        {
            Window.Close();
        }
    }
}
