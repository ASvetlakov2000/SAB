param([switch]$Capture, [switch]$AllTargets)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$python='C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$years=if($AllTargets){@(2022,2023,2024)}else{@(2023)}
foreach($year in $years) {
    $suffix=if($year -eq 2023){'production'}else{"production-$year"}
    $targetOutput=Join-Path $projectRoot "outputs\ui-production-preview\template-editor\$suffix"
    $targetIntermediate=Join-Path $projectRoot "outputs\ui-production-preview\template-editor\production-obj-$year"
    $apiDirectory=if($year -eq 2022){'C:\Users\svetl\Desktop\Codex\lib\Revit2022'}else{"C:\Program Files\Autodesk\Revit $year"}
    & $msbuild (Join-Path $projectRoot 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug "/p:RevitVersion=$year" "/p:RevitApiDirectory=$apiDirectory" "/p:OutputPath=$targetOutput\" "/p:BaseIntermediateOutputPath=$targetIntermediate\" "/p:IntermediateOutputPath=$targetIntermediate\Debug\" /v:quiet /nologo
    if($LASTEXITCODE -ne 0){throw "Production build failed for API$year"}
}
if($Capture) {
    & $python (Join-Path $PSScriptRoot 'PrepareProductionCheck.py')
    if($LASTEXITCODE -ne 0){throw 'Production harness preparation failed'}
    & $msbuild (Join-Path $PSScriptRoot 'ProductionCheck.csproj') /t:Build /v:quiet /nologo
    if($LASTEXITCODE -ne 0){throw 'Production harness build failed'}
    $exe=Join-Path $projectRoot 'outputs\ui-production-preview\template-editor\production\SAB.TemplateEditor.ProductionCheck.exe'
    $run=Start-Process -FilePath $exe -PassThru -Wait -WindowStyle Hidden
    if($run.ExitCode -ne 0){throw 'Actual SAB.dll WPF verification failed; see production-error.txt and screenshots-template-editor-production/verification.txt'}
}
