using Microsoft.Web.Administration;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NetCoreCompiler
{
    public static class Variables
    {
        public static string WATCH_DIRECTORY = "";
        public static FileSystemWatcher watcher = null;
        public static string fileChanged = "";
        public static Process buildProcess = null;

        public static string resultCMD, errorCMD;
        public static Thread MainThread = null;
        public static Thread ProcessThread = null;
        public static ApplicationPool currentBuildAppPool;
        public static BuildFile currentBuildFile;

        public delegate void ProcessComplete(Process proc, string result, string error);

        public class BuildFile
        {
            public string WebsiteName { get; set; }
            public string ApplicationPoolName { get; set; }
            public string BuildDirectory { get; set; }
            public string Location { get; set; }
        }
        public static string TempDirectory
        {
            get
            {
                string dir = Path.Combine(Path.GetTempPath(), "netCoreCompiler");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string WebsiteTemp(string websiteName)
        {
            string dir = Path.Combine(TempDirectory, websiteName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;

        }

    }
}
