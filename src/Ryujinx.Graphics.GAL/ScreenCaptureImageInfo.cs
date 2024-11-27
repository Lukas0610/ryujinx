using Ryujinx.Common.Buffers;

namespace Ryujinx.Graphics.GAL
{
    public readonly struct ScreenCaptureImageInfo
    {
        public ScreenCaptureImageInfo(IBuffer rentedBuffer, int width, int height, bool isBgra, bool flipX, bool flipY)
        {
            FrameBuffer = rentedBuffer;
            Width = width;
            Height = height;
            IsBgra = isBgra;
            FlipX = flipX;
            FlipY = flipY;
        }

        public IBuffer FrameBuffer { get; }
        public int Width { get; }
        public int Height { get; }
        public int DataLength { get; }
        public bool IsBgra { get; }
        public bool FlipX { get; }
        public bool FlipY { get; }
    }
}
