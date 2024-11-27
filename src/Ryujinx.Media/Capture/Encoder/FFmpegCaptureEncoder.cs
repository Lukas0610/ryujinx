using FFmpeg.AutoGen.Abstractions;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Media.Capture.Encoder.Configuration;
using Ryujinx.Media.Capture.Encoder.Frames;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static FFmpeg.AutoGen.Abstractions.ffmpeg;

namespace Ryujinx.Media.Capture.Encoder
{

    sealed unsafe class FFmpegCaptureEncoder : ICaptureEncoder
    {

        /// <summary>
        /// The timestamp-resolution of the output-stream/muxer
        /// </summary>
        private const int TargetStreamTimebase = 60;

        private static readonly int AVERROR_EINVAL = AVERROR(EINVAL);
        private static readonly int AVERROR_EAGAIN = AVERROR(EAGAIN);

        public static readonly CaptureOutputFormat[] SupportedOutputFormats = new[]
        {
            CaptureOutputFormat.Auto,
            CaptureOutputFormat.MKV,
            CaptureOutputFormat.MPEGTS,
        };

        public static readonly CaptureVideoCodec[] SupportedVideoCodecs = new[]
        {
            CaptureVideoCodec.Auto,
            CaptureVideoCodec.H264,
            CaptureVideoCodec.HEVC,
        };

        public static readonly CaptureAudioCodec[] SupportedAudioCodecs = new[]
        {
            CaptureAudioCodec.Auto,
            CaptureAudioCodec.AAC,
            CaptureAudioCodec.PCM,
            CaptureAudioCodec.Opus,
            CaptureAudioCodec.Vorbis,
        };

        private sealed class OutputState
        {
            public bool Configured;
            public CaptureOutputFormat Format;
            public string Path;

            public NativeOutputState* Native;

            public bool HasVariableFPS;
            public bool HasNonStrictTimestamps;

            public OutputState()
            {
                Format = CaptureOutputFormat.None;
            }
        }

        private struct NativeOutputState
        {
            public AVFormatContext* Context;
        }

        private sealed class VideoState
        {
            public bool Configured;

            public CaptureVideoCodec Codec;
            public int InputWidth;
            public int InputHeight;
            public MediaPixelFormat InputPixelFormat;
            public long Bitrate;

            public bool Scaling;
            public bool ScalingUsingHwFilters;
            public int OutputWidth;
            public int OutputHeight;

            public AVCodecID CodecId;
            public string CodecName;
            public AVHWDeviceType HwDeviceType;
            public AVPixelFormat InputPixFmt;
            public int InputBytesPerPixel;
            public int InputLinesize;
            public AVPixelFormat HwUploadPixFmt;
            public AVPixelFormat OutputPixFmt;

            public bool HwAcceleration;

            public bool FramePresented;
            public long InitialPts;

            public NativeVideoState* Native;

            public VideoState()
            {
                Codec = CaptureVideoCodec.None;
                HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            }
        }

        private struct NativeVideoState
        {
            public AVBufferRef* HwDeviceContextRef;
            public AVCodecContext* Context;
            public AVStream* Stream;
            public AVFrame* Frame;
            public AVFrame* HwFrame;
            public AVPacket* Packet;
            public SwsContext* SwsContext;
            public byte* VflipStrideBuffer;
        }

        private sealed class AudioState
        {
            public bool Configured;

            public CaptureAudioCodec Codec;
            public MediaSampleFormat SampleFormat;
            public uint SampleRate;
            public uint SampleCount;
            public uint ChannelCount;
            public long Bitrate;

            public AVCodecID CodecId;
            public ulong ChannelLayoutMask;
            public AVSampleFormat SourceSampleFmt;
            public AVSampleFormat TargetSampleFmt;

            public bool UsingDirectSampleEncoding;
            public AVRational EncodingTimeBase;
            public long NumberOfEncodedSamples;

            public bool FramePresented;
            public long InitialPts;
            public long LastPts;

            public NativeAudioState* Native;

            public AudioState()
            {
                Codec = CaptureAudioCodec.None;
            }
        }

        private struct NativeAudioState
        {
            public AVCodecContext* Context;
            public AVStream* Stream;
            public AVFrame* InputFrame;
            public AVFrame* OutputFrame;
            public AVPacket* Packet;
            public SwrContext* SwrContext;
            public AVAudioFifo* Fifo;
        }

        /// <summary>
        /// Enable strict return-code validation. When disabled, some native errors will be ignored,
        /// e.g. invalid frames will be silently dropped instead of throwing an exception
        /// </summary>
        public bool StrictMode = true;

        private readonly int _surfaceFlingerSwapInterval;
        private readonly int _surfaceFlingerFrameRate;

        private OutputState Output;
        private VideoState Video;
        private AudioState[] Audio;

        // Encoder-Queue
        private readonly Lock _chronoLock = new();
        private Stopwatch _chrono;

        private readonly object _frameQueueSignal;
        private volatile bool _frameQueueRunning;
        private volatile int _frameQueueSize;
        private Thread _frameQueueThread;

        private Queue<VideoCaptureFrame> _videoFrameQueue;
        private readonly Lock _videoFrameQueueLock;
        private volatile int _videoFrameQueueSize;

        private Queue<AudioCaptureFrame> _audioFrameQueue;
        private readonly Lock _audioFrameQueueLock;
        private volatile int _audioFrameQueueSize;

        private readonly Lock _encoderStatsLock = new();

        private Timer _encoderStatsTimer;
        private Stopwatch _encoderVideoRunTimeChrono;

        private Stopwatch _encoderVideoAvgTimeChrono;
        private TimeSpan _encoderVideoLastFrameTime;
        private TimeSpan _encoderVideoTotalFrameTime;
        private long _encoderVideoTotalFrameCount;

        public TimeSpan Elapsed
        {
            get
            {
                lock (_chronoLock)
                {
                    return _chrono.Elapsed;
                }
            }
        }

        public double EncoderFPSAverage
        {
            get
            {
                lock (_encoderStatsLock)
                {
                    return _encoderVideoTotalFrameCount / _encoderVideoTotalFrameTime.TotalSeconds;
                }
            }
        }

        public double EncoderFPSLastRun
        {
            get
            {
                lock (_encoderStatsLock)
                {
                    return 1 / _encoderVideoLastFrameTime.TotalSeconds;
                }
            }
        }

        public FFmpegCaptureEncoder(int sfSwapInterval)
        {
            _surfaceFlingerSwapInterval = sfSwapInterval;

            if (sfSwapInterval != 0)
            {
                _surfaceFlingerFrameRate = CaptureHandler.SurfaceFlingerSwapIntervalBase / sfSwapInterval;
            }
            else
            {
                // Although a zero swap-interval likely means that the user disabled vsync
                // and thus "unlimited" FPS, we just assume a target framerate of 60fps
                _surfaceFlingerFrameRate = CaptureHandler.SurfaceFlingerSwapIntervalBase;
            }

            _frameQueueSignal = new object();
            _frameQueueSize = 0;

            _videoFrameQueue = new Queue<VideoCaptureFrame>(16);
            _videoFrameQueueLock = new Lock();
            _videoFrameQueueSize = 0;

            _audioFrameQueue = new Queue<AudioCaptureFrame>(16);
            _audioFrameQueueLock = new Lock();
            _audioFrameQueueSize = 0;

            _encoderStatsTimer = new Timer(LogEncoderStats);
            _encoderVideoRunTimeChrono = new Stopwatch();
            _encoderVideoAvgTimeChrono = new Stopwatch();

            _frameQueueThread = new Thread(EncoderThreadStart)
            {
                Name = "Media.CaptureOutputEncoder.FrameQueueThread",
                Priority = ThreadPriority.AboveNormal,
            };

            NativeOutputState* outputNative = (NativeOutputState*)NativeMemory.AllocZeroed((nuint)sizeof(NativeOutputState));
            if (outputNative == null)
            {
                throw new OutOfMemoryException($"Failed to allocate {sizeof(NativeOutputState)} byte(s) for native output context");
            }

            NativeVideoState* videoNative = (NativeVideoState*)NativeMemory.AllocZeroed((nuint)sizeof(NativeVideoState));
            if (videoNative == null)
            {
                throw new OutOfMemoryException($"Failed to allocate {sizeof(NativeVideoState)} byte(s) for native video context");
            }

            Output = new OutputState { Native = outputNative };
            Video = new VideoState { Native = videoNative };

            Audio = new AudioState[CaptureHandler.NumberOfSupportedAudioSessions];

            for (int i = 0; i < Audio.Length; i++)
            {
                Audio[i] = new AudioState();
            }
        }

