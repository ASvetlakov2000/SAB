param([ValidateSet('2023','2024','Both')][string]$Version='Both')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$versions=if($Version -eq 'Both') { @('2023','2024') } else { @($Version) }
foreach($v in $versions) {
 $root=Join-Path $env:APPDATA "Autodesk/Revit/Addins/$v"
 $manifest=Join-Path $root "SAB_$v.addin"
 $build=Join-Path $repo "SAB/bin/Notifications$v"
 if(!(Test-Path -LiteralPath "$build/SAB.dll")) { throw "Missing build: $build" }
 [xml]$xml=Get-Content -LiteralPath $manifest -Raw
 $entry=@($xml.RevitAddIns.AddIn) | Where-Object { $_.FullClassName -eq 'SAB.MainPanel' }
 if(@($entry).Count -ne 1) { throw "Expected one SAB.MainPanel in $manifest" }
 $previous=[string]$entry.Assembly
 $old=if([IO.Path]::IsPathRooted($previous)) { $previous } else { Join-Path $root $previous }
 $oldFolder=Split-Path $old -Parent
 $suffix=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
 $destination=Join-Path $root "SAB_Notifications_$suffix"
 New-Item -ItemType Directory -Path $destination | Out-Null
 # Preserve all existing content and dependencies. Loaded assemblies remain untouched.
 Copy-Item -Path (Join-Path $oldFolder '*') -Destination $destination -Recurse -Force
 Copy-Item -Path (Join-Path $build '*') -Destination $destination -Recurse -Force
 $installed=Join-Path $destination 'SAB.dll'
 if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$build/SAB.dll").Hash) { throw 'Assembly verification failed' }
 if(!(Test-Path -LiteralPath "$destination/UI/Styles/SABWindowStyles.xaml")) { throw 'SAB styles missing' }
 Copy-Item -LiteralPath $manifest -Destination "$manifest.before-notifications-$suffix.bak"
 $entry.SelectSingleNode("Assembly").InnerText=[string]$installed
 $xml.Save($manifest)
 Write-Output "Revit $v -> $installed (next launch)"
}

