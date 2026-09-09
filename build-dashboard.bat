@echo off
set TAG=%~1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0"
set IMAGE=jchristn77/partio-dashboard
if "%TAG%"=="latest" (
    set TAGARGS=-t %IMAGE%:latest
) else (
    set TAGARGS=-t %IMAGE%:%TAG% -t %IMAGE%:latest
)
echo === Building %IMAGE%:%TAG% multi-arch and pushing to Docker Hub ===
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 %TAGARGS% -f dashboard/Dockerfile --push dashboard
if errorlevel 1 exit /b %errorlevel%
echo === Pulling %IMAGE%:%TAG% into the local Docker daemon ===
docker pull %IMAGE%:%TAG%
if errorlevel 1 exit /b %errorlevel%
