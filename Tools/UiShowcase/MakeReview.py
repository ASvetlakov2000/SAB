"""Historical v0.1 generator. Never overwrite current review or coverage."""
from pathlib import Path
import csv, html
root=Path(__file__).resolve().parents[2]
docs=root/'Docs/Ui/DesignSystem'
source=root/'Docs/Ui/ReferenceAudit-2026-10-11/types.csv'
primitive={'ComboBoxItem','ContentControl','ContentPresenter','ItemsPresenter','Path','RepeatButton','Run','ScrollBar','ScrollContentPresenter','Thumb','ToolTip','Track'}
with source.open(encoding='utf-8-sig',newline='') as f, (docs/'component-coverage-v0.1.csv').open('w',encoding='utf-8-sig',newline='') as out:
    writer=csv.writer(out);writer.writerow(['type','preview_status','location_or_note'])
    for row in csv.DictReader(f):
        name=row['type']
        status='template primitive / shared resource' if name in primitive else 'live preview'
        note='ReferenceView / Gallery / shared templates'
        if name=='Ellipse':status='production reference';note='Existing notification/illustrative assets; no new decorative example needed'
        if name=='AdaptiveSettingsGrid':status='source linked';note='SAB/UI/AdaptiveSettingsGrid.cs; no modification; narrow-window transition needs its own host'
        if name in {'CategorySelectionControl','ParameterPicker'}:status='adapted preview';note='CompositeControls.cs; synthetic DTOs, production class unchanged'
        writer.writerow([name,status,note])
concepts=[('Fluent Light','00-FluentLight', '36 / 72 DIP','Больше воздуха и спокойное чтение. Меньше видимых строк.'),('Fluent Compact','01-FluentCompact','28 / 54 DIP','Больше строк на экране. Плотнее составные редакторы.'),('Minimal SaaS','02-MinimalSaas','34 / 66 DIP','Белая композиция, поля с нижней линией. Границы ввода менее явные.'),('SAB Unified','03-Unified','32 / 60 DIP','Рекомендуемый баланс: ясные поля, компактность и спокойная таблица.')]
cards=''.join(f'<article><h2>{title}</h2><p><b>Поле / строка: {density}</b> · {description}</p><a href="screenshots/{file}.png"><img src="screenshots/{file}.png" alt="{title}: реальное WPF-окно" loading="lazy"></a></article>' for title,file,density,description in concepts)
text='''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SAB — выбор дизайн-системы</title><style>
*{box-sizing:border-box}body{margin:0;background:#f6f8fb;color:#202b3c;font:15px/1.5 "Segoe UI",sans-serif}main{max-width:1320px;margin:auto;padding:32px}h1{font-size:28px;margin:0 0 12px}h2{font-size:20px;margin:0 0 8px}p{margin:0 0 16px;color:#566479}nav{display:flex;gap:20px;margin:20px 0;flex-wrap:wrap}a{color:#0f6cbd}article{margin:32px 0 48px}img{display:block;width:100%;height:auto;border:1px solid #d5dde8}table{border-collapse:collapse;width:100%}td,th{padding:8px 12px;text-align:left;border-bottom:1px solid #d5dde8}footer{border-top:1px solid #d5dde8;padding-top:20px}a:focus-visible{outline:2px solid #0f6cbd;outline-offset:3px}
</style><main><h1>SAB — четыре варианта одного окна</h1><p>Черновик 0.1 · 11.10.2026 · ожидает одного выбора. Все изображения — реальный WPF-рендер, одинаковые тестовые данные. Рабочее окно SAB не изменено.</p><nav><a href="Specification.md">Стандарт</a><a href="LayoutPatterns.md">7 схем</a><a href="screenshots/verification.txt">Протокол проверок</a><a href="component-coverage.csv">Реестр компонентов</a></nav><p>Предлагается SAB Unified: современный светлый Windows UI, умеренная плотность и чистая иерархия. Ниже три исходные концепции и объединённый вариант в равном размере. Клик по снимку открывает оригинал.</p>'''+cards+'''<footer><h2>Что подготовлено</h2><p>Интерактивный Preview / Showcase: эталон, компоненты, состояния, 7 схем. Запуск: Tools/UiShowcase/Open.cmd из репозитория SAB.</p><p>Проверены сборка, реальные привязки и команды, поиск Enter/Escape, Space категории, валидация, минимум 980×680, длинные названия, загрузка и виртуализация. Системный масштаб — 100%; 150% проверен рендером 144 dpi, без смены масштаба Windows. В Revit проверки не выполнялись.</p><p>Для утверждения достаточно одного решения: принять Unified либо выбрать один из трёх вариантов. После выбора фиксируются версия и токены; обычные UI-задачи выполняются автономно. Массовая миграция рабочих окон не начата.</p></footer></main></html>'''
(docs/'Review-v0.1.html').write_text(text.replace('href="component-coverage.csv"','href="component-coverage-v0.1.csv"'),encoding='utf8')
print('Review.html and component-coverage.csv created.')
