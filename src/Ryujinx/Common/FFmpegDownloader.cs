using Ryujinx.Ava;
using Ryujinx.Common.Configuration;
using Ryujinx.UI.Helpers;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Common
{

    sealed partial class FFmpegDownloader
    {

        private const string DefaultVersion = "7.1";

        private const string RepositoryLink = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest";
        private const string RepositoryMasterLink = $"{RepositoryLink}/ffmpeg-master-latest-[platform]-gpl-shared.[extension]";
        private const string RepositoryReleaseLink = $"{RepositoryLink}/ffmpeg-n[version]-latest-[platform]-gpl-shared-[version].[extension]";

        [GeneratedRegex(@"\/(?<targetFileName>(avcodec|avdevice|avfilter|avformat|avutil|postproc|swresample|swscale)-[0-9]+?\.dll)$")]
        private static partial Regex ArchiveFileNameExpressionForWindows();

        // Match full name (libavcodec.so.XX.YY.ZZZ) but discard everything past the major-part (libavcodec.so.XX)
        [GeneratedRegex(@"\/(?<targetFileName>lib(avcodec|avdevice|avfilter|avformat|avutil|postproc|swresample|swscale)\.so.[0-9]+).[0-9]+.[0-9]+$")]
        private static partial Regex ArchiveFileNameExpressionForLinux();

        private readonly string _version;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private volatile bool _cancelled = false;

        private Thread _bgThread;
        private bool? _bgResult = null;

        public UIProgressReporter ProgressReporter { get; } = new()
        {
            Type = ProgressType.Bytes,
        };

        public FFmpegDownloader()
            : this(DefaultVersion)
        { }

        public FFmpegDownloader(string version)
        {
            _version = version;
            _cancellationTokenSource = new CancellationTokenSource();

            ProgressReporter.Cancelled += OnProgressReporterCancelled;
        }

        public static bool CanDownloadOnCurrentPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        return true;
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                    case Architecture.Arm64:
                        return true;
                }
            }

            return false;
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void RunBackground(Source source)
        {
            _bgThread = new Thread(ThreadStart)
            {
                Name = "FFmpegDownloader.BackgroundThread",
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true,
            };

            _bgThread.Start();

            async void ThreadStart()
            {
                _bgResult = await Run(source);
            }
        }

        public bool WaitForBackgroundRun()
        {
            _bgThread.Join();

            bool? bgResult = _bgResult;

            if (!bgResult.HasValue)
            {
                throw new InvalidOperationException();
            }

            return bgResult.Value;
        }

        public async Task<bool> Run(Source source)
        {
            string downloadUrlString = BuildDownloadURL(source);

            using MemoryStream memoryStream = new MemoryStream();

            //
            // Downloading
            //
            ProgressReporter.ReportProgress(downloadUrlString);

            using HttpClient httpClient = new HttpClient();

            using HttpResponseMessage response = await httpClient.GetAsync(
                downloadUrlString,
                HttpCompletionOption.ResponseHeadersRead,
                _cancellationTokenSource.Token);

            using Stream responseStream = response.Content.ReadAsStream();

            long? contentLength = response.Content.Headers.ContentLength;

            byte[] bytes = new byte[4096];
            int bytesRead;

            long totalBytesRead = 0;

            DateTime measureLastTime = DateTime.Now;
            long measureLastTotal = 0;

            while ((bytesRead = responseStream.Read(bytes, 0, bytes.Length)) > 0)
            {
                memoryStream.Write(bytes, 0, bytesRead);

                totalBytesRead += bytesRead;
                DispatchProgress(false);

                if (_cancelled)
                {
                    return false;
                }
            }

            DispatchProgress(true);

            //
            // Extracting
            //
            memoryStream.Seek(0, SeekOrigin.Begin);

            using IReader archiveReader = ReaderFactory.Open(memoryStream);

            Regex archiveFileNameExpression;

            if (OperatingSystem.IsWindows())
            {
                archiveFileNameExpression = ArchiveFileNameExpressionForWindows();
            }
            else if (OperatingSystem.IsLinux())
            {
                archiveFileNameExpression = ArchiveFileNameExpressionForLinux();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            while (archiveReader.MoveToNextEntry())
            {
                if (archiveReader.Entry.IsDirectory)
                {
                    continue;
                }

                Match archiveFileNameMatch = archiveFileNameExpression.Match(archiveReader.Entry.Key);

                if (archiveFileNameMatch.Success)
                {
                    string targetFileName = archiveFileNameMatch.Groups["targetFileName"].Value;
                    string targetFilePath = Path.Combine(Program.AppDataNativeRuntimesDirectory, targetFileName);

                    ProgressReporter.ReportProgress(targetFileName);

                    if (!File.Exists(targetFilePath))
                    {
                        if (!Directory.Exists(Program.AppDataNativeRuntimesDirectory))
                        {
                            Directory.CreateDirectory(Program.AppDataNativeRuntimesDirectory);
                        }

                        archiveReader.WriteEntryTo(targetFilePath);
                    }
                }

                if (_cancelled)
                {
                    return false;
                }
            }

            ProgressReporter.Finish();

            return true;

            void DispatchProgress(bool final)
            {
                DateTime nowTime = DateTime.Now;
                TimeSpan timeDelta = nowTime - measureLastTime;

                if (final)
                {
                    if (contentLength.HasValue)
                    {
                        ProgressReporter.ReportProgress(downloadUrlString, totalBytesRead, contentLength.Value, 0);
                    }
                    else
                    {
                        ProgressReporter.ReportProgress(downloadUrlString, totalBytesRead, totalBytesRead, 0);
                    }
                }
                else if (timeDelta.TotalSeconds >= .5 || (!contentLength.HasValue || totalBytesRead >= contentLength.Value))
                {
                    long delta = totalBytesRead - measureLastTotal;
                    double speed = delta / timeDelta.TotalSeconds;

                    if (contentLength.HasValue)
                    {
                        ProgressReporter.ReportProgress(downloadUrlString, totalBytesRead, contentLength.Value, speed);
                    }
                    else
                    {
                        ProgressReporter.ReportProgress(downloadUrlString, totalBytesRead, speed);
                    }

                    measureLastTime = nowTime;
                    measureLastTotal = totalBytesRead;
                }
            }
        }

        private string BuildDownloadURL(Source source)
        {
            string baseUrl = source switch
            {
                Source.Master => RepositoryMasterLink,
                Source.Release => RepositoryReleaseLink,
                _ => throw new ArgumentException("Unknown FFmpeg repository source type", nameof(source))
            };

            string platform;
            string extension;

            if (OperatingSystem.IsWindows())
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        platform = "win64";
                        extension = "zip";
                        break;
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        platform = "linux64";
                        extension = "tar.xz";
                        break;
                    case Architecture.Arm64:
                        platform = "linuxarm64";
                        extension = "tar.xz";
                        break;
                    default:
                        throw new PlatformNotSupportedException();
                }
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (platform == null || extension == null)
            {
                throw new InvalidOperationException();
            }

            return baseUrl
                .Replace("[version]", _version)
                .Replace("[platform]", platform)
                .Replace("[extension]", extension);
        }

        private void OnProgressReporterCancelled(object sender, EventArgs e)
        {
            _cancelled = true;
        }

        public enum Source
        {
            Master,
            Release
        }

        public class ExtractingFileEventArgs : EventArgs
        {

            public string FileName { get; }

            public ExtractingFileEventArgs(string fileName)
            {
                FileName = fileName;
            }

        }

    }

}
