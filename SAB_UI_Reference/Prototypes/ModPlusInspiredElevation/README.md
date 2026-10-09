# ModPlus-inspired prototype for «Развертки»

Standalone WPF prototype. It does not reference Revit, does not execute SAB commands and does not modify the production `ElevationSettingsWindow`.

## Run

```powershell
dotnet run --project .\ModPlusInspiredElevationPrototype.csproj
```

## Render a PNG

```powershell
dotnet run --project .\ModPlusInspiredElevationPrototype.csproj -- --render=.\artifacts\elevation-prototype.png
```

The prototype links the production `SABWindowStyles.xaml` at build time and adds only isolated prototype styles and UI-only motion.
