using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sab.UiReview
{
    public sealed class NumericValue : IDataErrorInfo
    {
        public string Value { get; set; }
        public string Error => null;
        public string this[string columnName] => double.TryParse(Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out _) ? null : "Введите число";
    }
    public sealed class ReviewRow : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        readonly Dictionary<string, object> values = new Dictionary<string, object>();
        T Get<T>(T fallback, [CallerMemberName] string key = null) => values.TryGetValue(key, out var value) ? (T)value : fallback;
        void Set(object value, [CallerMemberName] string key = null) { values[key] = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key)); }
        public bool Enabled { get => Get(true); set => Set(value); }
        public bool Required { get => Get(true); set => Set(value); }
        public string Name { get => Get(""); set => Set(value); }
        public string Number { get => Get(""); set => Set(value); }
        public string Sheet { get => Get(""); set => Set(value); }
        public string Scale { get => Get("50"); set => Set(value); }
        public string Template { get => Get("АР · Рабочий"); set => Set(value); }
        public string Floor { get => Get("01 этаж"); set => Set(value); }
        public string Kind { get => Get("План этажа"); set => Set(value); }
        public string Group { get => Get("Помещение"); set => Set(value); }
        public string Source { get => Get("Из помещения"); set => Set(value); }
        public string Value { get => Get(""); set => Set(value); }
        public string Input { get => Get(""); set => Set(value); }
        public string Output { get => Get(""); set => Set(value); }
        public string Detail { get => Get(""); set => Set(value); }
        public string RoomField { get => Get("Параметр помещения"); set => Set(value); }
        public string RoomParameter { get => Get("Корпус"); set => Set(value); }
        public string ElementParameter { get => Get("Комментарии"); set => Set(value); }
        public string ManualValues { get => Get("А; Б; В"); set => Set(value); }
        public string Status { get => Get("К проверке"); set => Set(value); }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class ReviewData
    {
        public ObservableCollection<ReviewRow> Elevations { get; } = new ObservableCollection<ReviewRow>();
        public ObservableCollection<ReviewRow> Sheets { get; } = new ObservableCollection<ReviewRow>();
        public ObservableCollection<ReviewRow> Rules { get; } = new ObservableCollection<ReviewRow>();
        public ObservableCollection<ReviewRow> Levels { get; } = new ObservableCollection<ReviewRow>();
        public ObservableCollection<ReviewRow> Mappings { get; } = new ObservableCollection<ReviewRow>();
        public ObservableCollection<ReviewRow> Categories { get; } = new ObservableCollection<ReviewRow>();
        public readonly string[] Sources = { "Из помещения", "Из модели: уровень", "Из модели: параметр", "Постоянное значение", "По таблице сопоставления", "Выбрать перед записью", "Корпус вручную" };
        public readonly string[] Groups = { "Зона", "Уровень", "Местоположение", "Помещение" };
        public ReviewData()
        {
            for (int i = 0; i < 8; i++) Elevations.Add(new ReviewRow { Name = $"101 · Переговорная · {i + 1}–{i + 2}", Number = "АР-101", Sheet = "Развёртки. Переговорная", Template = "АР · Развёртки", Detail = $"Углы {i + 1}–{i + 2}", Floor = "01 этаж" });
            string[] names = { "План первого этажа", "План потолков первого этажа", "План второго этажа", "План потолков второго этажа", "План третьего этажа", "План потолков третьего этажа" };
            for (int i = 0; i < names.Length; i++) Sheets.Add(new ReviewRow { Name = names[i], Number = $"АР-{i + 1:00}", Sheet = names[i], Scale = "100", Floor = $"{i / 2 + 1:00} этаж", Kind = i % 2 == 0 ? "План этажа" : "План потолков", Template = i % 2 == 0 ? "АР · Планы" : "АР · Потолки" });
            Rules.Add(new ReviewRow { Group = "Зона", Name = "SAB_Зона", Source = "Из помещения", Value = "А", Detail = "Параметр помещения: Корпус" });
            Rules.Add(new ReviewRow { Group = "Уровень", Name = "SAB_Уровень", Source = "Из модели: уровень", Value = "01", Detail = "Таблица уровней" });
            Rules.Add(new ReviewRow { Group = "Местоположение", Name = "SAB_Местоположение", Source = "Постоянное значение", Value = "Внутри", Detail = "Постоянное значение" });
            Rules.Add(new ReviewRow { Group = "Помещение", Name = "SAB_Номер помещения", Source = "Из помещения", Value = "101", RoomField = "Номер помещения", Detail = "Номер помещения" });
            Rules.Add(new ReviewRow { Group = "Помещение", Name = "SAB_Имя помещения", Source = "Из помещения", Value = "Переговорная", RoomField = "Имя помещения", Detail = "Имя помещения" });
            Rules.Add(new ReviewRow { Group = "Помещение", Name = "SAB_Отделка", Source = "Из помещения", Value = "Тип 02", RoomParameter = "Отделка", Required = false, Detail = "Параметр помещения: Отделка" });
            for (int i = 0; i < 5; i++) Levels.Add(new ReviewRow { Input = $"{i + 1:00} этаж", Output = $"{i + 1:00}" });
            Mappings.Add(new ReviewRow { Input = "Офис", Output = "Рабочее помещение" });
            Mappings.Add(new ReviewRow { Input = "Переговорная", Output = "Общее помещение" });
            string[] categories = { "Стены", "Перекрытия", "Потолки", "Двери", "Окна", "Обобщённые модели", "Мебель", "Оборудование", "Крыши", "Колонны" };
            for (int i = 0; i < categories.Length; i++) Categories.Add(new ReviewRow { Name = categories[i], Enabled = i < 6 });
        }
    }
}
