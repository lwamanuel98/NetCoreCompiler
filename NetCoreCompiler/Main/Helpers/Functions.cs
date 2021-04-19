using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace NetCoreCompiler
{
    public static class Functions
    {
        public static void CopyFilesRecursively(string sourcePath, string targetPath)
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
        public static Variables.BuildFile GetBuildFile(string fileChanged)
        {
            string fileName = Path.GetFileName(fileChanged);
            string path = Path.GetDirectoryName(fileChanged).Replace("\\", "/");
            string watchDir = Variables.WATCH_DIRECTORY.Replace("\\", "/");

            string file = Directory.GetFiles(path).Where(x => x.EndsWith("settings.ncc")).FirstOrDefault();
            while (file == null)
            {
                path = Path.GetFullPath(Path.Combine(path, @"../")).Replace("\\", "/");

                file = Directory.GetFiles(path).Where(x => x.EndsWith("settings.ncc")).FirstOrDefault();

                if (file == null && (watchDir == path || path == watchDir + "/"))
                    break;
            }

            if (file == null)
                return null;

            Variables.BuildFile bf = new Variables.BuildFile();

            try
            {
                using (StreamReader sr = new StreamReader(File.OpenRead(file)))
                {
                    bf = Newtonsoft.Json.JsonConvert.DeserializeObject<Variables.BuildFile>(sr.ReadToEnd());
                }
            }
            catch (Exception)
            {
                Logging.TransmitLog("Failed to parse build file!", null);
                return null;
            }
            if (bf == null)
            {
                Logging.TransmitLog("Failed to parse build file!", null);
                return null;
            }
            bf.Location = path;

            return bf;
        }

        public static void RunCMD(string cmd, string workingDir, Variables.ProcessComplete callback)
        {

            ThreadStart ths = new ThreadStart(() => {
                Variables.buildProcess = new Process();

                Variables.resultCMD = "";
                Variables.errorCMD = "";

                ProcessStartInfo psiDotNetRestore = new ProcessStartInfo(@"C:\Windows\System32\cmd.exe", "/c " + cmd);
                psiDotNetRestore.WindowStyle = ProcessWindowStyle.Maximized;
                psiDotNetRestore.WorkingDirectory = workingDir;

                psiDotNetRestore.RedirectStandardOutput = true;
                psiDotNetRestore.RedirectStandardError = true;
                psiDotNetRestore.UseShellExecute = false;
                psiDotNetRestore.CreateNoWindow = true;


                Variables.buildProcess.EnableRaisingEvents = true;

                Variables.buildProcess.OutputDataReceived += Proc_OutputDataReceived;
                Variables.buildProcess.ErrorDataReceived += Proc_ErrorDataReceived;

                Variables.buildProcess.StartInfo = psiDotNetRestore;

                Variables.buildProcess.Start();
                Variables.buildProcess.BeginOutputReadLine();
                Variables.buildProcess.BeginErrorReadLine();

                Variables.buildProcess.WaitForExit();

                callback.Invoke(Variables.buildProcess, Variables.resultCMD, Variables.errorCMD);
            });
            
            Variables.ProcessThread = new Thread(ths);
            Variables.ProcessThread.Start();
        }

        private static void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Logging.TransmitLog(e.Data, Variables.currentBuildFile.WebsiteName);
            Variables.errorCMD += e.Data + Environment.NewLine;
        }

        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Logging.TransmitLog(e.Data, Variables.currentBuildFile.WebsiteName);
            Variables.resultCMD += e.Data + Environment.NewLine;
        }
        public static Variables.BuildFile CanBuild()
        {
            if (Variables.fileChanged.Replace("\\", "/").Contains("/obj/"))
                return null;
            if (Variables.fileChanged.Replace("\\", "/").Contains("/bin/"))
                return null;

            Variables.BuildFile bf = Functions.GetBuildFile(Variables.fileChanged);

            if (bf == null)
            {
                //Logging.TransmitLog("No build settings file found.");
                return null;
            }
            if (Variables.fileChanged.Replace("\\", "/").Contains(bf.BuildDirectory.Replace("\\", "/")))
            {
                //Logging.TransmitLog("Created by build!");
                return null;
            }
            return bf;
        }
        public static void TriggerBuild()
        {
            Logging.TransmitLog($"Changed: {Variables.fileChanged}.", Variables.currentBuildFile.WebsiteName);

            Logging.TransmitLog("Build file found!", Variables.currentBuildFile.WebsiteName);

            try
            {
                var serverManager = new ServerManager();
                Variables.currentBuildAppPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name.Equals(Variables.currentBuildFile.ApplicationPoolName));
                if (Variables.currentBuildAppPool == null)
                {
                    Logging.TransmitLog("app pool is null", Variables.currentBuildFile.WebsiteName);
                    return;
                }

                string buildFolder = Path.Combine(Variables.WebsiteTemp(Variables.currentBuildFile.WebsiteName), "website_build");

                DirectoryInfo dir = new DirectoryInfo(buildFolder);
                if (dir.Exists)
                    dir.Delete(true);


                Logging.TransmitLog("Building...", Variables.currentBuildFile.WebsiteName);

                RunCMD(string.Format("dotnet publish -c Release -o \"{0}\"", buildFolder), Variables.currentBuildFile.Location, BuildComplete);

            }
            catch (Exception ex)
            {
                Logging.TransmitLog(ex.Message + "\r\n" + ex.StackTrace, Variables.currentBuildFile.WebsiteName);
            }
        }

        public static void BuildComplete(Process proc, string result, string error)
        {
            if (proc.ExitCode != 0)
            {
                Logging.TransmitLog("Build error detected! Cancelling process.", Variables.currentBuildFile.WebsiteName);
                // disable watch for a moment. Obj files may have changed triggering another build process
                return;
            }

            string buildFolder = Path.Combine(Variables.WebsiteTemp(Variables.currentBuildFile.WebsiteName), "website_build");

            Logging.TransmitLog("Build complete!", Variables.currentBuildFile.WebsiteName);


            foreach (var wp in Variables.currentBuildAppPool.WorkerProcesses)
            {
                Process.GetProcessById(wp.ProcessId).Kill();
            }

            if (Variables.currentBuildAppPool.State != ObjectState.Stopped)
            {
                Variables.currentBuildAppPool.Stop();
                Logging.TransmitLog("Stopped app pool!", Variables.currentBuildFile.WebsiteName);
            }

            Logging.TransmitLog("Copying build directory!", Variables.currentBuildFile.WebsiteName);

            CopyFilesRecursively(buildFolder, Variables.currentBuildFile.BuildDirectory);

            Logging.TransmitLog("Copied!", Variables.currentBuildFile.WebsiteName);

            Variables.currentBuildAppPool.Start();

            Logging.TransmitLog("application pool started!", Variables.currentBuildFile.WebsiteName);
        }
    }
}
