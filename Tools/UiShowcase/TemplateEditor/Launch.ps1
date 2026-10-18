$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$exe=Join-Path $projectRoot 'outputs\ui-production-preview\template-editor\SAB.TemplateEditor.Preview.exe'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') -Open; return }
Start-Process -FilePath $exe -WindowStyle Hidden
