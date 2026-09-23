using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис построения осей помещений.
    /// </summary>
    public class RoomAxisCreationService
    {
        private const string AxisMarkerComment = "SAB_ROOM_AXIS";

        private readonly RoomCollectorService _roomCollectorService;
        private readonly RoomBoundaryService _roomBoundaryService;
        private readonly RoomPlacementCheckService _roomPlacementCheckService;
        private readonly RoomAngleCheckService _roomAngleCheckService;
        private readonly RoomCentroidService _roomCentroidService;
        private readonly RoomAxisDirectionService _roomAxisDirectionService;
        private readonly RoomAxisClippingService _roomAxisClippingService;
        private readonly RevitStyleCollectorService _styleCollectorService;

        public RoomAxisCreationService(
            RoomCollectorService roomCollectorService,
            RoomBoundaryService roomBoundaryService,
            RoomPlacementCheckService roomPlacementCheckService,
            RoomAngleCheckService roomAngleCheckService,
            RoomCentroidService roomCentroidService,
            RoomAxisDirectionService roomAxisDirectionService,
            RoomAxisClippingService roomAxisClippingService,
            RevitStyleCollectorService styleCollectorService)
        {
            _roomCollectorService = roomCollectorService ?? new RoomCollectorService();
            _roomBoundaryService = roomBoundaryService ?? new RoomBoundaryService();
            _roomPlacementCheckService = roomPlacementCheckService ?? new RoomPlacementCheckService();
            _roomAngleCheckService = roomAngleCheckService ?? new RoomAngleCheckService(_roomBoundaryService);
            _roomCentroidService = roomCentroidService ?? new RoomCentroidService();
            _roomAxisDirectionService = roomAxisDirectionService ?? new RoomAxisDirectionService();
            _roomAxisClippingService = roomAxisClippingService ?? new RoomAxisClippingService();
            _styleCollectorService = styleCollectorService ?? new RevitStyleCollectorService();
        }

        public IList<RoomAxisCreationResult> CreateForSelectedRoom(UIDocument uiDocument, RoomGeometryToolsSettings settings)
        {
            List<RoomAxisCreationResult> results = new List<RoomAxisCreationResult>();

            if (uiDocument == null || uiDocument.Document == null)
            {
                results.Add(CreateGenericFailure("Документ Revit недоступен."));
                return results;
            }

            Document document = uiDocument.Document;
            View activeView = document.ActiveView;

            string validationMessage;
            if (!_roomCollectorService.IsValidPlanViewForDetailCurves(activeView, out validationMessage))
            {
                results.Add(CreateGenericFailure(validationMessage));
                return results;
            }

            Room selectedRoom = _roomCollectorService.GetSelectedRoom(uiDocument);
            if (selectedRoom == null)
            {
                results.Add(CreateGenericFailure("Выберите одно помещение перед запуском команды."));
                return results;
            }

            using (Transaction transaction = new Transaction(document, "SAB Построение осей помещения (выбранное)"))
            {
                transaction.Start();

                GraphicsStyle axisStyle = ResolveAxisLineStyle(document, settings);

                if (settings != null && settings.DeletePreviousAxesBeforeCreation)
                {
                    DeletePreviouslyCreatedAxes(document, activeView, axisStyle);
                }

                RoomAxisCreationResult result = CreateAxesForRoom(document, activeView, selectedRoom, axisStyle, settings);
                results.Add(result);

                transaction.Commit();
            }

            return results;
        }

        public IList<RoomAxisCreationResult> CreateForActiveViewRooms(
            UIDocument uiDocument,
            RoomGeometryToolsSettings settings,
            out int totalRoomsFound)
        {
            totalRoomsFound = 0;
            List<RoomAxisCreationResult> results = new List<RoomAxisCreationResult>();

            if (uiDocument == null || uiDocument.Document == null)
            {
                results.Add(CreateGenericFailure("Документ Revit недоступен."));
                return results;
            }

            Document document = uiDocument.Document;
            View activeView = document.ActiveView;

            string validationMessage;
            if (!_roomCollectorService.IsValidPlanViewForDetailCurves(activeView, out validationMessage))
            {
                results.Add(CreateGenericFailure(validationMessage));
                return results;
            }

            IList<Room> rooms = _roomCollectorService.GetRoomsOnActiveView(document, activeView);
            totalRoomsFound = rooms.Count;

            if (rooms.Count == 0)
            {
                results.Add(CreateGenericFailure("На активном виде не найдены помещения."));
                return results;
            }

            using (Transaction transaction = new Transaction(document, "SAB Построение осей помещений (активный вид)"))
            {
                transaction.Start();

                GraphicsStyle axisStyle = ResolveAxisLineStyle(document, settings);

                if (settings != null && settings.DeletePreviousAxesBeforeCreation)
                {
                    DeletePreviouslyCreatedAxes(document, activeView, axisStyle);
                }

                for (int i = 0; i < rooms.Count; i++)
                {
                    Room room = rooms[i];
                    RoomAxisCreationResult result = CreateAxesForRoom(document, activeView, room, axisStyle, settings);
                    results.Add(result);
                }

                transaction.Commit();
            }

            return results;
        }

        private RoomAxisCreationResult CreateAxesForRoom(
            Document document,
            View activeView,
            Room room,
            GraphicsStyle axisStyle,
            RoomGeometryToolsSettings settings)
        {
            RoomAxisCreationResult baseResult = CreateBaseResult(room);

            IList<RoomPlacementIssue> placementIssues = _roomPlacementCheckService.CheckRoom(room);
            if (placementIssues.Count > 0)
            {
                baseResult.IsSuccess = false;
                baseResult.Message = placementIssues[0].Message;
                return baseResult;
            }

            RoomBoundaryPolygon polygon = _roomBoundaryService.GetRoomBoundaryPolygon(room);
            if (!string.IsNullOrWhiteSpace(polygon.ErrorMessage))
            {
                baseResult.IsSuccess = false;
                baseResult.Message = polygon.ErrorMessage;
                return baseResult;
            }

            if (polygon.HasNonLinearSegments)
            {
                baseResult.IsSuccess = false;
                baseResult.Message = "Граница содержит нелинейные сегменты. Построение осей пропущено.";
                return baseResult;
            }

            // Проверка углов перед построением осей отключена по требованию.
            // Оси строятся для валидной границы помещения даже при углах, отличных от 90°.

            XYZ centroid;
            string centroidError;
            if (!_roomCentroidService.TryCalculateCentroid(room, polygon, out centroid, out centroidError))
            {
                baseResult.IsSuccess = false;
                baseResult.Message = centroidError;
                return baseResult;
            }

            XYZ mainDirection;
            XYZ secondaryDirection;
            string directionError;
            if (!_roomAxisDirectionService.TryGetAxisDirections(polygon, out mainDirection, out secondaryDirection, out directionError))
            {
                baseResult.IsSuccess = false;
                baseResult.Message = directionError;
                return baseResult;
            }

            Line mainAxis;
            string mainClipError;
            if (!_roomAxisClippingService.TryClipAxisByPolygon(centroid, mainDirection, polygon.OuterVertices, out mainAxis, out mainClipError))
            {
                baseResult.IsSuccess = false;
                baseResult.Message = "Ошибка отсечения главной оси: " + mainClipError;
                return baseResult;
            }

            Line secondaryAxis;
            string secondaryClipError;
            if (!_roomAxisClippingService.TryClipAxisByPolygon(centroid, secondaryDirection, polygon.OuterVertices, out secondaryAxis, out secondaryClipError))
            {
                baseResult.IsSuccess = false;
                baseResult.Message = "Ошибка отсечения вторичной оси: " + secondaryClipError;
                return baseResult;
            }

            try
            {
                DetailCurve firstCurve = document.Create.NewDetailCurve(activeView, mainAxis);
                DetailCurve secondCurve = document.Create.NewDetailCurve(activeView, secondaryAxis);

                ApplyAxisCurveStyle(firstCurve, axisStyle);
                ApplyAxisCurveStyle(secondCurve, axisStyle);

                MarkAxisCurve(firstCurve);
                MarkAxisCurve(secondCurve);

                baseResult.IsSuccess = true;
                baseResult.CreatedAxisCount = 2;
                baseResult.Message = "Оси построены успешно.";
                return baseResult;
            }
            catch (Exception exception)
            {
                baseResult.IsSuccess = false;
                baseResult.Message = "Ошибка создания DetailCurve: " + exception.Message;
                return baseResult;
            }
        }

        private GraphicsStyle ResolveAxisLineStyle(Document document, RoomGeometryToolsSettings settings)
        {
            if (document == null)
            {
                return null;
            }

            if (settings != null && settings.SelectedAxisLineStyleId != null && settings.SelectedAxisLineStyleId != ElementId.InvalidElementId)
            {
                GraphicsStyle style = document.GetElement(settings.SelectedAxisLineStyleId) as GraphicsStyle;
                if (style != null && style.IsValidObject)
                {
                    return style;
                }
            }

            IList<RevitStyleItem> styles = _styleCollectorService.GetDetailLineStyles(document);
            RevitStyleItem defaultStyle = _styleCollectorService.ResolveDefaultAxisStyle(styles);
            if (defaultStyle == null || defaultStyle.ElementId == ElementId.InvalidElementId)
            {
                return null;
            }

            return document.GetElement(defaultStyle.ElementId) as GraphicsStyle;
        }

        private static void ApplyAxisCurveStyle(DetailCurve curve, GraphicsStyle style)
        {
            if (curve == null || style == null)
            {
                return;
            }

            try
            {
                curve.LineStyle = style;
            }
            catch
            {
                // Если стиль не применился, оставляем дефолтный стиль Revit.
            }
        }

        private static void MarkAxisCurve(DetailCurve curve)
        {
            if (curve == null)
            {
                return;
            }

            Parameter commentsParameter = curve.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentsParameter == null || commentsParameter.IsReadOnly)
            {
                return;
            }

            commentsParameter.Set(AxisMarkerComment);
        }

        private static void DeletePreviouslyCreatedAxes(Document document, View activeView, GraphicsStyle selectedStyle)
        {
            if (document == null || activeView == null)
            {
                return;
            }

            // Важно: Revit не поддерживает OfClass(typeof(DetailCurve)).
            // Нужно собирать базовый CurveElement и далее фильтровать до DetailCurve.
            FilteredElementCollector collector = new FilteredElementCollector(document, activeView.Id);
            collector.OfClass(typeof(CurveElement));

            List<ElementId> idsToDelete = new List<ElementId>();

            foreach (Element element in collector)
            {
                DetailCurve curve = element as DetailCurve;
                if (curve == null)
                {
                    continue;
                }

                Parameter commentParameter = curve.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                string markerValue = commentParameter != null ? commentParameter.AsString() : string.Empty;
                bool hasMarker = string.Equals(markerValue, AxisMarkerComment, StringComparison.Ordinal);

                if (!hasMarker)
                {
                    continue;
                }

                if (selectedStyle != null)
                {
                    GraphicsStyle lineStyle = curve.LineStyle as GraphicsStyle;
                    if (lineStyle == null || lineStyle.Id != selectedStyle.Id)
                    {
                        continue;
                    }
                }

                idsToDelete.Add(curve.Id);
            }

            if (idsToDelete.Count > 0)
            {
                document.Delete(idsToDelete);
            }
        }

        private static RoomAxisCreationResult CreateBaseResult(Room room)
        {
            Level level = room != null ? room.Document.GetElement(room.LevelId) as Level : null;

            return new RoomAxisCreationResult
            {
                RoomId = room != null ? room.Id : ElementId.InvalidElementId,
                RoomNumber = RevitParameterUtils.GetRoomNumber(room),
                RoomName = RevitParameterUtils.GetRoomName(room),
                LevelName = level != null ? level.Name : "Без уровня",
                IsSuccess = false,
                CreatedAxisCount = 0,
                Message = string.Empty
            };
        }

        private static RoomAxisCreationResult CreateGenericFailure(string message)
        {
            return new RoomAxisCreationResult
            {
                RoomId = ElementId.InvalidElementId,
                RoomNumber = string.Empty,
                RoomName = string.Empty,
                LevelName = string.Empty,
                IsSuccess = false,
                CreatedAxisCount = 0,
                Message = message ?? string.Empty
            };
        }
    }
}
