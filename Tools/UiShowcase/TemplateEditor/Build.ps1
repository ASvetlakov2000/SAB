param([switch]$Capture, [switch]$Open)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$python='C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'outputs\ui-production-preview\plugin\SAB.dll'))) {
    & $msbuild (Join-Path $projectRoot 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug /p:RevitVersion=2023 '/p:OutputPath=..\outputs\ui-production-preview\plugin\' '/p:BaseIntermediateOutputPath=..\outputs\ui-production-preview\obj\' '/p:IntermediateOutputPath=..\outputs\ui-production-preview\obj\Debug\' /v:quiet /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Shared SAB assembly build failed.' }
}
& $python (Join-Path $PSScriptRoot 'Prepare.py')
if ($LASTEXITCODE -ne 0) { throw 'Preview source contract failed.' }
& $msbuild (Join-Path $PSScriptRoot 'TemplateEditor.csproj') /t:Build /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Template editor preview build failed.' }
$exe=Join-Path $projectRoot 'outputs\ui-production-preview\template-editor\SAB.TemplateEditor.Preview.exe'
if ($Capture) {
    $run=Start-Process -FilePath $exe -ArgumentList '--capture' -PassThru -Wait -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Preview verification failed; see screenshots-template-editor/verification.txt.' }
}
if ($Open) { Start-Process -FilePath $exe -WindowStyle Hidden }
