using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Configuration.UI;
using System.Collections.Generic;

namespace Ryujinx.UI.Common.Configuration
{
    public class ConfigurationFileFormat
    {
        /// <summary>
        /// The current version of the file format
        /// </summary>
        public const int CurrentVersion = 56;

        /// <summary>
        /// Version of the configuration file format
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Global game-configuration
        /// </summary>
        public GameConfigurationFileFormat Game { get; set; }

        /// <summary>
        /// Enables or disables logging to a file on disk
        /// </summary>
        public bool EnableFileLog { get; set; }

        /// <summary>
        /// Enables printing debug log messages
        /// </summary>
        public bool LoggingEnableDebug { get; set; }

        /// <summary>
        /// Enables printing stub log messages
        /// </summary>
        public bool LoggingEnableStub { get; set; }

        /// <summary>
        /// Enables printing info log messages
        /// </summary>
        public bool LoggingEnableInfo { get; set; }

        /// <summary>
        /// Enables printing warning log messages
        /// </summary>
        public bool LoggingEnableWarn { get; set; }

        /// <summary>
        /// Enables printing error log messages
        /// </summary>
        public bool LoggingEnableError { get; set; }

        /// <summary>
        /// Enables printing trace log messages
        /// </summary>
        public bool LoggingEnableTrace { get; set; }

        /// <summary>
        /// Enables printing guest log messages
        /// </summary>
        public bool LoggingEnableGuest { get; set; }

        /// <summary>
        /// Enables printing FS access log messages
        /// </summary>
        public bool LoggingEnableFsAccessLog { get; set; }

        /// <summary>
        /// Enables FS access log output to the console. Possible modes are 0-3
        /// </summary>
        public int FsGlobalAccessLogMode { get; set; }

        /// <summary>
        /// Controls which log messages are written to the log targets
        /// </summary>
        public LogClass[] LoggingFilteredClasses { get; set; }

        /// <summary>
        /// Change Graphics API debug log level
        /// </summary>
        public GraphicsDebugLevel LoggingGraphicsDebugLevel { get; set; }

        /// <summary>
        /// Checks for updates when Ryujinx starts when enabled
        /// </summary>
        public bool CheckUpdatesOnStart { get; set; }

        /// <summary>
        /// Show "Confirm Exit" Dialog
        /// </summary>
        public bool ShowConfirmExit { get; set; }

        /// <summary>
        /// Enables or disables save window size, position and state on close.
        /// </summary>
        public bool RememberWindowState { get; set; }

        /// <summary>
        /// Start games in fullscreen mode
        /// </summary>
        public bool StartFullscreen { get; set; }

        /// <summary>
        /// Enables or disables Discord Rich Presence
        /// </summary>
        public bool EnableDiscordIntegration { get; set; }

        /// <summary>
        /// Enables hardware-accelerated rendering for Avalonia
        /// </summary>
        public bool EnableHardwareAcceleration { get; set; }

        /// <summary>
        /// Used to toggle columns in the GUI
        /// </summary>
        public GuiColumns GuiColumns { get; set; }

        /// <summary>
        /// Used to configure column sort settings in the GUI
        /// </summary>
        public ColumnSort ColumnSort { get; set; }

        /// <summary>
        /// A list of directories containing games to be used to load games into the games list
        /// </summary>
        public List<string> GameDirs { get; set; }

        /// <summary>
        /// A list of file types to be hidden in the games List
        /// </summary>
        public ShownFileTypes ShownFileTypes { get; set; }

        /// <summary>
        /// Main window start-up position, size and state
        /// </summary>
        public WindowStartup WindowStartup { get; set; }

        /// <summary>
        /// Language Code for the UI
        /// </summary>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Enable or disable custom themes in the GUI
        /// </summary>
        public bool EnableCustomTheme { get; set; }

        /// <summary>
        /// Path to custom GUI theme
        /// </summary>
        public string CustomThemePath { get; set; }

        /// <summary>
        /// Chooses the base style // Not Used
        /// </summary>
        public string BaseStyle { get; set; }

        /// <summary>
        /// Chooses the view mode of the game list // Not Used
        /// </summary>
        public int GameListViewMode { get; set; }

        /// <summary>
        /// Show application name in Grid Mode // Not Used
        /// </summary>
        public bool ShowNames { get; set; }

        /// <summary>
        /// Sets App Icon Size // Not Used
        /// </summary>
        public int GridSize { get; set; }

        /// <summary>
        /// Sorts Apps in the game list // Not Used
        /// </summary>
        public int ApplicationSort { get; set; }

        /// <summary>
        /// Sets if Grid is ordered in Ascending Order // Not Used
        /// </summary>
        public bool IsAscendingOrder { get; set; }

        /// <summary>
        /// Show console window
        /// </summary>
        public bool ShowConsole { get; set; }

        /// <summary>
        /// Loads a configuration file from disk
        /// </summary>
        /// <param name="path">The path to the JSON configuration file</param>
        /// <param name="configurationFileFormat">Parsed configuration file</param>
        public static bool TryLoad(string path, out ConfigurationFileFormat configurationFileFormat)
        {
            try
            {
                configurationFileFormat = JsonHelper.DeserializeFromFile(path, ConfigurationFileFormatSettings.SerializerContext.ConfigurationFileFormat);

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
            JsonHelper.SerializeToFile(path, this, ConfigurationFileFormatSettings.SerializerContext.ConfigurationFileFormat);
        }
    }
}
