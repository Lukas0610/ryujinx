using Ryujinx.Common.Buffers;
using Ryujinx.Common.Buffers.Unsafe;
using Ryujinx.Common.Logging;
using Ryujinx.Media.Capture.Encoder;
using Ryujinx.Media.Capture.Encoder.Configuration;
using Ryujinx.Media.Capture.Encoder.Frames;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Media.Capture
{

    public sealed class CaptureHandler : IDisposable
    {

        /// <summary>
        /// Base-value to calculate FPS from the swap-interval provided by the surface flinger.
        /// </summary>
        /// <remarks>
        /// Should match <c>SurfaceFlinger.TargetFps</c>
        /// </remarks>
        internal const int SurfaceFlingerSwapIntervalBase = 60;

        /// <summary>
        /// Defines how many concurrent audio-states are supported by the capture-implementation
        /// </summary>
        internal const int NumberOfSupportedAudioSessions = 8;

        private readonly Lock _lock = new();
        private readonly Lock _encoderLock = new();

        private volatile ICaptureEncoder _encoder;

        private volatile VideoCaptureConfiguration _videoConfig;
        private volatile AudioCaptureConfiguration[] _audioConfig;

        private volatile CaptureConfigurationEventArgs _userConfig;

        private int _audioSessionCount;

        private volatile bool _enabled;
        private volatile bool _running;

        public event EventHandler StateChanged;
        public event EventHandler<CaptureConfigurationEventArgs> CreateConfiguration;

        /// <summary>
        /// Buffer-Pool for video frames
        /// </summary>
        /// <remarks>MinSize = 4MB. InitialMaxSize = 64MB</remarks>
        public IBufferPool VideoBufferPool { get; } = new UnsafeBufferPool(4194304, 5, 0, false);

        /// <summary>
        /// Buffer-Pool for audio frames
        /// </summary>
        /// <remarks>MinSize=128. InitialMaxSize=16KB</remarks>
        public IBufferPool AudioBufferPool { get; } = new UnsafeBufferPool(128, 8, 0, false);

        /// <summary>
        /// The swap interval as requested by the game
        /// </summary>
        /// <remarks>
        /// To be provided by the surface-flinger
        /// </remarks>
        public int SwapInterval { get; set; }

        public bool Enabled => _enabled;

        public bool Running => _running;

        public TimeSpan? Elapsed => _encoder?.Elapsed;

        public double? EncoderFPSAverage => _encoder?.EncoderFPSAverage;

        public double? EncoderFPSLastRun => _encoder?.EncoderFPSLastRun;

        public CaptureHandler()
        {
            _audioConfig = new AudioCaptureConfiguration[NumberOfSupportedAudioSessions];
        }

        public int GetNextAudioSessionIndex()
        {
            return Interlocked.Increment(ref _audioSessionCount) - 1;
        }

        public bool UpdateConfiguration(VideoCaptureConfiguration videoConfig)
        {
            lock (_lock)
            {
                if (_videoConfig != null && _videoConfig.Equals(videoConfig))
                {
                    // no changes compared to last-known configuration
                    return true;
                }

                _videoConfig = videoConfig;

                if (Running)
                {
                    Logger.Info?.Print(LogClass.Capture, "Starting new encoder-session (Video-configuration changed)");

                    if (!CreateEncoder(true))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool UpdateConfiguration(AudioCaptureConfiguration audioConfig)
        {
            lock (_lock)
            {
                if (_audioConfig[audioConfig.SessionIndex] != null &&
                    _audioConfig[audioConfig.SessionIndex].Equals(audioConfig))
                {
                    // no changes compared to last-known configuration
                    return true;
                }

                _audioConfig[audioConfig.SessionIndex] = audioConfig;

                if (Running)
                {
                    Logger.Info?.Print(LogClass.Capture, $"Starting new encoder-session (Audio-configuration on session #{audioConfig.SessionIndex} changed)");

                    if (!CreateEncoder(true))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Provides a frame to the sink, which will then pass it on to the encoder.
        /// Will also reconfigure the encoder if required (e.g. changed resolution).
        /// </summary>
        /// <param name="genericFrame">The frame to the encoded</param>
        /// <returns>Whether the frame wass successfully enqueued to be encoded</returns>
        public bool EnqueueFrame(GenericCaptureFrame genericFrame)
        {
            if (!FFmpegModule.IsInitialized)
            {
                // Return buffer to pool if present
                genericFrame.Buffer?.Dispose();

                return false;
            }

            lock (_lock)
            {
                if (!_enabled || !_running)
                {
                    return false;
                }
            }

            lock (_encoderLock)
            {
                if (_encoder == null)
                {
                    // Return buffer to pool if present
                    genericFrame.Buffer?.Dispose();

                    return false;
                }

                return _encoder.EnqueueFrame(genericFrame);
            }
        }

        /// <summary>
        /// Allow the user to start a capture
        /// </summary>
        /// <returns><see langword="true"/> if the operation was successful, <see langword="false"/> otherwise</returns>
        public bool Enable()
        {
            lock (_lock)
            {
                if (_enabled)
                {
                    return false;
                }

                _enabled = true;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Lock down the handler to prevent further captures from being started
        /// </summary>
        /// <returns><see langword="true"/> if the operation was successful, <see langword="false"/> otherwise</returns>
        public bool Disable()
        {
            lock (_lock)
            {
                if (!_enabled)
                {
                    return false;
                }

                _enabled = true;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Start the capture
        /// </summary>
        /// <returns><see langword="true"/> if the operation was successful, <see langword="false"/> otherwise</returns>
        public bool Start()
        {
            lock (_lock)
            {
                if (!_enabled || _running)
                {
                    return false;
                }

                _running = true;

                if (_videoConfig != null && !CreateEncoder(true))
                {
                    _running = false;
                    return false;
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Stop the capture and flush the output-file
        /// </summary>
        /// <returns><see langword="true"/> if the operation was successful, <see langword="false"/> otherwise</returns>
        public bool Stop()
        {
            bool wasEnabled;

            lock (_lock)
            {
                if (!_running)
                {
                    return false;
                }

                _running = false;

                // disable handler while flushing the encoder
                wasEnabled = _enabled;
                _enabled = false;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);

            ClearEncoder();

            if (wasEnabled)
            {
                lock (_lock)
                {
                    // enable the handler again if it was initially
                    _enabled = true;
                }

                StateChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        public void Dispose()
        {
            ClearEncoder();

            VideoBufferPool.Dispose();
            AudioBufferPool.Dispose();
        }

        private bool CreateEncoder(bool flushAsync)
        {
            ICaptureEncoder newEncoder;

            RefreshUserConfiguration();

            if (FFmpegCaptureEncoder.Supports(_userConfig.Format, _userConfig.VideoCodec, _userConfig.AudioCodec))
            {
                newEncoder = new FFmpegCaptureEncoder(SwapInterval)
                {
                    // Disable strict-mode, skip over invalid frames instead of aborting
                    StrictMode = false
                };
            }
            else
            {
                throw new ArgumentException("No supported capture-encoder found for the provided configuration");
            }

            if (!newEncoder.ConfigureOutput(_userConfig.Format,
                                            _userConfig.VideoCodec,
                                            _userConfig.AudioCodec,
                                            _userConfig.OutputPath))
            {
                newEncoder.Dispose();
                ClearEncoder();

                return false;
            }

            if (_videoConfig != null && !ConfigureVideoStream(newEncoder, _videoConfig))
            {
                newEncoder.Dispose();
                ClearEncoder();

                return false;
            }

            for (int i = 0; i < _audioConfig.Length; i++)
            {
                if (_audioConfig[i] != null)
                {
                    if (!ConfigureAudioStream(newEncoder, _audioConfig[i]))
                    {
                        newEncoder.Dispose();
                        ClearEncoder();

                        return false;
                    }
                }
            }

            ICaptureEncoder oldEncoder;

            lock (_encoderLock)
            {
                newEncoder.BeginEncoding();

                oldEncoder = _encoder;
                _encoder = newEncoder;

                newEncoder = null;
            }

            if (oldEncoder != null)
            {
                if (flushAsync)
                {
                    Task.Run(FlushOldEncoder);
                }
                else
                {
                    FlushOldEncoder();
                }

                void FlushOldEncoder()
                {
                    oldEncoder.FinishEncoding();
                    oldEncoder.Dispose();
                }
            }

            return true;
        }

        private bool ConfigureVideoStream(ICaptureEncoder encoder, VideoCaptureConfiguration videoConfig)
        {
            return encoder.ConfigureVideoStream(
                videoConfig.Width,
                videoConfig.Height,
                videoConfig.PixelFormat,
                _userConfig.VideoCodec,
                _userConfig.VideoScaleWidth,
                _userConfig.VideoScaleHeight,
                _userConfig.VideoUseBitrate,
                _userConfig.VideoBitrate,
                _userConfig.VideoUseQualityLevel,
                _userConfig.VideoQualityLevel,
                _userConfig.VideoUseLossless,
                _userConfig.VideoEncodingThreadCount,
                _userConfig.VideoAllowedHardwareDevices);
        }

        private bool ConfigureAudioStream(ICaptureEncoder encoder, AudioCaptureConfiguration videoConfig)
        {
            return encoder.ConfigureAudioStream(
                videoConfig.SessionIndex,
                videoConfig.SampleFormat,
                videoConfig.SampleRate,
                videoConfig.SampleCount,
                videoConfig.ChannelCount,
                _userConfig.AudioCodec,
                _userConfig.AudioBitrate);
        }

        private bool RefreshUserConfiguration()
        {
            CaptureConfigurationEventArgs configArgs = new();

            CreateConfiguration?.Invoke(this, configArgs);

            if (string.IsNullOrWhiteSpace(configArgs.OutputPath))
            {
                return false;
            }

            _userConfig = configArgs;

            return true;
        }

        private void ClearEncoder()
        {
            ICaptureEncoder encoder;

            lock (_encoderLock)
            {
                encoder = _encoder;
                _encoder = null;
            }

            encoder?.FinishEncoding();
            encoder?.Dispose();
        }

    }

}

