echo WARNING : This batch file should be run with administrator rights
echo Make sure the CAPSISWEBAPI_CAPSIS_PATH environmental variable points to the CAPSIS root folder
echo Usage : app_update destination_site

cd ..
git pull 
cd CapsisWebAPI

REM Deploy Capsis installation - the ant script messes up the current directory so we store it into a variable
set "CUR_DIR=%~dp0"
REM create a capsis/jar subfolder that is needed by the ant script to operate
mkdir %CAPSISWEBAPI_CAPSIS_PATH%\jar
call %CAPSISWEBAPI_CAPSIS_PATH%\ant.bat installer-export -Dmodules="quebecmrnf/**,artemis/**,artemis2014/**"

sc stop %1
if %errorlevel% neq 0 if %errorlevel% neq 1062 exit /b %errorlevel%

robocopy %CAPSISWEBAPI_CAPSIS_PATH%\export c:\inetpub\%1\capsis /E /purge
cd %CUR_DIR%
mkdir c:\inetpub\%1\capsis\data

REM build app
dotnet publish -c Release -r win-x64 
if %errorlevel% neq 0 exit /b %errorlevel%

robocopy bin\Release\net6.0\win-x64\publish c:\inetpub\%1 /E 
if %errorlevel% geq 9 exit /b %errorlevel%

sc start %1
if %errorlevel% neq 0 if %errorlevel% neq 1062 exit /b %errorlevel%

