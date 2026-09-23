param([ValidateSet("2023","2024")][string]$RevitVersion="2023")
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[Reflection.Assembly]::LoadFrom("C:/Program Files/Autodesk/Revit $RevitVersion/RevitAPI.dll") | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $repo "SAB/bin/Notifications$RevitVersion/SAB.dll")) | Out-Null
$tail=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "DeleteWarning.txt"),[Text.Encoding]::UTF8)
$text=[SAB.Notifications.DeleteWarningJournal]::Parse($tail)
if(!$text -or !$text.Contains('Помимо выбранных элементов будут удалены') -or !$text.Contains('Фунд. стена T.O.')) { throw "Actual journal was not recognized: $text" }
if([SAB.Notifications.DeleteWarningJournal]::Parse($tail+'ADialog::doModal stop')) { throw 'Stale dialog accepted' }
if([SAB.Notifications.DeleteWarningJournal]::Parse($tail.Replace('0 errors','1 errors'))) { throw 'Error dialog accepted' }
$testFolder=Join-Path ([IO.Path]::GetTempPath()) ('sab-html-test-'+[Guid]::NewGuid().ToString('N'))
$r=New-Object SAB.Notifications.Entry; $r.Text=$text+'<script>alert(1)</script>'; $r.Ids='123,456'; $r.Model='Тест'; $r.Action='OK'
$html=[SAB.Notifications.ReadableJournal]::Write($testFolder,$r)
[SAB.Notifications.ReadableJournal]::Write($testFolder,$r) | Out-Null
$body=[IO.File]::ReadAllText($html)
if($body.Contains('<script>') -or ([regex]::Matches($body,'<article>').Count -ne 2) -or !$body.EndsWith('</main></body></html>')) { throw 'HTML escaping or append failed' }
$hostWindow=New-Object Helpers.Notifications.ToastNotifications.SabStyledToastHost
$hostWindow.ShowDetailedToast('SAB — Предупреждение',$text,[Helpers.Notifications.ToastNotifications.ToastType]::Warning,(New-Object System.Windows.Controls.TextBox -Property @{Text='123,456';IsReadOnly=$true}),3)
$stack=$hostWindow.Content.Content
if($stack.Children.Count -ne 1) { throw 'Toast was not added' }
$frame=New-Object System.Windows.Threading.DispatcherFrame
$timer=New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval=[TimeSpan]::FromSeconds(4)
$timer.Add_Tick({$timer.Stop();$frame.Continue=$false})
$timer.Start(); [System.Windows.Threading.Dispatcher]::PushFrame($frame)
if($stack.Children.Count -ne 0) { throw 'Toast did not expire' }
$hostWindow.Close()
Write-Output 'PASS: actual deletion warning text; stale/error rejection; HTML escaping and append; toast expires after 3 seconds.'
