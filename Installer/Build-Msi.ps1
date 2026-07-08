param(
    [string]$BinFolder = "..\SAB\bin\Debug",
    [string]$SyncReminderBinFolder = "..\SyncReminderTest\bin\Debug",
    [string]$OutputFolder = ".\output",
    [string]$InstallerVersion = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Ensure-LocalWixTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolsFolder
    )

    $wixExePath = Join-Path $ToolsFolder "wix.exe"

    if (-not (Test-Path -LiteralPath $wixExePath)) {
        New-Item -ItemType Directory -Path $ToolsFolder -Force | Out-Null
        dotnet tool install --tool-path $ToolsFolder wix --version 4.0.5 | Out-Host
    }

    if (-not (Test-Path -LiteralPath $wixExePath)) {
        throw "wix.exe was not installed to: $ToolsFolder"
    }

    return $wixExePath
}

function Ensure-WixUiExtension {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WixExePath
    )

    # Блок установки UI-расширения, требуемого WixSharp по умолчанию.
    & $WixExePath extension add -g WixToolset.UI.wixext/4.0.5 | Out-Null
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
$installerProjectPath = Join-Path $scriptRoot "WixSharpInstaller\WixSharpInstaller.csproj"
$syncReminderProjectPath = Join-Path $repositoryRoot "SyncReminderTest\SyncReminderTest.csproj"
$binRootPath = Resolve-AbsolutePath -Path $BinFolder -BasePath $scriptRoot
$syncReminderBinRootPath = Resolve-AbsolutePath -Path $SyncReminderBinFolder -BasePath $scriptRoot
$syncReminderAssemblyPath = Join-Path $syncReminderBinRootPath "SyncReminderTest.dll"
$outputRootPath = Resolve-AbsolutePath -Path $OutputFolder -BasePath $scriptRoot
$toolsRootPath = Join-Path $scriptRoot ".tools"

if (-not (Test-Path -LiteralPath $installerProjectPath)) {
    throw "Installer project was not found: $installerProjectPath"
}

if (-not (Test-Path -LiteralPath $binRootPath)) {
    throw "Bin folder was not found: $binRootPath"
}

if ((-not (Test-Path -LiteralPath $syncReminderAssemblyPath)) -and (Test-Path -LiteralPath $syncReminderProjectPath)) {
    Write-Host "SyncReminderTest DLL was not found. Building test plugin:"
    Write-Host "  $syncReminderProjectPath"

    dotnet build "$syncReminderProjectPath" --configuration Debug --property:Platform=AnyCPU | Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "SyncReminderTest build failed."
    }
}

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

# Блок установки локального WiX 4 для WixSharp.
$wixExePath = Ensure-LocalWixTool -ToolsFolder $toolsRootPath
$env:PATH = "$toolsRootPath;$env:PATH"
Ensure-WixUiExtension -WixExePath $wixExePath

# Блок запуска WixSharp-генератора MSI.
$installerArguments = @(
    "--root", "$repositoryRoot",
    "--bin", "$binRootPath",
    "--out", "$outputRootPath",
    "--version", "$InstallerVersion"
)

if (Test-Path -LiteralPath $syncReminderBinRootPath) {
    $installerArguments += @("--sync-reminder-bin", "$syncReminderBinRootPath")
    Write-Host "SyncReminderTest will be included:"
    Write-Host "  $syncReminderBinRootPath"
}
else {
    Write-Host "SyncReminderTest bin folder was not found and will be skipped:"
    Write-Host "  $syncReminderBinRootPath"
}

dotnet run --project $installerProjectPath -- @installerArguments

if ($LASTEXITCODE -ne 0) {
    throw "WixSharp installer build failed."
}

$msi2023 = Join-Path $outputRootPath "SAB_Revit_2023.msi"
$msi2024 = Join-Path $outputRootPath "SAB_Revit_2024.msi"

if (-not (Test-Path -LiteralPath $msi2023) -or -not (Test-Path -LiteralPath $msi2024)) {
    throw "One or more MSI files were not generated."
}

Write-Host ""
Write-Host "MSI installers were generated successfully:"
Write-Host "  $outputRootPath"
