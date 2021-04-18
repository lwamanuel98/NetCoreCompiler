using System.Diagnostics;

namespace NetCoreCompiler
{
    public static class Logging
    {
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
    }
}
