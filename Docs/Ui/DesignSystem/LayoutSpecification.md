# Layout Specification — форма перед реализацией

Составлять для каждого нового/изменяемого окна **до XAML/C#**. Это структура задачи, не второй визуальный стандарт. [Specification](Specification.md), [Workflow](Workflow.md), [реестр](ErrorRegistry.md) обязательны. Старый wireframe0.1 сохранён [в истории](history/LayoutSpecification-0.1.txt).

```text
Задача / разрешённый scope / реальные поверхности, включая secondary:
Источник требований; source files/VM/commands/API boundary:
Approval/migration authority по STATUS:
Пользовательская последовательность и результат:
Обязательные/дополнительные поля, зависимости, условия Visibility/Enabled:
Основное/вторичное/опасное действие; правила сохранения/валидации:
Layout Pattern и обоснование:
Для сложного окна:2 схемы, критерии сравнения и выбор:
Wireframe:header → task groups/data → result/status → fixed footer:
Общий resource owner каждого компонента; отсутствующее coverage:
Minimum / long Russian labels / scrolling / last column / conditional content:
Сохраняемые Binding/Command/DataContext/x:Name/handlers/selection/ExternalEvent:
Applicability всех ID реестра и план evidence/состояний:
Целевые build/runtime/viewports/DPI; что недоступно и NOT RUN:
План независимого review/documenter и границы handoff:
```

Для новых окон не копировать прежние размеры1400×920 или1480×940 без обоснования. Компактный диалог и DockablePane имеют собственные размеры. В текущих примерах параметризация — Tabbed Workspace, виды/листы — Data Manager с source validation. Активная композиция0.5 задана Specification/кодом/снимками; исторические [Redesign0.2](../../../Tools/UiShowcase/Redesign-0.2.md) и [Refinement0.5](../../../Tools/UiShowcase/Refinement-0.5.md) объясняют выбор, а не отменяют текущие нормы.
