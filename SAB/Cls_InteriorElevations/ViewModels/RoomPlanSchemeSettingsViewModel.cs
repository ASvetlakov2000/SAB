using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.ViewModels
{
    /// <summary>
    /// ViewModel окна настроек создания план-схем помещений.
    /// </summary>
    public class RoomPlanSchemeSettingsViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly View _activeView;
        private RevitElementOption _selectedViewTemplate;

        public RoomPlanSchemeSettingsViewModel(Document document, View activeView, RoomPlanSchemeSettings initialSettings = null)
        {
            _document = document;
            _activeView = activeView;

            // Блок предзаполненных значений, которые пользователь может менять в окне.
            NamePart1Text = "План-схема разверток пом. ";
            NamePart2Text = "{Номер помещения}";
            NamePart3Text = string.Empty;
            CropOffsetMmText = "0";

            ViewTemplates = new ObservableCollection<RevitElementOption>();
            LoadViewTemplates();
            ApplyInitialSettings(initialSettings);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<RevitElementOption> ViewTemplates { get; private set; }

        public string NamePart1Text { get; set; }

        public string NamePart2Text { get; set; }

        public string NamePart3Text { get; set; }

        public string CropOffsetMmText { get; set; }

        public RevitElementOption SelectedViewTemplate
        {
            get { return _selectedViewTemplate; }
            set
            {
                _selectedViewTemplate = value;
                OnPropertyChanged(nameof(SelectedViewTemplate));
            }
        }

        public bool TryBuildSettings(out RoomPlanSchemeSettings settings, out string validationMessage)
        {
            settings = null;
            validationMessage = ValidateInput();
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return false;
            }

            settings = new RoomPlanSchemeSettings();
            settings.NamePart1 = NamePart1Text ?? string.Empty;
            settings.NamePart2 = NamePart2Text ?? string.Empty;
            settings.NamePart3 = NamePart3Text ?? string.Empty;
            settings.ViewTemplateId = SelectedViewTemplate != null
                ? SelectedViewTemplate.Id
                : ElementId.InvalidElementId;
            settings.CropOffsetMm = ParseDouble(CropOffsetMmText);
            return true;
        }

        private string ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(NamePart1Text) &&
                string.IsNullOrWhiteSpace(NamePart2Text) &&
                string.IsNullOrWhiteSpace(NamePart3Text))
            {
                return "Формула имени вида не может быть пустой.";
            }

            if (!TryParseDouble(CropOffsetMmText, out _))
            {
                return "Отступ границы обрезки должен быть числом (мм).";
            }

            return string.Empty;
        }

        private void LoadViewTemplates()
        {
            ViewTemplates.Clear();

            RevitElementOption emptyTemplateOption = new RevitElementOption
            {
                Id = ElementId.InvalidElementId,
                DisplayName = "<Не выбран>"
            };
            ViewTemplates.Add(emptyTemplateOption);

            if (_document == null)
            {
                SelectedViewTemplate = emptyTemplateOption;
                return;
            }

            List<RevitElementOption> templates = new List<RevitElementOption>();
            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null || !view.IsTemplate)
                {
                    continue;
                }

                // Блок фильтрации шаблонов: сначала предпочитаем тот же тип вида, что активный план.
                if (_activeView != null && view.ViewType == _activeView.ViewType)
                {
                    templates.Add(new RevitElementOption
                    {
                        Id = view.Id,
                        DisplayName = view.Name
                    });
                }
            }

            // Если шаблоны соответствующего типа не найдены, показываем все шаблоны планов как fallback.
            if (templates.Count == 0)
            {
                foreach (Element element in collector)
                {
                    View view = element as View;
                    if (view == null || !view.IsTemplate)
                    {
                        continue;
                    }

                    if (view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan)
                    {
                        templates.Add(new RevitElementOption
                        {
                            Id = view.Id,
                            DisplayName = view.Name
                        });
                    }
                }
            }

            templates.Sort(delegate (RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < templates.Count; i++)
            {
                ViewTemplates.Add(templates[i]);
            }

            SelectedViewTemplate = ViewTemplates.Count > 0 ? ViewTemplates[0] : null;
        }

        private void ApplyInitialSettings(RoomPlanSchemeSettings initialSettings)
        {
            if (initialSettings == null)
            {
                return;
            }

            NamePart1Text = initialSettings.NamePart1 ?? NamePart1Text;
            NamePart2Text = initialSettings.NamePart2 ?? NamePart2Text;
            NamePart3Text = initialSettings.NamePart3 ?? NamePart3Text;
            CropOffsetMmText = initialSettings.CropOffsetMm.ToString("0.###", CultureInfo.InvariantCulture);

            if (initialSettings.ViewTemplateId == null || initialSettings.ViewTemplateId == ElementId.InvalidElementId)
            {
                return;
            }

            for (int i = 0; i < ViewTemplates.Count; i++)
            {
                RevitElementOption option = ViewTemplates[i];
                if (option != null && RevitElementIdUtils.AreEqual(option.Id, initialSettings.ViewTemplateId))
                {
                    SelectedViewTemplate = option;
                    break;
                }
            }
        }

        private static bool TryParseDouble(string value, out double result)
        {
            result = 0.0;
            string prepared = (value ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(prepared, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static double ParseDouble(string value)
        {
            return TryParseDouble(value, out double result) ? result : 0.0;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

