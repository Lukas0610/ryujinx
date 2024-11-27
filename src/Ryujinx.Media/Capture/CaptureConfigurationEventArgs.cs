using System;

namespace Ryujinx.Media.Capture
{

    public sealed class CaptureConfigurationEventArgs : EventArgs
    {

        /// <summary>
        /// The file the capture-output is written to
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Format of the capture output file
        /// </summary>
        public CaptureOutputFormat Format { get; set; }

        /// <summary>
        /// The used video-codec for the capture
        /// </summary>
        public CaptureVideoCodec VideoCodec { get; set; }

        /// <summary>
        /// Width to scale the video-output to.
        /// Set to <c>0</c> to disable scaling, set to <c>-1</c> to keep the original aspect ratio.
        /// </summary>
        public int VideoScaleWidth { get; set; }

        /// <summary>
        /// Height to scale the video-output to.
        /// Set to <c>0</c> to disable scaling, set to <c>-1</c> to keep the original aspect ratio.
        /// </summary>
        public int VideoScaleHeight { get; set; }

        /// <summary>
        /// Whether to control quality by bitrate
        /// </summary>
        public bool VideoUseBitrate { get; set; }

        /// <summary>
        /// The target-bitrate of the video encoding
        /// </summary>
        /// <remarks>
        /// First quality-option to be taken into account (Highest priority)
        /// </remarks>
        public long VideoBitrate { get; set; }

        /// <summary>
        /// Whether to control quality by a codec-dependent level
        /// </summary>
        public bool VideoUseQualityLevel { get; set; }

        /// <summary>
        /// The level of quality (depending on the selected codec)
        /// </summary>
        /// <remarks>
        /// Second quality-option to be taken into account
        /// </remarks>
        public int VideoQualityLevel { get; set; }

        /// <summary>
        /// Whether to try and encode video-frames without loss of image-information
        /// </summary>
        /// <remarks>
        /// Third quality-option to be taken into account (Lowest priority)
        /// </remarks>
        public bool VideoUseLossless { get; set; }

        /// <summary>
        /// The used audio-codec for the capture
        /// </summary>
        public CaptureAudioCodec AudioCodec { get; set; }

        /// <summary>
        /// The target-bitrate of the audio encoding
        /// </summary>
        public long AudioBitrate { get; set; }

        /// <summary>
        /// the number of threads used for video encoding if supported by the codec.
        /// Depending on the codec, the actual number of threads used may vary.
        /// </summary>
        public int VideoEncodingThreadCount { get; set; }

        /// <summary>
        /// List of hardware-devices that should be considered when initializing hardware acceleration
        /// </summary>
        public CaptureVideoHardwareDevice VideoAllowedHardwareDevices { get; set; }

        public CaptureConfigurationEventArgs()
            : base()
        { }

    }

}
