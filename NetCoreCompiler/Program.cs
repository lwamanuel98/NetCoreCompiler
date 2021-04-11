using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreCompiler
{
    static class Program
    {
        private static EventLog eventLog1 = null;
        public static void logToFile(string txt)
        {
            initEventLog();

            eventLog1.WriteEntry(txt, EventLogEntryType.Information, 0);
        }

        public static void initEventLog()
        {
            if(eventLog1 == null)
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

        public static string BUILD_DIRECTORY = "";
        public static string WEBSITE_NAME = "";
        public static string POOL_NAME = "";
        public static string APPLICATION_PATH = "/";
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
            ServiceBase.Run(ServicesToRun);
        }
    }
}
