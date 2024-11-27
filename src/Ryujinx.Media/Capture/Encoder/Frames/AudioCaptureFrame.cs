namespace Ryujinx.Media.Capture.Encoder.Frames
{

    public sealed class AudioCaptureFrame : GenericCaptureFrame
    {

        public int SessionIndex { get; set; }

        public int FrameBufferSampleCount { get; set; }

    }

}
