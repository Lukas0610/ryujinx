using Ryujinx.Common.Utilities;

namespace Ryujinx.UI.Common.Configuration
{
    internal static class GameConfigurationFileFormatSettings
    {
        public static readonly GameConfigurationJsonSerializerContext SerializerContext = new(JsonHelper.GetDefaultSerializerOptions());
    }
}
