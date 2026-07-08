# SAB MSI Installer (WixSharp, Per-User)

## Purpose
This folder contains WixSharp-based installer builder for the Revit plugin.

The generated installers:
- do **not** require administrator rights;
- install into user profile:
  - `%AppData%\Autodesk\Revit\Addins\2023`
  - `%AppData%\Autodesk\Revit\Addins\2024`

## Technology
- `WixSharp.wix4` NuGet package
- Installer code: `Installer\WixSharpInstaller\Program.cs`
- Entry script: `Installer\Build-Msi.ps1`

## Build command
From repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\Build-Msi.ps1
```

Optional custom bin folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\Build-Msi.ps1 -BinFolder "..\SAB\bin\Release"
```

## Result
Output folder:
- `Installer\output`

Generated files:
- `SAB_Revit_2023.msi`
- `SAB_Revit_2024.msi`

## Important notes
- `Build-Msi.ps1` runs the WixSharp console project via `dotnet run`.
- `Program.cs` sets installer scope to `InstallScope.perUser`.
- Files from `bin` are included recursively into `...\Addins\<Year>\SAB`.
- `.addin` file is installed as `SAB.addin` into `...\Addins\<Year>`.
- Test plugin `SyncReminderTest` is built from `SyncReminderTest\SyncReminderTest.csproj` when needed and installed into `...\Addins\<Year>\SyncReminderTest`.
- If `SyncReminderTest` needs to be built, `Build-Msi.ps1` searches for `RevitAPI.dll` and `RevitAPIUI.dll` in shared `lib` folders, SAB bin folders, and installed `Program Files\Autodesk\Revit *` folders.
