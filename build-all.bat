@echo off
setlocal

if "%~1"=="" (
    echo Usage: build-all.bat ^<docker-image-tag^>
    exit /b 1
)

if not "%~2"=="" (
    echo Usage: build-all.bat ^<docker-image-tag^>
    exit /b 1
)

set TAG=%~1
cd /d "%~dp0"

call "%~dp0build-dashboard.bat" "%TAG%"
if errorlevel 1 exit /b %errorlevel%

call "%~dp0build-server.bat" "%TAG%"
if errorlevel 1 exit /b %errorlevel%

endlocal
