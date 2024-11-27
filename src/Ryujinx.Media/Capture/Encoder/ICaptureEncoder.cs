using Ryujinx.Media.Capture.Encoder.Configuration;
using Ryujinx.Media.Capture.Encoder.Frames;
using System;

namespace Ryujinx.Media.Capture.Encoder
{

    interface ICaptureEncoder : IDisposable
    {

        TimeSpan Elapsed { get; }

        double EncoderFPSAverage { get; }

        double EncoderFPSLastRun { get; }

        bool IsActiveConfiguration(VideoCaptureConfiguration videoConfig);

        bool IsActiveConfiguration(AudioCaptureConfiguration audioConfig);

        bool ConfigureOutput(CaptureOutputFormat outputFormat,
                             CaptureVideoCodec videoCodec,
                             CaptureAudioCodec audioCodec,
                             string outputPath);

        bool ConfigureVideoStream(int width,
                                  int height,
                                  MediaPixelFormat pixelFormat,
                                  CaptureVideoCodec codec,
                                  int scaleWidth,
                                  int scaleHeight,
                                  bool useBitrate,
                                  long bitrate,
                                  bool useQualityLevel,
                                  int qualityLevel,
                                  bool lossless,
                                  int threadCount,
                                  CaptureVideoHardwareDevice allowedHardwareDevices);

        bool ConfigureAudioStream(int sessionIndex,
                                  MediaSampleFormat sampleFormat,
                                  uint sampleRate,
                                  uint sampleCount,
                                  uint channelCount,
                                  CaptureAudioCodec codec,
                                  long bitrate);

        void BeginEncoding();

        void FinishEncoding();

        bool EnqueueFrame(GenericCaptureFrame genericFrame);

    }

}
