param([switch]$Capture, [switch]$AllVersions)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$output = Join-Path $root 'outputs\ui-production-preview\notification-settings\production'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$versions = @(2023)
if ($AllVersions) { $versions = @(2022,2024,2023) }
foreach ($version in $versions) {
    $api = Join-Path ${env:ProgramW6432} "Autodesk\Revit $version"
    if ($version -eq 2022) { $api = Join-Path (Split-Path -Parent (Split-Path -Parent $root)) 'lib\Revit2022' }
    $plugin = Join-Path $output "api$version\plugin\"
    if ($version -eq 2023) { $plugin = Join-Path $output 'plugin\' }
    $intermediate = Join-Path $output "api$version\obj\"
    $buildLog = Join-Path $output "build$version.log"
    & $msbuild (Join-Path $root 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug "/p:RevitVersion=$version" "/p:RevitApiDirectory=$api" "/p:OutputPath=$plugin" "/p:BaseIntermediateOutputPath=$intermediate" "/p:IntermediateOutputPath=$intermediate" /v:quiet /nologo /fl "/flp:logfile=$buildLog;verbosity=normal"
    if ($LASTEXITCODE -ne 0) { throw "Build $version failed: $buildLog" }
}
& $msbuild (Join-Path $PSScriptRoot 'ProductionHarness.csproj') /t:Build /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Production WPF host build failed' }
if ($Capture) {
    $process = Start-Process -FilePath (Join-Path $output 'plugin\SAB.NotificationSettings.ProductionHost.exe') -ArgumentList '--capture' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Capture failed: $output\error.txt" }
}
