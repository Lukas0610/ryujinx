using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Integration;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Utilities;
using Ryujinx.Graphics.GAL;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.UI;
using System;

namespace Ryujinx.HLE
{
    /// <summary>
    /// HLE configuration.
    /// </summary>
    public class HLEConfiguration
    {
        /// <summary>
        /// The virtual file system used by the FS service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly VirtualFileSystem VirtualFileSystem;

        /// <summary>
        /// The manager for handling a LibHac Horizon instance.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly LibHacHorizonManager LibHacHorizonManager;

        /// <summary>
        /// The account manager used by the account service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly AccountManager AccountManager;

        /// <summary>
        /// The content manager used by the NCM service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly ContentManager ContentManager;

        /// <summary>
        /// The persistent information between run for multi-application capabilities.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        public readonly UserChannelPersistence UserChannelPersistence;

        /// <summary>
        /// The GPU renderer to use for all GPU operations.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly IRenderer GpuRenderer;

        /// <summary>
        /// The audio device driver to use for all audio operations.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly IHardwareDeviceDriver AudioDeviceDriver;

        /// <summary>
        /// The handler for various UI related operations needed outside of HLE.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly IHostUIHandler HostUIHandler;

        /// <summary>
        /// Control the memory configuration used by the emulation context.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly MemoryConfiguration MemoryConfiguration;

        /// <summary>
        /// The system language to use in the settings service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly SystemLanguage SystemLanguage;

        /// <summary>
        /// The system region to use in the settings service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly RegionCode Region;

        /// <summary>
        /// Control the initial state of the vertical sync in the SurfaceFlinger service.
        /// </summary>
        internal readonly bool EnableVsync;

        /// <summary>
        /// Control the initial state of the docked mode.
        /// </summary>
        internal readonly bool EnableDockedMode;

        /// <summary>
        /// Control if the Profiled Translation Cache (PTC) should be used.
        /// </summary>
        internal readonly bool EnablePtc;

        /// <summary>
        /// Whether to use streaming PTC (SPTC) instead of the traditional PTC
        /// </summary>
        internal readonly bool UseStreamingPtc;

        /// <summary>
        /// Control if the guest application should be told that there is a Internet connection available.
        /// </summary>
        public bool EnableInternetAccess { internal get; set; }

        /// <summary>
        /// Control LibHac's integrity check level.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly IntegrityCheckLevel FsIntegrityCheckLevel;

        /// <summary>
        /// Control LibHac's global access logging level. Value must be between 0 and 3.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly int FsGlobalAccessLogMode;

        /// <summary>
        /// The system time offset to apply to the time service steady and local clocks.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly long SystemTimeOffset;

        /// <summary>
        /// The system timezone used by the time service.
        /// </summary>
        /// <remarks>This cannot be changed after <see cref="Switch"/> instantiation.</remarks>
        internal readonly string TimeZone;

        /// <summary>
        /// Type of the memory manager used on CPU emulation.
        /// </summary>
        public MemoryManagerMode MemoryManagerMode { internal get; set; }

        /// <summary>
        /// Control the initial state of the ignore missing services setting.
        /// If this is set to true, when a missing service is encountered, it will try to automatically handle it instead of throwing an exception.
        /// </summary>
        /// TODO: Update this again.
        public bool IgnoreMissingServices { internal get; set; }

        /// <summary>
        /// Aspect Ratio applied to the renderer window by the SurfaceFlinger service.
        /// </summary>
        public AspectRatio AspectRatio { get; set; }

        /// <summary>
        /// The audio volume level.
        /// </summary>
        public float AudioVolume { get; set; }

        /// <summary>
        /// Use Hypervisor over JIT if available.
        /// </summary>
        internal readonly bool UseHypervisor;

        /// <summary>
        /// Use sparse function address tables if available
        /// </summary>
        internal readonly bool UseSparseAddressTable;

        /// <summary>
        /// Multiplayer LAN Interface ID (device GUID)
        /// </summary>
        public string MultiplayerLanInterfaceId { internal get; set; }

        /// <summary>
        /// Multiplayer Mode
        /// </summary>
        public MultiplayerMode MultiplayerMode { internal get; set; }

