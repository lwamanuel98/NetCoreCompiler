using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CustomExtensions;


namespace NetCoreCompiler
{

    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {

            InitializeComponent();

        }
        protected override void OnBeforeInstall(IDictionary savedState)
        {
            SetServiceName();

            Context.Parameters["assemblypath"] = "\"" + Context.Parameters["assemblypath"] + "\"";

            Context.Parameters["assemblypath"] = Context.Parameters["assemblypath"]
                .AppendParameter(Context.Parameters["BuildDirectory"])
                .AppendParameter(Context.Parameters["WebsiteName"])
                .AppendParameter(Context.Parameters["PoolName"])
                .AppendParameter(Context.Parameters["ApplicationPath"]);

            Program.logToFile("Assembly Path: " + Context.Parameters["assemblypath"]);

            base.OnBeforeInstall(savedState);
        }
        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            SetServiceName(); 
            
            base.OnBeforeUninstall(savedState);
        }
        private void SetServiceName()
        {
            if (Context.Parameters.ContainsKey("WebsiteName"))
            {
                serviceInstaller1.ServiceName = "NetCoreCompiler [" + Context.Parameters["WebsiteName"] + "]";
            }

            if (Context.Parameters.ContainsKey("DisplayName"))
            {
                serviceInstaller1.DisplayName = Context.Parameters["DisplayName"];
            }

            Program.logToFile("Service Name: " + serviceInstaller1.ServiceName + "; Display Name: " + serviceInstaller1.DisplayName);
        }
    }
}

namespace CustomExtensions
{
    //Extension methods must be defined in a static class
    public static class StringExtension
    {
        // This is the extension method.
        // The first parameter takes the "this" modifier
        // and specifies the type for which the method is defined.
        public static string AppendParameter(this string appendTo, string parameter)
        {
            if (parameter == null)
                parameter = "";

            appendTo += " \"" + parameter + "\"";
            return appendTo;
        }
    }
}