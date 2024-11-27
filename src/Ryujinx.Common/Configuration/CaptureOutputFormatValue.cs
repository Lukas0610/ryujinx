using Ryujinx.Common.Utilities;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration
{

    [JsonConverter(typeof(TypedStringEnumConverter<CaptureOutputFormatValue>))]
    public enum CaptureOutputFormatValue
    {
        Auto = 0,
        MKV = 1,
        MPEGTS = 2,
    }

}
