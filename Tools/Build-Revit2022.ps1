param(
    [string]$RevitApiDirectory = '',
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $RevitApiDirectory) {
    $workspaceRoot = Split-Path -Parent (Split-Path -Parent $projectRoot)
    $RevitApiDirectory = Join-Path $workspaceRoot 'lib\Revit2022'
}
$RevitApiDirectory = (Resolve-Path -LiteralPath $RevitApiDirectory).Path
foreach ($assemblyFile in @('RevitAPI.dll', 'RevitAPIUI.dll')) {
    $assemblyPath = Join-Path $RevitApiDirectory $assemblyFile
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw "Missing local Revit2022 API: $assemblyPath" }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version
    if ($assemblyVersion.Major -ne 22) { throw "$assemblyFile must be Revit API 22; found $assemblyVersion" }
}
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$outputRoot = Join-Path $projectRoot 'outputs\ui-production-2022'
$pluginPath = Join-Path $outputRoot 'plugin'
$pluginDirectory = $pluginPath + '\'
$intermediateRoot = (Join-Path $outputRoot 'obj') + '\'
$intermediateDebug = (Join-Path $outputRoot 'obj\Debug') + '\'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$buildLog = Join-Path $outputRoot 'build.log'
& $msbuild (Join-Path $projectRoot 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug /p:RevitVersion=2022 "/p:RevitApiDirectory=$RevitApiDirectory" "/p:OutputPath=$pluginDirectory" "/p:BaseIntermediateOutputPath=$intermediateRoot" "/p:IntermediateOutputPath=$intermediateDebug" /v:quiet /nologo /fl "/flp:logfile=$buildLog;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Revit2022 build failed. See $buildLog" }
$apiManifest = @()
foreach ($assemblyFile in @('RevitAPI.dll', 'RevitAPIUI.dll')) {
    $assemblyPath = Join-Path $RevitApiDirectory $assemblyFile
    $apiManifest += [ordered]@{ name=$assemblyFile; path=$assemblyPath; assemblyVersion=([Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()); fileVersion=(Get-Item -LiteralPath $assemblyPath).VersionInfo.FileVersion; sha256=(Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash }
}
$pluginDll = Join-Path $pluginPath 'SAB.dll'
[ordered]@{ revitVersion=2022; configuration='Debug'; buildStatus='PASS'; apiFiles=$apiManifest; assemblyPath=$pluginDll; assemblyVersion=([Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString()); assemblySha256=(Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256).Hash; log=$buildLog; installationPerformed=$false } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'build-provenance.json') -Encoding UTF8
Write-Host "PASS Revit2022 build: $pluginPath"
if ($Capture) {
    $hostIntermediateRoot = (Join-Path $outputRoot 'host-obj') + '\'
    & $msbuild (Join-Path $PSScriptRoot 'UiShowcase\Sab.UiShowcase.csproj') /t:Build /p:Configuration=Debug "/p:PluginDirectory=$pluginDirectory" "/p:RevitApiDirectory=$RevitApiDirectory" "/p:BaseIntermediateOutputPath=$hostIntermediateRoot" /v:quiet /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Revit2022 WPF host build failed.' }
    $capturePath = Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-production-2022'
    $previousApiDirectory = $env:SAB_PREVIEW_REVIT_API_DIRECTORY
    try {
        $env:SAB_PREVIEW_REVIT_API_DIRECTORY = $RevitApiDirectory
        $captureRun = Start-Process -FilePath (Join-Path $pluginPath 'SAB.UI.Preview.exe') -ArgumentList @('--capture', '--out', ('"' + $capturePath + '"')) -Wait -PassThru -WindowStyle Hidden
        if ($captureRun.ExitCode -ne 0) { throw "Revit2022 capture failed. See $pluginPath\diagnostics\error.txt" }
        $python = 'C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
        & $python -X utf8 (Join-Path $PSScriptRoot 'UiShowcase\MakeRevit2022Evidence.py') --api-directory $RevitApiDirectory --plugin-directory $pluginPath --capture-directory $capturePath
        if ($LASTEXITCODE -ne 0) { throw 'Revit2022 capture evidence verification failed.' }
        Write-Host "PASS Revit2022 native WPF capture: $capturePath"
    }
    finally { $env:SAB_PREVIEW_REVIT_API_DIRECTORY = $previousApiDirectory }
}
