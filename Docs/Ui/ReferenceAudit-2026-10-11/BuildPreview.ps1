$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$build = Join-Path $repo 'outputs\ui-reference-audit\plugin'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
Push-Location $repo
try {
    & $msbuild 'SAB\SAB.csproj' '/t:Build' '/p:Configuration=Debug' '/p:RevitVersion=2023' '/p:OutputPath=..\outputs\ui-reference-audit\plugin\' '/p:BaseIntermediateOutputPath=..\outputs\ui-reference-audit\obj\' '/p:IntermediateOutputPath=..\outputs\ui-reference-audit\obj\Debug\' '/v:quiet' '/nologo'
    if ($LASTEXITCODE -ne 0) { throw 'SAB build failed' }
} finally { Pop-Location }
$compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$arguments = @('/nologo', '/target:exe', '/platform:x64', ('/out:' + (Join-Path $build 'SabReferencePreview.exe')))
foreach ($name in @('System','System.Core','WindowsBase','System.Xaml')) { $arguments += '/reference:' + (Join-Path $refs ($name + '.dll')) }
foreach ($name in @('PresentationCore','PresentationFramework')) { $arguments += '/reference:' + (Join-Path $refs ($name + '.dll')) }
$arguments += '/reference:' + (Join-Path $build 'SAB.dll')
$arguments += Join-Path $PSScriptRoot 'Preview.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Preview compilation failed' }
& (Join-Path $build 'SabReferencePreview.exe') (Join-Path $PSScriptRoot 'screenshots')
if ($LASTEXITCODE -ne 0) { throw 'Preview capture failed; see capture-manifest.txt' }
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'screenshots\capture-manifest.txt')
