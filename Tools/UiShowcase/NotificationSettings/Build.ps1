param([switch]$Capture,[switch]$Open)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if(!(Test-Path -LiteralPath (Join-Path $repo 'outputs/ui-production-preview/plugin/SAB.dll'))) { throw 'Existing shared SAB.dll is required. Build the shared showcase host first; this preview does not modify the production project.' }
& $msbuild (Join-Path $PSScriptRoot 'NotificationSettings.csproj') /t:Build /v:quiet /nologo
if($LASTEXITCODE -ne 0) {throw 'Notification settings preview build failed.'}
$exe=Join-Path $repo 'outputs/ui-production-preview/notification-settings/revision-2/SAB.NotificationSettings.Preview.Revision2.exe'
if($Capture) {
 $run=Start-Process -FilePath $exe -ArgumentList '--capture' -PassThru -Wait -WindowStyle Hidden
 if($run.ExitCode -ne 0) {throw 'Preview checks failed; inspect notification-settings/error.txt and screenshots-notification-settings/verification.txt.'}
}
if($Open) {Start-Process -FilePath $exe -WindowStyle Hidden}
