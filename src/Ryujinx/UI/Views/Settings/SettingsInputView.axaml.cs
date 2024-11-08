using Avalonia.Controls;
using Ryujinx.UI.Common.Configuration;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        public SettingsInputView()
        {
            InitializeComponent();
        }

        public void Initialize(GameConfigurationState gameConfig)
        {
            InputView.Initialize(gameConfig);
        }

        public void Dispose()
        {
            InputView.Dispose();
        }
    }
}
