using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

namespace NetCoreCompiler
{
    public partial class NetCoreCompiler : ServiceBase
    {
        public NetCoreCompiler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                if (args[1] != "")
                    Variables.WATCH_DIRECTORY = args[1];
                if (args.Length > 2)
                {
                    if (args[2] != "")
                    {
                        Variables.TCP_IP = args[2];
                    }
                }
            }

            Logging.StartTCPListener();



            Logging.TransmitLog("Watcher starting... \r\nDir to watch = " + Variables.WATCH_DIRECTORY, null);

            Variables.watcher = new FileSystemWatcher(Variables.WATCH_DIRECTORY);

            Variables.watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            Variables.watcher.Changed += OnChanged;
            Variables.watcher.Created += OnChanged;
            Variables.watcher.Deleted += OnChanged;
            Variables.watcher.Renamed += OnChanged;
            Variables.watcher.Error += OnError;

            Variables.watcher.IncludeSubdirectories = true;
            Variables.watcher.EnableRaisingEvents = true;


            Logging.TransmitLog("Watcher started...", null);


        }
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.FullPath).Equals(""))
                return;

            Variables.fileChanged = e.FullPath;

            Variables.BuildFile bf = Functions.CanBuild();

            if (bf == null)
                return;

            if (Variables.MainThread != null && Variables.MainThread.ThreadState != System.Threading.ThreadState.Suspended && Variables.MainThread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                Variables.MainThread.Suspend();
            }


            ThreadStart ths = new ThreadStart(() =>
            {
                try
                {

                    if (Variables.ProcessThread != null && Variables.ProcessThread.ThreadState != System.Threading.ThreadState.Suspended && Variables.ProcessThread.ThreadState != System.Threading.ThreadState.Stopped)
                    {
                        if (!Variables.buildProcess.HasExited)
                            Variables.buildProcess.Kill();
                        Variables.ProcessThread.Suspend();

                        Variables.currentBuildFile = bf;

                        Logging.TransmitLog("\r\n-----------\r\nBuild restarted!\r\n-----------\r\n\r\n", Variables.currentBuildFile.WebsiteName);
                    } else
                    {
                        Variables.currentBuildFile = bf;

                        Logging.TransmitLog("\r\n-----------\r\nBuild started!\r\n-----------\r\n\r\n", Variables.currentBuildFile.WebsiteName);
                    }

                    Functions.TriggerBuild();

                }
                catch (Exception ex)
                {
                    Logging.TransmitLog(ex.Message, Variables.currentBuildFile.WebsiteName);
                }
            });

            Variables.MainThread = new Thread(ths);
            Variables.MainThread.Start();

            Variables.watcher.EnableRaisingEvents = false;
            Thread.Sleep(1000);
            Variables.watcher.EnableRaisingEvents = true;

        }
        private static void OnError(object sender, ErrorEventArgs e)
        {
            //PrintException(e.GetException()); surpressed
        }
        private static void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Logging.TransmitLog($"Message: {ex.Message}" + "\r\n" +
                    "Stacktrace:" + "\r\n" +
                    ex.StackTrace + "\r\n", null);

                PrintException(ex.InnerException);
            }
        }
        protected override void OnStop()
        {
            Logging.StopTCPListener();
        }
    }
}
