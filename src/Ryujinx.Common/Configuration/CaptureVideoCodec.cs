using Ryujinx.Common.Utilities;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{

    [JsonConverter(typeof(TypedStringEnumConverter<CaptureVideoCodecValue>))]
    public enum CaptureVideoCodecValue
    {
        Auto = 0,
        H264 = 1,
        HEVC = 2,
    }

}
