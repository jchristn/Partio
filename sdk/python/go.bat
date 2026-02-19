@echo off
REM Partio Python SDK Test Harness Runner
REM Usage: go.bat <endpoint> <access_key>
REM Example: go.bat http://localhost:8400 partioadmin

if "%~1"=="" (
    echo Usage: go.bat ^<endpoint^> ^<access_key^>
    echo Example: go.bat http://localhost:8400 partioadmin
    exit /b 1
)

if "%~2"=="" (
    echo Usage: go.bat ^<endpoint^> ^<access_key^>
    echo Example: go.bat http://localhost:8400 partioadmin
    exit /b 1
)

python test_harness.py "%~1" "%~2"
