using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Geometry;
using SAB.InteriorElevations.Services.Rooms;
using SAB.InteriorElevations.Services.Selection;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Services.Sheets;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationCropByExampleService
    {
        public const string SampleViewBaseName = "SAB-Развертки по линии_Граница вида";
        public const string TemporarySheetName = "SAB-Развертки по линии";

        private readonly DetailLineSelectionService _detailLineSelectionService;
        private readonly ElevationGeometryService _geometryService;
        private readonly LineOrientationService _lineOrientationService;
        private readonly ElevationMarkerService _markerService;
        private readonly ElevationCropService _cropService;
        private readonly SheetCreationService _sheetCreationService;
        private readonly ElevationSettingsStorageService _settingsStorageService;

        public ElevationCropByExampleService()
        {
            _detailLineSelectionService = new DetailLineSelectionService();
            _geometryService = new ElevationGeometryService();
            _lineOrientationService = new LineOrientationService();
            _markerService = new ElevationMarkerService();
            _cropService = new ElevationCropService();
            _sheetCreationService = new SheetCreationService();
            _settingsStorageService = new ElevationSettingsStorageService();
        }

        public CropByExampleSession CreateSession(
            ViewPlan sourcePlanView,
            ElementId sourcePlanLevelId,
            ElevationSettings settings)
        {
            CropByExampleSession session = new CropByExampleSession();
            session.SourcePlanViewId = sourcePlanView != null ? sourcePlanView.Id : ElementId.InvalidElementId;
            session.SourcePlanLevelId = sourcePlanLevelId != null ? sourcePlanLevelId : ElementId.InvalidElementId;
            session.SourceLineId = ElementId.InvalidElementId;
            session.SampleViewId = ElementId.InvalidElementId;
            session.Settings = settings;
            return session;
        }

        public bool TryPostDetailLineCommand(UIApplication uiApplication, IList<string> warnings)
        {
            if (uiApplication == null)
            {
                AddWarning(warnings, "Не удалось получить приложение Revit для запуска команды Линия детализации.");
                return false;
            }

            try
            {
                RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.DetailLine);
                if (commandId == null)
                {
                    AddWarning(warnings, "Команда Revit 'Линия детализации' недоступна.");
                    return false;
                }

                if (!uiApplication.CanPostCommand(commandId))
                {
                    AddWarning(warnings, "Revit сейчас не может запустить команду 'Линия детализации'.");
                    return false;
                }

                uiApplication.PostCommand(commandId);
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось запустить команду 'Линия детализации': " + exception.Message);
                return false;
            }
        }

        public bool TryPickSingleDetailLine(
            UIDocument uiDocument,
            CropByExampleSession session,
            IList<string> warnings,
            out DetailLine selectedLine)
        {
            selectedLine = null;

            if (uiDocument == null || session == null)
            {
                AddWarning(warnings, "Не удалось начать выбор линии для вида-примера.");
                return false;
            }

            Document document = uiDocument.Document;
            ViewPlan sourcePlanView = document.GetElement(session.SourcePlanViewId) as ViewPlan;
            if (sourcePlanView == null)
            {
                AddWarning(warnings, "Исходный план для выбора линии не найден.");
                return false;
            }

            if (!TryActivateView(uiDocument, sourcePlanView, warnings))
            {
                return false;
            }

            DetailLineSelectionResult selectionResult = _detailLineSelectionService.PickSingleDetailLine(
                uiDocument,
                sourcePlanView,
                "Выберите одну линию детализации для развертки-примера.");

            AppendWarnings(warnings, selectionResult.Warnings);
            if (selectionResult.IsCancelled || selectionResult.Lines.Count == 0)
            {
                return false;
            }

            selectedLine = selectionResult.Lines[0];
            session.SourceLineId = selectedLine.Id;
            return true;
        }

        public bool TryPickRoomDataForSampleView(
            UIDocument uiDocument,
            CropByExampleSession session,
            IList<string> warnings,
            out RoomData roomData)
        {
            roomData = null;

            if (uiDocument == null || session == null)
            {
                AddWarning(warnings, "Не удалось начать выбор помещения для вида-примера.");
                return false;
            }

            RoomDetectionService roomDetectionService = new RoomDetectionService();
            string roomSelectionError;
            bool roomPicked = roomDetectionService.TryPickRoomData(uiDocument, out roomData, out roomSelectionError);
            if (!roomPicked)
            {
                if (!string.IsNullOrWhiteSpace(roomSelectionError))
                {
                    AddWarning(warnings, roomSelectionError);
                }

                return false;
            }

            if (!RevitElementIdUtils.AreEqual(roomData.LevelId, session.SourcePlanLevelId))
            {
                AddWarning(warnings, "Выбранное помещение находится на другом уровне. Выберите помещение на уровне исходного плана.");
                roomData = null;
                return false;
            }

            return true;
        }

        public bool TryCreateSampleView(
            UIDocument uiDocument,
            CropByExampleSession session,
            RoomData roomData,
            IList<string> warnings,
            out ViewSection sampleView)
        {
            sampleView = null;

            if (uiDocument == null || session == null || session.Settings == null || roomData == null)
            {
                AddWarning(warnings, "Недостаточно данных для создания вида-примера.");
                return false;
            }

            Document document = uiDocument.Document;
            ViewPlan sourcePlanView = document.GetElement(session.SourcePlanViewId) as ViewPlan;
            DetailLine sourceLine = document.GetElement(session.SourceLineId) as DetailLine;

            if (sourcePlanView == null)
            {
                AddWarning(warnings, "Исходный план для вида-примера не найден.");
                return false;
            }

            if (sourceLine == null)
            {
                AddWarning(warnings, "Линия-основа для вида-примера не найдена.");
                return false;
            }

            List<DetailLine> lines = new List<DetailLine>();
            lines.Add(sourceLine);

            List<ElevationLineData> lineDataList = _geometryService.BuildElevationLineData(lines, warnings);
            if (lineDataList.Count == 0)
            {
                AddWarning(warnings, "Не удалось получить геометрию линии-основы для вида-примера.");
                return false;
            }

            if (!_lineOrientationService.TryAssignInsideNormals(
                    document,
                    lineDataList,
                    roomData,
                    session.Settings.MarkerOffsetMm,
                    warnings))
            {
                AddWarning(warnings, "Не удалось определить направление вида-примера по выбранному помещению.");
                return false;
            }

            ElevationLineData lineData = lineDataList[0];
            lineData.Index = 1;
            lineData.EndIndex = 2;

            Transaction createTransaction = new Transaction(document, "SAB - создать вид-пример развертки");
            try
            {
                createTransaction.Start();

                ElementId markerElementId;
                sampleView = _markerService.CreateElevationForLine(
                    document,
                    sourcePlanView,
                    lineData,
                    session.Settings.ElevationViewFamilyTypeId,
                    session.Settings.ViewScale,
                    out markerElementId,
                    warnings);

                if (sampleView == null)
                {
                    createTransaction.RollBack();
                    AddWarning(warnings, "Revit не создал развертку-пример по выбранной линии.");
                    return false;
                }

                sampleView.Name = GenerateUniqueViewName(document, SampleViewBaseName);
                TryApplyTemplateAndScale(document, sampleView, session.Settings, warnings);
                _cropService.TryApplyCrop(sampleView, lineData, session.Settings, warnings);

                try
                {
                    sampleView.CropBoxVisible = true;
                }
                catch
                {
                    // Шаблон вида может контролировать показ рамки. Вид при этом остается созданным.
                }

                session.SampleViewId = sampleView.Id;
                createTransaction.Commit();
            }
            catch (Exception exception)
            {
                if (createTransaction.GetStatus() == TransactionStatus.Started)
                {
                    createTransaction.RollBack();
                }

                AddWarning(warnings, "Ошибка создания вида-примера: " + exception.Message);
                sampleView = null;
                return false;
            }

            // Размещение на временном листе не должно отменять уже созданный вид.
            TryPlaceSampleViewOnTemporarySheet(document, sampleView, session.Settings, warnings);
            TryActivateView(uiDocument, sampleView, warnings);
            return true;
        }

        public bool TryAcceptCropFromSampleView(
            UIDocument uiDocument,
            CropByExampleSession session,
            IList<string> warnings,
            out double topOffsetMm,
            out double bottomOffsetMm)
        {
            topOffsetMm = 0.0;
            bottomOffsetMm = 0.0;

            if (uiDocument == null || session == null || session.Settings == null)
            {
                AddWarning(warnings, "Недостаточно данных для чтения границ вида-примера.");
                return false;
            }

            Document document = uiDocument.Document;
            ViewSection sampleView = document.GetElement(session.SampleViewId) as ViewSection;
            DetailLine sourceLine = document.GetElement(session.SourceLineId) as DetailLine;

            if (sampleView == null)
            {
                AddWarning(warnings, "Вид-пример не найден.");
                return false;
            }

            if (sourceLine == null)
            {
                AddWarning(warnings, "Линия-основа вида-примера не найдена.");
                return false;
            }

            ElevationLineData lineData;
            if (!TryBuildSingleLineData(sourceLine, warnings, out lineData))
            {
                return false;
            }

            BoundingBoxXYZ cropBox = sampleView.CropBox;
            if (cropBox == null || cropBox.Transform == null)
            {
                AddWarning(warnings, "У вида-примера недоступна рамка обрезки.");
                return false;
            }

            try
            {
                Transform inverse = cropBox.Transform.Inverse;
                XYZ startLocal = inverse.OfPoint(lineData.StartPoint);
                XYZ endLocal = inverse.OfPoint(lineData.EndPoint);

                double lineCenterY = (startLocal.Y + endLocal.Y) / 2.0;
                double minY = Math.Min(cropBox.Min.Y, cropBox.Max.Y);
                double maxY = Math.Max(cropBox.Min.Y, cropBox.Max.Y);

                topOffsetMm = Math.Max(0.0, UnitConversionUtils.FeetToMillimeters(maxY - lineCenterY));
                bottomOffsetMm = Math.Max(0.0, UnitConversionUtils.FeetToMillimeters(lineCenterY - minY));

                session.Settings.TopOffsetMm = topOffsetMm;
                session.Settings.BottomOffsetMm = bottomOffsetMm;
                _settingsStorageService.SaveSettings(session.Settings);
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось прочитать верхнюю и нижнюю границу вида-примера: " + exception.Message);
                return false;
            }
        }

        private bool TryBuildSingleLineData(DetailLine sourceLine, IList<string> warnings, out ElevationLineData lineData)
        {
            lineData = null;

            List<DetailLine> lines = new List<DetailLine>();
            lines.Add(sourceLine);

            List<ElevationLineData> lineDataList = _geometryService.BuildElevationLineData(lines, warnings);
            if (lineDataList.Count == 0)
            {
                AddWarning(warnings, "Не удалось прочитать геометрию линии-основы вида-примера.");
                return false;
            }

            lineData = lineDataList[0];
            return true;
        }

        private void TryApplyTemplateAndScale(
            Document document,
            ViewSection sampleView,
            ElevationSettings settings,
            IList<string> warnings)
        {
            if (document == null || sampleView == null || settings == null)
            {
                return;
            }

            if (settings.ViewTemplateId != null && settings.ViewTemplateId != ElementId.InvalidElementId)
            {
                try
                {
                    sampleView.ViewTemplateId = settings.ViewTemplateId;
                }
                catch (Exception templateException)
                {
                    AddWarning(warnings, "Не удалось применить шаблон к виду-примеру: " + templateException.Message);
                }
            }

            if (settings.ViewScale <= 0)
            {
                return;
            }

            if (IsScaleControlledByTemplate(document, sampleView))
            {
                AddWarning(warnings, "Масштаб вида-примера контролируется шаблоном вида.");
                return;
            }

            try
            {
                sampleView.Scale = settings.ViewScale;
            }
            catch (Exception scaleException)
            {
                AddWarning(warnings, "Не удалось установить масштаб вида-примера: " + scaleException.Message);
            }
        }

        private bool IsScaleControlledByTemplate(Document document, View view)
        {
            if (document == null || view == null)
            {
                return false;
            }

            ElementId templateId = view.ViewTemplateId;
            if (templateId == null || templateId == ElementId.InvalidElementId)
            {
                return false;
            }

            View templateView = document.GetElement(templateId) as View;
            if (templateView == null || !templateView.IsTemplate)
            {
                return false;
            }

            ICollection<ElementId> nonControlled = templateView.GetNonControlledTemplateParameterIds();
            if (nonControlled == null)
            {
                return true;
            }

            ElementId scaleParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE);
            ElementId scaleMetricParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
            ElementId scaleImperialParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_IMPERIAL);

            bool scaleIsNonControlled =
                nonControlled.Contains(scaleParameterId) ||
                nonControlled.Contains(scaleMetricParameterId) ||
                nonControlled.Contains(scaleImperialParameterId);

            return !scaleIsNonControlled;
        }

        private void TryPlaceSampleViewOnTemporarySheet(
            Document document,
            ViewSection sampleView,
            ElevationSettings settings,
            IList<string> warnings)
        {
            if (document == null || sampleView == null || settings == null)
            {
                return;
            }

            if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                AddWarning(warnings, "Вид-пример создан, но временный лист не создан: не выбран тип основной надписи.");
                return;
            }

            Transaction sheetTransaction = new Transaction(document, "SAB - разместить вид-пример на временном листе");
            try
            {
                sheetTransaction.Start();

                ViewSheet temporarySheet = _sheetCreationService.CreateCoordinateSelectionSheet(
                    document,
                    settings,
                    TemporarySheetName);

                if (temporarySheet == null)
                {
                    sheetTransaction.RollBack();
                    AddWarning(warnings, "Вид-пример создан, но временный лист создать не удалось.");
                    return;
                }

                if (!Viewport.CanAddViewToSheet(document, temporarySheet.Id, sampleView.Id))
                {
                    sheetTransaction.Commit();
                    AddWarning(warnings, "Вид-пример создан, но его нельзя разместить на временном листе.");
                    return;
                }

                XYZ placementPoint = new XYZ(
                    UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings != null ? settings.SheetLayoutSettings.StartXmm : 0.0),
                    UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings != null ? settings.SheetLayoutSettings.StartYmm : 0.0),
                    0.0);

                Viewport.Create(document, temporarySheet.Id, sampleView.Id, placementPoint);
                sheetTransaction.Commit();
            }
            catch (Exception exception)
            {
                if (sheetTransaction.GetStatus() == TransactionStatus.Started)
                {
                    sheetTransaction.RollBack();
                }

                AddWarning(warnings, "Вид-пример создан, но размещение на временном листе не выполнено: " + exception.Message);
            }
        }

        private bool TryActivateView(UIDocument uiDocument, View view, IList<string> warnings)
        {
            if (uiDocument == null || view == null || !view.IsValidObject)
            {
                AddWarning(warnings, "Не удалось активировать вид Revit.");
                return false;
            }

            try
            {
                uiDocument.ActiveView = view;
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось открыть вид \"" + view.Name + "\": " + exception.Message);
                return false;
            }
        }

        private string GenerateUniqueViewName(Document document, string baseName)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(baseName)
                ? SampleViewBaseName
                : baseName.Trim();

            if (!ViewNameExists(document, safeBaseName))
            {
                return safeBaseName;
            }

            for (int index = 2; index < 1000; index++)
            {
                string candidate = safeBaseName + " " + index;
                if (!ViewNameExists(document, candidate))
                {
                    return candidate;
                }
            }

            return safeBaseName + " " + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private bool ViewNameExists(Document document, string viewName)
        {
            if (document == null || string.IsNullOrWhiteSpace(viewName))
            {
                return false;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null)
                {
                    continue;
                }

                if (string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void AppendWarnings(IList<string> target, IList<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                target.Add(source[index]);
            }
        }

        private void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            warnings.Add(warning);
        }
    }
}
