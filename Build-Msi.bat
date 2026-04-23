@echo off
setlocal

cd /d "%~dp0"

if not exist "Installer\Build-Msi.ps1" (
  echo [ERROR] File not found: Installer\Build-Msi.ps1
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File ".\Installer\Build-Msi.ps1"
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
