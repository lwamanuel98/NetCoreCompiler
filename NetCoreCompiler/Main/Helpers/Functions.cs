using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        public static Process RunCMD(string cmd, string workingDir, out string result, out string error)
        {
            ProcessStartInfo psiDotNetRestore = new ProcessStartInfo(@"C:\Windows\System32\cmd.exe", "/c " + cmd);
            psiDotNetRestore.WindowStyle = ProcessWindowStyle.Maximized;
            psiDotNetRestore.WorkingDirectory = workingDir;

            psiDotNetRestore.RedirectStandardOutput = true;
            psiDotNetRestore.RedirectStandardError = true;
            psiDotNetRestore.UseShellExecute = false;
            psiDotNetRestore.CreateNoWindow = true;

            Process proc = new Process();
            proc.StartInfo = psiDotNetRestore;
            proc.Start();

            result = proc.StandardOutput.ReadToEnd();
            error = proc.StandardError.ReadToEnd();

            Variables.buildProcess = proc;

            return proc;
        }

        public static void TriggerBuild()
        {
            var buildFile = GetBuildFile(Variables.fileChanged);
            if (buildFile == null)
            {
                Console.WriteLine("No build settings file found.");
                return;
            }
            if (Variables.fileChanged.Replace("\\", "/").Contains(buildFile.BuildDirectory.Replace("\\", "/")))
            {
                Console.WriteLine("Created by build!");
                return;
            }
            Console.WriteLine("Build file found!");

            if (Variables.buildProcess != null && !Variables.buildProcess.HasExited)
            {
                Console.WriteLine("Other build cancelled!");
                Variables.buildProcess.Kill();
                Variables.buildProcess = null;
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

                string buildFolder = Path.Combine(Variables.WebsiteTemp(buildFile.WebsiteName), "website_build");


                DirectoryInfo dir = new DirectoryInfo(buildFolder);
                if (dir.Exists)
                    dir.Delete(true);




                Console.WriteLine("Building...");
                RunCMD(string.Format("dotnet publish -c Release -o \"{0}\"", buildFolder), buildFile.Location, out result, out error);

                Console.Write(result);

                if (Variables.buildProcess == null) // has been killed - new build will have started
                {
                    Console.WriteLine("Build cancelled!");

                    return;
                }

                Console.WriteLine("Build complete!");

                Variables.watcher.EnableRaisingEvents = false;

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
                Variables.watcher.EnableRaisingEvents = true;

                appPool.Start();

                Console.WriteLine("application pool started!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
    }
}
