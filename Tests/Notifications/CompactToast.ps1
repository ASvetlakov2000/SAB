param([ValidateSet('2023','2024')][string]$RevitVersion='2023')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Drawing
[Reflection.Assembly]::LoadFrom((Join-Path $repo "SAB/bin/Notifications$RevitVersion/SAB.dll")) | Out-Null
if(![SAB.Notifications.UndoEligibility]::Matches(5,5,'Edit',[string[]]@('Child','Edit'),$false,0)) { throw 'Composite transaction rejected' }
if([SAB.Notifications.UndoEligibility]::Matches(5,6,'Edit',[string[]]@('Edit'),$false,0)) { throw 'Stale transaction accepted' }
if([SAB.Notifications.UndoEligibility]::Matches(5,5,'Edit',[string[]]@('Unrelated'),$false,0)) { throw 'Unrelated transaction accepted' }
if(![SAB.Notifications.UndoEligibility]::Matches(5,5,$null,[string[]]@('Delete','Child'),$true,8)) { throw 'Confirmed deletion rejected' }
if([SAB.Notifications.UndoEligibility]::Matches(5,5,$null,[string[]]@('Delete'),$true,0)) { throw 'Empty deletion accepted' }
foreach($size in @(16,32)) { $img=[Drawing.Image]::FromFile((Join-Path $repo "SAB/Resources/Notifications_$size.png")); try { if($img.Width-ne $size -or $img.Height-ne $size) { throw 'Wrong icon dimensions' } } finally { $img.Dispose() } }
$details=New-Object Windows.Controls.StackPanel
$actions=New-Object Windows.Controls.StackPanel; $actions.Orientation='Horizontal'
$actions.Children.Add((New-Object Windows.Controls.Button -Property @{Content='Откатить';Padding='8,4,8,4'})) | Out-Null
$actions.Children.Add((New-Object Windows.Controls.Button -Property @{Content='Журнал';Padding='8,4,8,4';Margin='8,0,0,0'})) | Out-Null
$details.Children.Add($actions) | Out-Null
$message=('Не удалось сохранить элементы присоединенными. Полный текст ошибки сохранён в журнале. '+[Environment]::NewLine)*8
$card=[Helpers.Notifications.ToastNotifications.SabStyledToastHost]::CreateDetailedToast('SAB — Предупреждение',$message,[Helpers.Notifications.ToastNotifications.ToastType]::Warning,$details)
$card.Measure((New-Object Windows.Size(414,1000))); $collapsed=$card.DesiredSize.Height
$card.Arrange((New-Object Windows.Rect(0,0,414,$collapsed))); $card.UpdateLayout()
$bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap(414,([int][Math]::Ceiling($collapsed)),96,96,([Windows.Media.PixelFormats]::Pbgra32)); $bitmap.Render($card)
$encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder; $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$preview=Join-Path ([IO.Path]::GetTempPath()) 'SAB-compact-toast.png'; $stream=[IO.File]::Create($preview); try {$encoder.Save($stream)} finally {$stream.Dispose()}
$expander=$card.Child.Children | Where-Object {$_ -is [Windows.Controls.Expander]}
if($expander.IsExpanded) { throw 'Message expanded by default' }
$expander.IsExpanded=$true; $expander.UpdateLayout(); $card.InvalidateMeasure(); $card.Measure((New-Object Windows.Size(414,1000)))
if($card.DesiredSize.Height -le $collapsed) { throw 'Expansion did not expose full text' }
if($expander.Content.Text -ne $message) { throw 'Full error text changed' }
if($collapsed -gt 190) { throw "Toast too tall: $collapsed" }
Write-Output "PASS: Undo matching and invalidation, 16/32px icons, compact height $collapsed, expandable full text. Preview: $preview"
