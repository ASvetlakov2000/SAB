# Прототип окна «Развёртки» по мотивам ModPlus

Самостоятельный прототип WPF без зависимости от Revit. Он не выполняет команды SAB
и не изменяет рабочее окно `ElevationSettingsWindow`.

## Запуск

```powershell
dotnet run --project .\ModPlusInspiredElevationPrototype.csproj
```

## Сохранение изображения PNG

```powershell
dotnet run --project .\ModPlusInspiredElevationPrototype.csproj -- --render=.\artifacts\elevation-prototype.png
```

При сборке прототип подключает рабочий словарь `SABWindowStyles.xaml`.
Дополнительные стили и анимации действуют только внутри прототипа.
