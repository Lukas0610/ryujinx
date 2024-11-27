using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE;
using Ryujinx.Media.Capture;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace Ryujinx.UI.Common.Configuration
{
    public class GameConfigurationState
    {
        /// <summary>
        /// System configuration section
        /// </summary>
        public class SystemSection
        {
            /// <summary>
            /// Change System Language
            /// </summary>
            public ReactiveObject<Language> Language { get; private set; }

            /// <summary>
            /// Change System Region
            /// </summary>
            public ReactiveObject<Region> Region { get; private set; }

            /// <summary>
            /// Change System TimeZone
            /// </summary>
            public ReactiveObject<string> TimeZone { get; private set; }

            /// <summary>
            /// System Time Offset in Seconds
            /// </summary>
            public ReactiveObject<long> SystemTimeOffset { get; private set; }

            /// <summary>
            /// Enables or disables Docked Mode
            /// </summary>
            public ReactiveObject<bool> EnableDockedMode { get; private set; }

            /// <summary>
            /// Enables or disables profiled translation cache persistency
            /// </summary>
            public ReactiveObject<bool> EnablePtc { get; private set; }

            /// <summary>
            /// Whether to use streaming PTC (SPTC) instead of the traditional PTC
            /// </summary>
            public ReactiveObject<bool> UseStreamingPtc { get; private set; }

            /// <summary>
            /// Enables or disables guest Internet access
            /// </summary>
            public ReactiveObject<bool> EnableInternetAccess { get; private set; }

            /// <summary>
            /// Enables integrity checks on Game content files
            /// </summary>
            public ReactiveObject<bool> EnableFsIntegrityChecks { get; private set; }

            /// <summary>
            /// Whether to enable managed buffering of game file contents
            /// </summary>
            public ReactiveObject<bool> EnableHostFsBuffering { get; set; }

            /// <summary>
            /// Whether to attempt to fully buffer game file contents before booting
            /// </summary>
            public ReactiveObject<bool> EnableHostFsBufferingPrefetch { get; set; }

            /// <summary>
            /// Limit the size of the shared host file I/O cache
            /// </summary>
            public ReactiveObject<long> HostFsBufferingMaxCacheSize { get; set; }

            /// <summary>
            /// The selected audio backend
            /// </summary>
            public ReactiveObject<AudioBackend> AudioBackend { get; private set; }

            /// <summary>
            /// The audio backend volume
            /// </summary>
            public ReactiveObject<float> AudioVolume { get; private set; }

            /// <summary>
            /// The selected memory manager mode
            /// </summary>
            public ReactiveObject<MemoryManagerMode> MemoryManagerMode { get; private set; }

            /// <summary>
            /// The selected memory configuration/size
            /// </summary>
            public ReactiveObject<MemoryConfiguration> MemoryConfiguration { get; private set; }

            /// <summary>
            /// Enable or disable ignoring missing services
            /// </summary>
            public ReactiveObject<bool> IgnoreMissingServices { get; private set; }

            /// <summary>
            /// Uses Hypervisor over JIT if available
            /// </summary>
            public ReactiveObject<bool> UseHypervisor { get; private set; }

            /// <summary>
            /// Use sparse function address tables if available
            /// </summary>
            public ReactiveObject<bool> UseSparseAddressTable { get; set; }

            /// <summary>
            /// List of logical CPU cores the HLE kernel threads are allowed to run on
            /// </summary>
            public ReactiveObject<string> HleKernelThreadsCPUSet { get; set; }

            /// <summary>
            /// Whether to assign a HLE kernel-thread to a single logical CPU core picked from the configured CPU set
            /// </summary>
            public ReactiveObject<bool> HleKernelThreadsCPUSetStaticCore { get; set; }

            /// <summary>
            /// List of logical CPU cores the PTC background threads are allowed to run on
            /// </summary>
            public ReactiveObject<string> PtcBackgroundThreadsCPUSet { get; set; }

            /// <summary>
            /// Number of PTC background threads to start
            /// </summary>
            public ReactiveObject<int> PtcBackgroundThreadCount { get; set; }

            public SystemSection(GameConfigurationState state)
            {
                Language = new ReactiveObject<Language>();
                Region = new ReactiveObject<Region>();
                TimeZone = new ReactiveObject<string>();
                SystemTimeOffset = new ReactiveObject<long>();
                EnableDockedMode = new ReactiveObject<bool>();
                EnableDockedMode.Event += (sender, e) => LogValueChange(state, e, nameof(EnableDockedMode));
                EnablePtc = new ReactiveObject<bool>();
                EnablePtc.Event += (sender, e) => LogValueChange(state, e, nameof(EnablePtc));
                UseStreamingPtc = new ReactiveObject<bool>();
                UseStreamingPtc.Event += (sender, e) => LogValueChange(state, e, nameof(UseStreamingPtc));
                EnableInternetAccess = new ReactiveObject<bool>();
                EnableInternetAccess.Event += (sender, e) => LogValueChange(state, e, nameof(EnableInternetAccess));
                EnableFsIntegrityChecks = new ReactiveObject<bool>();
                EnableFsIntegrityChecks.Event += (sender, e) => LogValueChange(state, e, nameof(EnableFsIntegrityChecks));
                EnableHostFsBuffering = new ReactiveObject<bool>();
                EnableHostFsBuffering.Event += (sender, e) => LogValueChange(state, e, nameof(EnableHostFsBuffering));
                EnableHostFsBufferingPrefetch = new ReactiveObject<bool>();
                EnableHostFsBufferingPrefetch.Event += (sender, e) => LogValueChange(state, e, nameof(EnableHostFsBufferingPrefetch));
                HostFsBufferingMaxCacheSize = new ReactiveObject<long>();
                HostFsBufferingMaxCacheSize.Event += (sender, e) => LogValueChange(state, e, nameof(HostFsBufferingMaxCacheSize));
                AudioBackend = new ReactiveObject<AudioBackend>();
                AudioBackend.Event += (sender, e) => LogValueChange(state, e, nameof(AudioBackend));
                MemoryManagerMode = new ReactiveObject<MemoryManagerMode>();
                MemoryManagerMode.Event += (sender, e) => LogValueChange(state, e, nameof(MemoryManagerMode));
                MemoryConfiguration = new ReactiveObject<MemoryConfiguration>();
                MemoryConfiguration.Event += (sender, e) => LogValueChange(state, e, nameof(MemoryConfiguration));
                IgnoreMissingServices = new ReactiveObject<bool>();
                IgnoreMissingServices.Event += (sender, e) => LogValueChange(state, e, nameof(IgnoreMissingServices));
                AudioVolume = new ReactiveObject<float>();
                AudioVolume.Event += (sender, e) => LogValueChange(state, e, nameof(AudioVolume));
                UseHypervisor = new ReactiveObject<bool>();
                UseHypervisor.Event += (sender, e) => LogValueChange(state, e, nameof(UseHypervisor));
                UseSparseAddressTable = new ReactiveObject<bool>();
                UseSparseAddressTable.Event += (sender, e) => LogValueChange(state, e, nameof(UseSparseAddressTable));
                HleKernelThreadsCPUSet = new ReactiveObject<string>();
                HleKernelThreadsCPUSet.Event += (sender, e) => LogValueChange(state, e, nameof(HleKernelThreadsCPUSet));
                HleKernelThreadsCPUSetStaticCore = new ReactiveObject<bool>();
                HleKernelThreadsCPUSetStaticCore.Event += (sender, e) => LogValueChange(state, e, nameof(HleKernelThreadsCPUSetStaticCore));
                PtcBackgroundThreadsCPUSet = new ReactiveObject<string>();
                PtcBackgroundThreadsCPUSet.Event += (sender, e) => LogValueChange(state, e, nameof(PtcBackgroundThreadsCPUSet));
                PtcBackgroundThreadCount = new ReactiveObject<int>();
                PtcBackgroundThreadCount.Event += (sender, e) => LogValueChange(state, e, nameof(PtcBackgroundThreadCount));
            }
        }

        /// <summary>
        /// Hid configuration section
        /// </summary>
        public class HidSection
        {
            /// <summary>
            /// Enable or disable keyboard support (Independent from controllers binding)
            /// </summary>
            public ReactiveObject<bool> EnableKeyboard { get; private set; }

            /// <summary>
            /// Enable or disable mouse support (Independent from controllers binding)
            /// </summary>
            public ReactiveObject<bool> EnableMouse { get; private set; }

            /// <summary>
            /// Hotkey Keyboard Bindings
            /// </summary>
            public ReactiveObject<KeyboardHotkeys> Hotkeys { get; private set; }

            /// <summary>
            /// Input device configuration.
            /// NOTE: This ReactiveObject won't issue an event when the List has elements added or removed.
            /// TODO: Implement a ReactiveList class.
            /// </summary>
            public ReactiveObject<List<InputConfig>> InputConfig { get; private set; }

            public HidSection()
            {
                EnableKeyboard = new ReactiveObject<bool>();
                EnableMouse = new ReactiveObject<bool>();
                Hotkeys = new ReactiveObject<KeyboardHotkeys>();
                InputConfig = new ReactiveObject<List<InputConfig>>();
            }
        }

        /// <summary>
        /// Graphics configuration section
        /// </summary>
        public class GraphicsSection
        {
            /// <summary>
            /// Whether or not backend threading is enabled. The "Auto" setting will determine whether threading should be enabled at runtime.
            /// </summary>
            public ReactiveObject<BackendThreading> BackendThreading { get; private set; }

            /// <summary>
            /// Max Anisotropy. Values range from 0 - 16. Set to -1 to let the game decide.
            /// </summary>
            public ReactiveObject<float> MaxAnisotropy { get; private set; }

            /// <summary>
            /// Aspect Ratio applied to the renderer window.
            /// </summary>
            public ReactiveObject<AspectRatio> AspectRatio { get; private set; }

            /// <summary>
            /// Resolution Scale. An integer scale applied to applicable render targets. Values 1-4, or -1 to use a custom floating point scale instead.
            /// </summary>
            public ReactiveObject<int> ResScale { get; private set; }

            /// <summary>
            /// Custom Resolution Scale. A custom floating point scale applied to applicable render targets. Only active when Resolution Scale is -1.
            /// </summary>
            public ReactiveObject<float> ResScaleCustom { get; private set; }

            /// <summary>
            /// Dumps shaders in this local directory
            /// </summary>
            public ReactiveObject<string> ShadersDumpPath { get; private set; }

            /// <summary>
            /// Enables or disables Vertical Sync
            /// </summary>
            public ReactiveObject<bool> EnableVsync { get; private set; }

            /// <summary>
            /// Enables or disables Shader cache
            /// </summary>
            public ReactiveObject<bool> EnableShaderCache { get; private set; }

            /// <summary>
            /// Enables or disables texture recompression
            /// </summary>
            public ReactiveObject<bool> EnableTextureRecompression { get; private set; }

            /// <summary>
            /// Enables or disables Macro high-level emulation
            /// </summary>
            public ReactiveObject<bool> EnableMacroHLE { get; private set; }

            /// <summary>
            /// Enables or disables color space passthrough, if available.
            /// </summary>
            public ReactiveObject<bool> EnableColorSpacePassthrough { get; private set; }

            /// <summary>
            /// Graphics backend
            /// </summary>
            public ReactiveObject<GraphicsBackend> GraphicsBackend { get; private set; }

            /// <summary>
            /// Applies anti-aliasing to the renderer.
            /// </summary>
            public ReactiveObject<AntiAliasing> AntiAliasing { get; private set; }

            /// <summary>
            /// Sets the framebuffer upscaling type.
            /// </summary>
            public ReactiveObject<ScalingFilter> ScalingFilter { get; private set; }

            /// <summary>
            /// Sets the framebuffer upscaling level.
            /// </summary>
            public ReactiveObject<int> ScalingFilterLevel { get; private set; }

            /// <summary>
            /// Preferred GPU
            /// </summary>
            public ReactiveObject<string> PreferredGpu { get; private set; }

            public GraphicsSection(GameConfigurationState state)
            {
                BackendThreading = new ReactiveObject<BackendThreading>();
                BackendThreading.Event += (sender, e) => LogValueChange(state, e, nameof(BackendThreading));
                ResScale = new ReactiveObject<int>();
                ResScale.Event += (sender, e) => LogValueChange(state, e, nameof(ResScale));
                ResScaleCustom = new ReactiveObject<float>();
                ResScaleCustom.Event += (sender, e) => LogValueChange(state, e, nameof(ResScaleCustom));
                MaxAnisotropy = new ReactiveObject<float>();
                MaxAnisotropy.Event += (sender, e) => LogValueChange(state, e, nameof(MaxAnisotropy));
                AspectRatio = new ReactiveObject<AspectRatio>();
                AspectRatio.Event += (sender, e) => LogValueChange(state, e, nameof(AspectRatio));
                ShadersDumpPath = new ReactiveObject<string>();
                EnableVsync = new ReactiveObject<bool>();
                EnableVsync.Event += (sender, e) => LogValueChange(state, e, nameof(EnableVsync));
                EnableShaderCache = new ReactiveObject<bool>();
                EnableShaderCache.Event += (sender, e) => LogValueChange(state, e, nameof(EnableShaderCache));
                EnableTextureRecompression = new ReactiveObject<bool>();
                EnableTextureRecompression.Event += (sender, e) => LogValueChange(state, e, nameof(EnableTextureRecompression));
                GraphicsBackend = new ReactiveObject<GraphicsBackend>();
                GraphicsBackend.Event += (sender, e) => LogValueChange(state, e, nameof(GraphicsBackend));
                PreferredGpu = new ReactiveObject<string>();
                PreferredGpu.Event += (sender, e) => LogValueChange(state, e, nameof(PreferredGpu));
                EnableMacroHLE = new ReactiveObject<bool>();
                EnableMacroHLE.Event += (sender, e) => LogValueChange(state, e, nameof(EnableMacroHLE));
                EnableColorSpacePassthrough = new ReactiveObject<bool>();
                EnableColorSpacePassthrough.Event += (sender, e) => LogValueChange(state, e, nameof(EnableColorSpacePassthrough));
                AntiAliasing = new ReactiveObject<AntiAliasing>();
                AntiAliasing.Event += (sender, e) => LogValueChange(state, e, nameof(AntiAliasing));
                ScalingFilter = new ReactiveObject<ScalingFilter>();
                ScalingFilter.Event += (sender, e) => LogValueChange(state, e, nameof(ScalingFilter));
                ScalingFilterLevel = new ReactiveObject<int>();
                ScalingFilterLevel.Event += (sender, e) => LogValueChange(state, e, nameof(ScalingFilterLevel));
            }
        }

        /// <summary>
        /// Game-Capturing configuration section
        /// </summary>
        public class CaptureSection
        {

            /// <summary>
            /// Whether to automatically begin capturing when starting the game
            /// </summary>
            public ReactiveObject<bool> BeginOnStart { get; set; }

            /// <summary>
            /// The output-format used for game captures
            /// </summary>
            public ReactiveObject<CaptureOutputFormatValue> OutputFormat { get; set; }

            /// <summary>
            /// The video-codec used for game captures
            /// </summary>
            public ReactiveObject<CaptureVideoCodecValue> VideoCodec { get; set; }

            /// <summary>
            /// Whether to scale the game capture video to a predefined dimension
            /// </summary>
            public ReactiveObject<bool> VideoScaleEnabled { get; set; }

            /// <summary>
            /// Width to scale the game capture video-output to.
            /// </summary>
            public ReactiveObject<int> VideoScaleWidth { get; set; }

            /// <summary>
            /// Whether to scale the width of the video automatically depending on
            /// the configured height, keeping the original aspect ratio.
            /// </summary>
            public ReactiveObject<bool> VideoScaleWidthAuto { get; set; }

            /// <summary>
            /// Height to scale the game capture video-output to.
            /// </summary>
            public ReactiveObject<int> VideoScaleHeight { get; set; }

            /// <summary>
            /// Whether to scale the hight of the video automatically depending on
            /// the configured width, keeping the original aspect ratio.
            /// </summary>
            public ReactiveObject<bool> VideoScaleHeightAuto { get; set; }

            /// <summary>
            /// Whether to control quality by bitrate
            /// </summary>
            public ReactiveObject<bool> VideoUseBitrate { get; set; }

            /// <summary>
            /// The bitrate of the video-stream in game captures
            /// </summary>
            public ReactiveObject<long> VideoBitrate { get; set; }

            /// <summary>
            /// The level of quality of the video-stream in game captures (depending on the selected codec)
            /// </summary>
            public ReactiveObject<bool> VideoUseQualityLevel { get; set; }

            /// <summary>
            /// The bitrate of the video-stream in game captures
            /// </summary>
            public ReactiveObject<int> VideoQualityLevel { get; set; }

            /// <summary>
            /// Whether to try and encode video-stream in game captures without loss of image-information
            /// </summary>
            public ReactiveObject<bool> VideoUseLossless { get; set; }

            /// <summary>
            /// Number of threads to use for encoding video-frames if multithreading is supported by the video-codec.
            /// </summary>
            public ReactiveObject<int> VideoEncoderThreadCount { get; set; }

            /// <summary>
            /// Whether to use hardware-acceleration when encoding video-frames for game captures
            /// </summary>
            public ReactiveObject<bool> VideoEncoderHardwareAcceleration { get; set; }

            /// <summary>
            /// Allow NVENC-devices to be considered when initializing hardware-acceleration
            /// </summary>
            public ReactiveObject<bool> VideoEncoderHardwareAccelerationAllowNvenc { get; set; }

            /// <summary>
            /// Allow Intel QSV-devices to be considered when initializing hardware-acceleration
            /// </summary>
            public ReactiveObject<bool> VideoEncoderHardwareAccelerationAllowQsv { get; set; }

            /// <summary>
            /// Allow Vulkan-devices to be considered when initializing hardware-acceleration
            /// </summary>
            public ReactiveObject<bool> VideoEncoderHardwareAccelerationAllowVulkan { get; set; }

            /// <summary>
            /// The audio-codec used for game captures
            /// </summary>
            public ReactiveObject<CaptureAudioCodecValue> AudioCodec { get; set; }

            public CaptureSection(GameConfigurationState state)
            {
                BeginOnStart = new ReactiveObject<bool>();
                BeginOnStart.Event += (sender, e) => LogValueChange(state, e, nameof(BeginOnStart));
                OutputFormat = new ReactiveObject<CaptureOutputFormatValue>();
                OutputFormat.Event += (sender, e) => LogValueChange(state, e, nameof(OutputFormat));
                VideoCodec = new ReactiveObject<CaptureVideoCodecValue>();
                VideoCodec.Event += (sender, e) => LogValueChange(state, e, nameof(VideoCodec));
                VideoScaleEnabled = new ReactiveObject<bool>();
                VideoScaleEnabled.Event += (sender, e) => LogValueChange(state, e, nameof(VideoScaleEnabled));
                VideoScaleWidth = new ReactiveObject<int>();
                VideoScaleWidth.Event += (sender, e) => LogValueChange(state, e, nameof(VideoScaleWidth));
                VideoScaleWidthAuto = new ReactiveObject<bool>();
                VideoScaleWidthAuto.Event += (sender, e) => LogValueChange(state, e, nameof(VideoScaleWidthAuto));
                VideoScaleHeight = new ReactiveObject<int>();
                VideoScaleHeight.Event += (sender, e) => LogValueChange(state, e, nameof(VideoScaleHeight));
                VideoScaleHeightAuto = new ReactiveObject<bool>();
                VideoScaleHeightAuto.Event += (sender, e) => LogValueChange(state, e, nameof(VideoScaleHeightAuto));
                VideoUseBitrate = new ReactiveObject<bool>();
                VideoUseBitrate.Event += (sender, e) => LogValueChange(state, e, nameof(VideoUseBitrate));
                VideoBitrate = new ReactiveObject<long>();
                VideoBitrate.Event += (sender, e) => LogValueChange(state, e, nameof(VideoBitrate));
                VideoUseQualityLevel = new ReactiveObject<bool>();
                VideoUseQualityLevel.Event += (sender, e) => LogValueChange(state, e, nameof(VideoUseQualityLevel));
                VideoQualityLevel = new ReactiveObject<int>();
                VideoQualityLevel.Event += (sender, e) => LogValueChange(state, e, nameof(VideoQualityLevel));
                VideoUseLossless = new ReactiveObject<bool>();
                VideoUseLossless.Event += (sender, e) => LogValueChange(state, e, nameof(VideoUseLossless));
                VideoEncoderThreadCount = new ReactiveObject<int>();
                VideoEncoderThreadCount.Event += (sender, e) => LogValueChange(state, e, nameof(VideoEncoderThreadCount));
                VideoEncoderHardwareAcceleration = new ReactiveObject<bool>();
                VideoEncoderHardwareAcceleration.Event += (sender, e) => LogValueChange(state, e, nameof(VideoEncoderHardwareAcceleration));
                VideoEncoderHardwareAccelerationAllowNvenc = new ReactiveObject<bool>();
                VideoEncoderHardwareAccelerationAllowNvenc.Event += (sender, e) => LogValueChange(state, e, nameof(VideoEncoderHardwareAccelerationAllowNvenc));
                VideoEncoderHardwareAccelerationAllowQsv = new ReactiveObject<bool>();
                VideoEncoderHardwareAccelerationAllowQsv.Event += (sender, e) => LogValueChange(state, e, nameof(VideoEncoderHardwareAccelerationAllowQsv));
                VideoEncoderHardwareAccelerationAllowVulkan = new ReactiveObject<bool>();
                VideoEncoderHardwareAccelerationAllowVulkan.Event += (sender, e) => LogValueChange(state, e, nameof(VideoEncoderHardwareAccelerationAllowVulkan));
                AudioCodec = new ReactiveObject<CaptureAudioCodecValue>();
                AudioCodec.Event += (sender, e) => LogValueChange(state, e, nameof(AudioCodec));
            }
        }

        /// <summary>
        /// Multiplayer configuration section
        /// </summary>
        public class MultiplayerSection
        {
            /// <summary>
            /// GUID for the network interface used by LAN (or 0 for default)
            /// </summary>
            public ReactiveObject<string> LanInterfaceId { get; private set; }

            /// <summary>
            /// Multiplayer Mode
            /// </summary>
            public ReactiveObject<MultiplayerMode> Mode { get; private set; }

            public MultiplayerSection(GameConfigurationState state)
            {
                LanInterfaceId = new ReactiveObject<string>();
                Mode = new ReactiveObject<MultiplayerMode>();
                Mode.Event += (_, e) => LogValueChange(state, e, nameof(MultiplayerMode));
            }
        }

        /// <summary>
        /// The System section
        /// </summary>
        public SystemSection System { get; private set; }

        /// <summary>
        /// The Graphics section
        /// </summary>
        public GraphicsSection Graphics { get; private set; }

        /// <summary>
        /// The Hid section
        /// </summary>
        public HidSection Hid { get; private set; }

        /// <summary>
        /// The Multiplayer section
        /// </summary>
        public MultiplayerSection Multiplayer { get; private set; }

        /// <summary>
        /// The game-capture section
        /// </summary>
        public CaptureSection Capture { get; private set; }

        /// <summary>
        /// Whether to use the game-specific configuration values instead of the global values
        /// </summary>
        public ReactiveObject<bool> UseGameConfig { get; private set; }

        /// <summary>
        /// Hide Cursor on Idle
        /// </summary>
        public ReactiveObject<HideCursorMode> HideCursor { get; private set; }

        /// <summary>
        /// Name of the game this configuration-state belongs top
        /// </summary>
        public string TitleName { get; }

        /// <summary>
        /// ID as string of the game this configuration-state belongs top
        /// </summary>
        public string TitleIdString { get; }

        /// <summary>
        /// The path of the current game configuration instance
        /// </summary>
        public string ConfigurationFilePath { get; }

        /// <summary>
        /// Whether this instance if the global configuration-state
        /// </summary>
        public bool IsGlobalState { get; }

        /// <summary>
        /// The configuration-state of the currently running game.
        /// Will be <see langword="null"/> if no game is running.
        /// </summary>
        public static GameConfigurationState Current { get; set; }

        internal bool EnableLogging { get; private set; }

        public GameConfigurationState(string titleName, string titleIdString, string filePath)
        {
            TitleName = titleName;
            TitleIdString = titleIdString;
            ConfigurationFilePath = filePath;

            IsGlobalState = filePath == null;

            System = new SystemSection(this);
            Graphics = new GraphicsSection(this);
            Hid = new HidSection();
            Capture = new CaptureSection(this);
            Multiplayer = new MultiplayerSection(this);
            UseGameConfig = new ReactiveObject<bool>();
            UseGameConfig.Event += (sender, e) => LogValueChange(this, e, nameof(UseGameConfig));
            HideCursor = new ReactiveObject<HideCursorMode>();
            HideCursor.Event += (sender, e) => LogValueChange(this, e, nameof(HideCursor));
        }

        public static GameConfigurationState Global()
        {
            return new GameConfigurationState(null, null, null);
        }

        public GameConfigurationFileFormat ToFileFormat()
        {
            GameConfigurationFileFormat configurationFile = new()
            {
                Version = GameConfigurationFileFormat.CurrentVersion,
                UseGameConfig = UseGameConfig,
                BackendThreading = Graphics.BackendThreading,
                ResScale = Graphics.ResScale,
                ResScaleCustom = Graphics.ResScaleCustom,
                MaxAnisotropy = Graphics.MaxAnisotropy,
                AspectRatio = Graphics.AspectRatio,
                AntiAliasing = Graphics.AntiAliasing,
                ScalingFilter = Graphics.ScalingFilter,
                ScalingFilterLevel = Graphics.ScalingFilterLevel,
                GraphicsShadersDumpPath = Graphics.ShadersDumpPath,
                SystemLanguage = System.Language,
                SystemRegion = System.Region,
                SystemTimeZone = System.TimeZone,
                SystemTimeOffset = System.SystemTimeOffset,
                DockedMode = System.EnableDockedMode,
                HideCursor = HideCursor,
                EnableVsync = Graphics.EnableVsync,
                EnableShaderCache = Graphics.EnableShaderCache,
                EnableTextureRecompression = Graphics.EnableTextureRecompression,
                EnableMacroHLE = Graphics.EnableMacroHLE,
                EnableColorSpacePassthrough = Graphics.EnableColorSpacePassthrough,
                EnablePtc = System.EnablePtc,
                UseStreamingPtc = System.UseStreamingPtc,
                EnableInternetAccess = System.EnableInternetAccess,
                EnableFsIntegrityChecks = System.EnableFsIntegrityChecks,
                EnableHostFsBuffering = System.EnableHostFsBuffering,
                EnableHostFsBufferingPrefetch = System.EnableHostFsBufferingPrefetch,
                HostFsBufferingMaxCacheSize = System.HostFsBufferingMaxCacheSize,
                AudioBackend = System.AudioBackend,
                AudioVolume = System.AudioVolume,
                MemoryManagerMode = System.MemoryManagerMode,
                MemoryConfiguration = System.MemoryConfiguration,
                IgnoreMissingServices = System.IgnoreMissingServices,
                UseHypervisor = System.UseHypervisor,
                UseSparseAddressTable = System.UseSparseAddressTable,
                HleKernelThreadsCPUSet = System.HleKernelThreadsCPUSet,
                HleKernelThreadsCPUSetStaticCore = System.HleKernelThreadsCPUSetStaticCore,
                PtcBackgroundThreadsCPUSet = System.PtcBackgroundThreadsCPUSet,
                PtcBackgroundThreadCount = System.PtcBackgroundThreadCount,
                EnableKeyboard = Hid.EnableKeyboard,
                EnableMouse = Hid.EnableMouse,
                Hotkeys = Hid.Hotkeys,
                KeyboardConfig = new List<JsonObject>(),
                ControllerConfig = new List<JsonObject>(),
                InputConfig = Hid.InputConfig,
                GraphicsBackend = Graphics.GraphicsBackend,
                PreferredGpu = Graphics.PreferredGpu,
                MultiplayerLanInterfaceId = Multiplayer.LanInterfaceId,
                MultiplayerMode = Multiplayer.Mode,
                CaptureBeginOnStart = Capture.BeginOnStart,
                CaptureOutputFormat = Capture.OutputFormat,
                CaptureVideoCodec = Capture.VideoCodec,
                CaptureVideoScaleEnabled = Capture.VideoScaleEnabled,
                CaptureVideoScaleWidth = Capture.VideoScaleWidth,
                CaptureVideoScaleWidthAuto = Capture.VideoScaleWidthAuto,
                CaptureVideoScaleHeight = Capture.VideoScaleHeight,
                CaptureVideoScaleHeightAuto = Capture.VideoScaleHeightAuto,
                CaptureVideoUseBitrate = Capture.VideoUseBitrate,
                CaptureVideoBitrate = Capture.VideoBitrate,
                CaptureVideoUseQualityLevel = Capture.VideoUseQualityLevel,
                CaptureVideoQualityLevel = Capture.VideoQualityLevel,
                CaptureVideoUseLossless = Capture.VideoUseLossless,
                CaptureVideoEncoderThreadCount = Capture.VideoEncoderThreadCount,
                CaptureVideoEncoderHardwareAcceleration = Capture.VideoEncoderHardwareAcceleration,
                CaptureVideoEncoderHardwareAccelerationAllowNvenc = Capture.VideoEncoderHardwareAccelerationAllowNvenc,
                CaptureVideoEncoderHardwareAccelerationAllowQsv = Capture.VideoEncoderHardwareAccelerationAllowQsv,
                CaptureVideoEncoderHardwareAccelerationAllowVulkan = Capture.VideoEncoderHardwareAccelerationAllowVulkan,
                CaptureAudioCodec = Capture.AudioCodec,
            };

            return configurationFile;
        }

        public virtual void LoadDefault()
        {
            UseGameConfig.Value = false;
            Graphics.BackendThreading.Value = BackendThreading.Auto;
            Graphics.ResScale.Value = 1;
            Graphics.ResScaleCustom.Value = 1.0f;
            Graphics.MaxAnisotropy.Value = -1.0f;
            Graphics.AspectRatio.Value = AspectRatio.Fixed16x9;
            Graphics.GraphicsBackend.Value = DefaultGraphicsBackend();
            Graphics.PreferredGpu.Value = "";
            Graphics.ShadersDumpPath.Value = "";
            System.Language.Value = Language.AmericanEnglish;
            System.Region.Value = Region.USA;
            System.TimeZone.Value = "UTC";
            System.SystemTimeOffset.Value = 0;
            System.EnableDockedMode.Value = true;
            HideCursor.Value = HideCursorMode.OnIdle;
            Graphics.EnableVsync.Value = true;
            Graphics.EnableShaderCache.Value = true;
            Graphics.EnableTextureRecompression.Value = false;
            Graphics.EnableMacroHLE.Value = true;
            Graphics.EnableColorSpacePassthrough.Value = false;
            Graphics.AntiAliasing.Value = AntiAliasing.None;
            Graphics.ScalingFilter.Value = ScalingFilter.Bilinear;
            Graphics.ScalingFilterLevel.Value = 80;
            System.EnablePtc.Value = true;
            System.UseStreamingPtc.Value = false;
            System.EnableInternetAccess.Value = false;
            System.EnableFsIntegrityChecks.Value = true;
            System.EnableHostFsBuffering.Value = true;
            System.EnableHostFsBufferingPrefetch.Value = false;
            System.HostFsBufferingMaxCacheSize.Value = 2L * 1024 * 1024 * 1024; // 2 GiB
            System.AudioBackend.Value = AudioBackend.SDL2;
            System.AudioVolume.Value = 1;
            System.MemoryManagerMode.Value = MemoryManagerMode.HostMappedUnsafe;
            System.MemoryConfiguration.Value = MemoryConfiguration.MemoryConfiguration4GiB;
            System.IgnoreMissingServices.Value = false;
            System.UseHypervisor.Value = true;
            System.UseSparseAddressTable.Value = true;
            System.HleKernelThreadsCPUSet.Value = "*";
            System.HleKernelThreadsCPUSetStaticCore.Value = false;
            System.PtcBackgroundThreadsCPUSet.Value = "*";
            System.PtcBackgroundThreadCount.Value = Math.Min(4, Math.Max(1, (Environment.ProcessorCount - 6) / 3));
            Capture.BeginOnStart.Value = false;
            Capture.OutputFormat.Value = CaptureOutputFormatValue.MPEGTS;
            Capture.VideoCodec.Value = CaptureVideoCodecValue.H264;
            Capture.VideoScaleEnabled.Value = false;
            Capture.VideoScaleWidth.Value = 1280;
            Capture.VideoScaleWidthAuto.Value = false;
            Capture.VideoScaleHeight.Value = 720;
            Capture.VideoScaleHeightAuto.Value = false;
            Capture.VideoUseBitrate.Value = false;
            Capture.VideoBitrate.Value = 15000000;
            Capture.VideoUseQualityLevel.Value = true;
            Capture.VideoQualityLevel.Value = 23;
            Capture.VideoUseLossless.Value = false;
            Capture.VideoEncoderThreadCount.Value = Math.Clamp(Environment.ProcessorCount / 3, 4, 8);
            Capture.VideoEncoderHardwareAcceleration.Value = false;
            Capture.VideoEncoderHardwareAccelerationAllowNvenc.Value = true;
            Capture.VideoEncoderHardwareAccelerationAllowQsv.Value = true;
            Capture.VideoEncoderHardwareAccelerationAllowVulkan.Value = false;
            Capture.AudioCodec.Value = CaptureAudioCodecValue.AAC;
            Multiplayer.LanInterfaceId.Value = "0";
            Multiplayer.Mode.Value = MultiplayerMode.Disabled;
            Hid.EnableKeyboard.Value = false;
            Hid.EnableMouse.Value = false;
            Hid.Hotkeys.Value = new KeyboardHotkeys
            {
                ToggleVsync = Key.F1,
                ToggleMute = Key.F2,
                StartCapture = Key.F6,
                StopCapture = Key.F7,
                Screenshot = Key.F8,
                ShowUI = Key.F4,
                Pause = Key.F5,
                ResScaleUp = Key.Unbound,
                ResScaleDown = Key.Unbound,
                VolumeUp = Key.Unbound,
                VolumeDown = Key.Unbound,
            };
            Hid.InputConfig.Value = new List<InputConfig>
            {
                new StandardKeyboardInputConfig
                {
                    Version = InputConfig.CurrentVersion,
                    Backend = InputBackendType.WindowKeyboard,
                    Id = "0",
                    PlayerIndex = PlayerIndex.Player1,
                    ControllerType = ControllerType.JoyconPair,
                    LeftJoycon = new LeftJoyconCommonConfig<Key>
                    {
                        DpadUp = Key.Up,
                        DpadDown = Key.Down,
                        DpadLeft = Key.Left,
                        DpadRight = Key.Right,
                        ButtonMinus = Key.Minus,
                        ButtonL = Key.E,
                        ButtonZl = Key.Q,
                        ButtonSl = Key.Unbound,
                        ButtonSr = Key.Unbound,
                    },
                    LeftJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = Key.W,
                        StickDown = Key.S,
                        StickLeft = Key.A,
                        StickRight = Key.D,
                        StickButton = Key.F,
                    },
                    RightJoycon = new RightJoyconCommonConfig<Key>
                    {
                        ButtonA = Key.Z,
                        ButtonB = Key.X,
                        ButtonX = Key.C,
                        ButtonY = Key.V,
                        ButtonPlus = Key.Plus,
                        ButtonR = Key.U,
                        ButtonZr = Key.O,
                        ButtonSl = Key.Unbound,
                        ButtonSr = Key.Unbound,
                    },
                    RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = Key.I,
                        StickDown = Key.K,
                        StickLeft = Key.J,
                        StickRight = Key.L,
                        StickButton = Key.H,
                    },
                },
            };
        }

        public void Load(GameConfigurationFileFormat gameConfigurationFileFormat)
        {
            bool configurationFileUpdated = false;

            if (gameConfigurationFileFormat.Version < 2)
            {
                Logger.Warning?.Print(LogClass.Application, $"Outdated configuration version {gameConfigurationFileFormat.Version}, migrating to version 2.");

                gameConfigurationFileFormat.CaptureBeginOnStart = false;
                gameConfigurationFileFormat.CaptureOutputFormat = CaptureOutputFormatValue.MPEGTS;
                gameConfigurationFileFormat.CaptureVideoCodec = CaptureVideoCodecValue.H264;
                gameConfigurationFileFormat.CaptureVideoScaleEnabled = false;
                gameConfigurationFileFormat.CaptureVideoScaleWidth = 1280;
                gameConfigurationFileFormat.CaptureVideoScaleWidthAuto = false;
                gameConfigurationFileFormat.CaptureVideoScaleHeight = 720;
                gameConfigurationFileFormat.CaptureVideoScaleHeightAuto = false;
                gameConfigurationFileFormat.CaptureVideoUseBitrate = false;
                gameConfigurationFileFormat.CaptureVideoBitrate = 15000000;
                gameConfigurationFileFormat.CaptureVideoUseQualityLevel = true;
                gameConfigurationFileFormat.CaptureVideoQualityLevel = 23;
                gameConfigurationFileFormat.CaptureVideoUseLossless = false;
                gameConfigurationFileFormat.CaptureVideoEncoderThreadCount = Math.Clamp(Environment.ProcessorCount / 3, 4, 8);
                gameConfigurationFileFormat.CaptureVideoEncoderHardwareAcceleration = false;
                gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowNvenc = true;
                gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowQsv = true;
                gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowVulkan = false;
                gameConfigurationFileFormat.CaptureAudioCodec = CaptureAudioCodecValue.AAC;

                if (gameConfigurationFileFormat.Hotkeys != null)
                {
                    gameConfigurationFileFormat.Hotkeys.StartCapture = Key.F6;
                    gameConfigurationFileFormat.Hotkeys.StopCapture = Key.F7;
                }

                configurationFileUpdated = true;
            }

            UseGameConfig.Value = gameConfigurationFileFormat.UseGameConfig;
            Graphics.ResScale.Value = gameConfigurationFileFormat.ResScale;
            Graphics.ResScaleCustom.Value = gameConfigurationFileFormat.ResScaleCustom;
            Graphics.MaxAnisotropy.Value = gameConfigurationFileFormat.MaxAnisotropy;
            Graphics.AspectRatio.Value = gameConfigurationFileFormat.AspectRatio;
            Graphics.ShadersDumpPath.Value = gameConfigurationFileFormat.GraphicsShadersDumpPath;
            Graphics.BackendThreading.Value = gameConfigurationFileFormat.BackendThreading;
            Graphics.GraphicsBackend.Value = gameConfigurationFileFormat.GraphicsBackend;
            Graphics.PreferredGpu.Value = gameConfigurationFileFormat.PreferredGpu;
            Graphics.AntiAliasing.Value = gameConfigurationFileFormat.AntiAliasing;
            Graphics.ScalingFilter.Value = gameConfigurationFileFormat.ScalingFilter;
            Graphics.ScalingFilterLevel.Value = gameConfigurationFileFormat.ScalingFilterLevel;
            System.Language.Value = gameConfigurationFileFormat.SystemLanguage;
            System.Region.Value = gameConfigurationFileFormat.SystemRegion;
            System.TimeZone.Value = gameConfigurationFileFormat.SystemTimeZone;
            System.SystemTimeOffset.Value = gameConfigurationFileFormat.SystemTimeOffset;
            System.EnableDockedMode.Value = gameConfigurationFileFormat.DockedMode;
            HideCursor.Value = gameConfigurationFileFormat.HideCursor;
            Graphics.EnableVsync.Value = gameConfigurationFileFormat.EnableVsync;
            Graphics.EnableShaderCache.Value = gameConfigurationFileFormat.EnableShaderCache;
            Graphics.EnableTextureRecompression.Value = gameConfigurationFileFormat.EnableTextureRecompression;
            Graphics.EnableMacroHLE.Value = gameConfigurationFileFormat.EnableMacroHLE;
            Graphics.EnableColorSpacePassthrough.Value = gameConfigurationFileFormat.EnableColorSpacePassthrough;
            System.EnablePtc.Value = gameConfigurationFileFormat.EnablePtc;
            System.UseStreamingPtc.Value = gameConfigurationFileFormat.UseStreamingPtc;
            System.EnableInternetAccess.Value = gameConfigurationFileFormat.EnableInternetAccess;
            System.EnableFsIntegrityChecks.Value = gameConfigurationFileFormat.EnableFsIntegrityChecks;
            System.EnableHostFsBuffering.Value = gameConfigurationFileFormat.EnableHostFsBuffering;
            System.EnableHostFsBufferingPrefetch.Value = gameConfigurationFileFormat.EnableHostFsBufferingPrefetch;
            System.HostFsBufferingMaxCacheSize.Value = gameConfigurationFileFormat.HostFsBufferingMaxCacheSize;
            System.AudioBackend.Value = gameConfigurationFileFormat.AudioBackend;
            System.AudioVolume.Value = gameConfigurationFileFormat.AudioVolume;
            System.MemoryManagerMode.Value = gameConfigurationFileFormat.MemoryManagerMode;
            System.MemoryConfiguration.Value = gameConfigurationFileFormat.MemoryConfiguration;
            System.IgnoreMissingServices.Value = gameConfigurationFileFormat.IgnoreMissingServices;
            System.UseHypervisor.Value = gameConfigurationFileFormat.UseHypervisor;
            System.UseSparseAddressTable.Value = gameConfigurationFileFormat.UseSparseAddressTable;
            System.HleKernelThreadsCPUSet.Value = gameConfigurationFileFormat.HleKernelThreadsCPUSet;
            System.HleKernelThreadsCPUSetStaticCore.Value = gameConfigurationFileFormat.HleKernelThreadsCPUSetStaticCore;
            System.PtcBackgroundThreadsCPUSet.Value = gameConfigurationFileFormat.PtcBackgroundThreadsCPUSet;
            System.PtcBackgroundThreadCount.Value = gameConfigurationFileFormat.PtcBackgroundThreadCount;
            Hid.EnableKeyboard.Value = gameConfigurationFileFormat.EnableKeyboard;
            Hid.EnableMouse.Value = gameConfigurationFileFormat.EnableMouse;
            Hid.Hotkeys.Value = gameConfigurationFileFormat.Hotkeys;
            Hid.InputConfig.Value = gameConfigurationFileFormat.InputConfig;

            if (Hid.InputConfig.Value == null)
            {
                Hid.InputConfig.Value = new List<InputConfig>();
            }

            Multiplayer.LanInterfaceId.Value = gameConfigurationFileFormat.MultiplayerLanInterfaceId;
            Multiplayer.Mode.Value = gameConfigurationFileFormat.MultiplayerMode;
            Capture.BeginOnStart.Value = gameConfigurationFileFormat.CaptureBeginOnStart;
            Capture.OutputFormat.Value = gameConfigurationFileFormat.CaptureOutputFormat;
            Capture.VideoCodec.Value = gameConfigurationFileFormat.CaptureVideoCodec;
            Capture.VideoScaleEnabled.Value = gameConfigurationFileFormat.CaptureVideoScaleEnabled;
            Capture.VideoScaleWidth.Value = gameConfigurationFileFormat.CaptureVideoScaleWidth;
            Capture.VideoScaleWidthAuto.Value = gameConfigurationFileFormat.CaptureVideoScaleWidthAuto;
            Capture.VideoScaleHeight.Value = gameConfigurationFileFormat.CaptureVideoScaleHeight;
            Capture.VideoScaleHeightAuto.Value = gameConfigurationFileFormat.CaptureVideoScaleHeightAuto;
            Capture.VideoUseBitrate.Value = gameConfigurationFileFormat.CaptureVideoUseBitrate;
            Capture.VideoBitrate.Value = gameConfigurationFileFormat.CaptureVideoBitrate;
            Capture.VideoUseQualityLevel.Value = gameConfigurationFileFormat.CaptureVideoUseQualityLevel;
            Capture.VideoQualityLevel.Value = gameConfigurationFileFormat.CaptureVideoQualityLevel;
            Capture.VideoUseLossless.Value = gameConfigurationFileFormat.CaptureVideoUseLossless;
            Capture.VideoEncoderThreadCount.Value = gameConfigurationFileFormat.CaptureVideoEncoderThreadCount;
            Capture.VideoEncoderHardwareAcceleration.Value = gameConfigurationFileFormat.CaptureVideoEncoderHardwareAcceleration;
            Capture.VideoEncoderHardwareAccelerationAllowNvenc.Value = gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowNvenc;
            Capture.VideoEncoderHardwareAccelerationAllowQsv.Value = gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowQsv;
            Capture.VideoEncoderHardwareAccelerationAllowVulkan.Value = gameConfigurationFileFormat.CaptureVideoEncoderHardwareAccelerationAllowVulkan;
            Capture.AudioCodec.Value = gameConfigurationFileFormat.CaptureAudioCodec;

            if (configurationFileUpdated && !IsGlobalState)
            {
                ToFileFormat().SaveConfig(ConfigurationFilePath);
                Logger.Notice.Print(LogClass.Application, $"Configuration file updated to version {GameConfigurationFileFormat.CurrentVersion}");
            }

            EnableLogging = true;
        }

        public void Reload()
        {
            if (File.Exists(ConfigurationFilePath) &&
                GameConfigurationFileFormat.TryLoad(ConfigurationFilePath, out GameConfigurationFileFormat gameConfigFormat))
            {
                Load(gameConfigFormat);
            }
        }

        private static GraphicsBackend DefaultGraphicsBackend()
        {
            // Any system running macOS or returning any amount of valid Vulkan devices should default to Vulkan.
            // Checks for if the Vulkan version and featureset is compatible should be performed within VulkanRenderer.
            if (OperatingSystem.IsMacOS() || VulkanRenderer.GetPhysicalDevices().Length > 0)
            {
                return GraphicsBackend.Vulkan;
            }

            return GraphicsBackend.OpenGl;
        }

        private static void LogValueChange<T>(GameConfigurationState state, ReactiveEventArgs<T> eventArgs, string valueName)
        {
            if (state.EnableLogging)
            {
                string message = !state.IsGlobalState
                    ? string.Create(CultureInfo.InvariantCulture, $"\"{state.TitleName}\" ({state.TitleIdString}): {valueName} set to: {eventArgs.NewValue}")
                    : string.Create(CultureInfo.InvariantCulture, $"{valueName} set to: {eventArgs.NewValue}");

                Logger.Info?.Print(LogClass.Configuration, message);
            }
        }
    }
}
