echo WARNING : This batch file should be run with administrator rights
echo Usage : app_update destination_site

cd ..
git pull 
cd CapsisWebAPI

REM Deploy Capsis installation
pushd %CAPSISWEBAPI_CAPSIS_PATH%
ant installer-export
robocopy bin\Release\net6.0\win-x64\publish c:\inetpub\%1\capsis /E 
popd

REM build app
dotnet publish -c Release -r win-x64 
if %errorlevel% neq 0 exit /b %errorlevel%

%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:%1
if %errorlevel% neq 0 if %errorlevel% neq 1062 exit /b %errorlevel%

robocopy bin\Release\net5.0\win-x64\publish c:\inetpub\%1 /E 
if %errorlevel% geq 9 exit /b %errorlevel%

%windir%\system32\inetsrv\appcmd start apppool /apppool.name:%1
REM %windir%\system32\inetsrv\appcmd start site /site.name:%1

