param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$InstallerVersion = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "SAB\SAB.csproj"
$installerScriptPath = Join-Path $scriptRoot "Build-Msi.ps1"
$installerOutputPath = Join-Path $scriptRoot "output"
$families2022SourcePath = Join-Path $repositoryRoot "SAB\Families for Plugin\CreateInteriorElevationsCommand\2022"
$familiesSharedSourcePath = Join-Path $repositoryRoot "SAB\Families for Plugin\CreateInteriorElevationsCommand\2023-2024"
$requiredFamilyFileNames = @(
    "SAB_Марка угла_План.rfa",
    "SAB_Марка угла_Развертки.rfa"
)

foreach ($filePath in @($projectPath, $installerScriptPath)) {
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Required build file was not found: $filePath"
    }
}

$msBuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
$msBuildPath = if ($msBuildCommand) {
    $msBuildCommand.Source
}
else {
    "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
}

if (-not (Test-Path -LiteralPath $msBuildPath -PathType Leaf)) {
    throw "MSBuild.exe was not found."
}

function Resolve-RevitApiDirectory {
    param([string]$Year)

    $candidates = @((Join-Path $env:ProgramFiles "Autodesk\Revit $Year"))
    if ($Year -eq "2022") {
        $candidates += Join-Path $scriptRoot ".tools\Revit2022Api"
    }

    $expectedMajorVersion = [int]$Year - 2000
    foreach ($candidate in $candidates) {
        $revitApiPath = Join-Path $candidate "RevitAPI.dll"
        $revitApiUiPath = Join-Path $candidate "RevitAPIUI.dll"
        if (-not (Test-Path -LiteralPath $revitApiPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $revitApiUiPath -PathType Leaf)) {
            continue
        }

        $actualMajorVersion = (Get-Item -LiteralPath $revitApiPath).VersionInfo.FileMajorPart
        if ($actualMajorVersion -ne $expectedMajorVersion) {
            throw "Expected Revit $Year API (version $expectedMajorVersion), found version $actualMajorVersion in: $candidate"
        }

        return $candidate
    }

    throw "Revit $Year API assemblies were not found. Checked: $($candidates -join ', ')"
}

function Assert-InteriorElevationFamilies {
    param(
        [string]$SourcePath,
        [string]$ExpectedFormat,
        [string]$RevitApiDirectory
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "Interior elevation families folder was not found: $SourcePath"
    }

    foreach ($familyFileName in $requiredFamilyFileNames) {
        $familyFilePath = Join-Path $SourcePath $familyFileName
        if (-not (Test-Path -LiteralPath $familyFilePath -PathType Leaf)) {
            throw "Required Revit $ExpectedFormat family was not found: $familyFilePath"
        }
    }

    $formatReaderCandidates = @(
        (Join-Path $env:ProgramFiles "Autodesk\Revit 2023\RevitAPI.dll"),
        (Join-Path $env:ProgramFiles "Autodesk\Revit 2024\RevitAPI.dll"),
        (Join-Path $RevitApiDirectory "RevitAPI.dll")
    )
    $formatReaderLoaded = $false
    foreach ($formatReaderPath in $formatReaderCandidates) {
        if (-not (Test-Path -LiteralPath $formatReaderPath -PathType Leaf)) {
            continue
        }

        try {
            Add-Type -Path $formatReaderPath -ErrorAction Stop
            $formatReaderLoaded = $true
            break
        }
        catch {
            # Reference-only API packages cannot inspect Revit files at runtime.
        }
    }

    if (-not $formatReaderLoaded) {
        throw "A Revit installation with a loadable RevitAPI.dll is required to verify family file versions."
    }

    foreach ($familyFile in (Get-ChildItem -LiteralPath $SourcePath -Filter "*.rfa" -File)) {
        $familyFormat = [Autodesk.Revit.DB.BasicFileInfo]::Extract($familyFile.FullName).Format
        if ($familyFormat -ne $ExpectedFormat) {
            throw "Expected a Revit $ExpectedFormat family, found format $familyFormat in: $($familyFile.FullName)"
        }
    }
}

foreach ($year in @("2022", "2023", "2024")) {
    Write-Host ""
    Write-Host "Building SAB and MSI for Revit $year..."

    $revitApiDirectory = Resolve-RevitApiDirectory -Year $year
    $buildOutputPath = Join-Path $repositoryRoot "SAB\bin\Revit$year"
    New-Item -ItemType Directory -Path $buildOutputPath -Force | Out-Null

    $familySourcePath = if ($year -eq "2022") { $families2022SourcePath } else { $familiesSharedSourcePath }
    $expectedFamilyFormat = if ($year -eq "2022") { "2022" } else { "2023" }
    Assert-InteriorElevationFamilies `
        -SourcePath $familySourcePath `
        -ExpectedFormat $expectedFamilyFormat `
        -RevitApiDirectory $revitApiDirectory

    $familiesTargetPath = [System.IO.Path]::GetFullPath(
        (Join-Path $buildOutputPath "Families for Plugin\CreateInteriorElevationsCommand"))
    $buildOutputFullPath = [System.IO.Path]::GetFullPath($buildOutputPath).TrimEnd('\')
    if (-not $familiesTargetPath.StartsWith(
        $buildOutputFullPath + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Families output path is outside the build folder: $familiesTargetPath"
    }

    if (Test-Path -LiteralPath $familiesTargetPath -PathType Container) {
        Get-ChildItem -LiteralPath $familiesTargetPath -Filter "*.rfa" -Recurse -File |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }

    & $msBuildPath $projectPath `
        /t:Rebuild `
        /m `
        /nologo `
        /v:minimal `
        "/p:Configuration=$Configuration" `
        /p:PlatformTarget=x64 `
        "/p:RevitVersion=$year" `
        "/p:RevitApiDirectory=$revitApiDirectory" `
        "/p:OutputPath=$buildOutputPath\"

    if ($LASTEXITCODE -ne 0) {
        throw "SAB build for Revit $year failed."
    }

    $assemblyPath = Join-Path $buildOutputPath "SAB.dll"
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "SAB.dll was not generated: $assemblyPath"
    }

    if ($year -eq "2022") {
        $families2022TargetPath = Join-Path $familiesTargetPath "2022"
        New-Item -ItemType Directory -Path $families2022TargetPath -Force | Out-Null
        foreach ($familyFile in (Get-ChildItem -LiteralPath $families2022SourcePath -Filter "*.rfa" -File)) {
            Copy-Item -LiteralPath $familyFile.FullName `
                -Destination (Join-Path $families2022TargetPath $familyFile.Name) -Force
        }
    }

    & $installerScriptPath `
        -BinFolder $buildOutputPath `
        -OutputFolder $installerOutputPath `
        -Years @($year) `
        -InstallerVersion $InstallerVersion

    if ($LASTEXITCODE -ne 0) {
        throw "SAB installer build for Revit $year failed."
    }

    $msiPath = Join-Path $installerOutputPath "SAB_Revit_$year.msi"
    if (-not (Test-Path -LiteralPath $msiPath -PathType Leaf)) {
        throw "MSI file was not generated: $msiPath"
    }

    Write-Host "Built: $msiPath"
}

Write-Host ""
Write-Host "All Revit installers were built successfully."
