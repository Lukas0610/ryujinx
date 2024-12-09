using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static FFmpeg.AutoGen.Abstractions.ffmpeg;

namespace Ryujinx.Media
{

    public static class FFmpegModule
    {

        private record class CodecInfo(
            AVCodecID ID,
            string Name,
            string LongName
        );

        private record class OutputFormatInfo(
            string Name,
            string LongName,
            string[] Extensions
        );

        private const int VersionMajorShift = 16;
        private const uint VersionMajorMask = 0xFFFF0000;

        private const int VersionMinorShift = 8;
        private const uint VersionMinorMask = 0x0000FF00;

        private const int VersionMicroShift = 0;
        private const uint VersionMicroMask = 0x000000FF;

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

        public static string BuildInfoText(FFmpegModuleInfo infoFlags)
        {
            StringBuilder sb = new();

            if (infoFlags.HasFlag(FFmpegModuleInfo.Library))
            {
                string avcodecConfig = avcodec_configuration();

                sb.AppendLine("");
                sb.AppendLine("=========================== Library ===========================");
                sb.AppendLine("");
                sb.AppendFormatLine("FFmpeg {0}", av_version_info());
                sb.AppendLine();

                AppendLibraryInfo(sb, "avcodec", avcodecConfig);
                AppendLibraryInfo(sb, "avformat", avcodecConfig);
                AppendLibraryInfo(sb, "avdevice", avcodecConfig);
                AppendLibraryInfo(sb, "avfilter", avcodecConfig);
                AppendLibraryInfo(sb, "avutil", avcodecConfig);
                AppendLibraryInfo(sb, "swscale", avcodecConfig);
                AppendLibraryInfo(sb,"swresample", avcodecConfig);
                AppendLibraryInfo(sb, "postproc", avcodecConfig);

                sb.AppendLine();

                string[] configOptions = avcodecConfig.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (string configOption in configOptions)
                {
                    sb.AppendFormatLine("\t--{0}", configOption);
                }

                sb.AppendLine();
            }

            if (infoFlags.HasFlag(FFmpegModuleInfo.Codecs))
            {
                List<CodecInfo> codecInfos = new();

                unsafe
                {
                    void* opaque = null;
                    AVCodec* codec = null;

                    while ((codec = av_codec_iterate(&opaque)) != null)
                    {
                        string name = Marshal.PtrToStringAnsi((nint)codec->name).Trim();
                        string longName = Marshal.PtrToStringAnsi((nint)codec->long_name).Trim();

                        codecInfos.Add(new CodecInfo(codec->id, name, longName));
                    }
                }

                var groupedCodecs = codecInfos
                    .GroupBy(x => Enum.GetName(x.ID) ?? x.ID.ToString())
                    .OrderBy(x => x.Key);

                sb.AppendLine("");
                sb.AppendLine("=========================== Codecs ============================");
                sb.AppendLine("");

                foreach (var g in groupedCodecs)
                {
                    const string AVCodecIDPrefix = "AV_CODEC_ID_";

                    string avCodecIdString = g.Key.ToString();

                    if (avCodecIdString.StartsWith(AVCodecIDPrefix))
                    {
                        avCodecIdString = avCodecIdString[AVCodecIDPrefix.Length..];
                    }

                    sb.AppendFormatLine("{0}", avCodecIdString);
                    sb.AppendLine(new string('-', avCodecIdString.Length));

                    int codecInfoNameMaxLength = g.Max(x => x.Name.Length);

                    foreach (CodecInfo codecInfo in g)
                    {
                        sb.AppendFormatLine("{0} {1}", $"[ {codecInfo.Name} ]".PadRight(codecInfoNameMaxLength + 4, ' '), codecInfo.LongName);
                    }

                    sb.AppendLine();
                }
            }

            if (infoFlags.HasFlag(FFmpegModuleInfo.Formats))
            {
                List<OutputFormatInfo> outputFormatInfos = new();

                unsafe
                {
                    void* opaque = null;
                    AVOutputFormat* ofmt = null;

                    while ((ofmt = av_muxer_iterate(&opaque)) != null)
                    {
                        string name = Marshal.PtrToStringAnsi((nint)ofmt->name).Trim();
                        string longName = Marshal.PtrToStringAnsi((nint)ofmt->long_name).Trim();

                        string[] extensions = Array.Empty<string>();

                        if (ofmt->extensions != null)
                        {
                            extensions = Marshal.PtrToStringAnsi((nint)ofmt->extensions).Trim()
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        }

                        outputFormatInfos.Add(new OutputFormatInfo(name, longName, extensions));
                    }
                }

                var sortedOutputFormatInfos = outputFormatInfos
                    .OrderBy(x => x.Name);

                sb.AppendLine("");
                sb.AppendLine("=========================== Formats ===========================");
                sb.AppendLine("");

                int outputFormatInfoNameMaxLength = sortedOutputFormatInfos.Max(x => x.Name.Length);

                foreach (var outputFormatInfo in sortedOutputFormatInfos)
                {
                    string lineIndent = new string(' ', outputFormatInfoNameMaxLength + 4);

                    sb.AppendFormatLine("{0} {1}",
                        $"[ {outputFormatInfo.Name} ]".PadRight(outputFormatInfoNameMaxLength + 4, ' '),
                        outputFormatInfo.LongName);

                    if (outputFormatInfo.Extensions.Length >= 1)
                    {
                        sb.AppendFormatLine("{0} {1}",
                            lineIndent,
                            string.Join(", ", outputFormatInfo.Extensions.Select(x => $"*.{x}")));
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static void AppendLibraryInfo(StringBuilder sb, string libraryName, string avcodecConfig)
        {
            string upperLibraryName = libraryName.ToUpper();

            string config = Method<string>($"{libraryName}_configuration");
            string ident = Field<string>($"LIB{upperLibraryName}_IDENT");

            uint runtimeVersion = Method<uint>($"{libraryName}_version");
            uint runtimeVersionMajor = (runtimeVersion & VersionMajorMask) >> VersionMajorShift;
            uint runtimeVersionMinor = (runtimeVersion & VersionMinorMask) >> VersionMinorShift;
            uint runtimeVersionMicro = (runtimeVersion & VersionMicroMask) >> VersionMicroShift;
            string runtimeVersionString = AV_VERSION_DOT(runtimeVersionMajor, runtimeVersionMinor, runtimeVersionMicro);

            uint autogenVersion = (uint)Field<int>($"LIB{upperLibraryName}_VERSION_INT");
            uint autogenVersionMajor = (runtimeVersion & VersionMajorMask) >> VersionMajorShift;
            string autogenVersionString = Field<string>($"LIB{upperLibraryName}_VERSION");

            sb.AppendFormatLine("{0} {1} {2}",
                libraryName.PadRight(12, ' '),
                $"[ {ident} {runtimeVersionString} ]".PadRight(25, ' '),
                $"[ wrapper {autogenVersionString} ]");

            bool hasWarnings = false;

            if (runtimeVersionMajor != autogenVersionMajor)
            {
                sb.AppendFormatLine("\tWARNING: mismatching major versions of library ({0}) and wrapper ({1})", runtimeVersionMajor, autogenVersionMajor);
                hasWarnings = true;
            }

            if (avcodecConfig != config)
            {
                sb.AppendFormatLine("\tWARNING: mismatching build configuration");
                hasWarnings = true;
            }

            if (hasWarnings)
            {
                sb.AppendLine();
            }

            // Reflection
            T Method<T>(string name) => (T)typeof(ffmpeg).GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            T Field<T>(string name) => (T)typeof(ffmpeg).GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
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

                av_log_set_level(AV_LOG_MAX_OFFSET);

                unsafe
                {
                    av_log_set_callback((av_log_set_callback_callback_func)PrivateLogCallback);
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
            if (level > av_log_get_level())
            {
                return;
            }

            int lineSize = 1024;
            byte* lineBuffer = stackalloc byte[lineSize];
            int printPrefix = 1;

            av_log_format_line(avcl, level, fmt, vl, lineBuffer, lineSize, &printPrefix);

            string line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer).Trim();

            switch (level)
            {
                case AV_LOG_PANIC:
                case AV_LOG_FATAL:
                case AV_LOG_ERROR:
                    Logger.Error?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case AV_LOG_WARNING:
                    Logger.Warning?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case AV_LOG_INFO:
                    Logger.Info?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case AV_LOG_VERBOSE:
                case AV_LOG_DEBUG:
                    Logger.Debug?.Print(LogClass.Media, line, "FFmpeg");
                    break;
                case AV_LOG_TRACE:
                    Logger.Trace?.Print(LogClass.Media, line, "FFmpeg");
                    break;
            }
        }

    }

}
