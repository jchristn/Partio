@echo off
REM Partio JavaScript SDK Test Harness Runner
REM Usage: go.bat <endpoint> <access_key>
REM Example: go.bat http://localhost:8000 partioadmin

if "%~1"=="" (
    echo Usage: go.bat ^<endpoint^> ^<access_key^>
    echo Example: go.bat http://localhost:8000 partioadmin
    exit /b 1
)

if "%~2"=="" (
    echo Usage: go.bat ^<endpoint^> ^<access_key^>
    echo Example: go.bat http://localhost:8000 partioadmin
    exit /b 1
)

node test-harness.js "%~1" "%~2"
