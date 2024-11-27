using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.Media
{

    public static class FFmpegModule
    {

        private static readonly string[] _searchPaths = new[]
        {
            CommonRuntimeInformation.ApplicationNativeRuntimesDirectory,
            CommonRuntimeInformation.ApplicationDirectory,
            ""
        };

        private static readonly string[] _requiredLibraries = new[]
        {
            "avcodec",
            "avdevice",
            "avfilter",
            "avformat",
            "avutil",
            "swresample",
            "swscale",
        };

        public static bool IsInitialized { get; private set; }

        public static string[] AvailableLibraries { get; private set; }
        
        public static bool Initialize(params string[] additionalSearchPaths)
        {
            foreach (string searchPath in additionalSearchPaths)
            {
                if (TryInitializeWithSearchPath(searchPath))
                {
                    return true;
                }
            }

            foreach (string searchPath in _searchPaths)
            {
                if (TryInitializeWithSearchPath(searchPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryInitializeWithSearchPath(string searchPath)
        {
            bool librariesPresent = true;
            string realSearchPath = null;

            List<string> availableLibraries = new();

            if (string.IsNullOrWhiteSpace(searchPath) || !Directory.Exists(searchPath))
            {
                return false;
            }

            foreach (KeyValuePair<string, int> libraryVersionEntry in DynamicallyLoadedBindings.LibraryVersionMap)
            {
                string libraryFileName = GetLibraryFileName(libraryVersionEntry.Key, libraryVersionEntry.Value);

                string libraryFullPath = !string.IsNullOrWhiteSpace(searchPath)
                    ? Path.Combine(searchPath, libraryFileName)
                    : libraryFileName;

                if (File.Exists(libraryFullPath))
                {
                    availableLibraries.Add(libraryVersionEntry.Key);

                    if (realSearchPath == null)
                    {
                        realSearchPath = Path.GetFullPath(Path.GetDirectoryName(libraryFullPath));
                    }
                }
                else
                {
                    if (_requiredLibraries.Contains(libraryVersionEntry.Key))
                    {
                        librariesPresent = false;
                    }
                }
            }

            if (librariesPresent && realSearchPath != null && Directory.Exists(realSearchPath))
            {
                DynamicallyLoadedBindings.LibrariesPath = realSearchPath;
                DynamicallyLoadedBindings.Initialize();

                ffmpeg.av_log_set_level(ffmpeg.AV_LOG_MAX_OFFSET);

                unsafe
                {
                    ffmpeg.av_log_set_callback((av_log_set_callback_callback_func)PrivateLogCallback);
                }

                IsInitialized = true;
                AvailableLibraries = availableLibraries.ToArray();

                return true;
            }

            return false;
        }

        private static string GetLibraryFileName(string libraryName, int version)
        {
            if (OperatingSystem.IsWindows())
            {
                return $"{libraryName}-{version}.dll";
            }
            else if (OperatingSystem.IsLinux())
            {
                return $"lib{libraryName}.so.{version}";
            }
            else if (OperatingSystem.IsMacOS())
            {
                return $"lib{libraryName}.{version}.dylib";
            }

            throw new PlatformNotSupportedException();
        }

        private static unsafe void PrivateLogCallback(void* avcl, int level, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt, byte* vl)
        {
            if (level > ffmpeg.av_log_get_level())
            {
                return;
            }

            int lineSize = 1024;
            byte* lineBuffer = stackalloc byte[lineSize];
            int printPrefix = 1;

            ffmpeg.av_log_format_line(avcl, level, fmt, vl, lineBuffer, lineSize, &printPrefix);

            string line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer).Trim();

            switch (level)
            {
                case ffmpeg.AV_LOG_PANIC:
                case ffmpeg.AV_LOG_FATAL:
                case ffmpeg.AV_LOG_ERROR:
                    Logger.Error?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case ffmpeg.AV_LOG_WARNING:
                    Logger.Warning?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case ffmpeg.AV_LOG_INFO:
                    Logger.Info?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case ffmpeg.AV_LOG_VERBOSE:
                case ffmpeg.AV_LOG_DEBUG:
                    Logger.Debug?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case ffmpeg.AV_LOG_TRACE:
                    Logger.Trace?.Print(LogClass.Media, line, "FFmpeg");
                    break;
            }
        }

    }

}
