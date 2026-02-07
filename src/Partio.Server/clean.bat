@echo off
echo Cleaning Partio data files...
if exist partio.json del /f partio.json
if exist partio.db del /f partio.db
if exist logs rd /s /q logs
if exist request-history rd /s /q request-history
echo Done.
