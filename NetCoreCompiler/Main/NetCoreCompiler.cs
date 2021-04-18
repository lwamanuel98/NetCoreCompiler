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

            try
            {
                Console.WriteLine($"Changed: {e.FullPath}" + "\r\n");

                Variables.fileChanged = e.FullPath;

                Functions.TriggerBuild();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
        private static void OnError(object sender, ErrorEventArgs e)
        {
            PrintException(e.GetException());
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
