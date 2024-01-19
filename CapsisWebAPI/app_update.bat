echo WARNING : This batch file should be run with administrator rights
echo Usage : app_update destination_site

cd ..
git pull 
cd CapsisWebAPI

REM Deploy Capsis installation - the ant script messes up the current directory so we store it into a variable
set "CUR_DIR=%~dp0"
REM create a capsis/jar subfolder that is needed by the ant script to operate
mkdir %CAPSISWEBAPI_CAPSIS_PATH%\jar
call %CAPSISWEBAPI_CAPSIS_PATH%\ant.bat installer-export -Dmodules="quebecmrnf/**,artemis/**"
robocopy %CAPSISWEBAPI_CAPSIS_PATH%\export c:\inetpub\%1\capsis /E /purge
cd %CUR_DIR%
mkdir c:\inetpub\%1\capsis\data

REM build app
dotnet publish -c Release -r win-x64 
if %errorlevel% neq 0 exit /b %errorlevel%

%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:%1
if %errorlevel% neq 0 if %errorlevel% neq 1062 exit /b %errorlevel%

robocopy bin\Release\net6.0\win-x64\publish c:\inetpub\%1 /E 
if %errorlevel% geq 9 exit /b %errorlevel%

%windir%\system32\inetsrv\appcmd start apppool /apppool.name:%1
REM %windir%\system32\inetsrv\appcmd start site /site.name:%1

