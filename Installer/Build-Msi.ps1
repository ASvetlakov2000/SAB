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

function Get-FileRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + "\"
    $file = [System.IO.Path]::GetFullPath($FilePath)
    return $file.Substring($root.Length)
}

function New-StableGuid {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Seed
    )

    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Seed)
        $hash = $md5.ComputeHash($bytes)

        # RFC 4122 compatible version/variant bits.
        $hash[6] = ($hash[6] -band 0x0F) -bor 0x40
        $hash[8] = ($hash[8] -band 0x3F) -bor 0x80

        return [System.Guid]::new($hash).ToString()
    }
    finally {
        $md5.Dispose()
    }
}

function New-SafeId {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prefix,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $sanitized = [System.Text.RegularExpressions.Regex]::Replace($Value, "[^A-Za-z0-9_]", "_")

    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        $sanitized = "Item"
    }

    if ($sanitized.Length -gt 45) {
        $sanitized = $sanitized.Substring(0, 45)
    }

    $suffix = (New-StableGuid -Seed "$Prefix|$Value").Substring(0, 8)
    return "{0}_{1}_{2}" -f $Prefix, $sanitized, $suffix
}

function Escape-Xml {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

function Get-MsiVersionFromAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )

    if (-not (Test-Path -LiteralPath $AssemblyPath)) {
        return "1.0.0"
    }

    try {
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($AssemblyPath).FileVersion

        if ([string]::IsNullOrWhiteSpace($version)) {
            return "1.0.0"
        }

        $parsed = [System.Version]::Parse($version)
        $build = if ($parsed.Build -lt 0) { 0 } else { $parsed.Build }
        return "{0}.{1}.{2}" -f $parsed.Major, $parsed.Minor, $build
    }
    catch {
        return "1.0.0"
    }
}

function Ensure-WixTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolsFolder
    )

    $wixExePath = Join-Path $ToolsFolder "wix.exe"

    if (Test-Path -LiteralPath $wixExePath) {
        return $wixExePath
    }

    New-Item -ItemType Directory -Path $ToolsFolder -Force | Out-Null
    dotnet tool install --tool-path $ToolsFolder wix --version 4.0.5 | Out-Host

    if (-not (Test-Path -LiteralPath $wixExePath)) {
        throw "WiX tool was not installed successfully."
    }

    return $wixExePath
}

