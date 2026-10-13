param([switch]$Elevations)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$previewExe=Join-Path $projectRoot 'outputs\ui-production-preview\plugin\SAB.UI.Preview.exe'
$launchMode=if($Elevations){'Elevations'}else{'Modules'}
$launchMutex=New-Object Threading.Mutex($false,('Local\SAB.UiShowcase.Launch.'+$launchMode))
$locked=$false
try {
    $locked=$launchMutex.WaitOne(10000)
    if(-not $locked){throw 'Another preview launch is still in progress.'}
    # Open a built artifact. Never overwrite files held by a live preview process.
    if(-not (Test-Path -LiteralPath $previewExe -PathType Leaf)) {
        & (Join-Path $PSScriptRoot 'Build.ps1') -Elevations:$Elevations
        if(-not (Test-Path -LiteralPath $previewExe -PathType Leaf)){throw 'Preview build did not produce the executable.'}
    }
    $existing=@(Get-CimInstance Win32_Process -Filter "Name = 'SAB.UI.Preview.exe'" | Where-Object {
        $_.ExecutablePath -eq $previewExe -and
        $_.CommandLine -notmatch '--capture|--notifications' -and
        (($Elevations -and $_.CommandLine -match '--elevations') -or (-not $Elevations -and $_.CommandLine -notmatch '--elevations'))
    }) | Select-Object -First 1
    if($existing) {
        $previewProcess=Get-Process -Id $existing.ProcessId
        if(-not ('SabPreviewWindow' -as [type])) { Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SabPreviewWindow {
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetLastActivePopup(IntPtr window);
}
'@
        }
        $windowHandle=$previewProcess.MainWindowHandle
        if($windowHandle -ne [IntPtr]::Zero) {
            if([SabPreviewWindow]::IsIconic($windowHandle)){[void][SabPreviewWindow]::ShowWindowAsync($windowHandle,9)}
            $popupHandle=[SabPreviewWindow]::GetLastActivePopup($windowHandle)
            if($popupHandle -ne [IntPtr]::Zero){$windowHandle=$popupHandle}
            [void][SabPreviewWindow]::SetForegroundWindow($windowHandle)
        }
        Write-Host ('Reused '+$launchMode+' preview; PID='+$previewProcess.Id+'. No build performed.')
    }
    else {
        $startOptions=@{FilePath=$previewExe;WorkingDirectory=(Split-Path -Parent $previewExe);PassThru=$true;WindowStyle='Hidden'}
        if($Elevations){$startOptions.ArgumentList='--elevations'}
        $previewProcess=Start-Process @startOptions
        if($previewProcess.WaitForExit(1000)) {
            if($previewProcess.ExitCode -ne 0) {
                $diagnostics=Join-Path (Split-Path -Parent $previewExe) 'diagnostics\error.txt'
                if(Test-Path -LiteralPath $diagnostics){throw ([IO.File]::ReadAllText($diagnostics))}
                throw ('Preview exited with code '+$previewProcess.ExitCode)
            }
        }
        Write-Host ('Opened '+$launchMode+' preview; PID='+$previewProcess.Id+'. No rebuild of existing artifact.')
    }
}
finally {
    if($locked){$launchMutex.ReleaseMutex()}
    $launchMutex.Dispose()
}
