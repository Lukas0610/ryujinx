using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.Views.Main
{
    public partial class MainStatusBarView : UserControl
    {
        public MainWindow Window;

        public MainStatusBarView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow window)
            {
                Window = window;
            }

            DataContext = Window.ViewModel;
        }

        private void VsyncStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            Window.ViewModel.AppHost.ToggleVSync();

            Logger.Info?.Print(LogClass.Application, $"VSync toggled to: {Window.ViewModel.AppHost.Device.EnableDeviceVsync}");
        }

        private void DockedStatus_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            GameConfigurationState gameConfig = Window.ViewModel.AppHost.GameConfig;

            gameConfig.System.EnableDockedMode.Value = !gameConfig.System.EnableDockedMode.Value;

            Window.ViewModel.AppHost.SaveGameConfig();
        }

        private void AspectRatioStatus_OnClick(object sender, RoutedEventArgs e)
        {
            GameConfigurationState gameConfig = Window.ViewModel.AppHost.GameConfig;
            AspectRatio aspectRatio = gameConfig.Graphics.AspectRatio.Value;

            gameConfig.Graphics.AspectRatio.Value = (int)aspectRatio + 1 > Enum.GetNames(typeof(AspectRatio)).Length - 1 ? AspectRatio.Fixed4x3 : aspectRatio + 1;

            Window.ViewModel.AppHost.SaveGameConfig();
        }

        private void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            Window.LoadApplications();
        }

        private void VolumeStatus_OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            // Change the volume by 5% at a time
            float newValue = Window.ViewModel.Volume + (float)e.Delta.Y * 0.05f;

            Window.ViewModel.Volume = newValue switch
            {
                < 0 => 0,
                > 1 => 1,
                _ => newValue,
            };

            e.Handled = true;
        }
    }
}
