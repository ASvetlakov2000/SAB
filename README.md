# SAB — плагин для Autodesk Revit

Рабочие исходники SAB для Revit 2022, 2023 и 2024. Модули записи параметров,
материалов, заливок и напоминания о синхронизации входят в единую `SAB.dll`.

## Скачать и установить

**[Скачать последнюю версию SAB](https://github.com/ASvetlakov2000/SAB/releases/latest)**

Выберите установщик для своей версии Revit:

| Версия Revit | Установщик |
| --- | --- |
| Revit 2022 | [Скачать SAB для Revit 2022](https://github.com/ASvetlakov2000/SAB/releases/latest/download/SAB_Revit_2022.msi) |
| Revit 2023 | [Скачать SAB для Revit 2023](https://github.com/ASvetlakov2000/SAB/releases/latest/download/SAB_Revit_2023.msi) |
| Revit 2024 | [Скачать SAB для Revit 2024](https://github.com/ASvetlakov2000/SAB/releases/latest/download/SAB_Revit_2024.msi) |

1. Полностью закройте Revit.
2. Скачайте подходящий `.msi` и запустите его двойным щелчком.
3. Завершите установку и снова откройте Revit. На ленте появится вкладка **SAB**.

Установка выполняется для текущего пользователя Windows без прав администратора.
Если используете несколько версий Revit, установите пакет для каждого года.
Для Revit 2025 и более новых версий эти установщики не подходят.
Скачивать исходники или устанавливать Visual Studio для использования SAB не нужно.
Архивы **Source code**, которые GitHub добавляет к выпуску, содержат исходники;
для установки выбирайте именно `.msi`.

Инструкции по инструментам доступны прямо в Revit: **SAB → Настройки → Инструкции**.
Они устанавливаются вместе с плагином и работают без интернета.
Для обновления закройте Revit и запустите новый установщик соответствующего года.
Удалить плагин можно через список установленных приложений Windows: **SAB Revit <год>**.

Установщики пока не подписаны цифровым сертификатом. Windows может показать
предупреждение о неизвестном издателе. Перед запуском проверьте, что файл скачан
из этого репозитория; контрольные суммы опубликованы в файле `SHA256SUMS.txt` рядом с MSI.

## Сборка после клонирования на другом ПК

Требования:

- Windows x64, Visual Studio или Build Tools с MSBuild, инструментами WPF и .NET Framework 4.8.
- Установленный Revit соответствующего года с `RevitAPI.dll` и `RevitAPIUI.dll`.
  Эти библиотеки Autodesk не хранятся в репозитории.
- .NET SDK 8 или новее для тестов и инструмента упаковки MSI; интернет для восстановления NuGet.

Клонировать репозиторий и открыть PowerShell в его корне:

```powershell
git clone https://github.com/ASvetlakov2000/SAB.git
Set-Location SAB
```

В Developer PowerShell для Visual Studio восстановить зависимости:

```powershell
$solutionDir = (Get-Location).Path + '\'
MSBuild.exe SAB\SAB.csproj /t:Restore /p:RestorePackagesConfig=true "/p:SolutionDir=$solutionDir"
```

Зависимости перечислены в `SAB/packages.config` и восстанавливаются в `packages`.
Папки NuGet, `bin`, `obj`, `outputs` и результаты сборки установщиков в Git не включены.

Пример сборки для Revit 2023:

```powershell
MSBuild.exe SAB\SAB.csproj /t:Rebuild /p:Configuration=Release /p:RevitVersion=2023 /p:OutputPath=bin\Revit2023\
```

По умолчанию API ищется в `C:\Program Files\Autodesk\Revit <год>`.
При другом расположении добавить `/p:RevitApiDirectory="D:\путь\к\Revit 2023"`.
Для другой версии заменить год и выходную папку. Нельзя собирать для одного года
с библиотеками API другого года.

Собрать установщики всех трёх версий:

```powershell
.\Build-Msi.bat
```

Для этой команды нужны API всех трёх версий. Для 2022 также поддерживается локальный
кэш `Installer/.tools/Revit2022Api`, если туда самостоятельно помещены соответствующие
библиотеки Autodesk. Папка кэша в Git не хранится. Подробности упаковки и требуемые
семейства описаны в [Installer/README.md](Installer/README.md).

## Проверки и инструкции

```powershell
dotnet run --project Tests\ParameterTools\ParameterTools.Tests.csproj --configuration Release
dotnet run --project Tests\ParameterTools.RoomResolution\ParameterTools.RoomResolution.Tests.csproj --configuration Release
dotnet run --project Tests\ParameterTools.Write\ParameterTools.Write.Tests.csproj --configuration Release
dotnet run --project Tests\Instructions\Instructions.Tests.csproj --configuration Release
```

Проверки ядра и WPF вне Revit не заменяют проверку записи в реальной модели.
Проверки RoomResolution используют подмены API с простой геометрией и не проверяют ядро Revit.
Конфигурации параметров можно передавать через экспорт/импорт JSON в окне настроек.

- [Запись параметров, группы и диагностика](Docs/PluginInstructions/ParameterTools.md)
- [Сценарии проверки параметров](Docs/ParameterTools_Acceptance.md)
- [Сборка и установка](Installer/README.md)
