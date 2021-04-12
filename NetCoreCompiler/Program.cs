using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace NetCoreCompiler
{
    static class Program
    {
        public static string WebsiteTemp
        {
            get
            {
                string dir = Path.Combine(TempDirectory, Program.WEBSITE_NAME);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static string SettingsFile
        {
            get
            {
                return Path.Combine(WebsiteTemp, "ncc.settings");
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
        public static Dictionary<string, string> Settings
        {
            get
            {
                if (File.Exists(SettingsFile))
                {
                    using (StreamReader sr = new StreamReader(File.OpenRead(SettingsFile)))
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(sr.ReadToEnd());
                    }
                }
                else
                {
                    Dictionary<string, string> defaults = new Dictionary<string, string>();
                    defaults.Add("current_switch", "0");
                    defaults.Add("switch_folder_0", "www");
                    defaults.Add("switch_folder_1", "www1");
                    defaults.Add("switch_folder_2", "www2");
                    defaults.Add("switch_folder_3", "www3");
                    //defaults.Add("switch_folder_4", "www4");

                    File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));

                    return defaults;
                }
            }
            set
            {

                Dictionary<string, string> defaults = value;

                File.WriteAllText(SettingsFile, Newtonsoft.Json.JsonConvert.SerializeObject(defaults));
            }
        }
        public static int MaxSwitch
        {
            get
            {
                var settings = Settings;
                int i = 0;
                while (settings.ContainsKey("switch_folder_" + i))
                {
                    i++;
                }
                i--; // remove one, it broke out of the loop as it didnt exist!
                return i;
            }
        }
        public static int CurrentSwitch
        {
            get
            {
                return int.Parse(Settings["current_switch"]);
            }
            set
            {
                Dictionary<string, string> settings = Settings;
                settings["current_switch"] = value.ToString();
                Settings = settings;
            }
        }
        public static string CurrentSwitchFolder
        {
            get
            {
                return Path.Combine(DEST_DIRECTORY, Settings["switch_folder_" + CurrentSwitch]);
            }
        }

        public static void NextSwitchFolder()
        {
            if (CurrentSwitch < MaxSwitch)
                CurrentSwitch++;
            else
                CurrentSwitch = 0;
        }

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
        public static string DEST_DIRECTORY = WebsiteTemp;
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
