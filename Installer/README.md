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
Double-click `Build-Msi.bat` in the repository root. It compiles the single SAB
assembly, including parameter tools, material quantity and filled-region tools, against every
supported Revit API and then creates the matching MSI installers. The default
configuration is Release. For a Debug build, run
`Build-Msi.bat Debug`; an optional second argument sets the installer version,
for example `Build-Msi.bat Release 1.2.3`.

The 2022 build uses the installed Revit 2022 API or the local reference cache
in `Installer\.tools\Revit2022Api`. Revit 2023 and 2024 API assemblies must be
available in their installed Revit folders under `Program Files\Autodesk`.

`Installer\Build-Msi.ps1` only packages an already compiled SAB assembly. When
using it directly, pass a version-specific `-BinFolder` and matching `-Years`.

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
- All production commands are installed in `...\Addins\<Year>\SAB` as one
  `SAB.dll` and registered by the single `SAB_<Year>.addin` manifest.
- Parameter fill/check commands are built into `SAB.dll`; all commands and
  settings are on `SAB / Параметры`. See
  `Docs/PluginInstructions/ParameterTools.md`.
- Installation removes the obsolete standalone `SAB.ParameterTools.addin`
  registration. Existing parameter profiles and source-room metadata retain
  their storage schema identifiers.
- During installation, legacy `SAB.MaterialQuantity.addin` and
  `FilledRegionFromMaterial.addin` manifests and their obsolete plugin folders
  are removed to prevent duplicate ribbon registrations and stale DLLs from
  earlier standalone builds.
- Test plugin `SyncReminderTest` is built from `SyncReminderTest\SyncReminderTest.csproj` when needed and installed into `...\Addins\<Year>\SyncReminderTest`.
- Local HTML help is opened by **SAB → Настройки → Инструкции**. The installer includes
  `Docs/PluginInstructions/IDEOLOGIST_HTML_Instruktsii.html`, the parameter guide and
  `assets/template.css` at `%APPDATA%\Autodesk\Revit\Addins\<Year>\SAB\Docs\PluginInstructions`.
  Missing required help files fail the installer build. No web server or internet is needed.
- `Nice3point.Revit.Toolkit.dll` is bundled for the corresponding Revit version. All three
  pinned toolkit packages are restored from `SAB/packages.config`.
- If `SyncReminderTest` needs to be built, `Build-Msi.ps1` searches for `RevitAPI.dll` and `RevitAPIUI.dll` in shared `lib` folders, SAB bin folders, and installed `Program Files\Autodesk\Revit *` folders.
