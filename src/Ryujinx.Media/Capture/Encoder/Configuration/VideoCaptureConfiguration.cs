using System;

namespace Ryujinx.Media.Capture.Encoder.Configuration
{

    public sealed class VideoCaptureConfiguration : IEquatable<VideoCaptureConfiguration>
    {

        public int Width { get; set; }

        public int Height { get; set; }

        public MediaPixelFormat PixelFormat { get; set; }

        public bool Equals(VideoCaptureConfiguration other)
        {
            return (other != null) &&
                (Width == other.Width) &&
                (Height == other.Height) &&
                (PixelFormat == other.PixelFormat);
        }

    }

}
