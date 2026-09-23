using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.ViewModels;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public enum ElevationSettingsWindowAction
    {
        Cancel = 0,
        PickSelection = 1,
        Create = 2,
        PickSheetPoint = 3,
        PickCropByExample = 4
    }

    public partial class ElevationSettingsWindow : Window
    {
        private static readonly HashSet<string> ValidFormulaParameters = new HashSet<string>(StringComparer.Ordinal)
        {
            "[Номер помещения]",
            "[Имя помещения]",
            "[Начальный угол]",
            "[Конечный угол]",
            "[Помещения]"
        };

        private readonly ElevationSettingsViewModel _viewModel;
        private readonly bool _initialHasSelection;
        private readonly string _initialSelectionStatusText;
        private readonly string _initialWarningInfoText;

        private Button _okButton;
        private Button _cancelButton;
        private Button _pickLinesButton;
        private Button _pickSheetPointButton;
        private Button _pickCropByExampleButton;
        private RichTextBox _elevationNameFormulaEditor;
        private RichTextBox _elevationTitleFormulaEditor;
        private RichTextBox _sheetNameFormulaEditor;
        private RichTextBox _roomPlanNameFormulaEditor;
        private Border _selectionStatusBorder;
        private Border _warningInfoBorder;
        private TextBlock _selectionStatusTextBlock;
        private TextBlock _warningInfoTextBlock;
        private bool _isUpdatingFormulaEditors;

        public ElevationSettingsWindow(ElevationSettingsViewModel viewModel)
            : this(viewModel, false, "Линии и помещение не выбраны.", string.Empty)
        {
        }

        public ElevationSettingsWindow(
            ElevationSettingsViewModel viewModel,
            bool initialHasSelection,
            string initialSelectionStatusText,
            string initialWarningInfoText)
        {
            _viewModel = viewModel;
            _initialHasSelection = initialHasSelection;
            _initialSelectionStatusText = string.IsNullOrWhiteSpace(initialSelectionStatusText)
                ? "Линии и помещение не выбраны."
                : initialSelectionStatusText;
            _initialWarningInfoText = string.IsNullOrWhiteSpace(initialWarningInfoText)
                ? "Перед созданием нажмите Выбрать линии, затем укажите линии детализации и помещение в активном плане. Параметры сохраняются и будут использованы при следующем запуске команды."
                : initialWarningInfoText;

            // Основной блок инициализации окна: загружаем XAML, назначаем DataContext и подключаем кнопки.
            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            InitializeFormulaEditors();
            ApplyInitialSelectionUiState();
            AttachButtonHandlers();
        }

        public ElevationSettings SelectedSettings { get; private set; }

        public ElevationSettingsWindowAction RequestedAction { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ElevationSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "ElevationSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл настроек не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить ElevationSettingsWindow.xaml.");
                }

                _okButton = loadedWindow.FindName("OkButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;
                _pickLinesButton = loadedWindow.FindName("PickLinesButton") as Button;
                _pickSheetPointButton = loadedWindow.FindName("PickSheetPointButton") as Button;
                _pickCropByExampleButton = loadedWindow.FindName("PickCropByExampleButton") as Button;
                _elevationNameFormulaEditor = loadedWindow.FindName("ElevationNameFormulaEditor") as RichTextBox;
                _elevationTitleFormulaEditor = loadedWindow.FindName("ElevationTitleFormulaEditor") as RichTextBox;
                _sheetNameFormulaEditor = loadedWindow.FindName("SheetNameFormulaEditor") as RichTextBox;
                _roomPlanNameFormulaEditor = loadedWindow.FindName("RoomPlanNameFormulaEditor") as RichTextBox;
                _selectionStatusBorder = loadedWindow.FindName("SelectionStatusBorder") as Border;
                _warningInfoBorder = loadedWindow.FindName("WarningInfoBorder") as Border;
                _selectionStatusTextBlock = loadedWindow.FindName("SelectionStatusTextBlock") as TextBlock;
                _warningInfoTextBlock = loadedWindow.FindName("WarningInfoTextBlock") as TextBlock;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Style = loadedWindow.Style;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                FontWeight = loadedWindow.FontWeight;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;

                WindowSizeSettingsService.Apply(this, "InteriorElevations.ElevationSettingsWindow.V2");
            }
        }

        private void InitializeFormulaEditors()
        {
            _elevationNameFormulaEditor = _elevationNameFormulaEditor ??
                                          FindElementByName<RichTextBox>(Content as DependencyObject, "ElevationNameFormulaEditor");
            _elevationTitleFormulaEditor = _elevationTitleFormulaEditor ??
                                           FindElementByName<RichTextBox>(Content as DependencyObject, "ElevationTitleFormulaEditor");
            _sheetNameFormulaEditor = _sheetNameFormulaEditor ??
                                      FindElementByName<RichTextBox>(Content as DependencyObject, "SheetNameFormulaEditor");
            _roomPlanNameFormulaEditor = _roomPlanNameFormulaEditor ??
                                         FindElementByName<RichTextBox>(Content as DependencyObject, "RoomPlanNameFormulaEditor");

            if (_elevationNameFormulaEditor == null ||
                _elevationTitleFormulaEditor == null ||
                _sheetNameFormulaEditor == null ||
                _roomPlanNameFormulaEditor == null)
            {
                throw new InvalidOperationException("Не удалось привязать редакторы формул наименований.");
            }

            SetFormulaEditorText(_elevationNameFormulaEditor, _viewModel.ElevationNameFormulaText);
            SetFormulaEditorText(_elevationTitleFormulaEditor, _viewModel.ElevationTitleFormulaText);
            SetFormulaEditorText(_sheetNameFormulaEditor, _viewModel.SheetNameFormulaText);
            SetFormulaEditorText(_roomPlanNameFormulaEditor, _viewModel.RoomPlanNameFormulaText);

            AttachFormulaEditor(_elevationNameFormulaEditor);
            AttachFormulaEditor(_elevationTitleFormulaEditor);
            AttachFormulaEditor(_sheetNameFormulaEditor);
            AttachFormulaEditor(_roomPlanNameFormulaEditor);
        }

        private void AttachFormulaEditor(RichTextBox editor)
        {
            editor.TextChanged += FormulaEditor_TextChanged;
            editor.PreviewKeyDown += FormulaEditor_PreviewKeyDown;
        }

        private void FormulaEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                e.Handled = true;
            }
        }

        private void FormulaEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFormulaEditors)
            {
                return;
            }

            RichTextBox editor = sender as RichTextBox;
            if (editor == null)
            {
                return;
            }

            int caretOffset = GetCaretTextOffset(editor);
            string formula = GetFormulaEditorText(editor)
                .Replace("\r", " ")
                .Replace("\n", " ");

            if (ReferenceEquals(editor, _elevationNameFormulaEditor))
            {
                _viewModel.ElevationNameFormulaText = formula;
            }
            else if (ReferenceEquals(editor, _elevationTitleFormulaEditor))
            {
                _viewModel.ElevationTitleFormulaText = formula;
            }
            else if (ReferenceEquals(editor, _sheetNameFormulaEditor))
            {
                _viewModel.SheetNameFormulaText = formula;
            }
            else if (ReferenceEquals(editor, _roomPlanNameFormulaEditor))
            {
                _viewModel.RoomPlanNameFormulaText = formula;
            }

            ApplyFormulaHighlighting(editor, formula, caretOffset);
        }

        private void SetFormulaEditorText(RichTextBox editor, string formula)
        {
            string normalizedFormula = formula ?? string.Empty;
            ApplyFormulaHighlighting(editor, normalizedFormula, normalizedFormula.Length);
        }

        private void ApplyFormulaHighlighting(RichTextBox editor, string formula, int caretOffset)
        {
            _isUpdatingFormulaEditors = true;
            try
            {
                string safeFormula = formula ?? string.Empty;
                Brush accentBrush = TryFindResource("SabBrush.Accent") as Brush ?? Brushes.DodgerBlue;
                Brush textBrush = TryFindResource("SabBrush.FormulaText") as Brush ?? Brushes.DarkOrange;

                Paragraph paragraph = new Paragraph();
                paragraph.Margin = new Thickness(0);

                int textIndex = 0;
                MatchCollection matches = Regex.Matches(safeFormula, @"\[[^\[\]\r\n]+\]");
                foreach (Match match in matches)
                {
                    if (match.Index > textIndex)
                    {
                        Run plainRun = new Run(safeFormula.Substring(textIndex, match.Index - textIndex));
                        plainRun.Foreground = textBrush;
                        paragraph.Inlines.Add(plainRun);
                    }

                    Run parameterRun = new Run(match.Value);
                    if (ValidFormulaParameters.Contains(match.Value))
                    {
                        parameterRun.Foreground = accentBrush;
                        parameterRun.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        parameterRun.Foreground = textBrush;
                    }

                    paragraph.Inlines.Add(parameterRun);
                    textIndex = match.Index + match.Length;
                }

                if (textIndex < safeFormula.Length)
                {
                    Run plainRun = new Run(safeFormula.Substring(textIndex));
                    plainRun.Foreground = textBrush;
                    paragraph.Inlines.Add(plainRun);
                }

                editor.Document.Blocks.Clear();
                editor.Document.Blocks.Add(paragraph);
                editor.CaretPosition = GetTextPositionAtOffset(
                    editor.Document,
                    Math.Max(0, Math.Min(caretOffset, safeFormula.Length)));
            }
            finally
            {
                _isUpdatingFormulaEditors = false;
            }
        }

        private string GetFormulaEditorText(RichTextBox editor)
        {
            TextRange range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
            return range.Text.TrimEnd('\r', '\n');
        }

        private int GetCaretTextOffset(RichTextBox editor)
        {
            TextRange range = new TextRange(editor.Document.ContentStart, editor.CaretPosition);
            return range.Text.TrimEnd('\r', '\n').Length;
        }

        private TextPointer GetTextPositionAtOffset(FlowDocument document, int textOffset)
        {
            TextPointer navigator = document.ContentStart;
            int traversedCharacters = 0;
            while (navigator != null)
            {
                if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = navigator.GetTextInRun(LogicalDirection.Forward);
                    if (traversedCharacters + textRun.Length >= textOffset)
                    {
                        TextPointer target = navigator.GetPositionAtOffset(
                            textOffset - traversedCharacters,
                            LogicalDirection.Forward);
                        return target ?? document.ContentEnd;
                    }

                    traversedCharacters += textRun.Length;
                }

                navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
            }

            return document.ContentEnd;
        }

        private void AttachButtonHandlers()
        {
            if (_okButton == null)
            {
                _okButton = FindElementByName<Button>(Content as DependencyObject, "OkButton");
            }

            if (_cancelButton == null)
            {
                _cancelButton = FindElementByName<Button>(Content as DependencyObject, "CancelButton");
            }

            if (_pickLinesButton == null)
            {
                _pickLinesButton = FindElementByName<Button>(Content as DependencyObject, "PickLinesButton");
            }

            if (_pickSheetPointButton == null)
            {
                _pickSheetPointButton = FindElementByName<Button>(Content as DependencyObject, "PickSheetPointButton");
            }

            if (_pickCropByExampleButton == null)
            {
                _pickCropByExampleButton = FindElementByName<Button>(Content as DependencyObject, "PickCropByExampleButton");
            }

            if (_selectionStatusBorder == null)
            {
                _selectionStatusBorder = FindElementByName<Border>(Content as DependencyObject, "SelectionStatusBorder");
            }

            if (_warningInfoBorder == null)
            {
                _warningInfoBorder = FindElementByName<Border>(Content as DependencyObject, "WarningInfoBorder");
            }

            if (_selectionStatusTextBlock == null)
            {
                _selectionStatusTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "SelectionStatusTextBlock");
            }

            if (_warningInfoTextBlock == null)
            {
                _warningInfoTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "WarningInfoTextBlock");
            }

            if (_okButton == null ||
                _cancelButton == null ||
                _pickLinesButton == null ||
                _pickSheetPointButton == null ||
                _pickCropByExampleButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки окна настроек.");
            }

            _okButton.Click += OkButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _pickLinesButton.Click += PickLinesButton_Click;
            _pickSheetPointButton.Click += PickSheetPointButton_Click;
            _pickCropByExampleButton.Click += PickCropByExampleButton_Click;
        }

        private void ApplyInitialSelectionUiState()
        {
            if (_selectionStatusTextBlock == null)
            {
                _selectionStatusTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "SelectionStatusTextBlock");
            }

            if (_selectionStatusBorder == null)
            {
                _selectionStatusBorder = FindElementByName<Border>(Content as DependencyObject, "SelectionStatusBorder");
            }

            if (_warningInfoTextBlock == null)
            {
                _warningInfoTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "WarningInfoTextBlock");
            }

            if (_warningInfoBorder == null)
            {
                _warningInfoBorder = FindElementByName<Border>(Content as DependencyObject, "WarningInfoBorder");
            }

            if (_selectionStatusTextBlock != null)
            {
                _selectionStatusTextBlock.Text = _initialSelectionStatusText;
            }

            ApplySelectionState(_initialHasSelection);
            SetWarningInfoText(_initialWarningInfoText);
        }

        private T FindElementByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            FrameworkElement frameworkElement = root as FrameworkElement;
            if (frameworkElement != null && string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
            {
                return frameworkElement as T;
            }

            foreach (object childObject in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject childDependencyObject = childObject as DependencyObject;
                if (childDependencyObject == null)
                {
                    continue;
                }

                T nestedChild = FindElementByName<T>(childDependencyObject, name);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ElevationSettings settings;
            string validationMessage;

            if (!_viewModel.TryBuildSettings(out settings, out validationMessage))
            {
                SetWarningInfoText(validationMessage);
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            SelectedSettings = settings;
            RequestedAction = ElevationSettingsWindowAction.Create;
            DialogResult = true;
            Close();
        }

        private void PickLinesButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = ElevationSettingsWindowAction.PickSelection;
            DialogResult = true;
            Close();
        }

        private void PickSheetPointButton_Click(object sender, RoutedEventArgs e)
        {
            ElevationSettings settings;
            string validationMessage;

            if (!_viewModel.TryBuildSettings(out settings, out validationMessage, false))
            {
                SetWarningInfoText(validationMessage);
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            SelectedSettings = settings;
            RequestedAction = ElevationSettingsWindowAction.PickSheetPoint;
            DialogResult = true;
            Close();
        }

        private void PickCropByExampleButton_Click(object sender, RoutedEventArgs e)
        {
            ElevationSettings settings;
            string validationMessage;

            if (!_viewModel.TryBuildSettings(out settings, out validationMessage))
            {
                SetWarningInfoText(validationMessage);
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            SelectedSettings = settings;
            RequestedAction = ElevationSettingsWindowAction.PickCropByExample;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = ElevationSettingsWindowAction.Cancel;
            DialogResult = false;
            Close();
        }

        private void ApplySelectionState(bool hasSelection)
        {
            if (_okButton != null)
            {
                _okButton.IsEnabled = hasSelection;
                _okButton.ToolTip = hasSelection
                    ? null
                    : "Сначала нажмите Выбрать линии и укажите линии детализации и помещение.";
            }

            Style panelStyle = TryFindResource(hasSelection ? "SabAccentInfoPanelStyle" : "SabSelectionPendingPanelStyle") as Style;
            Style textStyle = TryFindResource(hasSelection ? "SabAccentInfoTextStyle" : "SabSelectionPendingTextStyle") as Style;

            if (_selectionStatusBorder != null && panelStyle != null)
            {
                _selectionStatusBorder.Style = panelStyle;
            }

            if (_selectionStatusTextBlock != null && textStyle != null)
            {
                _selectionStatusTextBlock.Style = textStyle;
            }
        }

        private void SetWarningInfoText(string text)
        {
            if (_warningInfoTextBlock == null)
            {
                return;
            }

            _warningInfoTextBlock.Text = string.IsNullOrWhiteSpace(text)
                ? "Перед созданием нажмите Выбрать линии, затем укажите линии детализации и помещение в активном плане. Параметры сохраняются и будут использованы при следующем запуске команды."
                : text;

            Style panelStyle = TryFindResource("SabAccentInfoPanelStyle") as Style;
            Style textStyle = TryFindResource("SabAccentInfoTextStyle") as Style;

            if (_warningInfoBorder != null && panelStyle != null)
            {
                _warningInfoBorder.Style = panelStyle;
            }

            if (textStyle != null)
            {
                _warningInfoTextBlock.Style = textStyle;
            }
        }
    }
}
