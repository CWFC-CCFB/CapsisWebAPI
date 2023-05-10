echo WARNING : This batch file should be run with administrator rights
echo Usage : app_update_and_switch [CapsisWebAPI_A, CapsisWebAPI_B]

SET newport=none

IF "%1"=="CapsisWebAPI_A" (
SET newport=8101
SET oldapp=CapsisWebAPI_B)

IF "%1"=="CapsisWebAPI_B" (
SET newport=8102
SET oldapp=CapsisWebAPI_A)

echo %newport%
echo %oldapp%

IF "%newport%"=="none" (
  Echo Wrong parameter value given : %1
  exit /b 1
)

call app_update %1
if %errorlevel% neq 0 exit /b %errorlevel%

echo Waiting for warmup to complete on %1...
curl http://localhost:%newport% --connect-timeout 999
if %errorlevel% neq 0 exit /b %errorlevel%

%windir%\system32\inetsrv\appcmd stop site /site.name:RootProxy%oldapp%
%windir%\system32\inetsrv\appcmd start site /site.name:RootProxy%1

REM now that everything is up, shut down the old application pool to free RAM
%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:%oldapp%
if %errorlevel% neq 0 if %errorlevel% neq 1062 exit /b %errorlevel%