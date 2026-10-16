# Проверка параметризации1.1.2

Дата13.10.2026. Последнее уточнение пользователя: полностью убрать отчёты подсветки, оставить только уведомление «Подсветка элементов активирована». Предыдущие1.1.1 изменения сохранены; история1.1.0/1.1.1 не переписана.

| Проверка | Результат |
| --- | --- |
| Production/API2023 WPF build | PASS, штатные исходники SAB.dll; Application без host/global dictionaries. |
| Actual targeted WPF |90PASS/14PNG: Sources load/reload/minimum, duplicate-name GUID, programmatic-text guard, routed search/Enter/Escape, explicitactualpopup, fullcatalog120/detachedscope/defaultWalls, WhatCheck normal/minimum/search/empty/validation/lastrow, exactactivationtoast/noReport. |
| Actual existing modules regression |81PASS/27PNG, separate directory, clean Application; current parameterization and views/sheets classes. |
| Source contract |71boundedchecks; full historical Workflow reversal retains fill behavior, existing shared category control unchanged; exact7productionowner delta. |
| Hash portability |118files LF/CRLF accepted, content mutation rejected. |
| Normal installer | Build-Msi.bat noarguments exit0; Release2022/2023/2024, ProductVersion1.1.2/DLL1.1.2.0. |
| Extracted MSI payload | DLL, all looseXAML/styles/Toolkit/23HTML matchcurrentbuild/source; newCheckSettingsCommand/CheckCategories/ArmSearchmetadata present, APIreferences22/23/24, stableUpgradeCodes, nohost/testartifacts. |
| Instructions |23HTML/CSS/Toolkit included in all3MSIs; guide describes separate settings, directhotkey and nohighlightreports. |
| Independent review | Recapture accepted; no material UI/code findings. Final independent MSI gate14.10: PASS, disposition ship for bounded UI/code/payload1.1.2. |

[Компоновка](ParameterCheck-Layout.md), [снимки](ParameterCheck.html), [provenance](screenshots-parameter-checks/provenance.json), [actual log](screenshots-parameter-checks/verification.txt), [regressions](screenshots-parameter-checks/regressions/verification.txt), [77-IDreport](ParameterCheck-Report.json), [review](ParameterCheck-Review.md), [installerproof](ParameterCheck-Installer.json).

Builder found initial minimum620 collapsed list, fixed to680×760 (default760×820), added listheight>=90DIP and complete lastrow tests. Reviewer rejected transparent/black pickerfixture capture; changed only fixtureground/capturetiming, not plugincode; actual open popup separately captured from PART_Popup.Child. Current proof excludes rejected shot.

Commands: `Tools/UiShowcase/Build.ps1 -ParameterChecks`; root `Build-Msi.bat`; `Tools/UiShowcase/MakeParameterCheckEvidence.py`; `Tools/UiShowcase/SourceHash.py`; `Tools/UiShowcase/ValidateUiDocs.py --report Docs/Ui/DesignSystem/ParameterCheck-Report.json`. Current working outputs/ui-production-preview and Installer/output reused.

NOTRUN: native Revit catalog enumeration/ribbon/hotkey, profiletransactions/graphicsfilters/automaticunhighlight/model effects, physicalkeyboard/F4/paste/IME/accessibility, OS DPI/monitor transitions, nativeAPI22runtime, installation. Routed test input is programmatic WPF, not physical input. Localinstallers built and payloadchecked, no newGitHubrelease orinstallation performed. Errorhandling/binding/filter limitations retained; successfulactivationnotice occurs after Apply, failures do not claim success.
