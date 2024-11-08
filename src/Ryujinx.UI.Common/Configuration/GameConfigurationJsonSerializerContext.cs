using System.Text.Json.Serialization;

namespace Ryujinx.UI.Common.Configuration
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(GameConfigurationFileFormat))]
    internal partial class GameConfigurationJsonSerializerContext : JsonSerializerContext
    {
    }
}
