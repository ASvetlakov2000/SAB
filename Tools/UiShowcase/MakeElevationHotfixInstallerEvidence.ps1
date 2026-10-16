$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$reader = New-Object -ComObject WindowsInstaller.Installer
$packages = @()
$identities = @()
$installerSource = Get-Content (Join-Path $root 'Installer/WixSharpInstaller/Program.cs') -Raw
try {
    foreach ($year in @('2022','2023','2024')) {
        $msi = Join-Path $root "Installer/output/SAB_Revit_$year.msi"
        $db = $reader.OpenDatabase($msi, 0)
        $view = $db.OpenView('SELECT `Property`, `Value` FROM `Property`')
        try {
            $view.Execute(); $properties = @{}
            while ($row = $view.Fetch()) {
                $properties[$row.StringData(1)] = $row.StringData(2)
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($row)
            }
            $expectedUpgrade = [regex]::Match($installerSource, ('UpgradeCode'+$year+' = "([^"]+)"')).Groups[1].Value
            if ($properties.ProductVersion -ne '1.1.1' -or $properties.ProductName -ne "SAB Revit $year") { throw "Incorrect MSI version/name: $year" }
            if ($properties.UpgradeCode.Trim('{}') -ne $expectedUpgrade) { throw "Incorrect MSI upgrade identity: $year" }
            $identities += [ordered]@{ year=$year; ProductVersion=$properties.ProductVersion; ProductCode=$properties.ProductCode; UpgradeCode=$properties.UpgradeCode }
        }
        finally {
            $view.Close()
            foreach ($com in @($view,$db)) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($com) }
        }
        $proof = Get-Content (Join-Path $root "outputs/ui-production-preview/msi-check/$year/verification.json") -Raw | ConvertFrom-Json
        if ($proof.status -ne 'PASS' -or $proof.assemblyVersion -ne '1.1.1.0') { throw "Invalid extracted payload proof: $year" }
        if ((Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash -ne $proof.msiSha256) { throw "Stale MSI proof: $year" }
        $packages += $proof
    }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reader) }
& (Join-Path $root 'Tests/Instructions/verify-msi.ps1') -RepositoryRoot $root
[ordered]@{ date='2026-10-13'; version='1.1.1'; name='Исправления развёрток'; entryPoint='Build-Msi.bat'; exitCode=0; configuration='Release'; scope='Actual read-only room list, footer, scoped notification resources and safe result delivery; extracted CAB payload and correct API references for all three years'; packages=$packages; identities=$identities; installationPerformed=$false; unverified=@('Installed Revit operations/transactions/rollback/native selection','Native runtime/API22','Physical input/accessibility','OS DPI/monitor transitions') } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'Docs/Ui/DesignSystem/ElevationHotfix-Installer.json') -Encoding UTF8
Write-Host 'PASS hotfix MSI1.1.1: three extracted payloads, API references, product versions and stable upgrade identities; installation not performed.'
