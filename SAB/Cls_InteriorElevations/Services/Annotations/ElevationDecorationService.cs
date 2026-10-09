using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Annotations
{
    public class ElevationDecorationProgressInfo
    {
        public int CurrentStep { get; set; }

        public int TotalSteps { get; set; }

        public string Stage { get; set; }

        public string Details { get; set; }

        public ElevationDecorationResult Result { get; set; }
    }

    public class ElevationDecorationService
    {
        private const double ElevationToleranceMm = 5.0;
        private const double PlinthMaximumHeightMm = 300.0;
        private const double SectionPlaneToleranceMm = 1.0;
        private const double BroadWallMaximumDirectionDot = 0.35;
        private const string DiagnosticFamilyName = "SAB_Диагностика оформления";

        private readonly ElevationAnnotationOwnershipService _ownershipService;
        private readonly ElevationDecorationCatalogService _catalogService;
        private ElevationDecorationCatalog _catalog;

        public ElevationDecorationService()
        {
            _ownershipService = new ElevationAnnotationOwnershipService();
            _catalogService = new ElevationDecorationCatalogService();
        }

        public ElevationDecorationResult DecorateViews(
            Document document,
            IList<ViewSection> views,
            ElevationDecorationSettings settings,
            IList<string> warnings,
            Action<ElevationDecorationProgressInfo> progress = null)
        {
            ElevationDecorationResult result = new ElevationDecorationResult();
            if (document == null || views == null || settings == null)
            {
                return result;
            }

            _catalog = _catalogService.Load();
            List<ElevationDecorationCatalogRole> missingRoles = Enum
                .GetValues(typeof(ElevationDecorationCatalogRole))
                .Cast<ElevationDecorationCatalogRole>()
                .Where(role => !_catalogService.HasRole(_catalog, role))
                .ToList();
            if (missingRoles.Count > 0)
            {
                AddWarning(
                    warnings,
                    "Оформление остановлено: каталог не заполнен для ролей: " +
                    string.Join(", ", missingRoles.Select(
                        ElevationDecorationCatalogRoleNames.GetDisplayName)) + ".");
                return result;
            }

            int stepsPerView = 2;
            if (settings.PlaceSpotElevations)
            {
                stepsPerView++;
            }

            if (settings.PlaceTags)
            {
                stepsPerView++;
            }

            if (settings.PlaceDimensions)
            {
                stepsPerView++;
            }

            int totalSteps = Math.Max(1, views.Count * stepsPerView);
            ReportProgress(
                progress,
                0,
                totalSteps,
                "Подготовка оформления",
                "Подготовлено видов: " + views.Count + ".",
                result);

            for (int index = 0; index < views.Count; index++)
            {
                ViewSection view = views[index];
                int currentStep = index * stepsPerView;
                if (view == null || view.IsTemplate)
                {
                    result.FailedItems++;
                    ReportProgress(
                        progress,
                        (index + 1) * stepsPerView,
                        totalSteps,
                        "Вид пропущен",
                        "Вид " + (index + 1) + " из " + views.Count + " недоступен для оформления.",
                        result);
                    continue;
                }

                try
                {
                    string viewDetails = "Вид " + (index + 1) + " из " + views.Count +
                                         ": " + view.Name + ".";
                    ElevationViewGeometryContext geometryContext;
                    string geometryError;
                    if (!TryCreateViewGeometryContext(
                        view,
                        out geometryContext,
                        out geometryError))
                    {
                        result.FailedItems++;
                        AddWarning(
                            warnings,
                            "Не удалось получить геометрию вида \"" + view.Name +
                            "\": " + geometryError);
                        ReportProgress(
                            progress,
                            (index + 1) * stepsPerView,
                            totalSteps,
                            "Геометрия вида не обработана",
                            viewDetails,
                            result);
                        continue;
                    }

                    currentStep++;
                    ReportProgress(
                        progress,
                        currentStep,
                        totalSteps,
                        "Анализ геометрии",
                        viewDetails,
                        result);

                    result.DeletedOwnedAnnotations += _ownershipService.DeleteOwnedAnnotations(
                        document,
                        view.Id,
                        settings);
                    currentStep++;
                    ReportProgress(
                        progress,
                        currentStep,
                        totalSteps,
                        "Обновление оформления",
                        viewDetails + " Предыдущее оформление SAB удалено.",
                        result);

                    if (settings.PlaceSpotElevations)
                    {
                        result.SpotElevationsCreated += PlaceSpotElevations(
                            document,
                            view,
                            geometryContext,
                            settings,
                            warnings);
                        currentStep++;
                        ReportProgress(
                            progress,
                            currentStep,
                            totalSteps,
                            "Высотные отметки",
                            viewDetails,
                            result);
                    }

                    if (settings.PlaceTags)
                    {
                        result.TagsCreated += PlaceTags(
                            document,
                            view,
                            geometryContext,
                            settings,
                            warnings);
                        currentStep++;
                        ReportProgress(
                            progress,
                            currentStep,
                            totalSteps,
                            "Марки материалов",
                            viewDetails,
                            result);
                    }

                    if (settings.PlaceDimensions)
                    {
                        result.DimensionsCreated += PlaceDimensions(
                            document,
                            view,
                            geometryContext,
                            settings,
                            warnings);
                        currentStep++;
                        ReportProgress(
                            progress,
                            currentStep,
                            totalSteps,
                            "Размерные линии",
                            viewDetails,
                            result);
                    }

                    result.ViewsProcessed++;
                }
                catch (Exception exception)
                {
                    result.FailedItems++;
                    if (warnings != null)
                    {
                        warnings.Add(
                            "Не удалось оформить вид \"" + view.Name + "\": " + exception.Message);
                    }

                    ReportProgress(
                        progress,
                        (index + 1) * stepsPerView,
                        totalSteps,
                        "Ошибка оформления",
                        "Вид " + (index + 1) + " из " + views.Count +
                        ": " + view.Name + ".",
                        result);
                }
            }

            return result;
        }

        private static void ReportProgress(
            Action<ElevationDecorationProgressInfo> progress,
            int currentStep,
            int totalSteps,
            string stage,
            string details,
            ElevationDecorationResult result)
        {
            if (progress == null)
            {
                return;
            }

            progress(new ElevationDecorationProgressInfo
            {
                CurrentStep = Math.Max(0, Math.Min(currentStep, totalSteps)),
                TotalSteps = Math.Max(1, totalSteps),
                Stage = stage,
                Details = details,
                Result = result
            });
        }

        private int PlaceSpotElevations(
            Document document,
            ViewSection view,
            ElevationViewGeometryContext geometryContext,
            ElevationDecorationSettings settings,
            IList<string> warnings)
        {
            if (!IsValidSpotType(document, settings.FloorSpotElevationTypeId) ||
                !IsValidSpotType(document, settings.OverheadSpotElevationTypeId))
            {
                AddWarning(
                    warnings,
                    "Для высотных отметок не выбраны корректные типы пола и потолка/ЖБ-перекрытия.");
                return 0;
            }

            bool hasRelativeBase = IsValidRelativeBaseLevel(
                document,
                settings.SpotElevationRelativeBaseLevelId);
            if (!hasRelativeBase)
            {
                AddWarning(
                    warnings,
                    "Для высотных отметок не выбрана относительная база. " +
                    "Отметки будут созданы с настройкой типа семейства.");
            }

            double? sectionPlaneDepth = geometryContext.SectionPlaneDepth;

            List<HostFaceCandidate> floorTopFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true);
            List<HostFaceCandidate> floorBottomFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                false);
            List<HostFaceCandidate> ceilingFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false);
            List<HostFaceCandidate> linkedFloorTopFaces = CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true);
            List<HostFaceCandidate> linkedFloorBottomFaces = CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                false);
            List<HostFaceCandidate> linkedCeilingFaces = CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false);

            floorTopFaces = FilterHorizontalFacesForSpotElevations(
                view,
                floorTopFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            floorBottomFaces = FilterHorizontalFacesForSpotElevations(
                view,
                floorBottomFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            ceilingFaces = FilterHorizontalFacesForSpotElevations(
                view,
                ceilingFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            linkedFloorTopFaces = FilterHorizontalFacesForSpotElevations(
                view,
                linkedFloorTopFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            linkedFloorBottomFaces = FilterHorizontalFacesForSpotElevations(
                view,
                linkedFloorBottomFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            linkedCeilingFaces = FilterHorizontalFacesForSpotElevations(
                view,
                linkedCeilingFaces,
                sectionPlaneDepth,
                settings.AnnotationSide);
            ceilingFaces.AddRange(linkedCeilingFaces);

            List<HostFaceCandidate> finishFloorFaces = FilterFacesByCatalogRole(
                floorTopFaces.Concat(linkedFloorTopFaces),
                ElevationDecorationCatalogRole.FinishFloor);
            List<HostFaceCandidate> subfloorCatalogFaces = FilterFacesByCatalogRole(
                floorTopFaces.Concat(linkedFloorTopFaces),
                ElevationDecorationCatalogRole.Subfloor);
            List<HostFaceCandidate> structuralFloorTopFaces = FilterFacesByCatalogRole(
                floorTopFaces.Concat(linkedFloorTopFaces),
                ElevationDecorationCatalogRole.StructuralSlab);
            List<HostFaceCandidate> structuralFloorBottomFaces = FilterFacesByCatalogRole(
                floorBottomFaces.Concat(linkedFloorBottomFaces),
                ElevationDecorationCatalogRole.StructuralSlab);
            ceilingFaces = FilterFacesByCatalogRole(
                ceilingFaces,
                ElevationDecorationCatalogRole.Ceiling);

            double floorReferenceElevation = GetFloorReferenceElevation(view);
            HostFaceCandidate finishFloor = FindFinishFloor(
                finishFloorFaces,
                floorReferenceElevation);
            if (settings.PlaceFinishFloorSpotElevation && finishFloor == null)
            {
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name +
                    "\" не найден чистовой пол с верхней гранью на отметке связанного уровня.");
            }

            double floorElevation = finishFloor != null
                ? finishFloor.Elevation
                : floorReferenceElevation;

            HostFaceCandidate structuralBase = FindStructuralBase(
                structuralFloorTopFaces,
                floorReferenceElevation);
            if (settings.PlaceStructuralBaseSpotElevation && structuralBase == null)
            {
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name +
                    "\" не найдено ЖБ-основание из каталога ниже чистого нуля.");
            }

            double elevationTolerance = UnitConversionUtils.MillimetersToFeet(
                ElevationToleranceMm);
            double structuralBaseElevation = structuralBase != null
                ? structuralBase.Elevation
                : double.NegativeInfinity;
            List<HostFaceCandidate> subfloorFaces = subfloorCatalogFaces
                .Where(candidate =>
                    candidate.Elevation < floorReferenceElevation - elevationTolerance &&
                    candidate.Elevation > structuralBaseElevation + elevationTolerance)
                .OrderByDescending(candidate => candidate.Elevation)
                .ToList();

            List<HostFaceCandidate> slabCandidates = structuralFloorBottomFaces
                .Where(candidate =>
                    candidate.Elevation > floorElevation + UnitConversionUtils.MillimetersToFeet(1000.0))
                .OrderBy(candidate => candidate.Elevation)
                .ToList();
            HostFaceCandidate upperSlab = slabCandidates.FirstOrDefault();
            if (upperSlab == null)
            {
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name +
                    "\" не найдено верхнее перекрытие из каталога, " +
                    "которое пересекает плоскость сечения выше чистого пола.");
            }

            List<HostFaceCandidate> targets = new List<HostFaceCandidate>();
            if (settings.PlaceFinishFloorSpotElevation && finishFloor != null)
            {
                targets.Add(finishFloor.WithRole("Spot.FinishFloor"));
            }

            if (settings.PlaceSubfloorSpotElevations)
            {
                AddUniqueElevationCandidates(
                    targets,
                    subfloorFaces,
                    "Spot.Subfloor");
            }

            if (settings.PlaceStructuralBaseSpotElevation && structuralBase != null)
            {
                targets.Add(structuralBase.WithRole("Spot.StructuralBase"));
            }

            double maximumCeilingElevation = upperSlab != null
                ? upperSlab.Elevation + UnitConversionUtils.MillimetersToFeet(ElevationToleranceMm)
                : double.MaxValue;
            AddUniqueElevationCandidates(
                targets,
                ceilingFaces
                    .Where(candidate =>
                        candidate.Elevation > floorElevation + UnitConversionUtils.MillimetersToFeet(300.0) &&
                        candidate.Elevation < maximumCeilingElevation)
                    .OrderBy(candidate => candidate.Elevation),
                "Spot.Ceiling");

            if (upperSlab != null)
            {
                targets.Add(upperSlab.WithRole("Spot.StructuralSlab"));
            }

            int createdCount = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                HostFaceCandidate target = targets[index];
                try
                {
                    bool usedFallbackReferencePoint;
                    SpotDimension spot = CreateSpotElevation(
                        document,
                        view,
                        target,
                        settings,
                        sectionPlaneDepth,
                        out usedFallbackReferencePoint);
                    if (spot == null)
                    {
                        string reason = "Revit не создал высотную отметку " +
                            target.Role + ", хотя подходящая грань была найдена.";
                        CreateFailureDiagnostic(
                            document,
                            view,
                            target.ReferencePoint,
                            target.Role,
                            reason);
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                        continue;
                    }

                    bool isFloorRelatedSpot =
                        string.Equals(target.Role, "Spot.FinishFloor", StringComparison.Ordinal) ||
                        string.Equals(target.Role, "Spot.Subfloor", StringComparison.Ordinal) ||
                        string.Equals(target.Role, "Spot.StructuralBase", StringComparison.Ordinal);
                    ElementId spotTypeId = isFloorRelatedSpot
                        ? settings.FloorSpotElevationTypeId
                        : settings.OverheadSpotElevationTypeId;
                    if (spot.GetTypeId() != spotTypeId)
                    {
                        spot.ChangeTypeId(spotTypeId);
                    }

                    document.Regenerate();
                    string relativeBaseError;
                    if (hasRelativeBase && !TrySetSpotRelativeBase(
                            spot,
                            settings.SpotElevationRelativeBaseLevelId,
                            out relativeBaseError))
                    {
                        string reason = "Высотная отметка создана, но относительная база " +
                            "не назначена: " + relativeBaseError;
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                    }

                    if (usedFallbackReferencePoint)
                    {
                        string reason = "Высотная отметка создана по ближайшей " +
                            "доступной точке грани вне выбранной боковой пятой части вида.";
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                    }

                    _ownershipService.MarkOwned(spot, view.Id, target.Role);
                    createdCount++;
                }
                catch (Exception exception)
                {
                    string reason = "Не удалось поставить высотную отметку " +
                        target.Role + ": " + exception.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        target.ReferencePoint,
                        target.Role,
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            return createdCount;
        }

        private int PlaceDimensions(
            Document document,
            ViewSection view,
            ElevationViewGeometryContext geometryContext,
            ElevationDecorationSettings settings,
            IList<string> warnings)
        {
            bool invalidVerticalTypes = settings.PlaceVerticalDimensions &&
                (!IsValidDimensionType(document, settings.DetailedDimensionTypeId) ||
                 !IsValidDimensionType(document, settings.OverallDimensionTypeId));
            bool invalidHorizontalType = settings.PlaceHorizontalDimensions &&
                !IsValidDimensionType(document, settings.WidthDimensionTypeId);
            if (invalidVerticalTypes || invalidHorizontalType)
            {
                AddWarning(warnings, "Для размерных линий не выбраны корректные линейные типы.");
                return 0;
            }

            List<ElementCandidate> visibleWalls = CollectHostElements(
                document,
                view,
                BuiltInCategory.OST_Walls);
            visibleWalls.AddRange(CollectLinkedElements(
                document,
                view,
                BuiltInCategory.OST_Walls));
            visibleWalls = FilterElementsByCatalogRole(
                visibleWalls,
                ElevationDecorationCatalogRole.FinishWall);
            double? sectionPlaneDepth = geometryContext.SectionPlaneDepth;
            int createdCount = 0;

            if (settings.PlaceHorizontalDimensions)
            {
                try
                {
                    IList<WallFaceCandidate> widthFaces = FindRoomWidthWallFaces(
                        view,
                        visibleWalls,
                        sectionPlaneDepth);
                    Dimension widthDimension = CreateHorizontalDimension(
                        document,
                        view,
                        widthFaces,
                        settings.WidthDimensionTypeId,
                        settings.WidthDimensionOffsetPaperMm);
                    if (widthDimension != null)
                    {
                        _ownershipService.MarkOwned(
                            widthDimension,
                            view.Id,
                            "Dimension.Width");
                        createdCount++;
                    }
                    else
                    {
                        string reason = "Не найдены левая и правая чистовые стены для размера ширины.";
                        CreateFailureDiagnostic(
                            document,
                            view,
                            GetViewDiagnosticPoint(view, 0.50, 0.08),
                            "Dimension.Width",
                            reason);
                        AddWarning(warnings, "На виде \"" + view.Name + "\" " + reason);
                    }
                }
                catch (Exception exception)
                {
                    string reason = "Не удалось создать размер ширины помещения: " +
                        exception.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        GetViewDiagnosticPoint(view, 0.50, 0.08),
                        "Dimension.Width",
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            if (!settings.PlaceVerticalDimensions)
            {
                return createdCount;
            }

            List<HostFaceCandidate> floorTopFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true);
            floorTopFaces.AddRange(CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true));
            floorTopFaces = FilterFacesBySectionPlane(view, floorTopFaces, sectionPlaneDepth);
            List<HostFaceCandidate> finishFloorFaces = FilterFacesByCatalogRole(
                floorTopFaces,
                ElevationDecorationCatalogRole.FinishFloor);
            List<HostFaceCandidate> floorBottomFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                false);
            floorBottomFaces.AddRange(CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                false));
            floorBottomFaces = FilterFacesBySectionPlane(view, floorBottomFaces, sectionPlaneDepth);
            floorBottomFaces = FilterFacesByCatalogRole(
                floorBottomFaces,
                ElevationDecorationCatalogRole.StructuralSlab);

            List<HostFaceCandidate> ceilingFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false);
            ceilingFaces.AddRange(CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false));
            ceilingFaces = FilterFacesBySectionPlane(view, ceilingFaces, sectionPlaneDepth);
            ceilingFaces = FilterFacesByCatalogRole(
                ceilingFaces,
                ElevationDecorationCatalogRole.Ceiling);

            HostFaceCandidate finishFloor = FindFinishFloor(
                finishFloorFaces,
                GetFloorReferenceElevation(view));
            if (finishFloor == null)
            {
                string reason = "Не найден чистовой пол из каталога в секущей плоскости вида.";
                CreateFailureDiagnostic(
                    document,
                    view,
                    GetViewDiagnosticPoint(view, 0.35, 0.08),
                    "Dimension.FinishFloor",
                    reason);
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name + "\" " + reason);
                return createdCount;
            }

            double floorElevation = finishFloor.Elevation;
            HostFaceCandidate upperSlab = floorBottomFaces
                .Where(candidate =>
                    candidate.Elevation > floorElevation +
                        UnitConversionUtils.MillimetersToFeet(1000.0))
                .OrderBy(candidate => candidate.Elevation)
                .FirstOrDefault();
            if (upperSlab == null)
            {
                string reason = "Не найден низ верхнего ЖБ-перекрытия из каталога.";
                CreateFailureDiagnostic(
                    document,
                    view,
                    GetViewDiagnosticPoint(view, 0.35, 0.92),
                    "Dimension.StructuralSlab",
                    reason);
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name + "\" " + reason);
                return createdCount;
            }

            List<HostFaceCandidate> detailedReferences = new List<HostFaceCandidate>
            {
                finishFloor.WithRole("Dimension.Detailed.Floor")
            };

            AddUniqueElevationCandidates(
                detailedReferences,
                CollectPlinthTopFaces(document, view, sectionPlaneDepth)
                    .Where(candidate =>
                        candidate.Elevation > floorElevation +
                            UnitConversionUtils.MillimetersToFeet(1.0) &&
                        candidate.Elevation <= floorElevation +
                            UnitConversionUtils.MillimetersToFeet(PlinthMaximumHeightMm + 10.0))
                    .OrderBy(candidate => candidate.Elevation),
                "Dimension.Detailed.Plinth");

            AddUniqueElevationCandidates(
                detailedReferences,
                ceilingFaces
                    .Where(candidate =>
                        candidate.Elevation > floorElevation +
                            UnitConversionUtils.MillimetersToFeet(300.0) &&
                        candidate.Elevation < upperSlab.Elevation +
                            UnitConversionUtils.MillimetersToFeet(ElevationToleranceMm))
                    .OrderBy(candidate => candidate.Elevation),
                "Dimension.Detailed.Ceiling");
            AddUniqueElevationCandidates(
                detailedReferences,
                new[] { upperSlab },
                "Dimension.Detailed.StructuralSlab");

            List<HostFaceCandidate> overallReferences = new List<HostFaceCandidate>
            {
                finishFloor,
                upperSlab
            };

            Dimension detailedDimension = null;
            try
            {
                detailedDimension = CreateVerticalDimension(
                    document,
                    view,
                    detailedReferences,
                    settings.DetailedDimensionTypeId,
                    settings.DetailedDimensionOffsetPaperMm,
                    settings.AnnotationSide);
            }
            catch (Exception exception)
            {
                List<HostFaceCandidate> withoutPlinth = detailedReferences
                    .Where(candidate =>
                        candidate.Role == null ||
                        !candidate.Role.Contains("Plinth"))
                    .ToList();
                try
                {
                    detailedDimension = CreateVerticalDimension(
                        document,
                        view,
                        withoutPlinth,
                        settings.DetailedDimensionTypeId,
                        settings.DetailedDimensionOffsetPaperMm,
                        settings.AnnotationSide);
                    if (detailedDimension != null)
                    {
                        string reason = "Вертикальная цепочка создана без привязки " +
                            "плинтуса: " + exception.Message;
                        CreateFailureDiagnostic(
                            document,
                            view,
                            GetViewDiagnosticPoint(view, 0.10, 0.50),
                            "Dimension.Detailed.Plinth",
                            reason);
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                    }
                }
                catch (Exception fallbackException)
                {
                    string reason = "Не удалось создать вертикальную цепочку размеров: " +
                        fallbackException.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        GetViewDiagnosticPoint(view, 0.10, 0.50),
                        "Dimension.Detailed",
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            if (detailedDimension != null)
            {
                _ownershipService.MarkOwned(
                    detailedDimension,
                    view.Id,
                    "Dimension.Detailed");
                createdCount++;
            }
            else if (warnings != null && !warnings.Any(message =>
                message.StartsWith(
                    "На виде \"" + view.Name +
                    "\" Не удалось создать вертикальную цепочку размеров:",
                    StringComparison.Ordinal)))
            {
                string reason = "Вертикальная цепочка размеров не создана: " +
                    "недостаточно корректных геометрических ссылок.";
                CreateFailureDiagnostic(
                    document,
                    view,
                    GetViewDiagnosticPoint(view, 0.10, 0.50),
                    "Dimension.Detailed",
                    reason);
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name + "\" " + reason);
            }

            Dimension overallDimension = null;
            try
            {
                overallDimension = CreateVerticalDimension(
                    document,
                    view,
                    overallReferences,
                    settings.OverallDimensionTypeId,
                    settings.OverallDimensionOffsetPaperMm,
                    settings.AnnotationSide);
            }
            catch (Exception exception)
            {
                string reason = "Не удалось создать общий вертикальный размер: " +
                    exception.Message;
                CreateFailureDiagnostic(
                    document,
                    view,
                    GetViewDiagnosticPoint(view, 0.04, 0.50),
                    "Dimension.Overall",
                    reason);
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name + "\" " + reason);
            }

            if (overallDimension != null)
            {
                _ownershipService.MarkOwned(
                    overallDimension,
                    view.Id,
                    "Dimension.Overall");
                createdCount++;
            }
            else if (warnings != null && !warnings.Any(message =>
                message.StartsWith(
                    "На виде \"" + view.Name +
                    "\" Не удалось создать общий вертикальный размер:",
                    StringComparison.Ordinal)))
            {
                string reason = "Общий вертикальный размер не создан: " +
                    "не удалось получить две допустимые ссылки.";
                CreateFailureDiagnostic(
                    document,
                    view,
                    GetViewDiagnosticPoint(view, 0.04, 0.50),
                    "Dimension.Overall",
                    reason);
                AddWarning(
                    warnings,
                    "На виде \"" + view.Name + "\" " + reason);
            }

            return createdCount;
        }

        private int PlaceTags(
            Document document,
            ViewSection view,
            ElevationViewGeometryContext geometryContext,
            ElevationDecorationSettings settings,
            IList<string> warnings)
        {
            int createdCount = 0;

            List<ElementCandidate> visibleWalls = CollectHostElements(
                document,
                view,
                BuiltInCategory.OST_Walls);
            visibleWalls.AddRange(CollectLinkedElements(
                document,
                view,
                BuiltInCategory.OST_Walls));
            double? sectionPlaneDepth = geometryContext.SectionPlaneDepth;

            List<HostFaceCandidate> visibleFloorFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true);
            visibleFloorFaces.AddRange(CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Floors,
                true));
            visibleFloorFaces = FilterFacesBySectionPlane(
                view,
                visibleFloorFaces,
                sectionPlaneDepth);
            visibleFloorFaces = FilterFacesByCatalogRole(
                visibleFloorFaces,
                ElevationDecorationCatalogRole.FinishFloor);
            double floorReferenceElevation = GetFloorReferenceElevation(view);
            double elevationTolerance = UnitConversionUtils.MillimetersToFeet(
                ElevationToleranceMm);
            List<HostFaceCandidate> finishFloorFaces = visibleFloorFaces
                .Where(candidate =>
                    Math.Abs(candidate.Elevation - floorReferenceElevation) <=
                    elevationTolerance)
                .ToList();
            if (settings.PlaceFloorTags)
            {
                createdCount += TagHorizontalFacesByTypeRule(
                    document,
                    view,
                    finishFloorFaces,
                    settings.FloorTagTypeId,
                    settings.FloorTagHasLeader,
                    TagVerticalPlacement.Above,
                    settings,
                    "Tag.Floor",
                    warnings);
            }

            List<HostFaceCandidate> ceilingFaces = CollectHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false);
            ceilingFaces.AddRange(CollectLinkedHostFaces(
                document,
                view,
                BuiltInCategory.OST_Ceilings,
                false));
            ceilingFaces = FilterFacesBySectionPlane(
                view,
                ceilingFaces,
                sectionPlaneDepth);
            ceilingFaces = FilterFacesByCatalogRole(
                ceilingFaces,
                ElevationDecorationCatalogRole.Ceiling);
            if (settings.PlaceCeilingTags)
            {
                createdCount += TagHorizontalFacesByTypeRule(
                    document,
                    view,
                    ceilingFaces,
                    settings.CeilingTagTypeId,
                    settings.CeilingTagHasLeader,
                    TagVerticalPlacement.Below,
                    settings,
                    "Tag.Ceiling",
                    warnings);
            }

            List<ElementCandidate> finishWalls = FilterElementsByCatalogRole(
                visibleWalls,
                ElevationDecorationCatalogRole.FinishWall);
            finishWalls = finishWalls
                .Where(candidate => !IsEdgeOnWall(view, candidate))
                .ToList();
            finishWalls = FilterPrimaryVisibleWalls(view, finishWalls);
            if (settings.PlaceWallTags)
            {
                createdCount += TagWalls(
                    document,
                    view,
                    finishWalls,
                    settings.WallTagTypeId,
                    TagVerticalPlacement.Center,
                    settings,
                    "Tag.Wall",
                    warnings);
            }

            List<HostFaceCandidate> plinthTopFaces = CollectPlinthTopFaces(
                document,
                view,
                sectionPlaneDepth);
            plinthTopFaces = FilterFacesBySectionPlane(
                view,
                plinthTopFaces,
                sectionPlaneDepth);
            if (settings.PlacePlinthTags)
            {
                createdCount += TagHorizontalFacesByTypeRule(
                    document,
                    view,
                    plinthTopFaces,
                    settings.PlinthTagTypeId,
                    settings.PlinthTagHasLeader,
                    TagVerticalPlacement.Above,
                    settings,
                    "Tag.Plinth",
                    warnings);
            }

            List<ElementCandidate> doors = CollectHostElements(
                document,
                view,
                BuiltInCategory.OST_Doors);
            doors.AddRange(CollectLinkedElements(document, view, BuiltInCategory.OST_Doors));
            doors = FilterElementsByCatalogRole(
                doors,
                ElevationDecorationCatalogRole.Door);
            doors = doors
                .Where(candidate => IsFrontFacingOpening(view, candidate))
                .ToList();
            doors = DeduplicateElementCandidates(doors);
            if (settings.PlaceDoorTags)
            {
                createdCount += TagEveryCandidate(
                    document,
                    view,
                    doors,
                    settings.DoorTagTypeId,
                    settings.DoorTagHasLeader,
                    TagVerticalPlacement.Center,
                    settings,
                    "Tag.Door",
                    warnings);
            }

            List<ElementCandidate> windows = CollectHostElements(
                document,
                view,
                BuiltInCategory.OST_Windows);
            windows.AddRange(CollectLinkedElements(
                document,
                view,
                BuiltInCategory.OST_Windows));
            windows = FilterElementsByCatalogRole(
                windows,
                ElevationDecorationCatalogRole.Window);
            windows = windows
                .Where(candidate => IsFrontFacingOpening(view, candidate))
                .ToList();
            windows = DeduplicateElementCandidates(windows);
            if (settings.PlaceWindowTags)
            {
                createdCount += TagEveryCandidate(
                    document,
                    view,
                    windows,
                    settings.WindowTagTypeId,
                    settings.WindowTagHasLeader,
                    TagVerticalPlacement.Center,
                    settings,
                    "Tag.Window",
                    warnings);
            }

            return createdCount;
        }

        private Dimension CreateVerticalDimension(
            Document document,
            ViewSection view,
            IList<HostFaceCandidate> candidates,
            ElementId dimensionTypeId,
            double offsetPaperMm,
            ElevationAnnotationSide side)
        {
            if (document == null || view == null || candidates == null)
            {
                return null;
            }

            double elevationTolerance = UnitConversionUtils.MillimetersToFeet(
                ElevationToleranceMm);
            List<HostFaceCandidate> ordered = candidates
                .Where(candidate => candidate != null && candidate.Reference != null)
                .OrderBy(candidate => candidate.Elevation)
                .ToList();
            List<HostFaceCandidate> unique = new List<HostFaceCandidate>();
            for (int index = 0; index < ordered.Count; index++)
            {
                HostFaceCandidate candidate = ordered[index];
                if (unique.Any(existing =>
                    Math.Abs(existing.Elevation - candidate.Elevation) <= elevationTolerance))
                {
                    continue;
                }

                unique.Add(candidate);
            }

            if (unique.Count < 2)
            {
                return null;
            }

            BoundingBoxXYZ crop = view.CropBox;
            Transform cropInverse = crop.Transform.Inverse;
            List<XYZ> localReferencePoints = unique
                .Select(candidate => cropInverse.OfPoint(candidate.ReferencePoint))
                .ToList();
            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, offsetPaperMm) * Math.Max(1, view.Scale));
            bool placeOnLeft = side == ElevationAnnotationSide.Left;
            double cropEdgeX = placeOnLeft
                ? Math.Min(crop.Min.X, crop.Max.X)
                : Math.Max(crop.Min.X, crop.Max.X);
            double dimensionX = cropEdgeX + (placeOnLeft ? -offset : offset);
            double lineZ = (crop.Min.Z + crop.Max.Z) / 2.0;
            double minimumY = localReferencePoints.Min(point => point.Y);
            double maximumY = localReferencePoints.Max(point => point.Y);
            if (maximumY - minimumY <= elevationTolerance)
            {
                return null;
            }

            Line dimensionLine = Line.CreateBound(
                crop.Transform.OfPoint(new XYZ(dimensionX, minimumY, lineZ)),
                crop.Transform.OfPoint(new XYZ(dimensionX, maximumY, lineZ)));
            ReferenceArray references = new ReferenceArray();
            for (int index = 0; index < unique.Count; index++)
            {
                references.Append(unique[index].Reference);
            }

            DimensionType dimensionType = document.GetElement(dimensionTypeId) as DimensionType;
            return document.Create.NewDimension(
                view,
                dimensionLine,
                references,
                dimensionType);
        }

        private IList<WallFaceCandidate> FindRoomWidthWallFaces(
            ViewSection view,
            IList<ElementCandidate> visibleWalls,
            double? sectionPlaneDepth)
        {
            List<WallFaceCandidate> result = new List<WallFaceCandidate>();
            if (view == null || visibleWalls == null || visibleWalls.Count < 2)
            {
                return result;
            }

            XYZ horizontalViewDirection = new XYZ(
                view.ViewDirection.X,
                view.ViewDirection.Y,
                0.0);
            if (horizontalViewDirection.GetLength() <= 1e-9)
            {
                return result;
            }

            horizontalViewDirection = horizontalViewDirection.Normalize();
            List<ElementCandidate> sideWalls = visibleWalls
                .Where(candidate =>
                {
                    XYZ wallDirection;
                    return TryGetWallDirection(candidate, out wallDirection) &&
                           Math.Abs(wallDirection.DotProduct(horizontalViewDirection)) >= 0.80;
                })
                .ToList();
            if (sideWalls.Count < 2)
            {
                return result;
            }

            Transform cropInverse = view.CropBox.Transform.Inverse;
            double sectionTolerance = UnitConversionUtils.MillimetersToFeet(
                SectionPlaneToleranceMm);
            List<WallFaceCandidate> faces = new List<WallFaceCandidate>();
            for (int wallIndex = 0; wallIndex < sideWalls.Count; wallIndex++)
            {
                ElementCandidate wallCandidate = sideWalls[wallIndex];
                Wall wall = wallCandidate.Element as Wall;
                if (wall == null)
                {
                    continue;
                }

                Transform sourceToHost = wallCandidate.SourceToHostTransform ?? Transform.Identity;

                List<Reference> sideFaceReferences = new List<Reference>();
                sideFaceReferences.AddRange(
                    HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior));
                sideFaceReferences.AddRange(
                    HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior));
                for (int faceIndex = 0; faceIndex < sideFaceReferences.Count; faceIndex++)
                {
                    Reference faceReference = sideFaceReferences[faceIndex];
                    PlanarFace face = wall.GetGeometryObjectFromReference(faceReference) as PlanarFace;
                    if (face == null)
                    {
                        continue;
                    }

                    XYZ normal = sourceToHost.OfVector(face.FaceNormal);
                    if (normal.GetLength() <= 1e-9 ||
                        Math.Abs(normal.Normalize().DotProduct(view.RightDirection)) < 0.95)
                    {
                        continue;
                    }

                    if (sectionPlaneDepth.HasValue &&
                        !FaceIntersectsSectionPlane(
                            view,
                            face,
                            sourceToHost,
                            sectionPlaneDepth.Value,
                            sectionTolerance))
                    {
                        continue;
                    }

                    BoundingBoxUV uvBounds = face.GetBoundingBox();
                    XYZ sourcePoint = face.Evaluate(new UV(
                        (uvBounds.Min.U + uvBounds.Max.U) / 2.0,
                        (uvBounds.Min.V + uvBounds.Max.V) / 2.0));
                    XYZ point = sourceToHost.OfPoint(sourcePoint);
                    Reference hostReference = wallCandidate.LinkInstance != null
                        ? faceReference.CreateLinkReference(wallCandidate.LinkInstance)
                        : faceReference;
                    faces.Add(new WallFaceCandidate
                    {
                        WallCandidate = wallCandidate,
                        Reference = hostReference,
                        Point = point,
                        LocalX = cropInverse.OfPoint(point).X
                    });
                }
            }

            if (faces.Count < 2)
            {
                return result;
            }

            double cropCenterX =
                (view.CropBox.Min.X + view.CropBox.Max.X) / 2.0;
            double cropMinX = Math.Min(view.CropBox.Min.X, view.CropBox.Max.X);
            double cropMaxX = Math.Max(view.CropBox.Min.X, view.CropBox.Max.X);
            List<IGrouping<ElementCandidate, WallFaceCandidate>> wallGroups = faces
                .GroupBy(face => face.WallCandidate)
                .ToList();
            IGrouping<ElementCandidate, WallFaceCandidate> leftWall = wallGroups
                .Where(group => cropInverse.OfPoint(group.First().WallCandidate.Center).X < cropCenterX)
                .OrderBy(group => group.Min(face => Math.Abs(face.LocalX - cropMinX)))
                .ThenBy(group => GetWallWidth(group.First().WallCandidate))
                .FirstOrDefault();
            IGrouping<ElementCandidate, WallFaceCandidate> rightWall = wallGroups
                .Where(group => cropInverse.OfPoint(group.First().WallCandidate.Center).X > cropCenterX)
                .OrderBy(group => group.Min(face => Math.Abs(face.LocalX - cropMaxX)))
                .ThenBy(group => GetWallWidth(group.First().WallCandidate))
                .FirstOrDefault();
            if (leftWall == null || rightWall == null || ReferenceEquals(leftWall.Key, rightWall.Key))
            {
                return result;
            }

            WallFaceCandidate leftInteriorFace = leftWall
                .OrderByDescending(face => face.LocalX)
                .First();
            WallFaceCandidate rightInteriorFace = rightWall
                .OrderBy(face => face.LocalX)
                .First();
            result.Add(leftInteriorFace);
            result.Add(rightInteriorFace);
            return result;
        }

        private Dimension CreateHorizontalDimension(
            Document document,
            ViewSection view,
            IList<WallFaceCandidate> wallFaces,
            ElementId dimensionTypeId,
            double offsetPaperMm)
        {
            if (document == null || view == null || wallFaces == null || wallFaces.Count != 2)
            {
                return null;
            }

            WallFaceCandidate left = wallFaces.OrderBy(face => face.LocalX).First();
            WallFaceCandidate right = wallFaces.OrderBy(face => face.LocalX).Last();
            if (right.LocalX - left.LocalX <= UnitConversionUtils.MillimetersToFeet(10.0))
            {
                return null;
            }

            BoundingBoxXYZ crop = view.CropBox;
            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, offsetPaperMm) * Math.Max(1, view.Scale));
            double dimensionY = Math.Min(crop.Min.Y, crop.Max.Y) - offset;
            double lineZ = (crop.Min.Z + crop.Max.Z) / 2.0;
            Line dimensionLine = Line.CreateBound(
                crop.Transform.OfPoint(new XYZ(left.LocalX, dimensionY, lineZ)),
                crop.Transform.OfPoint(new XYZ(right.LocalX, dimensionY, lineZ)));
            ReferenceArray references = new ReferenceArray();
            references.Append(left.Reference);
            references.Append(right.Reference);
            DimensionType dimensionType = document.GetElement(dimensionTypeId) as DimensionType;
            return document.Create.NewDimension(
                view,
                dimensionLine,
                references,
                dimensionType);
        }

        private int TagByTypeRule(
            Document document,
            ViewSection view,
            IList<ElementCandidate> candidates,
            ElementId tagTypeId,
            bool hasLeader,
            TagVerticalPlacement placement,
            ElevationDecorationSettings settings,
            string role,
            IList<string> warnings)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            int distinctTypeCount = candidates
                .Select(candidate => new
                {
                    SourceDocument = candidate.Element != null
                        ? candidate.Element.Document
                        : null,
                    TypeId = RevitElementIdUtils.GetElementIdValue(candidate.TypeId)
                })
                .Distinct()
                .Count();

            IList<ElementCandidate> selected = distinctTypeCount <= 1
                ? new List<ElementCandidate> { candidates[0] }
                : candidates;

            return TagEveryCandidate(
                document,
                view,
                selected,
                tagTypeId,
                hasLeader,
                placement,
                settings,
                role,
                warnings);
        }

        private int TagHorizontalFacesByTypeRule(
            Document document,
            ViewSection view,
            IList<HostFaceCandidate> faces,
            ElementId tagTypeId,
            bool hasLeader,
            TagVerticalPlacement placement,
            ElevationDecorationSettings settings,
            string role,
            IList<string> warnings)
        {
            if (faces == null || faces.Count == 0)
            {
                return 0;
            }

            if (!IsValidType(document, tagTypeId))
            {
                AddWarning(
                    warnings,
                    "Не выбран корректный тип марки для категории " + role + ".");
                return 0;
            }

            List<HostFaceCandidate> elementFaces = faces
                .Where(face => face != null &&
                    face.VisibleFragment != null &&
                    face.ElementCandidate != null &&
                    face.ElementCandidate.Element != null &&
                    face.ElementCandidate.Reference != null)
                .GroupBy(face => BuildElementCandidateKey(face.ElementCandidate))
                .Select(group => group
                    .OrderByDescending(face =>
                        face.VisibleFragment.MaxX - face.VisibleFragment.MinX)
                    .First())
                .ToList();
            if (elementFaces.Count == 0)
            {
                return 0;
            }

            int distinctTypeCount = elementFaces
                .Select(face => new
                {
                    SourceDocument = face.ElementCandidate.Element.Document,
                    TypeId = RevitElementIdUtils.GetElementIdValue(
                        face.ElementCandidate.TypeId)
                })
                .Distinct()
                .Count();
            List<HostFaceCandidate> selected = distinctTypeCount <= 1
                ? new List<HostFaceCandidate>
                {
                    elementFaces
                        .OrderByDescending(face =>
                            face.VisibleFragment.MaxX - face.VisibleFragment.MinX)
                        .First()
                }
                : elementFaces;

            int createdCount = 0;
            for (int index = 0; index < selected.Count; index++)
            {
                HostFaceCandidate face = selected[index];
                ElementCandidate element = face.ElementCandidate;
                try
                {
                    XYZ previewPoint = face.VisibleFragment.CenterPoint;
                    TagBodyExtents tagBody = MeasureTagBodyExtents(
                        document,
                        view,
                        tagTypeId,
                        element.Reference,
                        previewPoint);
                    XYZ leaderEndPoint = BuildHorizontalFaceTagAnchor(
                        view,
                        face,
                        tagBody.HalfWidth);
                    XYZ headPoint = BuildTagHeadPointFromAnchor(
                        view,
                        leaderEndPoint,
                        placement,
                        settings.TagOffsetPaperMm,
                        tagBody.HalfHeight);
                    IndependentTag tag = IndependentTag.Create(
                        document,
                        tagTypeId,
                        view.Id,
                        element.Reference,
                        hasLeader,
                        TagOrientation.Horizontal,
                        headPoint);
                    if (tag == null)
                    {
                        throw new InvalidOperationException(
                            "Revit не создал марку по рассчитанной точке.");
                    }

                    tag.TagHeadPosition = headPoint;
                    if (hasLeader)
                    {
                        string leaderError;
                        if (!TryConfigureTagLeader(
                            tag,
                            view,
                            element.Reference,
                            leaderEndPoint,
                            headPoint,
                            true,
                            out leaderError))
                        {
                            string reason = "Марка " + role +
                                " создана, но вертикальная выноска не закреплена: " +
                                leaderError;
                            CreateFailureDiagnostic(
                                document,
                                view,
                                leaderEndPoint,
                                role,
                                reason);
                            AddWarning(
                                warnings,
                                "На виде \"" + view.Name + "\" " + reason);
                        }
                    }

                    _ownershipService.MarkOwned(tag, view.Id, role);
                    createdCount++;
                }
                catch (Exception exception)
                {
                    string reason = "Не удалось поставить марку " + role +
                        " по горизонтальной грани: " + exception.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        face.ReferencePoint,
                        role,
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            return createdCount;
        }

        private XYZ BuildHorizontalFaceTagAnchor(
            ViewSection view,
            HostFaceCandidate face,
            double tagBodyHalfWidth)
        {
            BoundingBoxXYZ crop = view.CropBox;
            Transform inverse = crop.Transform.Inverse;
            VisibleFaceFragment fragment = face.VisibleFragment;
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double safeMinX = cropMinX + Math.Max(0.0, tagBodyHalfWidth);
            double safeMaxX = cropMaxX - Math.Max(0.0, tagBodyHalfWidth);
            double visibleMinX = Math.Max(fragment.MinX, cropMinX);
            double visibleMaxX = Math.Min(fragment.MaxX, cropMaxX);
            double desiredX = (visibleMinX + visibleMaxX) / 2.0;

            if (face.ElementCandidate != null)
            {
                double elementCenterX = inverse.OfPoint(
                    face.ElementCandidate.Center).X;
                if (elementCenterX >= visibleMinX && elementCenterX <= visibleMaxX)
                {
                    desiredX = elementCenterX;
                }
            }

            double allowedMinX = Math.Max(visibleMinX, safeMinX);
            double allowedMaxX = Math.Min(visibleMaxX, safeMaxX);
            if (allowedMinX <= allowedMaxX)
            {
                desiredX = Math.Max(allowedMinX, Math.Min(allowedMaxX, desiredX));
            }
            else
            {
                double cropCenterX = (cropMinX + cropMaxX) / 2.0;
                desiredX = cropCenterX >= visibleMinX && cropCenterX <= visibleMaxX
                    ? cropCenterX
                    : Math.Max(visibleMinX, Math.Min(visibleMaxX, desiredX));
            }

            XYZ point;
            return TryGetPointOnFaceFragmentAtLocalX(view, face, desiredX, out point)
                ? point
                : fragment.CenterPoint;
        }

        private bool TryGetPointOnFaceFragmentAtLocalX(
            ViewSection view,
            HostFaceCandidate face,
            double localX,
            out XYZ point)
        {
            point = null;
            if (view == null || face == null || face.VisibleFragment == null)
            {
                return false;
            }

            Transform inverse = view.CropBox.Transform.Inverse;
            XYZ left = face.VisibleFragment.LeftPoint;
            XYZ right = face.VisibleFragment.RightPoint;
            XYZ leftLocal = inverse.OfPoint(left);
            XYZ rightLocal = inverse.OfPoint(right);
            double deltaX = rightLocal.X - leftLocal.X;
            XYZ estimated;
            if (Math.Abs(deltaX) <= 1e-9)
            {
                estimated = face.VisibleFragment.CenterPoint;
            }
            else
            {
                double parameter = (localX - leftLocal.X) / deltaX;
                parameter = Math.Max(0.0, Math.Min(1.0, parameter));
                estimated = left + (right - left) * parameter;
            }

            return TryProjectPointToFace(face, estimated, out point);
        }

        private string BuildElementCandidateKey(ElementCandidate candidate)
        {
            if (candidate == null || candidate.Element == null)
            {
                return string.Empty;
            }

            string linkKey = candidate.LinkInstance != null
                ? RevitElementIdUtils.GetElementIdValue(candidate.LinkInstance.Id).ToString()
                : "host";
            return linkKey + "|" + candidate.Element.UniqueId;
        }

        private int TagEveryCandidate(
            Document document,
            ViewSection view,
            IList<ElementCandidate> candidates,
            ElementId tagTypeId,
            bool hasLeader,
            TagVerticalPlacement placement,
            ElevationDecorationSettings settings,
            string role,
            IList<string> warnings)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            if (!IsValidType(document, tagTypeId))
            {
                AddWarning(warnings, "Не выбран корректный тип марки для категории " + role + ".");
                return 0;
            }

            int createdCount = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                ElementCandidate candidate = candidates[index];
                try
                {
                    XYZ preliminaryAnchor = BuildVisibleElementAnchorPoint(
                        view,
                        candidate,
                        placement);
                    TagBodyExtents tagBody = MeasureTagBodyExtents(
                        document,
                        view,
                        tagTypeId,
                        candidate.Reference,
                        preliminaryAnchor);
                    XYZ leaderEndPoint = BuildStableTagAnchorPoint(
                        view,
                        candidate,
                        placement,
                        tagBody.HalfWidth);
                    XYZ headPoint = BuildTagHeadPointFromAnchor(
                        view,
                        leaderEndPoint,
                        placement,
                        settings.TagOffsetPaperMm,
                        0.0);
                    IndependentTag tag = IndependentTag.Create(
                        document,
                        tagTypeId,
                        view.Id,
                        candidate.Reference,
                        hasLeader,
                        TagOrientation.Horizontal,
                        headPoint);
                    if (tag == null)
                    {
                        string reason = "Revit не создал марку " + role +
                            ", хотя элемент был отобран.";
                        CreateFailureDiagnostic(
                            document,
                            view,
                            candidate.Center,
                            role,
                            reason);
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                        continue;
                    }

                    tag.TagHeadPosition = headPoint;
                    if (hasLeader)
                    {
                        string leaderError;
                        if (!TryConfigureTagLeader(
                            tag,
                            view,
                            candidate.Reference,
                            leaderEndPoint,
                            headPoint,
                            true,
                            out leaderError))
                        {
                            string reason = "Марка " + role +
                                " создана, но конец выноски не закреплён на грани: " +
                                leaderError;
                            ApplyDiagnosticOverride(view, tag);
                            CreateFailureDiagnostic(
                                document,
                                view,
                                leaderEndPoint,
                                role,
                                reason);
                            AddWarning(
                                warnings,
                                "На виде \"" + view.Name + "\" " + reason);
                        }
                    }

                    _ownershipService.MarkOwned(tag, view.Id, role);
                    createdCount++;
                }
                catch (Exception exception)
                {
                    string reason = "Не удалось поставить марку " + role +
                        ": " + exception.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        candidate.Center,
                        role,
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            return createdCount;
        }

        private int TagWalls(
            Document document,
            ViewSection view,
            IList<ElementCandidate> candidates,
            ElementId tagTypeId,
            TagVerticalPlacement placement,
            ElevationDecorationSettings settings,
            string role,
            IList<string> warnings)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            if (!IsValidType(document, tagTypeId))
            {
                AddWarning(warnings, "Не выбран корректный тип марки для категории " + role + ".");
                return 0;
            }

            int createdCount = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                ElementCandidate candidate = candidates[index];
                if (IsEdgeOnWall(view, candidate))
                {
                    // Стены слева и справа задают границы горизонтального размера.
                    // Отдельные марки на их торцах не нужны.
                    continue;
                }

                try
                {
                    bool hasLeader = false;
                    XYZ headPoint = BuildTagHeadPoint(
                        view,
                        candidate,
                        placement,
                        settings.TagOffsetPaperMm);

                    IndependentTag tag = IndependentTag.Create(
                        document,
                        tagTypeId,
                        view.Id,
                        candidate.Reference,
                        hasLeader,
                        TagOrientation.Horizontal,
                        headPoint);
                    if (tag == null)
                    {
                        string reason = "Revit не создал марку фронтальной стены, " +
                            "хотя стена была отобрана.";
                        CreateFailureDiagnostic(
                            document,
                            view,
                            candidate.Center,
                            role,
                            reason);
                        AddWarning(
                            warnings,
                            "На виде \"" + view.Name + "\" " + reason);
                        continue;
                    }

                    tag.TagHeadPosition = headPoint;
                    KeepTagInsideView(
                        document,
                        view,
                        tag,
                        candidate,
                        candidate.Reference,
                        placement,
                        hasLeader);

                    _ownershipService.MarkOwned(tag, view.Id, role);
                    createdCount++;
                }
                catch (Exception exception)
                {
                    string reason = "Не удалось поставить марку фронтальной стены: " +
                        exception.Message;
                    CreateFailureDiagnostic(
                        document,
                        view,
                        candidate.Center,
                        role,
                        reason);
                    AddWarning(
                        warnings,
                        "На виде \"" + view.Name + "\" " + reason);
                }
            }

            return createdCount;
        }

        private SpotDimension CreateSpotElevation(
            Document document,
            ViewSection view,
            HostFaceCandidate target,
            ElevationDecorationSettings settings,
            double? sectionPlaneDepth,
            out bool usedFallbackReferencePoint)
        {
            usedFallbackReferencePoint = false;
            BoundingBoxXYZ cropBox = view.CropBox;
            Transform inverse = cropBox.Transform.Inverse;
            bool placeOnLeft = settings.AnnotationSide == ElevationAnnotationSide.Left;
            bool preferredBandAvailable;
            IList<XYZ> referencePoints = BuildSpotReferencePoints(
                view,
                target,
                placeOnLeft,
                out preferredBandAvailable);
            if (referencePoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "На найденной грани нет точки внутри проекции вида.");
            }

            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, settings.SpotOffsetPaperMm) * Math.Max(1, view.Scale));
            double cropEdgeX = placeOnLeft
                ? Math.Min(cropBox.Min.X, cropBox.Max.X)
                : Math.Max(cropBox.Min.X, cropBox.Max.X);
            double endX = cropEdgeX + (placeOnLeft ? -offset : offset);
            double bendX = cropEdgeX + (placeOnLeft ? -offset * 0.55 : offset * 0.55);
            Exception lastException = null;
            for (int index = 0; index < referencePoints.Count; index++)
            {
                XYZ referencePoint = referencePoints[index];
                XYZ referenceLocal = inverse.OfPoint(referencePoint);
                XYZ bend = cropBox.Transform.OfPoint(
                    new XYZ(bendX, referenceLocal.Y, referenceLocal.Z));
                XYZ end = cropBox.Transform.OfPoint(
                    new XYZ(endX, referenceLocal.Y, referenceLocal.Z));
                SubTransaction attempt = new SubTransaction(document);
                bool started = false;
                try
                {
                    attempt.Start();
                    started = true;
                    SpotDimension spot = document.Create.NewSpotElevation(
                        view,
                        target.Reference,
                        referencePoint,
                        bend,
                        end,
                        referencePoint,
                        true);
                    if (spot == null)
                    {
                        attempt.RollBack();
                        started = false;
                        continue;
                    }

                    attempt.Commit();
                    started = false;
                    usedFallbackReferencePoint = !preferredBandAvailable || index > 0;
                    return spot;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    if (started)
                    {
                        try
                        {
                            attempt.RollBack();
                        }
                        catch
                        {
                            // Следующая точка может оказаться допустимой для той же грани.
                        }
                    }
                }
                finally
                {
                    attempt.Dispose();
                }
            }

            throw new InvalidOperationException(
                "Revit отклонил все точки в боковой пятой части вида.",
                lastException);
        }

        private IList<XYZ> BuildSpotReferencePoints(
            ViewSection view,
            HostFaceCandidate target,
            bool placeOnLeft,
            out bool preferredBandAvailable)
        {
            preferredBandAvailable = false;
            List<XYZ> result = new List<XYZ>();
            if (view == null || target == null || target.VisibleFragment == null)
            {
                throw new InvalidOperationException(
                    "Для грани не построен видимый фрагмент в плоскости разреза.");
            }

            BoundingBoxXYZ crop = view.CropBox;
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double bandWidth = Math.Max(0.0, cropMaxX - cropMinX) / 5.0;
            VisibleFaceFragment fragment = target.VisibleFragment;
            double preferredMinX = placeOnLeft
                ? cropMinX
                : cropMaxX - bandWidth;
            double preferredMaxX = placeOnLeft
                ? cropMinX + bandWidth
                : cropMaxX;
            double preferredX = placeOnLeft
                ? cropMinX + bandWidth / 2.0
                : cropMaxX - bandWidth / 2.0;
            AddSpotPointFromBand(
                view,
                target,
                preferredMinX,
                preferredMaxX,
                preferredX,
                result,
                out preferredBandAvailable);

            if (!preferredBandAvailable)
            {
                double oppositeMinX = placeOnLeft
                    ? cropMaxX - bandWidth
                    : cropMinX;
                double oppositeMaxX = placeOnLeft
                    ? cropMaxX
                    : cropMinX + bandWidth;
                double oppositeX = placeOnLeft
                    ? cropMaxX - bandWidth / 2.0
                    : cropMinX + bandWidth / 2.0;
                bool ignored;
                AddSpotPointFromBand(
                    view,
                    target,
                    oppositeMinX,
                    oppositeMaxX,
                    oppositeX,
                    result,
                    out ignored);
            }

            AddUniquePoint(result, fragment.CenterPoint, UnitConversionUtils.MillimetersToFeet(1.0));
            AddUniquePoint(
                result,
                placeOnLeft ? fragment.LeftPoint : fragment.RightPoint,
                UnitConversionUtils.MillimetersToFeet(1.0));
            return result;
        }

        private void AddSpotPointFromBand(
            ViewSection view,
            HostFaceCandidate target,
            double bandMinX,
            double bandMaxX,
            double preferredX,
            IList<XYZ> points,
            out bool bandAvailable)
        {
            bandAvailable = false;
            VisibleFaceFragment fragment = target.VisibleFragment;
            double overlapMinX = Math.Max(fragment.MinX, bandMinX);
            double overlapMaxX = Math.Min(fragment.MaxX, bandMaxX);
            if (overlapMinX > overlapMaxX)
            {
                return;
            }

            double localX = Math.Max(overlapMinX, Math.Min(overlapMaxX, preferredX));
            XYZ point;
            if (!TryGetPointOnFaceFragmentAtLocalX(view, target, localX, out point))
            {
                return;
            }

            AddUniquePoint(
                points,
                point,
                UnitConversionUtils.MillimetersToFeet(1.0));
            bandAvailable = true;
        }

        private bool TryFindFacePointNearViewEdge(
            ViewSection view,
            HostFaceCandidate target,
            double? sectionPlaneDepth,
            double cropEdgeX,
            out XYZ result)
        {
            result = null;
            if (view == null || target == null || target.Face == null)
            {
                return false;
            }

            try
            {
                Mesh mesh = target.Face.Triangulate();
                if (mesh == null || mesh.NumTriangles == 0)
                {
                    return false;
                }

                Transform sourceToHost = target.SourceToHostTransform ?? Transform.Identity;
                Transform cropInverse = view.CropBox.Transform.Inverse;
                double depthTolerance = UnitConversionUtils.MillimetersToFeet(
                    SectionPlaneToleranceMm);
                List<XYZ> candidates = new List<XYZ>();

                for (int triangleIndex = 0; triangleIndex < mesh.NumTriangles; triangleIndex++)
                {
                    MeshTriangle triangle = mesh.get_Triangle(triangleIndex);
                    XYZ[] vertices = new XYZ[3];
                    for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                    {
                        vertices[vertexIndex] = sourceToHost.OfPoint(
                            triangle.get_Vertex(vertexIndex));
                    }

                    if (!sectionPlaneDepth.HasValue)
                    {
                        candidates.AddRange(vertices);
                        continue;
                    }

                    for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
                    {
                        XYZ first = vertices[edgeIndex];
                        XYZ second = vertices[(edgeIndex + 1) % 3];
                        double firstDepth = GetSignedViewDepth(view, first);
                        double secondDepth = GetSignedViewDepth(view, second);
                        double firstDelta = firstDepth - sectionPlaneDepth.Value;
                        double secondDelta = secondDepth - sectionPlaneDepth.Value;

                        if (Math.Abs(firstDelta) <= depthTolerance)
                        {
                            candidates.Add(first);
                        }

                        if (firstDelta * secondDelta > 0.0 ||
                            Math.Abs(secondDepth - firstDepth) <= 1e-9)
                        {
                            continue;
                        }

                        double parameter =
                            (sectionPlaneDepth.Value - firstDepth) /
                            (secondDepth - firstDepth);
                        if (parameter >= 0.0 && parameter <= 1.0)
                        {
                            candidates.Add(first + (second - first) * parameter);
                        }
                    }
                }

                XYZ closest = candidates
                    .Where(point => IsPointInsideCropProjection(view, point))
                    .OrderBy(point => Math.Abs(cropInverse.OfPoint(point).X - cropEdgeX))
                    .FirstOrDefault();
                if (closest == null)
                {
                    return false;
                }

                result = closest;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private XYZ BuildTagHeadPoint(
            ViewSection view,
            ElementCandidate candidate,
            TagVerticalPlacement placement,
            double paperOffsetMm,
            bool alignToViewCenter = false,
            double tagBodyHalfHeight = 0.0)
        {
            XYZ center = BuildVisibleElementAnchorPoint(
                view,
                candidate,
                placement);
            BoundingBoxXYZ crop = view.CropBox;
            Transform cropInverse = crop.Transform.Inverse;
            XYZ centerLocal = cropInverse.OfPoint(center);
            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, paperOffsetMm) * Math.Max(1, view.Scale));

            if (placement == TagVerticalPlacement.Above)
            {
                centerLocal = new XYZ(
                    centerLocal.X,
                    centerLocal.Y + tagBodyHalfHeight + offset,
                    centerLocal.Z);
            }
            else if (placement == TagVerticalPlacement.Below)
            {
                centerLocal = new XYZ(
                    centerLocal.X,
                    centerLocal.Y - tagBodyHalfHeight - offset,
                    centerLocal.Z);
            }

            center = crop.Transform.OfPoint(centerLocal);

            return alignToViewCenter
                ? MovePointToViewCenterLine(view, center)
                : center;
        }

        private TagBodyExtents MeasureTagBodyExtents(
            Document document,
            ViewSection view,
            ElementId tagTypeId,
            Reference reference,
            XYZ previewHeadPoint)
        {
            TagBodyExtents result = new TagBodyExtents();
            if (document == null || view == null || reference == null ||
                previewHeadPoint == null)
            {
                return result;
            }

            SubTransaction previewTransaction = new SubTransaction(document);
            bool started = false;
            try
            {
                previewTransaction.Start();
                started = true;
                IndependentTag previewTag = IndependentTag.Create(
                    document,
                    tagTypeId,
                    view.Id,
                    reference,
                    false,
                    TagOrientation.Horizontal,
                    previewHeadPoint);
                if (previewTag == null)
                {
                    return result;
                }

                document.Regenerate();
                BoundingBoxXYZ bounds = previewTag.get_BoundingBox(view);
                if (bounds == null)
                {
                    return result;
                }

                Transform cropInverse = view.CropBox.Transform.Inverse;
                IList<XYZ> localCorners = GetCorners(bounds)
                    .Select(cropInverse.OfPoint)
                    .ToList();
                result.HalfWidth = Math.Max(
                    0.0,
                    (localCorners.Max(point => point.X) -
                     localCorners.Min(point => point.X)) / 2.0);
                result.HalfHeight = Math.Max(
                    0.0,
                    (localCorners.Max(point => point.Y) -
                     localCorners.Min(point => point.Y)) / 2.0);
                return result;
            }
            catch
            {
                return result;
            }
            finally
            {
                if (started)
                {
                    try
                    {
                        previewTransaction.RollBack();
                    }
                    catch
                    {
                        // Предварительная марка существует только для замера.
                    }
                }

                previewTransaction.Dispose();
            }
        }

        private XYZ BuildStableTagAnchorPoint(
            ViewSection view,
            ElementCandidate candidate,
            TagVerticalPlacement placement,
            double tagBodyHalfWidth)
        {
            XYZ surfaceAnchor = BuildVisibleElementAnchorPoint(
                view,
                candidate,
                placement);
            BoundingBoxXYZ crop = view.CropBox;
            Transform inverse = crop.Transform.Inverse;
            XYZ surfaceLocal = inverse.OfPoint(surfaceAnchor);
            XYZ elementCenterLocal = inverse.OfPoint(candidate.Center);
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double safeMinX = cropMinX + Math.Max(0.0, tagBodyHalfWidth);
            double safeMaxX = cropMaxX - Math.Max(0.0, tagBodyHalfWidth);
            double elementCenterX = elementCenterLocal.X;
            bool elementCenterFits = safeMinX <= safeMaxX &&
                elementCenterX >= safeMinX &&
                elementCenterX <= safeMaxX;
            double anchorX = elementCenterFits
                ? elementCenterX
                : (cropMinX + cropMaxX) / 2.0;

            return crop.Transform.OfPoint(
                new XYZ(anchorX, surfaceLocal.Y, surfaceLocal.Z));
        }

        private XYZ BuildTagHeadPointFromAnchor(
            ViewSection view,
            XYZ anchorPoint,
            TagVerticalPlacement placement,
            double paperOffsetMm,
            double tagBodyHalfHeight)
        {
            BoundingBoxXYZ crop = view.CropBox;
            Transform inverse = crop.Transform.Inverse;
            XYZ local = inverse.OfPoint(anchorPoint);
            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, paperOffsetMm) * Math.Max(1, view.Scale));
            double verticalOffset = Math.Max(0.0, tagBodyHalfHeight) + offset;
            if (placement == TagVerticalPlacement.Above)
            {
                local = new XYZ(local.X, local.Y + verticalOffset, local.Z);
            }
            else if (placement == TagVerticalPlacement.Below)
            {
                local = new XYZ(local.X, local.Y - verticalOffset, local.Z);
            }

            return crop.Transform.OfPoint(local);
        }

        private XYZ BuildTagAnchorPoint(
            ViewSection view,
            ElementCandidate candidate,
            TagVerticalPlacement placement,
            bool alignToViewCenter)
        {
            if (candidate.VisibleAnchorPoint != null)
            {
                return alignToViewCenter
                    ? MovePointToViewCenterLine(view, candidate.VisibleAnchorPoint)
                    : candidate.VisibleAnchorPoint;
            }

            BoundingBoxXYZ bounds = candidate.Bounds;
            XYZ anchorPoint = candidate.Center;
            if (placement == TagVerticalPlacement.Above)
            {
                anchorPoint = new XYZ(anchorPoint.X, anchorPoint.Y, bounds.Max.Z);
            }
            else if (placement == TagVerticalPlacement.Below)
            {
                anchorPoint = new XYZ(anchorPoint.X, anchorPoint.Y, bounds.Min.Z);
            }

            return alignToViewCenter
                ? MovePointToViewCenterLine(view, anchorPoint)
                : anchorPoint;
        }

        private XYZ MovePointToViewCenterLine(ViewSection view, XYZ point)
        {
            BoundingBoxXYZ crop = view.CropBox;
            Transform inverse = crop.Transform.Inverse;
            XYZ local = inverse.OfPoint(point);
            double cropCenterX = (crop.Min.X + crop.Max.X) / 2.0;
            return crop.Transform.OfPoint(new XYZ(cropCenterX, local.Y, local.Z));
        }

        private void ConfigureTagLeader(
            IndependentTag tag,
            ViewSection view,
            Reference reference,
            XYZ leaderEndPoint,
            XYZ headPoint,
            bool forceVertical)
        {
            string ignoredError;
            TryConfigureTagLeader(
                tag,
                view,
                reference,
                leaderEndPoint,
                headPoint,
                forceVertical,
                out ignoredError);
        }

        private bool TryConfigureTagLeader(
            IndependentTag tag,
            ViewSection view,
            Reference reference,
            XYZ leaderEndPoint,
            XYZ headPoint,
            bool forceVertical,
            out string error)
        {
            error = string.Empty;
            if (tag == null)
            {
                error = "марка не создана";
                return false;
            }

            Reference actualReference = GetActualTaggedReference(tag, reference);
            if (actualReference == null)
            {
                error = "Revit не вернул ссылку промаркированного элемента";
                return false;
            }

            try
            {
                tag.HasLeader = true;
            }
            catch (Exception exception)
            {
                error = "семейство не разрешает включить выноску: " +
                    exception.Message;
                return false;
            }

            try
            {
                System.Reflection.MethodInfo visibilityMethod = tag.GetType().GetMethod(
                    "SetIsLeaderVisible",
                    new[] { typeof(Reference), typeof(bool) });
                if (visibilityMethod != null)
                {
                    visibilityMethod.Invoke(tag, new object[] { actualReference, true });
                }
            }
            catch
            {
                // Метод отсутствует в отдельных версиях Revit или недоступен типу марки.
            }

            try
            {
                tag.LeaderEndCondition = LeaderEndCondition.Free;
            }
            catch (Exception exception)
            {
                error = "тип марки не поддерживает свободный конец выноски: " +
                    exception.Message;
                return false;
            }

            XYZ actualHeadPoint = tag.TagHeadPosition ?? headPoint;
            if (forceVertical && view != null)
            {
                BoundingBoxXYZ crop = view.CropBox;
                Transform cropInverse = crop.Transform.Inverse;
                XYZ headLocal = cropInverse.OfPoint(actualHeadPoint);
                XYZ endLocal = cropInverse.OfPoint(leaderEndPoint);
                leaderEndPoint = crop.Transform.OfPoint(
                    new XYZ(headLocal.X, endLocal.Y, headLocal.Z));
            }

            try
            {
                tag.SetLeaderEnd(actualReference, leaderEndPoint);
            }
            catch (Exception exception)
            {
                error = "Revit отклонил рассчитанную точку конца выноски: " +
                    exception.Message;
                return false;
            }

            try
            {
                XYZ elbowPoint = new XYZ(
                    (leaderEndPoint.X + actualHeadPoint.X) / 2.0,
                    (leaderEndPoint.Y + actualHeadPoint.Y) / 2.0,
                    (leaderEndPoint.Z + actualHeadPoint.Z) / 2.0);
                tag.SetLeaderElbow(actualReference, elbowPoint);
            }
            catch
            {
                // Прямая выноска допустима, если семейство не поддерживает локоть.
            }

            try
            {
                XYZ actualEndPoint = tag.GetLeaderEnd(actualReference);
                if (actualEndPoint != null && view != null)
                {
                    Transform inverse = view.CropBox.Transform.Inverse;
                    XYZ expectedLocal = inverse.OfPoint(leaderEndPoint);
                    XYZ actualLocal = inverse.OfPoint(actualEndPoint);
                    double deviation = Math.Sqrt(
                        Math.Pow(expectedLocal.X - actualLocal.X, 2.0) +
                        Math.Pow(expectedLocal.Y - actualLocal.Y, 2.0));
                    if (deviation > UnitConversionUtils.MillimetersToFeet(2.0))
                    {
                        error = "Revit сместил конец выноски от рассчитанной точки на " +
                            Math.Round(deviation * 304.8, 1) + " мм";
                        return false;
                    }
                }
            }
            catch
            {
                // Старые версии Revit не всегда возвращают положение конца,
                // однако успешный SetLeaderEnd уже подтверждает установку.
            }

            return true;
        }

        private Reference GetActualTaggedReference(
            IndependentTag tag,
            Reference fallbackReference)
        {
            if (tag == null)
            {
                return fallbackReference;
            }

            try
            {
                System.Reflection.MethodInfo method = tag.GetType().GetMethod(
                    "GetTaggedReferences",
                    Type.EmptyTypes);
                System.Collections.IEnumerable references = method != null
                    ? method.Invoke(tag, null) as System.Collections.IEnumerable
                    : null;
                if (references != null)
                {
                    foreach (object item in references)
                    {
                        Reference actualReference = item as Reference;
                        if (actualReference != null)
                        {
                            return actualReference;
                        }
                    }
                }
            }
            catch
            {
                // В Revit без multi-leader API используем исходную ссылку.
            }

            return fallbackReference;
        }

        private void KeepTagInsideView(
            Document document,
            ViewSection view,
            IndependentTag tag,
            ElementCandidate candidate,
            Reference reference,
            TagVerticalPlacement placement,
            bool hasLeader)
        {
            if (document == null || view == null || tag == null || candidate == null)
            {
                return;
            }

            XYZ originalHeadPoint = tag.TagHeadPosition;
            XYZ originalLeaderEndPoint = BuildTagAnchorPoint(
                view,
                candidate,
                placement,
                false);
            bool leaderTemporarilyHidden = false;
            if (hasLeader)
            {
                try
                {
                    tag.HasLeader = false;
                    leaderTemporarilyHidden = true;
                }
                catch
                {
                    // Для отдельных семейств свойство может быть недоступно.
                    // Тогда проверяем общий габарит марки вместе с выноской.
                }
            }

            document.Regenerate();
            BoundingBoxXYZ tagBounds = tag.get_BoundingBox(view);
            if (leaderTemporarilyHidden)
            {
                tag.HasLeader = true;
            }

            if (tagBounds == null)
            {
                if (leaderTemporarilyHidden)
                {
                    ConfigureTagLeader(
                        tag,
                        view,
                        reference,
                        originalLeaderEndPoint,
                        originalHeadPoint,
                        false);
                }

                return;
            }

            BoundingBoxXYZ crop = view.CropBox;
            Transform cropInverse = crop.Transform.Inverse;
            IList<XYZ> tagLocalCorners = GetCorners(tagBounds)
                .Select(cropInverse.OfPoint)
                .ToList();
            double tolerance = UnitConversionUtils.MillimetersToFeet(0.5);
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            bool outsideHorizontally =
                tagLocalCorners.Min(point => point.X) < cropMinX - tolerance ||
                tagLocalCorners.Max(point => point.X) > cropMaxX + tolerance;
            if (!outsideHorizontally)
            {
                if (leaderTemporarilyHidden)
                {
                    ConfigureTagLeader(
                        tag,
                        view,
                        reference,
                        originalLeaderEndPoint,
                        originalHeadPoint,
                        false);
                }

                return;
            }

            XYZ centeredHeadPoint = MovePointToViewCenterLine(view, originalHeadPoint);
            tag.TagHeadPosition = centeredHeadPoint;
            if (!hasLeader)
            {
                return;
            }

            XYZ visibleLeaderEndPoint = BuildVisibleElementAnchorPoint(
                view,
                candidate,
                placement);
            ConfigureTagLeader(
                tag,
                view,
                reference,
                visibleLeaderEndPoint,
                centeredHeadPoint,
                false);
        }

        private XYZ BuildVisibleElementAnchorPoint(
            ViewSection view,
            ElementCandidate candidate,
            TagVerticalPlacement placement)
        {
            if (candidate.VisibleAnchorPoint != null)
            {
                return candidate.VisibleAnchorPoint;
            }

            XYZ anchorPoint = BuildTagAnchorPoint(view, candidate, placement, false);
            BoundingBoxXYZ crop = view.CropBox;
            Transform cropInverse = crop.Transform.Inverse;
            XYZ anchorLocal = cropInverse.OfPoint(anchorPoint);
            IList<XYZ> candidateLocalCorners = GetCorners(candidate.Bounds)
                .Select(cropInverse.OfPoint)
                .ToList();

            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double candidateMinX = candidateLocalCorners.Min(point => point.X);
            double candidateMaxX = candidateLocalCorners.Max(point => point.X);
            double visibleMinX = Math.Max(cropMinX, candidateMinX);
            double visibleMaxX = Math.Min(cropMaxX, candidateMaxX);
            double visibleCenterX = visibleMinX <= visibleMaxX
                ? (visibleMinX + visibleMaxX) / 2.0
                : (cropMinX + cropMaxX) / 2.0;

            return crop.Transform.OfPoint(
                new XYZ(visibleCenterX, anchorLocal.Y, anchorLocal.Z));
        }

        private XYZ BuildEdgeWallTagHeadPoint(
            ViewSection view,
            ElementCandidate candidate,
            double paperOffsetMm)
        {
            XYZ wallDirection;
            if (!TryGetWallDirection(candidate, out wallDirection))
            {
                return BuildTagHeadPoint(
                    view,
                    candidate,
                    TagVerticalPlacement.Center,
                    paperOffsetMm);
            }

            XYZ normal = new XYZ(-wallDirection.Y, wallDirection.X, 0.0);
            if (normal.GetLength() <= 1e-9)
            {
                return candidate.Center;
            }

            normal = normal.Normalize();
            BoundingBoxXYZ crop = view.CropBox;
            XYZ cropCenter = crop.Transform.OfPoint(new XYZ(
                (crop.Min.X + crop.Max.X) / 2.0,
                (crop.Min.Y + crop.Max.Y) / 2.0,
                (crop.Min.Z + crop.Max.Z) / 2.0));
            XYZ towardVisibleArea = cropCenter - candidate.Center;
            // Для боковой отделки головка марки должна оставаться снаружи:
            // слева от левой грани и справа от правой. Выноска при этом
            // подходит к широкой плоскости стены строго по ее нормали.
            if (normal.DotProduct(towardVisibleArea) > 0.0)
            {
                normal = -normal;
            }

            double offset = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, paperOffsetMm) * Math.Max(1, view.Scale));
            double halfWidth = Math.Min(
                GetWallWidth(candidate) / 2.0,
                UnitConversionUtils.MillimetersToFeet(500.0));
            return candidate.Center + normal * (halfWidth + offset);
        }

        private bool IsEdgeOnWall(ViewSection view, ElementCandidate candidate)
        {
            XYZ wallDirection;
            if (view == null || !TryGetWallDirection(candidate, out wallDirection))
            {
                return false;
            }

            XYZ horizontalViewDirection = new XYZ(
                view.ViewDirection.X,
                view.ViewDirection.Y,
                0.0);
            if (horizontalViewDirection.GetLength() <= 1e-9)
            {
                return false;
            }

            horizontalViewDirection = horizontalViewDirection.Normalize();
            double directionDot = Math.Abs(wallDirection.DotProduct(horizontalViewDirection));
            return directionDot > BroadWallMaximumDirectionDot;
        }

        private bool IsFrontFacingOpening(
            ViewSection view,
            ElementCandidate candidate)
        {
            FamilyInstance instance = candidate != null
                ? candidate.Element as FamilyInstance
                : null;
            if (view == null || instance == null)
            {
                return false;
            }

            XYZ viewDirection = new XYZ(
                view.ViewDirection.X,
                view.ViewDirection.Y,
                0.0);
            if (viewDirection.GetLength() <= 1e-9)
            {
                return false;
            }

            viewDirection = viewDirection.Normalize();
            Wall hostWall = instance.Host as Wall;
            LocationCurve hostLocation = hostWall != null
                ? hostWall.Location as LocationCurve
                : null;
            Curve hostCurve = hostLocation != null ? hostLocation.Curve : null;
            if (hostCurve != null)
            {
                XYZ hostDirection = hostCurve.GetEndPoint(1) - hostCurve.GetEndPoint(0);
                hostDirection = candidate.SourceToHostTransform != null
                    ? candidate.SourceToHostTransform.OfVector(hostDirection)
                    : hostDirection;
                hostDirection = new XYZ(hostDirection.X, hostDirection.Y, 0.0);
                if (hostDirection.GetLength() > 1e-9)
                {
                    return Math.Abs(
                        hostDirection.Normalize().DotProduct(viewDirection)) <=
                        BroadWallMaximumDirectionDot;
                }
            }

            try
            {
                XYZ facing = instance.FacingOrientation;
                facing = candidate.SourceToHostTransform != null
                    ? candidate.SourceToHostTransform.OfVector(facing)
                    : facing;
                facing = new XYZ(facing.X, facing.Y, 0.0);
                if (facing.GetLength() > 1e-9)
                {
                    return Math.Abs(
                        facing.Normalize().DotProduct(viewDirection)) >= 0.75;
                }
            }
            catch
            {
                // Для нестандартного семейства используем ориентацию стены-основы.
            }

            return false;
        }

        private bool TryGetWallDirection(ElementCandidate candidate, out XYZ direction)
        {
            direction = XYZ.Zero;
            Wall wall = candidate != null ? candidate.Element as Wall : null;
            LocationCurve location = wall != null ? wall.Location as LocationCurve : null;
            Curve curve = location != null ? location.Curve : null;
            if (curve == null)
            {
                return false;
            }

            XYZ rawDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
            rawDirection = candidate.SourceToHostTransform != null
                ? candidate.SourceToHostTransform.OfVector(rawDirection)
                : rawDirection;
            direction = new XYZ(rawDirection.X, rawDirection.Y, 0.0);
            if (direction.GetLength() <= 1e-9)
            {
                direction = XYZ.Zero;
                return false;
            }

            direction = direction.Normalize();
            return true;
        }

        private bool TryCreateViewGeometryContext(
            ViewSection view,
            out ElevationViewGeometryContext context,
            out string error)
        {
            context = null;
            error = string.Empty;
            if (view == null)
            {
                error = "вид не задан";
                return false;
            }

            XYZ origin = view.Origin;
            XYZ viewDirection = view.ViewDirection;
            XYZ rightDirection = view.RightDirection;
            XYZ upDirection = view.UpDirection;
            BoundingBoxXYZ cropBox = view.CropBox;
            if (origin == null || viewDirection == null || rightDirection == null ||
                upDirection == null || cropBox == null || cropBox.Transform == null)
            {
                error = "Revit не вернул полную систему координат вида";
                return false;
            }

            if (viewDirection.GetLength() <= 1e-9 ||
                rightDirection.GetLength() <= 1e-9 ||
                upDirection.GetLength() <= 1e-9)
            {
                error = "одно из направлений вида имеет нулевую длину";
                return false;
            }

            context = new ElevationViewGeometryContext
            {
                Origin = origin,
                ViewDirection = viewDirection.Normalize(),
                RightDirection = rightDirection.Normalize(),
                UpDirection = upDirection.Normalize(),
                CropBox = cropBox,
                SectionPlaneDepth = 0.0
            };
            return true;
        }

        private List<ElementCandidate> FilterElementsBySectionPlane(
            ViewSection view,
            IList<ElementCandidate> candidates,
            double? sectionPlaneDepth)
        {
            if (candidates == null)
            {
                return new List<ElementCandidate>();
            }

            if (!sectionPlaneDepth.HasValue)
            {
                return candidates.ToList();
            }

            double tolerance = UnitConversionUtils.MillimetersToFeet(SectionPlaneToleranceMm);
            return candidates
                .Where(candidate => ElementIntersectsSectionPlane(
                    view,
                    candidate,
                    sectionPlaneDepth.Value,
                    tolerance))
                .ToList();
        }

        private List<HostFaceCandidate> FilterFacesBySectionPlane(
            ViewSection view,
            IList<HostFaceCandidate> candidates,
            double? sectionPlaneDepth)
        {
            if (candidates == null)
            {
                return new List<HostFaceCandidate>();
            }

            if (!sectionPlaneDepth.HasValue)
            {
                return candidates.ToList();
            }

            double tolerance = UnitConversionUtils.MillimetersToFeet(SectionPlaneToleranceMm);
            List<HostFaceCandidate> visibleFragments = new List<HostFaceCandidate>();
            for (int index = 0; index < candidates.Count; index++)
            {
                HostFaceCandidate candidate = candidates[index];
                VisibleFaceFragment fragment;
                if (!TryBuildVisibleFaceFragment(
                    view,
                    candidate,
                    sectionPlaneDepth.Value,
                    tolerance,
                    out fragment))
                {
                    continue;
                }

                candidate.VisibleFragment = fragment;
                candidate.ReferencePoint = fragment.CenterPoint;
                candidate.Elevation = fragment.CenterPoint.Z;
                visibleFragments.Add(candidate);
            }

            return visibleFragments;
        }

        private List<HostFaceCandidate> FilterHorizontalFacesForSpotElevations(
            ViewSection view,
            IList<HostFaceCandidate> candidates,
            double? sectionPlaneDepth,
            ElevationAnnotationSide side)
        {
            List<HostFaceCandidate> exact = FilterFacesBySectionPlane(
                view,
                candidates,
                sectionPlaneDepth);
            if (view == null || candidates == null || !sectionPlaneDepth.HasValue)
            {
                return exact;
            }

            HashSet<HostFaceCandidate> accepted = new HashSet<HostFaceCandidate>(exact);
            BoundingBoxXYZ crop = view.CropBox;
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double viewWidth = Math.Max(0.0, cropMaxX - cropMinX);
            double sideBandWidth = viewWidth / 5.0;
            double sideBandMinX = side == ElevationAnnotationSide.Left
                ? cropMinX
                : cropMaxX - sideBandWidth;
            double sideBandMaxX = side == ElevationAnnotationSide.Left
                ? cropMinX + sideBandWidth
                : cropMaxX;
            double oppositeBandMinX = side == ElevationAnnotationSide.Left
                ? cropMaxX - sideBandWidth
                : cropMinX;
            double oppositeBandMaxX = side == ElevationAnnotationSide.Left
                ? cropMaxX
                : cropMinX + sideBandWidth;
            double maximumDepthOffset = Math.Max(
                UnitConversionUtils.MillimetersToFeet(50.0),
                sideBandWidth);
            double tolerance = UnitConversionUtils.MillimetersToFeet(
                SectionPlaneToleranceMm);

            for (int index = 0; index < candidates.Count; index++)
            {
                HostFaceCandidate candidate = candidates[index];
                if (accepted.Contains(candidate))
                {
                    continue;
                }

                VisibleFaceFragment fragment;
                bool fragmentFound = TryBuildNearestHorizontalFaceFragment(
                    view,
                    candidate,
                    sectionPlaneDepth.Value,
                    maximumDepthOffset,
                    tolerance,
                    sideBandMinX,
                    sideBandMaxX,
                    out fragment);
                if (!fragmentFound)
                {
                    fragmentFound = TryBuildNearestHorizontalFaceFragment(
                        view,
                        candidate,
                        sectionPlaneDepth.Value,
                        maximumDepthOffset,
                        tolerance,
                        oppositeBandMinX,
                        oppositeBandMaxX,
                        out fragment);
                }

                if (!fragmentFound)
                {
                    continue;
                }

                candidate.VisibleFragment = fragment;
                candidate.ReferencePoint = fragment.CenterPoint;
                candidate.Elevation = fragment.CenterPoint.Z;
                exact.Add(candidate);
            }

            return exact;
        }

        private bool TryBuildNearestHorizontalFaceFragment(
            ViewSection view,
            HostFaceCandidate candidate,
            double sectionPlaneDepth,
            double maximumDepthOffset,
            double tolerance,
            double sideBandMinX,
            double sideBandMaxX,
            out VisibleFaceFragment fragment)
        {
            fragment = null;
            if (view == null || candidate == null || candidate.Face == null)
            {
                return false;
            }

            try
            {
                Mesh mesh = candidate.Face.Triangulate();
                if (mesh == null || mesh.NumTriangles == 0)
                {
                    return false;
                }

                Transform transform = candidate.SourceToHostTransform ?? Transform.Identity;
                Transform cropInverse = view.CropBox.Transform.Inverse;
                double cropMinY = Math.Min(view.CropBox.Min.Y, view.CropBox.Max.Y);
                double cropMaxY = Math.Max(view.CropBox.Min.Y, view.CropBox.Max.Y);
                List<double> depths = new List<double>();
                for (int triangleIndex = 0; triangleIndex < mesh.NumTriangles; triangleIndex++)
                {
                    MeshTriangle triangle = mesh.get_Triangle(triangleIndex);
                    List<XYZ> vertices = new List<XYZ>();
                    for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                    {
                        vertices.Add(transform.OfPoint(triangle.get_Vertex(vertexIndex)));
                    }

                    IList<XYZ> local = vertices.Select(cropInverse.OfPoint).ToList();
                    if (!IntervalsOverlap(
                            local.Min(point => point.X),
                            local.Max(point => point.X),
                            sideBandMinX,
                            sideBandMaxX) ||
                        !IntervalsOverlap(
                            local.Min(point => point.Y),
                            local.Max(point => point.Y),
                            cropMinY,
                            cropMaxY))
                    {
                        continue;
                    }

                    depths.AddRange(vertices.Select(point =>
                        GetSignedViewDepth(view, point)));
                }

                if (depths.Count == 0)
                {
                    return false;
                }

                double minimumDepth = depths.Min();
                double maximumDepth = depths.Max();
                double probeDepth;
                if (sectionPlaneDepth < minimumDepth)
                {
                    probeDepth = minimumDepth + tolerance;
                }
                else if (sectionPlaneDepth > maximumDepth)
                {
                    probeDepth = maximumDepth - tolerance;
                }
                else
                {
                    return false;
                }

                if (Math.Abs(probeDepth - sectionPlaneDepth) > maximumDepthOffset)
                {
                    return false;
                }

                if (!TryBuildVisibleFaceFragment(
                    view,
                    candidate,
                    probeDepth,
                    tolerance,
                    out fragment))
                {
                    return false;
                }

                return IntervalsOverlap(
                    fragment.MinX,
                    fragment.MaxX,
                    sideBandMinX,
                    sideBandMaxX);
            }
            catch
            {
                return false;
            }
        }

        private bool TryBuildVisibleFaceFragment(
            ViewSection view,
            HostFaceCandidate candidate,
            double sectionPlaneDepth,
            double tolerance,
            out VisibleFaceFragment fragment)
        {
            fragment = null;
            if (view == null || candidate == null || candidate.Face == null)
            {
                return false;
            }

            List<VisibleFaceSegment> segments = CollectVisibleFaceSegments(
                view,
                candidate.Face,
                candidate.SourceToHostTransform,
                sectionPlaneDepth,
                tolerance);
            if (segments.Count == 0)
            {
                return false;
            }

            double mergeTolerance = UnitConversionUtils.MillimetersToFeet(1.0);
            List<VisibleFaceInterval> intervals = segments
                .OrderBy(segment => segment.MinX)
                .Select(segment => new VisibleFaceInterval
                {
                    MinX = segment.MinX,
                    MaxX = segment.MaxX,
                    Segments = new List<VisibleFaceSegment> { segment }
                })
                .ToList();
            List<VisibleFaceInterval> merged = new List<VisibleFaceInterval>();
            for (int index = 0; index < intervals.Count; index++)
            {
                VisibleFaceInterval current = intervals[index];
                VisibleFaceInterval previous = merged.LastOrDefault();
                if (previous == null || current.MinX > previous.MaxX + mergeTolerance)
                {
                    merged.Add(current);
                    continue;
                }

                previous.MaxX = Math.Max(previous.MaxX, current.MaxX);
                previous.Segments.AddRange(current.Segments);
            }

            VisibleFaceInterval selected = merged
                .OrderByDescending(interval => interval.MaxX - interval.MinX)
                .ThenBy(interval => interval.MinX)
                .FirstOrDefault();
            if (selected == null || selected.MaxX - selected.MinX <= 1e-9)
            {
                return false;
            }

            XYZ leftPoint;
            XYZ centerPoint;
            XYZ rightPoint;
            if (!TryGetPointOnVisibleSegments(selected.Segments, selected.MinX, out leftPoint) ||
                !TryGetPointOnVisibleSegments(
                    selected.Segments,
                    (selected.MinX + selected.MaxX) / 2.0,
                    out centerPoint) ||
                !TryGetPointOnVisibleSegments(selected.Segments, selected.MaxX, out rightPoint))
            {
                return false;
            }

            if (!TryProjectPointToFace(candidate, leftPoint, out leftPoint) ||
                !TryProjectPointToFace(candidate, centerPoint, out centerPoint) ||
                !TryProjectPointToFace(candidate, rightPoint, out rightPoint))
            {
                return false;
            }

            fragment = new VisibleFaceFragment
            {
                MinX = selected.MinX,
                MaxX = selected.MaxX,
                LeftPoint = leftPoint,
                CenterPoint = centerPoint,
                RightPoint = rightPoint
            };
            return true;
        }

        private bool TryProjectPointToFace(
            HostFaceCandidate candidate,
            XYZ hostPoint,
            out XYZ projectedHostPoint)
        {
            projectedHostPoint = null;
            if (candidate == null || candidate.Face == null || hostPoint == null)
            {
                return false;
            }

            try
            {
                Transform sourceToHost = candidate.SourceToHostTransform ??
                    Transform.Identity;
                XYZ sourcePoint = sourceToHost.Inverse.OfPoint(hostPoint);
                IntersectionResult projection = candidate.Face.Project(sourcePoint);
                if (projection == null || projection.XYZPoint == null)
                {
                    return false;
                }

                projectedHostPoint = sourceToHost.OfPoint(projection.XYZPoint);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<VisibleFaceSegment> CollectVisibleFaceSegments(
            ViewSection view,
            Face face,
            Transform sourceToHost,
            double sectionPlaneDepth,
            double tolerance)
        {
            List<VisibleFaceSegment> result = new List<VisibleFaceSegment>();
            Mesh mesh;
            try
            {
                mesh = face.Triangulate();
            }
            catch
            {
                return result;
            }

            if (mesh == null || mesh.NumTriangles == 0)
            {
                return result;
            }

            Transform transform = sourceToHost ?? Transform.Identity;
            BoundingBoxXYZ crop = view.CropBox;
            Transform cropInverse = crop.Transform.Inverse;
            double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
            double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
            double cropMinY = Math.Min(crop.Min.Y, crop.Max.Y);
            double cropMaxY = Math.Max(crop.Min.Y, crop.Max.Y);

            for (int triangleIndex = 0; triangleIndex < mesh.NumTriangles; triangleIndex++)
            {
                MeshTriangle triangle = mesh.get_Triangle(triangleIndex);
                XYZ[] vertices = new XYZ[3];
                for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                {
                    vertices[vertexIndex] = transform.OfPoint(
                        triangle.get_Vertex(vertexIndex));
                }

                List<XYZ> intersections = new List<XYZ>();
                for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
                {
                    XYZ first = vertices[edgeIndex];
                    XYZ second = vertices[(edgeIndex + 1) % 3];
                    double firstDelta = GetSignedViewDepth(view, first) - sectionPlaneDepth;
                    double secondDelta = GetSignedViewDepth(view, second) - sectionPlaneDepth;

                    if (Math.Abs(firstDelta) <= tolerance)
                    {
                        AddUniquePoint(intersections, first, tolerance);
                    }

                    if (Math.Abs(secondDelta) <= tolerance)
                    {
                        AddUniquePoint(intersections, second, tolerance);
                    }

                    if (firstDelta * secondDelta >= 0.0 ||
                        Math.Abs(secondDelta - firstDelta) <= 1e-12)
                    {
                        continue;
                    }

                    double parameter = -firstDelta / (secondDelta - firstDelta);
                    if (parameter >= 0.0 && parameter <= 1.0)
                    {
                        AddUniquePoint(
                            intersections,
                            first + (second - first) * parameter,
                            tolerance);
                    }
                }

                if (intersections.Count < 2)
                {
                    continue;
                }

                XYZ firstPoint = null;
                XYZ secondPoint = null;
                double maximumDistance = 0.0;
                for (int firstIndex = 0; firstIndex < intersections.Count; firstIndex++)
                {
                    for (int secondIndex = firstIndex + 1;
                        secondIndex < intersections.Count;
                        secondIndex++)
                    {
                        double distance = intersections[firstIndex].DistanceTo(
                            intersections[secondIndex]);
                        if (distance <= maximumDistance)
                        {
                            continue;
                        }

                        maximumDistance = distance;
                        firstPoint = intersections[firstIndex];
                        secondPoint = intersections[secondIndex];
                    }
                }

                XYZ clippedStart;
                XYZ clippedEnd;
                if (firstPoint == null || secondPoint == null ||
                    !TryClipSegmentToCrop(
                        cropInverse,
                        firstPoint,
                        secondPoint,
                        cropMinX,
                        cropMaxX,
                        cropMinY,
                        cropMaxY,
                        out clippedStart,
                        out clippedEnd))
                {
                    continue;
                }

                XYZ startLocal = cropInverse.OfPoint(clippedStart);
                XYZ endLocal = cropInverse.OfPoint(clippedEnd);
                if (Math.Abs(endLocal.X - startLocal.X) <= 1e-9)
                {
                    continue;
                }

                result.Add(new VisibleFaceSegment
                {
                    StartPoint = clippedStart,
                    EndPoint = clippedEnd,
                    StartLocal = startLocal,
                    EndLocal = endLocal,
                    MinX = Math.Min(startLocal.X, endLocal.X),
                    MaxX = Math.Max(startLocal.X, endLocal.X)
                });
            }

            return result;
        }

        private void AddUniquePoint(IList<XYZ> points, XYZ point, double tolerance)
        {
            if (point == null || points.Any(existing => existing.DistanceTo(point) <= tolerance))
            {
                return;
            }

            points.Add(point);
        }

        private bool TryClipSegmentToCrop(
            Transform cropInverse,
            XYZ startPoint,
            XYZ endPoint,
            double minX,
            double maxX,
            double minY,
            double maxY,
            out XYZ clippedStart,
            out XYZ clippedEnd)
        {
            clippedStart = null;
            clippedEnd = null;
            XYZ startLocal = cropInverse.OfPoint(startPoint);
            XYZ endLocal = cropInverse.OfPoint(endPoint);
            XYZ delta = endLocal - startLocal;
            double startParameter = 0.0;
            double endParameter = 1.0;

            if (!ClipParameter(-delta.X, startLocal.X - minX, ref startParameter, ref endParameter) ||
                !ClipParameter(delta.X, maxX - startLocal.X, ref startParameter, ref endParameter) ||
                !ClipParameter(-delta.Y, startLocal.Y - minY, ref startParameter, ref endParameter) ||
                !ClipParameter(delta.Y, maxY - startLocal.Y, ref startParameter, ref endParameter) ||
                endParameter < startParameter)
            {
                return false;
            }

            clippedStart = startPoint + (endPoint - startPoint) * startParameter;
            clippedEnd = startPoint + (endPoint - startPoint) * endParameter;
            return clippedStart.DistanceTo(clippedEnd) > 1e-9;
        }

        private bool ClipParameter(
            double direction,
            double distance,
            ref double startParameter,
            ref double endParameter)
        {
            if (Math.Abs(direction) <= 1e-12)
            {
                return distance >= 0.0;
            }

            double parameter = distance / direction;
            if (direction < 0.0)
            {
                if (parameter > endParameter)
                {
                    return false;
                }

                startParameter = Math.Max(startParameter, parameter);
            }
            else
            {
                if (parameter < startParameter)
                {
                    return false;
                }

                endParameter = Math.Min(endParameter, parameter);
            }

            return true;
        }

        private bool TryGetPointOnVisibleSegments(
            IList<VisibleFaceSegment> segments,
            double targetX,
            out XYZ point)
        {
            point = null;
            VisibleFaceSegment segment = segments
                .Where(item => targetX >= item.MinX - 1e-9 && targetX <= item.MaxX + 1e-9)
                .OrderBy(item => Math.Abs((item.MinX + item.MaxX) / 2.0 - targetX))
                .FirstOrDefault();
            if (segment == null)
            {
                segment = segments
                    .OrderBy(item => Math.Min(
                        Math.Abs(item.MinX - targetX),
                        Math.Abs(item.MaxX - targetX)))
                    .FirstOrDefault();
            }

            if (segment == null)
            {
                return false;
            }

            double deltaX = segment.EndLocal.X - segment.StartLocal.X;
            if (Math.Abs(deltaX) <= 1e-12)
            {
                point = (segment.StartPoint + segment.EndPoint) / 2.0;
                return true;
            }

            double parameter = (targetX - segment.StartLocal.X) / deltaX;
            parameter = Math.Max(0.0, Math.Min(1.0, parameter));
            point = segment.StartPoint + (segment.EndPoint - segment.StartPoint) * parameter;
            return true;
        }

        private bool ElementIntersectsSectionPlane(
            ViewSection view,
            ElementCandidate candidate,
            double sectionPlaneDepth,
            double tolerance)
        {
            HostObject hostObject = candidate != null ? candidate.Element as HostObject : null;
            if (hostObject == null)
            {
                return candidate != null && BoundsIntersectSectionPlane(
                    view,
                    candidate.Bounds,
                    sectionPlaneDepth,
                    tolerance);
            }

            bool inspectedFace = false;
            try
            {
                IEnumerable<Reference> faceReferences = HostObjectUtils.GetTopFaces(hostObject)
                    .Concat(HostObjectUtils.GetBottomFaces(hostObject));
                foreach (Reference faceReference in faceReferences)
                {
                    Face face = hostObject.GetGeometryObjectFromReference(faceReference) as Face;
                    if (face == null)
                    {
                        continue;
                    }

                    inspectedFace = true;
                    if (FaceIntersectsSectionPlane(
                        view,
                        face,
                        candidate.SourceToHostTransform,
                        sectionPlaneDepth,
                        tolerance))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                inspectedFace = false;
            }

            return !inspectedFace && BoundsIntersectSectionPlane(
                view,
                candidate.Bounds,
                sectionPlaneDepth,
                tolerance);
        }

        private bool FaceIntersectsSectionPlane(
            ViewSection view,
            Face face,
            Transform sourceToHost,
            double sectionPlaneDepth,
            double tolerance)
        {
            if (view == null || face == null)
            {
                return false;
            }

            try
            {
                Mesh mesh = face.Triangulate();
                if (mesh == null || mesh.NumTriangles == 0)
                {
                    return false;
                }

                Transform transform = sourceToHost ?? Transform.Identity;
                BoundingBoxXYZ crop = view.CropBox;
                Transform cropInverse = crop.Transform.Inverse;
                double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
                double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
                double cropMinY = Math.Min(crop.Min.Y, crop.Max.Y);
                double cropMaxY = Math.Max(crop.Min.Y, crop.Max.Y);

                for (int triangleIndex = 0; triangleIndex < mesh.NumTriangles; triangleIndex++)
                {
                    MeshTriangle triangle = mesh.get_Triangle(triangleIndex);
                    List<XYZ> hostVertices = new List<XYZ>();
                    for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                    {
                        hostVertices.Add(transform.OfPoint(triangle.get_Vertex(vertexIndex)));
                    }

                    double minimumDepth = hostVertices.Min(point => GetSignedViewDepth(view, point));
                    double maximumDepth = hostVertices.Max(point => GetSignedViewDepth(view, point));
                    if (minimumDepth > sectionPlaneDepth + tolerance ||
                        maximumDepth < sectionPlaneDepth - tolerance)
                    {
                        continue;
                    }

                    IList<XYZ> localVertices = hostVertices.Select(cropInverse.OfPoint).ToList();
                    if (IntervalsOverlap(
                            localVertices.Min(point => point.X),
                            localVertices.Max(point => point.X),
                            cropMinX,
                            cropMaxX) &&
                        IntervalsOverlap(
                            localVertices.Min(point => point.Y),
                            localVertices.Max(point => point.Y),
                            cropMinY,
                            cropMaxY))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool BoundsIntersectSectionPlane(
            ViewSection view,
            BoundingBoxXYZ bounds,
            double sectionPlaneDepth,
            double tolerance)
        {
            if (view == null || bounds == null)
            {
                return false;
            }

            IList<double> depths = GetCorners(bounds)
                .Select(point => GetSignedViewDepth(view, point))
                .ToList();
            double minimumDepth = depths.Min();
            double maximumDepth = depths.Max();
            return minimumDepth <= sectionPlaneDepth + tolerance &&
                   maximumDepth >= sectionPlaneDepth - tolerance;
        }

        private List<ElementCandidate> FilterPrimaryVisibleWalls(
            ViewSection view,
            IList<ElementCandidate> candidates)
        {
            List<ElementCandidate> unique = DeduplicateElementCandidates(candidates);
            if (view == null || unique.Count < 2)
            {
                return unique;
            }

            List<ProjectedCandidate> ordered = unique
                .Select(candidate => ProjectCandidate(view, candidate))
                .OrderBy(candidate => candidate.Depth)
                .ToList();
            List<ProjectedCandidate> accepted = new List<ProjectedCandidate>();
            double samePlaneTolerance = UnitConversionUtils.MillimetersToFeet(30.0);
            for (int index = 0; index < ordered.Count; index++)
            {
                ProjectedCandidate current = ordered[index];
                bool coveredByFrontWall = accepted.Any(front =>
                    front.Depth <= current.Depth + samePlaneTolerance &&
                    GetIntervalOverlapRatio(
                        front.MinX,
                        front.MaxX,
                        current.MinX,
                        current.MaxX) >= 0.35 &&
                    GetIntervalOverlapRatio(
                        front.MinY,
                        front.MaxY,
                        current.MinY,
                        current.MaxY) >= 0.35);
                if (!coveredByFrontWall)
                {
                    accepted.Add(current);
                }
            }

            return accepted
                .Select(candidate => candidate.Candidate)
                .ToList();
        }

        private double GetIntervalOverlapRatio(
            double firstMin,
            double firstMax,
            double secondMin,
            double secondMax)
        {
            double firstLength = Math.Max(0.0, firstMax - firstMin);
            double secondLength = Math.Max(0.0, secondMax - secondMin);
            double minimumLength = Math.Min(firstLength, secondLength);
            if (minimumLength <= 1e-9)
            {
                return 0.0;
            }

            double overlap = Math.Max(
                0.0,
                Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin));
            return overlap / minimumLength;
        }

        private List<ElementCandidate> DeduplicateElementCandidates(
            IEnumerable<ElementCandidate> candidates)
        {
            if (candidates == null)
            {
                return new List<ElementCandidate>();
            }

            return candidates
                .Where(candidate => candidate != null && candidate.Element != null)
                .GroupBy(BuildElementCandidateKey)
                .Select(group => group.First())
                .ToList();
        }

        private List<ElementCandidate> FilterFrontmostCandidates(
            ViewSection view,
            IList<ElementCandidate> candidates,
            double depthToleranceMm = 100.0)
        {
            if (candidates == null || candidates.Count < 2)
            {
                return candidates != null
                    ? candidates.ToList()
                    : new List<ElementCandidate>();
            }

            List<ProjectedCandidate> projected = candidates
                .Select(candidate => ProjectCandidate(view, candidate))
                .OrderBy(candidate => candidate.Depth)
                .ToList();
            List<ProjectedCandidate> accepted = new List<ProjectedCandidate>();
            double depthTolerance = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, depthToleranceMm));
            double tolerance = UnitConversionUtils.MillimetersToFeet(1.0);

            for (int index = 0; index < projected.Count; index++)
            {
                ProjectedCandidate current = projected[index];
                bool hiddenBehindAccepted = accepted.Any(front =>
                    front.Depth + depthTolerance < current.Depth &&
                    front.MinX <= current.MinX + tolerance &&
                    front.MaxX >= current.MaxX - tolerance &&
                    front.MinY <= current.MinY + tolerance &&
                    front.MaxY >= current.MaxY - tolerance);
                if (!hiddenBehindAccepted)
                {
                    accepted.Add(current);
                }
            }

            return accepted.Select(candidate => candidate.Candidate).ToList();
        }

        private List<ElementCandidate> FilterFirstVisibleWallsByRay(
            Document document,
            ViewSection view,
            IList<ElementCandidate> candidates,
            IList<ElementCandidate> allVisibleWalls,
            double? sectionPlaneDepth)
        {
            if (candidates == null || candidates.Count < 2)
            {
                return candidates != null
                    ? candidates.ToList()
                    : new List<ElementCandidate>();
            }

            if (document == null || view == null || allVisibleWalls == null ||
                allVisibleWalls.Count == 0)
            {
                return FilterFrontmostCandidates(view, candidates, 5.0);
            }

            View3D rayView = new FilteredElementCollector(document)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(candidate => !candidate.IsTemplate)
                .OrderBy(candidate => candidate.IsSectionBoxActive ? 1 : 0)
                .FirstOrDefault();
            if (rayView == null)
            {
                return FilterFrontmostCandidates(view, candidates, 5.0);
            }

            try
            {
                ElementFilter rayFilter = new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementClassFilter(typeof(RevitLinkInstance)));
                ReferenceIntersector intersector = new ReferenceIntersector(
                    rayFilter,
                    FindReferenceTarget.Face,
                    rayView)
                {
                    FindReferencesInRevitLinks = true
                };
                XYZ viewDirection = view.ViewDirection.Normalize();
                BoundingBoxXYZ crop = view.CropBox;
                Transform cropInverse = crop.Transform.Inverse;
                double cropMinX = Math.Min(crop.Min.X, crop.Max.X);
                double cropMaxX = Math.Max(crop.Min.X, crop.Max.X);
                double cropMinY = Math.Min(crop.Min.Y, crop.Max.Y);
                double cropMaxY = Math.Max(crop.Min.Y, crop.Max.Y);
                double rayStartOffset = UnitConversionUtils.MillimetersToFeet(10.0);
                double targetDepth = sectionPlaneDepth ?? 0.0;
                bool resolvedAnyRay = false;
                List<ElementCandidate> accepted = new List<ElementCandidate>();

                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    ElementCandidate candidate = candidates[candidateIndex];
                    ProjectedCandidate projected = ProjectCandidate(view, candidate);
                    double minX = Math.Max(projected.MinX, cropMinX);
                    double maxX = Math.Min(projected.MaxX, cropMaxX);
                    double minY = Math.Max(projected.MinY, cropMinY);
                    double maxY = Math.Min(projected.MaxY, cropMaxY);
                    if (maxX <= minX || maxY <= minY)
                    {
                        continue;
                    }

                    bool isFirstVisible = false;
                    double[] horizontalFractions = { 0.12, 0.32, 0.50, 0.68, 0.88 };
                    double[] verticalFractions = { 0.20, 0.50, 0.80 };
                    for (int xIndex = 0;
                        xIndex < horizontalFractions.Length && !isFirstVisible;
                        xIndex++)
                    {
                        for (int yIndex = 0;
                            yIndex < verticalFractions.Length && !isFirstVisible;
                            yIndex++)
                        {
                            double localX = minX + (maxX - minX) * horizontalFractions[xIndex];
                            double localY = minY + (maxY - minY) * verticalFractions[yIndex];
                            double localZ = (crop.Min.Z + crop.Max.Z) / 2.0;
                            XYZ sample = crop.Transform.OfPoint(
                                new XYZ(localX, localY, localZ));
                            sample -= viewDirection *
                                (GetSignedViewDepth(view, sample) - targetDepth);
                            XYZ rayOrigin = sample - viewDirection * rayStartOffset;

                            IList<ReferenceWithContext> hits = intersector
                                .Find(rayOrigin, viewDirection)
                                .OrderBy(hit => hit.Proximity)
                                .ToList();
                            for (int hitIndex = 0; hitIndex < hits.Count; hitIndex++)
                            {
                                Reference hitReference = hits[hitIndex].GetReference();
                                ElementCandidate hitWall = FindMatchingCandidate(
                                    hitReference,
                                    allVisibleWalls);
                                if (hitWall == null)
                                {
                                    continue;
                                }

                                resolvedAnyRay = true;
                                isFirstVisible = AreSameCandidate(candidate, hitWall);
                                break;
                            }
                        }
                    }

                    if (isFirstVisible)
                    {
                        accepted.Add(candidate);
                    }
                }

                return resolvedAnyRay
                    ? accepted
                    : FilterFrontmostCandidates(view, candidates, 5.0);
            }
            catch
            {
                // Если в проекте нет пригодного 3D-вида или трассировка запрещена
                // настройками вида, сохраняем консервативную проверку по глубине.
                return FilterFrontmostCandidates(view, candidates, 5.0);
            }
        }

        private ElementCandidate FindMatchingCandidate(
            Reference reference,
            IList<ElementCandidate> candidates)
        {
            if (reference == null || candidates == null)
            {
                return null;
            }

            ElementId hostElementId = reference.ElementId;
            ElementId linkedElementId = reference.LinkedElementId;
            for (int index = 0; index < candidates.Count; index++)
            {
                ElementCandidate candidate = candidates[index];
                if (candidate == null || candidate.Element == null)
                {
                    continue;
                }

                if (candidate.LinkInstance == null)
                {
                    if (RevitElementIdUtils.AreEqual(
                        candidate.Element.Id,
                        hostElementId))
                    {
                        return candidate;
                    }

                    continue;
                }

                if (RevitElementIdUtils.AreEqual(
                        candidate.LinkInstance.Id,
                        hostElementId) &&
                    linkedElementId != null &&
                    linkedElementId != ElementId.InvalidElementId &&
                    RevitElementIdUtils.AreEqual(
                        candidate.Element.Id,
                        linkedElementId))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool AreSameCandidate(
            ElementCandidate first,
            ElementCandidate second)
        {
            if (first == null || second == null || first.Element == null ||
                second.Element == null)
            {
                return false;
            }

            bool sameLink = first.LinkInstance == null && second.LinkInstance == null ||
                first.LinkInstance != null && second.LinkInstance != null &&
                RevitElementIdUtils.AreEqual(
                    first.LinkInstance.Id,
                    second.LinkInstance.Id);
            return sameLink && RevitElementIdUtils.AreEqual(
                first.Element.Id,
                second.Element.Id);
        }

        private ProjectedCandidate ProjectCandidate(ViewSection view, ElementCandidate candidate)
        {
            Transform inverse = view.CropBox.Transform.Inverse;
            IList<XYZ> localCorners = GetCorners(candidate.Bounds)
                .Select(inverse.OfPoint)
                .ToList();
            return new ProjectedCandidate
            {
                Candidate = candidate,
                MinX = localCorners.Min(point => point.X),
                MaxX = localCorners.Max(point => point.X),
                MinY = localCorners.Min(point => point.Y),
                MaxY = localCorners.Max(point => point.Y),
                Depth = GetViewDepth(view, candidate.Center)
            };
        }

        private List<ElementCandidate> FilterOccludedByWalls(
            ViewSection view,
            IList<ElementCandidate> candidates,
            IList<ElementCandidate> walls)
        {
            if (candidates == null || candidates.Count == 0 || walls == null || walls.Count == 0)
            {
                return candidates != null
                    ? candidates.ToList()
                    : new List<ElementCandidate>();
            }

            List<ProjectedCandidate> projectedWalls = walls
                .Select(wall => ProjectCandidate(view, wall))
                .ToList();
            double sameHostTolerance = UnitConversionUtils.MillimetersToFeet(300.0);

            return candidates
                .Where(candidate =>
                {
                    ProjectedCandidate projectedCandidate = ProjectCandidate(view, candidate);
                    return !projectedWalls.Any(wall =>
                        !IsOpeningHostWall(candidate, wall.Candidate) &&
                        wall.Depth + sameHostTolerance < projectedCandidate.Depth &&
                        IntervalsOverlap(wall.MinX, wall.MaxX, projectedCandidate.MinX, projectedCandidate.MaxX) &&
                        IntervalsOverlap(wall.MinY, wall.MaxY, projectedCandidate.MinY, projectedCandidate.MaxY));
                })
                .ToList();
        }

        private bool IsOpeningHostWall(
            ElementCandidate openingCandidate,
            ElementCandidate wallCandidate)
        {
            FamilyInstance opening = openingCandidate != null
                ? openingCandidate.Element as FamilyInstance
                : null;
            Wall hostWall = opening != null ? opening.Host as Wall : null;
            if (hostWall == null || wallCandidate == null || wallCandidate.Element == null)
            {
                return false;
            }

            bool sameLink = openingCandidate.LinkInstance == null &&
                    wallCandidate.LinkInstance == null ||
                openingCandidate.LinkInstance != null &&
                wallCandidate.LinkInstance != null &&
                RevitElementIdUtils.AreEqual(
                    openingCandidate.LinkInstance.Id,
                    wallCandidate.LinkInstance.Id);
            return sameLink && RevitElementIdUtils.AreEqual(
                hostWall.Id,
                wallCandidate.Element.Id);
        }

        private bool IntervalsOverlap(double minA, double maxA, double minB, double maxB)
        {
            return Math.Min(maxA, maxB) >= Math.Max(minA, minB);
        }

        private List<HostFaceCandidate> FilterFacesByCatalogRole(
            IEnumerable<HostFaceCandidate> candidates,
            ElevationDecorationCatalogRole role)
        {
            return candidates == null
                ? new List<HostFaceCandidate>()
                : candidates
                    .Where(candidate => candidate != null &&
                        candidate.ElementCandidate != null &&
                        MatchesCatalogRole(candidate.ElementCandidate.Element, role))
                    .ToList();
        }

        private List<ElementCandidate> FilterElementsByCatalogRole(
            IEnumerable<ElementCandidate> candidates,
            ElevationDecorationCatalogRole role)
        {
            return candidates == null
                ? new List<ElementCandidate>()
                : candidates
                    .Where(candidate => candidate != null &&
                        MatchesCatalogRole(candidate.Element, role))
                    .ToList();
        }

        private bool MatchesCatalogRole(
            Element element,
            ElevationDecorationCatalogRole role)
        {
            return _catalogService.Matches(_catalog, role, element);
        }

        private List<ElementCandidate> ToVisibleElementCandidates(
            IEnumerable<HostFaceCandidate> faces)
        {
            if (faces == null)
            {
                return new List<ElementCandidate>();
            }

            List<ElementCandidate> result = new List<ElementCandidate>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HostFaceCandidate face in faces
                .Where(candidate => candidate != null &&
                    candidate.ElementCandidate != null &&
                    candidate.VisibleFragment != null)
                .OrderByDescending(candidate =>
                    candidate.VisibleFragment.MaxX - candidate.VisibleFragment.MinX))
            {
                ElementCandidate source = face.ElementCandidate;
                string linkKey = source.LinkInstance != null
                    ? RevitElementIdUtils.GetElementIdValue(source.LinkInstance.Id).ToString()
                    : "host";
                string key = linkKey + "|" +
                    (source.Element != null ? source.Element.UniqueId : string.Empty);
                if (!keys.Add(key))
                {
                    continue;
                }

                result.Add(new ElementCandidate
                {
                    Element = source.Element,
                    TypeId = source.TypeId,
                    Bounds = source.Bounds,
                    Reference = source.Reference,
                    SourceToHostTransform = source.SourceToHostTransform,
                    LinkInstance = source.LinkInstance,
                    VisibleAnchorPoint = face.VisibleFragment.CenterPoint,
                    VisibleMinX = face.VisibleFragment.MinX,
                    VisibleMaxX = face.VisibleFragment.MaxX
                });
            }

            return result;
        }

        private HostFaceCandidate FindFinishFloor(
            IList<HostFaceCandidate> candidates,
            double referenceElevation)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            double elevationTolerance = UnitConversionUtils.MillimetersToFeet(
                ElevationToleranceMm);
            return candidates
                .Where(candidate =>
                    Math.Abs(candidate.Elevation - referenceElevation) <=
                    elevationTolerance)
                .OrderByDescending(candidate => candidate.VisibleFragment != null
                    ? candidate.VisibleFragment.MaxX - candidate.VisibleFragment.MinX
                    : 0.0)
                .FirstOrDefault();
        }

        private HostFaceCandidate FindStructuralBase(
            IList<HostFaceCandidate> candidates,
            double referenceElevation)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            double elevationTolerance = UnitConversionUtils.MillimetersToFeet(
                ElevationToleranceMm);
            return candidates
                .Where(candidate =>
                    candidate.Elevation < referenceElevation - elevationTolerance)
                .OrderByDescending(candidate => candidate.Elevation)
                .FirstOrDefault();
        }

        private void AddUniqueElevationCandidates(
            IList<HostFaceCandidate> target,
            IEnumerable<HostFaceCandidate> source,
            string role)
        {
            double tolerance = UnitConversionUtils.MillimetersToFeet(ElevationToleranceMm);
            foreach (HostFaceCandidate candidate in source
                .OrderByDescending(item => item.VisibleFragment != null
                    ? item.VisibleFragment.MaxX - item.VisibleFragment.MinX
                    : 0.0))
            {
                if (target.Any(existing => Math.Abs(existing.Elevation - candidate.Elevation) <= tolerance))
                {
                    continue;
                }

                target.Add(candidate.WithRole(role));
            }
        }

        private List<HostFaceCandidate> CollectHostFaces(
            Document document,
            ViewSection view,
            BuiltInCategory category,
            bool topFaces)
        {
            List<HostFaceCandidate> result = new List<HostFaceCandidate>();
            IList<ElementCandidate> elements = CollectHostElements(document, view, category);
            for (int index = 0; index < elements.Count; index++)
            {
                HostObject hostObject = elements[index].Element as HostObject;
                if (hostObject == null)
                {
                    continue;
                }

                result.AddRange(BuildHostFaceCandidates(
                    elements[index],
                    Transform.Identity,
                    null,
                    topFaces));
            }

            return result;
        }

        private List<HostFaceCandidate> CollectPlinthTopFaces(
            Document document,
            ViewSection view,
            double? sectionPlaneDepth)
        {
            List<HostFaceCandidate> result = new List<HostFaceCandidate>();
            List<ElementCandidate> plinths = FilterElementsBySectionPlane(
                    view,
                    CollectHostElements(
                        document,
                        view,
                        BuiltInCategory.OST_StairsRailing),
                    sectionPlaneDepth)
                .Where(candidate =>
                    MatchesCatalogRole(
                        candidate.Element,
                        ElevationDecorationCatalogRole.Plinth))
                .Where(candidate =>
                    candidate.Height <= UnitConversionUtils.MillimetersToFeet(
                        PlinthMaximumHeightMm))
                .ToList();

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = view
            };
            for (int index = 0; index < plinths.Count; index++)
            {
                ElementCandidate plinth = plinths[index];
                try
                {
                    GeometryElement geometry = plinth.Element.get_Geometry(options);
                    CollectTopPlanarFaces(
                        geometry,
                        Transform.Identity,
                        plinth.Bounds,
                        plinth,
                        view,
                        sectionPlaneDepth,
                        null,
                        result);
                }
                catch
                {
                    // Плинтус останется без промежуточной засечки, но остальные
                    // уровни цепочки размеров продолжат строиться.
                }
            }

            List<ElementCandidate> linkedPlinths = FilterElementsBySectionPlane(
                    view,
                    CollectLinkedElements(
                        document,
                        view,
                        BuiltInCategory.OST_StairsRailing),
                    sectionPlaneDepth)
                .Where(candidate =>
                    MatchesCatalogRole(
                        candidate.Element,
                        ElevationDecorationCatalogRole.Plinth))
                .Where(candidate =>
                    candidate.Height <= UnitConversionUtils.MillimetersToFeet(
                        PlinthMaximumHeightMm))
                .ToList();
            Options linkedOptions = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false
            };
            for (int index = 0; index < linkedPlinths.Count; index++)
            {
                ElementCandidate plinth = linkedPlinths[index];
                try
                {
                    GeometryElement geometry = plinth.Element.get_Geometry(linkedOptions);
                    CollectTopPlanarFaces(
                        geometry,
                        plinth.SourceToHostTransform ?? Transform.Identity,
                        plinth.Bounds,
                        plinth,
                        view,
                        sectionPlaneDepth,
                        plinth.LinkInstance,
                        result);
                }
                catch
                {
                    // Остальные привязки размера не должны зависеть от одной связи.
                }
            }

            return result;
        }

        private void CollectTopPlanarFaces(
            GeometryElement geometry,
            Transform sourceToHost,
            BoundingBoxXYZ hostBounds,
            ElementCandidate elementCandidate,
            ViewSection view,
            double? sectionPlaneDepth,
            RevitLinkInstance linkInstance,
            IList<HostFaceCandidate> result)
        {
            if (geometry == null || result == null)
            {
                return;
            }

            foreach (GeometryObject geometryObject in geometry)
            {
                Solid solid = geometryObject as Solid;
                if (solid != null && solid.Faces != null && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        PlanarFace planarFace = face as PlanarFace;
                        if (planarFace == null || planarFace.Reference == null)
                        {
                            continue;
                        }

                        XYZ hostNormal = sourceToHost.OfVector(planarFace.FaceNormal);
                        if (hostNormal.GetLength() <= 1e-9 ||
                            hostNormal.Normalize().DotProduct(XYZ.BasisZ) < 0.95)
                        {
                            continue;
                        }

                        if (sectionPlaneDepth.HasValue &&
                            !FaceIntersectsSectionPlane(
                                view,
                                planarFace,
                                sourceToHost,
                                sectionPlaneDepth.Value,
                                UnitConversionUtils.MillimetersToFeet(
                                    SectionPlaneToleranceMm)))
                        {
                            continue;
                        }

                        BoundingBoxUV uvBounds = planarFace.GetBoundingBox();
                        UV centerUv = new UV(
                            (uvBounds.Min.U + uvBounds.Max.U) / 2.0,
                            (uvBounds.Min.V + uvBounds.Max.V) / 2.0);
                        XYZ hostPoint = sourceToHost.OfPoint(planarFace.Evaluate(centerUv));
                        result.Add(new HostFaceCandidate
                        {
                            Reference = linkInstance != null
                                ? planarFace.Reference.CreateLinkReference(linkInstance)
                                : planarFace.Reference,
                            ReferencePoint = hostPoint,
                            Elevation = hostPoint.Z,
                            Bounds = hostBounds,
                            Face = planarFace,
                            SourceToHostTransform = sourceToHost,
                            ElementCandidate = elementCandidate
                        });
                    }

                    continue;
                }

                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance == null)
                {
                    continue;
                }

                GeometryElement symbolGeometry = instance.GetSymbolGeometry();
                Transform nestedTransform = sourceToHost.Multiply(instance.Transform);
                CollectTopPlanarFaces(
                    symbolGeometry,
                    nestedTransform,
                    hostBounds,
                    elementCandidate,
                    view,
                    sectionPlaneDepth,
                    linkInstance,
                    result);
            }
        }

        private List<HostFaceCandidate> CollectLinkedHostFaces(
            Document document,
            ViewSection view,
            BuiltInCategory category,
            bool topFaces)
        {
            List<HostFaceCandidate> result = new List<HostFaceCandidate>();
            IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(document, view.Id)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            for (int linkIndex = 0; linkIndex < linkInstances.Count; linkIndex++)
            {
                RevitLinkInstance linkInstance = linkInstances[linkIndex];
                Document linkDocument = linkInstance.GetLinkDocument();
                if (linkDocument == null)
                {
                    continue;
                }

                Transform linkTransform = linkInstance.GetTotalTransform();
                IList<Element> hostObjects = new FilteredElementCollector(linkDocument)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();
                for (int hostObjectIndex = 0; hostObjectIndex < hostObjects.Count; hostObjectIndex++)
                {
                    HostObject hostObject = hostObjects[hostObjectIndex] as HostObject;
                    BoundingBoxXYZ linkedBounds = hostObjects[hostObjectIndex].get_BoundingBox(null);
                    BoundingBoxXYZ hostBounds = TransformBounds(linkedBounds, linkTransform);
                    if (hostObject == null || hostBounds == null || !IntersectsViewCrop(view, hostBounds))
                    {
                        continue;
                    }

                    ElementCandidate elementCandidate = new ElementCandidate
                    {
                        Element = hostObject,
                        TypeId = hostObject.GetTypeId(),
                        Bounds = hostBounds,
                        Reference = new Reference(hostObject).CreateLinkReference(linkInstance),
                        SourceToHostTransform = linkTransform,
                        LinkInstance = linkInstance
                    };
                    result.AddRange(BuildHostFaceCandidates(
                        elementCandidate,
                        linkTransform,
                        linkInstance,
                        topFaces));
                }
            }

            return result;
        }

        private List<HostFaceCandidate> BuildHostFaceCandidates(
            ElementCandidate elementCandidate,
            Transform transform,
            RevitLinkInstance linkInstance,
            bool topFace)
        {
            List<HostFaceCandidate> result = new List<HostFaceCandidate>();
            HostObject hostObject = elementCandidate != null
                ? elementCandidate.Element as HostObject
                : null;
            if (hostObject == null)
            {
                return result;
            }

            IList<Reference> references = topFace
                ? HostObjectUtils.GetTopFaces(hostObject)
                : HostObjectUtils.GetBottomFaces(hostObject);
            if (references == null || references.Count == 0)
            {
                return result;
            }

            for (int index = 0; index < references.Count; index++)
            {
                Reference sourceReference = references[index];
                Face face = hostObject.GetGeometryObjectFromReference(sourceReference) as Face;
                if (face == null)
                {
                    continue;
                }

                BoundingBoxUV uvBounds = face.GetBoundingBox();
                UV uv = new UV(
                    (uvBounds.Min.U + uvBounds.Max.U) / 2.0,
                    (uvBounds.Min.V + uvBounds.Max.V) / 2.0);
                XYZ point = face.Evaluate(uv);
                XYZ hostPoint = transform.OfPoint(point);
                Reference hostReference = linkInstance != null
                    ? sourceReference.CreateLinkReference(linkInstance)
                    : sourceReference;
                BoundingBoxXYZ bounds = elementCandidate.Bounds;
                double thickness = bounds != null
                    ? Math.Abs(bounds.Max.Z - bounds.Min.Z)
                    : 0.0;

                result.Add(new HostFaceCandidate
                {
                    Reference = hostReference,
                    ReferencePoint = hostPoint,
                    Elevation = hostPoint.Z,
                    Thickness = thickness,
                    Bounds = TransformBounds(bounds, transform),
                    Face = face,
                    SourceToHostTransform = transform,
                    ElementCandidate = elementCandidate
                });
            }

            return result;
        }

        private List<ElementCandidate> CollectHostElements(
            Document document,
            ViewSection view,
            BuiltInCategory category)
        {
            List<ElementCandidate> result = new List<ElementCandidate>();
            IList<Element> elements = new FilteredElementCollector(document, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();
            for (int index = 0; index < elements.Count; index++)
            {
                Element element = elements[index];
                BoundingBoxXYZ sourceBounds = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
                BoundingBoxXYZ bounds = TransformBounds(sourceBounds, Transform.Identity);
                if (bounds == null || !IntersectsViewCrop(view, bounds))
                {
                    continue;
                }

                result.Add(new ElementCandidate
                {
                    Element = element,
                    TypeId = element.GetTypeId(),
                    Bounds = bounds,
                    Reference = new Reference(element),
                        SourceToHostTransform = Transform.Identity,
                        LinkInstance = null
                });
            }

            return result;
        }

        private List<ElementCandidate> CollectLinkedElements(
            Document document,
            ViewSection view,
            BuiltInCategory category)
        {
            List<ElementCandidate> result = new List<ElementCandidate>();
            IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(document, view.Id)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            for (int linkIndex = 0; linkIndex < linkInstances.Count; linkIndex++)
            {
                RevitLinkInstance linkInstance = linkInstances[linkIndex];
                Document linkDocument = linkInstance.GetLinkDocument();
                if (linkDocument == null)
                {
                    continue;
                }

                Transform transform = linkInstance.GetTotalTransform();
                IList<Element> linkedElements = new FilteredElementCollector(linkDocument)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();
                for (int elementIndex = 0; elementIndex < linkedElements.Count; elementIndex++)
                {
                    Element linkedElement = linkedElements[elementIndex];
                    BoundingBoxXYZ bounds = TransformBounds(linkedElement.get_BoundingBox(null), transform);
                    if (bounds == null || !IntersectsViewCrop(view, bounds))
                    {
                        continue;
                    }

                    result.Add(new ElementCandidate
                    {
                        Element = linkedElement,
                        TypeId = linkedElement.GetTypeId(),
                        Bounds = bounds,
                        Reference = new Reference(linkedElement).CreateLinkReference(linkInstance),
                        SourceToHostTransform = transform,
                        LinkInstance = linkInstance
                    });
                }
            }

            return result;
        }

        private bool IntersectsViewCrop(ViewSection view, BoundingBoxXYZ bounds)
        {
            if (view == null || bounds == null || !view.CropBoxActive)
            {
                return true;
            }

            BoundingBoxXYZ crop = view.CropBox;
            Transform inverse = crop.Transform.Inverse;
            IList<XYZ> localCorners = GetCorners(bounds)
                .Select(inverse.OfPoint)
                .ToList();
            double boundsMinX = localCorners.Min(point => point.X);
            double boundsMaxX = localCorners.Max(point => point.X);
            double boundsMinY = localCorners.Min(point => point.Y);
            double boundsMaxY = localCorners.Max(point => point.Y);
            double boundsMinZ = localCorners.Min(point => point.Z);
            double boundsMaxZ = localCorners.Max(point => point.Z);

            return IntervalsOverlap(
                       boundsMinX,
                       boundsMaxX,
                       Math.Min(crop.Min.X, crop.Max.X),
                       Math.Max(crop.Min.X, crop.Max.X)) &&
                   IntervalsOverlap(
                       boundsMinY,
                       boundsMaxY,
                       Math.Min(crop.Min.Y, crop.Max.Y),
                       Math.Max(crop.Min.Y, crop.Max.Y)) &&
                   IntervalsOverlap(
                       boundsMinZ,
                       boundsMaxZ,
                       Math.Min(crop.Min.Z, crop.Max.Z),
                       Math.Max(crop.Min.Z, crop.Max.Z));
        }

        private bool IsPointInsideCropProjection(ViewSection view, XYZ point)
        {
            if (view == null || point == null || !view.CropBoxActive)
            {
                return true;
            }

            BoundingBoxXYZ crop = view.CropBox;
            XYZ local = crop.Transform.Inverse.OfPoint(point);
            return local.X >= Math.Min(crop.Min.X, crop.Max.X) &&
                   local.X <= Math.Max(crop.Min.X, crop.Max.X) &&
                   local.Y >= Math.Min(crop.Min.Y, crop.Max.Y) &&
                   local.Y <= Math.Max(crop.Min.Y, crop.Max.Y);
        }

        private BoundingBoxXYZ TransformBounds(BoundingBoxXYZ source, Transform transform)
        {
            if (source == null || transform == null)
            {
                return null;
            }

            IList<XYZ> points = GetCorners(source).Select(transform.OfPoint).ToList();
            BoundingBoxXYZ result = new BoundingBoxXYZ();
            result.Min = new XYZ(
                points.Min(point => point.X),
                points.Min(point => point.Y),
                points.Min(point => point.Z));
            result.Max = new XYZ(
                points.Max(point => point.X),
                points.Max(point => point.Y),
                points.Max(point => point.Z));
            return result;
        }

        private IEnumerable<XYZ> GetCorners(BoundingBoxXYZ bounds)
        {
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        XYZ local = new XYZ(
                            x == 0 ? bounds.Min.X : bounds.Max.X,
                            y == 0 ? bounds.Min.Y : bounds.Max.Y,
                            z == 0 ? bounds.Min.Z : bounds.Max.Z);
                        yield return bounds.Transform != null
                            ? bounds.Transform.OfPoint(local)
                            : local;
                    }
                }
            }
        }

        private double GetCropBottomElevation(ViewSection view)
        {
            BoundingBoxXYZ crop = view.CropBox;
            double minY = Math.Min(crop.Min.Y, crop.Max.Y);
            XYZ modelPoint = crop.Transform.OfPoint(new XYZ(0.0, minY, 0.0));
            return modelPoint.Z;
        }

        private double GetFloorReferenceElevation(ViewSection view)
        {
            Level associatedLevel = view != null ? view.GenLevel : null;
            if (associatedLevel != null)
            {
                return associatedLevel.Elevation;
            }

            double cropBottomElevation = GetCropBottomElevation(view);
            Level nearestLevel = new FilteredElementCollector(view.Document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - cropBottomElevation))
                .FirstOrDefault();
            return nearestLevel != null
                ? nearestLevel.Elevation
                : cropBottomElevation;
        }

        private double GetViewDepth(ViewSection view, XYZ point)
        {
            return Math.Abs((point - view.Origin).DotProduct(view.ViewDirection));
        }

        private double GetSignedViewDepth(ViewSection view, XYZ point)
        {
            return (point - view.Origin).DotProduct(view.ViewDirection);
        }

        private double GetWallWidth(ElementCandidate candidate)
        {
            Wall wall = candidate != null ? candidate.Element as Wall : null;
            WallType wallType = wall != null ? wall.WallType : null;
            return wallType != null ? wallType.Width : double.MaxValue;
        }

        private bool IsValidType(Document document, ElementId typeId)
        {
            return document != null && typeId != null &&
                   typeId != ElementId.InvalidElementId &&
                   document.GetElement(typeId) is ElementType;
        }

        private bool IsValidSpotType(Document document, ElementId typeId)
        {
            return document != null && typeId != null &&
                   typeId != ElementId.InvalidElementId &&
                   document.GetElement(typeId) is SpotDimensionType;
        }

        private bool IsValidRelativeBaseLevel(Document document, ElementId levelId)
        {
            return document != null && levelId != null &&
                   levelId != ElementId.InvalidElementId &&
                   document.GetElement(levelId) is Level;
        }

        private bool TrySetSpotRelativeBase(
            SpotDimension spot,
            ElementId levelId,
            out string error)
        {
            error = null;
            if (spot == null)
            {
                error = "отметка не создана.";
                return false;
            }

            Parameter relativeBase = spot.get_Parameter(
                BuiltInParameter.SPOT_ELEV_RELATIVE_BASE);
            if (relativeBase == null)
            {
                error = "у выбранного типа отсутствует параметр «Относительная база». " +
                        "Проверьте, что в свойствах типа «База отметки» установлена в «Относительно».";
                return false;
            }

            if (relativeBase.IsReadOnly)
            {
                error = "параметр «Относительная база» доступен только для чтения.";
                return false;
            }

            if (!relativeBase.Set(levelId))
            {
                error = "Revit отклонил выбранный уровень.";
                return false;
            }

            return true;
        }

        private bool IsValidDimensionType(Document document, ElementId typeId)
        {
            DimensionType dimensionType = document != null && typeId != null
                ? document.GetElement(typeId) as DimensionType
                : null;
            return dimensionType != null &&
                   dimensionType.StyleType == DimensionStyleType.Linear;
        }

        private XYZ GetViewDiagnosticPoint(
            ViewSection view,
            double horizontalFraction,
            double verticalFraction)
        {
            BoundingBoxXYZ crop = view.CropBox;
            double minX = Math.Min(crop.Min.X, crop.Max.X);
            double maxX = Math.Max(crop.Min.X, crop.Max.X);
            double minY = Math.Min(crop.Min.Y, crop.Max.Y);
            double maxY = Math.Max(crop.Min.Y, crop.Max.Y);
            double localX = minX + (maxX - minX) * Math.Max(0.0, Math.Min(1.0, horizontalFraction));
            double localY = minY + (maxY - minY) * Math.Max(0.0, Math.Min(1.0, verticalFraction));
            double localZ = (crop.Min.Z + crop.Max.Z) / 2.0;
            return crop.Transform.OfPoint(new XYZ(localX, localY, localZ));
        }

        private void CreateFailureDiagnostic(
            Document document,
            ViewSection view,
            XYZ sourcePoint,
            string role,
            string reason = null)
        {
            if (document == null || view == null || sourcePoint == null)
            {
                return;
            }

            try
            {
                XYZ viewDirection = view.ViewDirection.Normalize();
                XYZ pointOnViewPlane = sourcePoint -
                    viewDirection * GetSignedViewDepth(view, sourcePoint);
                string diagnosticReason = string.IsNullOrWhiteSpace(reason)
                    ? BuildDefaultDiagnosticReason(role)
                    : reason.Trim();
                Element diagnostic = TryCreateDiagnosticFamilyInstance(
                    document,
                    view,
                    pointOnViewPlane,
                    diagnosticReason);
                if (diagnostic == null)
                {
                    diagnostic = CreateDiagnosticTextNote(
                        document,
                        view,
                        pointOnViewPlane,
                        diagnosticReason);
                }

                if (diagnostic != null)
                {
                    SetDiagnosticReason(diagnostic, diagnosticReason);
                    ApplyDiagnosticOverride(view, diagnostic);
                    _ownershipService.MarkOwned(
                        diagnostic,
                        view.Id,
                        "Diagnostic." + (role ?? string.Empty));
                }
            }
            catch
            {
                // Диагностика не должна прерывать оформление остальных элементов.
            }
        }

        private Element TryCreateDiagnosticFamilyInstance(
            Document document,
            ViewSection view,
            XYZ point,
            string reason)
        {
            try
            {
                FamilySymbol symbol = new FilteredElementCollector(document)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(candidate =>
                        candidate.Family != null &&
                        string.Equals(
                            candidate.Family.Name,
                            DiagnosticFamilyName,
                            StringComparison.OrdinalIgnoreCase));
                if (symbol == null)
                {
                    return null;
                }

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    document.Regenerate();
                }

                FamilyInstance instance = document.Create.NewFamilyInstance(
                    point,
                    symbol,
                    view);
                SetDiagnosticReason(instance, reason);
                return instance;
            }
            catch
            {
                // Пока диагностическое семейство не загружено или несовместимо
                // с видом, ниже будет создана текстовая диагностическая метка.
                return null;
            }
        }

        private TextNote CreateDiagnosticTextNote(
            Document document,
            ViewSection view,
            XYZ point,
            string reason)
        {
            ElementId textTypeId = document.GetDefaultElementTypeId(
                ElementTypeGroup.TextNoteType);
            if (textTypeId == null || textTypeId == ElementId.InvalidElementId)
            {
                TextNoteType textType = new FilteredElementCollector(document)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();
                textTypeId = textType != null
                    ? textType.Id
                    : ElementId.InvalidElementId;
            }

            if (textTypeId == ElementId.InvalidElementId)
            {
                return null;
            }

            string diagnosticText = "✕ " + reason;
            if (diagnosticText.Length > 240)
            {
                diagnosticText = diagnosticText.Substring(0, 237) + "...";
            }

            return TextNote.Create(
                document,
                view.Id,
                point,
                diagnosticText,
                textTypeId);
        }

        private void SetDiagnosticReason(Element element, string reason)
        {
            if (element == null || string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            string[] parameterNames =
            {
                "Причина ошибки",
                "Комментарий ошибки",
                "Комментарии"
            };
            for (int index = 0; index < parameterNames.Length; index++)
            {
                Parameter parameter = element.LookupParameter(parameterNames[index]);
                if (parameter != null && !parameter.IsReadOnly &&
                    parameter.StorageType == StorageType.String)
                {
                    parameter.Set(reason);
                    return;
                }
            }

            Parameter comments = element.get_Parameter(
                BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments != null && !comments.IsReadOnly &&
                comments.StorageType == StorageType.String)
            {
                comments.Set(reason);
            }
        }

        private string BuildDefaultDiagnosticReason(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return "Не удалось создать элемент оформления. Причина не определена.";
            }

            if (role.StartsWith("Spot", StringComparison.Ordinal))
            {
                return "Не создана высотная отметка: не найдена подходящая грань или Revit отклонил ссылку.";
            }

            if (role.StartsWith("Tag", StringComparison.Ordinal))
            {
                return "Не создана марка " + role + ": Revit отклонил ссылку на элемент.";
            }

            if (role.StartsWith("Dimension", StringComparison.Ordinal))
            {
                return "Не создан размер " + role + ": не найдены необходимые геометрические ссылки.";
            }

            return "Не создан элемент оформления " + role + ".";
        }

        private void ApplyDiagnosticOverride(View view, Element element)
        {
            if (view == null || element == null)
            {
                return;
            }

            OverrideGraphicSettings overrides = new OverrideGraphicSettings();
            overrides.SetProjectionLineColor(new Autodesk.Revit.DB.Color(255, 0, 0));
            overrides.SetProjectionLineWeight(6);
            view.SetElementOverrides(element.Id, overrides);
        }

        private void AddWarning(IList<string> warnings, string message)
        {
            if (warnings != null && !warnings.Contains(message))
            {
                warnings.Add(message);
            }
        }

        private enum TagVerticalPlacement
        {
            Center,
            Above,
            Below
        }

        private class TagBodyExtents
        {
            public double HalfWidth { get; set; }
            public double HalfHeight { get; set; }
        }

        private class ElevationViewGeometryContext
        {
            public XYZ Origin { get; set; }
            public XYZ ViewDirection { get; set; }
            public XYZ RightDirection { get; set; }
            public XYZ UpDirection { get; set; }
            public BoundingBoxXYZ CropBox { get; set; }
            public double SectionPlaneDepth { get; set; }
        }

        private class ElementCandidate
        {
            public Element Element { get; set; }
            public ElementId TypeId { get; set; }
            public BoundingBoxXYZ Bounds { get; set; }
            public Reference Reference { get; set; }
            public Transform SourceToHostTransform { get; set; }
            public RevitLinkInstance LinkInstance { get; set; }
            public XYZ VisibleAnchorPoint { get; set; }
            public double VisibleMinX { get; set; }
            public double VisibleMaxX { get; set; }

            public XYZ Center
            {
                get
                {
                    return new XYZ(
                        (Bounds.Min.X + Bounds.Max.X) / 2.0,
                        (Bounds.Min.Y + Bounds.Max.Y) / 2.0,
                        (Bounds.Min.Z + Bounds.Max.Z) / 2.0);
                }
            }

            public double Height
            {
                get { return Math.Abs(Bounds.Max.Z - Bounds.Min.Z); }
            }
        }

        private class HostFaceCandidate
        {
            public Reference Reference { get; set; }
            public XYZ ReferencePoint { get; set; }
            public double Elevation { get; set; }
            public double Thickness { get; set; }
            public string Role { get; set; }
            public BoundingBoxXYZ Bounds { get; set; }
            public Face Face { get; set; }
            public Transform SourceToHostTransform { get; set; }
            public ElementCandidate ElementCandidate { get; set; }
            public VisibleFaceFragment VisibleFragment { get; set; }

            public HostFaceCandidate WithRole(string role)
            {
                Role = role;
                return this;
            }
        }

        private class VisibleFaceFragment
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public XYZ LeftPoint { get; set; }
            public XYZ CenterPoint { get; set; }
            public XYZ RightPoint { get; set; }
        }

        private class VisibleFaceSegment
        {
            public XYZ StartPoint { get; set; }
            public XYZ EndPoint { get; set; }
            public XYZ StartLocal { get; set; }
            public XYZ EndLocal { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
        }

        private class VisibleFaceInterval
        {
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public List<VisibleFaceSegment> Segments { get; set; }
        }

        private class ProjectedCandidate
        {
            public ElementCandidate Candidate { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double Depth { get; set; }
        }

        private class WallFaceCandidate
        {
            public ElementCandidate WallCandidate { get; set; }
            public Reference Reference { get; set; }
            public XYZ Point { get; set; }
            public double LocalX { get; set; }
        }
    }
}
