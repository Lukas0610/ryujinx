using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.UI.Common.Configuration.UI;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ryujinx.UI.Common.Configuration
{
    public class ConfigurationState
    {
        /// <summary>
        /// UI configuration section
        /// </summary>
        public class UISection
        {
            public class Columns
            {
                public ReactiveObject<bool> FavColumn { get; private set; }
                public ReactiveObject<bool> IconColumn { get; private set; }
                public ReactiveObject<bool> AppColumn { get; private set; }
                public ReactiveObject<bool> DevColumn { get; private set; }
                public ReactiveObject<bool> VersionColumn { get; private set; }
                public ReactiveObject<bool> TimePlayedColumn { get; private set; }
                public ReactiveObject<bool> LastPlayedColumn { get; private set; }
                public ReactiveObject<bool> FileExtColumn { get; private set; }
                public ReactiveObject<bool> FileSizeColumn { get; private set; }
                public ReactiveObject<bool> PathColumn { get; private set; }

                public Columns()
                {
                    FavColumn = new ReactiveObject<bool>();
                    IconColumn = new ReactiveObject<bool>();
                    AppColumn = new ReactiveObject<bool>();
                    DevColumn = new ReactiveObject<bool>();
                    VersionColumn = new ReactiveObject<bool>();
                    TimePlayedColumn = new ReactiveObject<bool>();
                    LastPlayedColumn = new ReactiveObject<bool>();
                    FileExtColumn = new ReactiveObject<bool>();
                    FileSizeColumn = new ReactiveObject<bool>();
                    PathColumn = new ReactiveObject<bool>();
                }
            }

            public class ColumnSortSettings
            {
                public ReactiveObject<int> SortColumnId { get; private set; }
                public ReactiveObject<bool> SortAscending { get; private set; }

                public ColumnSortSettings()
                {
                    SortColumnId = new ReactiveObject<int>();
                    SortAscending = new ReactiveObject<bool>();
                }
            }

            /// <summary>
            /// Used to toggle which file types are shown in the UI
            /// </summary>
            public class ShownFileTypeSettings
            {
                public ReactiveObject<bool> NSP { get; private set; }
                public ReactiveObject<bool> PFS0 { get; private set; }
                public ReactiveObject<bool> XCI { get; private set; }
                public ReactiveObject<bool> NCA { get; private set; }
                public ReactiveObject<bool> NRO { get; private set; }
                public ReactiveObject<bool> NSO { get; private set; }

                public ShownFileTypeSettings()
                {
                    NSP = new ReactiveObject<bool>();
                    PFS0 = new ReactiveObject<bool>();
                    XCI = new ReactiveObject<bool>();
                    NCA = new ReactiveObject<bool>();
                    NRO = new ReactiveObject<bool>();
                    NSO = new ReactiveObject<bool>();
                }
            }

            // <summary>
            /// Determines main window start-up position, size and state
            ///<summary>
            public class WindowStartupSettings
            {
                public ReactiveObject<int> WindowSizeWidth { get; private set; }
                public ReactiveObject<int> WindowSizeHeight { get; private set; }
                public ReactiveObject<int> WindowPositionX { get; private set; }
                public ReactiveObject<int> WindowPositionY { get; private set; }
                public ReactiveObject<bool> WindowMaximized { get; private set; }

                public WindowStartupSettings()
                {
                    WindowSizeWidth = new ReactiveObject<int>();
                    WindowSizeHeight = new ReactiveObject<int>();
                    WindowPositionX = new ReactiveObject<int>();
                    WindowPositionY = new ReactiveObject<int>();
                    WindowMaximized = new ReactiveObject<bool>();
                }
            }

            /// <summary>
            /// Used to toggle columns in the GUI
            /// </summary>
            public Columns GuiColumns { get; private set; }

            /// <summary>
            /// Used to configure column sort settings in the GUI
            /// </summary>
            public ColumnSortSettings ColumnSort { get; private set; }

            /// <summary>
            /// A list of directories containing games to be used to load games into the games list
            /// </summary>
            public ReactiveObject<List<string>> GameDirs { get; private set; }

            /// <summary>
            /// A list of file types to be hidden in the games List
            /// </summary>
            public ShownFileTypeSettings ShownFileTypes { get; private set; }

            /// <summary>
            /// Determines main window start-up position, size and state
            /// </summary>
            public WindowStartupSettings WindowStartup { get; private set; }

            /// <summary>
            /// Language Code for the UI
            /// </summary>
            public ReactiveObject<string> LanguageCode { get; private set; }

            /// <summary>
            /// Enable or disable custom themes in the GUI
            /// </summary>
            public ReactiveObject<bool> EnableCustomTheme { get; private set; }

            /// <summary>
            /// Path to custom GUI theme
            /// </summary>
            public ReactiveObject<string> CustomThemePath { get; private set; }

            /// <summary>
            /// Selects the base style
            /// </summary>
            public ReactiveObject<string> BaseStyle { get; private set; }

            /// <summary>
            /// Hide / Show Console Window
            /// </summary>
            public ReactiveObject<bool> ShowConsole { get; private set; }

            /// <summary>
            /// View Mode of the Game list
            /// </summary>
            public ReactiveObject<int> GameListViewMode { get; private set; }

            /// <summary>
            /// Show application name in Grid Mode
            /// </summary>
            public ReactiveObject<bool> ShowNames { get; private set; }

            /// <summary>
            /// Sets App Icon Size in Grid Mode
            /// </summary>
            public ReactiveObject<int> GridSize { get; private set; }

            /// <summary>
            /// Sorts Apps in Grid Mode
            /// </summary>
            public ReactiveObject<int> ApplicationSort { get; private set; }

            /// <summary>
            /// Sets if Grid is ordered in Ascending Order
            /// </summary>
            public ReactiveObject<bool> IsAscendingOrder { get; private set; }

            public UISection()
            {
                GuiColumns = new Columns();
                ColumnSort = new ColumnSortSettings();
                GameDirs = new ReactiveObject<List<string>>();
                ShownFileTypes = new ShownFileTypeSettings();
                WindowStartup = new WindowStartupSettings();
                EnableCustomTheme = new ReactiveObject<bool>();
                CustomThemePath = new ReactiveObject<string>();
                BaseStyle = new ReactiveObject<string>();
                GameListViewMode = new ReactiveObject<int>();
                ShowNames = new ReactiveObject<bool>();
                GridSize = new ReactiveObject<int>();
                ApplicationSort = new ReactiveObject<int>();
                IsAscendingOrder = new ReactiveObject<bool>();
                LanguageCode = new ReactiveObject<string>();
                ShowConsole = new ReactiveObject<bool>();
                ShowConsole.Event += static (s, e) => { ConsoleHelper.SetConsoleWindowState(e.NewValue); };
            }
        }

        /// <summary>
        /// Logger configuration section
        /// </summary>
        public class LoggerSection
        {
            /// <summary>
            /// Enables printing debug log messages
            /// </summary>
            public ReactiveObject<bool> EnableDebug { get; private set; }

            /// <summary>
            /// Enables printing stub log messages
            /// </summary>
            public ReactiveObject<bool> EnableStub { get; private set; }

            /// <summary>
            /// Enables printing info log messages
            /// </summary>
            public ReactiveObject<bool> EnableInfo { get; private set; }

            /// <summary>
            /// Enables printing warning log messages
            /// </summary>
            public ReactiveObject<bool> EnableWarn { get; private set; }

            /// <summary>
            /// Enables printing error log messages
            /// </summary>
            public ReactiveObject<bool> EnableError { get; private set; }

            /// <summary>
            /// Enables printing trace log messages
            /// </summary>
            public ReactiveObject<bool> EnableTrace { get; private set; }

            /// <summary>
            /// Enables printing guest log messages
            /// </summary>
            public ReactiveObject<bool> EnableGuest { get; private set; }

            /// <summary>
            /// Enables printing FS access log messages
            /// </summary>
            public ReactiveObject<bool> EnableFsAccessLog { get; private set; }

            /// <summary>
            /// Controls which log messages are written to the log targets
            /// </summary>
            public ReactiveObject<LogClass[]> FilteredClasses { get; private set; }

            /// <summary>
            /// Enables or disables logging to a file on disk
            /// </summary>
            public ReactiveObject<bool> EnableFileLog { get; private set; }

            /// <summary>
            /// Controls which OpenGL log messages are recorded in the log
            /// </summary>
            public ReactiveObject<GraphicsDebugLevel> GraphicsDebugLevel { get; private set; }

            public LoggerSection()
            {
                EnableDebug = new ReactiveObject<bool>();
                EnableStub = new ReactiveObject<bool>();
                EnableInfo = new ReactiveObject<bool>();
                EnableWarn = new ReactiveObject<bool>();
                EnableError = new ReactiveObject<bool>();
                EnableTrace = new ReactiveObject<bool>();
                EnableGuest = new ReactiveObject<bool>();
                EnableFsAccessLog = new ReactiveObject<bool>();
                FilteredClasses = new ReactiveObject<LogClass[]>();
                EnableFileLog = new ReactiveObject<bool>();
                EnableFileLog.Event += static (sender, e) => LogValueChange(e, nameof(EnableFileLog));
                GraphicsDebugLevel = new ReactiveObject<GraphicsDebugLevel>();
            }
        }

        /// <summary>
        /// The default configuration instance
        /// </summary>
        public static ConfigurationState Instance { get; private set; }

        /// <summary>
        /// The UI section
        /// </summary>
        public UISection UI { get; private set; }

        /// <summary>
        /// The Logger section
        /// </summary>
        public LoggerSection Logger { get; private set; }

        /// <summary>
        /// The global game configuration-state
        /// </summary>
        public GameConfigurationState Game { get; private set; }

        /// <summary>
        /// Checks for updates when Ryujinx starts when enabled
        /// </summary>
        public ReactiveObject<bool> CheckUpdatesOnStart { get; private set; }

        /// <summary>
        /// Show "Confirm Exit" Dialog
        /// </summary>
        public ReactiveObject<bool> ShowConfirmExit { get; private set; }

        /// <summary>
        /// Enables or disables save window size, position and state on close.
        /// </summary>
        public ReactiveObject<bool> RememberWindowState { get; private set; }

        /// <summary>
        /// Start games in fullscreen mode
        /// </summary>
        public ReactiveObject<bool> StartFullscreen { get; private set; }

        /// <summary>
        /// Enables or disables Discord Rich Presence
        /// </summary>
        public ReactiveObject<bool> EnableDiscordIntegration { get; private set; }

        /// <summary>
        /// Enables hardware-accelerated rendering for Avalonia
        /// </summary>
        public ReactiveObject<bool> EnableHardwareAcceleration { get; private set; }

        private ConfigurationState()
        {
            UI = new UISection();
            Logger = new LoggerSection();
            CheckUpdatesOnStart = new ReactiveObject<bool>();
            ShowConfirmExit = new ReactiveObject<bool>();
            RememberWindowState = new ReactiveObject<bool>();
            StartFullscreen = new ReactiveObject<bool>();
            EnableDiscordIntegration = new ReactiveObject<bool>();
            EnableHardwareAcceleration = new ReactiveObject<bool>();
        }

        public ConfigurationFileFormat ToFileFormat()
        {
            ConfigurationFileFormat configurationFile = new()
            {
                Version = ConfigurationFileFormat.CurrentVersion,
                Game = Game.ToFileFormat(),
                EnableFileLog = Logger.EnableFileLog,
                LoggingEnableDebug = Logger.EnableDebug,
                LoggingEnableStub = Logger.EnableStub,
                LoggingEnableInfo = Logger.EnableInfo,
                LoggingEnableWarn = Logger.EnableWarn,
                LoggingEnableError = Logger.EnableError,
                LoggingEnableTrace = Logger.EnableTrace,
                LoggingEnableGuest = Logger.EnableGuest,
                LoggingEnableFsAccessLog = Logger.EnableFsAccessLog,
                LoggingFilteredClasses = Logger.FilteredClasses,
                LoggingGraphicsDebugLevel = Logger.GraphicsDebugLevel,
                CheckUpdatesOnStart = CheckUpdatesOnStart,
                ShowConfirmExit = ShowConfirmExit,
                RememberWindowState = RememberWindowState,
                StartFullscreen = StartFullscreen,
                EnableDiscordIntegration = EnableDiscordIntegration,
                EnableHardwareAcceleration = EnableHardwareAcceleration,
                GuiColumns = new GuiColumns
                {
                    FavColumn = UI.GuiColumns.FavColumn,
                    IconColumn = UI.GuiColumns.IconColumn,
                    AppColumn = UI.GuiColumns.AppColumn,
                    DevColumn = UI.GuiColumns.DevColumn,
                    VersionColumn = UI.GuiColumns.VersionColumn,
                    TimePlayedColumn = UI.GuiColumns.TimePlayedColumn,
                    LastPlayedColumn = UI.GuiColumns.LastPlayedColumn,
                    FileExtColumn = UI.GuiColumns.FileExtColumn,
                    FileSizeColumn = UI.GuiColumns.FileSizeColumn,
                    PathColumn = UI.GuiColumns.PathColumn,
                },
                ColumnSort = new ColumnSort
                {
                    SortColumnId = UI.ColumnSort.SortColumnId,
                    SortAscending = UI.ColumnSort.SortAscending,
                },
                GameDirs = UI.GameDirs,
                ShownFileTypes = new ShownFileTypes
                {
                    NSP = UI.ShownFileTypes.NSP,
                    PFS0 = UI.ShownFileTypes.PFS0,
                    XCI = UI.ShownFileTypes.XCI,
                    NCA = UI.ShownFileTypes.NCA,
                    NRO = UI.ShownFileTypes.NRO,
                    NSO = UI.ShownFileTypes.NSO,
                },
                WindowStartup = new WindowStartup
                {
                    WindowSizeWidth = UI.WindowStartup.WindowSizeWidth,
                    WindowSizeHeight = UI.WindowStartup.WindowSizeHeight,
                    WindowPositionX = UI.WindowStartup.WindowPositionX,
                    WindowPositionY = UI.WindowStartup.WindowPositionY,
                    WindowMaximized = UI.WindowStartup.WindowMaximized,
                },
                LanguageCode = UI.LanguageCode,
                EnableCustomTheme = UI.EnableCustomTheme,
                CustomThemePath = UI.CustomThemePath,
                BaseStyle = UI.BaseStyle,
                GameListViewMode = UI.GameListViewMode,
                ShowNames = UI.ShowNames,
                GridSize = UI.GridSize,
                ApplicationSort = UI.ApplicationSort,
                IsAscendingOrder = UI.IsAscendingOrder,
                ShowConsole = UI.ShowConsole,
            };

            return configurationFile;
        }

        public void LoadDefault()
        {
            Game.LoadDefault();

            Logger.EnableFileLog.Value = true;
            Logger.EnableDebug.Value = false;
            Logger.EnableStub.Value = true;
            Logger.EnableInfo.Value = true;
            Logger.EnableWarn.Value = true;
            Logger.EnableError.Value = true;
            Logger.EnableTrace.Value = false;
            Logger.EnableGuest.Value = true;
            Logger.EnableFsAccessLog.Value = false;
            Logger.FilteredClasses.Value = Array.Empty<LogClass>();
            Logger.GraphicsDebugLevel.Value = GraphicsDebugLevel.None;
            CheckUpdatesOnStart.Value = true;
            ShowConfirmExit.Value = true;
            RememberWindowState.Value = true;
            StartFullscreen.Value = false;
            EnableDiscordIntegration.Value = true;
            EnableHardwareAcceleration.Value = true;
            UI.GuiColumns.FavColumn.Value = true;
            UI.GuiColumns.IconColumn.Value = true;
            UI.GuiColumns.AppColumn.Value = true;
            UI.GuiColumns.DevColumn.Value = true;
            UI.GuiColumns.VersionColumn.Value = true;
            UI.GuiColumns.TimePlayedColumn.Value = true;
            UI.GuiColumns.LastPlayedColumn.Value = true;
            UI.GuiColumns.FileExtColumn.Value = true;
            UI.GuiColumns.FileSizeColumn.Value = true;
            UI.GuiColumns.PathColumn.Value = true;
            UI.ColumnSort.SortColumnId.Value = 0;
            UI.ColumnSort.SortAscending.Value = false;
            UI.GameDirs.Value = new List<string>();
            UI.ShownFileTypes.NSP.Value = true;
            UI.ShownFileTypes.PFS0.Value = true;
            UI.ShownFileTypes.XCI.Value = true;
            UI.ShownFileTypes.NCA.Value = true;
            UI.ShownFileTypes.NRO.Value = true;
            UI.ShownFileTypes.NSO.Value = true;
            UI.EnableCustomTheme.Value = true;
            UI.LanguageCode.Value = "en_US";
            UI.CustomThemePath.Value = "";
            UI.BaseStyle.Value = "Dark";
            UI.GameListViewMode.Value = 0;
            UI.ShowNames.Value = true;
            UI.GridSize.Value = 2;
            UI.ApplicationSort.Value = 0;
            UI.IsAscendingOrder.Value = true;
            UI.ShowConsole.Value = true;
            UI.WindowStartup.WindowSizeWidth.Value = 1280;
            UI.WindowStartup.WindowSizeHeight.Value = 760;
            UI.WindowStartup.WindowPositionX.Value = 0;
            UI.WindowStartup.WindowPositionY.Value = 0;
            UI.WindowStartup.WindowMaximized.Value = false;
        }

        public void Load(ConfigurationFileFormat configurationFileFormat, string configurationFilePath)
        {
            bool configurationFileUpdated = false;

            if (configurationFileFormat.Version < 56)
            {
                Ryujinx.Common.Logging.Logger.Warning?.Print(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 56.");

                if (GameConfigurationFileFormat.TryLoad(configurationFilePath, out GameConfigurationFileFormat gameConfigurationFileFormat))
                {
                    Game = GameConfigurationState.Global();
                    Game.Load(gameConfigurationFileFormat);
                }

                configurationFileUpdated = true;
            }

            Game = GameConfigurationState.Global();
            Game.Load(configurationFileFormat.Game);

            Logger.EnableFileLog.Value = configurationFileFormat.EnableFileLog;
            Logger.EnableDebug.Value = configurationFileFormat.LoggingEnableDebug;
            Logger.EnableStub.Value = configurationFileFormat.LoggingEnableStub;
            Logger.EnableInfo.Value = configurationFileFormat.LoggingEnableInfo;
            Logger.EnableWarn.Value = configurationFileFormat.LoggingEnableWarn;
            Logger.EnableError.Value = configurationFileFormat.LoggingEnableError;
            Logger.EnableTrace.Value = configurationFileFormat.LoggingEnableTrace;
            Logger.EnableGuest.Value = configurationFileFormat.LoggingEnableGuest;
            Logger.EnableFsAccessLog.Value = configurationFileFormat.LoggingEnableFsAccessLog;
            Logger.FilteredClasses.Value = configurationFileFormat.LoggingFilteredClasses;
            Logger.GraphicsDebugLevel.Value = configurationFileFormat.LoggingGraphicsDebugLevel;
            CheckUpdatesOnStart.Value = configurationFileFormat.CheckUpdatesOnStart;
            ShowConfirmExit.Value = configurationFileFormat.ShowConfirmExit;
            RememberWindowState.Value = configurationFileFormat.RememberWindowState;
            StartFullscreen.Value = configurationFileFormat.StartFullscreen;
            EnableDiscordIntegration.Value = configurationFileFormat.EnableDiscordIntegration;
            EnableHardwareAcceleration.Value = configurationFileFormat.EnableHardwareAcceleration;
            UI.GuiColumns.FavColumn.Value = configurationFileFormat.GuiColumns.FavColumn;
            UI.GuiColumns.IconColumn.Value = configurationFileFormat.GuiColumns.IconColumn;
            UI.GuiColumns.AppColumn.Value = configurationFileFormat.GuiColumns.AppColumn;
            UI.GuiColumns.DevColumn.Value = configurationFileFormat.GuiColumns.DevColumn;
            UI.GuiColumns.VersionColumn.Value = configurationFileFormat.GuiColumns.VersionColumn;
            UI.GuiColumns.TimePlayedColumn.Value = configurationFileFormat.GuiColumns.TimePlayedColumn;
            UI.GuiColumns.LastPlayedColumn.Value = configurationFileFormat.GuiColumns.LastPlayedColumn;
            UI.GuiColumns.FileExtColumn.Value = configurationFileFormat.GuiColumns.FileExtColumn;
            UI.GuiColumns.FileSizeColumn.Value = configurationFileFormat.GuiColumns.FileSizeColumn;
            UI.GuiColumns.PathColumn.Value = configurationFileFormat.GuiColumns.PathColumn;
            UI.ColumnSort.SortColumnId.Value = configurationFileFormat.ColumnSort.SortColumnId;
            UI.ColumnSort.SortAscending.Value = configurationFileFormat.ColumnSort.SortAscending;
            UI.GameDirs.Value = configurationFileFormat.GameDirs;
            UI.ShownFileTypes.NSP.Value = configurationFileFormat.ShownFileTypes.NSP;
            UI.ShownFileTypes.PFS0.Value = configurationFileFormat.ShownFileTypes.PFS0;
            UI.ShownFileTypes.XCI.Value = configurationFileFormat.ShownFileTypes.XCI;
            UI.ShownFileTypes.NCA.Value = configurationFileFormat.ShownFileTypes.NCA;
            UI.ShownFileTypes.NRO.Value = configurationFileFormat.ShownFileTypes.NRO;
            UI.ShownFileTypes.NSO.Value = configurationFileFormat.ShownFileTypes.NSO;
            UI.EnableCustomTheme.Value = configurationFileFormat.EnableCustomTheme;
            UI.LanguageCode.Value = configurationFileFormat.LanguageCode;
            UI.CustomThemePath.Value = configurationFileFormat.CustomThemePath;
            UI.BaseStyle.Value = configurationFileFormat.BaseStyle;
            UI.GameListViewMode.Value = configurationFileFormat.GameListViewMode;
            UI.ShowNames.Value = configurationFileFormat.ShowNames;
            UI.IsAscendingOrder.Value = configurationFileFormat.IsAscendingOrder;
            UI.GridSize.Value = configurationFileFormat.GridSize;
            UI.ApplicationSort.Value = configurationFileFormat.ApplicationSort;
            UI.ShowConsole.Value = configurationFileFormat.ShowConsole;
            UI.WindowStartup.WindowSizeWidth.Value = configurationFileFormat.WindowStartup.WindowSizeWidth;
            UI.WindowStartup.WindowSizeHeight.Value = configurationFileFormat.WindowStartup.WindowSizeHeight;
            UI.WindowStartup.WindowPositionX.Value = configurationFileFormat.WindowStartup.WindowPositionX;
            UI.WindowStartup.WindowPositionY.Value = configurationFileFormat.WindowStartup.WindowPositionY;
            UI.WindowStartup.WindowMaximized.Value = configurationFileFormat.WindowStartup.WindowMaximized;

            if (configurationFileUpdated)
            {
                ToFileFormat().SaveConfig(configurationFilePath);

                Ryujinx.Common.Logging.Logger.Notice.Print(LogClass.Application, $"Configuration file updated to version {ConfigurationFileFormat.CurrentVersion}");
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

        private static void LogValueChange<T>(ReactiveEventArgs<T> eventArgs, string valueName)
        {
            string message = string.Create(CultureInfo.InvariantCulture, $"{valueName} set to: {eventArgs.NewValue}");

            Ryujinx.Common.Logging.Logger.Info?.Print(LogClass.Configuration, message);
        }

        public static void Initialize()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Configuration is already initialized");
            }

            Instance = new ConfigurationState();
        }
    }
}
