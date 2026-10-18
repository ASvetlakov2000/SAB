@echo off
set "SAB_HOST=%~dp0..\..\..\outputs\ui-production-preview\notification-settings\production\plugin\SAB.NotificationSettings.ProductionHost.exe"
if not exist "%SAB_HOST%" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Production.ps1"
if errorlevel 1 exit /b 1
start "" "%SAB_HOST%"
