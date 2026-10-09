param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath($RepositoryRoot)
$docs = Join-Path $repository 'Docs\PluginInstructions'
$guides = @(Get-ChildItem -LiteralPath $docs -Filter '*.html' -File)
if ($guides.Count -ne 23) { throw 'Expected catalog and 22 guides.' }
$reader = New-Object -ComObject WindowsInstaller.Installer
foreach ($year in @('2022', '2023', '2024')) {
    $msi = Join-Path $repository "Installer\output\SAB_Revit_$year.msi"
    $database = $reader.OpenDatabase($msi, 0)
    $view = $database.OpenView('SELECT FileName, FileSize FROM File')
    $view.Execute()
    $files = @{}
    while ($record = $view.Fetch()) {
        $name = $record.StringData(1).Split('|')[-1]
        $files[$name] = $record.IntegerData(2)
    }
    $view.Close()
    foreach ($guide in $guides) {
        if (!$files.ContainsKey($guide.Name) -or $files[$guide.Name] -ne $guide.Length) {
            throw "$year MSI: missing or outdated guide $($guide.Name)"
        }
    }
    foreach ($required in @('SAB.dll', 'Nice3point.Revit.Toolkit.dll', 'template.css', 'compact.css')) {
        if (!$files.ContainsKey($required)) { throw "$year MSI: missing $required" }
    }
    $compactStyle = Get-Item -LiteralPath (Join-Path $docs 'assets\compact.css')
    if ($files['compact.css'] -ne $compactStyle.Length) { throw "$year MSI: outdated compact styles" }
    # The dashboard has its own runtime HTML template; it is not a user guide.
    $html = @($files.Keys | Where-Object { $_.EndsWith('.html') -and $_ -ne 'dashboard_template.html' })
    if ($html.Count -ne 23 -or @($html | Where-Object { !$_.StartsWith('SAB_HTML_') }).Count -gt 0) {
        throw "$year MSI: unexpected HTML documents or branding"
    }
    foreach ($hostDll in @('RevitAPI.dll', 'RevitAPIUI.dll', 'RevitDBAPI.dll', 'UtilityAPI.dll')) {
        if ($files.ContainsKey($hostDll)) { throw "$year MSI includes host library $hostDll" }
    }
    Write-Output "PASS: Revit $year MSI contains current SAB catalog, all 22 guides, CSS and Toolkit; no host libraries."
    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
}
[void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($reader)
