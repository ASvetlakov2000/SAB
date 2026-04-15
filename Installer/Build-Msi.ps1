param(
    [string]$BinFolder = "..\SAB\bin\Debug",
    [string]$OutputFolder = ".\output"
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
$binRootPath = Resolve-AbsolutePath -Path $BinFolder -BasePath $scriptRoot
$outputRootPath = Resolve-AbsolutePath -Path $OutputFolder -BasePath $scriptRoot
$toolsRootPath = Join-Path $scriptRoot ".tools"

if (-not (Test-Path -LiteralPath $installerProjectPath)) {
    throw "Installer project was not found: $installerProjectPath"
}

if (-not (Test-Path -LiteralPath $binRootPath)) {
    throw "Bin folder was not found: $binRootPath"
}

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

# Блок установки локального WiX 4 для WixSharp.
$wixExePath = Ensure-LocalWixTool -ToolsFolder $toolsRootPath
$env:PATH = "$toolsRootPath;$env:PATH"
Ensure-WixUiExtension -WixExePath $wixExePath

# Блок запуска WixSharp-генератора MSI.
dotnet run --project $installerProjectPath -- `
    --root "$repositoryRoot" `
    --bin "$binRootPath" `
    --out "$outputRootPath"

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
