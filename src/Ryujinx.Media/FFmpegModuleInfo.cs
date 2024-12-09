using System;

namespace Ryujinx.Media
{

    [Flags]
    public enum FFmpegModuleInfo
    {
        Library = 1 << 1,
        Codecs = 1 << 2,
        Formats = 1 << 3
    }

}
