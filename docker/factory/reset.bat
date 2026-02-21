@echo off
REM reset.bat - Reset the Partio Docker deployment to factory defaults.
REM
REM This script:
REM   1. Prompts the user to confirm by typing RESET
REM   2. Runs docker compose down to stop and remove containers
REM   3. Deletes transient files (logs, request history)
REM   4. Restores the factory database and configuration

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%.."

echo.
echo =========================================
echo   Partio Factory Reset
echo =========================================
echo.
echo This will:
echo   - Stop and remove all Partio containers
echo   - Delete all log files
echo   - Delete all request history files
echo   - Replace the database with a clean factory copy
echo   - Replace partio.json with the factory default
echo.
echo WARNING: All current data will be lost!
echo.
set /p "confirmation=Type RESET to confirm: "

if not "%confirmation%"=="RESET" (
    echo.
    echo Reset cancelled.
    exit /b 1
)

echo.
echo Stopping containers...
pushd "%DOCKER_DIR%"
docker compose down 2>nul
popd

echo Removing log files...
if exist "%DOCKER_DIR%\logs" (
    del /q /s "%DOCKER_DIR%\logs\*" >nul 2>nul
    for /d %%d in ("%DOCKER_DIR%\logs\*") do rd /s /q "%%d" 2>nul
)

echo Removing request history files...
if exist "%DOCKER_DIR%\request-history" (
    del /q /s "%DOCKER_DIR%\request-history\*" >nul 2>nul
    for /d %%d in ("%DOCKER_DIR%\request-history\*") do rd /s /q "%%d" 2>nul
)

echo Restoring factory database...
if not exist "%DOCKER_DIR%\data" mkdir "%DOCKER_DIR%\data"
copy /y "%SCRIPT_DIR%data\partio.db" "%DOCKER_DIR%\data\partio.db" >nul
REM Remove WAL/SHM files if present
if exist "%DOCKER_DIR%\data\partio.db-wal" del /q "%DOCKER_DIR%\data\partio.db-wal"
if exist "%DOCKER_DIR%\data\partio.db-shm" del /q "%DOCKER_DIR%\data\partio.db-shm"

echo Restoring factory configuration...
copy /y "%SCRIPT_DIR%partio.json" "%DOCKER_DIR%\partio.json" >nul

echo.
echo =========================================
echo   Reset complete!
echo =========================================
echo.
echo Default credentials:
echo   User     : admin@partio / password
echo   Token    : default
echo   Admin key: partioadmin
echo.
echo Run 'docker compose up -d' to start fresh.
echo.

endlocal