        public bool IsActiveConfiguration(VideoCaptureConfiguration videoConfig)
        {
            return Video.Configured &&
                (Video.InputWidth == videoConfig.Width) &&
                (Video.InputHeight == videoConfig.Height) &&
                (Video.InputPixelFormat == videoConfig.PixelFormat);
        }

        public bool IsActiveConfiguration(AudioCaptureConfiguration audioConfig)
        {
            if (Audio.Length <= audioConfig.SessionIndex)
            {
                return false;
            }

            AudioState audio = Audio[audioConfig.SessionIndex];

            return audio.Configured &&
                (audio.SampleFormat == audioConfig.SampleFormat) &&
                (audio.SampleRate == audioConfig.SampleRate) &&
                (audio.SampleCount == audioConfig.SampleCount) &&
                (audio.ChannelCount == audioConfig.ChannelCount);
        }

        public static bool Supports(CaptureOutputFormat outputFormat, CaptureVideoCodec videoCodec, CaptureAudioCodec audioCodec)
        {
            return SupportedOutputFormats.Contains(outputFormat) &&
                SupportedVideoCodecs.Contains(videoCodec) &&
                SupportedAudioCodecs.Contains(audioCodec);
        }

        public bool ConfigureOutput(CaptureOutputFormat outputFormat,
                                    CaptureVideoCodec videoCodec,
                                    CaptureAudioCodec audioCodec,
                                    string outputPath)
        {
            // @remark: video- and audio-codec may be set to auto at this point

            if (Output.Configured)
            {
                return false;
            }

            if (outputFormat == CaptureOutputFormat.Auto)
            {
                if ((videoCodec == CaptureVideoCodec.H264 || videoCodec == CaptureVideoCodec.HEVC) &&
                    audioCodec == CaptureAudioCodec.AAC)
                {
                    // Only select MPEG-TS if both video- and audio-codec are well-known
                    outputFormat = CaptureOutputFormat.MPEGTS;
                }
                else
                {
                    // For other combinations, go the safe way and choose MKV at its more versatile
                    outputFormat = CaptureOutputFormat.MKV;
                }
            }

            Output.Path = outputPath;
            Output.Format = outputFormat;

            string formatName = outputFormat switch
            {
                CaptureOutputFormat.MKV => "matroska",
                CaptureOutputFormat.MPEGTS => "mpegts",
                _ => throw new ArgumentException("Unsupported output-format", nameof(outputFormat)),
            };

            _chrono = new Stopwatch();
            _chrono.Start();

            Assert(avformat_alloc_output_context2(&Output.Native->Context, null, formatName, null));

            Output.HasVariableFPS = (Output.Native->Context->oformat->flags & AVFMT_TS_NONSTRICT) != AVFMT_TS_NONSTRICT;
            Output.HasNonStrictTimestamps = (Output.Native->Context->oformat->flags & AVFMT_TS_NONSTRICT) != AVFMT_TS_NONSTRICT;

            Assert(av_dict_set(&Output.Native->Context->metadata, "EMULATOR", $"Ryujinx {ReleaseInformation.Version}", 0));

            // Successfully configured
            Output.Configured = true;

            return true;
        }

        public bool ConfigureVideoStream(int width,
                                         int height,
                                         MediaPixelFormat pixelFormat,
                                         CaptureVideoCodec codecType,
                                         int scaleWidth,
                                         int scaleHeight,
                                         bool useBitrate,
                                         long bitrate,
                                         bool useQualityLevel,
                                         int qualityLevel,
                                         bool lossless,
                                         int threadCount,
                                         CaptureVideoHardwareDevice allowedHardwareDevices)
        {
            if (Video.Configured)
            {
                return true;
            }

            if (codecType == CaptureVideoCodec.Auto)
            {
                // For now, auto just means H.264
                //
                // As an idea for the future, check at which dimension/bitrate/etc. other codecs
                // may perform better than H.264 in respect to quality, size and encoding-performance
                codecType = CaptureVideoCodec.H264;
            }

            switch (codecType)
            {
                case CaptureVideoCodec.H264:
                    Video.CodecId = AVCodecID.AV_CODEC_ID_H264;
                    break;
                case CaptureVideoCodec.HEVC:
                    Video.CodecId = AVCodecID.AV_CODEC_ID_HEVC;
                    break;
                default:
                    throw new ArgumentException("Unsupported video codec", nameof(codecType));
            }

            switch (pixelFormat)
            {
                case MediaPixelFormat.Bgra8888:
                    Video.InputPixFmt = AVPixelFormat.AV_PIX_FMT_BGRA;
                    Video.InputBytesPerPixel = 4;
                    break;
                case MediaPixelFormat.Rgba8888:
                    Video.InputPixFmt = AVPixelFormat.AV_PIX_FMT_RGBA;
                    Video.InputBytesPerPixel = 4;
                    break;
                default:
                    throw new ArgumentException("Unsupported pixel format", nameof(pixelFormat));
            }

            Video.Codec = codecType;
            Video.InputWidth = width;
            Video.InputHeight = height;
            Video.InputPixelFormat = pixelFormat;
            Video.Bitrate = bitrate;

            Video.OutputWidth = width;
            Video.OutputHeight = height;

            // If either scale-value is zero, scaling is disabled.
            if (scaleWidth != 0 && scaleHeight != 0)
            {
                if (scaleWidth > 0)
                {
                    if (scaleWidth % 2 != 0)
                    {
                        throw new ArgumentException("Target video width must be divisible by 2", nameof(scaleWidth));
                    }

                    Video.OutputWidth = scaleWidth;
                }
                else if (scaleWidth == -1)
                {
                    // scale according to requested target-height and input-width to keep aspect-ratio
                    Video.OutputWidth = (int)(width * ((decimal)scaleHeight / height));
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(scaleWidth), "Target scale width must either be -1 or any positive value");
                }

                if (scaleHeight > 0)
                {
                    if (scaleHeight % 2 != 0)
                    {
                        throw new ArgumentException("Target video height must be divisible by 2", nameof(scaleHeight));
                    }

                    Video.OutputHeight = scaleHeight;
                }
                else if (scaleHeight == -1)
                {
                    // scale according to requested target-width and input-height to keep aspect-ratio
                    Video.OutputHeight = (int)(height * ((decimal)scaleWidth / width));
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(scaleHeight), "Target scale height must either be -1 or any positive value");
                }

                Video.Scaling =
                    (Video.InputWidth != Video.OutputWidth) ||
                    (Video.InputHeight != Video.OutputHeight);

                Video.ScalingUsingHwFilters = false;
            }

            Video.HwAcceleration = false;

            int ret;
            AVBufferRef* hwDeviceContextRef = null;

