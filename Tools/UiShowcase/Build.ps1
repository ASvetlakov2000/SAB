param([switch]$Capture, [switch]$Open, [switch]$Notifications, [switch]$Elevations, [switch]$Redesign)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot | Split-Path -Parent
if ($Open -and -not $Capture -and -not $Notifications) {
    & (Join-Path $PSScriptRoot 'LaunchPreview.ps1') -Elevations:$Elevations
    return
}
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$python='C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $python (Join-Path $PSScriptRoot 'GenerateSourceViews.py')
if ($LASTEXITCODE -ne 0) { throw 'Production UI source contract failed.' }
& $python (Join-Path $PSScriptRoot 'GenerateElevationPreview.py')
if ($LASTEXITCODE -ne 0) { throw 'Isolated elevations preview source contract failed.' }
# Actual production classes, markup and resources. No presentation substitutions.
& $msbuild (Join-Path $projectRoot 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug /p:RevitVersion=2023 '/p:OutputPath=..\outputs\ui-production-preview\plugin\' '/p:BaseIntermediateOutputPath=..\outputs\ui-production-preview\obj\' '/p:IntermediateOutputPath=..\outputs\ui-production-preview\obj\Debug\' /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Production SAB build failed.' }
$plugin=Join-Path $projectRoot 'outputs\ui-production-preview\plugin'
& $msbuild (Join-Path $PSScriptRoot 'Sab.UiShowcase.csproj') /t:Build /p:Configuration=Debug /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'WPF host build failed.' }
$previewExe=Join-Path $plugin 'SAB.UI.Preview.exe'
if ($Redesign) {
    foreach ($case in @(@('--capture-redesign','production'),@('--notifications','notifications'),@('--capture','modules'),@('--capture-elevations','sessions'))) {
        $capturePath=Join-Path $projectRoot ('Docs\Ui\DesignSystem\screenshots-redesign-'+$case[1])
        $run=Start-Process -FilePath $previewExe -ArgumentList @($case[0],'--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
        if ($run.ExitCode -ne 0) { throw ('Redesign capture failed: '+$capturePath) }
    }
    & $python (Join-Path $PSScriptRoot 'MakeRedesignEvidence.py')
    if ($LASTEXITCODE -ne 0) { throw 'Redesign evidence verification failed.' }
    return
}
if ($Notifications) {
    $notificationPath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-notifications'
    $notificationRun=Start-Process -FilePath $previewExe -ArgumentList @('--notifications','--out',('"'+$notificationPath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($notificationRun.ExitCode -ne 0) { throw 'Notification WPF verification failed.' }
    & $python (Join-Path $PSScriptRoot 'MakeNotificationEvidence.py')
    if ($LASTEXITCODE -ne 0) { throw 'Notification evidence verification failed.' }
}
if ($Capture -and $Elevations) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-elevations-preview'
    $captureRun=Start-Process -FilePath $previewExe -ArgumentList @('--capture-elevations','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($captureRun.ExitCode -ne 0) { throw ('Elevations capture failed. See '+$capturePath+'\verification.txt') }
    & $python (Join-Path $PSScriptRoot 'MakeElevationEvidence.py')
    if ($LASTEXITCODE -ne 0) { throw 'Elevations evidence verification failed.' }
}
elseif ($Capture) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-production-0.5'
    $captureRun=Start-Process -FilePath $previewExe -ArgumentList @('--capture','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($captureRun.ExitCode -ne 0) { throw ('Capture failed. See '+$plugin+'\diagnostics\error.txt') }
    & $python (Join-Path $PSScriptRoot 'MakeProductionReview.py')
    if ($LASTEXITCODE -ne 0) { throw 'Production provenance failed.' }
}
if ($Open) {
    if ($Elevations) { Start-Process -FilePath $previewExe -ArgumentList '--elevations' -WindowStyle Hidden }
    else { Start-Process -FilePath $previewExe -WindowStyle Hidden }
}