function Write-MsiSourceFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Year,
        [Parameter(Mandatory = $true)]
        [string]$UpgradeCode,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$BinRootPath,
        [Parameter(Mandatory = $true)]
        [string]$AddinFilePath,
        [Parameter(Mandatory = $true)]
        [string]$OutputWxsPath
    )

    $allFiles = Get-ChildItem -Path $BinRootPath -Recurse -File | Sort-Object FullName

    if ($allFiles.Count -eq 0) {
        throw "No files found in bin folder: $BinRootPath"
    }

    $directoryIdByRelativePath = @{}
    $directoryIdByRelativePath[""] = "PluginRootFolder"
    $directoryChildrenByParent = @{}

    foreach ($file in $allFiles) {
        $relativePath = Get-FileRelativePath -RootPath $BinRootPath -FilePath $file.FullName
        $relativeDirectoryPath = Split-Path -Path $relativePath -Parent

        if ($relativeDirectoryPath -eq "." -or [string]::IsNullOrWhiteSpace($relativeDirectoryPath)) {
            continue
        }

        $parts = $relativeDirectoryPath.Split('\')
        $currentRelative = ""

        foreach ($part in $parts) {
            if ([string]::IsNullOrWhiteSpace($part)) {
                continue
            }

            $nextRelative = if ([string]::IsNullOrWhiteSpace($currentRelative)) { $part } else { "$currentRelative\$part" }

            if (-not $directoryIdByRelativePath.ContainsKey($nextRelative)) {
                $directoryIdByRelativePath[$nextRelative] = New-SafeId -Prefix "Dir" -Value $nextRelative

                if (-not $directoryChildrenByParent.ContainsKey($currentRelative)) {
                    $directoryChildrenByParent[$currentRelative] = New-Object System.Collections.Generic.List[object]
                }

                $directoryChildrenByParent[$currentRelative].Add([PSCustomObject]@{
                    RelativePath = $nextRelative
                    Name = $part
                })
            }

            $currentRelative = $nextRelative
        }
    }

    $componentDefinitions = New-Object System.Collections.Generic.List[string]
    $componentRefs = New-Object System.Collections.Generic.List[string]

    foreach ($file in $allFiles) {
        $relativePath = Get-FileRelativePath -RootPath $BinRootPath -FilePath $file.FullName
        $relativeDirectoryPath = Split-Path -Path $relativePath -Parent

        if ($relativeDirectoryPath -eq "." -or [string]::IsNullOrWhiteSpace($relativeDirectoryPath)) {
            $relativeDirectoryPath = ""
        }

        $directoryId = $directoryIdByRelativePath[$relativeDirectoryPath]
        $componentId = New-SafeId -Prefix "Cmp" -Value "$Year|$relativePath"
        $componentGuid = New-StableGuid -Seed "$Year|$relativePath|component"
        $escapedSource = Escape-Xml -Value $file.FullName

        $componentDefinitions.Add("    <Component Id=""$componentId"" Guid=""$componentGuid"" Directory=""$directoryId"">")
        $componentDefinitions.Add("      <File Source=""$escapedSource"" KeyPath=""yes"" />")
        $componentDefinitions.Add("    </Component>")

        $componentRefs.Add("      <ComponentRef Id=""$componentId"" />")
    }

    $addinComponentId = "CmpAddin$Year"
    $addinComponentGuid = New-StableGuid -Seed "$Year|addin"
    $escapedAddinSource = Escape-Xml -Value $AddinFilePath

    $componentDefinitions.Add("    <Component Id=""$addinComponentId"" Guid=""$addinComponentGuid"" Directory=""AddinsYearFolder"">")
    $componentDefinitions.Add("      <File Source=""$escapedAddinSource"" KeyPath=""yes"" />")
    $componentDefinitions.Add("    </Component>")
    $componentRefs.Add("      <ComponentRef Id=""$addinComponentId"" />")

    $builder = New-Object System.Text.StringBuilder
    $null = $builder.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    $null = $builder.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
    $null = $builder.AppendLine("  <Package Name=""SAB Revit $Year"" Manufacturer=""SAB"" Version=""$Version"" UpgradeCode=""$UpgradeCode"" Scope=""perUser"" InstallerVersion=""500"" Language=""1049"">")
    $null = $builder.AppendLine('    <SummaryInformation Description="SAB Revit plugin installer" Manufacturer="SAB" />')
    $null = $builder.AppendLine('    <MajorUpgrade DowngradeErrorMessage="A newer version of SAB Revit plugin is already installed." />')
    $null = $builder.AppendLine('    <MediaTemplate EmbedCab="yes" />')
    $null = $builder.AppendLine('')
    $null = $builder.AppendLine('    <StandardDirectory Id="AppDataFolder">')
    $null = $builder.AppendLine('      <Directory Id="AutodeskFolder" Name="Autodesk">')
    $null = $builder.AppendLine('        <Directory Id="RevitFolder" Name="Revit">')
    $null = $builder.AppendLine('          <Directory Id="AddinsFolder" Name="Addins">')
    $null = $builder.AppendLine("            <Directory Id=""AddinsYearFolder"" Name=""$Year"">")
    $null = $builder.AppendLine('              <Directory Id="PluginRootFolder" Name="SAB">')

    function Append-DirectoryNodes {
        param(
            [Parameter(Mandatory = $true)]
            [AllowEmptyString()]
            [string]$ParentRelativePath,
            [Parameter(Mandatory = $true)]
            [int]$Indent
        )

        if (-not $directoryChildrenByParent.ContainsKey($ParentRelativePath)) {
            return
        }

        $children = $directoryChildrenByParent[$ParentRelativePath] | Sort-Object Name

        foreach ($child in $children) {
            $childDirectoryId = $directoryIdByRelativePath[$child.RelativePath]
            $escapedName = Escape-Xml -Value $child.Name
            $padding = " " * $Indent
            $null = $builder.AppendLine("$padding<Directory Id=""$childDirectoryId"" Name=""$escapedName"">")
            Append-DirectoryNodes -ParentRelativePath $child.RelativePath -Indent ($Indent + 2)
            $null = $builder.AppendLine("$padding</Directory>")
        }
    }

    Append-DirectoryNodes -ParentRelativePath "" -Indent 16

    $null = $builder.AppendLine('              </Directory>')
    $null = $builder.AppendLine('            </Directory>')
    $null = $builder.AppendLine('          </Directory>')
    $null = $builder.AppendLine('        </Directory>')
    $null = $builder.AppendLine('      </Directory>')
    $null = $builder.AppendLine('    </StandardDirectory>')
    $null = $builder.AppendLine('')

    foreach ($componentDefinitionLine in $componentDefinitions) {
        $null = $builder.AppendLine($componentDefinitionLine)
    }

    $null = $builder.AppendLine('')
    $null = $builder.AppendLine('    <Feature Id="MainFeature" Title="SAB Revit Plugin" Level="1">')

    foreach ($componentRefLine in $componentRefs) {
        $null = $builder.AppendLine($componentRefLine)
    }

    $null = $builder.AppendLine('    </Feature>')
    $null = $builder.AppendLine('  </Package>')
    $null = $builder.AppendLine('</Wix>')

    Set-Content -Path $OutputWxsPath -Value $builder.ToString() -Encoding UTF8
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
$binRootPath = Resolve-AbsolutePath -Path $BinFolder -BasePath $scriptRoot
$outputRootPath = Resolve-AbsolutePath -Path $OutputFolder -BasePath $scriptRoot
$toolsFolderPath = Join-Path $scriptRoot ".tools"
$wixSourceFolderPath = Join-Path $scriptRoot ".wxs"

if (-not (Test-Path -LiteralPath $binRootPath)) {
    throw "Bin folder was not found: $binRootPath"
}

$sabDllPath = Join-Path $binRootPath "SAB.dll"
$installerVersion = Get-MsiVersionFromAssembly -AssemblyPath $sabDllPath

New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null
New-Item -ItemType Directory -Path $wixSourceFolderPath -Force | Out-Null

$wixExePath = Ensure-WixTool -ToolsFolder $toolsFolderPath

$installerDefinitions = @(
    @{
        Year = "2023"
        Addin = (Join-Path $repositoryRoot "SAB_2023.addin")
        UpgradeCode = "F9D69961-6B30-4C1E-A469-0CC9C31EFD8E"
    },
    @{
        Year = "2024"
        Addin = (Join-Path $repositoryRoot "SAB_2024.addin")
        UpgradeCode = "D7A573D9-4A9A-42A1-8D0D-F552AEEA6B87"
    }
)

foreach ($definition in $installerDefinitions) {
    $year = [string]$definition.Year
    $addinFilePath = [string]$definition.Addin
    $upgradeCode = [string]$definition.UpgradeCode

    if (-not (Test-Path -LiteralPath $addinFilePath)) {
        throw "Addin file was not found for Revit ${year}: $addinFilePath"
    }

    $wxsPath = Join-Path $wixSourceFolderPath ("SAB_Revit_{0}.wxs" -f $year)
    $msiPath = Join-Path $outputRootPath ("SAB_Revit_{0}.msi" -f $year)

    Write-MsiSourceFile `
        -Year $year `
        -UpgradeCode $upgradeCode `
        -Version $installerVersion `
        -BinRootPath $binRootPath `
        -AddinFilePath $addinFilePath `
        -OutputWxsPath $wxsPath

    & $wixExePath build $wxsPath -arch x64 -o $msiPath | Out-Host

    if ($LASTEXITCODE -ne 0) {
        throw "MSI build failed for Revit $year."
    }
}

Write-Host ""
Write-Host "MSI installers were generated successfully:"
Write-Host "  $outputRootPath"
