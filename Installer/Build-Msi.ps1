param(
    [string]$BinFolder = "..\SAB\bin\Debug",
    [string]$SyncReminderBinFolder = "",
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

function Test-RevitApiReferenceFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FolderPath
    )

    if ([string]::IsNullOrWhiteSpace($FolderPath)) {
        return $false
    }

    $revitApiPath = Join-Path $FolderPath "RevitAPI.dll"
    $revitApiUiPath = Join-Path $FolderPath "RevitAPIUI.dll"

    return (Test-Path -LiteralPath $revitApiPath) -and (Test-Path -LiteralPath $revitApiUiPath)
}

function Resolve-RevitApiReferenceFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$BinRootPath
    )

    $candidateFolders = New-Object System.Collections.Generic.List[string]
    $candidateFolders.Add((Resolve-AbsolutePath -Path "..\..\lib" -BasePath $RepositoryRoot))
    $candidateFolders.Add((Resolve-AbsolutePath -Path "..\lib" -BasePath $RepositoryRoot))
    $candidateFolders.Add((Resolve-AbsolutePath -Path ".\lib" -BasePath $RepositoryRoot))
    $candidateFolders.Add($BinRootPath)
    $candidateFolders.Add((Resolve-AbsolutePath -Path ".\SAB\bin\Debug" -BasePath $RepositoryRoot))
    $candidateFolders.Add((Resolve-AbsolutePath -Path ".\SAB\bin\Release" -BasePath $RepositoryRoot))

    $autodeskFolder = Join-Path $env:ProgramFiles "Autodesk"
    if (Test-Path -LiteralPath $autodeskFolder) {
        $revitFolders = Get-ChildItem -LiteralPath $autodeskFolder -Directory -Filter "Revit *" -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending

        foreach ($revitFolder in $revitFolders) {
            $candidateFolders.Add($revitFolder.FullName)
        }
    }

    foreach ($candidateFolder in $candidateFolders) {
        if (Test-RevitApiReferenceFolder -FolderPath $candidateFolder) {
            return [System.IO.Path]::GetFullPath($candidateFolder)
        }
    }

    throw "RevitAPI.dll and RevitAPIUI.dll were not found. Put them in a shared lib folder, near SAB bin, or install Revit on this machine."
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
$syncReminderBinRootPath = ""
$syncReminderAssemblyPath = ""
if (-not [string]::IsNullOrWhiteSpace($SyncReminderBinFolder)) {
    $syncReminderBinRootPath = Resolve-AbsolutePath -Path $SyncReminderBinFolder -BasePath $scriptRoot
    $syncReminderAssemblyPath = Join-Path $syncReminderBinRootPath "SyncReminderTest.dll"
}
$outputRootPath = Resolve-AbsolutePath -Path $OutputFolder -BasePath $scriptRoot
$toolsRootPath = Join-Path $scriptRoot ".tools"

if (-not (Test-Path -LiteralPath $installerProjectPath)) {
    throw "Installer project was not found: $installerProjectPath"
}

if (-not (Test-Path -LiteralPath $binRootPath)) {
    throw "Bin folder was not found: $binRootPath"
}

if ((-not [string]::IsNullOrWhiteSpace($syncReminderAssemblyPath)) -and
    (-not (Test-Path -LiteralPath $syncReminderAssemblyPath)) -and
    (Test-Path -LiteralPath $syncReminderProjectPath)) {
    $revitApiReferenceFolder = Resolve-RevitApiReferenceFolder -RepositoryRoot $repositoryRoot -BinRootPath $binRootPath
    $revitApiDllPath = Join-Path $revitApiReferenceFolder "RevitAPI.dll"
    $revitApiUiDllPath = Join-Path $revitApiReferenceFolder "RevitAPIUI.dll"

    Write-Host "SyncReminderTest DLL was not found. Building test plugin:"
    Write-Host "  $syncReminderProjectPath"
    Write-Host "Using Revit API references:"
    Write-Host "  $revitApiReferenceFolder"

    dotnet build "$syncReminderProjectPath" `
        --configuration Debug `
        --property:Platform=AnyCPU `
        --property:RevitApiDll="$revitApiDllPath" `
        --property:RevitApiUiDll="$revitApiUiDllPath" | Out-Host

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

if ((-not [string]::IsNullOrWhiteSpace($syncReminderBinRootPath)) -and
    (Test-Path -LiteralPath $syncReminderBinRootPath)) {
    $installerArguments += @("--sync-reminder-bin", "$syncReminderBinRootPath")
    Write-Host "SyncReminderTest will be included:"
    Write-Host "  $syncReminderBinRootPath"
}
else {
    Write-Host "SyncReminderTest is not included as a separate add-in."
    Write-Host "Sync reminder is now built into SAB.dll."
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
