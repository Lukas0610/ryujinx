using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace Ryujinx.Ava.UI.Helpers
{
    public static class ContentDialogHelper
    {
        private static bool _isChoiceDialogOpen;
        private static ContentDialogOverlayWindow _contentDialogOverlayWindow;

        private async static Task<UserResult> ShowContentDialog(
             string title,
             Control content,
             string primaryButton,
             string secondaryButton,
             string closeButton,
             UserResult primaryButtonResult = UserResult.Ok,
             ManualResetEvent deferResetEvent = null,
             TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> deferCloseAction = null,
             double? contentDialogMaxWidth = null)
        {
            UserResult result = UserResult.None;

            ContentDialog contentDialog = new()
            {
                Title = title,
                PrimaryButtonText = primaryButton,
                SecondaryButtonText = secondaryButton,
                CloseButtonText = closeButton,
                Content = content,
                PrimaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = primaryButtonResult;
                }),
            };

            if (contentDialogMaxWidth.HasValue)
            {
                contentDialog.Styles.Resources["ContentDialogMaxWidth"] = contentDialogMaxWidth.Value + 50;
            }

            contentDialog.SecondaryButtonCommand = MiniCommand.Create(() =>
            {
                result = UserResult.No;
                contentDialog.PrimaryButtonClick -= deferCloseAction;
            });

            contentDialog.CloseButtonCommand = MiniCommand.Create(() =>
            {
                result = UserResult.Cancel;
                contentDialog.PrimaryButtonClick -= deferCloseAction;
            });

            if (deferResetEvent != null)
            {
                contentDialog.PrimaryButtonClick += deferCloseAction;
            }

            await ShowAsync(contentDialog);

            return result;
        }

        public async static Task<UserResult> ShowProgressDialog(
            string title,
            string defaultText,
            UIProgressReporter progressReporter)
        {
            UserResult result = UserResult.None;

            Grid content = new()
            {
                RowDefinitions = new RowDefinitions { new(), new() },
                ColumnDefinitions = new ColumnDefinitions { new(GridLength.Auto), new(GridLength.Star) },

                MinWidth = 500,
                MinHeight = 80,
            };

            // Icon
            SymbolIcon icon = new()
            {
                Symbol = Symbol.Download,
                Margin = new Thickness(10),
                FontSize = 40,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(icon, 0);
            Grid.SetRowSpan(icon, 2);
            Grid.SetRow(icon, 0);
            content.Children.Add(icon);

            // Text
            TextBlock textLabel = new()
            {
                Text = defaultText ?? "",
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 450,
            };

            Grid.SetColumn(textLabel, 1);
            Grid.SetRow(textLabel, 0);
            content.Children.Add(textLabel);

            //
            // Progress
            //
            Grid progressContent = new()
            {
                RowDefinitions = new RowDefinitions { new(), new() },
                ColumnDefinitions = new ColumnDefinitions { new(GridLength.Star), new(GridLength.Star) }
            };

            Grid.SetColumn(progressContent, 1);
            Grid.SetRow(progressContent, 1);
            content.Children.Add(progressContent);

            // Value Label
            TextBlock progressValueLabel = new()
            {
                Text = "0 / 0",
                Margin = new Thickness(5),
                TextAlignment = TextAlignment.Left,
                TextWrapping = TextWrapping.Wrap,
            };

            Grid.SetColumn(progressValueLabel, 0);
            Grid.SetRow(progressValueLabel, 0);
            progressContent.Children.Add(progressValueLabel);

            // Speed Label
            TextBlock progressSpeedLabel = new()
            {
                Text = "0",
                Margin = new Thickness(5),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap,
            };

            Grid.SetColumn(progressSpeedLabel, 1);
            Grid.SetRow(progressSpeedLabel, 0);
            progressContent.Children.Add(progressSpeedLabel);

            // Progress-Bar
            ProgressBar progressBar = new()
            {
                Margin = new Thickness(5),
            };

            Grid.SetColumn(progressBar, 0);
            Grid.SetColumnSpan(progressBar, 2);
            Grid.SetRow(progressBar, 1);
            progressContent.Children.Add(progressBar);

            //
            // Dialog
            //
            ContentDialog contentDialog = new()
            {
                Title = title,
                Content = content,
                PrimaryButtonText = LocaleManager.Instance[LocaleKeys.InputDialogCancel],
                PrimaryButtonCommand = MiniCommand.Create(() =>
                {
                    progressReporter.Cancel();
                    result = UserResult.Cancel;
                }),
            };

            progressReporter.ProgressChanged += (s, e) =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    textLabel.Text = !string.IsNullOrEmpty(e.Text) ? e.Text : " ";

                    progressValueLabel.IsVisible = e.HasTotal;
                    progressSpeedLabel.IsVisible = e.HasSpeed;

                    if (e.HasTotal)
                    {
                        switch (progressReporter.Type)
                        {
                            case ProgressType.Amount:
                                progressValueLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatAmountCurrentWithTotal, e.Current, e.Total);
                                break;
                            case ProgressType.Bytes:
                                string current = ReadableStringUtils.FormatSize(e.Current);
                                string total = ReadableStringUtils.FormatSize(e.Total);

                                progressValueLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatBytesCurrentWithTotal, current, total);
                                break;
                        }

                        progressBar.IsIndeterminate = false;
                        progressBar.Maximum = e.Total;
                        progressBar.Value = e.Current;
                    }
                    else
                    {
                        if (e.HasCurrent)
                        {
                            switch (progressReporter.Type)
                            {
                                case ProgressType.Amount:
                                    progressValueLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatAmountCurrentOnly, e.Current);
                                    break;
                                case ProgressType.Bytes:
                                    string current = ReadableStringUtils.FormatSize(e.Current);

                                    progressValueLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatBytesCurrentOnly, current);
                                    break;
                            }
                        }
                        else
                        {
                            progressValueLabel.Text = " ";
                        }

                        progressBar.IsIndeterminate = true;
                        progressBar.Maximum = 100;
                        progressBar.Value = 50;
                    }

                    if (e.HasSpeed)
                    {
                        switch (progressReporter.Type)
                        {
                            case ProgressType.Amount:
                                progressSpeedLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatAmountSpeed, e.Speed);
                                break;
                            case ProgressType.Bytes:
                                string speed = ReadableStringUtils.FormatSize(e.Speed);

                                progressSpeedLabel.Text = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProgressFormatBytesSpeed, speed);
                                break;
                        }
                    }
                    else
                    {
                        progressSpeedLabel.Text = " ";
                    }
                });
            };

            progressReporter.Finished += (s, e) =>
            {
                Dispatcher.UIThread.Invoke(() => contentDialog.Hide(ContentDialogResult.None));
            };

            await ShowAsync(contentDialog);

            return result;
        }

        public async static Task<UserResult> ShowTextDialog(
            string title,
            string primaryText,
            string secondaryText,
            string primaryButton,
            string secondaryButton,
            string closeButton,
            int iconSymbol,
            UserResult primaryButtonResult = UserResult.Ok,
            ManualResetEvent deferResetEvent = null,
            TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> deferCloseAction = null)
        {
            Grid content = CreateTextDialogContent(primaryText, secondaryText, iconSymbol);

            return await ShowContentDialog(title, content, primaryButton, secondaryButton, closeButton, primaryButtonResult, deferResetEvent, deferCloseAction);
        }

        public async static Task<UserResult> ShowDeferredContentDialog(
            StyleableWindow window,
            string title,
            string primaryText,
            string secondaryText,
            string primaryButton,
            string secondaryButton,
            string closeButton,
            int iconSymbol,
            ManualResetEvent deferResetEvent,
            Func<Window, Task> doWhileDeferred = null)
        {
            bool startedDeferring = false;

            return await ShowTextDialog(
                title,
                primaryText,
                secondaryText,
                primaryButton,
                secondaryButton,
                closeButton,
                iconSymbol,
                primaryButton == LocaleManager.Instance[LocaleKeys.InputDialogYes] ? UserResult.Yes : UserResult.Ok,
                deferResetEvent,
                DeferClose);

            async void DeferClose(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            {
                if (startedDeferring)
                {
                    return;
                }

                sender.PrimaryButtonClick -= DeferClose;

                startedDeferring = true;

                var deferral = args.GetDeferral();

                sender.PrimaryButtonClick -= DeferClose;

                _ = Task.Run(() =>
                {
                    deferResetEvent.WaitOne();

                    Dispatcher.UIThread.Post(() =>
                    {
                        deferral.Complete();
                    });
                });

                if (doWhileDeferred != null)
                {
                    await doWhileDeferred(window);

                    deferResetEvent.Set();
                }
            }
        }

        private static Grid CreateTextDialogContent(string primaryText, string secondaryText, int symbol)
        {
            Grid content = new()
            {
                RowDefinitions = new RowDefinitions { new(), new() },
                ColumnDefinitions = new ColumnDefinitions { new(GridLength.Auto), new() },

                MinHeight = 80,
            };

            SymbolIcon icon = new()
            {
                Symbol = (Symbol)symbol,
                Margin = new Thickness(10),
                FontSize = 40,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(icon, 0);
            Grid.SetRowSpan(icon, 2);
            Grid.SetRow(icon, 0);

            TextBlock primaryLabel = new()
            {
                Text = primaryText,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 450,
            };

            TextBlock secondaryLabel = new()
            {
                Text = secondaryText,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 450,
            };

            Grid.SetColumn(primaryLabel, 1);
            Grid.SetColumn(secondaryLabel, 1);
            Grid.SetRow(primaryLabel, 0);
            Grid.SetRow(secondaryLabel, 1);

            content.Children.Add(icon);
            content.Children.Add(primaryLabel);
            content.Children.Add(secondaryLabel);

            return content;
        }

        public static async Task<UserResult> CreateInfoDialog(
            string primary,
            string secondaryText,
            string acceptButton,
            string closeButton,
            string title)
        {
            return await ShowTextDialog(
                title,
                primary,
                secondaryText,
                acceptButton,
                "",
                closeButton,
                (int)Symbol.Important);
        }

        public static async Task<UserResult> CreateDebugOutputDialog(
            string text,
            string title)
        {
            Grid content = new()
            {
                RowDefinitions = new RowDefinitions { new(GridLength.Auto), new() },
                ColumnDefinitions = new ColumnDefinitions { new(GridLength.Star) },

                Width = 850,
                MinHeight = 80,
            };

            Button clipboardButton = new()
            {
                Content = LocaleManager.Instance[LocaleKeys.DialogDebugOutputCopyToClipboard],
                Margin = new Thickness(5),
            };

            clipboardButton.Click += async (s, e) =>
            {
                await ClipboardService.SetTextAsync(text);
            };

            Grid.SetColumn(clipboardButton, 0);
            Grid.SetRow(clipboardButton, 0);
            content.Children.Add(clipboardButton);

            TextBlock textBlock = new()
            {
                Text = text,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = FontFamily.Parse("Courier New"),
            };

            Grid.SetColumn(textBlock, 0);
            Grid.SetRow(textBlock, 1);
            content.Children.Add(textBlock);

            return await ShowContentDialog(title, content, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty, contentDialogMaxWidth: 850);
        }

        internal static async Task<UserResult> CreateConfirmationDialog(
            string primaryText,
            string secondaryText,
            string acceptButtonText,
            string cancelButtonText,
            string title,
            UserResult primaryButtonResult = UserResult.Yes)
        {
            return await ShowTextDialog(
                string.IsNullOrWhiteSpace(title) ? LocaleManager.Instance[LocaleKeys.DialogConfirmationTitle] : title,
                primaryText,
                secondaryText,
                acceptButtonText,
                "",
                cancelButtonText,
                (int)Symbol.Help,
                primaryButtonResult);
        }

        internal static async Task<UserResult> CreateConfirmationDialogExtended(
            string primaryText,
            string secondaryText,
            string acceptButtonText,
            string noacceptButtonText,
            string cancelButtonText,
            string title,
            UserResult primaryButtonResult = UserResult.Yes)
            => await ShowTextDialog(
                string.IsNullOrWhiteSpace(title) ? LocaleManager.Instance[LocaleKeys.DialogConfirmationTitle] : title,
                primaryText,
                secondaryText,
                acceptButtonText,
                noacceptButtonText,
                cancelButtonText,
                (int)Symbol.Help,
                primaryButtonResult);

        internal static async Task<UserResult> CreateLocalizedConfirmationDialog(string primaryText, string secondaryText)
            => await CreateConfirmationDialog(
                primaryText,
                secondaryText,
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

        internal static async Task CreateUpdaterInfoDialog(string primary, string secondaryText)
        {
            await ShowTextDialog(
                LocaleManager.Instance[LocaleKeys.DialogUpdaterTitle],
                primary,
                secondaryText,
                "",
                "",
                LocaleManager.Instance[LocaleKeys.InputDialogOk],
                (int)Symbol.Important);
        }

        internal static async Task CreateWarningDialog(string primary, string secondaryText)
        {
            await ShowTextDialog(
                LocaleManager.Instance[LocaleKeys.DialogWarningTitle],
                primary,
                secondaryText,
                "",
                "",
                LocaleManager.Instance[LocaleKeys.InputDialogOk],
                (int)Symbol.Important);
        }

        internal static async Task CreateErrorDialog(string errorMessage, string secondaryErrorMessage = "")
        {
            Logger.Error?.Print(LogClass.Application, errorMessage);

            await ShowTextDialog(
                LocaleManager.Instance[LocaleKeys.DialogErrorTitle],
                LocaleManager.Instance[LocaleKeys.DialogErrorMessage],
                errorMessage,
                secondaryErrorMessage,
                "",
                LocaleManager.Instance[LocaleKeys.InputDialogOk],
                (int)Symbol.Dismiss);
        }

        internal static async Task<bool> CreateChoiceDialog(string title, string primary, string secondaryText)
        {
            if (_isChoiceDialogOpen)
            {
                return false;
            }

            _isChoiceDialogOpen = true;

            UserResult response = await ShowTextDialog(
                title,
                primary,
                secondaryText,
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                "",
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                (int)Symbol.Help,
                UserResult.Yes);

            _isChoiceDialogOpen = false;

            return response == UserResult.Yes;
        }

        internal static async Task<bool> CreateExitDialog()
        {
            return await CreateChoiceDialog(
                LocaleManager.Instance[LocaleKeys.DialogExitTitle],
                LocaleManager.Instance[LocaleKeys.DialogExitMessage],
                LocaleManager.Instance[LocaleKeys.DialogExitSubMessage]);
        }

        internal static async Task<bool> CreateStopEmulationDialog()
        {
            return await CreateChoiceDialog(
                LocaleManager.Instance[LocaleKeys.DialogStopEmulationTitle],
                LocaleManager.Instance[LocaleKeys.DialogStopEmulationMessage],
                LocaleManager.Instance[LocaleKeys.DialogExitSubMessage]);
        }

        public static async Task<ContentDialogResult> ShowAsync(ContentDialog contentDialog)
        {
            ContentDialogResult result;
            bool isTopDialog = true;

            Window parent = GetMainWindow();

            if (_contentDialogOverlayWindow != null)
            {
                isTopDialog = false;
            }

            if (parent is MainWindow window)
            {
                parent.Activate();

                _contentDialogOverlayWindow = new ContentDialogOverlayWindow
                {
                    Height = parent.Bounds.Height,
                    Width = parent.Bounds.Width,
                    Position = parent.PointToScreen(new Point()),
                    ShowInTaskbar = false,
                };

                parent.PositionChanged += OverlayOnPositionChanged;

                void OverlayOnPositionChanged(object sender, PixelPointEventArgs e)
                {
                    if (_contentDialogOverlayWindow is null)
                    {
                        return;
                    }

                    _contentDialogOverlayWindow.Position = parent.PointToScreen(new Point());
                }

                _contentDialogOverlayWindow.ContentDialog = contentDialog;

                bool opened = false;

                _contentDialogOverlayWindow.Opened += OverlayOnActivated;

                async void OverlayOnActivated(object sender, EventArgs e)
                {
                    if (opened)
                    {
                        return;
                    }

                    opened = true;

                    _contentDialogOverlayWindow.Position = parent.PointToScreen(new Point());

                    result = await ShowDialog();
                }

                result = await _contentDialogOverlayWindow.ShowDialog<ContentDialogResult>(parent);
            }
            else
            {
                result = await ShowDialog();
            }

            async Task<ContentDialogResult> ShowDialog()
            {
                if (_contentDialogOverlayWindow is not null)
                {
                    result = await contentDialog.ShowAsync(_contentDialogOverlayWindow);

                    _contentDialogOverlayWindow!.Close();
                }
                else
                {
                    result = ContentDialogResult.None;

                    Logger.Warning?.Print(LogClass.UI, "Content dialog overlay failed to populate. Default value has been returned.");
                }

                return result;
            }

            if (isTopDialog && _contentDialogOverlayWindow is not null)
            {
                _contentDialogOverlayWindow.Content = null;
                _contentDialogOverlayWindow.Close();
                _contentDialogOverlayWindow = null;
            }

            return result;
        }

        public static Task ShowWindowAsync(Window dialogWindow, Window mainWindow = null)
        {
            mainWindow ??= GetMainWindow();

            return dialogWindow.ShowDialog(_contentDialogOverlayWindow ?? mainWindow);
        }

        private static Window GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime al)
            {
                foreach (Window item in al.Windows)
                {
                    if (item is MainWindow window)
                    {
                        return window;
                    }
                }
            }

            return null;
        }
    }
}
