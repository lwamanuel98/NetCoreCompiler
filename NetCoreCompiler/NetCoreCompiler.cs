using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreCompiler
{
    public partial class NetCoreCompiler : ServiceBase
    {
        public static string WebsiteTemp
        {
            get
            {
                string dir = Path.Combine(TempDirectory, Program.WEBSITE_NAME);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static string SettingsFile
        {
            get
            {
                return Path.Combine(WebsiteTemp, "ncc.settings");
            }
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
        FileSystemWatcher watcher = null;
        public NetCoreCompiler()
        {
            InitializeComponent();
        }

        public static Dictionary<string, string> Settings
        {
            get
            {
                if (File.Exists(SettingsFile))
                {
                    using (StreamReader sr = new StreamReader(File.OpenRead(SettingsFile)))
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(sr.ReadToEnd());
                    }
                }
                else
                {
                    Dictionary<string, string> defaults = new Dictionary<string, string>();
                    defaults.Add("current_switch", "0");
                    defaults.Add("switch_folder_0", "www");
                    defaults.Add("switch_folder_1", "www1");

                    File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));

                    return defaults;
                }
            }
            set
            {

                Dictionary<string, string> defaults = value;

                File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));
            }
        }
        public static int CurrentSwitch
        {
            get
            {
                return int.Parse(Settings["current_switch"]);
            }
            set
            {
                Dictionary<string, string> settings = Settings;
                settings["current_switch"] = value.ToString();
                Settings = settings;
            }
        }
        public static string CurrentSwitchFolder
        {
            get
            {
                return Path.Combine(WebsiteTemp, Settings["switch_folder_" + CurrentSwitch]);
            }
        }

        public void NextSwitchFolder()
        {
            if (CurrentSwitch == 0)
                CurrentSwitch = 1;
            else if (CurrentSwitch == 1)
                CurrentSwitch = 0;
        }



        protected override void OnStart(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length >= 4)
            {
                if (args[1] != "")
                    Program.BUILD_DIRECTORY = args[1];
                if (args[2] != "")
                    Program.WEBSITE_NAME = args[2];
                if (args[3] != "")
                    Program.POOL_NAME = args[3];
                if (args[4] != "")
                    Program.APPLICATION_PATH = args[4];

                if (Program.POOL_NAME == "")
                    Program.POOL_NAME = Program.WEBSITE_NAME;
            }

            Program.logToFile("Watcher starting... \r\nDir to watch = " + Program.BUILD_DIRECTORY + "\r\nSiteName: " + Program.WEBSITE_NAME);

            watcher = new FileSystemWatcher(Program.BUILD_DIRECTORY);

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Error += OnError;

            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;


            Program.logToFile("Watcher started...");
        }

        public void RunCMD(string cmd, out string result, out string error)
        {

            ProcessStartInfo psiDotNetRestore = new ProcessStartInfo(@"C:\Windows\System32\cmd.exe", "/c " + cmd);
            psiDotNetRestore.WindowStyle = ProcessWindowStyle.Maximized;
            psiDotNetRestore.WorkingDirectory = Program.BUILD_DIRECTORY;

            psiDotNetRestore.RedirectStandardOutput = true;
            psiDotNetRestore.RedirectStandardError = true;
            psiDotNetRestore.UseShellExecute = false;
            psiDotNetRestore.CreateNoWindow = true;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = psiDotNetRestore;
            proc.Start();

            proc.WaitForExit();

            result = proc.StandardOutput.ReadToEnd();
            error = proc.StandardError.ReadToEnd();
        }
        public bool building = false;

        public void stopAppPool(string poolName)
        {
            try
            {
                var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(poolName));
                if (appPool == null)
                    return;
                appPool.Stop();
            }
            catch (Exception)
            {

            }
        }
        public void doIisSwitch(string websiteName, string location)
        {
            try
            {
                var serverManager = new ServerManager();
                var website = serverManager.Sites.FirstOrDefault(ap => ap.Name.Equals(websiteName));
                if (website == null)
                    return;
                website.Applications[Program.APPLICATION_PATH].VirtualDirectories["/"].PhysicalPath = location;

                serverManager.CommitChanges();

            }
            catch (Exception)
            {

            }

        }
        public void startAppPool(string poolName)
        {
            try
            {
                var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(poolName));
                if (appPool == null)
                    return;
                appPool.Start();
            }
            catch (Exception)
            {

            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (building)
                    return;

                Program.logToFile($"Changed: {e.FullPath}");

                string result, error;

                //Program.logToFile("Attempting to stop website...");

                //stopAppPool(Program.SITE_NAME);

                Program.logToFile("Initializing Build/Publish...");

                building = true;
                watcher.EnableRaisingEvents = false;

                var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(Program.POOL_NAME));
                if (appPool == null)
                    return;

                string oldSwitchFolder = CurrentSwitchFolder;

                NextSwitchFolder();

                string buildFolder = CurrentSwitchFolder;


                Program.logToFile("Building project to: (" + buildFolder + ")...");

                RunCMD(string.Format("dotnet publish -c Release -o \"{0}\"", buildFolder), out result, out error);

                Program.logToFile(result);
                Program.logToFile(error);

                Program.logToFile("Publish finished see result in next entry (" + buildFolder + ")...");

                Program.logToFile("Perform the IIS switch to: " + buildFolder);

                doIisSwitch(Program.WEBSITE_NAME, buildFolder);

                Program.logToFile("Running iis reset...");

                appPool.Stop();

                while (appPool.State != ObjectState.Stopped)
                {
                    appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(Program.POOL_NAME));
                    if (appPool == null)
                        return;
                }

                appPool.Start();

                Program.logToFile(result);
                Program.logToFile(error);

                Program.logToFile("Recyle the app pool to clear any use of the old switch files");

                try
                {
                    System.IO.DirectoryInfo di = new DirectoryInfo(oldSwitchFolder);

                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
                catch (Exception ex)
                {
                    Program.logToFile("Failed to clear old switch");
                    Program.logToFile(ex.Message);
                }

                //startAppPool(Program.POOL_NAME);

                building = false;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Program.logToFile(ex.Message);
            }

        }
        private static void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private static void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Program.logToFile($"Message: {ex.Message}" + "\r\n" +
                    "Stacktrace:" + "\r\n" +
                    ex.StackTrace + "\r\n");

                PrintException(ex.InnerException);
            }
        }

        protected override void OnStop()
        {
        }
    }
}
