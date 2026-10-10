param([switch]$Verify)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
dotnet build "$projectRoot\src\Sab.UiReview.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Review build failed' }
$reviewDist = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $reviewDist -Force | Out-Null
Copy-Item "$projectRoot\src\bin\Release\net48\*" -Destination $reviewDist -Recurse -Force
if ($Verify) {
    $process = Start-Process -FilePath "$reviewDist\SAB.UI.Review.exe" -ArgumentList '--capture', ('"' + "$projectRoot\outputs" + '"') -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Native review failed: $projectRoot\outputs\failure.log" }
    Get-Content "$projectRoot\outputs\verification.txt"
}
