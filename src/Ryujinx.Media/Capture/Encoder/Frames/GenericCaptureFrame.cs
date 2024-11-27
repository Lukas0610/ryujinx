using Ryujinx.Common.Buffers;

namespace Ryujinx.Media.Capture.Encoder.Frames
{

    public abstract class GenericCaptureFrame
    {

        public IBuffer Buffer { get; set; }

        internal long PresentationTimeStamp { get; set; }

        internal protected GenericCaptureFrame() { }

    }

}
