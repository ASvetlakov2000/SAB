using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Marks
{
    public class PlanCornerMarkAlignmentService
    {
        private const double GeometryTolerance = 1e-9;
        private const double EndpointDetectionToleranceMm = 5.0;

        public PlanCornerMarkAlignmentResult AlignSelectedMarks(
            Document document,
            ViewPlan activePlanView,
            IList<FamilyInstance> selectedMarks,
            PlanCornerMarkAlignmentSettings settings,
            IList<string> warnings)
        {
            PlanCornerMarkAlignmentResult result = new PlanCornerMarkAlignmentResult();
            result.SelectedCount = selectedMarks != null ? selectedMarks.Count : 0;

            if (document == null || activePlanView == null || selectedMarks == null || selectedMarks.Count == 0 || settings == null)
            {
                return result;
            }

            List<DetailLineInfo> detailLineInfos = CollectDetailLineInfos(document, activePlanView, warnings);
            if (detailLineInfos.Count == 0)
            {
                AddWarning(warnings, "На активном плане не найдено линий детализации для расчета смещения марок.");
                result.FailedCount = result.SelectedCount;
                return result;
            }

            XYZ selectionCenter = CalculateSelectionCenter(selectedMarks);
            double userOffsetFeet = UnitConversionUtils.MillimetersToFeet(settings.CornerOffsetMm);
            List<PendingLeaderData> pendingLeaders = new List<PendingLeaderData>();

            // Ð­Ñ‚Ð°Ð¿ 1: Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿ÐµÑ€ÐµÐ¼ÐµÑ‰Ð°ÐµÐ¼ Ð¼Ð°Ñ€ÐºÐ¸ Ð² Ñ€Ð°ÑÑÑ‡Ð¸Ñ‚Ð°Ð½Ð½Ñ‹Ðµ Ñ‚Ð¾Ñ‡ÐºÐ¸.
            for (int index = 0; index < selectedMarks.Count; index++)
            {
                FamilyInstance markInstance = selectedMarks[index];
                if (markInstance == null || !markInstance.IsValidObject)
                {
                    result.FailedCount++;
                    continue;
                }

                if (!CornerMarkConstants.IsAnnotationInstance(markInstance))
                {
                    result.FailedCount++;
                    AddWarning(warnings, "Элемент " + markInstance.Id.IntegerValue + " не относится к категории '" + CornerMarkConstants.GetAnnotationCategoryNameForMessage() + "'.");
                    continue;
                }

                LocationPoint locationPoint = markInstance.Location as LocationPoint;
                if (locationPoint == null)
                {
                    result.FailedCount++;
                    AddWarning(warnings, "Марка " + markInstance.Id.IntegerValue + " не имеет точки размещения.");
                    continue;
                }

                XYZ originalPoint = locationPoint.Point;
                CornerContext cornerContext;
                if (!TryBuildCornerContext(originalPoint, selectionCenter, detailLineInfos, out cornerContext))
                {
                    result.FailedCount++;
                    AddWarning(warnings, "Для марки " + markInstance.Id.IntegerValue + " не удалось определить угол по линиям детализации.");
                    continue;
                }

                // ÐžÐ´Ð¸Ð½Ð°ÐºÐ¾Ð²Ð°Ñ Ð±Ð°Ð·Ð° Ð¾Ñ‚ÑÑ‚ÑƒÐ¿Ð° Ð´Ð»Ñ Ð²ÑÐµÑ… ÑƒÐ³Ð»Ð¾Ð²: Ð¿Ð¾Ð»ÑƒÐ´Ð¸Ð°Ð³Ð¾Ð½Ð°Ð»ÑŒ Ð³Ð°Ð±Ð°Ñ€Ð¸Ñ‚Ð° + Ð¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÑŒÑÐºÐ¸Ð¹ Ð¾Ñ‚ÑÑ‚ÑƒÐ¿.
                double markHalfDiagonal = GetBoundingHalfDiagonal(markInstance, activePlanView);
                double totalShift = markHalfDiagonal + userOffsetFeet;

                XYZ shiftDirection = cornerContext.ResultNormal;
                if (cornerContext.IsSingleLineCorner &&
                    cornerContext.SecondaryShiftDirection != null &&
                    cornerContext.SecondaryShiftDirection.GetLength() > GeometryTolerance)
                {
                    // Ð”Ð»Ñ ÑƒÐ³Ð»Ð° Ð¸Ð· Ð¾Ð´Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸ Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ = ÑÑƒÐ¼Ð¼Ð° (Ð½Ð¾Ñ€Ð¼Ð°Ð»ÑŒ + Ð²Ð´Ð¾Ð»ÑŒ Ð»Ð¸Ð½Ð¸Ð¸ Ðº Ð¿Ñ€Ð¾Ñ‚Ð¸Ð²Ð¾Ð¿Ð¾Ð»Ð¾Ð¶Ð½Ð¾Ð¹ Ñ‚Ð¾Ñ‡ÐºÐµ).
                    XYZ combinedDirection = cornerContext.ResultNormal + cornerContext.SecondaryShiftDirection;
                    if (combinedDirection.GetLength() > GeometryTolerance)
                    {
                        shiftDirection = combinedDirection.Normalize();
                    }
                }

                XYZ targetHeadPoint = originalPoint + shiftDirection * totalShift;
                XYZ elbowPoint = BuildElbowPoint(
                    targetHeadPoint,
                    originalPoint,
                    cornerContext.ElbowDirection,
                    settings.LeaderBreakAngle);

                bool wasPinned = markInstance.Pinned;
                try
                {
                    if (wasPinned)
                    {
                        markInstance.Pinned = false;
                    }

                    locationPoint.Point = targetHeadPoint;

                    PendingLeaderData pendingLeaderData = new PendingLeaderData();
                    pendingLeaderData.MarkInstance = markInstance;
                    pendingLeaderData.LeaderEndPoint = originalPoint;
                    pendingLeaderData.LeaderElbowPoint = elbowPoint;
                    pendingLeaders.Add(pendingLeaderData);
                    result.AlignedCount++;
                }
                catch (Exception exception)
                {
                    result.FailedCount++;
                    AddWarning(warnings, "Ошибка выравнивания марки " + markInstance.Id.IntegerValue + ": " + exception.Message);
                }
                finally
                {
                    if (wasPinned && markInstance.IsValidObject)
                    {
                        markInstance.Pinned = true;
                    }
                }
            }

            // Ð­Ñ‚Ð°Ð¿ 2: ÑÐ¾Ð·Ð´Ð°ÐµÐ¼ Ð¸ Ð½Ð°ÑÑ‚Ñ€Ð°Ð¸Ð²Ð°ÐµÐ¼ Ð²Ñ‹Ð½Ð¾ÑÐºÐ¸ Ð¿Ð¾ÑÐ»Ðµ Ð·Ð°Ð²ÐµÑ€ÑˆÐµÐ½Ð¸Ñ Ð²ÑÐµÑ… Ð¿ÐµÑ€ÐµÐ¼ÐµÑ‰ÐµÐ½Ð¸Ð¹.
            for (int leaderIndex = 0; leaderIndex < pendingLeaders.Count; leaderIndex++)
            {
                PendingLeaderData pendingLeaderData = pendingLeaders[leaderIndex];
                if (pendingLeaderData == null || pendingLeaderData.MarkInstance == null || !pendingLeaderData.MarkInstance.IsValidObject)
                {
                    continue;
                }

                bool leaderApplied = TryCreateLeaderForMark(
                    pendingLeaderData.MarkInstance,
                    pendingLeaderData.LeaderEndPoint,
                    pendingLeaderData.LeaderElbowPoint,
                    warnings);

                if (!leaderApplied)
                {
                    AddWarning(warnings, "Марка " + pendingLeaderData.MarkInstance.Id.IntegerValue + " перемещена, но выноску создать не удалось.");
                }
            }

            return result;
        }

        private bool TryCreateLeaderForMark(
            FamilyInstance markInstance,
            XYZ leaderEndPoint,
            XYZ leaderElbowPoint,
            IList<string> warnings)
        {
            if (markInstance == null || !markInstance.IsValidObject)
            {
                return false;
            }

            AnnotationSymbol annotationSymbol = markInstance as AnnotationSymbol;
            if (annotationSymbol == null)
            {
                AddWarning(warnings, "Элемент " + markInstance.Id.IntegerValue + " не является AnnotationSymbol.");
                return false;
            }

            bool wasPinned = annotationSymbol.Pinned;
            try
            {
                if (wasPinned)
                {
                    annotationSymbol.Pinned = false;
                }

                IList<Leader> leadersBefore = GetLeaders(annotationSymbol);
                int countBefore = leadersBefore != null ? leadersBefore.Count : 0;

                // ÐŸÐ¾ Ñ‚Ñ€ÐµÐ±Ð¾Ð²Ð°Ð½Ð¸ÑŽ: Ð´Ð¾Ð±Ð°Ð²Ð»ÑÐµÐ¼ Ð²Ñ‹Ð½Ð¾ÑÐºÑƒ Ñ‡ÐµÑ€ÐµÐ· Ð²Ñ‹Ð·Ð¾Ð² AnnotationSymbol.AddLeader().
                if (!TryInvokeAddLeader(annotationSymbol))
                {
                    AddWarning(warnings, "У марки " + markInstance.Id.IntegerValue + " не найден метод AddLeader().");
                    return false;
                }

                annotationSymbol.Document.Regenerate();

                IList<Leader> leadersAfter = GetLeaders(annotationSymbol);
                if (leadersAfter == null || leadersAfter.Count == 0)
                {
                    AddWarning(warnings, "У марки " + markInstance.Id.IntegerValue + " после AddLeader не найдена выноска.");
                    return false;
                }

                Leader leader = null;
                if (leadersAfter.Count > countBefore)
                {
                    leader = leadersAfter[leadersAfter.Count - 1];
                }
                else
                {
                    leader = leadersAfter[leadersAfter.Count - 1];
                }

                if (leader == null)
                {
                    AddWarning(warnings, "У марки " + markInstance.Id.IntegerValue + " не удалось получить объект Leader.");
                    return false;
                }

                // End = Ð¸ÑÑ…Ð¾Ð´Ð½Ð°Ñ Ñ‚Ð¾Ñ‡ÐºÐ° Ð¼Ð°Ñ€ÐºÐ¸ Ð´Ð¾ ÑÐ¼ÐµÑ‰ÐµÐ½Ð¸Ñ (ÑÑ‚Ð°Ñ€Ñ‚/Ñ„Ð¸Ð½Ð¸Ñˆ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸).
                leader.End = leaderEndPoint;
                leader.Elbow = leaderElbowPoint;
                annotationSymbol.Document.Regenerate();
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Ошибка создания выноски у марки " + markInstance.Id.IntegerValue + ": " + exception.Message);
                return false;
            }
            finally
            {
                if (wasPinned && annotationSymbol.IsValidObject)
                {
                    annotationSymbol.Pinned = true;
                }
            }
        }

        private bool TryInvokeAddLeader(AnnotationSymbol annotationSymbol)
        {
            if (annotationSymbol == null)
            {
                return false;
            }

            Type symbolType = annotationSymbol.GetType();
            MethodInfo[] methods = symbolType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                if (!string.Equals(method.Name, "AddLeader", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                try
                {
                    if (parameters.Length == 0)
                    {
                        method.Invoke(annotationSymbol, null);
                        return true;
                    }

                    if (parameters.Length == 1)
                    {
                        Type parameterType = parameters[0].ParameterType;
                        if (parameterType.IsEnum)
                        {
                            Array enumValues = Enum.GetValues(parameterType);
                            if (enumValues.Length > 0)
                            {
                                method.Invoke(annotationSymbol, new[] { enumValues.GetValue(0) });
                                return true;
                            }
                        }
                        else if (parameterType == typeof(bool))
                        {
                            method.Invoke(annotationSymbol, new object[] { true });
                            return true;
                        }
                        else if (parameterType == typeof(int))
                        {
                            method.Invoke(annotationSymbol, new object[] { 0 });
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private IList<Leader> GetLeaders(AnnotationSymbol annotationSymbol)
        {
            if (annotationSymbol == null)
            {
                return new List<Leader>();
            }

            IList<Leader> leaders = annotationSymbol.GetLeaders();
            return leaders ?? new List<Leader>();
        }

        private List<DetailLineInfo> CollectDetailLineInfos(Document document, ViewPlan activePlanView, IList<string> warnings)
        {
            List<DetailLineInfo> lineInfos = new List<DetailLineInfo>();
            FilteredElementCollector collector = new FilteredElementCollector(document, activePlanView.Id)
                .OfClass(typeof(CurveElement))
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                CurveElement curveElement = element as CurveElement;
                if (curveElement == null)
                {
                    continue;
                }

                if (curveElement.OwnerViewId != activePlanView.Id)
                {
                    continue;
                }

                if (curveElement.CurveElementType != CurveElementType.DetailCurve)
                {
                    continue;
                }

                Line sourceLine = GetLine(curveElement);
                if (sourceLine == null)
                {
                    continue;
                }

                XYZ rawDirection = sourceLine.GetEndPoint(1) - sourceLine.GetEndPoint(0);
                XYZ xyDirection = new XYZ(rawDirection.X, rawDirection.Y, 0.0);
                if (xyDirection.GetLength() <= GeometryTolerance)
                {
                    AddWarning(warnings, "Линия " + curveElement.Id.IntegerValue + " имеет некорректную длину в плоскости XY и была пропущена.");
                    continue;
                }

                xyDirection = xyDirection.Normalize();
                XYZ normal = xyDirection.CrossProduct(XYZ.BasisZ);
                if (normal.GetLength() <= GeometryTolerance)
                {
                    AddWarning(warnings, "Для линии " + curveElement.Id.IntegerValue + " не удалось вычислить нормаль.");
                    continue;
                }

                normal = normal.Normalize();

                DetailLineInfo info = new DetailLineInfo();
                info.LineId = curveElement.Id;
                info.StartPoint = sourceLine.GetEndPoint(0);
                info.EndPoint = sourceLine.GetEndPoint(1);
                info.Direction = xyDirection;
                info.Normal = normal;
                lineInfos.Add(info);
            }

            return lineInfos;
        }

        private Line GetLine(CurveElement curveElement)
        {
            if (curveElement == null)
            {
                return null;
            }

            Curve curve = curveElement.GeometryCurve;
            if (curve == null)
            {
                LocationCurve locationCurve = curveElement.Location as LocationCurve;
                if (locationCurve != null)
                {
                    curve = locationCurve.Curve;
                }
            }

            return curve as Line;
        }

        private bool TryBuildCornerContext(
            XYZ markPoint,
            XYZ selectionCenter,
            IList<DetailLineInfo> detailLineInfos,
            out CornerContext context)
        {
            context = null;
            if (markPoint == null || detailLineInfos == null || detailLineInfos.Count == 0)
            {
                return false;
            }

            DetailLineInfo nearestLine = null;
            XYZ anchorPoint = null;
            double minDistance = double.MaxValue;

            for (int i = 0; i < detailLineInfos.Count; i++)
            {
                DetailLineInfo lineInfo = detailLineInfos[i];

                double distanceToStart = markPoint.DistanceTo(lineInfo.StartPoint);
                if (distanceToStart < minDistance)
                {
                    minDistance = distanceToStart;
                    nearestLine = lineInfo;
                    anchorPoint = lineInfo.StartPoint;
                }

                double distanceToEnd = markPoint.DistanceTo(lineInfo.EndPoint);
                if (distanceToEnd < minDistance)
                {
                    minDistance = distanceToEnd;
                    nearestLine = lineInfo;
                    anchorPoint = lineInfo.EndPoint;
                }
            }

            if (nearestLine == null || anchorPoint == null)
            {
                return false;
            }

            double endpointTolerance = UnitConversionUtils.MillimetersToFeet(EndpointDetectionToleranceMm);
            List<DetailLineInfo> connectedLines = new List<DetailLineInfo>();

            for (int i = 0; i < detailLineInfos.Count; i++)
            {
                DetailLineInfo lineInfo = detailLineInfos[i];
                bool isConnected =
                    lineInfo.StartPoint.DistanceTo(anchorPoint) <= endpointTolerance ||
                    lineInfo.EndPoint.DistanceTo(anchorPoint) <= endpointTolerance;

                if (isConnected)
                {
                    connectedLines.Add(lineInfo);
                }
            }

            if (connectedLines.Count == 0)
            {
                connectedLines.Add(nearestLine);
            }

            // Ð•ÑÐ»Ð¸ Ð² Ñ‚Ð¾Ñ‡ÐºÐµ Ð±Ð¾Ð»ÑŒÑˆÐµ Ð´Ð²ÑƒÑ… Ð»Ð¸Ð½Ð¸Ð¹, Ð±ÐµÑ€ÐµÐ¼ Ð¿Ð°Ñ€Ñƒ Ñ Ð¼Ð°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¼ Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð¾Ñ‚Ð¾Ð¼.
            if (connectedLines.Count > 2)
            {
                connectedLines = PickMostRelevantLines(connectedLines, nearestLine);
            }

            XYZ referenceVector = selectionCenter - anchorPoint;
            XYZ normalSum = XYZ.Zero;

            for (int i = 0; i < connectedLines.Count; i++)
            {
                DetailLineInfo lineInfo = connectedLines[i];
                XYZ orientedNormal = lineInfo.Normal;

                if (referenceVector.GetLength() > GeometryTolerance && orientedNormal.DotProduct(referenceVector) < 0.0)
                {
                    orientedNormal = -orientedNormal;
                }

                normalSum += orientedNormal;
            }

            if (normalSum.GetLength() <= GeometryTolerance)
            {
                normalSum = nearestLine.Normal;
            }

            XYZ resultNormal = new XYZ(normalSum.X, normalSum.Y, 0.0);
            if (resultNormal.GetLength() <= GeometryTolerance)
            {
                return false;
            }

            resultNormal = resultNormal.Normalize();

            XYZ elbowDirection = new XYZ(-resultNormal.Y, resultNormal.X, 0.0);
            if (elbowDirection.GetLength() <= GeometryTolerance)
            {
                elbowDirection = nearestLine.Direction;
            }
            else
            {
                elbowDirection = elbowDirection.Normalize();
            }

            bool isSingleLineCorner = connectedLines.Count == 1;
            XYZ secondaryShiftDirection = XYZ.Zero;

            if (isSingleLineCorner)
            {
                DetailLineInfo singleLine = connectedLines[0];
                bool anchorAtStart = singleLine.StartPoint.DistanceTo(anchorPoint) <= singleLine.EndPoint.DistanceTo(anchorPoint);
                XYZ oppositePoint = anchorAtStart ? singleLine.EndPoint : singleLine.StartPoint;

                secondaryShiftDirection = oppositePoint - anchorPoint;
                if (secondaryShiftDirection.GetLength() <= GeometryTolerance)
                {
                    secondaryShiftDirection = anchorAtStart ? singleLine.Direction : -singleLine.Direction;
                }
                else
                {
                    secondaryShiftDirection = new XYZ(secondaryShiftDirection.X, secondaryShiftDirection.Y, 0.0).Normalize();
                }

                elbowDirection = secondaryShiftDirection;
            }

            context = new CornerContext();
            context.AnchorPoint = anchorPoint;
            context.ResultNormal = resultNormal;
            context.ElbowDirection = elbowDirection;
            context.SecondaryShiftDirection = secondaryShiftDirection;
            context.IsSingleLineCorner = isSingleLineCorner;
            return true;
        }

        private List<DetailLineInfo> PickMostRelevantLines(List<DetailLineInfo> connectedLines, DetailLineInfo nearestLine)
        {
            List<DetailLineInfo> result = new List<DetailLineInfo>();
            if (connectedLines == null || connectedLines.Count == 0)
            {
                return result;
            }

            DetailLineInfo firstLine = nearestLine != null ? nearestLine : connectedLines[0];
            if (!connectedLines.Contains(firstLine))
            {
                firstLine = connectedLines[0];
            }

            result.Add(firstLine);

            DetailLineInfo secondLine = null;
            double bestScore = double.MinValue;

            for (int i = 0; i < connectedLines.Count; i++)
            {
                DetailLineInfo candidate = connectedLines[i];
                if (candidate == firstLine)
                {
                    continue;
                }

                double dot = Math.Abs(firstLine.Direction.DotProduct(candidate.Direction));
                double score = 1.0 - dot;
                if (score > bestScore)
                {
                    bestScore = score;
                    secondLine = candidate;
                }
            }

            if (secondLine != null)
            {
                result.Add(secondLine);
            }

            return result;
        }

        private double GetBoundingHalfDiagonal(FamilyInstance markInstance, ViewPlan activePlanView)
        {
            if (markInstance == null)
            {
                return 0.0;
            }

            BoundingBoxXYZ boundingBox = markInstance.get_BoundingBox(activePlanView);
            if (boundingBox == null)
            {
                boundingBox = markInstance.get_BoundingBox(null);
            }

            if (boundingBox == null)
            {
                return 0.0;
            }

            double width = boundingBox.Max.X - boundingBox.Min.X;
            double height = boundingBox.Max.Y - boundingBox.Min.Y;
            return Math.Sqrt(width * width + height * height) / 2.0;
        }

        private XYZ BuildElbowPoint(
            XYZ leaderHeadPoint,
            XYZ leaderEndPoint,
            XYZ elbowDirection,
            PlanLeaderBreakAngleType breakAngleType)
        {
            if (leaderHeadPoint == null || leaderEndPoint == null)
            {
                return XYZ.Zero;
            }

            XYZ midpoint = (leaderHeadPoint + leaderEndPoint) * 0.5;
            XYZ headToEnd = leaderEndPoint - leaderHeadPoint;
            double headToEndLength = headToEnd.GetLength();
            if (headToEndLength <= GeometryTolerance)
            {
                return leaderHeadPoint;
            }

            XYZ normalizedDirection = elbowDirection;
            if (normalizedDirection == null || normalizedDirection.GetLength() <= GeometryTolerance)
            {
                XYZ fallbackDirection = new XYZ(-headToEnd.Y, headToEnd.X, 0.0);
                if (fallbackDirection.GetLength() <= GeometryTolerance)
                {
                    fallbackDirection = XYZ.BasisX;
                }

                normalizedDirection = fallbackDirection;
            }

            normalizedDirection = new XYZ(normalizedDirection.X, normalizedDirection.Y, 0.0);
            if (normalizedDirection.GetLength() <= GeometryTolerance)
            {
                normalizedDirection = XYZ.BasisX;
            }
            else
            {
                normalizedDirection = normalizedDirection.Normalize();
            }

            // Ð’Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÐ¼ ÑÑ‚Ð°Ñ€ÑƒÑŽ Ð¼ÐµÑ…Ð°Ð½Ð¸ÐºÑƒ:
            // 90Â°: ÑÐ¼ÐµÑ‰ÐµÐ½Ð¸Ðµ Ð¾Ñ‚ ÑÐµÑ€ÐµÐ´Ð¸Ð½Ñ‹ Ð½Ð° Ð¿Ð¾Ð»Ð¾Ð²Ð¸Ð½Ñƒ Ð´Ð»Ð¸Ð½Ñ‹.
            // 135Â°: ÑÐ¼ÐµÑ‰ÐµÐ½Ð¸Ðµ Ð¾Ñ‚ ÑÐµÑ€ÐµÐ´Ð¸Ð½Ñ‹ Ð½Ð° Ð¿Ð¾Ð»Ð¾Ð²Ð¸Ð½Ñƒ Ð´Ð»Ð¸Ð½Ñ‹ * 0.41421356237.
            double halfLength = headToEndLength / 2.0;
            double factor = breakAngleType == PlanLeaderBreakAngleType.Degrees135
                ? 0.41421356237
                : 1.0;

            double elbowShift = halfLength * factor;
            return midpoint + normalizedDirection * elbowShift;
        }

        private XYZ CalculateSelectionCenter(IList<FamilyInstance> selectedMarks)
        {
            if (selectedMarks == null || selectedMarks.Count == 0)
            {
                return XYZ.Zero;
            }

            double x = 0.0;
            double y = 0.0;
            double z = 0.0;
            int pointsCount = 0;

            for (int i = 0; i < selectedMarks.Count; i++)
            {
                FamilyInstance markInstance = selectedMarks[i];
                if (markInstance == null)
                {
                    continue;
                }

                LocationPoint locationPoint = markInstance.Location as LocationPoint;
                if (locationPoint == null || locationPoint.Point == null)
                {
                    continue;
                }

                x += locationPoint.Point.X;
                y += locationPoint.Point.Y;
                z += locationPoint.Point.Z;
                pointsCount++;
            }

            if (pointsCount == 0)
            {
                return XYZ.Zero;
            }

            return new XYZ(x / pointsCount, y / pointsCount, z / pointsCount);
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            warnings.Add(text);
        }

        private class DetailLineInfo
        {
            public ElementId LineId { get; set; }

            public XYZ StartPoint { get; set; }

            public XYZ EndPoint { get; set; }

            public XYZ Direction { get; set; }

            public XYZ Normal { get; set; }
        }

        private class CornerContext
        {
            public XYZ AnchorPoint { get; set; }

            public XYZ ResultNormal { get; set; }

            public XYZ ElbowDirection { get; set; }

            public XYZ SecondaryShiftDirection { get; set; }

            public bool IsSingleLineCorner { get; set; }
        }

        private class PendingLeaderData
        {
            public FamilyInstance MarkInstance { get; set; }

            public XYZ LeaderEndPoint { get; set; }

            public XYZ LeaderElbowPoint { get; set; }
        }
    }
}