        /// <summary>
        /// An action called when HLE force a refresh of output after docked mode changed.
        /// </summary>
        public Action RefreshInputConfig { internal get; set; }

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
        /// List of logical CPU cores the HLE kernel threads are allowed to run on
        /// </summary>
        public CPUSet HleKernelThreadsCPUSet { get; }

        /// <summary>
        /// Whether to assign a HLE kernel-thread to a single logical CPU core from the configured CPU set
        /// </summary>
        public bool HleKernelThreadsCPUSetStaticCore { get; }

        /// <summary>
        /// List of logical CPU cores the PTC background threads are allowed to run on
        /// </summary>
        public CPUSet PtcBackgroundThreadsCPUSet { get; }

        /// <summary>
        /// Number of PTC background threads to start
        /// </summary>
        public int PtcBackgroundThreadCount { get; }

        public HLEConfiguration(VirtualFileSystem virtualFileSystem,
                                LibHacHorizonManager libHacHorizonManager,
                                ContentManager contentManager,
                                AccountManager accountManager,
                                UserChannelPersistence userChannelPersistence,
                                IRenderer gpuRenderer,
                                IHardwareDeviceDriver audioDeviceDriver,
                                MemoryConfiguration memoryConfiguration,
                                IHostUIHandler hostUIHandler,
                                SystemLanguage systemLanguage,
                                RegionCode region,
                                bool enableVsync,
                                bool enableDockedMode,
                                bool enablePtc,
                                bool useStreamingPtc,
                                bool enableInternetAccess,
                                IntegrityCheckLevel fsIntegrityCheckLevel,
                                bool enableHostFsBuffering,
                                bool enableHostFsBufferingPrefetch,
                                long hostFsBufferingMaxCacheSize,
                                int fsGlobalAccessLogMode,
                                long systemTimeOffset,
                                string timeZone,
                                MemoryManagerMode memoryManagerMode,
                                bool ignoreMissingServices,
                                AspectRatio aspectRatio,
                                float audioVolume,
                                bool useHypervisor,
                                bool useSparseAddressTable,
                                string multiplayerLanInterfaceId,
                                MultiplayerMode multiplayerMode,
                                CPUSet hleKernelThreadsCPUSet,
                                bool hleKernelThreadsCPUSetStaticCore,
                                CPUSet ptcBackgroundThreadsCPUSet,
                                int ptcBackgroundThreadCount)
        {
            VirtualFileSystem = virtualFileSystem;
            LibHacHorizonManager = libHacHorizonManager;
            AccountManager = accountManager;
            ContentManager = contentManager;
            UserChannelPersistence = userChannelPersistence;
            GpuRenderer = gpuRenderer;
            AudioDeviceDriver = audioDeviceDriver;
            MemoryConfiguration = memoryConfiguration;
            HostUIHandler = hostUIHandler;
            SystemLanguage = systemLanguage;
            Region = region;
            EnableVsync = enableVsync;
            EnableDockedMode = enableDockedMode;
            EnablePtc = enablePtc;
            UseStreamingPtc = useStreamingPtc;
            EnableInternetAccess = enableInternetAccess;
            FsIntegrityCheckLevel = fsIntegrityCheckLevel;
            EnableHostFsBuffering = enableHostFsBuffering;
            EnableHostFsBufferingPrefetch = enableHostFsBufferingPrefetch;
            HostFsBufferingMaxCacheSize = hostFsBufferingMaxCacheSize;
            FsGlobalAccessLogMode = fsGlobalAccessLogMode;
            SystemTimeOffset = systemTimeOffset;
            TimeZone = timeZone;
            MemoryManagerMode = memoryManagerMode;
            IgnoreMissingServices = ignoreMissingServices;
            AspectRatio = aspectRatio;
            AudioVolume = audioVolume;
            UseHypervisor = useHypervisor;
            UseSparseAddressTable = useSparseAddressTable;
            MultiplayerLanInterfaceId = multiplayerLanInterfaceId;
            MultiplayerMode = multiplayerMode;
            HleKernelThreadsCPUSet = hleKernelThreadsCPUSet;
            HleKernelThreadsCPUSetStaticCore = hleKernelThreadsCPUSetStaticCore;
            PtcBackgroundThreadsCPUSet = ptcBackgroundThreadsCPUSet;
            PtcBackgroundThreadCount = ptcBackgroundThreadCount;
        }
    }
}
