#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [switch]$PrepareOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputFolder = Join-Path $PSScriptRoot 'output'
$repository = 'ASvetlakov2000/SAB'
$api = "https://api.github.com/repos/$repository"
$tag = "v$Version"
$assetNames = @('SAB_Revit_2022.msi', 'SAB_Revit_2023.msi', 'SAB_Revit_2024.msi')
$notes = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ReleaseNotes.md') -Raw -Encoding UTF8
foreach ($name in $assetNames) {
    $notes = $notes.Replace("($name)", "(https://github.com/$repository/releases/download/$tag/$name)")
}

# Проверка текущей справки и отсутствия библиотек Revit в каждом MSI.
& (Join-Path $repositoryRoot 'Tests\Instructions\verify-msi.ps1') -RepositoryRoot $repositoryRoot
$reader = New-Object -ComObject WindowsInstaller.Installer
try {
    foreach ($year in @('2022', '2023', '2024')) {
        $database = $reader.OpenDatabase((Join-Path $outputFolder "SAB_Revit_$year.msi"), 0)
        $view = $null
        try {
            $view = $database.OpenView('SELECT `Property`, `Value` FROM `Property`')
            $view.Execute()
            $properties = @{}
            while ($row = $view.Fetch()) { $properties[$row.StringData(1)] = $row.StringData(2) }
            if ($properties['ProductVersion'] -ne $Version -or $properties['ProductName'] -ne "SAB Revit $year") {
                throw "Версия или название MSI для Revit $year не соответствует выпуску $Version."
            }
            if ($properties.ContainsKey('ALLUSERS') -and $properties['ALLUSERS'] -ne '') {
                throw "MSI для Revit $year должен устанавливаться для текущего пользователя."
            }
        }
        finally {
            if ($view) { $view.Close(); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) }
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
        }
    }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reader) }

$checksumLines = foreach ($name in $assetNames) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $outputFolder $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $outputFolder 'SHA256SUMS.txt'), [string[]]$checksumLines, [Text.Encoding]::ASCII)
$assetNames += 'SHA256SUMS.txt'
if ($PrepareOnly) { Write-Host "Проверки пройдены. Файлы выпуска: $outputFolder"; return }

# Выпуск привязывается к сохранённым и отправленным исходникам.
$origin = & git -C $repositoryRoot remote get-url origin
if ($LASTEXITCODE -ne 0 -or $origin -notmatch '^https://github\.com/ASvetlakov2000/SAB(?:\.git)?/?$') {
    throw 'Ожидался origin репозитория ASvetlakov2000/SAB.'
}
& git -C $repositoryRoot diff --quiet HEAD --
if ($LASTEXITCODE -ne 0) { throw 'Сначала сохраните изменения отслеживаемых файлов в Git.' }
& git -C $repositoryRoot ls-files --error-unmatch Installer/ReleaseNotes.md Installer/Publish-GitHubRelease.ps1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Сначала добавьте описание и скрипт выпуска в Git.' }
$commit = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Не удалось определить исходный коммит.' }
$remoteHead = & git -C $repositoryRoot ls-remote origin refs/heads/master
if ($LASTEXITCODE -ne 0 -or ($remoteHead -split '\s+')[0] -ne $commit) {
    throw 'Сначала отправьте текущий коммит в origin/master.'
}

# Учётные данные используются только в памяти и HTTP-заголовках.
$token = $env:GH_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) { $token = $env:GITHUB_TOKEN }
if ([string]::IsNullOrWhiteSpace($token)) {
    $previousInteractive = $env:GCM_INTERACTIVE
    try {
        $env:GCM_INTERACTIVE = 'Never'
        $credentialLines = @('protocol=https', 'host=github.com', '') | git -C $repositoryRoot credential fill 2>$null
        foreach ($line in $credentialLines) {
            if ($line.StartsWith('password=')) { $token = $line.Substring(9) }
        }
    }
    finally { $env:GCM_INTERACTIVE = $previousInteractive; $credentialLines = $null }
}
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Нет доступа к GitHub: настройте Git Credential Manager или GH_TOKEN.' }
$headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'SAB-Release-Builder'; 'X-GitHub-Api-Version' = '2022-11-28' }
try {
    $release = $null
    try { $release = Invoke-RestMethod -Uri "$api/releases/tags/$tag" -Headers $headers }
    catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
    if ($release) {
        if (-not $release.draft -or $release.target_commitish -ne $commit) {
            throw "Выпуск $tag уже опубликован или относится к другому коммиту. Используйте новую версию."
        }
    }
    else {
        $body = @{ tag_name = $tag; target_commitish = $commit; name = "SAB $Version — установщики для Revit 2022–2024"; body = $notes; draft = $true; prerelease = $false } | ConvertTo-Json
        $release = Invoke-RestMethod -Uri "$api/releases" -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    }
    foreach ($name in $assetNames) {
        $path = Join-Path $outputFolder $name
        $file = Get-Item -LiteralPath $path
        $digest = 'sha256:' + (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $assets = @(Invoke-RestMethod -Uri "$api/releases/$($release.id)/assets" -Headers $headers | ForEach-Object { $_ })
        $asset = $assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if (-not $asset) {
            $uploadUri = "https://uploads.github.com/repos/$repository/releases/$($release.id)/assets?name=$name"
            $asset = Invoke-RestMethod -Uri $uploadUri -Headers $headers -Method Post -ContentType 'application/octet-stream' -InFile $path
        }
        if ($asset.size -ne $file.Length -or $asset.digest -ne $digest -or $asset.state -ne 'uploaded') {
            throw "Проверка загруженного файла $name не пройдена. Выпуск оставлен черновиком."
        }
        Write-Host "Проверен файл: $name"
    }
    $body = @{ body = $notes; draft = $false; make_latest = 'true' } | ConvertTo-Json
    $published = Invoke-RestMethod -Uri "$api/releases/$($release.id)" -Headers $headers -Method Patch -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    Write-Host "Выпуск опубликован: $($published.html_url)"
}
finally { $headers = $null; $token = $null }
