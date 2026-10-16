param([switch]$Capture, [switch]$Open, [switch]$Notifications, [switch]$Elevations, [switch]$Redesign, [switch]$ElevationHotfix, [switch]$ParameterChecks, [switch]$DirectionPreview, [switch]$PreviewRuntime, [switch]$DirectionsWindow, [switch]$SelectionGuidance, [switch]$MaterialSelection)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot | Split-Path -Parent
if ($Open -and -not $Capture -and -not $Notifications) {
    & (Join-Path $PSScriptRoot 'LaunchPreview.ps1') -Elevations:$Elevations
    return
}
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$python='C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (-not ($DirectionPreview -or $PreviewRuntime -or $DirectionsWindow -or $SelectionGuidance -or $MaterialSelection)) {
& $python (Join-Path $PSScriptRoot 'GenerateSourceViews.py')
if ($LASTEXITCODE -ne 0) { throw 'Production UI source contract failed.' }
& $python (Join-Path $PSScriptRoot 'GenerateElevationPreview.py')
if ($LASTEXITCODE -ne 0) { throw 'Isolated elevations preview source contract failed.' }
}
# Actual production classes, markup and resources. No presentation substitutions.
& $msbuild (Join-Path $projectRoot 'SAB\SAB.csproj') /t:Build /p:Configuration=Debug /p:RevitVersion=2023 '/p:OutputPath=..\outputs\ui-production-preview\plugin\' '/p:BaseIntermediateOutputPath=..\outputs\ui-production-preview\obj\' '/p:IntermediateOutputPath=..\outputs\ui-production-preview\obj\Debug\' /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Production SAB build failed.' }
$plugin=Join-Path $projectRoot 'outputs\ui-production-preview\plugin'
& $msbuild (Join-Path $PSScriptRoot 'Sab.UiShowcase.csproj') /t:Build /p:Configuration=Debug /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'WPF host build failed.' }
$previewExe=Join-Path $plugin 'SAB.UI.Preview.exe'
if ($MaterialSelection) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-material-selection'
    $run=Start-Process -FilePath $previewExe -ArgumentList @('--capture-material-selection','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Material selection actual WPF capture failed.' }
    return
}
if ($SelectionGuidance) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-selection-guidance'
    $run=Start-Process -FilePath $previewExe -ArgumentList @('--capture-selection-guidance','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Selection guidance actual WPF capture failed.' }
    $regressions=Join-Path $capturePath 'notifications-regression'
    $run=Start-Process -FilePath $previewExe -ArgumentList @('--notifications','--clean-resources','--out',('"'+$regressions+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Shared notification regressions failed.' }
    return
}
if ($DirectionsWindow) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-directions-window'
    $run=Start-Process -FilePath $previewExe -ArgumentList @('--capture-directions-window','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Directions window actual WPF capture failed.' }
    return
}
if ($DirectionPreview -or $PreviewRuntime) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-preview-runtime'
    $run=Start-Process -FilePath $previewExe -ArgumentList @('--capture-preview-runtime','--out',('"'+$capturePath+'"')) -Wait -PassThru -WindowStyle Hidden
    if ($run.ExitCode -ne 0) { throw 'Direction preview actual WPF capture failed.' }
    & $python (Join-Path $PSScriptRoot 'MakePreviewRuntimeEvidence.py')
    if ($LASTEXITCODE -ne 0) { throw 'Direction preview evidence verification failed.' }
    return
}
if ($ParameterChecks) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-parameter-checks'
    foreach ($case in @(@('--capture-parameter-checks',''),@('--capture','regressions'))) {
        $destination=if ($case[1]) { Join-Path $capturePath $case[1] } else { $capturePath }
        $run=Start-Process -FilePath $previewExe -ArgumentList @($case[0],'--clean-resources','--out',('"'+$destination+'"')) -Wait -PassThru -WindowStyle Hidden
        if ($run.ExitCode -ne 0) { throw ('Parameter check capture failed: '+$destination) }
    }
    return
}
if ($ElevationHotfix) {
    $capturePath=Join-Path $projectRoot 'Docs\Ui\DesignSystem\screenshots-elevation-hotfix'
    foreach ($case in @(@('--capture-elevation-hotfix',''),@('--notifications','notifications'),@('--capture-elevations','sessions'))) {
        $destination=if ($case[1]) { Join-Path $capturePath $case[1] } else { $capturePath }
        $arguments=@($case[0],'--out',('"'+$destination+'"'))
        if ($case[0] -eq '--notifications') { $arguments+='--clean-resources' }
        $run=Start-Process -FilePath $previewExe -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
        if ($run.ExitCode -ne 0) { throw ('Hotfix capture failed: '+$destination) }
    }
    & $python (Join-Path $PSScriptRoot 'MakeElevationHotfixEvidence.py')
    if ($LASTEXITCODE -ne 0) { throw 'Hotfix evidence verification failed.' }
    return
}
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

