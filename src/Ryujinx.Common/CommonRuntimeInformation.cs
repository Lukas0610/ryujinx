using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryujinx.Common
{

    public static class CommonRuntimeInformation
    {

        public static string ApplicationDirectory { get; set; }

        public static string ApplicationRuntimesDirectory { get; set; }

        public static string ApplicationNativeRuntimesDirectory { get; set; }

        static CommonRuntimeInformation()
        {
            ApplicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ApplicationRuntimesDirectory = Path.Combine(ApplicationDirectory, "runtimes");
            ApplicationNativeRuntimesDirectory = GetNativeRuntimesDirectory(ApplicationDirectory);
        }

        public static string GetNativeRuntimesDirectory(string directory)
        {
            return Path.Combine(directory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
        }

    }

}
