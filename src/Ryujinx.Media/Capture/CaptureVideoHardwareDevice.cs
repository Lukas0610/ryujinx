using System;

namespace Ryujinx.Media.Capture
{

    [Flags]
    public enum CaptureVideoHardwareDevice
    {
        None = 0,
        NVENC = 1 << 1,
        QSV = 1 << 2,
        Vulkan = 1 << 3,
    }

}
