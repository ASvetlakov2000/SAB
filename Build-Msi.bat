@echo off
setlocal

cd /d "%~dp0"

if not exist "Installer\Build-Msi.ps1" (
  echo [ERROR] File not found: Installer\Build-Msi.ps1
  pause
  exit /b 1
)

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

if /I not "%CONFIG%"=="Debug" if /I not "%CONFIG%"=="Release" (
  echo [ERROR] Invalid configuration: %CONFIG%
  echo         Allowed values: Debug or Release
  pause
  exit /b 1
)

set "BIN_FOLDER=..\SAB\bin\%CONFIG%"
set "INSTALLER_VERSION=%~2"

if "%INSTALLER_VERSION%"=="" (
  echo [INFO] Building MSI from bin folder: %BIN_FOLDER%
  powershell -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Msi.ps1" -BinFolder "%BIN_FOLDER%"
) else (
  echo [INFO] Building MSI from bin folder: %BIN_FOLDER%
  echo [INFO] Installer version override: %INSTALLER_VERSION%
  powershell -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Msi.ps1" -BinFolder "%BIN_FOLDER%" -InstallerVersion "%INSTALLER_VERSION%"
)

set "exitcode=%ERRORLEVEL%"

if not "%exitcode%"=="0" (
  echo.
  echo [ERROR] Build-Msi.ps1 finished with code %exitcode%.
  pause
  exit /b %exitcode%
)

echo.
echo [OK] MSI build completed.
pause
exit /b 0
