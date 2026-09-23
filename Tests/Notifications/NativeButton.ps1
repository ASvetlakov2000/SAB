param([ValidateSet("2023","2024")][string]$RevitVersion="2023")
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,WindowsBase,System.Windows.Forms
$dll=(Join-Path $repo "SAB/bin/Notifications$RevitVersion/SAB.dll")
$asm=[Reflection.Assembly]::LoadFrom($dll)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeFixture {
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetId(IntPtr h,int n,IntPtr v);
}
"@
$owner=New-Object Windows.Forms.Form; $owner.ShowInTaskbar=$false
$dialog=New-Object Windows.Forms.Form; $dialog.ShowInTaskbar=$false; $dialog.StartPosition='Manual'; $dialog.Location=New-Object Drawing.Point(-10000,-10000)
$ok=New-Object Windows.Forms.Button; $ok.Text='OK'; $dialog.Controls.Add($ok)
$cancel=New-Object Windows.Forms.Button; $cancel.Text='Cancel'; $dialog.Controls.Add($cancel)
$show=New-Object Windows.Forms.Button; $show.Text='Show'; $dialog.Controls.Add($show)
$expand=New-Object Windows.Forms.Button; $expand.Text='Expand >>'; $dialog.Controls.Add($expand)
[NativeFixture]::SetId($ok.Handle,-12,[IntPtr]1) | Out-Null
[NativeFixture]::SetId($cancel.Handle,-12,[IntPtr]2) | Out-Null
$script:clicked=$false; $script:posted=$false
$ok.Add_Click({$script:clicked=$true;$dialog.Close()})
$type=$asm.GetType('SAB.Notifications.DeleteWarningButton')
$watcher=[Activator]::CreateInstance($type,$true)
$callback=[Action[bool]] { param($value) $script:posted=$value; if(!$value) { $dialog.Close() } }
$type.GetMethod('Arm').Invoke($watcher,@($owner.Handle,$callback)) | Out-Null
try { $dialog.ShowDialog($owner) | Out-Null } finally { $watcher.Dispose();$dialog.Dispose();$owner.Dispose() }
if(!$script:posted -or !$script:clicked) { throw 'Native OK was not clicked' }
Write-Output 'PASS: native OK button executes Click on owned modal dialog.'
