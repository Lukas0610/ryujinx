using Ryujinx.Common.Utilities;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{

    [JsonConverter(typeof(TypedStringEnumConverter<CaptureAudioCodecValue>))]
    public enum CaptureAudioCodecValue
    {
        Auto = 0,
        PCM = 1,
        AAC = 2,
        Opus = 3,
        Vorbis = 4,
    }

}
