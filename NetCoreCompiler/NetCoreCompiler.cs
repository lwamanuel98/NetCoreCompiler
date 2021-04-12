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
        FileSystemWatcher watcher = null;
        public NetCoreCompiler()
        {
            InitializeComponent();
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
                if(args[5] != "")
                    Program.DEST_DIRECTORY = args[5];


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
            string log = "";
            try
            {
                if (building)
                    return;

                log += ($"Changed: {e.FullPath}") + "\r\n";

                string result, error;

                //Program.logToFile("Attempting to stop website...");

                //stopAppPool(Program.SITE_NAME);

                log += ("Initializing Build/Publish...") + "\r\n";

                building = true;
                watcher.EnableRaisingEvents = false;

                var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(Program.POOL_NAME));
                if (appPool == null)
                    return;

                string oldSwitchFolder = Program.CurrentSwitchFolder;

                Program.NextSwitchFolder();

                string buildFolder = Program.CurrentSwitchFolder;

                log += ("Building project to: (" + buildFolder + ")...") + "\r\n";

                RunCMD(string.Format("dotnet publish -c Release -o \"{0}\"", buildFolder), out result, out error);

                log += ("Publish finished see result in next entry (" + buildFolder + ")...") + "\r\n";

                log +=(result) + "\r\n";
                log += (error) + "\r\n";

                log += ("Perform the IIS switch to: " + buildFolder) + "\r\n";
                log += ("Complete!") + "\r\n";

                doIisSwitch(Program.WEBSITE_NAME, buildFolder);


                /*Program.logToFile("Running iis reset...");                
                // stop
                appPool.Stop();

                // wait until stopped
                while (appPool.State != ObjectState.Stopped)
                {
                    // re get state
                    appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(Program.POOL_NAME));
                    if (appPool == null)
                        return;
                }

                // start again
                appPool.Start();

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
                }*/

                //startAppPool(Program.POOL_NAME);

                building = false;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Program.logToFile(ex.Message);
            }

            Program.logToFile(log);
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
