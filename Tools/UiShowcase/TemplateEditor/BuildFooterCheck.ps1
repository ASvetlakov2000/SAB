param([switch]$Capture)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$output=Join-Path $root 'outputs\ui-production-preview\template-editor\footer'
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$python='C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $msbuild (Join-Path $root 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug /p:RevitVersion=2023 "/p:OutputPath=$output\plugin\" "/p:BaseIntermediateOutputPath=$output\obj\" "/p:IntermediateOutputPath=$output\obj\" /v:quiet /nologo
if($LASTEXITCODE -ne 0){throw 'Footer plugin build failed'}
& $python (Join-Path $PSScriptRoot 'PrepareFooterCheck.py')
if($LASTEXITCODE -ne 0){throw 'Footer fixture preparation failed'}
& $msbuild (Join-Path $PSScriptRoot 'FooterCheck.csproj') /t:Build /v:quiet /nologo
if($LASTEXITCODE -ne 0){throw 'Footer actual-DLL host build failed'}
if($Capture) {
    $run=Start-Process -FilePath (Join-Path $output 'plugin\SAB.TemplateEditor.FooterCheck.exe') -Wait -PassThru -WindowStyle Hidden
    if($run.ExitCode -ne 0){throw "Footer native capture failed: $output\error.txt"}
}
