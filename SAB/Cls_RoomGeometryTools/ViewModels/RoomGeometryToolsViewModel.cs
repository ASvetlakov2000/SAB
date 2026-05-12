using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Services;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace SAB.RoomGeometryTools.ViewModels
{
    /// <summary>
    /// ViewModel окна проверки геометрии помещений.
    /// </summary>
    public class RoomGeometryToolsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDocument;
        private readonly Document _document;

        private readonly RoomCollectorService _roomCollectorService;
        private readonly RoomBoundaryService _roomBoundaryService;
        private readonly RoomAngleCheckService _roomAngleCheckService;
        private readonly RoomPlacementCheckService _roomPlacementCheckService;
        private readonly RoomAreaChangeCheckService _roomAreaChangeCheckService;
        private readonly RoomCentroidService _roomCentroidService;
        private readonly RoomAxisDirectionService _roomAxisDirectionService;
        private readonly RoomAxisClippingService _roomAxisClippingService;
        private readonly RoomAxisCreationService _roomAxisCreationService;
        private readonly RoomDiagnosticGraphicsService _roomDiagnosticGraphicsService;
        private readonly RevitStyleCollectorService _revitStyleCollectorService;
        private readonly RoomReportService _roomReportService;

        private readonly RoomGeometryStartupAction _startupAction;
        private readonly ExternalEvent _externalEvent;
        private readonly RoomGeometryExternalEventHandler _externalEventHandler;

        private string _statusText;
        private string _areaDeviationThresholdPercentText;
        private RevitStyleItem _selectedAxisLineStyle;
        private RevitStyleItem _selectedAngularDimensionType;
        private bool _deletePreviousAxesBeforeCreation;
        private bool _skipRoomsWithGeometryErrors;
        private bool _startupActionExecuted;

        public RoomGeometryToolsViewModel(
            UIDocument uiDocument,
            RoomGeometryStartupAction startupAction,
            ExternalEvent externalEvent,
            RoomGeometryExternalEventHandler externalEventHandler)
        {
            _uiDocument = uiDocument;
            _document = uiDocument != null ? uiDocument.Document : null;
            _startupAction = startupAction;
            _externalEvent = externalEvent;
            _externalEventHandler = externalEventHandler;

            _roomCollectorService = new RoomCollectorService();
            _roomBoundaryService = new RoomBoundaryService();
            _roomAngleCheckService = new RoomAngleCheckService(_roomBoundaryService);
            _roomPlacementCheckService = new RoomPlacementCheckService();
            _roomAreaChangeCheckService = new RoomAreaChangeCheckService();
            _roomCentroidService = new RoomCentroidService();
            _roomAxisDirectionService = new RoomAxisDirectionService();
            _roomAxisClippingService = new RoomAxisClippingService();
            _revitStyleCollectorService = new RevitStyleCollectorService();
            _roomAxisCreationService = new RoomAxisCreationService(
                _roomCollectorService,
                _roomBoundaryService,
                _roomPlacementCheckService,
                _roomAngleCheckService,
                _roomCentroidService,
                _roomAxisDirectionService,
                _roomAxisClippingService,
                _revitStyleCollectorService);
            _roomDiagnosticGraphicsService = new RoomDiagnosticGraphicsService(_roomBoundaryService);
            _roomReportService = new RoomReportService();

            AreaDeviationThresholdPercentText = "10.00";
            DeletePreviousAxesBeforeCreation = true;
            SkipRoomsWithGeometryErrors = true;
            StatusText = "Готово к проверке.";

            AxisLineStyles = new ObservableCollection<RevitStyleItem>();
            AngularDimensionTypes = new ObservableCollection<RevitStyleItem>();
            AngleIssues = new ObservableCollection<RoomAngleIssue>();
            PlacementIssues = new ObservableCollection<RoomPlacementIssue>();
            AreaIssues = new ObservableCollection<RoomAreaChangeIssue>();
            AxisResults = new ObservableCollection<RoomAxisCreationResult>();

            CheckAnglesCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.CheckAngles));
            CheckUnplacedRoomsCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.CheckUnplacedRooms));
            CheckAreaChangesCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.CheckAreaChanges));
            ShowProblematicAnglesCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.ShowProblematicAngles));
            CreateAxesForSelectedRoomCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.CreateAxesForSelectedRoom));
            CreateAxesForActiveViewRoomsCommand = new RelayCommand(_ => QueueOperation(RoomGeometryUiOperation.CreateAxesForActiveViewRooms));
            ExportCsvCommand = new RelayCommand(_ => ExecuteExportCsv());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));

            LoadStyles();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler RequestClose;

        public ObservableCollection<RevitStyleItem> AxisLineStyles { get; private set; }

        public ObservableCollection<RevitStyleItem> AngularDimensionTypes { get; private set; }

        public ObservableCollection<RoomAngleIssue> AngleIssues { get; private set; }

        public ObservableCollection<RoomPlacementIssue> PlacementIssues { get; private set; }

        public ObservableCollection<RoomAreaChangeIssue> AreaIssues { get; private set; }

        public ObservableCollection<RoomAxisCreationResult> AxisResults { get; private set; }

        public ICommand CheckAnglesCommand { get; private set; }

        public ICommand CheckUnplacedRoomsCommand { get; private set; }

        public ICommand CheckAreaChangesCommand { get; private set; }

        public ICommand ShowProblematicAnglesCommand { get; private set; }

        public ICommand CreateAxesForSelectedRoomCommand { get; private set; }

        public ICommand CreateAxesForActiveViewRoomsCommand { get; private set; }

        public ICommand ExportCsvCommand { get; private set; }

        public ICommand CloseCommand { get; private set; }

        public string AreaDeviationThresholdPercentText
        {
            get => _areaDeviationThresholdPercentText;
            set
            {
                _areaDeviationThresholdPercentText = value;
                OnPropertyChanged(nameof(AreaDeviationThresholdPercentText));
            }
        }

        public RevitStyleItem SelectedAxisLineStyle
        {
            get => _selectedAxisLineStyle;
            set
            {
                _selectedAxisLineStyle = value;
                OnPropertyChanged(nameof(SelectedAxisLineStyle));
            }
        }

        public RevitStyleItem SelectedAngularDimensionType
        {
            get => _selectedAngularDimensionType;
            set
            {
                _selectedAngularDimensionType = value;
                OnPropertyChanged(nameof(SelectedAngularDimensionType));
            }
        }

        public bool DeletePreviousAxesBeforeCreation
        {
            get => _deletePreviousAxesBeforeCreation;
            set
            {
                _deletePreviousAxesBeforeCreation = value;
                OnPropertyChanged(nameof(DeletePreviousAxesBeforeCreation));
            }
        }

        public bool SkipRoomsWithGeometryErrors
        {
            get => _skipRoomsWithGeometryErrors;
            set
            {
                _skipRoomsWithGeometryErrors = value;
                OnPropertyChanged(nameof(SkipRoomsWithGeometryErrors));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        /// <summary>
        /// Выполняет стартовую операцию, если команда была запущена не из общей кнопки.
        /// </summary>
        public void RunStartupActionIfNeeded()
        {
            if (_startupActionExecuted)
            {
                return;
            }

            _startupActionExecuted = true;

            RequestStartupAction(_startupAction);
        }

        /// <summary>
        /// Позволяет внешнему оркестратору инициировать действие,
        /// например если окно уже открыто и запущена отдельная команда.
        /// </summary>
        public void RequestStartupAction(RoomGeometryStartupAction startupAction)
        {
            RoomGeometryUiOperation operation = MapStartupActionToUiOperation(startupAction);
            if (operation != RoomGeometryUiOperation.None)
            {
                QueueOperation(operation);
            }
        }

        /// <summary>
        /// Точка входа для ExternalEventHandler. Выполняется внутри API-контекста Revit.
        /// </summary>
        public void ExecuteOperationFromExternalEvent(RoomGeometryUiOperation operation)
        {
            switch (operation)
            {
                case RoomGeometryUiOperation.CheckAngles:
                    ExecuteCheckAngles();
                    break;
                case RoomGeometryUiOperation.CheckUnplacedRooms:
                    ExecuteCheckUnplacedRooms();
                    break;
                case RoomGeometryUiOperation.CheckAreaChanges:
                    ExecuteCheckAreaChanges();
                    break;
                case RoomGeometryUiOperation.ShowProblematicAngles:
                    ExecuteShowProblematicAngles();
                    break;
                case RoomGeometryUiOperation.CreateAxesForSelectedRoom:
                    ExecuteCreateAxesForSelectedRoom();
                    break;
                case RoomGeometryUiOperation.CreateAxesForActiveViewRooms:
                    ExecuteCreateAxesForActiveViewRooms();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Установка статуса из ExternalEventHandler при верхнеуровневых исключениях.
        /// </summary>
        public void SetStatusFromExternalEvent(string text)
        {
            StatusText = text ?? string.Empty;
        }

        private void LoadStyles()
        {
            AxisLineStyles.Clear();
            AngularDimensionTypes.Clear();

            if (_document == null)
            {
                StatusText = "Не удалось прочитать активный документ Revit.";
                return;
            }

            IList<RevitStyleItem> lineStyles = _revitStyleCollectorService.GetDetailLineStyles(_document);
            for (int i = 0; i < lineStyles.Count; i++)
            {
                AxisLineStyles.Add(lineStyles[i]);
            }

            IList<RevitStyleItem> angularStyles = _revitStyleCollectorService.GetAngularDimensionStyles(_document);
            for (int i = 0; i < angularStyles.Count; i++)
            {
                AngularDimensionTypes.Add(angularStyles[i]);
            }

            SelectedAxisLineStyle = _revitStyleCollectorService.ResolveDefaultAxisStyle(lineStyles);
            SelectedAngularDimensionType = _revitStyleCollectorService.ResolveDefaultAngularStyle(angularStyles);

            if (SelectedAngularDimensionType == null)
            {
                StatusText = "Не найден угловой размерный стиль. Для диагностики углов будет использован TextNote fallback.";
            }
        }

        private void ExecuteCheckAngles()
        {
            try
            {
                if (_document == null)
                {
                    StatusText = "Документ недоступен.";
                    return;
                }

                IList<Room> rooms = _roomCollectorService.GetAllRooms(_document);
                IList<RoomAngleIssue> issues = _roomAngleCheckService.CheckRooms(rooms);

                ReplaceCollection(AngleIssues, issues);
                StatusText = "Проверка углов завершена. Всего помещений: " + rooms.Count + ". Найдено проблем: " + issues.Count + ".";
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка проверки углов: " + exception.Message;
            }
        }

        private void ExecuteCheckUnplacedRooms()
        {
            try
            {
                if (_document == null)
                {
                    StatusText = "Документ недоступен.";
                    return;
                }

                IList<Room> rooms = _roomCollectorService.GetAllRooms(_document);
                IList<RoomPlacementIssue> issues = _roomPlacementCheckService.CheckRooms(rooms);

                ReplaceCollection(PlacementIssues, issues);
                StatusText = "Проверка неразмещенных помещений завершена. Всего помещений: " + rooms.Count + ". Проблем: " + issues.Count + ".";
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка проверки размещения: " + exception.Message;
            }
        }

        private void ExecuteCheckAreaChanges()
        {
            try
            {
                if (_document == null)
                {
                    StatusText = "Документ недоступен.";
                    return;
                }

                if (!TryParseThresholdPercent(out double thresholdPercent))
                {
                    StatusText = "Введите корректное значение допустимого изменения площади (%).";
                    return;
                }

                IList<Room> rooms = _roomCollectorService.GetAllRooms(_document);
                string warningMessage;
                IList<RoomAreaChangeIssue> issues = _roomAreaChangeCheckService.CheckRooms(rooms, thresholdPercent, out warningMessage);
                ReplaceCollection(AreaIssues, issues);

                if (!string.IsNullOrWhiteSpace(warningMessage))
                {
                    StatusText = warningMessage;
                    ToastNotifier.ShowWarning("Проверка геометрии помещений", warningMessage, 12);
                    return;
                }

                StatusText = "Проверка изменения площади завершена. Всего помещений: " + rooms.Count + ". Проблем: " + issues.Count + ".";
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка проверки площади: " + exception.Message;
            }
        }

        private void ExecuteShowProblematicAngles()
        {
            try
            {
                if (_document == null)
                {
                    StatusText = "Документ недоступен.";
                    return;
                }

                if (AngleIssues.Count == 0)
                {
                    StatusText = "Нет проблемных углов для отображения.";
                    return;
                }

                ElementId angularTypeId = SelectedAngularDimensionType != null
                    ? SelectedAngularDimensionType.ElementId
                    : ElementId.InvalidElementId;

                List<RoomAngleIssue> issues = new List<RoomAngleIssue>(AngleIssues);
                IList<string> warnings;

                using (Transaction transaction = new Transaction(_document, "SAB Диагностика углов помещений"))
                {
                    transaction.Start();
                    warnings = _roomDiagnosticGraphicsService.CreateDiagnostics(_document, issues, angularTypeId);
                    transaction.Commit();
                }

                if (warnings.Count > 0)
                {
                    StatusText = "Диагностика углов завершена с предупреждениями: " + warnings.Count + ".";
                }
                else
                {
                    StatusText = "Диагностика углов выполнена успешно.";
                }
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка построения диагностики: " + exception.Message;
            }
        }

        private void ExecuteCreateAxesForSelectedRoom()
        {
            try
            {
                RoomGeometryToolsSettings settings = BuildSettings();
                IList<RoomAxisCreationResult> results = _roomAxisCreationService.CreateForSelectedRoom(_uiDocument, settings);
                ReplaceCollection(AxisResults, results);

                int successCount = CountSuccess(results);
                int skippedCount = results.Count - successCount;
                StatusText = "Построение осей по выбранному помещению завершено. Успешно: " + successCount + ", пропущено: " + skippedCount + ".";
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка построения осей по выбранному помещению: " + exception.Message;
            }
        }

        private void ExecuteCreateAxesForActiveViewRooms()
        {
            try
            {
                RoomGeometryToolsSettings settings = BuildSettings();
                int totalRoomsFound;
                IList<RoomAxisCreationResult> results = _roomAxisCreationService.CreateForActiveViewRooms(_uiDocument, settings, out totalRoomsFound);
                ReplaceCollection(AxisResults, results);

                int successCount = CountSuccess(results);
                int skippedCount = results.Count - successCount;
                StatusText = "Построение осей по активному виду завершено. Найдено помещений: " + totalRoomsFound +
                             ", обработано: " + results.Count + ", успешно: " + successCount + ", пропущено: " + skippedCount + ".";
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка построения осей по активному виду: " + exception.Message;
            }
        }

        private void ExecuteExportCsv()
        {
            try
            {
                if (AngleIssues.Count == 0 &&
                    PlacementIssues.Count == 0 &&
                    AreaIssues.Count == 0 &&
                    AxisResults.Count == 0)
                {
                    StatusText = "Нет данных для экспорта в CSV.";
                    return;
                }

                string reportPath = _roomReportService.ExportCsv(
                    new List<RoomAngleIssue>(AngleIssues),
                    new List<RoomPlacementIssue>(PlacementIssues),
                    new List<RoomAreaChangeIssue>(AreaIssues),
                    new List<RoomAxisCreationResult>(AxisResults));

                if (string.IsNullOrWhiteSpace(reportPath))
                {
                    StatusText = "Экспорт CSV отменен пользователем.";
                    return;
                }

                StatusText = "CSV отчет сохранен: " + reportPath;
                ToastNotifier.ShowFolderLinkSuccess(
                    "Проверка геометрии помещений",
                    "CSV отчет успешно сохранен.",
                    System.IO.Path.GetDirectoryName(reportPath),
                    12);
            }
            catch (Exception exception)
            {
                StatusText = "Ошибка экспорта CSV: " + exception.Message;
            }
        }

        /// <summary>
        /// Блок маршрутизации команд UI в ExternalEvent для modeless-режима.
        /// </summary>
        private void QueueOperation(RoomGeometryUiOperation operation)
        {
            if (operation == RoomGeometryUiOperation.None)
            {
                return;
            }

            // Если ExternalEvent не передан, выполняем напрямую (fallback для безопасной совместимости).
            if (_externalEvent == null || _externalEventHandler == null)
            {
                ExecuteOperationFromExternalEvent(operation);
                return;
            }

            try
            {
                _externalEventHandler.Enqueue(operation);
                ExternalEventRequest request = _externalEvent.Raise();
                if (request != ExternalEventRequest.Accepted)
                {
                    StatusText = "Revit отклонил запрос на выполнение операции. Повторите попытку.";
                }
            }
            catch (Exception exception)
            {
                StatusText = "Не удалось запустить операцию: " + exception.Message;
            }
        }

        private static RoomGeometryUiOperation MapStartupActionToUiOperation(RoomGeometryStartupAction startupAction)
        {
            switch (startupAction)
            {
                case RoomGeometryStartupAction.CheckAngles:
                    return RoomGeometryUiOperation.CheckAngles;
                case RoomGeometryStartupAction.CheckUnplaced:
                    return RoomGeometryUiOperation.CheckUnplacedRooms;
                case RoomGeometryStartupAction.CheckAreaChanges:
                    return RoomGeometryUiOperation.CheckAreaChanges;
                case RoomGeometryStartupAction.CreateAxesForSelectedRoom:
                    return RoomGeometryUiOperation.CreateAxesForSelectedRoom;
                case RoomGeometryStartupAction.CreateAxesForActiveViewRooms:
                    return RoomGeometryUiOperation.CreateAxesForActiveViewRooms;
                default:
                    return RoomGeometryUiOperation.None;
            }
        }

        private RoomGeometryToolsSettings BuildSettings()
        {
            RoomGeometryToolsSettings settings = new RoomGeometryToolsSettings();

            if (!TryParseThresholdPercent(out double thresholdPercent))
            {
                thresholdPercent = 10.0;
            }

            settings.AreaDeviationThresholdPercent = thresholdPercent;
            settings.DeletePreviousAxesBeforeCreation = DeletePreviousAxesBeforeCreation;
            settings.SkipRoomsWithGeometryErrors = SkipRoomsWithGeometryErrors;

            if (SelectedAxisLineStyle != null)
            {
                settings.SelectedAxisLineStyleId = SelectedAxisLineStyle.ElementId;
                settings.SelectedAxisLineStyleName = SelectedAxisLineStyle.Name;
            }

            if (SelectedAngularDimensionType != null)
            {
                settings.SelectedAngularDimensionTypeId = SelectedAngularDimensionType.ElementId;
                settings.SelectedAngularDimensionTypeName = SelectedAngularDimensionType.Name;
            }

            return settings;
        }

        private bool TryParseThresholdPercent(out double thresholdPercent)
        {
            thresholdPercent = 0.0;

            string raw = (AreaDeviationThresholdPercentText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            raw = raw.Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out thresholdPercent))
            {
                return false;
            }

            return thresholdPercent >= 0.0;
        }

        private static int CountSuccess(IList<RoomAxisCreationResult> results)
        {
            int count = 0;
            if (results == null)
            {
                return count;
            }

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] != null && results[i].IsSuccess)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IList<T> source)
        {
            target.Clear();

            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
