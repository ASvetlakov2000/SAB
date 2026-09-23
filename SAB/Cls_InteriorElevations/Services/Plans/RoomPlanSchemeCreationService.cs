using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Plans
{
    /// <summary>
    /// Сервис создания план-схем помещений на основе контуров помещений.
    /// </summary>
    public class RoomPlanSchemeCreationService
    {
        private const double PointToleranceFeet = 1e-4;
        private const double BoundaryConnectionToleranceMm = 5.0;

        /// <summary>
        /// Основной метод создания план-схем.
        /// Если передан manualBoundaryLoop, он используется как граница обрезки вместо границы помещения.
        /// </summary>
        public RoomPlanSchemeCreationSummary CreateRoomPlanSchemes(
            Document document,
            ViewPlan sourcePlanView,
            IList<Room> rooms,
            RoomPlanSchemeSettings settings,
            CurveLoop manualBoundaryLoop = null,
            Transform roomToHost = null)
        {
            RoomPlanSchemeCreationSummary summary = new RoomPlanSchemeCreationSummary();

            if (document == null || sourcePlanView == null || rooms == null || settings == null)
            {
                summary.Warnings.Add("Недостаточно входных данных для создания план-схем.");
                return summary;
            }

            summary.ProcessedRoomsCount = rooms.Count;

            HashSet<string> existingViewNames = CollectExistingViewNames(document);
            ElementId sourceViewTypeId = sourcePlanView.GetTypeId();
            ElementId levelId = sourcePlanView.GenLevel != null ? sourcePlanView.GenLevel.Id : ElementId.InvalidElementId;

            if (sourceViewTypeId == ElementId.InvalidElementId || levelId == ElementId.InvalidElementId)
            {
                summary.Warnings.Add("Не удалось определить тип вида или уровень активного плана.");
                summary.SkippedRoomsCount = rooms.Count;
                return summary;
            }

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                Room room = rooms[roomIndex];
                if (room == null)
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add("Помещение с индексом " + roomIndex + " не найдено.");
                    continue;
                }

                if (!IsRoomValidForScheme(room, out string roomValidationError))
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add(BuildRoomPrefix(room) + roomValidationError);
                    continue;
                }

                CurveLoop sourceRoomLoop = null;
                bool useManualBoundary = manualBoundaryLoop != null;
                if (!useManualBoundary)
                {
                    Transform boundaryTransform = BuildRoomBoundaryTransform(room, sourcePlanView, roomToHost);
                    if (!TryGetRoomOuterBoundaryLoop(
                            room,
                            sourcePlanView,
                            boundaryTransform,
                            out sourceRoomLoop,
                            out bool usedRectangularBoundaryFallback,
                            out string boundaryError))
                    {
                        summary.SkippedRoomsCount++;
                        summary.Warnings.Add(BuildRoomPrefix(room) + boundaryError);
                        continue;
                    }

                    if (usedRectangularBoundaryFallback)
                    {
                        summary.Warnings.Add(
                            BuildRoomPrefix(room) +
                            "Контур помещения имеет разрывы. Для границы план-схемы использован прямоугольник по габаритам помещения.");
                    }
                }

                // Блок создания отдельного вида-плана для текущего помещения.
                ViewPlan createdView;
                try
                {
                    createdView = ViewPlan.Create(document, sourceViewTypeId, levelId);
                }
                catch (Exception createViewException)
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add(BuildRoomPrefix(room) + "Не удалось создать вид: " + createViewException.Message);
                    continue;
                }

                if (createdView == null)
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add(BuildRoomPrefix(room) + "Созданный вид равен null.");
                    continue;
                }

                // Блок назначения имени вида по формуле.
                string baseName = BuildViewName(room, settings);
                string uniqueName = GenerateUniqueViewName(existingViewNames, baseName);

                try
                {
                    createdView.Name = uniqueName;
                }
                catch (Exception renameException)
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add(BuildRoomPrefix(room) + "Не удалось присвоить имя виду: " + renameException.Message);
                    TryDeleteView(document, createdView.Id);
                    continue;
                }

                existingViewNames.Add(uniqueName);

                // Сначала применяем шаблон (если выбран), чтобы корректно определить,
                // контролируется ли параметр масштаба шаблоном вида.
                if (settings.ViewTemplateId != null && settings.ViewTemplateId != ElementId.InvalidElementId)
                {
                    try
                    {
                        createdView.ViewTemplateId = settings.ViewTemplateId;
                    }
                    catch (Exception templateException)
                    {
                        summary.Warnings.Add(BuildRoomPrefix(room) + "Не удалось применить шаблон: " + templateException.Message);
                    }
                }

                // Блок установки масштаба план-схемы.
                // Правило:
                // 1) если масштаб контролируется шаблоном (выбранным или дефолтным у типа вида), значение из окна не применяем;
                // 2) если шаблона нет или он не контролирует масштаб, применяем значение из окна (или fallback к масштабу активного плана).
                int planViewScale = settings.ViewScale > 0 ? settings.ViewScale : sourcePlanView.Scale;
                TryApplyScaleRespectTemplate(document, createdView, planViewScale, BuildRoomPrefix(room), summary.Warnings);

                CurveLoop cropSourceLoop = useManualBoundary ? manualBoundaryLoop : sourceRoomLoop;
                if (!TryApplyRoomCrop(
                        createdView,
                        cropSourceLoop,
                        settings.CropOffsetMm,
                        useManualBoundary,
                        out string cropError))
                {
                    summary.SkippedRoomsCount++;
                    summary.Warnings.Add(BuildRoomPrefix(room) + cropError);

                    // Fallback-режим: создаем линии для ручной правки только если граница шла от помещения.
                    if (!useManualBoundary)
                    {
                        int helperLinesCount = CreateHelperBoundaryLines(
                            document,
                            sourcePlanView,
                            sourceRoomLoop,
                            settings.CropOffsetMm,
                            out string helperLinesError);

                        summary.ManualBoundaryRequired = true;
                        summary.HelperBoundaryLinesCount += helperLinesCount;

                        if (!string.IsNullOrWhiteSpace(helperLinesError))
                        {
                            summary.Warnings.Add(BuildRoomPrefix(room) + helperLinesError);
                        }
                    }

                    TryDeleteView(document, createdView.Id);
                    continue;
                }

                summary.CreatedViewsCount++;
                summary.CreatedViewIds.Add(createdView.Id);
            }

            return summary;
        }

        private static Transform BuildRoomBoundaryTransform(Room room, ViewPlan sourcePlanView, Transform roomToHost)
        {
            Transform linkTransform = roomToHost ?? Transform.Identity;
            if (room == null || sourcePlanView == null || room.Document == sourcePlanView.Document ||
                sourcePlanView.GenLevel == null)
            {
                return linkTransform;
            }

            Level roomLevel = room.Document.GetElement(room.LevelId) as Level;
            if (roomLevel == null)
            {
                return linkTransform;
            }

            double roomLevelInHost = linkTransform.OfPoint(new XYZ(0.0, 0.0, roomLevel.Elevation)).Z;
            double verticalShift = sourcePlanView.GenLevel.Elevation - roomLevelInHost;
            return Transform.CreateTranslation(new XYZ(0.0, 0.0, verticalShift)).Multiply(linkTransform);
        }

        private static bool IsRoomValidForScheme(Room room, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (room == null)
            {
                errorMessage = "Помещение недоступно.";
                return false;
            }

            if (room.Location == null)
            {
                errorMessage = "Помещение не размещено.";
                return false;
            }

            if (room.Area <= 0)
            {
                errorMessage = "Площадь помещения должна быть больше нуля.";
                return false;
            }

            return true;
        }

        private static string BuildViewName(Room room, RoomPlanSchemeSettings settings)
        {
            string roomNumber = GetRoomNumber(room);
            string roomName = GetRoomName(room);

            string part1 = settings != null ? settings.NamePart1 : string.Empty;
            string part2Template = settings != null ? settings.NamePart2 : string.Empty;
            string part3Template = settings != null ? settings.NamePart3 : string.Empty;

            string part2 = ResolveFormulaPart(part2Template, roomNumber, roomName);
            string part3 = ResolveFormulaPart(part3Template, roomNumber, roomName);

            // Блок сборки имени: пустые части формулы полностью пропускаем.
            StringBuilder builder = new StringBuilder();
            AppendIfNotEmpty(builder, part1);
            AppendIfNotEmpty(builder, part2);
            AppendIfNotEmpty(builder, part3);

            string rawName = builder.ToString().Trim();
            return RevitNameUtils.SanitizeName(rawName, "План-схема разверток пом.");
        }

        private static void AppendIfNotEmpty(StringBuilder builder, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.Append(value);
        }

        private static string ResolveFormulaPart(string template, string roomNumber, string roomName)
        {
            string value = template ?? string.Empty;

            if (string.Equals(value.Trim(), "Номер помещения", StringComparison.OrdinalIgnoreCase))
            {
                return roomNumber ?? string.Empty;
            }

            if (string.Equals(value.Trim(), "Имя помещения", StringComparison.OrdinalIgnoreCase))
            {
                return roomName ?? string.Empty;
            }

            value = value.Replace("[Номер помещения]", roomNumber ?? string.Empty);
            value = value.Replace("[Имя помещения]", roomName ?? string.Empty);
            value = value.Replace("{Номер помещения}", roomNumber ?? string.Empty);
            value = value.Replace("{Имя помещения}", roomName ?? string.Empty);
            return value;
        }

        private static string GetRoomNumber(Room room)
        {
            if (room == null)
            {
                return string.Empty;
            }

            Parameter parameter = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            string value = parameter != null ? parameter.AsString() : string.Empty;
            return RevitNameUtils.SanitizeName(value, "Без номера");
        }

        private static string GetRoomName(Room room)
        {
            if (room == null)
            {
                return string.Empty;
            }

            Parameter parameter = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            string value = parameter != null ? parameter.AsString() : room.Name;
            return RevitNameUtils.SanitizeName(value, "Без имени");
        }

        private static string BuildRoomPrefix(Room room)
        {
            string number = GetRoomNumber(room);
            string name = GetRoomName(room);
            int id = room != null && room.Id != null ? room.Id.IntegerValue : -1;
            return "[Помещение " + number + " | " + name + " | Id:" + id + "] ";
        }

        private static HashSet<string> CollectExistingViewNames(Document document)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document == null)
            {
                return names;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(view.Name))
                {
                    continue;
                }

                names.Add(view.Name.Trim());
            }

            return names;
        }

        private static string GenerateUniqueViewName(HashSet<string> existingNames, string baseName)
        {
            string safeBaseName = RevitNameUtils.SanitizeName(baseName, "План-схема разверток пом.");
            if (existingNames == null || !existingNames.Contains(safeBaseName))
            {
                return safeBaseName;
            }

            int suffix = 1;
            while (true)
            {
                string candidate = safeBaseName + "_" + suffix;
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private static bool TryGetRoomOuterBoundaryLoop(
            Room room,
            ViewPlan sourcePlanView,
            Transform roomToHost,
            out CurveLoop outerLoop,
            out bool usedRectangularFallback,
            out string errorMessage)
        {
            outerLoop = null;
            usedRectangularFallback = false;
            errorMessage = string.Empty;

            if (room == null)
            {
                errorMessage = "Помещение недоступно.";
                return false;
            }

            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> loops = room.GetBoundarySegments(options);
            if (loops == null || loops.Count == 0)
            {
                errorMessage = "Граница помещения не найдена.";
                return false;
            }

            double maxArea = double.MinValue;
            CurveLoop selectedLoop = null;
            bool selectedLoopUsesRectangularFallback = false;

            for (int i = 0; i < loops.Count; i++)
            {
                IList<BoundarySegment> segments = loops[i];
                if (segments == null || segments.Count == 0)
                {
                    continue;
                }

                List<Curve> boundaryCurves = new List<Curve>();

                for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
                {
                    BoundarySegment segment = segments[segmentIndex];
                    if (segment == null)
                    {
                        continue;
                    }

                    Curve sourceCurve = segment.GetCurve();
                    if (sourceCurve == null)
                    {
                        continue;
                    }

                    try
                    {
                        boundaryCurves.Add(sourceCurve.CreateTransformed(roomToHost ?? Transform.Identity));
                    }
                    catch
                    {
                        // Поврежденный сегмент связи не должен прерывать построение остальных границ.
                    }
                }

                if (boundaryCurves.Count == 0)
                {
                    continue;
                }

                CurveLoop curveLoop;
                List<XYZ> polygon;
                bool usesRectangularFallback = false;
                if (!TryBuildConnectedBoundaryLoop(boundaryCurves, out curveLoop, out polygon))
                {
                    if (!TryBuildBoundaryRectangle(
                            room,
                            sourcePlanView,
                            roomToHost,
                            boundaryCurves,
                            out curveLoop,
                            out polygon))
                    {
                        continue;
                    }

                    usesRectangularFallback = true;
                }

                double loopArea = Math.Abs(CalculatePolygonArea(polygon));
                if (loopArea > maxArea)
                {
                    maxArea = loopArea;
                    selectedLoop = curveLoop;
                    selectedLoopUsesRectangularFallback = usesRectangularFallback;
                }
            }

            if (selectedLoop == null)
            {
                errorMessage = "Не удалось получить внешний контур помещения.";
                return false;
            }

            outerLoop = selectedLoop;
            usedRectangularFallback = selectedLoopUsesRectangularFallback;
            return true;
        }

        private static bool TryBuildConnectedBoundaryLoop(
            IList<Curve> sourceCurves,
            out CurveLoop curveLoop,
            out List<XYZ> polygon)
        {
            curveLoop = null;
            polygon = new List<XYZ>();

            if (sourceCurves == null || sourceCurves.Count < 3)
            {
                return false;
            }

            try
            {
                List<Curve> remainingCurves = new List<Curve>();
                for (int i = 0; i < sourceCurves.Count; i++)
                {
                    if (sourceCurves[i] != null)
                    {
                        remainingCurves.Add(sourceCurves[i].Clone());
                    }
                }

                if (remainingCurves.Count < 3)
                {
                    return false;
                }

                List<Curve> orderedCurves = new List<Curve>();
                orderedCurves.Add(remainingCurves[0]);
                remainingCurves.RemoveAt(0);

                double connectionTolerance = UnitConversionUtils.MillimetersToFeet(BoundaryConnectionToleranceMm);
                while (remainingCurves.Count > 0)
                {
                    XYZ currentEnd = orderedCurves[orderedCurves.Count - 1].GetEndPoint(1);
                    int bestIndex = -1;
                    bool reverseBestCurve = false;
                    double bestDistance = double.MaxValue;

                    for (int curveIndex = 0; curveIndex < remainingCurves.Count; curveIndex++)
                    {
                        Curve candidate = remainingCurves[curveIndex];
                        double startDistance = currentEnd.DistanceTo(candidate.GetEndPoint(0));
                        if (startDistance < bestDistance)
                        {
                            bestDistance = startDistance;
                            bestIndex = curveIndex;
                            reverseBestCurve = false;
                        }

                        double endDistance = currentEnd.DistanceTo(candidate.GetEndPoint(1));
                        if (endDistance < bestDistance)
                        {
                            bestDistance = endDistance;
                            bestIndex = curveIndex;
                            reverseBestCurve = true;
                        }
                    }

                    if (bestIndex < 0 || bestDistance > connectionTolerance)
                    {
                        return false;
                    }

                    Curve nextCurve = remainingCurves[bestIndex];
                    remainingCurves.RemoveAt(bestIndex);
                    orderedCurves.Add(reverseBestCurve ? nextCurve.CreateReversed() : nextCurve);
                }

                XYZ loopStart = orderedCurves[0].GetEndPoint(0);
                XYZ loopEnd = orderedCurves[orderedCurves.Count - 1].GetEndPoint(1);
                if (loopStart.DistanceTo(loopEnd) > connectionTolerance)
                {
                    return false;
                }

                CurveLoop connectedLoop = new CurveLoop();
                for (int i = 0; i < orderedCurves.Count; i++)
                {
                    Curve curve = orderedCurves[i];
                    connectedLoop.Append(curve);
                    XYZ startPoint = curve.GetEndPoint(0);
                    polygon.Add(new XYZ(startPoint.X, startPoint.Y, 0));
                }

                if (connectedLoop.IsOpen())
                {
                    polygon.Clear();
                    return false;
                }

                curveLoop = connectedLoop;
                return polygon.Count >= 3;
            }
            catch
            {
                curveLoop = null;
                polygon.Clear();
                return false;
            }
        }

        private static bool TryBuildBoundaryRectangle(
            Room room,
            ViewPlan sourcePlanView,
            Transform roomToHost,
            IList<Curve> sourceCurves,
            out CurveLoop curveLoop,
            out List<XYZ> polygon)
        {
            curveLoop = null;
            polygon = new List<XYZ>();

            try
            {
                List<XYZ> vertices = new List<XYZ>();

                if (sourceCurves != null)
                {
                    for (int i = 0; i < sourceCurves.Count; i++)
                    {
                        Curve curve = sourceCurves[i];
                        if (curve == null)
                        {
                            continue;
                        }

                        try
                        {
                            IList<XYZ> tessellatedPoints = curve.Tessellate();
                            if (tessellatedPoints != null)
                            {
                                for (int pointIndex = 0; pointIndex < tessellatedPoints.Count; pointIndex++)
                                {
                                    vertices.Add(tessellatedPoints[pointIndex]);
                                }
                            }
                        }
                        catch
                        {
                            try
                            {
                                vertices.Add(curve.GetEndPoint(0));
                                vertices.Add(curve.GetEndPoint(1));
                            }
                            catch
                            {
                                // Один поврежденный сегмент не должен отменять прямоугольный fallback.
                            }
                        }
                    }
                }

                if (vertices.Count < 3)
                {
                    AddRoomBoundingBoxVertices(room, sourcePlanView, roomToHost, vertices);
                }

                if (vertices.Count < 3)
                {
                    return false;
                }

                if (!TryGetPlanDirections(sourcePlanView, out XYZ dirX, out XYZ dirY))
                {
                    return false;
                }

                XYZ origin = vertices[0];
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                double minY = double.MaxValue;
                double maxY = double.MinValue;

                for (int i = 0; i < vertices.Count; i++)
                {
                    XYZ local = ToLocal(vertices[i], origin, dirX, dirY);
                    if (local.X < minX) minX = local.X;
                    if (local.X > maxX) maxX = local.X;
                    if (local.Y < minY) minY = local.Y;
                    if (local.Y > maxY) maxY = local.Y;
                }

                if ((maxX - minX) <= PointToleranceFeet || (maxY - minY) <= PointToleranceFeet)
                {
                    return false;
                }

                double z = origin.Z;
                XYZ p1 = ToWorld(minX, minY, origin, dirX, dirY, z);
                XYZ p2 = ToWorld(maxX, minY, origin, dirX, dirY, z);
                XYZ p3 = ToWorld(maxX, maxY, origin, dirX, dirY, z);
                XYZ p4 = ToWorld(minX, maxY, origin, dirX, dirY, z);

                CurveLoop rectangle = new CurveLoop();
                rectangle.Append(Line.CreateBound(p1, p2));
                rectangle.Append(Line.CreateBound(p2, p3));
                rectangle.Append(Line.CreateBound(p3, p4));
                rectangle.Append(Line.CreateBound(p4, p1));

                polygon.Add(p1);
                polygon.Add(p2);
                polygon.Add(p3);
                polygon.Add(p4);
                curveLoop = rectangle;
                return true;
            }
            catch
            {
                curveLoop = null;
                polygon.Clear();
                return false;
            }
        }

        private static bool TryGetPlanDirections(ViewPlan sourcePlanView, out XYZ dirX, out XYZ dirY)
        {
            dirX = XYZ.BasisX;
            dirY = XYZ.BasisY;

            try
            {
                XYZ right = sourcePlanView != null ? sourcePlanView.RightDirection : XYZ.BasisX;
                XYZ up = sourcePlanView != null ? sourcePlanView.UpDirection : XYZ.BasisY;
                XYZ flatRight = new XYZ(right.X, right.Y, 0);
                XYZ flatUp = new XYZ(up.X, up.Y, 0);
                if (flatRight.GetLength() <= PointToleranceFeet || flatUp.GetLength() <= PointToleranceFeet)
                {
                    return false;
                }

                dirX = flatRight.Normalize();
                XYZ orthogonalUp = flatUp - dirX.Multiply(flatUp.DotProduct(dirX));
                if (orthogonalUp.GetLength() <= PointToleranceFeet)
                {
                    orthogonalUp = new XYZ(-dirX.Y, dirX.X, 0);
                }

                dirY = orthogonalUp.Normalize();
                return true;
            }
            catch
            {
                dirX = XYZ.BasisX;
                dirY = XYZ.BasisY;
                return sourcePlanView == null;
            }
        }

        private static void AddRoomBoundingBoxVertices(
            Room room,
            ViewPlan sourcePlanView,
            Transform roomToHost,
            IList<XYZ> vertices)
        {
            if (room == null || vertices == null)
            {
                return;
            }

            try
            {
                ViewPlan roomDocumentView = sourcePlanView != null && room.Document == sourcePlanView.Document
                    ? sourcePlanView
                    : null;
                BoundingBoxXYZ boundingBox = room.get_BoundingBox(roomDocumentView);
                if (boundingBox == null)
                {
                    boundingBox = room.get_BoundingBox(null);
                }

                if (boundingBox == null)
                {
                    return;
                }

                Transform transform = boundingBox.Transform ?? Transform.Identity;
                Transform hostTransform = roomToHost ?? Transform.Identity;
                double z = boundingBox.Min.Z;
                vertices.Add(hostTransform.OfPoint(transform.OfPoint(new XYZ(boundingBox.Min.X, boundingBox.Min.Y, z))));
                vertices.Add(hostTransform.OfPoint(transform.OfPoint(new XYZ(boundingBox.Max.X, boundingBox.Min.Y, z))));
                vertices.Add(hostTransform.OfPoint(transform.OfPoint(new XYZ(boundingBox.Max.X, boundingBox.Max.Y, z))));
                vertices.Add(hostTransform.OfPoint(transform.OfPoint(new XYZ(boundingBox.Min.X, boundingBox.Max.Y, z))));
            }
            catch
            {
                // Если даже BoundingBox помещения недоступен, вызывающий метод пропустит план-схему.
            }
        }

        private static double CalculatePolygonArea(IList<XYZ> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return 0;
            }

            double area2 = 0.0;
            for (int i = 0; i < polygon.Count; i++)
            {
                XYZ current = polygon[i];
                XYZ next = polygon[(i + 1) % polygon.Count];
                area2 += (current.X * next.Y) - (next.X * current.Y);
            }

            return area2 * 0.5;
        }

        private static bool TryApplyRoomCrop(
            ViewPlan viewPlan,
            CurveLoop sourceLoop,
            double cropOffsetMm,
            bool isManualBoundary,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (viewPlan == null)
            {
                errorMessage = "Созданный вид недоступен.";
                return false;
            }

            if (sourceLoop == null)
            {
                errorMessage = "Не получен контур для границы вида.";
                return false;
            }

            try
            {
                viewPlan.CropBoxActive = true;
                viewPlan.CropBoxVisible = true;
            }
            catch
            {
                // Если параметры блокированы шаблоном, пробуем продолжить.
            }

            ViewCropRegionShapeManager cropManager = viewPlan.GetCropRegionShapeManager();
            if (cropManager == null)
            {
                errorMessage = "У вида недоступен менеджер границы обрезки.";
                return false;
            }

            // Если пользователь передал ручной контур, используем его как есть, без дополнительного отступа.
            if (isManualBoundary)
            {
                try
                {
                    cropManager.SetCropShape(sourceLoop);
                    return true;
                }
                catch (Exception manualException)
                {
                    errorMessage = "Не удалось применить выбранные линии как границу вида: " + manualException.Message;
                    return false;
                }
            }

            double offsetFeet = UnitConversionUtils.MillimetersToFeet(cropOffsetMm);

            // Для сложных контуров (больше 4 сторон) сразу переходим к упрощенному прямоугольному контуру.
            if (GetSideCount(sourceLoop) > 4)
            {
                if (!TryBuildSimplifiedRectLoop(
                        sourceLoop,
                        offsetFeet,
                        viewPlan,
                        out CurveLoop simplifiedLoop,
                        out string simplifiedError))
                {
                    errorMessage = "Не удалось упростить контур помещения: " + simplifiedError;
                    return false;
                }

                try
                {
                    cropManager.SetCropShape(simplifiedLoop);
                    return true;
                }
                catch (Exception simplifiedApplyException)
                {
                    errorMessage = "Не удалось применить упрощенную прямоугольную границу: " + simplifiedApplyException.Message;
                    return false;
                }
            }

            // Сначала пытаемся обычный путь: контур помещения + отступ.
            CurveLoop primaryLoop = sourceLoop;
            if (Math.Abs(offsetFeet) > 1e-9)
            {
                if (!TryCreateOffsetLoop(sourceLoop, offsetFeet, out primaryLoop) &&
                    !TryCreateOffsetLoop(sourceLoop, -offsetFeet, out primaryLoop))
                {
                    primaryLoop = null;
                }
            }

            if (primaryLoop != null)
            {
                try
                {
                    cropManager.SetCropShape(primaryLoop);
                    return true;
                }
                catch
                {
                    // Переходим в fallback ниже.
                }
            }

            // Fallback: упростить контур до прямоугольника и применить.
            if (!TryBuildSimplifiedRectLoop(
                    sourceLoop,
                    offsetFeet,
                    viewPlan,
                    out CurveLoop fallbackLoop,
                    out string fallbackError))
            {
                errorMessage = "Не удалось построить fallback-контур: " + fallbackError;
                return false;
            }

            try
            {
                cropManager.SetCropShape(fallbackLoop);
                return true;
            }
            catch (Exception fallbackApplyException)
            {
                errorMessage = "Не удалось применить fallback-контур: " + fallbackApplyException.Message;
                return false;
            }
        }

        private static int GetSideCount(CurveLoop loop)
        {
            int count = 0;
            if (loop == null)
            {
                return count;
            }

            foreach (Curve _ in loop)
            {
                count++;
            }

            return count;
        }

        private static bool TryBuildSimplifiedRectLoop(
            CurveLoop sourceLoop,
            double offsetFeet,
            ViewPlan sourcePlanView,
            out CurveLoop simplifiedLoop,
            out string errorMessage)
        {
            simplifiedLoop = null;
            errorMessage = string.Empty;

            if (!TryExtractVertices(sourceLoop, out List<XYZ> vertices, out double z, out errorMessage))
            {
                return false;
            }

            if (!TryGetPlanDirections(sourcePlanView, out XYZ dirX, out XYZ dirY))
            {
                errorMessage = "Не удалось определить ориентацию прямоугольной границы по осям плана.";
                return false;
            }

            XYZ origin = vertices[0];

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ local = ToLocal(vertices[i], origin, dirX, dirY);
                if (local.X < minX) minX = local.X;
                if (local.X > maxX) maxX = local.X;
                if (local.Y < minY) minY = local.Y;
                if (local.Y > maxY) maxY = local.Y;
            }

            double grow = Math.Abs(offsetFeet);
            minX -= grow;
            maxX += grow;
            minY -= grow;
            maxY += grow;

            if ((maxX - minX) < PointToleranceFeet || (maxY - minY) < PointToleranceFeet)
            {
                errorMessage = "Упрощенный контур вырожден.";
                return false;
            }

            XYZ p1 = ToWorld(minX, minY, origin, dirX, dirY, z);
            XYZ p2 = ToWorld(maxX, minY, origin, dirX, dirY, z);
            XYZ p3 = ToWorld(maxX, maxY, origin, dirX, dirY, z);
            XYZ p4 = ToWorld(minX, maxY, origin, dirX, dirY, z);

            CurveLoop rect = new CurveLoop();
            rect.Append(Line.CreateBound(p1, p2));
            rect.Append(Line.CreateBound(p2, p3));
            rect.Append(Line.CreateBound(p3, p4));
            rect.Append(Line.CreateBound(p4, p1));
            simplifiedLoop = rect;
            return true;
        }

        private static bool TryExtractVertices(
            CurveLoop loop,
            out List<XYZ> vertices,
            out double z,
            out string errorMessage)
        {
            vertices = new List<XYZ>();
            z = 0;
            errorMessage = string.Empty;

            if (loop == null)
            {
                errorMessage = "Исходный контур не задан.";
                return false;
            }

            bool zAssigned = false;
            foreach (Curve curve in loop)
            {
                if (curve == null)
                {
                    continue;
                }

                XYZ start = curve.GetEndPoint(0);
                if (start == null)
                {
                    continue;
                }

                if (!zAssigned)
                {
                    z = start.Z;
                    zAssigned = true;
                }

                vertices.Add(new XYZ(start.X, start.Y, 0));
            }

            if (vertices.Count < 3)
            {
                errorMessage = "В контуре меньше 3 вершин.";
                return false;
            }

            return true;
        }

        private static bool TryFindLongestDirection(
            IList<XYZ> vertices,
            out XYZ direction,
            out string errorMessage)
        {
            direction = XYZ.BasisX;
            errorMessage = string.Empty;

            if (vertices == null || vertices.Count < 2)
            {
                errorMessage = "Недостаточно вершин для расчета направления.";
                return false;
            }

            double maxLength = 0.0;
            XYZ bestDirection = null;

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ current = vertices[i];
                XYZ next = vertices[(i + 1) % vertices.Count];
                XYZ vector = new XYZ(next.X - current.X, next.Y - current.Y, 0);
                double length = vector.GetLength();

                if (length <= PointToleranceFeet)
                {
                    continue;
                }

                if (length > maxLength)
                {
                    maxLength = length;
                    bestDirection = new XYZ(vector.X / length, vector.Y / length, 0);
                }
            }

            if (bestDirection == null)
            {
                errorMessage = "Не найдена валидная длинная сторона помещения.";
                return false;
            }

            direction = bestDirection;
            return true;
        }

        private static XYZ ToLocal(XYZ point, XYZ origin, XYZ dirX, XYZ dirY)
        {
            XYZ relative = new XYZ(point.X - origin.X, point.Y - origin.Y, 0);
            double x = (relative.X * dirX.X) + (relative.Y * dirX.Y);
            double y = (relative.X * dirY.X) + (relative.Y * dirY.Y);
            return new XYZ(x, y, 0);
        }

        private static XYZ ToWorld(double x, double y, XYZ origin, XYZ dirX, XYZ dirY, double z)
        {
            double worldX = origin.X + (dirX.X * x) + (dirY.X * y);
            double worldY = origin.Y + (dirX.Y * x) + (dirY.Y * y);
            return new XYZ(worldX, worldY, z);
        }

        private static bool TryCreateOffsetLoop(CurveLoop sourceLoop, double offset, out CurveLoop offsetLoop)
        {
            offsetLoop = null;
            try
            {
                offsetLoop = CurveLoop.CreateViaOffset(sourceLoop, offset, XYZ.BasisZ);
                return offsetLoop != null;
            }
            catch
            {
                return false;
            }
        }

        private static int CreateHelperBoundaryLines(
            Document document,
            ViewPlan sourcePlanView,
            CurveLoop sourceLoop,
            double cropOffsetMm,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (document == null || sourcePlanView == null || sourceLoop == null)
            {
                errorMessage = "Не удалось создать вспомогательные линии: отсутствуют входные данные.";
                return 0;
            }

            CurveLoop helperLoop = sourceLoop;
            double offsetFeet = UnitConversionUtils.MillimetersToFeet(cropOffsetMm);
            if (Math.Abs(offsetFeet) > 1e-9)
            {
                if (!TryCreateOffsetLoop(sourceLoop, offsetFeet, out helperLoop) &&
                    !TryCreateOffsetLoop(sourceLoop, -offsetFeet, out helperLoop))
                {
                    helperLoop = sourceLoop;
                }
            }

            int created = 0;
            foreach (Curve curve in helperLoop)
            {
                if (curve == null)
                {
                    continue;
                }

                try
                {
                    Curve clone = curve.Clone();
                    document.Create.NewDetailCurve(sourcePlanView, clone);
                    created++;
                }
                catch
                {
                    // Пропускаем проблемную кривую и продолжаем создание остальных.
                }
            }

            if (created == 0)
            {
                errorMessage = "Вспомогательные линии не созданы.";
            }

            return created;
        }

        private static void TryApplyScaleRespectTemplate(
            Document document,
            View view,
            int requestedScale,
            string roomPrefix,
            IList<string> warnings)
        {
            if (document == null || view == null || requestedScale <= 0)
            {
                return;
            }

            if (IsScaleControlledByTemplate(document, view))
            {
                if (warnings != null)
                {
                    warnings.Add(
                        roomPrefix +
                        "Масштаб вида задается шаблоном. Значение из окна настроек не применено.");
                }

                return;
            }

            try
            {
                view.Scale = requestedScale;
            }
            catch (Exception scaleException)
            {
                if (warnings != null)
                {
                    warnings.Add(
                        roomPrefix +
                        "Не удалось установить масштаб вида: " + scaleException.Message);
                }
            }
        }

        private static bool IsScaleControlledByTemplate(Document document, View view)
        {
            if (document == null || view == null)
            {
                return false;
            }

            // Считываем фактически назначенный шаблон на виде.
            // Это покрывает оба сценария:
            // - шаблон выбран пользователем в окне;
            // - шаблон пришел автоматически через тип вида.
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

            ICollection<ElementId> nonControlledParameters = templateView.GetNonControlledTemplateParameterIds();
            if (nonControlledParameters == null)
            {
                // Если API не вернул список, безопасно считаем параметр контролируемым шаблоном.
                return true;
            }

            ElementId viewScaleId = new ElementId((int)BuiltInParameter.VIEW_SCALE);
            ElementId viewScaleMetricId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
            ElementId viewScaleImperialId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_IMPERIAL);

            bool scaleIsNotControlled =
                nonControlledParameters.Contains(viewScaleId) ||
                nonControlledParameters.Contains(viewScaleMetricId) ||
                nonControlledParameters.Contains(viewScaleImperialId);

            return !scaleIsNotControlled;
        }

        private static void TryDeleteView(Document document, ElementId viewId)
        {
            if (document == null || viewId == null || viewId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                document.Delete(viewId);
            }
            catch
            {
                // Если удалить не удалось, просто продолжаем без остановки основной команды.
            }
        }
    }
}
