using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Ryujinx.UI.Common.Configuration
{
    public class GameConfigurationFileFormat
    {
        /// <summary>
        /// The current version of the file format
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Version of the configuration file format
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Whether to use the game-specific configuration values instead of the global values
        /// </summary>
        public bool UseGameConfig { get; set; }

        /// <summary>
        /// Whether or not backend threading is enabled. The "Auto" setting will determine whether threading should be enabled at runtime.
        /// </summary>
        public BackendThreading BackendThreading { get; set; }

        /// <summary>
        /// Resolution Scale. An integer scale applied to applicable render targets. Values 1-4, or -1 to use a custom floating point scale instead.
        /// </summary>
        public int ResScale { get; set; }

        /// <summary>
        /// Custom Resolution Scale. A custom floating point scale applied to applicable render targets. Only active when Resolution Scale is -1.
        /// </summary>
        public float ResScaleCustom { get; set; }

        /// <summary>
        /// Max Anisotropy. Values range from 0 - 16. Set to -1 to let the game decide.
        /// </summary>
        public float MaxAnisotropy { get; set; }

        /// <summary>
        /// Aspect Ratio applied to the renderer window.
        /// </summary>
        public AspectRatio AspectRatio { get; set; }

        /// <summary>
        /// Applies anti-aliasing to the renderer.
        /// </summary>
        public AntiAliasing AntiAliasing { get; set; }

        /// <summary>
        /// Sets the framebuffer upscaling type.
        /// </summary>
        public ScalingFilter ScalingFilter { get; set; }

        /// <summary>
        /// Sets the framebuffer upscaling level.
        /// </summary>
        public int ScalingFilterLevel { get; set; }

        /// <summary>
        /// Dumps shaders in this local directory
        /// </summary>
        public string GraphicsShadersDumpPath { get; set; }

        /// <summary>
        /// Change System Language
        /// </summary>
        public Language SystemLanguage { get; set; }

        /// <summary>
        /// Change System Region
        /// </summary>
        public Region SystemRegion { get; set; }

        /// <summary>
        /// Change System TimeZone
        /// </summary>
        public string SystemTimeZone { get; set; }

        /// <summary>
        /// Change System Time Offset in seconds
        /// </summary>
        public long SystemTimeOffset { get; set; }

        /// <summary>
        /// Enables or disables Docked Mode
        /// </summary>
        public bool DockedMode { get; set; }

        /// <summary>
        /// Whether to hide cursor on idle, always or never
        /// </summary>
        public HideCursorMode HideCursor { get; set; }

        /// <summary>
        /// Enables or disables Vertical Sync
        /// </summary>
        public bool EnableVsync { get; set; }

        /// <summary>
        /// Enables or disables Shader cache
        /// </summary>
        public bool EnableShaderCache { get; set; }

        /// <summary>
        /// Enables or disables texture recompression
        /// </summary>
        public bool EnableTextureRecompression { get; set; }

        /// <summary>
        /// Enables or disables Macro high-level emulation
        /// </summary>
        public bool EnableMacroHLE { get; set; }

        /// <summary>
        /// Enables or disables color space passthrough, if available.
        /// </summary>
        public bool EnableColorSpacePassthrough { get; set; }

        /// <summary>
        /// Enables or disables profiled translation cache persistency
        /// </summary>
        public bool EnablePtc { get; set; }

        /// <summary>
        /// Whether to use streaming PTC (SPTC) instead of the traditional PTC
        /// </summary>
        public bool UseStreamingPtc { get; set; }

        /// <summary>
        /// Enables or disables guest Internet access
        /// </summary>
        public bool EnableInternetAccess { get; set; }

        /// <summary>
        /// Enables integrity checks on Game content files
        /// </summary>
        public bool EnableFsIntegrityChecks { get; set; }

        /// <summary>
        /// Whether to enable managed buffering of game file contents
        /// </summary>
        public bool EnableHostFsBuffering { get; set; }

        /// <summary>
        /// Whether to attempt to fully buffer game file contents before booting
        /// </summary>
        public bool EnableHostFsBufferingPrefetch { get; set; }

        /// <summary>
        /// Limit the size of the shared host file I/O cache
        /// </summary>
        public long HostFsBufferingMaxCacheSize { get; set; }

        /// <summary>
        /// Enables FS access log output to the console. Possible modes are 0-3
        /// </summary>
        public int FsGlobalAccessLogMode { get; set; }

        /// <summary>
        /// The selected audio backend
        /// </summary>
        public AudioBackend AudioBackend { get; set; }

        /// <summary>
        /// The audio volume
        /// </summary>
        public float AudioVolume { get; set; }

        /// <summary>
        /// The selected memory manager mode
        /// </summary>
        public MemoryManagerMode MemoryManagerMode { get; set; }

        /// <summary>
        /// Expands the RAM amount on the emulated system from 4GiB to 8GiB
        /// </summary>
        [Obsolete("Replaced by MemoryConfiguration")]
        public bool ExpandRam { get; set; }

        /// <summary>
        /// The selected memory configuration/size
        /// </summary>
        public MemoryConfiguration MemoryConfiguration { get; set; }

        /// <summary>
        /// Enable or disable ignoring missing services
        /// </summary>
        public bool IgnoreMissingServices { get; set; }

        /// <summary>
        /// Enable or disable keyboard support (Independent from controllers binding)
        /// </summary>
        public bool EnableKeyboard { get; set; }

        /// <summary>
        /// Enable or disable mouse support (Independent from controllers binding)
        /// </summary>
        public bool EnableMouse { get; set; }

        /// <summary>
        /// Hotkey Keyboard Bindings
        /// </summary>
        public KeyboardHotkeys Hotkeys { get; set; }

        /// <summary>
        /// Legacy keyboard control bindings
        /// </summary>
        /// <remarks>Kept for file format compatibility (to avoid possible failure when parsing configuration on old versions)</remarks>
        /// TODO: Remove this when those older versions aren't in use anymore.
        public List<JsonObject> KeyboardConfig { get; set; }

        /// <summary>
        /// Legacy controller control bindings
        /// </summary>
        /// <remarks>Kept for file format compatibility (to avoid possible failure when parsing configuration on old versions)</remarks>
        /// TODO: Remove this when those older versions aren't in use anymore.
        public List<JsonObject> ControllerConfig { get; set; }

        /// <summary>
        /// Input configurations
        /// </summary>
        public List<InputConfig> InputConfig { get; set; }

        /// <summary>
        /// Graphics backend
        /// </summary>
        public GraphicsBackend GraphicsBackend { get; set; }

        /// <summary>
        /// Preferred GPU
        /// </summary>
        public string PreferredGpu { get; set; }

        /// <summary>
        /// Multiplayer Mode
        /// </summary>
        public MultiplayerMode MultiplayerMode { get; set; }

        /// <summary>
        /// GUID for the network interface used by LAN (or 0 for default)
        /// </summary>
        public string MultiplayerLanInterfaceId { get; set; }

        /// <summary>
        /// Uses Hypervisor over JIT if available
        /// </summary>
        public bool UseHypervisor { get; set; }

        /// <summary>
        /// Use sparse function address tables if available
        /// </summary>
        public bool UseSparseAddressTable { get; set; }

        /// <summary>
        /// List of logical CPU cores the HLE kernel threads are allowed to run on
        /// </summary>
        public string HleKernelThreadsCPUSet { get; set; }

        /// <summary>
        /// Whether to assign a HLE kernel-thread to a single logical CPU core picked from the configured CPU set
        /// </summary>
        public bool HleKernelThreadsCPUSetStaticCore { get; set; }

        /// <summary>
        /// List of logical CPU cores the PTC background threads are allowed to run on
        /// </summary>
        public string PtcBackgroundThreadsCPUSet { get; set; }

        /// <summary>
        /// Number of PTC background threads to start
        /// </summary>
        public int PtcBackgroundThreadCount { get; set; }

        /// <summary>
        /// Whether to automatically begin capturing when starting the game
        /// </summary>
        public bool CaptureBeginOnStart { get; set; }

        /// <summary>
        /// The output-format used for game captures
        /// </summary>
        public CaptureOutputFormatValue CaptureOutputFormat { get; set; }

        /// <summary>
        /// The video-codec used for game captures
        /// </summary>
        public CaptureVideoCodecValue CaptureVideoCodec { get; set; }

        /// <summary>
        /// Whether to scale the game capture video to a predefined dimension
        /// </summary>
        public bool CaptureVideoScaleEnabled { get; set; }

        /// <summary>
        /// Width to scale the game capture video to.
        /// </summary>
        public int CaptureVideoScaleWidth { get; set; }

        /// <summary>
        /// Whether to scale the width of the video automatically depending on
        /// the configured height, keeping the original aspect ratio.
        /// </summary>
        public bool CaptureVideoScaleWidthAuto { get; set; }

        /// <summary>
        /// Height to scale the game capture video to.
        /// </summary>
        public int CaptureVideoScaleHeight { get; set; }

        /// <summary>
        /// Whether to scale the hight of the video automatically depending on
        /// the configured width, keeping the original aspect ratio.
        /// </summary>
        public bool CaptureVideoScaleHeightAuto { get; set; }

        /// <summary>
        /// Whether to control quality of the video-stream in game captures by bitrate
        /// </summary>
        public bool CaptureVideoUseBitrate { get; set; }

        /// <summary>
        /// Whether to control quality of the video-stream in game captures by a codec-dependent level
        /// </summary>
        public long CaptureVideoBitrate { get; set; }

        /// <summary>
        /// The level of quality of the video-stream in game captures (depending on the selected codec)
        /// </summary>
        public bool CaptureVideoUseQualityLevel { get; set; }

        /// <summary>
        /// The bitrate of the video-stream in game captures
        /// </summary>
        public int CaptureVideoQualityLevel { get; set; }

        /// <summary>
        /// Whether to try and encode video-stream in game captures without loss of image-information
        /// </summary>
        public bool CaptureVideoUseLossless { get; set; }

        /// <summary>
        /// Number of threads to use for encoding video-frames if multithreading is supported by the video-codec.
        /// </summary>
        public int CaptureVideoEncoderThreadCount { get; set; }

        /// <summary>
        /// Whether to use hardware-acceleration when encoding video-frames for game captures
        /// </summary>
        public bool CaptureVideoEncoderHardwareAcceleration { get; set; }

        /// <summary>
        /// Allow NVENC-devices to be considered when initializing hardware-acceleration
        /// </summary>
        public bool CaptureVideoEncoderHardwareAccelerationAllowNvenc { get; set; }

        /// <summary>
        /// Allow Intel QSV-devices to be considered when initializing hardware-acceleration
        /// </summary>
        public bool CaptureVideoEncoderHardwareAccelerationAllowQsv { get; set; }

        /// <summary>
        /// Allow Vulkan-devices to be considered when initializing hardware-acceleration
        /// </summary>
        public bool CaptureVideoEncoderHardwareAccelerationAllowVulkan { get; set; }

        /// <summary>
        /// The audio-codec used for game captures
        /// </summary>
        public CaptureAudioCodecValue CaptureAudioCodec { get; set; }

        /// <summary>
        /// Loads a configuration file from disk
        /// </summary>
        /// <param name="path">The path to the JSON configuration file</param>
        /// <param name="configurationFileFormat">Parsed configuration file</param>
        public static bool TryLoad(string path, out GameConfigurationFileFormat configurationFileFormat)
        {
            try
            {
                configurationFileFormat = JsonHelper.DeserializeFromFile(path, GameConfigurationFileFormatSettings.SerializerContext.GameConfigurationFileFormat);

                return configurationFileFormat.Version != 0;
            }
            catch
            {
                configurationFileFormat = null;

                return false;
            }
        }

        /// <summary>
        /// Save a configuration file to disk
        /// </summary>
        /// <param name="path">The path to the JSON configuration file</param>
        public void SaveConfig(string path)
        {
            JsonHelper.SerializeToFile(path, this, GameConfigurationFileFormatSettings.SerializerContext.GameConfigurationFileFormat);
        }
    }
}