            if (allowedHardwareDevices != CaptureVideoHardwareDevice.None)
            {
                // NVENC
                if (((allowedHardwareDevices & CaptureVideoHardwareDevice.NVENC) == CaptureVideoHardwareDevice.NVENC) &&
                    FFmpegBuildSupportsHWDeviceType(AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA))
                {
                    ret = av_hwdevice_ctx_create(&hwDeviceContextRef, AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA, null, null, 0);

                    if (ret == 0)
                    {
                        if (codecType == CaptureVideoCodec.H264 &&
                            avcodec_find_encoder_by_name("h264_nvenc") != null)
                        {
                            Video.CodecName = "h264_nvenc";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_CUDA;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else if (codecType == CaptureVideoCodec.HEVC &&
                                 avcodec_find_encoder_by_name("hevc_nvenc") != null)
                        {
                            Video.CodecName = "hevc_nvenc";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_CUDA;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else
                        {
                            av_buffer_unref(&hwDeviceContextRef);
                        }
                    }
                }

                // Intel QSV
                if (!Video.HwAcceleration &&
                    ((allowedHardwareDevices & CaptureVideoHardwareDevice.QSV) == CaptureVideoHardwareDevice.QSV) &&
                    FFmpegBuildSupportsHWDeviceType(AVHWDeviceType.AV_HWDEVICE_TYPE_QSV))
                {
                    ret = av_hwdevice_ctx_create(&hwDeviceContextRef, AVHWDeviceType.AV_HWDEVICE_TYPE_QSV, null, null, 0);

                    if (ret == 0)
                    {
                        if (codecType == CaptureVideoCodec.H264 &&
                            avcodec_find_encoder_by_name("h264_qsv") != null)
                        {
                            Video.CodecName = "h264_qsv";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_QSV;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_QSV;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else if (codecType == CaptureVideoCodec.HEVC &&
                                 avcodec_find_encoder_by_name("hevc_qsv") != null)
                        {
                            Video.CodecName = "hevc_qsv";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_QSV;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_QSV;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else
                        {
                            av_buffer_unref(&hwDeviceContextRef);
                        }
                    }
                }

                // Vulkan
                if (!Video.HwAcceleration &&
                    ((allowedHardwareDevices & CaptureVideoHardwareDevice.Vulkan) == CaptureVideoHardwareDevice.Vulkan) &&
                    FFmpegBuildSupportsHWDeviceType(AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN))
                {
                    ret = av_hwdevice_ctx_create(&hwDeviceContextRef, AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN, null, null, 0);

                    if (ret == 0)
                    {
                        if (codecType == CaptureVideoCodec.H264 &&
                            avcodec_find_encoder_by_name("h264_vulkan") != null)
                        {
                            Video.CodecName = "h264_vulkan";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_VULKAN;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else if (codecType == CaptureVideoCodec.HEVC &&
                                 avcodec_find_encoder_by_name("hevc_vulkan") != null)
                        {
                            Video.CodecName = "hevc_vulkan";
                            Video.HwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN;
                            Video.HwUploadPixFmt = AVPixelFormat.AV_PIX_FMT_NV12;
                            Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_VULKAN;

                            Video.HwAcceleration = true;

                            Video.Native->HwDeviceContextRef = hwDeviceContextRef;
                        }
                        else
                        {
                            av_buffer_unref(&hwDeviceContextRef);
                        }
                    }
                }
            }

            //
            // Codec
            //
            AVCodec* codec;

            if (Video.HwAcceleration)
            {
                codec = avcodec_find_encoder_by_name(Video.CodecName);

                if (codec == null)
                {
                    throw new FFmpegException($"Failed to find '{Video.CodecName}' codec, check your FFmpeg build");
                }

            }
            else
            {
                codec = avcodec_find_encoder(Video.CodecId);

                if (codec == null)
                {
                    throw new FFmpegException($"Failed to find {Video.CodecId} codec, check your FFmpeg build");
                }
            }

            Video.CodecId = codec->id;

            if (avformat_query_codec(Output.Native->Context->oformat, codec->id, FF_COMPLIANCE_NORMAL) == 0)
            {
                throw new FFmpegException($"Cannot store {codecType}-streams in {Output.Format} output format");
            }

            if (!Video.HwAcceleration)
            {
                switch (Video.CodecId)
                {
                    case AVCodecID.AV_CODEC_ID_H264:
                        Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
                        break;
                    case AVCodecID.AV_CODEC_ID_HEVC:
                        Video.OutputPixFmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
                        break;
                    default:
                        throw new ArgumentException("Unsupported video codec ID", nameof(codecType));
                }
            }

            //
            // Codec-Context
            //
            AVCodecContext* context = avcodec_alloc_context3(codec);
            if (context == null)
            {
                throw new FFmpegException("Failed to allocate codec context");
            }

            Video.Native->Context = context;

            context->width = Video.OutputWidth;
            context->height = Video.OutputHeight;
            context->pix_fmt = Video.OutputPixFmt;
            context->gop_size = 12;
            context->max_b_frames = 0; // disable B-frames for low latency
            context->bit_rate = 0;

            // Defaults
            if (Video.HwAcceleration)
            {
                if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA)
                {
                    // NVENC
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                            av_opt_set(context->priv_data, "profile", "high", 0);
                            av_opt_set(context->priv_data, "preset", "p1", 0);
                            av_opt_set(context->priv_data, "tune", "ull", 0);
                            av_opt_set(context->priv_data, "level", "auto", 0);
                            break;
                        case CaptureVideoCodec.HEVC:
                            av_opt_set(context->priv_data, "profile", "main", 0);
                            av_opt_set(context->priv_data, "preset", "p1", 0);
                            av_opt_set(context->priv_data, "tune", "ull", 0);
                            av_opt_set(context->priv_data, "level", "auto", 0);
                            break;
                    }
                }
                else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_QSV)
                {
                    // Intel QSV
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                            av_opt_set(context->priv_data, "profile", "high", 0);
                            av_opt_set(context->priv_data, "preset", "veryfast", 0);
                            break;
                        case CaptureVideoCodec.HEVC:
                            av_opt_set(context->priv_data, "profile", "main", 0);
                            av_opt_set(context->priv_data, "preset", "veryfast", 0);
                            break;
                    }
                }
                else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN)
                {
                    // Vulkan
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                            av_opt_set(context->priv_data, "profile", "high", 0);
                            av_opt_set(context->priv_data, "tune", "ull", 0);
                            break;
                        case CaptureVideoCodec.HEVC:
                            av_opt_set(context->priv_data, "profile", "main", 0);
                            av_opt_set(context->priv_data, "tune", "ull", 0);
                            break;
                    }

                    av_opt_set(context->priv_data, "usage", "record", 0);
                    av_opt_set(context->priv_data, "content", "desktop", 0);
                }
            }
            else
            {
                // Software
                switch (codecType)
                {
                    case CaptureVideoCodec.H264:
                        av_opt_set(context->priv_data, "profile", "high", 0);
                        av_opt_set(context->priv_data, "preset", "ultrafast", 0);
                        av_opt_set(context->priv_data, "tune", "zerolatency", 0);
                        break;
                    case CaptureVideoCodec.HEVC:
                        av_opt_set(context->priv_data, "profile", "main", 0);
                        av_opt_set(context->priv_data, "preset", "ultrafast", 0);
                        av_opt_set(context->priv_data, "tune", "zerolatency", 0);
                        break;
                }
            }

            // User-Configured
            if (useBitrate)
            {
                context->bit_rate = bitrate;
                context->rc_max_rate = bitrate;

                if (Video.HwAcceleration)
                {
                    if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA)
                    {
                        // NVENC
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc", "cbr", 0); // constant bitrate mode
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_QSV)
                    {
                        // Intel QSV -- nothing to do here
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN)
                    {
                        // Vulkan
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc_mode", "cbr", 0); // constant bitrate mode
                                break;
                        }
                    }
                }
            }
            else if (useQualityLevel)
            {
                if (Video.HwAcceleration)
                {
                    if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA)
                    {
                        // NVENC
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc", "constqp", 0); // constant QP mode
                                av_opt_set_int(context->priv_data, "qp", Math.Clamp(qualityLevel, 0, 51), 0);
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_QSV)
                    {
                        // Intel QSV
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                // expected to use the intelligent constant quality (ICQ)
                                context->global_quality = Math.Clamp(qualityLevel, 0, 51);
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN)
                    {
                        // Vulkan
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc", "cqp", 0); // constant quantizer mode
                                av_opt_set_int(context->priv_data, "qp", Math.Clamp(qualityLevel, 0, 255), 0);
                                break;
                        }
                    }
                }
                else
                {
                    // Software
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                        case CaptureVideoCodec.HEVC:
                            av_opt_set_int(context->priv_data, "crf", Math.Clamp(qualityLevel, 0, 51), 0);
                            break;
                    }
                }
            }
            else if (lossless)
            {
                if (Video.HwAcceleration)
                {
                    if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA)
                    {
                        // NVENC
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "tune", "lossless", 0);
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_QSV)
                    {
                        // Intel QSV
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                // expected to use the intelligent constant quality (ICQ)
                                context->global_quality = 0;
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN)
                    {
                        // Vulkan
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "tune", "lossless", 0);
                                break;
                        }
                    }
                }
                else
                {
                    // Software
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                            av_opt_set_int(context->priv_data, "crf", 0, 0);
                            break;
                        case CaptureVideoCodec.HEVC:
                            av_opt_set(context->priv_data, "x265-params", "lossless=1", 0);
                            break;
                    }
                }
            }
            else
            {
                if (Video.HwAcceleration)
                {
                    if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA)
                    {
                        // NVENC
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                                av_opt_set(context->priv_data, "rc", "constqp", 0); // constant QP mode
                                av_opt_set_int(context->priv_data, "qp", 23, 0);
                                break;
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc", "constqp", 0); // constant QP mode
                                av_opt_set_int(context->priv_data, "qp", 28, 0);
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_QSV)
                    {
                        // Intel QSV
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                                // expected to use the intelligent constant quality (ICQ)
                                context->global_quality = 23;
                                break;
                            case CaptureVideoCodec.HEVC:
                                // expected to use the intelligent constant quality (ICQ)
                                context->global_quality = 28;
                                break;
                        }
                    }
                    else if (Video.HwDeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN)
                    {
                        // Vulkan
                        switch (codecType)
                        {
                            case CaptureVideoCodec.H264:
                                av_opt_set(context->priv_data, "rc", "cqp", 0); // constant quantizer mode
                                av_opt_set_int(context->priv_data, "qp", 23, 0);
                                break;
                            case CaptureVideoCodec.HEVC:
                                av_opt_set(context->priv_data, "rc", "cqp", 0); // constant quantizer mode
                                av_opt_set_int(context->priv_data, "qp", 28, 0);
                                break;
                        }
                    }
                }
                else
                {
                    // Software
                    switch (codecType)
                    {
                        case CaptureVideoCodec.H264:
                            av_opt_set_int(context->priv_data, "crf", 23, 0);
                            break;
                        case CaptureVideoCodec.HEVC:
                            av_opt_set_int(context->priv_data, "crf", 28, 0);
                            break;
                    }
                }
            }

            if (!Video.HwAcceleration)
            {
                // Configure Multithreading
                context->thread_count = threadCount;
                context->thread_type = 0;

                // Prefer "native" multithreading over FFmpeg-implemented version.
                // If no native multithreading is available, prefer slice-based over frame-based
                if ((context->codec->capabilities & AV_CODEC_CAP_OTHER_THREADS) == 0)
                {
                    if ((context->codec->capabilities & AV_CODEC_CAP_FRAME_THREADS) == AV_CODEC_CAP_FRAME_THREADS)
                    {
                        context->thread_type = FF_THREAD_FRAME;
                    }

                    if ((context->codec->capabilities & AV_CODEC_CAP_SLICE_THREADS) == AV_CODEC_CAP_SLICE_THREADS)
                    {
                        context->thread_type = FF_THREAD_SLICE;
                    }
                }
            }

            context->framerate = new AVRational { num = _surfaceFlingerFrameRate, den = 1 };

            // Use a timebase of 1ms so we can use the elapsed time since start in milliseconds
            // when each frame was enqueued as the presentation-timestamp for the encoder/muxer
            context->time_base = new AVRational { num = 1, den = 1000 };

            // https://github.com/FFmpeg/FFmpeg/blob/817c6a6762696e6efee44ddc4e2d706922b880e0/doc/examples/mux.c#L196C28-L196C50
            if (context->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
            {
                // Needed to avoid using macroblocks in which some coeffs overflow.
                // This does not happen with normal video, it just happens here as
                // the motion of the chroma plane does not match the luma plane.
                context->mb_decision = 2;
            }

            // Some formats want stream headers to be separate.
            // https://github.com/FFmpeg/FFmpeg/blob/817c6a6762696e6efee44ddc4e2d706922b880e0/doc/examples/mux.c#L208C1-L210C49
            if ((Output.Native->Context->oformat->flags & AVFMT_GLOBALHEADER) != 0)
            {
                context->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            if ((codec->capabilities & AV_CODEC_CAP_ENCODER_REORDERED_OPAQUE) == AV_CODEC_CAP_ENCODER_REORDERED_OPAQUE)
            {
                context->flags |= AV_CODEC_FLAG_COPY_OPAQUE;
            }

            context->flags |= AV_CODEC_FLAG_FRAME_DURATION;

            if (Video.HwAcceleration)
            {
                // Configure hardware-context
                AVBufferRef* hwFramesContextRef = av_hwframe_ctx_alloc(hwDeviceContextRef);
                if (hwFramesContextRef == null)
                {
                    throw new FFmpegException("Failed to allocate hardware frame context");
                }

                AVHWFramesContext* hwFramesContext = (AVHWFramesContext*)hwFramesContextRef->data;

                hwFramesContext->format = Video.OutputPixFmt;
                hwFramesContext->sw_format = Video.HwUploadPixFmt;
                hwFramesContext->width = Video.OutputWidth;
                hwFramesContext->height = Video.OutputHeight;
                hwFramesContext->initial_pool_size = 20;

                ret = av_hwframe_ctx_init(hwFramesContextRef);

                if (ret < 0)
                {
                    av_buffer_unref(&hwFramesContextRef);
                    Assert(ret);
                }

                context->hw_frames_ctx = av_buffer_ref(hwFramesContextRef);

                if (context->hw_frames_ctx == null)
                {
                    av_buffer_unref(&hwFramesContextRef);
                    throw FFmpegException.OutOfMemory();
                }

                av_buffer_unref(&hwFramesContextRef);
            }

            Assert(avcodec_open2(context, codec, null));

            //
            // Stream
            //
            AVStream* stream = avformat_new_stream(Output.Native->Context, codec);
            if (stream == null)
            {
                throw new FFmpegException("Failed to create output stream");
            }

            Video.Native->Stream = stream;

            Assert(avcodec_parameters_from_context(stream->codecpar, context));

            stream->index = (int)(Output.Native->Context->nb_streams - 1);

            stream->avg_frame_rate = new AVRational { num = _surfaceFlingerFrameRate, den = 1 };

            stream->time_base = new AVRational { num = 1, den = TargetStreamTimebase };
            stream->r_frame_rate = stream->time_base;

            //
            // Frame
            //
            AVFrame* frame = av_frame_alloc();
            if (frame == null)
            {
                throw FFmpegException.OutOfMemory();
            }

            Video.Native->Frame = frame;

            // If scaling is enabled and done via hw-filters, the intermediate software-frame is
            // only there for pixel-format conversion and thus needs to keep the input-dimensions
            if (Video.Scaling && Video.ScalingUsingHwFilters && Video.HwAcceleration)
            {
                frame->width = Video.InputWidth;
                frame->height = Video.InputHeight;
            }
            else
            {
                frame->width = Video.OutputWidth;
                frame->height = Video.OutputHeight;
            }

            if (Video.HwAcceleration)
            {
                frame->format = (int)Video.HwUploadPixFmt;
            }
            else
            {
                frame->format = (int)Video.OutputPixFmt;
            }

            Assert(av_frame_get_buffer(frame, 0));

            //
            // Hardware Frame
            //
            if (Video.HwAcceleration)
            {
                AVFrame* hwFrame = av_frame_alloc();
                if (hwFrame == null)
                {
                    throw FFmpegException.OutOfMemory();
                }

                Video.Native->HwFrame = hwFrame;

                Assert(av_hwframe_get_buffer(context->hw_frames_ctx, hwFrame, 0));

                if (hwFrame->hw_frames_ctx == null)
                {
                    throw FFmpegException.OutOfMemory();
                }
            }

            //
            // Packet
            //
            AVPacket* packet = av_packet_alloc();
            if (packet == null)
            {
                throw FFmpegException.OutOfMemory();
            }

            Video.Native->Packet = packet;

            //
            // Scaler
            //
            SwsContext* swsContext = sws_getContext(Video.InputWidth,
                                                    Video.InputHeight,
                                                    Video.InputPixFmt,
                                                    frame->width,
                                                    frame->height,
                                                    (AVPixelFormat)frame->format,
                                                    SWS_FAST_BILINEAR,
                                                    null,
                                                    null,
                                                    null);

            if (swsContext == null)
            {
                throw new FFmpegException("Failed to initialize software rescaler context");
            }

            Video.Native->SwsContext = swsContext;

            //
            // Additional Buffers
            //
            Video.InputLinesize = Video.InputBytesPerPixel * Video.InputWidth;
            Video.Native->VflipStrideBuffer = (byte*)NativeMemory.AllocZeroed((nuint)Video.InputLinesize);

            // Successfully configured
            Video.Configured = true;

            return true;
        }

        public bool ConfigureAudioStream(int sessionIndex,
                                         MediaSampleFormat sampleFormat,
                                         uint sampleRate,
                                         uint sampleCount,
                                         uint channelCount,
                                         CaptureAudioCodec codecType,
                                         long bitrate)
        {
            if (Audio.Length <= sessionIndex)
            {
                throw new InvalidOperationException($"Attempted to configure audio-session #{sessionIndex + 1} while only {Audio.Length} sessions are supported");
            }

            AudioState audio = Audio[sessionIndex];

            if (Audio[sessionIndex].Configured)
            {
                return true;
            }

            NativeAudioState* audioNative = (NativeAudioState*)NativeMemory.AllocZeroed((nuint)sizeof(NativeAudioState));
            if (audioNative == null)
            {
                throw new OutOfMemoryException($"Failed to allocate {sizeof(NativeAudioState)} byte(s) for native audio context");
            }

            audio.Native = audioNative;

            if (codecType == CaptureAudioCodec.Auto)
            {
                if (Output.Format == CaptureOutputFormat.MKV)
                {
                    // Try to select a codec which supports the requested sample-count/frame-size
                    // so we can encode either fully without the overhead of using a FIFO-queue for the samples
                    // or to reduce the overhead by using a codec which can fully drain the FIFO-queue in one
                    // encoding run.
                    //
                    // The sample-counts are taken from the FFmpeg source of each supported encoder:
                    //   - AAC: https://github.com/FFmpeg/FFmpeg/blob/release/7.0/libavcodec/aacenc.c#L1246
                    //   - Vorbis: https://github.com/FFmpeg/FFmpeg/blob/release/7.0/libavcodec/libvorbisenc.c#L40
                    if (sampleCount % 1024 == 0)
                    {
                        codecType = CaptureAudioCodec.AAC;
                    }
                    else if (sampleCount % 64 == 0)
                    {
                        codecType = CaptureAudioCodec.Vorbis;
                    }
                    else if (sampleCount == 120 || sampleCount == 240 || sampleCount == 480 ||
                             sampleCount == 960 || sampleCount == 1920 || sampleCount == 2880)
                    {
                        codecType = CaptureAudioCodec.Opus;
                    }
                    else
                    {
                        // PCM has a variable frame size
                        codecType = CaptureAudioCodec.PCM;
                    }
                }
                else if (Output.Format == CaptureOutputFormat.MPEGTS)
                {
                    // MPEG-TS only accepts AAC out of the codecs we support
                    codecType = CaptureAudioCodec.AAC;
                }
                else
                {
                    throw new ArgumentException("Unsupported output-format");
                }
            }

            switch (sampleFormat)
            {
                case MediaSampleFormat.PcmInt8:
                    audio.SourceSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_U8;
                    break;
                case MediaSampleFormat.PcmInt16:
                    audio.SourceSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
                    break;
                case MediaSampleFormat.PcmInt32:
                    audio.SourceSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_S32;
                    break;
                case MediaSampleFormat.PcmFloat:
                    audio.SourceSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_FLT;
                    break;
                default:
                    throw new ArgumentException("Unsupported sample format", nameof(sampleFormat));
            }

            switch (codecType)
            {
                case CaptureAudioCodec.AAC:
                    audio.CodecId = AVCodecID.AV_CODEC_ID_AAC;
                    audio.TargetSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
                    break;
                case CaptureAudioCodec.PCM:
                    audio.TargetSampleFmt = audio.SourceSampleFmt;

                    switch (sampleFormat)
                    {
                        case MediaSampleFormat.PcmInt8:
                            audio.CodecId = AVCodecID.AV_CODEC_ID_PCM_U8;
                            break;
                        case MediaSampleFormat.PcmInt16:
                            audio.CodecId = AVCodecID.AV_CODEC_ID_PCM_S16LE;
                            break;
                        case MediaSampleFormat.PcmInt32:
                            audio.CodecId = AVCodecID.AV_CODEC_ID_PCM_S32LE;
                            break;
                        case MediaSampleFormat.PcmFloat:
                            audio.CodecId = AVCodecID.AV_CODEC_ID_PCM_F32LE;
                            break;
                        default:
                            // Fall back to 32-bit floaing point sample-format in case
                            // we encounter an unknown input sample-format
                            audio.TargetSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_FLT;
                            audio.CodecId = AVCodecID.AV_CODEC_ID_PCM_F32LE;
                            break;
                    }
                    break;
                case CaptureAudioCodec.Opus:
                    audio.CodecId = AVCodecID.AV_CODEC_ID_OPUS;
                    audio.TargetSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_FLT;
                    break;
                case CaptureAudioCodec.Vorbis:
                    audio.CodecId = AVCodecID.AV_CODEC_ID_VORBIS;
                    audio.TargetSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
                    break;
                default:
                    throw new ArgumentException("Unsupported audio codec", nameof(codecType));
            }

            switch (channelCount)
            {
                case 1:
                    audio.ChannelLayoutMask = AV_CH_LAYOUT_MONO;
                    break;
                case 2:
                    audio.ChannelLayoutMask = AV_CH_LAYOUT_STEREO;
                    break;
                default:
                    throw new ArgumentException(null, nameof(channelCount));
            }

            audio.Codec = codecType;

            audio.SampleFormat = sampleFormat;
            audio.SampleCount = sampleCount;
            audio.SampleRate = sampleRate;
            audio.ChannelCount = channelCount;
            audio.Bitrate = bitrate;

            if (avformat_query_codec(Output.Native->Context->oformat, audio.CodecId, FF_COMPLIANCE_NORMAL) == 0)
            {
                throw new FFmpegException($"Cannot store {codecType}-streams in {Output.Format} output format");
            }

            //
            // Codec
            //
            AVCodec* codec = avcodec_find_encoder(audio.CodecId);
            if (codec == null)
            {
                throw new FFmpegException($"Failed to find {audio.CodecId} codec, check your FFmpeg build");
            }

            //
            // Codec-Context
            //
            AVCodecContext* context = avcodec_alloc_context3(codec);
            if (context == null)
            {
                throw new FFmpegException("Failed to allocate codec context");
            }

            audioNative->Context = context;

            context->sample_fmt = audio.TargetSampleFmt;
            context->sample_rate = (int)audio.SampleRate;
            context->frame_size = (int)audio.SampleCount;

            if (bitrate > 0)
            {
                context->bit_rate = bitrate;
            }
            else
            {
                context->bit_rate = 0;
            }

            // The internal timebase is going to be the sample-rate (samples per second),
            // but will scaled to the context-timebase by the encoder-queue.
            audio.EncodingTimeBase = new AVRational { num = 1, den = (int)audio.SampleRate };
            context->time_base = new AVRational { num = 1, den = 1000 };

            // Some formats want stream headers to be separate.
            // https://github.com/FFmpeg/FFmpeg/blob/817c6a6762696e6efee44ddc4e2d706922b880e0/doc/examples/mux.c#L208C1-L210C49
            if ((Output.Native->Context->oformat->flags & AVFMT_GLOBALHEADER) != 0)
            {
                context->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
            }

            Assert(av_channel_layout_from_mask(&context->ch_layout, audio.ChannelLayoutMask));
            Assert(avcodec_open2(context, codec, null));

            // Frame-size of zero indicates a codec with support for variable input-frames (like PCM).
            //
            // If that case applies or the number of samples provided by the audio-source matches the number of samples
            // the encoder can handle with each frame, we can use direct-encoding, skipping over the sample FIFO queue.
            audio.UsingDirectSampleEncoding = (context->frame_size == 0) || (context->frame_size == (int)audio.SampleCount);

            //
            // Stream
            //
            AVStream* stream = avformat_new_stream(Output.Native->Context, codec);
            if (stream == null)
            {
                throw new FFmpegException("Failed to create output stream");
            }

            audioNative->Stream = stream;

            Assert(avcodec_parameters_from_context(stream->codecpar, context));

            stream->index = (int)(Output.Native->Context->nb_streams - 1);
            stream->time_base = new AVRational { num = 1, den = TargetStreamTimebase };

            av_dict_set(&stream->metadata, "SESSION", $"{sessionIndex}", 0);

            //
            // Input frame
            //
            AVFrame* inputFrame = av_frame_alloc();
            if (inputFrame == null)
            {
                throw FFmpegException.OutOfMemory();
            }

            audioNative->InputFrame = inputFrame;

            inputFrame->sample_rate = (int)audio.SampleRate;
            inputFrame->format = (int)audio.SourceSampleFmt;

            if (context->frame_size != 0)
            {
                inputFrame->nb_samples = context->frame_size;
            }
            else
            {
                inputFrame->nb_samples = (int)audio.SampleCount;
            }

            Assert(av_channel_layout_copy(&inputFrame->ch_layout, &context->ch_layout));
            Assert(av_frame_get_buffer(inputFrame, 0));

            //
            // Output Frame
            //
            AVFrame* outputFrame = av_frame_alloc();
            if (outputFrame == null)
            {
                throw FFmpegException.OutOfMemory();
            }

            audioNative->OutputFrame = outputFrame;

            outputFrame->sample_rate = (int)audio.SampleRate;
            outputFrame->format = (int)audio.TargetSampleFmt;

            if (context->frame_size != 0)
            {
                outputFrame->nb_samples = context->frame_size;
            }
            else
            {
                outputFrame->nb_samples = (int)audio.SampleCount;
            }

            Assert(av_channel_layout_copy(&outputFrame->ch_layout, &context->ch_layout));
            Assert(av_frame_get_buffer(outputFrame, 0));

            //
            // Packet
            //
            AVPacket* packet = av_packet_alloc();
            if (packet == null)
            {
                throw FFmpegException.OutOfMemory();
            }

            audioNative->Packet = packet;

            //
            // Resampler
            //
            if (audio.SourceSampleFmt != audio.TargetSampleFmt)
            {
                SwrContext* swrContext = swr_alloc();
                if (swrContext == null)
                {
                    throw new FFmpegException("Failed to initialize software resampler context");
                }

                Assert(swr_config_frame(swrContext, outputFrame, inputFrame));
                Assert(swr_init(swrContext));

                audioNative->SwrContext = swrContext;
            }

            //
            // Sample FIFO
            //
            if (!audio.UsingDirectSampleEncoding)
            {
                AVAudioFifo* inputFifo = av_audio_fifo_alloc(audio.SourceSampleFmt, (int)audio.ChannelCount, (int)audio.SampleCount);
                if (inputFifo == null)
                {
                    throw new FFmpegException("Failed to allocate audio FIFO");
                }

                audioNative->Fifo = inputFifo;
            }

            // Successfully configured
            audio.Configured = true;

            return true;
        }

        public void BeginEncoding()
        {
            if (Video.HwAcceleration)
            {
                Logger.Info?.Print(LogClass.Capture, $"Using FFmpeg hardware-accelerated encoder: output={Output.Format}, video={Video.CodecName}");
            }
            else
            {
                Logger.Info?.Print(LogClass.Capture, $"Using FFmpeg software encoder: output={Output.Format}, video={Video.Codec})");
            }

            for (int i = 0; i < Audio.Length; i++)
            {
                if (Audio[i].Configured)
                {
                    int frameSize = Audio[i].Native->Context->frame_size;
                    if (frameSize == 0)
                    {
                        frameSize = (int)Audio[i].SampleCount;
                    }

                    if (Audio[i].UsingDirectSampleEncoding)
                    {
                        if (Audio[i].SourceSampleFmt != Audio[i].TargetSampleFmt)
                        {
                            Logger.Info?.Print(LogClass.Capture, $"[audio:#{i}] codec={Audio[i].Codec}, handling=DirectResampling");
                        }
                        else
                        {
                            Logger.Info?.Print(LogClass.Capture, $"[audio:#{i}] codec={Audio[i].Codec}, handling=Direct");
                        }
                    }
                    else
                    {
                        if (Audio[i].SourceSampleFmt != Audio[i].TargetSampleFmt)
                        {
                            Logger.Info?.Print(LogClass.Capture, $"[audio:#{i}] codec={Audio[i].Codec}, handling=QueuedResampling");
                        }
                        else
                        {
                            Logger.Info?.Print(LogClass.Capture, $"[audio:#{i}] codec={Audio[i].Codec}, handling=Queued");
                        }
                    }
                }
            }

            Assert(avio_open(&Output.Native->Context->pb, Output.Path, AVIO_FLAG_WRITE));
            Assert(avformat_write_header(Output.Native->Context, null));

            _frameQueueRunning = true;
            _frameQueueThread.Start();

            _encoderStatsTimer.Change(0, 5000);
        }

        public void FinishEncoding()
        {
            Logger.Info?.Print(LogClass.Capture, "Stopping and flushing encoder");

            // Stop and signal encoder-thread
            _frameQueueRunning = false;

            Thread.MemoryBarrier();

            lock (_frameQueueSignal)
            {
                Monitor.PulseAll(_frameQueueSignal);
            }

            // Wait for thread to exit
            _frameQueueThread?.Join();

            _encoderStatsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            LogEncoderStats(null);

            Assert(av_write_trailer(Output.Native->Context));

            Logger.Info?.Print(LogClass.Capture, "Encoder done!");
        }

        public bool EnqueueFrame(GenericCaptureFrame genericFrame)
        {
            genericFrame.PresentationTimeStamp = _chrono.ElapsedMilliseconds;

            // Enqueue the frame, increment the atomic frame queue counter
            // and signal to the encoder-thread that a new frame is available
            if (genericFrame is VideoCaptureFrame videoFrame)
            {
                if (!Video.FramePresented)
                {
                    Video.InitialPts = videoFrame.PresentationTimeStamp;
                    Video.FramePresented = true;
                }

                Interlocked.Increment(ref _videoFrameQueueSize);
                Interlocked.Increment(ref _frameQueueSize);

                lock (_videoFrameQueueLock)
                {
                    _videoFrameQueue.Enqueue(videoFrame);
                }

                lock (_frameQueueSignal)
                {
                    Monitor.PulseAll(_frameQueueSignal);
                }

                return true;
            }
            else if (genericFrame is AudioCaptureFrame audioFrame)
            {
                AudioState audio = Audio[audioFrame.SessionIndex];

                if (audio == null || !audio.Configured)
                {
                    // Return buffer to pool
                    audioFrame.Buffer.Dispose();

                    // Drop frames assigned to invalid session
                    return false;
                }

                if (!audio.FramePresented)
                {
                    audio.InitialPts = audioFrame.PresentationTimeStamp;
                    audio.FramePresented = true;
                }

                Interlocked.Increment(ref _audioFrameQueueSize);
                Interlocked.Increment(ref _frameQueueSize);

                lock (_audioFrameQueueLock)
                {
                    _audioFrameQueue.Enqueue(audioFrame);
                }

                lock (_frameQueueSignal)
                {
                    Monitor.PulseAll(_frameQueueSignal);
                }

                return true;
            }

            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void LogEncoderStats(object state)
        {
            TimeSpan encoderVideoLastFrameTime, encoderVideoTotalFrameTime, chronoElapsed;
            long encoderVideoTotalFrameCount;

            lock (_encoderStatsLock)
            {
                encoderVideoLastFrameTime = _encoderVideoLastFrameTime;
                encoderVideoTotalFrameTime = _encoderVideoTotalFrameTime;
                encoderVideoTotalFrameCount = _encoderVideoTotalFrameCount;

                chronoElapsed = _chrono.Elapsed;
            }

            double encoderVideoLastFps = 1 / encoderVideoLastFrameTime.TotalSeconds;
            double encoderVideoLastMillis = encoderVideoLastFrameTime.TotalMilliseconds;
            double encoderVideoAvgFps = encoderVideoTotalFrameCount / encoderVideoTotalFrameTime.TotalSeconds;
            double encoderVideoAvgMillis = encoderVideoTotalFrameTime.TotalMilliseconds / encoderVideoTotalFrameCount;
            double outputVideoAvgFps = encoderVideoTotalFrameCount / chronoElapsed.TotalSeconds;
            double outputVideoAvgMillis = chronoElapsed.TotalMilliseconds / encoderVideoTotalFrameCount;

            string message = string.Join(", ",
            [
                $"NumberOfFrames = {_encoderVideoTotalFrameCount}",
                $"FPS = {FormatDouble(outputVideoAvgFps)} ({FormatDouble(outputVideoAvgMillis)}ms)",
                $"EncodingFPS = {FormatDouble(encoderVideoAvgFps)} ({FormatDouble(encoderVideoAvgMillis)}ms)",
                $"EncodingFPSLastRun = {FormatDouble(encoderVideoLastFps)} ({FormatDouble(encoderVideoLastMillis)}ms)"
            ]);

            Logger.Info?.Print(LogClass.Media, message, "CaptureEncoder");

            if (encoderVideoAvgFps < outputVideoAvgFps)
            {
                Logger.Warning?.Print(LogClass.Media, message, "Encoder too slow. Try to lower resolution, change video-codec or toggle hardware-acceleration");
            }

            string FormatDouble(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);
        }

        private void EncoderThreadStart()
        {
            long lastVideoPts = 0;
            long lastAudioPts = 0;

            while (_frameQueueRunning || _frameQueueSize > 0)
            {
                lock (_frameQueueSignal)
                {
                    while (_frameQueueRunning && _frameQueueSize == 0)
                    {
                        Monitor.Wait(_frameQueueSignal);
                    }
                }

                bool anyAudioSourceConfigured = false;

                for (int i = 0; i < Audio.Length; i++)
                {
                    if (Audio[i].Configured)
                    {
                        anyAudioSourceConfigured = true;
                    }
                }

                // Only dequeue video-frames until the audio-queue has to catch up (interleaved)
                while (Video.Configured &&
                       (!anyAudioSourceConfigured || (lastVideoPts < lastAudioPts)) &&
                       TryDequeueVideoFrame(out VideoCaptureFrame videoFrame))
                {
                    _encoderVideoAvgTimeChrono.Start();
                    _encoderVideoRunTimeChrono.Restart();

                    Interlocked.Decrement(ref _videoFrameQueueSize);
                    Debug.Assert(_videoFrameQueueSize >= 0);

                    Interlocked.Decrement(ref _frameQueueSize);
                    Debug.Assert(_frameQueueSize >= 0);

                    if (videoFrame.PresentationTimeStamp <= lastVideoPts)
                    {
                        // Return buffer to pool
                        videoFrame.Buffer.Dispose();

                        // Drop backwards frames
                        continue;
                    }

                    lastVideoPts = videoFrame.PresentationTimeStamp;

                    AVCodecContext* context = Video.Native->Context;
                    AVStream* stream = Video.Native->Stream;
                    AVFrame* frame = Video.Native->Frame;
                    AVFrame* hwFrame = Video.Native->HwFrame;
                    AVPacket* packet = Video.Native->Packet;

                    videoFrame.Buffer.ConsumeFixed(bufferPtr =>
                    {
                        if ((videoFrame.Flip & MediaImageFlip.Vertical) == MediaImageFlip.Vertical)
                        {
                            BufferedVerticalFlipOnInputData((byte*)bufferPtr,
                                                            videoFrame.Buffer.Length,
                                                            Video.Native->VflipStrideBuffer,
                                                            Video.InputLinesize,
                                                            Video.InputHeight,
                                                            Video.InputLinesize);
                        }

                        if ((videoFrame.Flip & MediaImageFlip.Horizontal) == MediaImageFlip.Horizontal)
                        {
                            BufferedHorizontalFlipOnInputData((byte*)bufferPtr,
                                                              videoFrame.Buffer.Length,
                                                              Video.InputWidth,
                                                              Video.InputHeight,
                                                              Video.InputBytesPerPixel);
                        }

                        Debug.Assert((videoFrame.Buffer.Length % Video.InputHeight) == 0);
                        Debug.Assert((videoFrame.Buffer.Length % Video.InputLinesize) == 0);

                        Assert(sws_scale(Video.Native->SwsContext,
                                         new byte_ptr8 { [0] = (byte*)bufferPtr },
                                         new int8 { [0] = videoFrame.Buffer.Length / Video.InputHeight },
                                         0,
                                         Video.InputHeight,
                                         frame->data,
                                         frame->linesize));
                    });

                    AVFrame* sendFrame = frame;

                    if (Video.HwAcceleration)
                    {
                        Assert(av_hwframe_transfer_data(hwFrame, frame, 0));
                        sendFrame = hwFrame;
                    }

                    sendFrame->pts = videoFrame.PresentationTimeStamp;

                    InterleavedWriteFrame(context, stream, sendFrame, packet);

                    _encoderVideoAvgTimeChrono.Stop();
                    _encoderVideoRunTimeChrono.Stop();

                    lock (_encoderStatsLock)
                    {
                        _encoderVideoLastFrameTime = _encoderVideoRunTimeChrono.Elapsed;
                        _encoderVideoTotalFrameTime = _encoderVideoAvgTimeChrono.Elapsed;
                        _encoderVideoTotalFrameCount++;
                    }
                }

                while (TryDequeueAudioFrame(out AudioCaptureFrame audioFrame))
                {
                    Interlocked.Decrement(ref _audioFrameQueueSize);
                    Debug.Assert(_audioFrameQueueSize >= 0);

                    Interlocked.Decrement(ref _frameQueueSize);
                    Debug.Assert(_frameQueueSize >= 0);

                    AudioState audio = Audio[audioFrame.SessionIndex];

                    if (audio == null || !audio.Configured)
                    {
                        // Return buffer to pool
                        audioFrame.Buffer.Dispose();

                        // Drop frames assigned to invalid session
                        continue;
                    }

                    if (audioFrame.PresentationTimeStamp <= audio.LastPts)
                    {
                        // Return buffer to pool
                        audioFrame.Buffer.Dispose();

                        // Drop backwards frames
                        continue;
                    }

                    if (lastAudioPts < audioFrame.PresentationTimeStamp)
                    {
                        lastAudioPts = audioFrame.PresentationTimeStamp;
                    }

                    audio.LastPts = audioFrame.PresentationTimeStamp;

                    AVCodecContext* context = audio.Native->Context;
                    AVStream* stream = audio.Native->Stream;
                    AVFrame* inFrame = audio.Native->InputFrame;
                    AVFrame* outFrame = audio.Native->OutputFrame;
                    AVPacket* packet = audio.Native->Packet;
                    AVAudioFifo* fifo = audio.Native->Fifo;

                    Debug.Assert(audio.SampleCount == audioFrame.FrameBufferSampleCount);

                    if (audio.UsingDirectSampleEncoding)
                    {
                        AVFrame* sendFrame;

                        audioFrame.Buffer.ConsumeFixed(bufferPtr =>
                        {
                            Buffer.MemoryCopy((void*)bufferPtr,
                                inFrame->data[0],
                                inFrame->linesize[0],
                                audioFrame.Buffer.Length);
                        });

                        if (audio.SourceSampleFmt != audio.TargetSampleFmt)
                        {
                            Assert(swr_convert_frame(audio.Native->SwrContext, outFrame, inFrame));
                            sendFrame = outFrame;
                        }
                        else
                        {
                            sendFrame = inFrame;
                        }

                        sendFrame->pts = audioFrame.PresentationTimeStamp;

                        InterleavedWriteFrame(context, stream, sendFrame, packet);
                    }
                    else
                    {
                        int currentFifoSize = av_audio_fifo_size(fifo);

                        // av_audio_fifo_realloc() will do the checks whether the reallocation is actually required
                        Assert(av_audio_fifo_realloc(fifo, audioFrame.FrameBufferSampleCount + currentFifoSize));

                        audioFrame.Buffer.ConsumeFixed(bufferPtr =>
                        {
                            int writtenSampleCount = Assert(av_audio_fifo_write(fifo, (void**)&bufferPtr, audioFrame.FrameBufferSampleCount));
                            Debug.Assert(writtenSampleCount == audioFrame.FrameBufferSampleCount);
                        });

                        // Calculate the difference in the timestamps of the first video- and audio-frame
                        // and use that difference as a correction-factor to prevent asynchronous audio.
                        // If no video-frame has been presented yet, set the correction-factor to zero.
                        long videoPtsLead = Video.FramePresented ? (audio.InitialPts - Video.InitialPts) : 0;

                        int frameSize = context->frame_size;
                        byte*[] inFrameData = inFrame->data;

                        bool shouldDrainFifoQueue = !_frameQueueRunning && (_audioFrameQueueSize == 0);

                        fixed (byte** inFrameDataPtr = &inFrameData[0])
                        {
                            int ret;

                            // Dequeue samples while enough data for a full output-frame is available, or until it is
                            // empty if we are flushing the encoder-queue and have reached the final audio frame
                            while ((av_audio_fifo_size(fifo) >= frameSize) ||
                                   (shouldDrainFifoQueue && av_audio_fifo_size(fifo) > 0))
                            {
                                int readSampleCount = Assert(av_audio_fifo_read(fifo, (void**)inFrameDataPtr, frameSize));
                                AVFrame* sendFrame;

                                Debug.Assert(shouldDrainFifoQueue || readSampleCount == frameSize);

                                if (audio.SourceSampleFmt != audio.TargetSampleFmt)
                                {
                                    Assert(swr_convert_frame(audio.Native->SwrContext, outFrame, inFrame));
                                    sendFrame = outFrame;
                                }
                                else
                                {
                                    sendFrame = inFrame;
                                }

                                // The internal timebase is the sample-rate (samples per second). Scale this to the timebase of
                                // the encoder-context and add the interval between the first video-frame and the first frame of
                                // the current audio-session to mitigate asynchronous audio.
                                sendFrame->pts = av_rescale_q(audio.NumberOfEncodedSamples, audio.EncodingTimeBase, context->time_base) + videoPtsLead;
                                audio.NumberOfEncodedSamples += readSampleCount;

                                Assert(avcodec_send_frame(context, sendFrame));
                                ret = avcodec_receive_packet(context, packet);

                                if (ret == AVERROR_EAGAIN || ret == AVERROR_EOF)
                                {
                                    continue;
                                }

                                Assert(ret);

                                av_packet_rescale_ts(packet, context->time_base, stream->time_base);

                                packet->stream_index = stream->index;

                                ret = av_interleaved_write_frame(Output.Native->Context, packet);

                                // If strict-mode is disabled, ignore invalid frames/packet-data
                                if (StrictMode || ret != AVERROR_EINVAL)
                                {
                                    Assert(ret);
                                }
                            }
                        }
                    }
                }
            }

            // flush encoders
            if (Video.Configured)
            {
                InterleavedWriteFrame(Video.Native->Context, Video.Native->Stream, null, Video.Native->Packet);
            }

            for (int i = 0; i < Audio.Length; i++)
            {
                if (Audio[i].Configured)
                {
                    InterleavedWriteFrame(Audio[i].Native->Context, Audio[i].Native->Stream, null, Audio[i].Native->Packet);
                }
            }
        }

        private bool TryDequeueVideoFrame(out VideoCaptureFrame videoFrame)
        {
            lock (_videoFrameQueueLock)
            {
                return _videoFrameQueue.TryDequeue(out videoFrame);
            }
        }

        private bool TryDequeueAudioFrame(out AudioCaptureFrame audioFrame)
        {
            lock (_audioFrameQueueLock)
            {
                return _audioFrameQueue.TryDequeue(out audioFrame);
            }
        }

        private void InterleavedWriteFrame(AVCodecContext* context, AVStream* stream, AVFrame* frame, AVPacket* packet)
        {
            int ret = 0;

            Assert(avcodec_send_frame(context, frame));

            while (ret >= 0)
            {
                ret = avcodec_receive_packet(context, packet);

                if (ret == AVERROR_EAGAIN || ret == AVERROR_EOF)
                {
                    break;
                }
                else if (ret < 0)
                {
                    Assert(ret);
                }

                av_packet_rescale_ts(packet, context->time_base, stream->time_base);

                packet->stream_index = stream->index;

                ret = av_interleaved_write_frame(Output.Native->Context, packet);

                // If strict-mode is disabled, ignore invalid frames/packet-data
                if (StrictMode || ret != AVERROR_EINVAL)
                {
                    Assert(ret);
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _videoFrameQueue = null;
                _audioFrameQueue = null;

                _chrono = null;
                _encoderVideoAvgTimeChrono = null;
                _encoderVideoRunTimeChrono = null;

                _frameQueueThread = null;

                _encoderStatsTimer.Dispose();
                _encoderStatsTimer = null;

                DisposeAudioState();
                DisposeVideoState();
                DisposeOutputState();
            }
        }

        private void DisposeOutputState()
        {
            NativeOutputState* outputNative = Output.Native;

            if (outputNative != null)
            {
                if (outputNative->Context != null)
                {
                    if (outputNative->Context->pb != null)
                    {
                        avio_closep(&outputNative->Context->pb);
                    }

                    avformat_free_context(outputNative->Context);
                }

                Output.Native = null;
                NativeMemory.Free(outputNative);

                outputNative = null;
            }
        }

        private void DisposeVideoState()
        {
            NativeVideoState* videoNative = Video.Native;

            if (videoNative != null)
            {
                if (videoNative->VflipStrideBuffer != null)
                {
                    NativeMemory.Free(videoNative->VflipStrideBuffer);
                    videoNative->VflipStrideBuffer = null;
                }

                if (videoNative->SwsContext != null)
                {
                    sws_freeContext(videoNative->SwsContext);
                    videoNative->SwsContext = null;
                }

                if (videoNative->Packet != null)
                {
                    av_packet_free(&videoNative->Packet);
                }

                if (videoNative->HwFrame != null)
                {
                    av_frame_free(&videoNative->HwFrame);
                }

                if (videoNative->Frame != null)
                {
                    av_frame_free(&videoNative->Frame);
                }

                if (videoNative->Context != null)
                {
                    avcodec_free_context(&videoNative->Context);
                }

                if (videoNative->HwDeviceContextRef != null)
                {
                    av_buffer_unref(&videoNative->HwDeviceContextRef);
                }

                Video.Native = null;
                NativeMemory.Free(videoNative);

                videoNative = null;
            }
        }

        private void DisposeAudioState()
        {
            for (int i = 0; i < Audio.Length; i++)
            {
                AudioState audio = Audio[i];
                NativeAudioState* audioNative = Audio[i].Native;

                if (audioNative != null)
                {
                    if (audioNative->Fifo != null)
                    {
                        av_audio_fifo_free(audioNative->Fifo);
                        audioNative->Fifo = null;
                    }

                    if (audioNative->SwrContext != null)
                    {
                        swr_free(&audioNative->SwrContext);
                    }

                    if (audioNative->Packet != null)
                    {
                        av_packet_free(&audioNative->Packet);
                    }

                    if (audioNative->OutputFrame != null)
                    {
                        av_frame_free(&audioNative->OutputFrame);
                    }

                    if (audioNative->InputFrame != null)
                    {
                        av_frame_free(&audioNative->InputFrame);
                    }

                    if (audioNative->Context != null)
                    {
                        avcodec_free_context(&audioNative->Context);
                    }

                    audio.Native = null;
                    NativeMemory.Free(audioNative);

                    audioNative = null;
                }
            }
        }

        private static bool QueryCodecPixelFormats(AVCodec* codec, AVPixelFormat pixelFormat)
        {
            AVPixelFormat* ptr = codec->pix_fmts;

            while (ptr != null && (int)*ptr != -1)
            {
                if (*ptr == pixelFormat)
                {
                    return true;
                }

                ptr++;
            }

            return false;
        }

        private static bool FFmpegBuildSupportsHWDeviceType(AVHWDeviceType queryType)
        {
            AVHWDeviceType type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;

            while ((type = av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                if (type == queryType)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Assert(int ret, [CallerMemberName] string caller = null)
        {
            if (ret < 0)
            {
                const int bufferSize = 1024;
                byte* buffer = stackalloc byte[bufferSize];

                if (av_strerror(ret, buffer, bufferSize) == 0)
                {
                    throw new FFmpegException($"{caller}(): {Marshal.PtrToStringAnsi((nint)buffer).TrimEnd('\0')}");
                }
                else
                {
                    throw new FFmpegException($"{caller}(): Error {ret}");
                }
            }

            return ret;
        }

        private static unsafe void BufferedVerticalFlipOnInputData(byte* inputData, int inputDataLength, byte* strideBuffer, int strideBufferLength, int height, int linesize)
        {
            Span<byte> inputDataSpan = new(inputData, inputDataLength);
            Span<byte> strideBufferSpan = new(strideBuffer, strideBufferLength);

            for (int y = 0; y < height >> 1; y++)
            {
                Span<byte> strideDataSpan = inputDataSpan.Slice(y * linesize, linesize);
                Span<byte> flipStrideDataSpan = inputDataSpan.Slice((height - 1 - y) * linesize, linesize);

                strideDataSpan.CopyTo(strideBufferSpan);
                flipStrideDataSpan.CopyTo(strideDataSpan);
                strideBufferSpan.CopyTo(flipStrideDataSpan);
            }
        }

        private static void BufferedHorizontalFlipOnInputData(byte* inputData, int inputDataLength, int width, int height, int bytesPerPixel)
        {
            Debug.Assert(inputDataLength == (width * height * bytesPerPixel));

            if (bytesPerPixel == 4)
            {
                BufferedHorizontalFlipOnInputDataAligned<int>(inputData, width, height);
            }
            else
            {
                throw new NotImplementedException("Buffered horizontal flip for images with {bytesPerPixel} byte(s) per pixel are not supported");
            }
        }

        private static void BufferedHorizontalFlipOnInputDataAligned<TPixel>(byte* data, int width, int height)
            where TPixel : struct
        {
            Span<TPixel> dataSpan = new Span<TPixel>(data, width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    int srcIndex = (y * width) + x;
                    int dstIndex = (y * width) + (width - 1 - x);

                    (dataSpan[dstIndex], dataSpan[srcIndex]) = (dataSpan[srcIndex], dataSpan[dstIndex]);
                }
            }
        }

    }

}
