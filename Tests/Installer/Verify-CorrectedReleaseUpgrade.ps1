param([string]$RepositoryRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
$reader = New-Object -ComObject WindowsInstaller.Installer
$results = @()
try {
    foreach ($year in @('2022','2023','2024')) {
        $path = Join-Path $RepositoryRoot "Installer\output\SAB_Revit_$year.msi"
        $database = $reader.OpenDatabase($path, 0)
        $views = @()
        try {
            $view = $database.OpenView('SELECT `Property`, `Value` FROM `Property`'); $views += $view; $view.Execute()
            $properties = @{}
            while ($row = $view.Fetch()) {
                $properties[$row.StringData(1)] = $row.StringData(2)
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($row)
            }
            if ($properties.ProductVersion -ne '1.1.1') { throw "${year}: expected corrected release1.1.1" }
            $view = $database.OpenView('SELECT `UpgradeCode`, `VersionMin`, `VersionMax`, `Attributes`, `ActionProperty` FROM `Upgrade`'); $views += $view; $view.Execute()
            $ranges = @()
            while ($row = $view.Fetch()) {
                $ranges += [pscustomobject]@{code=$row.StringData(1);minimum=$row.StringData(2);maximum=$row.StringData(3);attributes=$row.IntegerData(4);property=$row.StringData(5)}
                [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($row)
            }
            if ($ranges.Count -ne 4 -or @($ranges | Where-Object code -ne $properties.UpgradeCode).Count) { throw "${year}: invalid upgrade identity/range count" }
            foreach ($range in $ranges) {
                if ($properties.SecureCustomProperties.Split(';') -notcontains $range.property) { throw "${year}: insecure upgrade action property" }
            }
            $view = $database.OpenView('SELECT `Condition` FROM `LaunchCondition`'); $views += $view; $view.Execute()
            $conditions = @()
            while ($row = $view.Fetch()) { $conditions += $row.StringData(1); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($row) }
            if ($conditions -notcontains 'Installed OR (NOT SAB_NEWER_RELEASE AND NOT SAB_FUTURE_RELEASE)') { throw "${year}: missing newer-version guard" }
            $view = $database.OpenView('SELECT `Action`, `Sequence` FROM `InstallExecuteSequence`'); $views += $view; $view.Execute()
            $sequence = @{}
            while ($row = $view.Fetch()) { $sequence[$row.StringData(1)]=$row.IntegerData(2); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($row) }
            if ($sequence.RemoveExistingProducts -le $sequence.InstallInitialize -or $sequence.RemoveExistingProducts -ge $sequence.InstallFiles) { throw "${year}: replacement must run inside transaction before file installation" }
            $cases = @()
            foreach ($case in @(@('1.0.3','replace'),@('1.1.0','replace'),@('1.1.1','replace'),@('1.1.2','block'),@('1.1.3','block'),@('1.1.4','replace'),@('1.1.5','block'),@('1.2.0','block'),@('2.0.0','block'))) {
                $candidate = [version]$case[0]
                $matched = @($ranges | Where-Object {
                    $minimumMatches = -not $_.minimum -or $candidate -gt [version]$_.minimum -or ($candidate -eq [version]$_.minimum -and ($_.attributes -band 256))
                    $maximumMatches = -not $_.maximum -or $candidate -lt [version]$_.maximum -or ($candidate -eq [version]$_.maximum -and ($_.attributes -band 512))
                    $minimumMatches -and $maximumMatches
                })
                $outcome = if (@($matched | Where-Object { $_.attributes -band 2 }).Count) { 'block' } elseif ($matched.Count) { 'replace' } else { 'none' }
                if ($outcome -ne $case[1]) { throw "${year}: version $candidate incorrectly resolves to $outcome" }
                $cases += [ordered]@{installedVersion=$case[0];outcome=$outcome;status='PASS'}
            }
            $results += [ordered]@{year=$year;status='PASS';msiSha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();upgradeRanges=$ranges;removeExistingProductsSequence=$sequence.RemoveExistingProducts;cases=$cases}
            Write-Host "PASS ${year}: actual MSI upgrade table,9version boundaries, secure properties and transactional replacement schedule"
        } finally {
            foreach ($view in $views) { $view.Close(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) }
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
        }
    }
} finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reader) }
$report = [ordered]@{status='PASS';installationPerformed=$false;boundary='actual compiled MSI tables, not executed installation';results=$results}
[IO.File]::WriteAllText((Join-Path $RepositoryRoot 'outputs\ui-production-preview\corrected-release-upgrade.json'),($report | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
