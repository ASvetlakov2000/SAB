using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shell;
using SAB.InteriorElevations.Models;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public enum ElevationDecorationCatalogWindowAction
    {
        None = 0,
        AddHost = 1,
        AddLinked = 2,
        RemoveSelected = 3,
        ClearRole = 4
    }

    public class ElevationDecorationCatalogWindow : Window
    {
        private readonly ElevationDecorationCatalog _catalog;
        private readonly ElevationDecorationCatalogRole _initialRole;
        private Border _titleBar;
        private Button _closeButton;
        private Button _doneButton;
        private Button _addHostButton;
        private Button _addLinkedButton;
        private Button _removeSelectedButton;
        private Button _clearRoleButton;
        private ComboBox _roleComboBox;
        private ListView _entriesListView;

        public ElevationDecorationCatalogWindow(
            ElevationDecorationCatalog catalog,
            ElevationDecorationCatalogRole initialRole)
        {
            _catalog = catalog ?? new ElevationDecorationCatalog();
            _initialRole = initialRole;
            InitializeWindowFromXamlFile();
            ResolveControls();
            PopulateRoles();
            AttachHandlers();
            RefreshEntries();
        }

        public ElevationDecorationCatalogWindowAction RequestedAction { get; private set; }

        public ElevationDecorationCatalogRole SelectedRole
        {
            get
            {
                ElevationDecorationCatalogRoleOption option =
                    _roleComboBox.SelectedItem as ElevationDecorationCatalogRoleOption;
                return option != null ? option.Role : _initialRole;
            }
        }

        public string SelectedEntryKey
        {
            get
            {
                ElevationDecorationCatalogEntry entry =
                    _entriesListView.SelectedItem as ElevationDecorationCatalogEntry;
                return entry != null ? entry.Key : null;
            }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                typeof(ElevationDecorationCatalogWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_InteriorElevations",
                "Views",
                "ElevationDecorationCatalogWindow.xaml");
            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна каталога не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext context = new ParserContext
                {
                    BaseUri = new Uri(xamlPath, UriKind.Absolute)
                };
                Window loadedWindow = XamlReader.Load(stream, context) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException(
                        "Не удалось загрузить ElevationDecorationCatalogWindow.xaml.");
                }

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                WindowStyle = loadedWindow.WindowStyle;
                AllowsTransparency = loadedWindow.AllowsTransparency;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;

                WindowChrome chrome = WindowChrome.GetWindowChrome(loadedWindow);
                if (chrome != null)
                {
                    WindowChrome.SetWindowChrome(this, (WindowChrome)chrome.Clone());
                }

                WindowSizeSettingsService.Apply(
                    this,
                    "InteriorElevations.ElevationDecorationCatalogWindow.V1");
            }
        }

        private void ResolveControls()
        {
            _titleBar = Find<Border>("TitleBar");
            _closeButton = Find<Button>("CloseWindowButton");
            _doneButton = Find<Button>("DoneButton");
            _addHostButton = Find<Button>("AddHostButton");
            _addLinkedButton = Find<Button>("AddLinkedButton");
            _removeSelectedButton = Find<Button>("RemoveSelectedButton");
            _clearRoleButton = Find<Button>("ClearRoleButton");
            _roleComboBox = Find<ComboBox>("RoleComboBox");
            _entriesListView = Find<ListView>("EntriesListView");

            if (_titleBar == null || _closeButton == null || _doneButton == null ||
                _addHostButton == null || _addLinkedButton == null ||
                _removeSelectedButton == null || _clearRoleButton == null ||
                _roleComboBox == null || _entriesListView == null)
            {
                throw new InvalidOperationException(
                    "Не удалось привязать элементы окна каталога оформления.");
            }
        }

        private void PopulateRoles()
        {
            List<ElevationDecorationCatalogRoleOption> options = Enum
                .GetValues(typeof(ElevationDecorationCatalogRole))
                .Cast<ElevationDecorationCatalogRole>()
                .Select(role => new ElevationDecorationCatalogRoleOption
                {
                    Role = role,
                    DisplayName = ElevationDecorationCatalogRoleNames.GetDisplayName(role)
                })
                .ToList();
            _roleComboBox.ItemsSource = options;
            _roleComboBox.SelectedItem = options.FirstOrDefault(option =>
                option.Role == _initialRole) ?? options.FirstOrDefault();
        }

        private void AttachHandlers()
        {
            _titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                if (args.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };
            _closeButton.Click += delegate { DialogResult = false; Close(); };
            _doneButton.Click += delegate { DialogResult = false; Close(); };
            _roleComboBox.SelectionChanged += delegate { RefreshEntries(); };
            _addHostButton.Click += delegate { Complete(ElevationDecorationCatalogWindowAction.AddHost); };
            _addLinkedButton.Click += delegate { Complete(ElevationDecorationCatalogWindowAction.AddLinked); };
            _removeSelectedButton.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(SelectedEntryKey))
                {
                    Complete(ElevationDecorationCatalogWindowAction.RemoveSelected);
                }
            };
            _clearRoleButton.Click += delegate { Complete(ElevationDecorationCatalogWindowAction.ClearRole); };
        }

        private void Complete(ElevationDecorationCatalogWindowAction action)
        {
            RequestedAction = action;
            DialogResult = true;
            Close();
        }

        private void RefreshEntries()
        {
            if (_entriesListView == null || _roleComboBox == null)
            {
                return;
            }

            ElevationDecorationCatalogRole role = SelectedRole;
            _entriesListView.ItemsSource = (_catalog.Entries ??
                new List<ElevationDecorationCatalogEntry>())
                .Where(entry => entry.Role == role)
                .OrderBy(entry => entry.SourceDocumentTitle)
                .ThenBy(entry => entry.FamilyName)
                .ThenBy(entry => entry.TypeName)
                .ToList();
        }

        private T Find<T>(string name) where T : FrameworkElement
        {
            return FindElementByName<T>(Content as DependencyObject, name);
        }

        private T FindElementByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            FrameworkElement frameworkElement = root as FrameworkElement;
            if (frameworkElement != null &&
                string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
            {
                return frameworkElement as T;
            }

            foreach (object childObject in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject child = childObject as DependencyObject;
                if (child == null)
                {
                    continue;
                }

                T result = FindElementByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
