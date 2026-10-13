# SAB1.1.0 — перенос в рабочий плагин

Пользователь12.10.2026 утвердил комплект и запросил обычный установщик, commit+push и выпуск «1.1.0 Редизайн». Выполнен перенос9activeокон «Развёрток» в фактические SAB.dll classes/looseXAML; два ранее перенесённых модуля проверены повторно с общей системой. [Полный70IDотчёт](RedesignMigration-Report.json), [provenance](RedesignMigration-Provenance.json), [actualснимки](RedesignMigration.html), [независимыйship](RedesignMigration-Review.md).

| Свежий набор | PASS | PNG | Граница |
|---|---:|---:|---|
| ActualElevationconstructors |109|23|Реальные классы SAB.dll, optiondatafixtures/noDocument |
| ActualПараметризация/видыилисты |81|27|Реальные классы/DTOданные |
| Actualsharednotification |35|9|Реальные controls/timer/copy/adapters; syntheticresult |
| Isolatedsessionregression |635|44|Source-derivedadapters; не Revitdispatch |

Actualtotal:225assertions/59PNG.144dpi — offscreenrender, не OS150%. Historical635/44approvalpacket сохранён отдельно и не переписан productionснимками.

Проверены исходные formula/apply/group handlers, footerHTML, modegates,7валидных UI→categorymappings,3режима целевого листа, rawdraftroundtrip(включая17,), minimum/long/lastrow/resize. Производственная модель/хранение/сервис/очистка ownedannotations учитывают независимые Полы/Потолки/Перекрытия;8storage/rolecombinations и legacy1–8defaulttrue проверены внеRevit. Actualcreationresultcomposer отделяет ненулевые успешные действия от всех предупреждений. Прежнийprogress сохранён.

Sourcecontract:93первоначальныхфайла,66безизменений,8explicitfunctionalchanges и25migrationowners с пересечениями, before/afterфиксированы отдельно. Реальные collectors/API/ExternalEventне заменены fixtureкодом. Sharedstyles имеют одного owner. SourceHash проверяет114текстовыхфайлов, принимает только GitLF/CRLFпреобразование и отвергает изменение содержания; исторические rawhashне переписаны.

`Build-Msi.bat` без параметров завершилсяexit0 и создал MSI1.1.0 для2022/2023/2024. Каждый CABизвлечён без установки: DLL1.1.0.0, все looseXAML/styles/Toolkit/localHTML совпали с текущими исходниками/сборкой; APIreferences22/23/24верны, hostAPIdll/previewtestartifacts отсутствуют. [Payloadrecords](Installer-Payload.json). ReleasePrepareOnlyпроверилProductVersion1.1.0 и per-userустановку.

В работающемRevit геометрия/транзакции/nativepicking/ExternalEventне запускались; nativeAPI22runtime, physicalkeyboard/accessibility и OSDPI/monitortransitions остаютсяNOTRUN. Установка не выполнялась. Исходники отправляются в origin/master, MSI — через штатные GitHubReleaseassets, build/cache/outputs вGitне добавляются.


Локальные HTML-инструкции синхронизированы с тремя карточками, независимыми категориями отметок, кнопками «Каталог»/«Выбрать линии» и итоговым уведомлением. Исходный template.css сохранён. `node Tests/Instructions/verify-html.cjs`:46headless viewport checks (1440/390), PASS. Это проверка страниц в браузере, не проверка запуска браузера через кнопку Revit. Последний Build-Msi.bat повторно включил эти инструкции; текущие хеши — Installer-Payload.json.
