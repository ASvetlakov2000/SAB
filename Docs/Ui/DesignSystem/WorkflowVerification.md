# Проверка постоянного workflow — 11.10.2026

Область: документы, учёт покрытия, шаблон отчёта и read-only validator. Новый UI не реализовывался, heavy build/capture/Revit не запускались. Используется существующее доказательство0.5; оно не становится новым runtime PASS этой задачи.

## Результат

- Созданы единый реестр58 действующих ID и история7 отменённых норм; к каждой проверке привязаны источник, симптом/правило, общий владелец/профилактика, критерий и честное доказательство/статус. User requirements отделены от reviewer findings.
- Одна текущая Specification вместо конкурирующих разделов0.3/0.4/0.5. Прежний текст сохранён в history; historical banners у планов и backlog. Действующие AGENTS/repo skill ведут в единый пакет, а не повторяют версии правил.
- До/после checklist, постоянные контракты существующих builder/reviewer/documenter, форма Layout Specification и JSON отчёт каждого ID. Новый сеанс может найти их из AGENTS. Участие агента не выдаётся за постоянную инструкцию.
- Coverage51 типов:28 current_example,11 resource_only,12 missing. Отдельный CoveragePlan описывает все12 пробелов, недоказанные состояния и будущий общий production-слой. Исключённые Gallery/ReferenceView/CompositeControls0.1 больше не дают live claims. Historical MakeReview.py теперь пишет только versioned0.1 review/CSV и не перезаписывает текущие файлы.
- Первый будущий перенос выбран для обоих модулей: main/secondary виды-листы,4 вкладки параметров и реальные ParameterPicker/CategorySelectionControl. Он будет поручен отдельным последовательным промптом; production/установка сейчас не разрешены.

## Выполненные проверки

`Tools/UiShowcase/ValidateUiDocs.py --report Docs/Ui/DesignSystem/WorkflowReport-2026-10-11.json` — PASS:58 уникальных ID,16 связанных текущих документов,58 пустых NOT RUN записей шаблона, полнота заполненного отчёта,51 уникальный тип/существующие evidence paths, sidecar↔STATUS,27 SHA256 существующих captures, прежние75 PASS/COMPLETED,135/103 bindings и10 columns/methods.

`skill-creator/scripts/quick_validate.py .agents/skills/sab-wpf-ui` — PASS, с существующим PyYAML в ignored outputs/ui-tool-deps; зависимости продукта не менялись.

SHA256 одиннадцати активных UI/host/build файлов совпадают с backup outputs/workflow-before-20261011-231538. STATUS.json побайтно совпадает с backup:0.5.0-source-review,pending_user_approval,null/null,false. `git diff --check` и `git diff --exit-code -- SAB` — PASS. Commit/push не выполнялись.

[Отчёт текущей документационной задачи](WorkflowReport-2026-10-11.json):4 documentation PASS (006/010/048/056),54 N/A для неизменяемых этой задачей UI-сценариев. Это не58 runtime PASS. При следующей UI-задаче применимость заполняется заново; недоступная проверка — NOT RUN, отсутствие сценария — N/A с причиной.

## Границы

Реестр снижает повторение известных ошибок, не гарантирует отсутствие будущих. Для всех58 production критерии0.5 не выполнялись. Существующие ограничения [Verification](Verification.md) сохраняются: actual Revit/Document/transactions/ExternalEvent, physical input/drag-drop/full keyboard/accessibility, actual browser launch, OS DPI transitions NOT RUN;144dpi только offscreen render. Полная актуальная галерея/production migration не завершены. Независимая документационная оценка — **ship**; дословный ответ в [WorkflowReview](WorkflowReview.md). UI-verdict этим документом не пересматривается.
