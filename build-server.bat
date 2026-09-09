@echo off
set TAG=%~1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0"
set IMAGE=jchristn77/partio-server
if "%TAG%"=="latest" (
    set TAGARGS=-t %IMAGE%:latest
) else (
    set TAGARGS=-t %IMAGE%:%TAG% -t %IMAGE%:latest
)
echo === Building %IMAGE%:%TAG% for the local platform and loading into the local Docker daemon ===
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64 %TAGARGS% -f src/Partio.Server/Dockerfile --load src
if errorlevel 1 exit /b %errorlevel%
echo === Building %IMAGE%:%TAG% multi-arch and pushing to Docker Hub ===
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 %TAGARGS% -f src/Partio.Server/Dockerfile --push src
if errorlevel 1 exit /b %errorlevel%
