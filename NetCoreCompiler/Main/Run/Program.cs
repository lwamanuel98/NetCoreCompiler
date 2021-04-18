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
            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                onStartMethod.Invoke(service, new object[] { new string[] { } });
            }

            Console.WriteLine();
            Console.WriteLine(
                "Press any key to stop the services and end the process...");
            Console.ReadKey();

            MethodInfo onStopMethod = typeof(ServiceBase).GetMethod("OnStop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                onStopMethod.Invoke(service, null);
            }
        }
    }
}
