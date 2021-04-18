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
            if (args.Length >= 1)
            {
                if (args[1] != "")
                    Variables.WATCH_DIRECTORY = args[1];
            }

            Console.WriteLine("Watcher starting... \r\nDir to watch = " + Variables.WATCH_DIRECTORY);

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


            Console.WriteLine("Watcher started...");


        }
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Path.GetExtension(e.FullPath).Equals(""))
                return;

            Variables.fileChanged = e.FullPath;

            if (!Functions.CanBuild())
                return;

            if (Variables.MainThread != null && Variables.MainThread.ThreadState != System.Threading.ThreadState.Suspended && Variables.MainThread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                Variables.MainThread.Suspend();
            }


            ThreadStart ths = new ThreadStart(() =>
            {
                try
                {
                    Console.Clear();

                    Console.Write("\r\n-----------\r\nBuild started!\r\n-----------\r\n\r\n");

                    if (Variables.ProcessThread != null && Variables.ProcessThread.ThreadState != System.Threading.ThreadState.Suspended && Variables.ProcessThread.ThreadState != System.Threading.ThreadState.Stopped)
                    {
                        Console.Clear();

                        Console.Write("\r\n-----------\r\nBuild restarted!\r\n-----------\r\n\r\n");
                        if (!Variables.buildProcess.HasExited)
                            Variables.buildProcess.Kill();
                        Variables.ProcessThread.Suspend();
                    }

                    Functions.TriggerBuild();

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
