using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LibHac.Fs;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace Ryujinx.Ava.UI.Controls
{
    public class ApplicationContextMenu : MenuFlyout
    {
        public ApplicationContextMenu()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void ToggleFavorite_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                viewModel.SelectedApplication.Favorite = !viewModel.SelectedApplication.Favorite;

                ApplicationLibrary.LoadAndSaveMetaData(viewModel.SelectedApplication.IdString, appMetadata =>
                {
                    appMetadata.Favorite = viewModel.SelectedApplication.Favorite;
                });

                viewModel.RefreshView();
            }
        }

        public void OpenUserSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            if (sender is MenuItem { DataContext: MainWindowViewModel viewModel })
            {
                OpenSaveDirectory(viewModel, SaveDataType.Account, new UserId((ulong)viewModel.AccountManager.LastOpenedUser.UserId.High, (ulong)viewModel.AccountManager.LastOpenedUser.UserId.Low));
            }
        }

        public void OpenDeviceSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            OpenSaveDirectory(viewModel, SaveDataType.Device, default);
        }

        public void OpenBcatSaveDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            OpenSaveDirectory(viewModel, SaveDataType.Bcat, default);
        }

        private static void OpenSaveDirectory(MainWindowViewModel viewModel, SaveDataType saveDataType, UserId userId)
        {
            if (viewModel?.SelectedApplication != null)
            {
                var saveDataFilter = SaveDataFilter.Make(viewModel.SelectedApplication.Id, saveDataType, userId, saveDataId: default, index: default);

                ApplicationHelper.OpenSaveDir(in saveDataFilter, viewModel.SelectedApplication.Id, viewModel.SelectedApplication.ControlHolder, viewModel.SelectedApplication.Name);
            }
        }

        public async void OpenTitleUpdateManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await TitleUpdateWindow.Show(viewModel.VirtualFileSystem, viewModel.HostFileSystem, viewModel.SelectedApplication);
            }
        }

        public async void OpenDownloadableContentManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await DownloadableContentManagerWindow.Show(viewModel.VirtualFileSystem, viewModel.HostFileSystem, viewModel.SelectedApplication);
            }
        }

        public async void OpenCheatManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await new CheatWindow(
                    viewModel.VirtualFileSystem,
                    viewModel.HostFileSystem,
                    viewModel.SelectedApplication.IdString,
                    viewModel.SelectedApplication.Name,
                    viewModel.SelectedApplication.Path).ShowDialog(viewModel.TopLevel as Window);
            }
        }

        public void OpenModsDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string modsBasePath = ModLoader.GetModsBasePath();
                string titleModsPath = ModLoader.GetApplicationDir(modsBasePath, viewModel.SelectedApplication.IdString);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public void OpenSdModsDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string sdModsBasePath = ModLoader.GetSdModsBasePath();
                string titleModsPath = ModLoader.GetApplicationDir(sdModsBasePath, viewModel.SelectedApplication.IdString);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public async void OpenModManager_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ModManagerWindow.Show(viewModel.SelectedApplication.Id, viewModel.SelectedApplication.Name);
            }
        }

        public async void PurgePtcCache_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogWarning],
                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogPPTCDeletionMessage, viewModel.SelectedApplication.Name),
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ApplicationHelper.PurgePtcCache(viewModel.SelectedApplication.IdString);
                }
            }
        }

        public async void PurgeShaderCache_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogWarning],
                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogShaderDeletionMessage, viewModel.SelectedApplication.Name),
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ApplicationHelper.PurgeShaderCache(viewModel.SelectedApplication.IdString);
                }
            }
        }

        public void OpenPtcDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string ptcDir = Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "cpu");
                string mainDir = Path.Combine(ptcDir, "0");
                string backupDir = Path.Combine(ptcDir, "1");

                if (!Directory.Exists(ptcDir))
                {
                    Directory.CreateDirectory(ptcDir);
                    Directory.CreateDirectory(mainDir);
                    Directory.CreateDirectory(backupDir);
                }

                OpenHelper.OpenFolder(ptcDir);
            }
        }

        public void OpenShaderCacheDirectory_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                string shaderCacheDir = Path.Combine(AppDataManager.GamesDirPath, viewModel.SelectedApplication.IdString, "cache", "shader");

                if (!Directory.Exists(shaderCacheDir))
                {
                    Directory.CreateDirectory(shaderCacheDir);
                }

                OpenHelper.OpenFolder(shaderCacheDir);
            }
        }

        public async void ExtractApplicationExeFs_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Code,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public async void ExtractApplicationRomFs_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Data,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public async void ExtractApplicationLogo_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await ApplicationHelper.ExtractSection(
                    viewModel.StorageProvider,
                    NcaSectionType.Logo,
                    viewModel.SelectedApplication.Path,
                    viewModel.SelectedApplication.Name);
            }
        }

        public void CreateApplicationShortcut_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                ApplicationData selectedApplication = viewModel.SelectedApplication;
                ShortcutHelper.CreateAppShortcut(selectedApplication.Path, selectedApplication.Name, selectedApplication.IdString, selectedApplication.Icon);
            }
        }

        public async void RunApplication_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await viewModel.LoadApplication(viewModel.SelectedApplication);
            }
        }

        public async void TrimXCI_Click(object sender, RoutedEventArgs args)
        {
            var viewModel = (sender as MenuItem)?.DataContext as MainWindowViewModel;

            if (viewModel?.SelectedApplication != null)
            {
                await viewModel.TrimXCIFile(viewModel.SelectedApplication.Path);
            }
        }
    }
}
