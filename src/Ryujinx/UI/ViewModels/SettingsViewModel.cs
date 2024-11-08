using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
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
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.UI.Models.TimeZone;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ContentManager _contentManager;
        private TimeZoneContentManager _timeZoneContentManager;

        private readonly ConfigurationState _config;
        private readonly GameConfigurationState _gameConfig;

        private readonly List<string> _validTzRegions;

        private readonly Dictionary<string, string> _networkInterfaces;

        private bool _useCustomGameConfig;
        private float _customResolutionScale;
        private int _resolutionScale;
        private int _graphicsBackendMultithreadingIndex;
        private float _volume;
        private bool _isVulkanAvailable = true;
        private bool _directoryChanged;
        private readonly List<string> _gpuIds = new();
        private int _graphicsBackendIndex;
        private int _scalingFilter;
        private int _scalingFilterLevel;
        private bool _enableHostFsBuffering;
        private long _hostFsBufferingMaxCacheSize;
        private int _memoryMode;
        private bool _enablePptc;

        public event Action CloseWindow;
        public event Action SaveSettingsEvent;
        private int _networkInterfaceIndex;
        private int _multiplayerModeIndex;

        public ConfigurationState Config => _config;

        public GameConfigurationState GameConfig => _gameConfig;

        public bool IsIngame { get; }

        public bool IsGlobalConfig { get; }

        public bool IsGameConfig { get; }

        public bool UseCustomGameConfig
        {
            get => _useCustomGameConfig;
            set
            {
                _useCustomGameConfig = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigEnabled));
                OnPropertyChanged(nameof(IsGlobalConfigEnabled));
                OnPropertyChanged(nameof(IsGameConfigEnabled));
            }
        }

        public bool CanChangeUseCustomGameConfig
        {
            get => !IsGlobalConfig && !IsIngame;
        }

        public bool IsConfigEnabled
        {
            get => IsGlobalConfigEnabled || IsGameConfigEnabled;
        }

        public bool IsGlobalConfigEnabled
        {
            get => IsGlobalConfig;
        }

        public bool IsGameConfigEnabled
        {
            get => IsGlobalConfig || _useCustomGameConfig;
        }

        public int ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                _resolutionScale = value;

                OnPropertyChanged(nameof(CustomResolutionScale));
                OnPropertyChanged(nameof(IsCustomResolutionScaleActive));
            }
        }

        public int GraphicsBackendMultithreadingIndex
        {
            get => _graphicsBackendMultithreadingIndex;
            set
            {
                _graphicsBackendMultithreadingIndex = value;

                if (_graphicsBackendMultithreadingIndex != (int)_gameConfig.Graphics.BackendThreading.Value)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                         ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                            "",
                            "",
                            LocaleManager.Instance[LocaleKeys.InputDialogOk],
                            LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle])
                    );
                }

                OnPropertyChanged();
            }
        }

        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set
            {
                _customResolutionScale = MathF.Round(value, 1);

                OnPropertyChanged();
            }
        }

        public bool IsVulkanAvailable
        {
            get => _isVulkanAvailable;
            set
            {
                _isVulkanAvailable = value;

                OnPropertyChanged();
            }
        }

        public bool IsOpenGLAvailable => !OperatingSystem.IsMacOS();

        public bool IsHypervisorAvailable => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public bool SparseAddressTableAvailable => (!IsHypervisorAvailable || !UseHypervisor) && (MemoryMode != (int)MemoryManagerMode.SoftwarePageTable);

        public bool DirectoryChanged
        {
            get => _directoryChanged;
            set
            {
                _directoryChanged = value;

                OnPropertyChanged();
            }
        }

        public bool IsMacOS => OperatingSystem.IsMacOS();

        public bool EnableDiscordIntegration { get; set; }
        public bool CheckUpdatesOnStart { get; set; }
        public bool ShowConfirmExit { get; set; }
        public bool RememberWindowState { get; set; }
        public int HideCursor { get; set; }
        public bool EnableDockedMode { get; set; }
        public bool EnableKeyboard { get; set; }
        public bool EnableMouse { get; set; }
        public bool EnableVsync { get; set; }
        public bool EnablePptc
        {
            get => _enablePptc;
            set
            {
                _enablePptc = value;

                OnPropertyChanged();
            }
        }
        public bool UseStreamingPtc { get; set; }
        public bool EnableInternetAccess { get; set; }
        public bool EnableFsIntegrityChecks { get; set; }
        public bool IgnoreMissingServices { get; set; }
        public bool EnableShaderCache { get; set; }
        public bool EnableTextureRecompression { get; set; }
        public bool EnableMacroHLE { get; set; }
        public bool EnableColorSpacePassthrough { get; set; }
        public bool ColorSpacePassthroughAvailable => IsMacOS;
        public bool EnableFileLog { get; set; }
        public bool EnableStub { get; set; }
        public bool EnableInfo { get; set; }
        public bool EnableWarn { get; set; }
        public bool EnableError { get; set; }
        public bool EnableTrace { get; set; }
        public bool EnableGuest { get; set; }
        public bool EnableFsAccessLog { get; set; }
        public bool EnableDebug { get; set; }
        public bool IsOpenAlEnabled { get; set; }
        public bool IsSoundIoEnabled { get; set; }
        public bool IsSDL2Enabled { get; set; }
        public bool IsCustomResolutionScaleActive => _resolutionScale == 4;
        public bool IsScalingFilterActive => _scalingFilter == (int)Ryujinx.Common.Configuration.ScalingFilter.Fsr;

        public bool IsVulkanSelected => GraphicsBackendIndex == 0;
        public bool UseHypervisor { get; set; }
        public string HleKernelThreadsCPUSet { get; set; }
        public bool HleKernelThreadsCPUSetStaticCore { get; set; }
        public string PtcBackgroundThreadsCPUSet { get; set; }
        public int PtcBackgroundThreadCount { get; set; }
        public bool UseSparseAddressTable { get; set; }

        public string TimeZone { get; set; }
        public string ShaderDumpPath { get; set; }

        public int Language { get; set; }
        public int Region { get; set; }
        public int FsGlobalAccessLogMode { get; set; }
        public int AudioBackend { get; set; }
        public int MaxAnisotropy { get; set; }
        public int AspectRatio { get; set; }
        public int AntiAliasingEffect { get; set; }
        public string ScalingFilterLevelText => ScalingFilterLevel.ToString("0");
        public int ScalingFilterLevel
        {
            get => _scalingFilterLevel;
            set
            {
                _scalingFilterLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalingFilterLevelText));
            }
        }
        public int OpenglDebugLevel { get; set; }
        public int MemoryMode
        {
            get => _memoryMode;
            set
            {
                _memoryMode = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(SparseAddressTableAvailable));
            }
        }

        public int MemoryConfiguration { get; set; }
        public int BaseStyleIndex { get; set; }
        public int GraphicsBackendIndex
        {
            get => _graphicsBackendIndex;
            set
            {
                _graphicsBackendIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVulkanSelected));
            }
        }
        public int ScalingFilter
        {
            get => _scalingFilter;
            set
            {
                _scalingFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScalingFilterActive));
            }
        }

        public int PreferredGpuIndex { get; set; }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;

                _gameConfig.System.AudioVolume.Value = _volume / 100;

                OnPropertyChanged();
            }
        }

        public DateTimeOffset CurrentDate { get; set; }
        public TimeSpan CurrentTime { get; set; }

        public bool EnableHostFsBuffering
        {
            get => _enableHostFsBuffering;
            set
            {
                _enableHostFsBuffering = value;
                OnPropertyChanged();
            }
        }

        public bool EnableHostFsBufferingPrefetch { get; set; }

        public long HostFsBufferingMaxCacheSize
        {
            get => _hostFsBufferingMaxCacheSize;
            set
            {
                _hostFsBufferingMaxCacheSize = value;

                if (value > 0)
                    HostFsBufferingMaxCacheSizeString = ReadableStringUtils.FormatSize(value, 2);
                else
                    HostFsBufferingMaxCacheSizeString = "Auto";

                OnPropertyChanged();
                OnPropertyChanged(nameof(HostFsBufferingMaxCacheSizeString));
            }
        }

        internal string HostFsBufferingMaxCacheSizeString { get; private set; }

        internal AvaloniaList<TimeZone> TimeZones { get; set; }
        public AvaloniaList<string> GameDirectories { get; set; }
        public ObservableCollection<ComboBoxItem> AvailableGpus { get; set; }

        public int EnvironmentProcessorCount
        {
            get => Environment.ProcessorCount;
        }

        public AvaloniaList<string> NetworkInterfaceList
        {
            get => new(_networkInterfaces.Keys);
        }

        public HotkeyConfig KeyboardHotkey { get; set; }

        public int NetworkInterfaceIndex
        {
            get => _networkInterfaceIndex;
            set
            {
                _networkInterfaceIndex = value != -1 ? value : 0;
                _gameConfig.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[_networkInterfaceIndex]];
            }
        }

        public int MultiplayerModeIndex
        {
            get => _multiplayerModeIndex;
            set
            {
                _multiplayerModeIndex = value;
                _gameConfig.Multiplayer.Mode.Value = (MultiplayerMode)_multiplayerModeIndex;
            }
        }

        public SettingsViewModel(ConfigurationState config, GameConfigurationState gameConfig, bool ingame, VirtualFileSystem virtualFileSystem, ContentManager contentManager)
            : this(config, gameConfig, ingame)
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }
        }

        public SettingsViewModel(ConfigurationState config, GameConfigurationState gameConfig, bool ingame)
        {
            _config = config;
            _gameConfig = gameConfig;

            IsIngame = ingame;
            IsGlobalConfig = gameConfig.IsGlobalState;
            IsGameConfig = !gameConfig.IsGlobalState;

            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            AvailableGpus = new ObservableCollection<ComboBoxItem>();
            _validTzRegions = new List<string>();
            _networkInterfaces = new Dictionary<string, string>();

            Initialize();

            if (Program.PreviewerDetached)
            {
                LoadConfiguration();
            }
        }

        private void Initialize()
        {
            Task.Run(CheckSoundBackends);
            Task.Run(LoadAvailableGpus);
            Task.Run(PopulateNetworkInterfaces);

            async Task LoadAvailableGpus()
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
                PreferredGpuIndex = _gpuIds.Contains(_gameConfig.Graphics.PreferredGpu) ?
                                    _gpuIds.IndexOf(_gameConfig.Graphics.PreferredGpu) : 0;

                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(PreferredGpuIndex)));
            }

            async Task PopulateNetworkInterfaces()
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
                NetworkInterfaceIndex = _networkInterfaces.Values.ToList().IndexOf(_gameConfig.Multiplayer.LanInterfaceId.Value);

                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(NetworkInterfaceIndex)));
            }
        }

        public async Task CheckSoundBackends()
        {
            IsOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            IsSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            IsSDL2Enabled = SDL2HardwareDeviceDriver.IsSupported;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(IsOpenAlEnabled));
                OnPropertyChanged(nameof(IsSoundIoEnabled));
                OnPropertyChanged(nameof(IsSDL2Enabled));
            });
        }

        public async Task LoadTimeZones()
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

                    _validTzRegions.Add(location);
                });
            }

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(TimeZone)));
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTzRegions.Contains(location))
            {
                TimeZone = location;
            }
        }

        public void LoadConfiguration()
        {
            if (_config != null)
            {
                // User Interface
                EnableDiscordIntegration = _config.EnableDiscordIntegration;
                CheckUpdatesOnStart = _config.CheckUpdatesOnStart;
                ShowConfirmExit = _config.ShowConfirmExit;
                RememberWindowState = _config.RememberWindowState;

                GameDirectories.Clear();
                GameDirectories.AddRange(_config.UI.GameDirs.Value);

                BaseStyleIndex = _config.UI.BaseStyle.Value switch
                {
                    "Auto" => 0,
                    "Light" => 1,
                    "Dark" => 2,
                    _ => 0
                };

                // Logging
                EnableFileLog = _config.Logger.EnableFileLog;
                EnableStub = _config.Logger.EnableStub;
                EnableInfo = _config.Logger.EnableInfo;
                EnableWarn = _config.Logger.EnableWarn;
                EnableError = _config.Logger.EnableError;
                EnableTrace = _config.Logger.EnableTrace;
                EnableGuest = _config.Logger.EnableGuest;
                EnableDebug = _config.Logger.EnableDebug;
                EnableFsAccessLog = _config.Logger.EnableFsAccessLog;
                OpenglDebugLevel = (int)_config.Logger.GraphicsDebugLevel.Value;
            }

            if (!_gameConfig.IsGlobalState)
            {
                UseCustomGameConfig = _gameConfig.UseGameConfig.Value;
            }

            // User Interface
            HideCursor = (int)_gameConfig.HideCursor.Value;

            // Input
            EnableDockedMode = _gameConfig.System.EnableDockedMode;
            EnableKeyboard = _gameConfig.Hid.EnableKeyboard;
            EnableMouse = _gameConfig.Hid.EnableMouse;

            // Keyboard Hotkeys
            KeyboardHotkey = new HotkeyConfig(_gameConfig.Hid.Hotkeys.Value);

            // System
            Region = (int)_gameConfig.System.Region.Value;
            Language = (int)_gameConfig.System.Language.Value;
            TimeZone = _gameConfig.System.TimeZone;

            DateTime currentHostDateTime = DateTime.Now;
            TimeSpan systemDateTimeOffset = TimeSpan.FromSeconds(_gameConfig.System.SystemTimeOffset);
            DateTime currentDateTime = currentHostDateTime.Add(systemDateTimeOffset);
            CurrentDate = currentDateTime.Date;
            CurrentTime = currentDateTime.TimeOfDay;

            EnableVsync = _gameConfig.Graphics.EnableVsync;
            EnableFsIntegrityChecks = _gameConfig.System.EnableFsIntegrityChecks;
            EnableHostFsBuffering = _gameConfig.System.EnableHostFsBuffering;
            EnableHostFsBufferingPrefetch = _gameConfig.System.EnableHostFsBufferingPrefetch;
            HostFsBufferingMaxCacheSize = _gameConfig.System.HostFsBufferingMaxCacheSize;
            IgnoreMissingServices = _gameConfig.System.IgnoreMissingServices;

            // CPU
            EnablePptc = _gameConfig.System.EnablePtc;
            UseStreamingPtc = _gameConfig.System.UseStreamingPtc;
            MemoryMode = (int)_gameConfig.System.MemoryManagerMode.Value;
            MemoryConfiguration = (int)_gameConfig.System.MemoryConfiguration.Value;
            UseHypervisor = _gameConfig.System.UseHypervisor;
            UseSparseAddressTable = _gameConfig.System.UseSparseAddressTable;
            HleKernelThreadsCPUSet = _gameConfig.System.HleKernelThreadsCPUSet;
            HleKernelThreadsCPUSetStaticCore = _gameConfig.System.HleKernelThreadsCPUSetStaticCore;
            PtcBackgroundThreadsCPUSet = _gameConfig.System.PtcBackgroundThreadsCPUSet;
            PtcBackgroundThreadCount = _gameConfig.System.PtcBackgroundThreadCount;

            // Graphics
            GraphicsBackendIndex = (int)_gameConfig.Graphics.GraphicsBackend.Value;
            // Physical devices are queried asynchronously hence the prefered index config value is loaded in LoadAvailableGpus().
            EnableShaderCache = _gameConfig.Graphics.EnableShaderCache;
            EnableTextureRecompression = _gameConfig.Graphics.EnableTextureRecompression;
            EnableMacroHLE = _gameConfig.Graphics.EnableMacroHLE;
            EnableColorSpacePassthrough = _gameConfig.Graphics.EnableColorSpacePassthrough;
            ResolutionScale = _gameConfig.Graphics.ResScale == -1 ? 4 : _gameConfig.Graphics.ResScale - 1;
            CustomResolutionScale = _gameConfig.Graphics.ResScaleCustom;
            MaxAnisotropy = _gameConfig.Graphics.MaxAnisotropy == -1 ? 0 : (int)(MathF.Log2(_gameConfig.Graphics.MaxAnisotropy));
            AspectRatio = (int)_gameConfig.Graphics.AspectRatio.Value;
            GraphicsBackendMultithreadingIndex = (int)_gameConfig.Graphics.BackendThreading.Value;
            ShaderDumpPath = _gameConfig.Graphics.ShadersDumpPath;
            AntiAliasingEffect = (int)_gameConfig.Graphics.AntiAliasing.Value;
            ScalingFilter = (int)_gameConfig.Graphics.ScalingFilter.Value;
            ScalingFilterLevel = _gameConfig.Graphics.ScalingFilterLevel.Value;

            // Audio
            AudioBackend = (int)_gameConfig.System.AudioBackend.Value;
            Volume = _gameConfig.System.AudioVolume * 100;

            // Network
            EnableInternetAccess = _gameConfig.System.EnableInternetAccess;
            // LAN interface index is loaded asynchronously in PopulateNetworkInterfaces()

            MultiplayerModeIndex = (int)_gameConfig.Multiplayer.Mode.Value;
        }

        public void SaveConfiguration()
        {
            if (_config != null)
            {
                // User Interface
                _config.EnableDiscordIntegration.Value = EnableDiscordIntegration;
                _config.CheckUpdatesOnStart.Value = CheckUpdatesOnStart;
                _config.ShowConfirmExit.Value = ShowConfirmExit;
                _config.RememberWindowState.Value = RememberWindowState;

                if (_directoryChanged)
                {
                    List<string> gameDirs = new(GameDirectories);
                    _config.UI.GameDirs.Value = gameDirs;
                }

                _config.UI.BaseStyle.Value = BaseStyleIndex switch
                {
                    0 => "Auto",
                    1 => "Light",
                    2 => "Dark",
                    _ => "Auto"
                };

                // Logging
                _config.Logger.EnableFileLog.Value = EnableFileLog;
                _config.Logger.EnableStub.Value = EnableStub;
                _config.Logger.EnableInfo.Value = EnableInfo;
                _config.Logger.EnableWarn.Value = EnableWarn;
                _config.Logger.EnableError.Value = EnableError;
                _config.Logger.EnableTrace.Value = EnableTrace;
                _config.Logger.EnableGuest.Value = EnableGuest;
                _config.Logger.EnableDebug.Value = EnableDebug;
                _config.Logger.EnableFsAccessLog.Value = EnableFsAccessLog;
                _config.Logger.GraphicsDebugLevel.Value = (GraphicsDebugLevel)OpenglDebugLevel;
            }

            if (!_gameConfig.IsGlobalState)
            {
                _gameConfig.UseGameConfig.Value = UseCustomGameConfig;
            }

            // User Interface
            _gameConfig.HideCursor.Value = (HideCursorMode)HideCursor;

            // Input
            _gameConfig.System.EnableDockedMode.Value = EnableDockedMode;
            _gameConfig.Hid.EnableKeyboard.Value = EnableKeyboard;
            _gameConfig.Hid.EnableMouse.Value = EnableMouse;

            // Keyboard Hotkeys
            _gameConfig.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();

            // System
            _gameConfig.System.Region.Value = (Region)Region;
            _gameConfig.System.Language.Value = (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                _gameConfig.System.TimeZone.Value = TimeZone;
            }

            _gameConfig.System.SystemTimeOffset.Value = Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());
            _gameConfig.Graphics.EnableVsync.Value = EnableVsync;
            _gameConfig.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            _gameConfig.System.EnableHostFsBuffering.Value = EnableHostFsBuffering;
            _gameConfig.System.EnableHostFsBufferingPrefetch.Value = EnableHostFsBufferingPrefetch;
            _gameConfig.System.HostFsBufferingMaxCacheSize.Value = HostFsBufferingMaxCacheSize;
            _gameConfig.System.IgnoreMissingServices.Value = IgnoreMissingServices;

            // CPU
            _gameConfig.System.EnablePtc.Value = EnablePptc;
            _gameConfig.System.UseStreamingPtc.Value = UseStreamingPtc;
            _gameConfig.System.MemoryManagerMode.Value = (MemoryManagerMode)MemoryMode;
            _gameConfig.System.MemoryConfiguration.Value = (MemoryConfiguration)MemoryConfiguration;
            _gameConfig.System.UseHypervisor.Value = UseHypervisor;
            _gameConfig.System.UseSparseAddressTable.Value = UseSparseAddressTable;
            _gameConfig.System.HleKernelThreadsCPUSet.Value = HleKernelThreadsCPUSet;
            _gameConfig.System.HleKernelThreadsCPUSetStaticCore.Value = HleKernelThreadsCPUSetStaticCore;
            _gameConfig.System.PtcBackgroundThreadsCPUSet.Value = PtcBackgroundThreadsCPUSet;
            _gameConfig.System.PtcBackgroundThreadCount.Value = PtcBackgroundThreadCount;

            // Graphics
            _gameConfig.Graphics.GraphicsBackend.Value = (GraphicsBackend)GraphicsBackendIndex;
            _gameConfig.Graphics.PreferredGpu.Value = _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            _gameConfig.Graphics.EnableShaderCache.Value = EnableShaderCache;
            _gameConfig.Graphics.EnableTextureRecompression.Value = EnableTextureRecompression;
            _gameConfig.Graphics.EnableMacroHLE.Value = EnableMacroHLE;
            _gameConfig.Graphics.EnableColorSpacePassthrough.Value = EnableColorSpacePassthrough;
            _gameConfig.Graphics.ResScale.Value = ResolutionScale == 4 ? -1 : ResolutionScale + 1;
            _gameConfig.Graphics.ResScaleCustom.Value = CustomResolutionScale;
            _gameConfig.Graphics.MaxAnisotropy.Value = MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy);
            _gameConfig.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            _gameConfig.Graphics.AntiAliasing.Value = (AntiAliasing)AntiAliasingEffect;
            _gameConfig.Graphics.ScalingFilter.Value = (ScalingFilter)ScalingFilter;
            _gameConfig.Graphics.ScalingFilterLevel.Value = ScalingFilterLevel;

            if (_gameConfig.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }

            _gameConfig.Graphics.BackendThreading.Value = (BackendThreading)GraphicsBackendMultithreadingIndex;
            _gameConfig.Graphics.ShadersDumpPath.Value = ShaderDumpPath;

            // Audio
            AudioBackend audioBackend = (AudioBackend)AudioBackend;
            if (audioBackend != _gameConfig.System.AudioBackend.Value)
            {
                _gameConfig.System.AudioBackend.Value = audioBackend;

                Logger.Info?.Print(LogClass.Application, $"AudioBackend toggled to: {audioBackend}");
            }

            _gameConfig.System.AudioVolume.Value = Volume / 100;

            // Network
            _gameConfig.System.EnableInternetAccess.Value = EnableInternetAccess;

            // Logging
            _gameConfig.System.FsGlobalAccessLogMode.Value = FsGlobalAccessLogMode;

            _gameConfig.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            _gameConfig.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;

            if (_config != null)
            {
                _config.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }
            else
            {
                _gameConfig.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            }

            MainWindow.UpdateGraphicsConfig(_gameConfig);

            SaveSettingsEvent?.Invoke();

            _directoryChanged = false;
        }

        private static void RevertIfNotSaved()
        {
            Program.ReloadConfig();
        }

        public void ApplyButton()
        {
            SaveConfiguration();
        }

        public void OkButton()
        {
            SaveConfiguration();
            CloseWindow?.Invoke();
        }

        public void CancelButton()
        {
            RevertIfNotSaved();
            CloseWindow?.Invoke();
        }
    }
}
