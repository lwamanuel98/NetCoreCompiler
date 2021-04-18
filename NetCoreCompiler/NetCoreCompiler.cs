using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;

namespace NetCoreCompiler
{
    public partial class NetCoreCompiler : ServiceBase
    {
        FileSystemWatcher watcher = null;
        string fileChanged = "";
        public Process buildProcess = null;
        public class BuildFile
        {
            public string WebsiteName { get; set; }
            public string ApplicationPoolName { get; set; }
            public string BuildDirectory { get; set; }
            public string Location { get; set; }
        }
        public NetCoreCompiler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length >= 1)
            {
                if (args[1] != "")
                    Program.WATCH_DIRECTORY = args[1];
            }

            Console.WriteLine("Watcher starting... \r\nDir to watch = " + Program.WATCH_DIRECTORY);

            watcher = new FileSystemWatcher(Program.WATCH_DIRECTORY);

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


            Console.WriteLine("Watcher started...");


        }

        public Process RunCMD(string cmd, string workingDir, out string result, out string error)
        {
            ProcessStartInfo psiDotNetRestore = new ProcessStartInfo(@"C:\Windows\System32\cmd.exe", "/c " + cmd);
            psiDotNetRestore.WindowStyle = ProcessWindowStyle.Maximized;
            psiDotNetRestore.WorkingDirectory = workingDir;

            psiDotNetRestore.RedirectStandardOutput = true;
            psiDotNetRestore.RedirectStandardError = true;
            psiDotNetRestore.UseShellExecute = false;
            psiDotNetRestore.CreateNoWindow = true;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = psiDotNetRestore;
            proc.Start();

            result = proc.StandardOutput.ReadToEnd();
            error = proc.StandardError.ReadToEnd();

            buildProcess = proc;

            return proc;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.FullPath).Equals(""))
                return;

            try
            {
                Console.WriteLine($"Changed: {e.FullPath}" + "\r\n");

                fileChanged = e.FullPath;

                TriggerBuild();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }



        public BuildFile GetBuildFile(string fileChanged)
        {
            string fileName = Path.GetFileName(fileChanged);
            string path = Path.GetDirectoryName(fileChanged).Replace("\\", "/");
            string watchDir = Program.WATCH_DIRECTORY.Replace("\\", "/");

            string file = Directory.EnumerateFiles(path).Where(x => x.EndsWith("settings.ncc")).FirstOrDefault();
            while (file == null)
            {
                path = Path.GetFullPath(Path.Combine(path, @"../")).Replace("\\", "/");

                file = Directory.EnumerateFiles(path).Where(x => x.EndsWith("settings.ncc")).FirstOrDefault();

                if (file == null && (watchDir == path || path == watchDir + "/"))
                    break;
            }

            if (file == null)
                return null;

            BuildFile bf = new BuildFile();

            try
            {
                using (StreamReader sr = new StreamReader(File.OpenRead(file)))
                {
                    bf = Newtonsoft.Json.JsonConvert.DeserializeObject<BuildFile>(sr.ReadToEnd());
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to parse build file!");
                return null;
            }
            if (bf == null)
            {
                Console.WriteLine("Failed to parse build file!");
                return null;
            }
            bf.Location = path;

            return bf;
        }
        private void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            sourcePath = sourcePath.Replace("/", "\\");
            targetPath = targetPath.Replace("/", "\\");

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                if (!Directory.Exists(dirPath.Replace(sourcePath, targetPath)))
                {
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
                }
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public void TriggerBuild()
        {
            var buildFile = GetBuildFile(fileChanged);
            if (buildFile == null)
            {
                Console.WriteLine("No build settings file found.");
                return;
            }
            if (fileChanged.Replace("\\", "/").Contains(buildFile.BuildDirectory.Replace("\\", "/")))
            {
                Console.WriteLine("Created by build!");
                return;
            }
            Console.WriteLine("Build file found!");

            if (buildProcess != null && !buildProcess.HasExited)
            {
                Console.WriteLine("Other build cancelled!");
                buildProcess.Kill();
                buildProcess = null;
            }

            try
            {
                string result, error;

                var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(buildFile.ApplicationPoolName));
                if (appPool == null)
                {
                    Console.WriteLine("app pool is null");
                    return;
                }

                string buildFolder = Path.Combine(Program.WebsiteTemp(buildFile.WebsiteName), "website_build");


                DirectoryInfo dir = new DirectoryInfo(buildFolder);
                if (dir.Exists)
                    dir.Delete(true);




                Console.WriteLine("Building...");
                RunCMD(string.Format("dotnet publish -c Release -o \"{0}\"", buildFolder), buildFile.Location, out result, out error);

                Console.Write(result);

                if (buildProcess == null) // has been killed - new build will have started
                {
                    Console.WriteLine("Build cancelled!");

                    return;
                }

                Console.WriteLine("Build complete!");

                watcher.EnableRaisingEvents = false;

                foreach (var wp in appPool.WorkerProcesses)
                {
                    Process.GetProcessById(wp.ProcessId).Kill();
                }

                if (appPool.State != ObjectState.Stopped)
                {
                    appPool.Stop();
                    Console.WriteLine("Stopped app pool!");
                }

                Console.WriteLine("Copying build directory!");

                CopyFilesRecursively(buildFolder, buildFile.BuildDirectory);

                Console.WriteLine("Copied!");
                watcher.EnableRaisingEvents = true;

                appPool.Start();

                Console.WriteLine("application pool started!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private static void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private static void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}" + "\r\n" +
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
