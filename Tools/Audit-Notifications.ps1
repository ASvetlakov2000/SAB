param([switch]$IncludeLocalHistory)

# Run with Windows PowerShell 5.1: Revit 2022 contains mixed-mode .NET Framework metadata.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo 'Docs/NotificationsAudit'
New-Item -ItemType Directory -Force -Path $out | Out-Null
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run this script with Windows PowerShell 5.1 (powershell.exe), not pwsh.' }

Add-Type -Path @(
    (Join-Path $repo 'SAB/Notifications/DeleteWarningJournal.cs'),
    (Join-Path $repo 'SAB/Notifications/UndoEligibility.cs')
)
$checks = New-Object 'System.Collections.Generic.List[object]'
function Check([string]$name, [bool]$condition) {
    $checks.Add([pscustomobject]@{ Name = $name; Status = $(if ($condition) { 'PASS' } else { 'FAIL' }) })
    if (!$condition) { throw "Audit helper check failed: $name" }
}
$fixture = [IO.File]::ReadAllText((Join-Path $repo 'Tests/Notifications/DeleteWarning.txt'), [Text.Encoding]::UTF8)
$text = [SAB.Notifications.DeleteWarningJournal]::Parse($fixture)
Check 'Recognize repository deletion-warning fixture' ($text -and $text.Contains('Помимо выбранных элементов будут удалены'))
Check 'Reject completed dialog' (!$([SAB.Notifications.DeleteWarningJournal]::Parse($fixture + 'ADialog::doModal stop')))
Check 'Reject later command' (!$([SAB.Notifications.DeleteWarningJournal]::Parse($fixture + 'Jrn.Command')))
Check 'Reject error instead of warning' (!$([SAB.Notifications.DeleteWarningJournal]::Parse($fixture.Replace('0 errors', '1 errors'))))
Check 'Reject multiple warnings' (!$([SAB.Notifications.DeleteWarningJournal]::Parse($fixture.Replace('1 warnings', '2 warnings'))))
Check 'Reject unrelated warning with same counter' (!$([SAB.Notifications.DeleteWarningJournal]::Parse($fixture.Replace('Помимо выбранных элементов будут удалены', 'Линии должны образовывать замкнутые контуры'))))
Check 'Reject empty journal' (!$([SAB.Notifications.DeleteWarningJournal]::Parse('')))
Check 'Undo accepts containing transaction' ([SAB.Notifications.UndoEligibility]::Matches(5,5,'Edit',[string[]]@('Child','Edit'),$false,0))
Check 'Undo rejects changed revision' (![SAB.Notifications.UndoEligibility]::Matches(5,6,'Edit',[string[]]@('Edit'),$false,0))
Check 'Undo rejects unrelated transaction' (![SAB.Notifications.UndoEligibility]::Matches(5,5,'Edit',[string[]]@('Other'),$false,0))
Check 'Deletion undo requires deleted elements' (![SAB.Notifications.UndoEligibility]::Matches(5,5,$null,[string[]]@('Delete'),$true,0))
$weakDeletionAssociation = [SAB.Notifications.UndoEligibility]::Matches(5,5,$null,[string[]]@('Unrelated deletion'),$true,1)

$inventory = New-Object 'System.Collections.Generic.List[object]'
$versions = New-Object 'System.Collections.Generic.List[object]'
foreach ($version in 2022,2023,2024) {
    $folder = if ($version -eq 2022) { Join-Path $repo '../../lib/Revit2022' } else { "C:/Program Files/Autodesk/Revit $version" }
    $path = [IO.Path]::GetFullPath((Join-Path $folder 'RevitAPI.dll'))
    $api = [Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
    $groups = @($api.GetType('Autodesk.Revit.DB.BuiltInFailures').GetNestedTypes())
    $count = 0
    foreach ($group in $groups) {
        foreach ($property in $group.GetProperties()) {
            $inventory.Add([pscustomobject]@{ RevitVersion = $version; Category = $group.Name; Member = $property.Name })
            $count++
        }
    }
    $versions.Add([pscustomobject]@{
        RevitVersion = $version; AssemblyVersion = $api.GetName().Version.ToString()
        ApiSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        Groups = $groups.Count; Definitions = $count
        DocumentSketchOrEditModeProperties = @($api.GetType('Autodesk.Revit.DB.Document').GetProperties() | Where-Object { $_.Name -match 'Sketch|EditMode' } | Select-Object -ExpandProperty Name)
    })
}
$inventory | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $out 'ApiInventory.csv')

