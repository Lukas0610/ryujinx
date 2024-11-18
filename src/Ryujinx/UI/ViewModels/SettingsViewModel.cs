using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LibHac.Bcat;
using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.UI.Models.TimeZone;

namespace Ryujinx.Ava.UI.ViewModels
{

    public partial class SettingsViewModel : ObservableObject
    {

        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ContentManager _contentManager;
        private TimeZoneContentManager _timeZoneContentManager;

        private readonly Dictionary<string, string> _networkInterfaces;
        private readonly List<string> _validTimeZoneRegions;
        private readonly List<string> _gpuIds;

        private float _customResolutionScale;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConfigEnabled))]
        [NotifyPropertyChangedFor(nameof(IsGlobalConfigEnabled))]
        [NotifyPropertyChangedFor(nameof(IsGameConfigEnabled))]
        private bool _useCustomGameConfig;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomResolutionScale))]
        [NotifyPropertyChangedFor(nameof(IsCustomResolutionScaleActive))]
        private int _resolutionScale;

        [ObservableProperty]
        private int _graphicsBackendMultithreadingIndex;

        [ObservableProperty]
        private bool _isVulkanAvailable;

        [ObservableProperty]
        private bool _directoryChanged;

        [ObservableProperty]
        private bool _enableDiscordIntegration;

        [ObservableProperty]
        private bool _checkUpdatesOnStart;

        [ObservableProperty]
        private bool _showConfirmExit;

        [ObservableProperty]
        private bool _rememberWindowState;

        [ObservableProperty]
        private int _hideCursor;

        [ObservableProperty]
        private bool _enableDockedMode;

        [ObservableProperty]
        private bool _enableKeyboard;

        [ObservableProperty]
        private bool _enableMouse;

        [ObservableProperty]
        private bool _enableVsync;

        [ObservableProperty]
        private bool _enablePptc;

        [ObservableProperty]
        private bool _useStreamingPtc;

        [ObservableProperty]
        private bool _enableInternetAccess;

        [ObservableProperty]
        private bool _enableFsIntegrityChecks;

        [ObservableProperty]
        private bool _ignoreMissingServices;

        [ObservableProperty]
        private bool _enableShaderCache;

        [ObservableProperty]
        private bool _enableTextureRecompression;

        [ObservableProperty]
        private bool _enableMacroHLE;

        [ObservableProperty]
        private bool _enableColorSpacePassthrough;

        [ObservableProperty]
        private bool _enableFileLog;

        [ObservableProperty]
        private bool _enableStub;

        [ObservableProperty]
        private bool _enableInfo;

        [ObservableProperty]
        private bool _enableWarn;

        [ObservableProperty]
        private bool _enableError;

        [ObservableProperty]
        private bool _enableTrace;

        [ObservableProperty]
        private bool _enableGuest;

        [ObservableProperty]
        private bool _enableFsAccessLog;

        [ObservableProperty]
        private bool _enableDebug;

        [ObservableProperty]
        private bool _isOpenAlEnabled;

        [ObservableProperty]
        private bool _isSoundIoEnabled;

        [ObservableProperty]
        private bool _isSDL2Enabled;

        [ObservableProperty]
        private bool _useHypervisor;

        [ObservableProperty]
        private string _hleKernelThreadsCPUSet;

        [ObservableProperty]
        private bool _hleKernelThreadsCPUSetStaticCore;

        [ObservableProperty]
        private string _ptcBackgroundThreadsCPUSet;

        [ObservableProperty]
        private int _ptcBackgroundThreadCount;

        [ObservableProperty]
        private bool _useSparseAddressTable;

        [ObservableProperty]
        private string _timeZone;

        [ObservableProperty]
        private string _shaderDumpPath;

        [ObservableProperty]
        private int _language;

        [ObservableProperty]
        private int _region;

        [ObservableProperty]
        private int _fsGlobalAccessLogMode;

        [ObservableProperty]
        private int _audioBackend;

        [ObservableProperty]
        private int _maxAnisotropy;

        [ObservableProperty]
        private int _aspectRatio;

        [ObservableProperty]
        private int _antiAliasingEffect;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScalingFilterLevelText))]
        private int _scalingFilterLevel;

        [ObservableProperty]
        private int _openglDebugLevel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SparseAddressTableAvailable))]
        private int _memoryMode;

        [ObservableProperty]
        private int _memoryConfiguration;

        [ObservableProperty]
        private int _baseStyleIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVulkanSelected))]
        private int _graphicsBackendIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsScalingFilterActive))]
        private int _scalingFilter;

        [ObservableProperty]
        private int _preferredGpuIndex;

        [ObservableProperty]
        private float _volume;

        [ObservableProperty]
        private DateTimeOffset _currentDate;

        [ObservableProperty]
        private TimeSpan _currentTime;

        [ObservableProperty]
        private bool _enableHostFsBuffering;

        [ObservableProperty]
        private bool _enableHostFsBufferingPrefetch;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HostFsBufferingMaxCacheSizeString))]
        private long _hostFsBufferingMaxCacheSize;

        [ObservableProperty]
        private HotkeyConfig _keyboardHotkey;

        [ObservableProperty]
        private int _networkInterfaceIndex;

        [ObservableProperty]
        private int _multiplayerModeIndex;

        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set => SetProperty(ref _customResolutionScale, MathF.Round(value, 1));
        }

        public event Action CloseWindow;
        public event Action SaveSettingsEvent;

        public ConfigurationState Config { get; }

        public GameConfigurationState GameConfig { get; }

        public bool IsIngame { get; }

        public bool IsGlobalConfig { get; }

        public bool IsGameConfig { get; }

        public bool IsGameConfigInactive { get; }

        public AvaloniaList<string> GameDirectories { get; set; }

        public ObservableCollection<ComboBoxItem> AvailableGpus { get; set; }

        public bool CanChangeUseCustomGameConfig
            => !IsGlobalConfig;

        public bool IsConfigEnabled
            => IsGlobalConfigEnabled || IsGameConfigEnabled;

        public bool IsGlobalConfigEnabled
            => IsGlobalConfig;

        public bool IsGameConfigEnabled
            => IsGlobalConfig || UseCustomGameConfig;

        public int EnvironmentProcessorCount
            => Environment.ProcessorCount;

        public bool IsOpenGLAvailable
            => !OperatingSystem.IsMacOS();

        public bool IsHypervisorAvailable
            => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public bool SparseAddressTableAvailable
            => (!IsHypervisorAvailable || !UseHypervisor) && (MemoryMode != (int)MemoryManagerMode.SoftwarePageTable);

        public bool IsMacOS
            => OperatingSystem.IsMacOS();

        public bool ColorSpacePassthroughAvailable
            => IsMacOS;

        public bool IsCustomResolutionScaleActive
            => ResolutionScale == 4;

        public bool IsScalingFilterActive
            => ScalingFilter == (int)Ryujinx.Common.Configuration.ScalingFilter.Fsr;
        public bool IsVulkanSelected
        => GraphicsBackendIndex == 0;

        public string ScalingFilterLevelText
            => ScalingFilterLevel.ToString("0");

        public string HostFsBufferingMaxCacheSizeString
            => (HostFsBufferingMaxCacheSize > 0) ? ReadableStringUtils.FormatSize(HostFsBufferingMaxCacheSize, 2) : "Auto";

        internal AvaloniaList<TimeZone> TimeZones { get; }

        internal AvaloniaList<string> NetworkInterfaceList
        {
            get => new(_networkInterfaces.Keys);
        }

        public SettingsViewModel(ConfigurationState config, GameConfigurationState gameConfig, bool ingame, bool isGameConfigInactive, VirtualFileSystem virtualFileSystem, ContentManager contentManager)
            : this(config, gameConfig, ingame, isGameConfigInactive)
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }
        }

        public SettingsViewModel(ConfigurationState config, GameConfigurationState gameConfig, bool ingame, bool isGameConfigInactive)
        {
            Config = config;
            GameConfig = gameConfig;

            IsIngame = ingame;
            IsGlobalConfig = gameConfig.IsGlobalState;
            IsGameConfig = !gameConfig.IsGlobalState;
            IsGameConfigInactive = isGameConfigInactive;

            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            AvailableGpus = new ObservableCollection<ComboBoxItem>();

            _validTimeZoneRegions = new List<string>();
            _gpuIds = new List<string>();
            _networkInterfaces = new Dictionary<string, string>();

            Initialize();

            if (Program.PreviewerDetached)
            {
                LoadValuesFromConfiguration();
            }
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTimeZoneRegions.Contains(location))
            {
                TimeZone = location;
            }
        }

        public void ApplyButton()
        {
            ApplyValuesToConfiguration();
            SaveConfiguration();
        }

        public void OkButton()
        {
            ApplyValuesToConfiguration();
            SaveConfiguration();

            CloseWindow?.Invoke();
        }

        public void CancelButton()
        {
            if (GameConfig.IsGlobalState)
            {
                Config.Reload(Program.ConfigurationPath);
            }
            else
            {
                GameConfig.Reload();
            }

            CloseWindow?.Invoke();
        }

        public void DefaultsButton()
        {
            if (GameConfig.IsGlobalState)
            {
                Config.LoadDefault();
            }
            else
            {
                GameConfig.LoadDefault();
                GameConfig.UseGameConfig.Value = UseCustomGameConfig;
            }

            LoadValuesFromConfiguration();
        }

        public void GlobalValuesButton()
        {
            if (!GameConfig.IsGlobalState)
            {
                GameConfig.Load(Config.Game.ToFileFormat());
                GameConfig.UseGameConfig.Value = UseCustomGameConfig;

                LoadValuesFromConfiguration();
            }
        }

        private void Initialize()
        {
            PropertyChanged += HandlePropertyChanged;

            Task.Run(CheckSoundBackends);
            Task.Run(LoadAvailableGpus);
            Task.Run(PopulateNetworkInterfaces);
        }

        private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(UseCustomGameConfig):
                    if (IsGameConfigInactive && GameConfig.UseGameConfig != UseCustomGameConfig)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                            ContentDialogHelper.CreateInfoDialog(
                                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogUseCustomGameConfigChangedWhileIngame),
                                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogUseCustomGameConfigChangedWhileIngameMessage),
                                LocaleManager.Instance[LocaleKeys.InputDialogOk],
                                "",
                                LocaleManager.Instance[LocaleKeys.RyujinxInfo])
                        );
                    }
                    break;
                case nameof(GraphicsBackendMultithreadingIndex):
                    if (GraphicsBackendMultithreadingIndex != (int)GameConfig.Graphics.BackendThreading.Value)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                             ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                                "",
                                "",
                                LocaleManager.Instance[LocaleKeys.InputDialogOk],
                                LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle])
                        );
                    }
                    break;
                case nameof(Volume):
                    GameConfig.System.AudioVolume.Value = Volume / 100;
                    break;
                case nameof(NetworkInterfaceIndex):
                    GameConfig.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
                    break;
                case nameof(MultiplayerModeIndex):
                    GameConfig.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;
                    break;
            }
        }

        private async Task LoadAvailableGpus()
        {
            AvailableGpus.Clear();

            var devices = VulkanRenderer.GetPhysicalDevices();

            if (devices.Length == 0)
            {
                IsVulkanAvailable = false;
                GraphicsBackendIndex = 1;
            }
            else
            {
                foreach (var device in devices)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _gpuIds.Add(device.Id);
                        AvailableGpus.Add(new ComboBoxItem { Content = $"{device.Name} {(device.IsDiscrete ? "(dGPU)" : "")}" });
                    });
                }
            }

            // GPU configuration needs to be loaded during the async method or it will always return 0.
            int preferredGpuIndex = _gpuIds.Contains(GameConfig.Graphics.PreferredGpu)
                ? _gpuIds.IndexOf(GameConfig.Graphics.PreferredGpu)
                : 0;

            Dispatcher.UIThread.Post(() => PreferredGpuIndex = preferredGpuIndex);
        }

        private async Task PopulateNetworkInterfaces()
        {
            _networkInterfaces.Clear();
            _networkInterfaces.Add(LocaleManager.Instance[LocaleKeys.NetworkInterfaceDefault], "0");

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _networkInterfaces.Add(networkInterface.Name, networkInterface.Id);
                });
            }

            // Network interface index  needs to be loaded during the async method or it will always return 0.
            int networkInterfaceIndex = _networkInterfaces.Values.ToList().IndexOf(GameConfig.Multiplayer.LanInterfaceId.Value);

            Dispatcher.UIThread.Post(() => NetworkInterfaceIndex = networkInterfaceIndex);
        }

        private async Task CheckSoundBackends()
        {
            bool isOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            bool isSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            bool isSDL2Enabled = SDL2HardwareDeviceDriver.IsSupported;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsOpenAlEnabled = isOpenAlEnabled;
                IsSoundIoEnabled = isSoundIoEnabled;
                IsSDL2Enabled = isSDL2Enabled;
            });
        }

        private async Task LoadTimeZones()
        {
            _timeZoneContentManager = new TimeZoneContentManager();

            _timeZoneContentManager.InitializeInstance(_virtualFileSystem, _contentManager, IntegrityCheckLevel.None);

            foreach ((int offset, string location, string abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                int hours = Math.DivRem(offset, 3600, out int seconds);
                int minutes = Math.Abs(seconds) / 60;

                string abbr2 = abbr.StartsWith('+') || abbr.StartsWith('-') ? string.Empty : abbr;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TimeZones.Add(new TimeZone($"UTC{hours:+0#;-0#;+00}:{minutes:D2}", location, abbr2));

                    _validTimeZoneRegions.Add(location);
                });
            }

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(TimeZone)));
        }

        private void LoadValuesFromConfiguration()
        {
            if (GameConfig.IsGlobalState)
            {
                // User Interface
                EnableDiscordIntegration = Config.EnableDiscordIntegration;
                CheckUpdatesOnStart = Config.CheckUpdatesOnStart;
                ShowConfirmExit = Config.ShowConfirmExit;
                RememberWindowState = Config.RememberWindowState;

                GameDirectories.Clear();
                GameDirectories.AddRange(Config.UI.GameDirs.Value);

                BaseStyleIndex = Config.UI.BaseStyle.Value switch
                {
                    "Auto" => 0,
                    "Light" => 1,
                    "Dark" => 2,
                    _ => 0
                };

                // Logging
                EnableFileLog = Config.Logger.EnableFileLog;
                EnableStub = Config.Logger.EnableStub;
                EnableInfo = Config.Logger.EnableInfo;
                EnableWarn = Config.Logger.EnableWarn;
                EnableError = Config.Logger.EnableError;
                EnableTrace = Config.Logger.EnableTrace;
                EnableGuest = Config.Logger.EnableGuest;
                EnableDebug = Config.Logger.EnableDebug;
                EnableFsAccessLog = Config.Logger.EnableFsAccessLog;
                OpenglDebugLevel = (int)Config.Logger.GraphicsDebugLevel.Value;
            }
            else
            {
                UseCustomGameConfig = GameConfig.UseGameConfig.Value;
            }

            // User Interface
            HideCursor = (int)GameConfig.HideCursor.Value;

            // Input
            EnableDockedMode = GameConfig.System.EnableDockedMode;
            EnableKeyboard = GameConfig.Hid.EnableKeyboard;
            EnableMouse = GameConfig.Hid.EnableMouse;

            // Keyboard Hotkeys
            KeyboardHotkey = new HotkeyConfig(GameConfig.Hid.Hotkeys.Value);

            // System
            Region = (int)GameConfig.System.Region.Value;
            Language = (int)GameConfig.System.Language.Value;
            TimeZone = GameConfig.System.TimeZone;

            DateTime currentHostDateTime = DateTime.Now;
            TimeSpan systemDateTimeOffset = TimeSpan.FromSeconds(GameConfig.System.SystemTimeOffset);
            DateTime currentDateTime = currentHostDateTime.Add(systemDateTimeOffset);
            CurrentDate = currentDateTime.Date;
            CurrentTime = currentDateTime.TimeOfDay;

            EnableVsync = GameConfig.Graphics.EnableVsync;
            EnableFsIntegrityChecks = GameConfig.System.EnableFsIntegrityChecks;
            EnableHostFsBuffering = GameConfig.System.EnableHostFsBuffering;
            EnableHostFsBufferingPrefetch = GameConfig.System.EnableHostFsBufferingPrefetch;
            HostFsBufferingMaxCacheSize = GameConfig.System.HostFsBufferingMaxCacheSize;
            IgnoreMissingServices = GameConfig.System.IgnoreMissingServices;

            // CPU
            EnablePptc = GameConfig.System.EnablePtc;
            UseStreamingPtc = GameConfig.System.UseStreamingPtc;
            MemoryMode = (int)GameConfig.System.MemoryManagerMode.Value;
            MemoryConfiguration = (int)GameConfig.System.MemoryConfiguration.Value;
            UseHypervisor = GameConfig.System.UseHypervisor;
            UseSparseAddressTable = GameConfig.System.UseSparseAddressTable;
            HleKernelThreadsCPUSet = GameConfig.System.HleKernelThreadsCPUSet;
            HleKernelThreadsCPUSetStaticCore = GameConfig.System.HleKernelThreadsCPUSetStaticCore;
            PtcBackgroundThreadsCPUSet = GameConfig.System.PtcBackgroundThreadsCPUSet;
            PtcBackgroundThreadCount = GameConfig.System.PtcBackgroundThreadCount;

            // Graphics
            GraphicsBackendIndex = (int)GameConfig.Graphics.GraphicsBackend.Value;
            // Physical devices are queried asynchronously hence the prefered index config value is loaded in LoadAvailableGpus().
            EnableShaderCache = GameConfig.Graphics.EnableShaderCache;
            EnableTextureRecompression = GameConfig.Graphics.EnableTextureRecompression;
            EnableMacroHLE = GameConfig.Graphics.EnableMacroHLE;
            EnableColorSpacePassthrough = GameConfig.Graphics.EnableColorSpacePassthrough;
            ResolutionScale = GameConfig.Graphics.ResScale == -1 ? 4 : GameConfig.Graphics.ResScale - 1;
            CustomResolutionScale = GameConfig.Graphics.ResScaleCustom;
            MaxAnisotropy = GameConfig.Graphics.MaxAnisotropy == -1 ? 0 : (int)(MathF.Log2(GameConfig.Graphics.MaxAnisotropy));
            AspectRatio = (int)GameConfig.Graphics.AspectRatio.Value;
            GraphicsBackendMultithreadingIndex = (int)GameConfig.Graphics.BackendThreading.Value;
            ShaderDumpPath = GameConfig.Graphics.ShadersDumpPath;
            AntiAliasingEffect = (int)GameConfig.Graphics.AntiAliasing.Value;
            ScalingFilter = (int)GameConfig.Graphics.ScalingFilter.Value;
            ScalingFilterLevel = GameConfig.Graphics.ScalingFilterLevel.Value;

            // Audio
            AudioBackend = (int)GameConfig.System.AudioBackend.Value;
            Volume = GameConfig.System.AudioVolume * 100;

            // Network
            EnableInternetAccess = GameConfig.System.EnableInternetAccess;
            // LAN interface index is loaded asynchronously in PopulateNetworkInterfaces()

            MultiplayerModeIndex = (int)GameConfig.Multiplayer.Mode.Value;
        }

        private void ApplyValuesToConfiguration()
        {
            if (GameConfig.IsGlobalState)
            {
                // User Interface
                Config.EnableDiscordIntegration.Value = EnableDiscordIntegration;
                Config.CheckUpdatesOnStart.Value = CheckUpdatesOnStart;
                Config.ShowConfirmExit.Value = ShowConfirmExit;
                Config.RememberWindowState.Value = RememberWindowState;

                if (DirectoryChanged)
                {
                    List<string> gameDirs = new(GameDirectories);
                    Config.UI.GameDirs.Value = gameDirs;
                }

                Config.UI.BaseStyle.Value = BaseStyleIndex switch
                {
                    0 => "Auto",
                    1 => "Light",
                    2 => "Dark",
                    _ => "Auto"
                };

                // Logging
                Config.Logger.EnableFileLog.Value = EnableFileLog;
                Config.Logger.EnableStub.Value = EnableStub;
                Config.Logger.EnableInfo.Value = EnableInfo;
                Config.Logger.EnableWarn.Value = EnableWarn;
                Config.Logger.EnableError.Value = EnableError;
                Config.Logger.EnableTrace.Value = EnableTrace;
                Config.Logger.EnableGuest.Value = EnableGuest;
                Config.Logger.EnableDebug.Value = EnableDebug;
                Config.Logger.EnableFsAccessLog.Value = EnableFsAccessLog;
                Config.Logger.FsGlobalAccessLogMode.Value = FsGlobalAccessLogMode;
                Config.Logger.GraphicsDebugLevel.Value = (GraphicsDebugLevel)OpenglDebugLevel;
            }
            else
            {
                GameConfig.UseGameConfig.Value = UseCustomGameConfig;
            }

            // User Interface
            GameConfig.HideCursor.Value = (HideCursorMode)HideCursor;

            // Input
            GameConfig.System.EnableDockedMode.Value = EnableDockedMode;
            GameConfig.Hid.EnableKeyboard.Value = EnableKeyboard;
            GameConfig.Hid.EnableMouse.Value = EnableMouse;

            // Keyboard Hotkeys
            GameConfig.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();

            // System
            GameConfig.System.Region.Value = (Region)Region;
            GameConfig.System.Language.Value = (Language)Language;

            if (_validTimeZoneRegions.Contains(TimeZone))
            {
                GameConfig.System.TimeZone.Value = TimeZone;
            }

            GameConfig.System.SystemTimeOffset.Value = Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());
            GameConfig.Graphics.EnableVsync.Value = EnableVsync;
            GameConfig.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            GameConfig.System.EnableHostFsBuffering.Value = EnableHostFsBuffering;
            GameConfig.System.EnableHostFsBufferingPrefetch.Value = EnableHostFsBufferingPrefetch;
            GameConfig.System.HostFsBufferingMaxCacheSize.Value = HostFsBufferingMaxCacheSize;
            GameConfig.System.IgnoreMissingServices.Value = IgnoreMissingServices;

            // CPU
            GameConfig.System.EnablePtc.Value = EnablePptc;
            GameConfig.System.UseStreamingPtc.Value = UseStreamingPtc;
            GameConfig.System.MemoryManagerMode.Value = (MemoryManagerMode)MemoryMode;
            GameConfig.System.MemoryConfiguration.Value = (MemoryConfiguration)MemoryConfiguration;
            GameConfig.System.UseHypervisor.Value = UseHypervisor;
            GameConfig.System.UseSparseAddressTable.Value = UseSparseAddressTable;
            GameConfig.System.HleKernelThreadsCPUSet.Value = HleKernelThreadsCPUSet;
            GameConfig.System.HleKernelThreadsCPUSetStaticCore.Value = HleKernelThreadsCPUSetStaticCore;
            GameConfig.System.PtcBackgroundThreadsCPUSet.Value = PtcBackgroundThreadsCPUSet;
            GameConfig.System.PtcBackgroundThreadCount.Value = PtcBackgroundThreadCount;

            // Graphics
            GameConfig.Graphics.GraphicsBackend.Value = (GraphicsBackend)GraphicsBackendIndex;
            GameConfig.Graphics.PreferredGpu.Value = _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            GameConfig.Graphics.EnableShaderCache.Value = EnableShaderCache;
            GameConfig.Graphics.EnableTextureRecompression.Value = EnableTextureRecompression;
            GameConfig.Graphics.EnableMacroHLE.Value = EnableMacroHLE;
            GameConfig.Graphics.EnableColorSpacePassthrough.Value = EnableColorSpacePassthrough;
            GameConfig.Graphics.ResScale.Value = ResolutionScale == 4 ? -1 : ResolutionScale + 1;
            GameConfig.Graphics.ResScaleCustom.Value = CustomResolutionScale;
            GameConfig.Graphics.MaxAnisotropy.Value = MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy);
            GameConfig.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            GameConfig.Graphics.AntiAliasing.Value = (AntiAliasing)AntiAliasingEffect;
            GameConfig.Graphics.ScalingFilter.Value = (ScalingFilter)ScalingFilter;
            GameConfig.Graphics.ScalingFilterLevel.Value = ScalingFilterLevel;

            if (GameConfig.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }

            GameConfig.Graphics.BackendThreading.Value = (BackendThreading)GraphicsBackendMultithreadingIndex;
            GameConfig.Graphics.ShadersDumpPath.Value = ShaderDumpPath;

            // Audio
            AudioBackend audioBackend = (AudioBackend)AudioBackend;
            if (audioBackend != GameConfig.System.AudioBackend.Value)
            {
                GameConfig.System.AudioBackend.Value = audioBackend;

                Logger.Info?.Print(LogClass.Application, $"AudioBackend toggled to: {audioBackend}");
            }

            GameConfig.System.AudioVolume.Value = Volume / 100;

            // Network
            GameConfig.System.EnableInternetAccess.Value = EnableInternetAccess;

            GameConfig.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            GameConfig.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;

            MainWindow.UpdateGraphicsConfig(GameConfig);

            DirectoryChanged = false;
        }

        private void SaveConfiguration()
        {
            if (GameConfig.IsGlobalState)
            {
                Config.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
            else
            {
                GameConfig.ToFileFormat().SaveConfig(GameConfig.ConfigurationFilePath);
            }

            SaveSettingsEvent?.Invoke();
        }

    }
}
