param(
    [Parameter(Mandatory=$true)][string]$MsiPath,
    [Parameter(Mandatory=$true)][string]$BinFolder,
    [Parameter(Mandatory=$true)][ValidateSet('2022','2023','2024')][string]$Year
)
$ErrorActionPreference = 'Stop'
function Get-PayloadHash([string]$Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$work = Join-Path $repositoryRoot "outputs\ui-production-preview\msi-check\$Year"
New-Item -ItemType Directory -Path $work -Force | Out-Null
if (-not ('SabMsiPayloadReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class SabMsiPayloadReader {
    [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiOpenDatabase(string path, IntPtr mode, out uint handle);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiDatabaseOpenView(uint db, string query, out uint view);
    [DllImport("msi.dll")] static extern uint MsiViewExecute(uint view, uint record);
    [DllImport("msi.dll")] static extern uint MsiViewFetch(uint view, out uint record);
    [DllImport("msi.dll")] static extern uint MsiRecordReadStream(uint record, uint field, byte[] data, ref uint count);
    [DllImport("msi.dll")] static extern uint MsiCloseHandle(uint handle);
    static void Check(uint code) { if(code != 0) throw new InvalidOperationException("MSI read error: "+code); }
    public static void Extract(string path,string name,string target) {
        uint db=0,view=0,record=0;
        try {
            Check(MsiOpenDatabase(path,IntPtr.Zero,out db));
            Check(MsiDatabaseOpenView(db,"SELECT `Data` FROM `_Streams` WHERE `Name`='"+name.Replace("'","''")+"'",out view));
            Check(MsiViewExecute(view,0));Check(MsiViewFetch(view,out record));
            using(var file=File.Create(target)) { var bytes=new byte[65536]; while(true) { uint count=(uint)bytes.Length;Check(MsiRecordReadStream(record,1,bytes,ref count));if(count==0)break;file.Write(bytes,0,(int)count); } }
        } finally { if(record!=0)MsiCloseHandle(record);if(view!=0)MsiCloseHandle(view);if(db!=0)MsiCloseHandle(db); }
    }
}
'@
}
$reader = New-Object -ComObject WindowsInstaller.Installer
$database = $reader.OpenDatabase([IO.Path]::GetFullPath($MsiPath), 0)
$fileView = $null; $mediaView = $null; $propertyView = $null
try {
    $propertyView = $database.OpenView('SELECT `Property`, `Value` FROM `Property`'); $propertyView.Execute(); $properties = @{}
    while ($record = $propertyView.Fetch()) {
        $properties[$record.StringData(1)] = $record.StringData(2)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
    }
    $versionSource = Get-Content (Join-Path $repositoryRoot 'SAB/Properties/AssemblyInfo.cs') -Raw
    $expectedVersion = [regex]::Match($versionSource, 'AssemblyFileVersion\("(\d+\.\d+\.\d+)\.\d+"\)').Groups[1].Value
    $installerSource = Get-Content (Join-Path $repositoryRoot 'Installer/WixSharpInstaller/Program.cs') -Raw
    $expectedUpgrade = [regex]::Match($installerSource, ('UpgradeCode'+$Year+' = "([^"]+)"')).Groups[1].Value
    if ($properties.ProductVersion -ne $expectedVersion -or $properties.ProductName -ne "SAB Revit $Year") { throw "$Year MSI version/name differs from current source" }
    if ($properties.UpgradeCode.Trim('{}') -ne $expectedUpgrade) { throw "$Year MSI has incorrect upgrade identity" }
    $fileView = $database.OpenView('SELECT File, FileName, FileSize FROM File'); $fileView.Execute()
    $files = @{}
    while ($record = $fileView.Fetch()) {
        $name = $record.StringData(2).Split('|')[-1]
        if ($name -in @('RevitAPI.dll','RevitAPIUI.dll','RevitDBAPI.dll','UtilityAPI.dll')) { throw "Host API library in $Year MSI: $name" }
        if ($name -in @('SAB.UI.Preview.exe','verification.txt','error.txt')) { throw "Test artifact in $Year MSI: $name" }
        $files[$name] = [ordered]@{ id=$record.StringData(1); size=$record.IntegerData(3) }
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
    }
    # Reuse the canonical work directory without accepting files from an earlier extraction.
    $workFull = [IO.Path]::GetFullPath($work).TrimEnd('\')
    $expectedWork = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "outputs\ui-production-preview\msi-check\$Year")).TrimEnd('\')
    if (-not $workFull.Equals($expectedWork, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected MSI verification directory' }
    foreach ($file in $files.Values) {
        $extractedPath = [IO.Path]::GetFullPath((Join-Path $workFull $file.id))
        if (-not ([IO.Path]::GetDirectoryName($extractedPath)).Equals($workFull, [StringComparison]::OrdinalIgnoreCase)) { throw 'MSI file ID escapes verification directory' }
        if (Test-Path -LiteralPath $extractedPath -PathType Leaf) { Remove-Item -LiteralPath $extractedPath -Force }
    }
    $mediaView = $database.OpenView('SELECT Cabinet FROM Media'); $mediaView.Execute()
    while ($record = $mediaView.Fetch()) {
        $cabinet = $record.StringData(1)
        if (-not $cabinet.StartsWith('#')) { throw "Expected embedded cabinet in $Year MSI" }
        $cabinetPath = Join-Path $work $cabinet.Substring(1)
        [SabMsiPayloadReader]::Extract([IO.Path]::GetFullPath($MsiPath), $cabinet.Substring(1), $cabinetPath)
        & "$env:SystemRoot\System32\expand.exe" '-F:*' $cabinetPath $work | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Cannot extract $Year MSI cabinet" }
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
    }
    $verified = @()
    foreach ($style in @('SABFluentLight.xaml','SABWindowStyles.xaml')) {
        $builtStyle = Join-Path $BinFolder "UI\Styles\$style"
        $sourceStyle = Join-Path $repositoryRoot "SAB\UI\Styles\$style"
        if ((Get-PayloadHash $builtStyle) -ne (Get-PayloadHash $sourceStyle)) { throw "$Year build contains outdated source style: $style" }
    }
    $required = @{'SAB.dll'=(Join-Path $BinFolder 'SAB.dll'); 'SABFluentLight.xaml'=(Join-Path $BinFolder 'UI\Styles\SABFluentLight.xaml'); 'SABWindowStyles.xaml'=(Join-Path $BinFolder 'UI\Styles\SABWindowStyles.xaml'); 'Nice3point.Revit.Toolkit.dll'=(Join-Path $BinFolder 'Nice3point.Revit.Toolkit.dll')}
    foreach ($module in @('Cls_InteriorElevations','Cls_CreateViewsAndSheets')) {
        foreach ($sourceView in (Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "SAB\$module\Views") -Filter '*.xaml' -File)) {
            $builtView = Join-Path $BinFolder "$module\Views\$($sourceView.Name)"
            if ((Get-PayloadHash $builtView) -ne (Get-PayloadHash $sourceView.FullName)) { throw "$Year build contains outdated XAML: $($sourceView.Name)" }
            $required[$sourceView.Name] = $builtView
        }
    }
    foreach ($guide in (Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'Docs\PluginInstructions') -File)) { $required[$guide.Name] = $guide.FullName }
    $familyYear = if ($Year -eq '2022') { '2022' } else { '2023-2024' }
    if (-not $files.ContainsKey('families.manifest.tsv')) { throw "$Year MSI missing family manifest" }
    $previewRelativePath = "CreateInteriorElevationsCommand\$familyYear\SAB_Развертки_Направление.rfa"
    $previewEntries = @(Get-Content -LiteralPath (Join-Path $work $files['families.manifest.tsv'].id) | ForEach-Object {
        $parts = $_.Split([char]9)
        if ($parts.Length -eq 2 -and $parts[1] -eq $previewRelativePath) { $parts[0] }
    })
    if ($previewEntries.Count -ne 1) { throw "$Year MSI must contain one direction-preview family in its manifest" }
    $required[[IO.Path]::GetFileName($previewEntries[0])] = Join-Path $repositoryRoot "SAB\Families for Plugin\$previewRelativePath"
    foreach ($name in $required.Keys) {
        if (-not $files.ContainsKey($name)) { throw "$Year MSI missing $name" }
        $packaged = Join-Path $work $files[$name].id
        if (-not (Test-Path -LiteralPath $packaged -PathType Leaf)) { throw "$Year cabinet missing $name" }
        $expected = Get-PayloadHash $required[$name]
        if ((Get-PayloadHash $packaged) -ne $expected) { throw "$Year MSI stale payload: $name" }
        $verified += [ordered]@{ name=$name; sha256=$expected }
    }
    $assemblyPath = Join-Path $work $files['SAB.dll'].id
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($assemblyPath)
    $api = @($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -in @('RevitAPI','RevitAPIUI') })
    if ($api.Count -ne 2 -or @($api | Where-Object { $_.Version.Major -ne ([int]$Year-2000) }).Count -gt 0) { throw "$Year payload has incorrect Revit API references" }
    $metadata = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($assemblyPath))
    foreach ($type in @('ElevationDirectionPreviewWindow','ElevationDirectionPreviewController','ElevationDirectionPreviewService','ElevationSelectionGuidance','ContourDirectionGeometry','ContourLineOrientationService','get_PlannedDetailNumber')) { if (-not $metadata.Contains($type)) { throw "$Year MSI missing direction preview component: $type" } }
    foreach ($type in @('NotificationCard','NotificationStack','ParameterNotifications','CategorySelectionControl','CreateViewsAndSheetsWindow','ElevationCreationNotificationData','BuildNotification','get_PlaceFloorSpots','get_PlaceCeilingSpots','get_PlaceSlabSpots','ElevationAnnotationOwnershipService','ElevationSelectionListWindow','ElevationSelectionListItem','DeliverNotificationSafely','CheckSettingsCommand','CheckSettings','CheckCategories','ArmSearch')) { if (-not $metadata.Contains($type)) { throw "$Year MSI missing compiled UI component: $type" } }
    [ordered]@{ year=$Year; status='PASS'; productVersion=$properties.ProductVersion; productCode=$properties.ProductCode; upgradeCode=$properties.UpgradeCode; msi=[IO.Path]::GetFullPath($MsiPath); msiSha256=(Get-PayloadHash $MsiPath); sourceBin=[IO.Path]::GetFullPath($BinFolder); assemblyVersion=$assembly.GetName().Version.ToString(); apiReferences=@($api | ForEach-Object { $_.FullName }); verifiedPayload=$verified; installationPerformed=$false } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $work 'verification.json') -Encoding UTF8
    Write-Host "PASS Revit $Year MSI: extracted DLL, Fluent styles, Toolkit and all instructions match current build; notifications compiled; API references correct."
}
finally {
    foreach ($com in @($propertyView,$fileView,$mediaView,$database,$reader)) { if ($null -ne $com) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($com) } }
}
