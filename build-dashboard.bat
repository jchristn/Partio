@echo off
set TAG=%1
if "%TAG%"=="" set TAG=latest
cd /d "%~dp0"
if "%TAG%"=="latest" (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio-ui:latest -f dashboard/Dockerfile --push dashboard
) else (
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/partio-ui:%TAG% -t jchristn77/partio-ui:latest -f dashboard/Dockerfile --push dashboard
)
