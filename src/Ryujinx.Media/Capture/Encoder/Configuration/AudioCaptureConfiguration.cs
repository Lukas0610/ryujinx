using System;

namespace Ryujinx.Media.Capture.Encoder.Configuration
{

    public sealed class AudioCaptureConfiguration : IEquatable<AudioCaptureConfiguration>
    {

        public int SessionIndex { get; set; }

        public MediaSampleFormat SampleFormat { get; set; }

        public uint SampleRate { get; set; }

        public uint SampleCount { get; set; }

        public uint ChannelCount { get; set; }

        public bool Equals(AudioCaptureConfiguration other)
        {
            return (other != null) &&
                (SampleFormat == other.SampleFormat) &&
                (SessionIndex == other.SessionIndex) &&
                (SampleRate == other.SampleRate) &&
                (SampleCount == other.SampleCount) &&
                (ChannelCount == other.ChannelCount);
        }

    }

}
