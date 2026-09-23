using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.ViewModels
{
    public sealed class FrameModuleViewModel : INotifyPropertyChanged
    {
        private ElementTypeOption _selectedCurtainWallType;
        private string _studSpacingText;
        private bool _processDoors;
        private double _selectedCwPurchaseLength;
        private double _selectedUwPurchaseLength;
        private string _cutLossText;
        private string _minimumReusableOffcutText;
        private bool _performCutting;
        private bool _exportCsv;
        private string _exportDirectory;
        private string _validationMessage;

        public FrameModuleViewModel(
            FrameCommandMode mode,
            int selectedWallCount,
            int calculationMullionCount,
            IEnumerable<ElementTypeOption> curtainWallTypes,
            FrameModuleSettings settings)
        {
            Mode = mode;
            SelectedWallCount = selectedWallCount;
            CalculationMullionCount = calculationMullionCount;
            CurtainWallTypes = new ObservableCollection<ElementTypeOption>(curtainWallTypes ?? Enumerable.Empty<ElementTypeOption>());
            PurchaseLengths = new ObservableCollection<double>(new[] { 3000.0, 4000.0 });

            _selectedCurtainWallType = FindByName(CurtainWallTypes, settings.CurtainWallTypeName) ?? CurtainWallTypes.FirstOrDefault();
            _studSpacingText = Format(settings.StudSpacingMm);
            _processDoors = settings.ProcessDoors;
            _selectedCwPurchaseLength = EnsurePurchaseLength(settings.CwPurchaseLengthMm);
            _selectedUwPurchaseLength = EnsurePurchaseLength(settings.UwPurchaseLengthMm);
            _cutLossText = Format(settings.CutLossMm);
            _minimumReusableOffcutText = Format(settings.MinimumReusableOffcutMm);
            _performCutting = settings.PerformCutting;
            _exportCsv = settings.ExportCsv;
            _exportDirectory = settings.ExportDirectory;

            AcceptCommand = new RelayCommand(delegate { Accept(); });
            CancelCommand = new RelayCommand(delegate { if (RequestClose != null) RequestClose(false); });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public Action<bool> RequestClose { get; set; }

        public FrameCommandMode Mode { get; private set; }
        public int SelectedWallCount { get; private set; }
        public int CalculationMullionCount { get; private set; }
        public ObservableCollection<ElementTypeOption> CurtainWallTypes { get; private set; }
        public ObservableCollection<double> PurchaseLengths { get; private set; }
        public ICommand AcceptCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public bool IsGenerateMode
        {
            get { return Mode == FrameCommandMode.Generate; }
        }

        public string WindowTitle
        {
            get { return IsGenerateMode ? "SAB — создание расчётного каркаса" : "SAB — расчёт каркаса"; }
        }

        public string Heading
        {
            get { return IsGenerateMode ? "Создание расчётного каркаса" : "Пересчёт ведомости каркаса"; }
        }

        public string ActionText
        {
            get { return IsGenerateMode ? "Создать каркас" : "Рассчитать"; }
        }

        public ElementTypeOption SelectedCurtainWallType
        {
            get { return _selectedCurtainWallType; }
            set { SetField(ref _selectedCurtainWallType, value); }
        }

        public string StudSpacingText
        {
            get { return _studSpacingText; }
            set { SetField(ref _studSpacingText, value); }
        }

        public bool ProcessDoors
        {
            get { return _processDoors; }
            set { SetField(ref _processDoors, value); }
        }

        public double SelectedCwPurchaseLength
        {
            get { return _selectedCwPurchaseLength; }
            set { SetField(ref _selectedCwPurchaseLength, value); }
        }

        public double SelectedUwPurchaseLength
        {
            get { return _selectedUwPurchaseLength; }
            set { SetField(ref _selectedUwPurchaseLength, value); }
        }

        public string CutLossText
        {
            get { return _cutLossText; }
            set { SetField(ref _cutLossText, value); }
        }

        public string MinimumReusableOffcutText
        {
            get { return _minimumReusableOffcutText; }
            set { SetField(ref _minimumReusableOffcutText, value); }
        }

        public bool PerformCutting
        {
            get { return _performCutting; }
            set { SetField(ref _performCutting, value); }
        }

        public bool ExportCsv
        {
            get { return _exportCsv; }
            set { SetField(ref _exportCsv, value); }
        }

        public string ExportDirectory
        {
            get { return _exportDirectory; }
            set { SetField(ref _exportDirectory, value); }
        }

        public string ValidationMessage
        {
            get { return _validationMessage; }
            private set { SetField(ref _validationMessage, value); }
        }

        public bool TryBuildOptions(
            out FrameGenerationOptions generationOptions,
            out FrameCalculationOptions calculationOptions,
            out FrameModuleSettings settings)
        {
            generationOptions = null;
            calculationOptions = null;
            settings = null;

            double spacing;
            double cutLoss;
            double reusable;
            if (!TryParseNonNegative(StudSpacingText, out spacing) || spacing <= 0.0)
            {
                ValidationMessage = "Шаг стоек должен быть числом больше нуля.";
                return false;
            }

            if (!TryParseNonNegative(CutLossText, out cutLoss))
            {
                ValidationMessage = "Потери на рез должны быть неотрицательным числом.";
                return false;
            }

            if (!TryParseNonNegative(MinimumReusableOffcutText, out reusable))
            {
                ValidationMessage = "Минимальный полезный остаток должен быть неотрицательным числом.";
                return false;
            }

            if (IsGenerateMode && SelectedCurtainWallType == null)
            {
                ValidationMessage = "Выберите тип расчётного витража.";
                return false;
            }

            if (ExportCsv && string.IsNullOrWhiteSpace(ExportDirectory))
            {
                ValidationMessage = "Укажите каталог экспорта CSV.";
                return false;
            }

            generationOptions = new FrameGenerationOptions
            {
                StudSpacingMm = spacing,
                ProcessDoors = ProcessDoors,
                CalculationCurtainWallTypeId = SelectedCurtainWallType != null ? SelectedCurtainWallType.Id : null,
                CalculationCurtainWallTypeName = SelectedCurtainWallType != null ? SelectedCurtainWallType.DisplayName : string.Empty
            };
            calculationOptions = new FrameCalculationOptions
            {
                CwPurchaseLengthMm = SelectedCwPurchaseLength,
                UwPurchaseLengthMm = SelectedUwPurchaseLength,
                CutLossMm = cutLoss,
                MinimumReusableOffcutMm = reusable,
                PerformCutting = PerformCutting
            };
            settings = new FrameModuleSettings
            {
                StudSpacingMm = spacing,
                CurtainWallTypeName = SelectedCurtainWallType != null ? SelectedCurtainWallType.DisplayName : string.Empty,
                ProcessDoors = ProcessDoors,
                CwPurchaseLengthMm = SelectedCwPurchaseLength,
                UwPurchaseLengthMm = SelectedUwPurchaseLength,
                CutLossMm = cutLoss,
                MinimumReusableOffcutMm = reusable,
                PerformCutting = PerformCutting,
                ExportCsv = ExportCsv,
                ExportDirectory = ExportDirectory
            };
            ValidationMessage = string.Empty;
            return true;
        }

        private void Accept()
        {
            FrameGenerationOptions generationOptions;
            FrameCalculationOptions calculationOptions;
            FrameModuleSettings settings;
            if (TryBuildOptions(out generationOptions, out calculationOptions, out settings) && RequestClose != null)
            {
                RequestClose(true);
            }
        }

        private static ElementTypeOption FindByName(IEnumerable<ElementTypeOption> options, string name)
        {
            return options.FirstOrDefault(item => string.Equals(item.DisplayName, name, StringComparison.OrdinalIgnoreCase));
        }

        private double EnsurePurchaseLength(double value)
        {
            if (!PurchaseLengths.Contains(value))
            {
                PurchaseLengths.Add(value);
            }

            return value > 0.0 ? value : FrameModuleConstants.DefaultPurchaseLengthMm;
        }

        private static bool TryParseNonNegative(string text, out double value)
        {
            return (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
                   value >= 0.0;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
