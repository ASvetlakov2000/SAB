"""Shared semantic capsules for installed SAB guides (standard library only)."""
from html import escape, unescape
from html.parser import HTMLParser
import re


# Action labels from the current SAB windows. Fields, options, tabs and values
# deliberately use the outlined role, even when their label begins with a verb.
BUTTONS = {
    'Добавить правило',
    'Занести параметры',
    'Настройки параметров',
    'Проверить параметры',
    'Снять подсветку',
    'Сохранить настройки',
    'Экспорт конфигурации…',
    'Импорт конфигурации…',
    'Копировать отчёт',
    'Отменить',
    'Инструкция',
    'Инструкции',
    'Показать персонажа',
    'Сохранить',
    'Отмена',
    'Журнал',
    'Открыть журнал в браузере',
    'Показать папку журнала',
    'Откатить',
    'HTML-просмотрщик',
    'Добавить CSV',
    'Выбрать',
    'Открыть просмотрщик',
    'Очистить',
    'Очистить папки',
    'Создать',
    'Удалить',
    'Загрузить таблицу',
    'Удалить выбранное',
    'Назначить выделенным',
    'Добавить фильтр…',
    'Удалить из шаблона',
    'Снять выбор',
    'Применить',
    'Настроить каталог',
    'Пересчитать материалы',
    'Показать все материалы',
    'Создать развертки',
    'Выбрать / изменить',
    'Взять высоту с вида-примера',
    'Выбрать виды',
    'Все виды листа',
    'Выбрать в текущей модели',
    'Выбрать в связанных файлах',
    'Очистить роль',
    'Готово',
    'Оформить',
    'Создать каркас',
    'Пересчитать каркас',
    'Рассчитать',
    'Запустить',
    'Импорт настроек',
    'Экспорт настроек',
    'Сохранить правила',
    'Добавить соответствие',
    'Закрыть',
    'Синхронизировать с файлом хранилища',
    'Создать / обновить',
    '↑ Выше',
    '↓ Ниже',
    'PNG семейств',
    'Системные семейства',
    'Загружаемые семейства',
    'По точке',
    'По линии',
    'По границе',
    '1  Компоненты',
    '1  Экспорт CSV',
    '1  Экспорт материалов',
    '1  Экспорт наименований',
    '2  Применить материалы',
    '2  Применить наименования',
    '2  Размещение',
    '2  Экспорт PNG',
    '3  Загрузка PNG',
    '3  Экспорт PNG',
}

COMMANDS = '''Таймер синхронизации|Уведомления|Библиотека|Создать виды и листы|Удалить виды и листы|Редактор шаблонов|Области заливки из материала|Пересчет материалов|Создать развертки по линии|Повернуть на 180°|Границы видов|На следующий лист|Настройки оформления|Оформить развертки|Экспликации дверей и окон|Создать каркас|Инструкции'''.split('|')
ROUTE = re.compile(r'SAB → (?:Настройки|Библиотека|Альбом и графика|Материалы и ведомости|Развертки|Экспликации|Каркас) → (?:Альбом → )?(?:' + '|'.join(map(re.escape, sorted(COMMANDS, key=len, reverse=True))) + r')')
IDENTIFIERS = re.compile(r'(?<![\w])(?:ctg_system families|ctg_loadable families|ctg_lines-patterns|PNG_Pirogi|PNG_Lines|PNG_Fills|Библиотека_Стили линий|Библиотека_Штриховки|Profiles\.csv|CuttingPlan\.csv|assets)(?![\w])')
TOKEN = re.compile(ROUTE.pattern + r'|«[^»]+»|' + IDENTIFIERS.pattern)


def chip(label, role=None):
    role = role or ('cmd' if label in BUTTONS else 'kbd')
    return f'<span class="{role}">{escape(label)}</span>'


def format_text(text):
    def replace(match):
        value = match.group()
        if value.startswith('SAB → '):
            parts = value.split(' → ')
            return ' → '.join(chip(part, 'cmd' if i == len(parts) - 1 else 'kbd') for i, part in enumerate(parts))
        return chip(value[1:-1] if value.startswith('«') else value)
    return TOKEN.sub(replace, text)


class Markup(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=False)
        self.output = []
        self.stack = []

    def handle_starttag(self, tag, attrs):
        self.output.append(self.get_starttag_text())
        if tag not in {'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr'}:
            self.stack.append((tag, dict(attrs)))

    def handle_startendtag(self, tag, attrs):
        self.output.append(self.get_starttag_text())

    def handle_endtag(self, tag):
        self.output.append(f'</{tag}>')
        if self.stack and self.stack[-1][0] == tag:
            self.stack.pop()

    def handle_data(self, text):
        active = any(tag == 'main' for tag, _ in self.stack)
        excluded = any(tag in {'h1', 'h2', 'h3', 'summary', 'a', 'script', 'style'} or
                       set(attrs.get('class', '').split()) & {'cmd', 'kbd', 'hero'} for tag, attrs in self.stack)
        self.output.append(format_text(text) if active and not excluded else text)

    def handle_entityref(self, name):
        self.output.append(f'&{name};')

    def handle_charref(self, name):
        self.output.append(f'&#{name};')

    def handle_decl(self, decl):
        self.output.append(f'<!{decl}>')

    def handle_comment(self, comment):
        self.output.append(f'<!--{comment}-->')


def apply_markup(html):
    # Reclassify older parameter-guide capsules without nesting new spans.
    html = re.sub(r'<span class="cmd">([^<]*)</span>',
                  lambda m: chip(unescape(m[1]), 'cmd' if unescape(m[1]) in BUTTONS or unescape(m[1]) in COMMANDS else 'kbd'), html)
    parser = Markup()
    parser.feed(html)
    parser.close()
    return ''.join(parser.output)
