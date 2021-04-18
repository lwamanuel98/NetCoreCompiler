using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetCoreCompiler
{
    static class Program
    {
        public static string WebsiteTemp(string websiteName)
        {
            string dir = Path.Combine(TempDirectory, websiteName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;

        }
        public static string SettingsFile
        {
            get
            {
                return Path.Combine(TempDirectory, "ncc.settings");
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
        public static Dictionary<string, object> Settings
        {
            get
            {
                if (File.Exists(SettingsFile))
                {
                    using (StreamReader sr = new StreamReader(File.OpenRead(SettingsFile)))
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(sr.ReadToEnd());
                    }
                }
                else
                {
                    Dictionary<string, object> defaults = new Dictionary<string, object>();

                    File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));

                    return defaults;
                }
            }
            set
            {

                Dictionary<string, object> defaults = value;

                File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));
            }
        }

        private static EventLog eventLog1 = null;
        public static void logToFile(string txt)
        {
            initEventLog();

            eventLog1.WriteEntry(txt, EventLogEntryType.Information, 0);
        }

        public static void initEventLog()
        {
            if (eventLog1 == null)
            {
                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("NetCoreCompiler"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "NetCoreCompiler", "NCCInstaller");
                }
                eventLog1.Source = "NetCoreCompiler";
                eventLog1.Log = "NCCInstaller";
            }
        }

        public static string WATCH_DIRECTORY = "";
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new NetCoreCompiler()
            };
            if (!Environment.UserInteractive)
            {
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                RunInteractive(ServicesToRun);
            }
        }
        static void RunInteractive(ServiceBase[] servicesToRun)
        {
            Console.WriteLine("Services running in interactive mode.");
            Console.WriteLine();

            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                Console.Write("Starting {0}...", service.ServiceName);
                onStartMethod.Invoke(service, new object[] { new string[] { } });
                Console.Write("Started");
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(
                "Press any key to stop the services and end the process...");
            Console.ReadKey();
            Console.WriteLine();

            MethodInfo onStopMethod = typeof(ServiceBase).GetMethod("OnStop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                Console.Write("Stopping {0}...", service.ServiceName);
                onStopMethod.Invoke(service, null);
                Console.WriteLine("Stopped");
            }

            Console.WriteLine("All services stopped.");
            // Keep the console alive for a second to allow the user to see the message.
            Thread.Sleep(1000);
        }
    }
}
