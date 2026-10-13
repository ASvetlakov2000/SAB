@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0LaunchPreview.ps1" -Elevations
if errorlevel 1 pause
