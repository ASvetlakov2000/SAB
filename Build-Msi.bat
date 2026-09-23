@echo off
setlocal
cd /d "%~dp0"

if not exist "%~dp0Installer\Build-All-Msi.ps1" (
  echo [ERROR] File not found: Installer\Build-All-Msi.ps1
  pause
  exit /b 1
)

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

if /I not "%CONFIG%"=="Debug" if /I not "%CONFIG%"=="Release" (
  echo [ERROR] Invalid configuration: %CONFIG%
  echo         Allowed values: Debug or Release
  pause
  exit /b 1
)

set "INSTALLER_VERSION=%~2"

if "%INSTALLER_VERSION%"=="" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installer\Build-All-Msi.ps1" -Configuration "%CONFIG%"
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installer\Build-All-Msi.ps1" -Configuration "%CONFIG%" -InstallerVersion "%INSTALLER_VERSION%"
)

set "exitcode=%ERRORLEVEL%"

echo.
if not "%exitcode%"=="0" (
  echo [ERROR] Installer build failed. See the message above.
  pause
  exit /b %exitcode%
)

echo [OK] Installers: %~dp0Installer\output\SAB_Revit_2022.msi
echo [OK]             %~dp0Installer\output\SAB_Revit_2023.msi
echo [OK]             %~dp0Installer\output\SAB_Revit_2024.msi
pause
exit /b 0
