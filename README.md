------------------
NET CORE Compiler
------------------
This is a windows service that compiles source files of a .NET Core web application on the fly and publishes a build to a destination folder. This application also update's a specified IIS website to the new deployment folder. Switches between two deployment directories to minimise website downtime and automatically updates IIS website and app pool settings, using an existing setup within IIS.

Please ensure that the Website name and pool name specified match an existing setup in IIS. This tool does not support remote deployment.

Writen by: Luke Manuel

------------------
Dependencies
------------------
This application requires the SDK version of NET Core that you are compiling i.e. SDK 3.1 for NET Core 3.1 web applications.

You will also need .NET Framework 4 to install the service.

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

The IIS website should be setup, the physical path does not matter as the compiler will build the files to a directory and will update the physical path in IIS automatically (In future releases, this feature will be "disable-able"). In order to reduce downtime, the website will use two switch folders that can be configured via: C:\Windows\Temp\netCoreCompiler\ncc.settings

Each rebuild will switch between the two (or more) folders to minimise the downtime of websites (In future releases, this feature will be "disable-able")

There should only be a maximum downtime of up to 10 seconds using this tool. The capability to add more switch folders (to prevent file locking from older processes being an issue) will be added soon, this feature should remove any down time during building.

Using C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe you can install the service to a windows server/machine.

Steps:

1) Open an Administrator command prompt
2) Unzip Release folder. (*ReleaseFolder*) NOTE: This contains the service executable, store this somewhere it will not get touched!
2) cd to: C:\Windows\Microsoft.NET\Framework\v4.0.30319\
3) Execute (See parameters below): SetupUtil.exe /BuildDirectory=%SOURCE_DIRECTORY% /WebsiteName=%IIS WEBSITE NAME (EXACT!)% %Parameters% %ReleaseFolder%/NetCoreCompiler.exe
4) Start the service

------------------
Uninstall
------------------

Steps:

1) Open an Administrator command prompt
2) cd to: C:\Windows\Microsoft.NET\Framework\v4.0.30319\
3) Execute: SetupUtil.exe /WebsiteName=%IIS WEBSITE NAME (EXACT!)% %ReleaseFolder%/NetCoreCompiler.exe -u

Notes: Ensure that you close Services and Event log otherwise the service will not uninistall!

------------------
Parameters
------------------

Required parameters:

 - /WebsiteName
		This should be the exact match to the IIS Website name that will be manipulated on the web server.
		
 - /BuildDirectory
		Source directory that will be watched for changes. This should contain the project solution in the root.
		
Optional Parameters:

 - /PoolName
		Name of application pool that will be affected when updating the IIS website. If this is not specified, it will default to the Website Name. Please ensure that this is correct!

 - /ApplicationPath
		Path of the application. The default is '/'. If the website to be affected runs under a sub-application; specify the path here. '/' being the root application.
		
 - /DestinationDirectory
		Physical path to store the built files. This folder should have permissions for the website. Switch folders will be created here. By default temporary storage is used (Recommended to set this parameter)
		

------------------
Sample
------------------

Installation:

	C:\Windows\Microsoft.NET\Framework\v4.0.30319\SetupUtil.exe
		/WebsiteName="TestWebsite" ### Required
		/BuildDirectory="C:\wwwroot\TestWebsite\private\src" ### Required
		/PoolName="TestWebsiteApplicationPool" ### (Optional) defaults to website name
		/ApplicationPath="/" ### (Optional) defaults to '/'
		/DestinationDirectory="C:\wwwroot\TestWebsite\www" ### (Optional) defaults to temporary storage
		
		C:/Users/Developer/Desktop/NetCoreCompiler.exe ### Dont forget to specify the service executable to run!