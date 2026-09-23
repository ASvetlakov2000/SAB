# SAB MSI Installer (WixSharp, Per-User)

## Purpose
This folder contains WixSharp-based installer builder for the Revit plugin.

The generated installers:
- do **not** require administrator rights;
- install into user profile:
  - `%AppData%\Autodesk\Revit\Addins\2022`
  - `%AppData%\Autodesk\Revit\Addins\2023`
  - `%AppData%\Autodesk\Revit\Addins\2024`

## Technology
- `WixSharp.wix4` NuGet package
- Installer code: `Installer\WixSharpInstaller\Program.cs`
- Entry point for all versions: `Build-Msi.bat` in the repository root
- Version-specific build orchestration: `Installer\Build-All-Msi.ps1`
- Low-level MSI packaging script: `Installer\Build-Msi.ps1`

## Build command
Double-click `Build-Msi.bat` in the repository root. It compiles SAB separately
against the Revit 2022, 2023, and 2024 APIs, then creates all three matching
MSI installers. The default configuration is Release. For a Debug build, run
`Build-Msi.bat Debug`; an optional second argument sets the installer version,
for example `Build-Msi.bat Release 1.2.3`.

The 2022 build uses the installed Revit 2022 API or the local reference cache
in `Installer\.tools\Revit2022Api`. Revit 2023 and 2024 API assemblies must be
available in their installed Revit folders under `Program Files\Autodesk`.

`Installer\Build-Msi.ps1` only packages an already compiled DLL; when using it
directly, pass a version-specific `-BinFolder` and matching `-Years`.

Place families saved in Revit 2022 in
`SAB\Families for Plugin\CreateInteriorElevationsCommand\2022`, and families
saved in Revit 2023 for Revit 2023-2024 in
`SAB\Families for Plugin\CreateInteriorElevationsCommand\2023-2024`:

- `SAB_Марка угла_План.rfa` (required)
- `SAB_Марка угла_Развертки.rfa` (required)
- `SAB_Марка_Помещение.rfa` (optional; enables the built-in room tag)

The build verifies every family's file format before packaging. Each MSI contains
only its matching version folder under `Families for Plugin\CreateInteriorElevationsCommand`.

## Result
Output folder:
- `Installer\output`

Generated files:
- `SAB_Revit_2022.msi`
- `SAB_Revit_2023.msi`
- `SAB_Revit_2024.msi`

## Important notes
- `Build-Msi.ps1` runs the WixSharp console project via `dotnet run`.
- `Program.cs` sets installer scope to `InstallScope.perUser`.
- Files from `bin` are included recursively into `...\Addins\<Year>\SAB`.
- A version-specific `.addin` file is installed into `...\Addins\<Year>`.
- Test plugin `SyncReminderTest` is built from `SyncReminderTest\SyncReminderTest.csproj` when needed and installed into `...\Addins\<Year>\SyncReminderTest`.
- If `SyncReminderTest` needs to be built, `Build-Msi.ps1` searches for `RevitAPI.dll` and `RevitAPIUI.dll` in shared `lib` folders, SAB bin folders, and installed `Program Files\Autodesk\Revit *` folders.
