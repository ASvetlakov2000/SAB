using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис создания диагностической графики для помещений с проблемными углами.
    /// </summary>
    public class RoomDiagnosticGraphicsService
    {
        private readonly RoomBoundaryService _roomBoundaryService;

        public RoomDiagnosticGraphicsService(RoomBoundaryService roomBoundaryService)
        {
            _roomBoundaryService = roomBoundaryService ?? new RoomBoundaryService();
        }

        public IList<string> CreateDiagnostics(
            Document document,
            IList<RoomAngleIssue> angleIssues,
            ElementId angularDimensionTypeId)
        {
            List<string> warnings = new List<string>();

            if (document == null)
            {
                warnings.Add("Документ недоступен для создания диагностических видов.");
                return warnings;
            }

            if (angleIssues == null || angleIssues.Count == 0)
            {
                warnings.Add("Список проблемных углов пуст. Диагностика не требуется.");
                return warnings;
            }

            ElementId filledRegionTypeId = GetDefaultFilledRegionTypeId(document);
            ElementId textNoteTypeId = document.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
            DimensionType angularDimensionType = document.GetElement(angularDimensionTypeId) as DimensionType;

            Dictionary<int, ViewPlan> diagnosticViewsByLevel = new Dictionary<int, ViewPlan>();

            Dictionary<int, List<RoomAngleIssue>> issuesByRoom = GroupIssuesByRoom(angleIssues);

            foreach (KeyValuePair<int, List<RoomAngleIssue>> pair in issuesByRoom)
            {
                RoomAngleIssue firstIssue = pair.Value[0];
                Room room = document.GetElement(firstIssue.RoomId) as Room;

                if (room == null)
                {
                    warnings.Add("Не удалось найти помещение Id=" + firstIssue.RoomId.IntegerValue + " для диагностики.");
                    continue;
                }

                Level level = document.GetElement(room.LevelId) as Level;
                if (level == null)
                {
                    warnings.Add("Не удалось определить уровень помещения Id=" + room.Id.IntegerValue + ".");
                    continue;
                }

                ViewPlan diagnosticView;
                if (!TryGetOrCreateDiagnosticView(document, level, diagnosticViewsByLevel, out diagnosticView, out string viewError))
                {
                    warnings.Add(viewError);
                    continue;
                }

                RoomBoundaryPolygon polygon = _roomBoundaryService.GetRoomBoundaryPolygon(room);
                if (!string.IsNullOrWhiteSpace(polygon.ErrorMessage))
                {
                    warnings.Add("[" + firstIssue.RoomNumber + "] " + polygon.ErrorMessage);
                    continue;
                }

                DrawBoundaryCurves(document, diagnosticView, polygon, warnings, firstIssue);
                CreateFilledRegion(document, diagnosticView, polygon, filledRegionTypeId, warnings, firstIssue);

                for (int issueIndex = 0; issueIndex < pair.Value.Count; issueIndex++)
                {
                    RoomAngleIssue roomIssue = pair.Value[issueIndex];
                    if (!TryCreateAngularDimension(document, diagnosticView, roomIssue, angularDimensionType, warnings))
                    {
                        CreateTextFallback(document, diagnosticView, roomIssue, textNoteTypeId, warnings);
                    }
                }
            }

            return warnings;
        }

        private static Dictionary<int, List<RoomAngleIssue>> GroupIssuesByRoom(IList<RoomAngleIssue> issues)
        {
            Dictionary<int, List<RoomAngleIssue>> grouped = new Dictionary<int, List<RoomAngleIssue>>();

            for (int i = 0; i < issues.Count; i++)
            {
                RoomAngleIssue issue = issues[i];
                if (issue == null || issue.RoomId == null || issue.RoomId == ElementId.InvalidElementId)
                {
                    continue;
                }

                int roomId = issue.RoomId.IntegerValue;

                if (!grouped.ContainsKey(roomId))
                {
                    grouped[roomId] = new List<RoomAngleIssue>();
                }

                grouped[roomId].Add(issue);
            }

            return grouped;
        }

        private static bool TryGetOrCreateDiagnosticView(
            Document document,
            Level level,
            IDictionary<int, ViewPlan> cache,
            out ViewPlan diagnosticView,
            out string errorText)
        {
            diagnosticView = null;
            errorText = string.Empty;

            if (cache.TryGetValue(level.Id.IntegerValue, out diagnosticView))
            {
                return true;
            }

            string targetName = "SA_Проверка помещений_" + level.Name;

            FilteredElementCollector existingViews = new FilteredElementCollector(document).OfClass(typeof(ViewPlan));
            foreach (Element viewElement in existingViews)
            {
                ViewPlan candidate = viewElement as ViewPlan;
                if (candidate == null || candidate.IsTemplate)
                {
                    continue;
                }

                if (candidate.ViewType != ViewType.FloorPlan)
                {
                    continue;
                }

                if (string.Equals(candidate.Name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    diagnosticView = candidate;
                    cache[level.Id.IntegerValue] = candidate;
                    return true;
                }
            }

            ElementId floorPlanTypeId = GetFloorPlanViewTypeId(document);
            if (floorPlanTypeId == ElementId.InvalidElementId)
            {
                errorText = "Не найден тип вида Floor Plan для создания диагностического вида уровня " + level.Name + ".";
                return false;
            }

            try
            {
                ViewPlan createdView = ViewPlan.Create(document, floorPlanTypeId, level.Id);
                createdView.Name = BuildUniqueViewName(document, targetName);
                diagnosticView = createdView;
                cache[level.Id.IntegerValue] = createdView;
                return true;
            }
            catch (Exception exception)
            {
                errorText = "Ошибка создания диагностического вида для уровня " + level.Name + ": " + exception.Message;
                return false;
            }
        }

        private static string BuildUniqueViewName(Document document, string baseName)
        {
            string candidate = baseName;
            int suffix = 1;

            while (IsViewNameExists(document, candidate))
            {
                candidate = baseName + "_" + suffix;
                suffix++;
            }

            return candidate;
        }

        private static bool IsViewNameExists(Document document, string name)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null)
                {
                    continue;
                }

                if (string.Equals(view.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ElementId GetFloorPlanViewTypeId(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType));

            foreach (Element element in collector)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;
                if (viewFamilyType != null && viewFamilyType.ViewFamily == ViewFamily.FloorPlan)
                {
                    return viewFamilyType.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private static ElementId GetDefaultFilledRegionTypeId(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(FilledRegionType));
            foreach (Element element in collector)
            {
                FilledRegionType type = element as FilledRegionType;
                if (type != null)
                {
                    return type.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private static IList<DetailCurve> DrawBoundaryCurves(
            Document document,
            View view,
            RoomBoundaryPolygon polygon,
            IList<string> warnings,
            RoomAngleIssue issue)
        {
            List<DetailCurve> curves = new List<DetailCurve>();

            if (polygon == null || polygon.OuterVertices == null || polygon.OuterVertices.Count < 2)
            {
                return curves;
            }

            try
            {
                for (int i = 0; i < polygon.OuterVertices.Count; i++)
                {
                    XYZ start = polygon.OuterVertices[i];
                    XYZ end = polygon.OuterVertices[(i + 1) % polygon.OuterVertices.Count];
                    Line line = Line.CreateBound(new XYZ(start.X, start.Y, 0.0), new XYZ(end.X, end.Y, 0.0));
                    DetailCurve detailCurve = document.Create.NewDetailCurve(view, line);
                    curves.Add(detailCurve);
                }
            }
            catch (Exception exception)
            {
                warnings.Add("[" + issue.RoomNumber + "] Ошибка создания контуров DetailCurve: " + exception.Message);
            }

            return curves;
        }

        private static void CreateFilledRegion(
            Document document,
            View view,
            RoomBoundaryPolygon polygon,
            ElementId filledRegionTypeId,
            IList<string> warnings,
            RoomAngleIssue issue)
        {
            if (filledRegionTypeId == ElementId.InvalidElementId)
            {
                warnings.Add("Не найден FilledRegionType для диагностической заливки.");
                return;
            }

            if (polygon == null || polygon.OuterVertices == null || polygon.OuterVertices.Count < 3)
            {
                return;
            }

            try
            {
                CurveLoop outerLoop = new CurveLoop();
                for (int i = 0; i < polygon.OuterVertices.Count; i++)
                {
                    XYZ start = polygon.OuterVertices[i];
                    XYZ end = polygon.OuterVertices[(i + 1) % polygon.OuterVertices.Count];
                    outerLoop.Append(Line.CreateBound(new XYZ(start.X, start.Y, 0.0), new XYZ(end.X, end.Y, 0.0)));
                }

                List<CurveLoop> loops = new List<CurveLoop> { outerLoop };
                FilledRegion.Create(document, filledRegionTypeId, view.Id, loops);
            }
            catch (Exception exception)
            {
                warnings.Add("[" + issue.RoomNumber + "] Ошибка создания FilledRegion: " + exception.Message);
            }
        }

        private static bool TryCreateAngularDimension(
            Document document,
            View view,
            RoomAngleIssue issue,
            DimensionType angularDimensionType,
            IList<string> warnings)
        {
            if (issue == null || angularDimensionType == null)
            {
                return false;
            }

            try
            {
                XYZ vertex = issue.VertexPoint;
                XYZ firstDirection = BuildDirection(vertex, issue.FirstSegmentStart, issue.FirstSegmentEnd);
                XYZ secondDirection = BuildDirection(vertex, issue.SecondSegmentStart, issue.SecondSegmentEnd);

                if (firstDirection.GetLength() < 1e-9 || secondDirection.GetLength() < 1e-9)
                {
                    return false;
                }

                double armLength = RevitUnitUtils.MillimetersToInternal(600.0);
                XYZ firstEnd = vertex + firstDirection * armLength;
                XYZ secondEnd = vertex + secondDirection * armLength;

                DetailCurve firstArm = document.Create.NewDetailCurve(view, Line.CreateBound(vertex, firstEnd));
                DetailCurve secondArm = document.Create.NewDetailCurve(view, Line.CreateBound(vertex, secondEnd));

                Reference firstReference = firstArm.GeometryCurve.Reference;
                Reference secondReference = secondArm.GeometryCurve.Reference;

                XYZ bisectorDirection = (firstDirection + secondDirection);
                if (bisectorDirection.GetLength() < 1e-9)
                {
                    bisectorDirection = new XYZ(-firstDirection.Y, firstDirection.X, 0.0);
                }

                bisectorDirection = new XYZ(bisectorDirection.X, bisectorDirection.Y, 0.0).Normalize();
                XYZ arcStart = vertex + firstDirection * (armLength * 0.7);
                XYZ arcEnd = vertex + secondDirection * (armLength * 0.7);
                XYZ arcMiddle = vertex + bisectorDirection * (armLength * 0.7);

                Arc extensionArc = Arc.Create(arcStart, arcEnd, arcMiddle);

                object creator = document.Create;
                if (creator == null)
                {
                    return false;
                }

                MethodInfo[] methods = creator.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "NewAngularDimension", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();

                    try
                    {
                        if (parameters.Length == 5)
                        {
                            object created = method.Invoke(creator, new object[] { view, extensionArc, firstReference, secondReference, angularDimensionType });
                            if (created != null)
                            {
                                return true;
                            }
                        }
                        else if (parameters.Length == 4)
                        {
                            object created = method.Invoke(creator, new object[] { view, extensionArc, firstReference, secondReference });
                            if (created != null)
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Продолжаем поиск следующей сигнатуры.
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                warnings.Add("[" + issue.RoomNumber + "] Не удалось создать угловой размер: " + exception.Message);
                return false;
            }
        }

        private static XYZ BuildDirection(XYZ vertex, XYZ segmentPoint1, XYZ segmentPoint2)
        {
            XYZ first = segmentPoint1 - vertex;
            XYZ second = segmentPoint2 - vertex;
            XYZ candidate = first.GetLength() > second.GetLength() ? first : second;

            XYZ horizontal = new XYZ(candidate.X, candidate.Y, 0.0);
            if (horizontal.GetLength() < 1e-9)
            {
                return XYZ.Zero;
            }

            return horizontal.Normalize();
        }

        private static void CreateTextFallback(
            Document document,
            View view,
            RoomAngleIssue issue,
            ElementId textNoteTypeId,
            IList<string> warnings)
        {
            if (textNoteTypeId == ElementId.InvalidElementId || issue == null)
            {
                warnings.Add("Не удалось создать TextNote fallback: отсутствует TextNoteType.");
                return;
            }

            try
            {
                string text = issue.ActualAngleDegrees.ToString("0.##") + "° ≠ 90°";
                XYZ textPoint = issue.VertexPoint + new XYZ(RevitUnitUtils.MillimetersToInternal(350.0), RevitUnitUtils.MillimetersToInternal(350.0), 0.0);
                TextNote.Create(document, view.Id, textPoint, text, textNoteTypeId);
            }
            catch (Exception exception)
            {
                warnings.Add("[" + issue.RoomNumber + "] Не удалось создать TextNote fallback: " + exception.Message);
            }
        }
    }
}
