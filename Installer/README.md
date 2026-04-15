# SAB MSI Installer (Per-User)

## Purpose
This folder contains a script that builds MSI installers for the Revit plugin from the `bin` folder.

The generated installers:
- do **not** require administrator rights,
- install to user profile path:
  - `%AppData%\Autodesk\Revit\Addins\2023\...`
  - `%AppData%\Autodesk\Revit\Addins\2024\...`

## What is generated
Running the script creates:
- `SAB_Revit_2023.msi`
- `SAB_Revit_2024.msi`

Output folder:
- `Installer\output`

## Build command
From repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\Build-Msi.ps1
```

Optional custom bin folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\Build-Msi.ps1 -BinFolder "..\SAB\bin\Release"
```

## Notes
- Script installs WiX Toolset 4 locally into `Installer\.tools` if needed.
- MSI package scope is `perUser`, so elevation is not required.
- Image/resource/dashboard files from `bin` are included recursively with folder structure preserved.
