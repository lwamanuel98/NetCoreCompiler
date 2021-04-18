------------------
NET CORE Compiler
------------------
This is a windows service that compiles source files of a .NET Core web application on the fly and publishes a build to a destination folder. This application also handles the IIS application pool shutdown and restart reducing downtime of NET Core web application deployment.

Please ensure that the Website name and pool name specified match an existing setup in IIS. This tool does not support remote deployment.

Writen by: Luke Manuel

------------------
Dependencies
------------------
This application requires the SDK version of NET Core that you are compiling i.e. SDK 3.1 for NET Core 3.1 web applications.

------------------
Installation
------------------

Recommendation (used in sample):

First, setup a website in IIS and create the folder structure as follows:
 - /private
 - /private/src
 - /www

Locate all website source files within the '/private/src' folder.

Notes:

The IIS website should be setup, 

Each rebuild will switch between the two (or more) folders to minimise the downtime of websites (In future releases, this feature will be "disable-able")

There should only be a maximum downtime of up to 10 seconds using this tool.

Using C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe you can install the service to a windows server/machine.

Steps:

1) Open an Administrator command prompt
2) Unzip Release folder. (*ReleaseFolder*) NOTE: This contains the service executable, store this somewhere it will not get touched!
2) cd to: C:\Windows\Microsoft.NET\Framework\v4.0.30319\
3) Execute (See parameters below): SetupUtil.exe /WatchDirectory=%WWW_ROOT_FOLDER% %ReleaseFolder%/NetCoreCompiler.exe
4) Start the service

Website Setup:
1) Place a "settings.ncc" in the root of your website, where the "*.csproj" can be found
2) Configure the settings files as follows:

	{
		"WebsiteName": "Name Of Website In IIS",
		"ApplicationPoolName": "Exact Name Of Application Pool In IIS",
		"BuildDirectory": "Directory Where The Build Should Reside e.g. /www"
	}
3) Any changes made will automatically trigger a build to a temporary folder, the application pool will be shutdown, build files transferred and the application pool is restarted.

------------------
Parameters
------------------

Required parameters:

 - /WatchDirectory
		Should be the folder that contains all websites on your web server e.g. "/home" or "/inetpub/wwwroot". This folder will be watched and where a "settings.ncc" file can be found will determine the websites that should be built via the NCC.
