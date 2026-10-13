# Проверка установщика SAB — 12.10.2026

Запущен именно корневой `Build-Msi.bat` без аргументов (`Release`) через cmd с перенаправлением NUL только для завершающего pause. Exit code0. Цепочка: Build-Msi.bat → Installer/Build-All-Msi.ps1 → SAB/SAB.csproj Rebuild для каждого года → Installer/Build-Msi.ps1/WixSharp → Verify-MsiPayload.ps1. Установка не выполнялась.

Исправлено разрешение Revit2022 API: используются сохранённые пользовательские DLL из `C:\Users\svetl\Desktop\Codex\lib\Revit2022`, проверяется assembly major обеих библиотек. Revit2023/2024 используют установленные API соответствующего года. Reference DLL в MSI не включаются.

Для каждого MSI CAB извлечён read-only через Windows Installer API и expand.exe. SHA256 упакованных SAB.dll, SABFluentLight.xaml, SABWindowStyles.xaml, Toolkit и23HTML (каталог+22руководства) совпадает с текущими файлами сборки/исходниками; стили bin также совпадают с source. В DLL найдены compiled NotificationCard/NotificationStack/ParameterNotifications и мигрированные окна; actual RevitAPI/RevitAPIUI references имеют major22/23/24. Preview exe и host API libraries отсутствуют.

| Revit | DLL assembly | MSI SHA256 |
|---|---|---|
| 2022 | 1.0.2022.1 | `F3ED4D91306027F255BEFE9478BB5961B97DB8E0DE78166FE310C2C5229996D4` |
| 2023 | 1.0.2023.1 | `D49667EFFFE27B8E38CAE5A62B896CDE76034C41FEE1630DCAE14DC6EB0CA4DC` |
| 2024 | 1.0.2024.1 | `48F43CEDAC043DB6A1B902B556B7D1639E5163C3C12883F50080CD1BDE8AA01B` |

[Payload record](Installer-Payload.json) содержит хеши каждого проверенного файла. Полный batch log: `outputs/ui-production-preview/installer-build.log`. Номер MSI1.0.0 остаётся штатным; существующий MajorUpgrade разрешает same-version upgrades. Изменения UI входят в обычный production csproj, а не только отдельную preview DLL.

Готовые пакеты: `Installer/output/SAB_Revit_2022.msi`, `SAB_Revit_2023.msi`, `SAB_Revit_2024.msi`; SHA256SUMS.txt обновлён после всех проверок. Проверка payload теперь встроена в обычный batch и прерывает сборку при несовпадении. Служебные извлечённые данные повторно используют `outputs/ui-production-preview/msi-check`, без новой timestamp-папки на каждый запуск.

[Уведомления](Notification-Verification.md) отдельно проверены native WPF вне Revit:35 assertions/9captures. Проверка MSI доказывает состав пакета, но не выполнение транзакций, установку/обновление или запуск внутри Revit. Actual Revit2022 native runtime не доступен на этом компьютере; совместимостьAPI22 подтверждена сборкой/metadata, не выполнением команд.


Независимый packaging review обнаружил P2 в повторном использовании извлечённых файлов, а не в самих MSI. Исправлено: ValidateSet годов, absolute canonical workspace check, каждый File ID остаётся строго внутри него, только существующие current-ID Leaf-файлы удаляются через LiteralPath до expand. Повторная проверка всех трёх MSI после очистки PASS; байты установщиков не менялись. [Независимый packaging review](Installer-Review.md).

Текущий повторный запуск штатного batch завершён exit0 после bottom-up notification fix. Свежие пакеты сверены по extracted CAB. Изолированные макеты «Развёрток» в MSI не включены; production93 baseline-файла совпадают. Установка не выполнялась.


## Историческая сборка после первого functional feedback

12.10.2026 Build-Msi.bat Release exit0, все3CAB пересозданы/извлечены и сверены с текущими сборками. Включены5разрешённых файлов category functionality;88baseline files unchanged. UI-макеты Развёрток и proposal notification исключены; установки нет. Прежняя таблица выше историческая. Актуальные MSI SHA256:

- Revit 2022: `554FE717D3A8068D73FEB89C3FE43AE52C3BA5B5A2E8BB40C44D9B1F48EE0640`.
- Revit 2023: `FFEC33039A6B9DF5F708F5CE4ED3255DFF4606AFBD64D7146CA64C178570BA83`.
- Revit 2024: `7B3B43EBB708739BC3BFCF883A12BC5EE4E0E64E9A1ABD0B2FD2BF8F102A449D`.

Полный текущий payload: [Installer-Payload.json](Installer-Payload.json).


## Текущая сборка: независимые категории и итоговое уведомление

Build-Msi.bat Release exit0 для2022/2023/2024. Включены8authorized Elevations owners +2optional shared warning owners; baseline85unchanged. CAB повторно извлечён и SHA совпадают; дополнительно проверены compiled BuildNotification/ElevationCreationNotificationData/get_PlaceFloorSpots/get_PlaceCeilingSpots/get_PlaceSlabSpots/ownership service. Новые UI-макеты и416/16proposal исключены; установки нет. Текущие SHA:

- Revit 2022: `642F4AE39AFC3CA536AAFDAD022FC34A3081C910B4FDDE279F2A6EC36D1F6F7B`.
- Revit 2023: `1D38AFDB1DF3F0D548B1B57AD0C97C48D7D478BF052BE7E081794B765FAC285D`.
- Revit 2024: `95B2E23437AC2A9A33CD5271C13FE95B522B3EC8FEE41965D9F47AA56A16CFDF`.

[Актуальный payload](Installer-Payload.json).


## Текущий выпуск1.1.0 — Редизайн,13.10.2026

Build-Msi.bat без versionoverride:exit0, ProductVersion1.1.0 всех трёх MSI. Актуальные25production migration owners и все looseXAML/styles/DLL/Toolkit/инструкции включены;41payloadentries каждого года сверены после extraction, DLL1.1.0.0 и APIrefs22/23/24. [Currenthashrecords](Installer-Payload.json), [actualnativeUIverification](RedesignMigration-Verification.md). Предыдущие scope «UI/proposal excluded» исторические. Установка не выполнялась.