$history = $null
if ($IncludeLocalHistory) {
    $homePath = Join-Path $env:LOCALAPPDATA 'RevitQuiet'
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $badLines = 0
    foreach ($line in Get-Content -LiteralPath (Join-Path $homePath 'spool.jsonl') -Encoding UTF8) {
        try { $rows.Add(($line | ConvertFrom-Json -ErrorAction Stop)) } catch { $badLines++ }
    }
    # Original and updated JSONL records share an event key; counts are not command counts.
    $events = @($rows | Group-Object { @($_.Time,$_.Failure,$_.Severity,$_.Transaction,$_.Ids) -join '|' })
    $observed = @($rows | Group-Object Failure,Severity | Sort-Object Name | ForEach-Object {
        $row = $_.Group[0]
        $description = [regex]::Replace([string]$row.Text, 'В семействе "[^"]+"', 'В семействе "(имя скрыто)"')
        $description = [regex]::Replace($description, 'связи «[^»]+»', 'связи «(имя скрыто)»')
        [pscustomobject]@{
            Failure = $row.Failure; Severity = $row.Severity; Description = $description
            RawRecords = $_.Count
            EventKeys = @($_.Group | Group-Object { @($_.Time,$_.Failure,$_.Severity,$_.Transaction,$_.Ids) -join '|' }).Count
            RecordedActions = (@($_.Group.Action | Sort-Object -Unique) -join ' | ')
        }
    })
    $observed | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $out 'ObservedMessages.csv')
    $config = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $homePath 'settings.json') | ConvertFrom-Json
    $history = [pscustomobject]@{
        Source = '%LOCALAPPDATA%/RevitQuiet/spool.jsonl'; Sha256 = (Get-FileHash -LiteralPath (Join-Path $homePath 'spool.jsonl')).Hash
        RawRecords = $rows.Count; InvalidLines = $badLines; EventKeys = $events.Count; FailureSeverityKinds = $observed.Count
        First = ($rows.Time | Sort-Object | Select-Object -First 1); Last = ($rows.Time | Sort-Object | Select-Object -Last 1)
        OwnershipKeywordMatches = @($rows | Where-Object { $_.Text -match 'принадлежит|другим пользоват|занят другим|OwnedByOther|borrow|not editable' }).Count
        RollbackLimitRecords = @($rows | Where-Object { $_.Action -match 'превышен лимит' }).Count
        Config = [pscustomobject]@{ Enabled = $config.Enabled; SkipOrdinary = $config.SkipOrdinary; SkipImportant = $config.SkipImportant; DialogRules = @($config.DialogResults.PSObject.Properties).Count }
        Limitations = 'Historical logs do not record SAB build/Revit version or command completion. EventKeys are approximate; ownership keyword absence does not disprove the reported incident.'
    }
}
$sourcePaths = @(
    'SAB/Notifications/NotificationController.cs', 'SAB/Notifications/DeleteWarningButton.cs',
    'SAB/Notifications/DeleteWarningJournal.cs', 'SAB/Notifications/UndoEligibility.cs',
    'SAB/Notifications/ReadableJournal.cs', 'SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs',
    'SAB/Cls_ParameterTools/Workflow.cs', 'Docs/Notifications.md', 'Docs/PluginInstructions/SAB_HTML_Uvedomleniya.html'
)
$sources = @($sourcePaths | ForEach-Object { [pscustomobject]@{ Path = $_; Sha256 = (Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash } })
$evidence = [ordered]@{
    AuditDate = '2026-10-17'; TimeZone = 'Europe/Moscow'
    Head = (& git -C $repo rev-parse HEAD)
    Scope = 'Source audit, local historical log review, read-only API metadata inventory, isolated helper checks. No product changes, installation or Revit command execution.'
    Versions = $versions.ToArray(); SourceFiles = $sources; HelperChecks = $checks.ToArray()
    WeakDeletionAssociationObserved = $weakDeletionAssociation
    LocalHistory = $history; LiveRevitProcesses = @(Get-Process Revit -ErrorAction SilentlyContinue).Count
    RevitRuntimeScenarios = 'NOT RUN'; NativeButtonScenario = 'NOT RUN'; ProductionBuild = 'N/A: production source unchanged'
}
$evidence | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $out 'Evidence.json')
Write-Output ("PASS: {0} isolated helper checks; {1} API symbols inventoried. Revit runtime: NOT RUN." -f $checks.Count,$inventory.Count)
