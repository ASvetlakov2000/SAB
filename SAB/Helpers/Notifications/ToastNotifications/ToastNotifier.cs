using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Helpers.Notifications.ToastNotifications
{
    /// <summary>
    /// Статический класс для отображения Toast уведомлений в Revit.
    /// Позволяет показывать уведомления с иконкой, заголовком и текстом.
    /// Всплывает слева внизу экрана.
    /// </summary>
    public static class ToastNotifier
    {
        // Невидимое окно, создающее WPF-контекст для Revit
        private static Window _dummyWindow;

        // Хост уведомлений, внутри которого располагаются все Toast
        private static ToastHost _host;

        // Статический конструктор выполняется один раз при первом использовании класса
        static ToastNotifier()
        {
            // Создаем невидимое окно для работы WPF
            _dummyWindow = new Window
            {
                Width = 0, // нулевая ширина
                Height = 0, // нулевая высота
                ShowInTaskbar = false, // не отображать в панели задач
                WindowStyle = WindowStyle.None, // без рамок
                AllowsTransparency = true, // поддержка прозрачности
                Opacity = 0 // полностью прозрачное
            };
            _dummyWindow.Show(); // показываем окно, чтобы WPF-контекст был активен

            // Создаем хост уведомлений
            _host = new ToastHost
            {
                Owner = _dummyWindow,
                Left = 10, // смещение слева
                Top = SystemParameters.WorkArea.Bottom - 260 - 10 // смещение от нижнего края экрана
            };
            _host.Show(); // показываем хост
        }

        // Методы для показа уведомлений разных типов
        public static void ShowInfo(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Info, durationSeconds);

        public static void ShowSuccess(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Success, durationSeconds);

        public static void ShowWarning(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Warning, durationSeconds);

        public static void ShowError(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Error, durationSeconds);
    }

    /// <summary>
    /// Перечисление типов уведомлений
    /// </summary>
    public enum ToastType { Info, Success, Warning, Error }

    /// <summary>
    /// Окно-хост для уведомлений
    /// </summary>
    public class ToastHost : Window
    {
        // StackPanel для хранения всех активных уведомлений
        private StackPanel _stackPanel;

        public ToastHost()
        {
            Width = 340; // ширина хоста
            Height = 260; // высота хоста
            Topmost = true; // всегда поверх других окон
            AllowsTransparency = true; // поддержка прозрачности
            WindowStyle = WindowStyle.None; // без рамок
            Background = Brushes.Transparent; // прозрачный фон
            ShowInTaskbar = false; // не показывать в панели задач

            // Инициализация StackPanel для уведомлений
            _stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, // уведомления снизу
                HorizontalAlignment = HorizontalAlignment.Left, // слева
                Margin = new Thickness(10) // отступы от краев хоста
            };

            Content = _stackPanel; // устанавливаем StackPanel как содержимое окна
        }

        /// <summary>
        /// Метод показа уведомления
        /// </summary>
        /// <param name="title">Заголовок уведомления</param>
        /// <param name="message">Текст уведомления</param>
        /// <param name="type">Тип уведомления (Info, Success, Warning, Error)</param>
        /// <param name="durationSeconds">Время отображения в секундах</param>
        public void ShowToast(string title, string message, ToastType type, int durationSeconds)
        {
            // Создаем основной контейнер уведомления (Border)
            var border = new Border
            {
                Background = GetBackground(type), // цвет фона в зависимости от типа
                CornerRadius = new CornerRadius(8), // закругленные углы
                Margin = new Thickness(0, 0, 0, 5), // отступ между уведомлениями
                Padding = new Thickness(10), // внутренние отступы
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Grid вместо StackPanel для корректного растягивания и контроля размеров
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };

            // Определяем колонки: иконка, отступ, разделитель, отступ, текст
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // иконка
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) }); // отступ
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) }); // разделитель
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // отступ
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // текст

            // Иконка уведомления
            var icon = new TextBlock
            {
                Text = GetIcon(type), // символ для типа уведомления
                FontSize = 26, // размер иконки
                Foreground = Brushes.White, // цвет иконки
                VerticalAlignment = VerticalAlignment.Center, // выравнивание по центру рамки
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0)
            };
            Grid.SetColumn(icon, 0);

            // Вертикальный разделитель между иконкой и текстом
            var separator = new Rectangle
            {
                Width = 1.5, // толщина линии
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), // полупрозрачный белый
                VerticalAlignment = VerticalAlignment.Stretch // растягиваем по высоте Border
            };
            Grid.SetColumn(separator, 2);

            // StackPanel для текста (заголовок + сообщение)
            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Заголовок уведомления с подчеркиванием
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold, // жирный
                Foreground = Brushes.White, // цвет текста
                FontSize = 16, // размер
                TextWrapping = TextWrapping.Wrap, // перенос строк
                TextDecorations = TextDecorations.Underline // подчёркивание
            };

            // Основной текст уведомления
            var messageBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White, // цвет текста
                FontSize = 14, // размер текста
                TextWrapping = TextWrapping.Wrap, // перенос строк
                MaxWidth = 240, // максимальная ширина текста для переноса
                Margin = new Thickness(0, 2, 0, 0) // небольшой отступ сверху
            };

            // Добавляем заголовок и текст в вертикальный стек
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(messageBlock);
            Grid.SetColumn(textStack, 4);

            // Добавляем иконку, разделитель и текст в Grid
            grid.Children.Add(icon);
            grid.Children.Add(separator);
            grid.Children.Add(textStack);

            // Размещаем Grid в Border
            border.Child = grid;

            // Вставляем уведомление в начало списка (сверху StackPanel)
            _stackPanel.Children.Insert(0, border);

            // Анимация появления (плавное проявление)
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Таймер для автоматического закрытия уведомления
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop(); // остановка таймера
                // Анимация скрытия (плавное исчезновение)
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s2, e2) => _stackPanel.Children.Remove(border); // удаляем уведомление после анимации
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        /// <summary>
        /// Возвращает цвет фона уведомления в зависимости от типа
        /// </summary>
        private Brush GetBackground(ToastType type)
        {
            return type switch
            {
                ToastType.Info => new SolidColorBrush(Color.FromRgb(52, 152, 219)), // синий
                ToastType.Success => new SolidColorBrush(Color.FromRgb(46, 204, 113)), // зеленый
                ToastType.Warning => new SolidColorBrush(Color.FromRgb(241, 196, 15)), // желтый
                ToastType.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)), // красный
                _ => Brushes.Gray
            };
        }

        /// <summary>
        /// Возвращает символ иконки уведомления
        /// </summary>
        private string GetIcon(ToastType type)
        {
            return type switch
            {
                ToastType.Info => "ℹ",        // информация
                ToastType.Success => "✔",     // галочка
                ToastType.Warning => "!",     // восклицательный знак
                ToastType.Error => "✖",       // крестик
                _ => "?"
            };
        }
    }
}