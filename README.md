# CapsisWebAPI
A web API that allows simulations on the CAPSIS platform

## Creating the web application package

0. Install .NET Core Hosting Bundle
https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-6.0.1-windows-hosting-bundle-installer

## Capsis installation and setup

1. Install Apache Ant on the target host from this page : https://ant.apache.org/bindownload.cgi (1.10.13 recommended) and make sure your system PATH environment variable points to the ant folder /bin
2. Checkout capsis in any folder you want (and note it)
3. Edit the ant.bat file inside the capsis checkout folder and make sure the path correctly points to your Java 1.8 JDK installation (JRE won't work)
4. Test the ant installation and script by launching command : 
	ant installer-export
5. If everything ran ok, you should see the capsis files installed in ./export
6. Set a new environment variable CAPSISWEBAPI_CAPSIS_PATH to the location where Capsis has been checked out (not the export subfolder, but the main folder)

## Setup of the build system
1. Install Microsoft Build Tools 2022 from this page : https://visualstudio.microsoft.com/downloads/?q=build+tools
  - In the Workloads tab :
    - Select "Web development build tools" in the "Web & Cloud" section
  - In the Individual Components tab :
	  - Make sure the ".NET 6.0 Runtime" item is selected
  - click "Install"
2. Install GIT on the target host using this page : https://git-scm.com/download/win
3. Open a commandline (CMD) window and clone the GIT repository to the desired location on the target host using command
if cloning the default production branch use :
```
git clone https://github.com/CWFC-CCFB/CapsisWebAPI.git subfolder
```
if cloning a specific branch (ex: develop) use :
```
git clone -b develop https://github.com/CWFC-CCFB/CapsisWebAPI.git subfolder
```
note : If subfolder is not specified, git will automatically create a subfolder named CapsisWebAPI into the desired location
4. to ensure the build system is working properly, cd into the CapsisWebAPI subfolder of the project folder and launch command : 
```
dotnet publish -c Release -r win-x64
```

This will result in the app files being deployed in the CapsisWebAPI\bin\Release\net5.0\win-x64\publish folder
5. Test the app launch by executing the publish command again as in step 4
```
dotnet publish -c Release -r win-x64 
```

6. cd into the publish subfolder (as specified in step 4) then launch :
```
	WebAPI.exe
```

This should start the service (which will probably crash because it needs its capsis dependency to launch properly.  

## IIS Setup

### IIS Installation
1. type "features" in the windows search bar and start the "Turn Windows Features on or off" control panel
2. check the "Internet Information Services" and "Internet Information Service Hostable Core" items
3. under "Internet Information Services", check the "Application Development Features" and "Web Management Tools"
4. under "Web Management Tools", check the "IIS Management Console", "IIS Management Scripts and Tools" and "IIS Management Service" items
5. under "World Wide Web Services", check the "Application Development Features", "Common HTTP features", "Health and diagnostics", "Performance Features", and "Security" items.
6. under "Application Development Features", check the ".NET Extensibility 3.5", ".NET Extensibility 4.8", "Application Initialization", "ASP.NET 3.5", "ASP.NET 4.8", "ISAPI Extensions" and "ISAPI Filters" items
7. under "Common HTTP features", check the "Default Document", "Directory Browsing", "HTTP Errors" and "Static Content" 
8. under "Health and Diagnotics" check the "HTTP logging" item
9. under "Performance Features", check the "Static Content Compression" item
10. click OK

### IIS Configuration

Create and setup the website folder
1. Create a new folder named "CapsisWebAPI_A" in the IIS wwwroot location, typically : C:\inetpub
2. Right-click on the new CapsisWebAPI_A folder and select "properties", then go the Security tab
3. click "Edit", then click "Add" and enter IIS_IUSRS in the box, then click "Check Names" and then OK if the name has been found
4. Ensure "Read & Execute" is checked for the IIS_IUSRS user then click OK
5. Create a "capsis" subfolder in the "CapsisWebAPI_A" folder and ensure the IIS_IUSRS user has read access to this folder
6. Create a "capsis/var" subfolder in the "CapsisWebAPI_A" folder and ensure the IIS_IUSRS user has full access (read, write, execute) on this new folder	(used for capsis logging)
7. Create a "data" subfolder in the "CapsisWebAPI_A" folder and ensure the IIS_IUSRS user has full access (read, write, execute) on this new folder (used for temporary data files by the CapsisWebAPI)

### Create the IIS website and Application
1. Start the "Internet Information Services (IIS) Manager" app
2. Right-click on the "Sites" folder and select "Add Website"
3. Enter CapsisWebAPI_A in the Site name field
4. Click "..." and select the CapsisWebAPI_A folder location created above as "Physical path"
5. Leave the "Host name" field empty and click OK
6. Right-click on the newly created CapsisWebAPI_A website and select "Add Application"
7. enter "CapsisWebAPI_A" in the Alias field and click on "..." to select the same CapsisWebAPI_A folder created above again
8. Click OK

### Configure App to be "always running" instead of "on demand"
1. Right-click the App node, then Manage Application and Advanced Settings
2. Set "Preload Enabled" to "True"
3. Look the name of the "Application Pool" and then click OK
4. Click on the "Application Pools" node
5. Right-click the Application Pool item that is used by the App seen on step 3 and select "Advanced Settings"
6. set "Start Mode" to "AlwaysRunning" and click OK
7. set "Idle Time-out (minutes)" to 0 

### Configure App to avoid recycling
1. Right-click the App node, then Manage Application and Advanced Settings
2. Under Recycling set the "Regular time intervals (minutes)" option to 0 (by default it is set to 1740 min (i.e. 29 hours))

### Startup and test
1. Copy the build 
1. Click on the website then click on the Start button on the right panel under "Manage Website" to make sure it is started
2. Open a browser and use address "server.ip.address" and you should see the CapsisWebAPI home page

## Updating the .NET application

The app_update.bat batch file located in the CapsisWebAPI subfolder automatically performs the following steps :
	1. pulls the latest version from the git current branch
	2. builds and exports capsis using the ant script (and copies it inside the corresponding inetpub folder)	
	3. publishes the .net code 
	3. Stops the IIS server
	4. copies the resulting published files into the IIS CapsisWebAPI folder
	5. Starts the IIS server

Note 1 : Edit the app_update.bat batch file to ensure that the destination IIS folder is correct before using it.
Note 2 : The batch file must be launched with administrator rights

## Using two consurrent websites

Using two concurrent websites can be useful to deploy a new release canditate to IIS while the current production server is online.  
Final tests can then be run on the staging server, and when they pass, the release candidate is validated and incoming traffic can be redirected to it.

A simple way to implement this scheme is the following : 
1. Configure two different websites and apps linked to CapsisWebAPI_A and CapsisWebAPI_B folders under C:\inetpub
2. Bind CapsisWebAPI_A to port 8101 and CapsisWebAPI_B to port 8102
3. Open ports 80, 8101 and 8102 to incoming rules on the firewall
4. The reverse proxy mechanism described below will be used to switch between CapsisWebAPI_A and CapsisWebAPI_B apps

## Reverse-proxy / URL rewriting

an IIS site can be configured as a reverse proxy to act as a gateway for different web services running on different ports.  See the doc here : 
https://docs.microsoft.com/en-us/iis/extensions/url-rewrite-module/reverse-proxy-with-url-rewrite-v2-and-application-request-routing

In our case we will perform the following steps :

1. Install the Application Request Routing and URL Rewrite modules to IIS 
2. Create a folder in the inetpub folder named "RootProxyCapsisWebAPI_A" and add reading permissions to IIS_IUSRS (see IIS CONFIGURATION previous section)
3. Using IIS Manager, add a new site named "RootProxyCapsisWebAPI_A" bound to port 8100
4. Create a file named web.config into the RootProxyCapsisWebAPI_A folder containing the following:
```
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <rewrite>
            <rules>
                <clear />
                <rule name="forwardTo8101" stopProcessing="true">
                    <match url="(.*)" />
                    <conditions>
                        <add input="{CACHE_URL}" pattern="^(https?)://" />
                    </conditions>
                    <action type="Rewrite" url="{C:1}://localhost:8101/{R:1}" />
                </rule>
            </rules>
            <outboundRules>
                <preConditions>
                    <preCondition name="isHTML">
                        <add input="{RESPONSE_CONTENT_TYPE}" pattern="^text/html" />
                    </preCondition>
                </preConditions>
            </outboundRules>
        </rewrite>
    </system.webServer>
</configuration>
```
5. Ensure the new file has the correct READ access rights for user IIS_IUSRS
6. Create the same structure for RootProxyCapsisWebAPI_B and set its rewrite destination to 8102 instead of 8101 in the web.config file.
7. Add a new rule to the base RootProxy's server by adding this rule to its web.config file : 
```
<rule name="ReverseProxyCapsisWebAPIRoute" enabled="true" stopProcessing="true">
	<match url="^CapsisWebAPI/(.*)" />
	<conditions logicalGrouping="MatchAll" trackAllCaptures="false" />
	<action type="Rewrite" url="http://localhost:8100/CapsisWebAPI/{R:1}" />
</rule>
```
Note : This rule catches all requests done with a CapsisWebAPI prefix and forwards it to the 8100 port while preserving the CapsisWebAPI prefix in the request

Once both RootProxyCapsisWebAPI_A and RootProxyCapsisWebAPI_B are configured, the way to switch between RootProxyCapsisWebAPI_A and RootProxyCapsisWebAPI_B will be enabling one of the two RootProxy sites.  
Both sites cannot be active at the same time because they both are bound to port 8100.