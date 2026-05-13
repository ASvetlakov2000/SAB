using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationFlipService
    {
        private const double DirectionEpsilon = 1e-9;
        private const double ParallelDotThreshold = 0.20;

        /// <summary>
        /// Ð’Ñ‹Ð±Ð¾Ñ€ Ñ†ÐµÐ»ÐµÐ²Ð¾Ð³Ð¾ Ñ„Ð°ÑÐ°Ð´Ð° ÑÑ‚Ñ€Ð¾Ð³Ð¾ Ñ Ð°ÐºÑ‚Ð¸Ð²Ð½Ð¾Ð³Ð¾ Ð¿Ð»Ð°Ð½Ð° (Ð½Ðµ Ñ Ð»Ð¸ÑÑ‚Ð°).
        /// ÐŸÐ¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÑŒ ÐºÐ»Ð¸ÐºÐ°ÐµÑ‚ Ð¼Ð°Ñ€ÐºÐµÑ€/Ð¾Ð±Ð¾Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ Ñ„Ð°ÑÐ°Ð´Ð° Ð½Ð° Ð¿Ð»Ð°Ð½Ðµ ÑÑ‚Ð°Ð¶Ð°.
        /// </summary>
        public bool TryPickElevationTargetOnPlan(
            UIDocument uiDocument,
            View activeView,
            out ViewSection elevationView,
            out Viewport sourceViewport,
            out string errorMessage)
        {
            elevationView = null;
            sourceViewport = null;
            errorMessage = string.Empty;

            if (uiDocument == null || uiDocument.Document == null)
            {
                errorMessage = "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚ Revit.";
                return false;
            }

            if (activeView == null || (activeView.ViewType != ViewType.FloorPlan && activeView.ViewType != ViewType.CeilingPlan))
            {
                errorMessage = "ÐÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð²Ð¸Ð´ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ ÐŸÐ»Ð°Ð½Ð¾Ð¼ ÑÑ‚Ð°Ð¶Ð° Ð¸Ð»Ð¸ ÐŸÐ»Ð°Ð½Ð¾Ð¼ Ð¿Ð¾Ñ‚Ð¾Ð»ÐºÐ°.";
                return false;
            }

            Document document = uiDocument.Document;
            try
            {
                Reference pickedReference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new PlanElevationSelectionFilter(),
                    "Ð’Ñ‹Ð±ÐµÑ€Ð¸Ñ‚Ðµ Ð¾Ð±Ð¾Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ Ð½ÐµÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾Ð³Ð¾ Ñ„Ð°ÑÐ°Ð´Ð° Ð½Ð° Ð°ÐºÑ‚Ð¸Ð²Ð½Ð¾Ð¼ Ð¿Ð»Ð°Ð½Ðµ");

                if (pickedReference == null)
                {
                    errorMessage = "Ð¤Ð°ÑÐ°Ð´ Ð½Ðµ Ð²Ñ‹Ð±Ñ€Ð°Ð½.";
                    return false;
                }

                Element pickedElement = document.GetElement(pickedReference);
                if (!TryResolveElevationFromPlanElement(document, pickedElement, out elevationView))
                {
                    errorMessage = "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»Ð¸Ñ‚ÑŒ Ñ„Ð°ÑÐ°Ð´ Ð¿Ð¾ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¼Ñƒ Ð¾Ð±Ð¾Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸ÑŽ Ð½Ð° Ð¿Ð»Ð°Ð½Ðµ.";
                    return false;
                }

                sourceViewport = FindViewportForView(document, elevationView.Id);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                errorMessage = "Ð’Ñ‹Ð±Ð¾Ñ€ Ñ„Ð°ÑÐ°Ð´Ð° Ð¾Ñ‚Ð¼ÐµÐ½ÐµÐ½ Ð¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÐµÐ¼.";
                return false;
            }
        }

        /// <summary>
        /// Ð’Ñ‹Ð±Ð¾Ñ€ Ð¸ÑÑ…Ð¾Ð´Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸, Ð¿Ð¾ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¹ Ð±Ñ‹Ð»Ð° Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð° Ñ€Ð°Ð·Ð²ÐµÑ€Ñ‚ÐºÐ°.
        /// </summary>
        public bool TryPickSourceDetailLine(
            UIDocument uiDocument,
            out DetailLine detailLine,
            out string errorMessage)
        {
            detailLine = null;
            errorMessage = string.Empty;

            if (uiDocument == null || uiDocument.Document == null)
            {
                errorMessage = "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚ Revit.";
                return false;
            }

            try
            {
                Reference pickedReference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Ð’Ñ‹Ð±ÐµÑ€Ð¸Ñ‚Ðµ Ð»Ð¸Ð½Ð¸ÑŽ, Ð¿Ð¾ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¹ ÑÐ¾Ð·Ð´Ð°Ð²Ð°Ð»Ð°ÑÑŒ Ñ€Ð°Ð·Ð²ÐµÑ€Ñ‚ÐºÐ°");

                if (pickedReference == null)
                {
                    errorMessage = "Ð›Ð¸Ð½Ð¸Ñ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ Ð½Ðµ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð°.";
                    return false;
                }

                Element pickedElement = uiDocument.Document.GetElement(pickedReference);
                detailLine = pickedElement as DetailLine;
                if (detailLine == null)
                {
                    errorMessage = "Ð’Ñ‹Ð±Ñ€Ð°Ð½Ð½Ñ‹Ð¹ ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚ Ð½Ðµ ÑÐ²Ð»ÑÐµÑ‚ÑÑ Ð»Ð¸Ð½Ð¸ÐµÐ¹ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸.";
                    return false;
                }

                Line sourceLine = TryGetStraightLine(detailLine);
                if (sourceLine == null || sourceLine.Length <= DirectionEpsilon)
                {
                    errorMessage = "Ð’Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð°Ñ Ð»Ð¸Ð½Ð¸Ñ Ð½Ðµ ÑÐ²Ð»ÑÐµÑ‚ÑÑ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹Ð¼ Ð¿Ñ€ÑÐ¼Ñ‹Ð¼ Ð¾Ñ‚Ñ€ÐµÐ·ÐºÐ¾Ð¼.";
                    return false;
                }

                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                errorMessage = "Ð’Ñ‹Ð±Ð¾Ñ€ Ð»Ð¸Ð½Ð¸Ð¸ Ð¾Ñ‚Ð¼ÐµÐ½ÐµÐ½ Ð¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÐµÐ¼.";
                return false;
            }
        }

        /// <summary>
        /// Ð’Ñ‹Ð¿Ð¾Ð»Ð½ÑÐµÑ‚ Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð¾Ñ‚ Ñ„Ð°ÑÐ°Ð´Ð° Ð½Ð° 180 Ð³Ñ€Ð°Ð´ÑƒÑÐ¾Ð² Ð¾Ñ‚Ð½Ð¾ÑÐ¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸.
        /// Ð•ÑÐ»Ð¸ Ð¸ÑÑ…Ð¾Ð´Ð½Ñ‹Ð¹ Ñ„Ð°ÑÐ°Ð´ ÑÑ‚Ð¾Ð¸Ñ‚ Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ, ÑÐ¾Ð·Ð´Ð°ÐµÑ‚ÑÑ Ð½Ð¾Ð²Ñ‹Ð¹ Ð²Ð¸Ð´ Ð¸ ÑÑ‚Ð°Ð²Ð¸Ñ‚ÑÑ Ð½Ð° Ñ‚Ð¾Ñ‚ Ð¶Ðµ Ð»Ð¸ÑÑ‚ Ð² Ñ‚Ñƒ Ð¶Ðµ Ñ‚Ð¾Ñ‡ÐºÑƒ.
        /// </summary>
        public ElevationFlipResult FlipElevationBySourceLine(
            Document document,
            View planView,
            ViewSection sourceElevationView,
            DetailLine sourceDetailLine,
            Viewport sourceViewport,
            IList<string> warnings)
        {
            ElevationFlipResult result = new ElevationFlipResult();
            ViewStateSnapshot sourceViewState = CaptureViewState(sourceElevationView);

            if (document == null)
            {
                result.Message = "Ð”Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚ Revit Ð½ÐµÐ´Ð¾ÑÑ‚ÑƒÐ¿ÐµÐ½.";
                return result;
            }

            if (!IsSupportedElevationView(sourceElevationView))
            {
                result.Message = "Ð’Ñ‹Ð±Ñ€Ð°Ð½Ð½Ñ‹Ð¹ Ð²Ð¸Ð´ Ð½Ðµ ÑÐ²Ð»ÑÐµÑ‚ÑÑ Ñ„Ð°ÑÐ°Ð´Ð¾Ð¼.";
                return result;
            }

            if (sourceDetailLine == null)
            {
                result.Message = "ÐÐµ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð° Ð¸ÑÑ…Ð¾Ð´Ð½Ð°Ñ Ð»Ð¸Ð½Ð¸Ñ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸.";
                return result;
            }

            result.SourceViewId = sourceElevationView.Id;

            // Ð‘Ð»Ð¾Ðº 1. ÐŸÐ¾Ð¸ÑÐº Ð¼Ð°Ñ€ÐºÐµÑ€Ð° Ñ„Ð°ÑÐ°Ð´Ð° Ð¸ Ð¸Ð½Ð´ÐµÐºÑÐ° Ð²Ð¸Ð´Ð° Ð² Ð¼Ð°Ñ€ÐºÐµÑ€Ðµ.
            ElevationMarker marker;
            int markerIndex;
            if (!TryFindElevationMarkerForView(document, sourceElevationView.Id, out marker, out markerIndex))
            {
                result.Message = "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð½Ð°Ð¹Ñ‚Ð¸ ElevationMarker Ð´Ð»Ñ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð³Ð¾ Ñ„Ð°ÑÐ°Ð´Ð°.";
                return result;
            }XYZ lineDirection = GetLineDirectionXY(sourceDetailLine);
            XYZ currentDirection = GetHorizontalDirection(sourceElevationView.ViewDirection);
            if (lineDirection.GetLength() <= DirectionEpsilon || currentDirection.GetLength() <= DirectionEpsilon)
            {
                result.Message = "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹Ðµ Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ñ Ð»Ð¸Ð½Ð¸Ð¸/Ð²Ð¸Ð´Ð° Ð´Ð»Ñ Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð¾Ñ‚Ð°.";
                return result;
            }

            // Ð‘Ð»Ð¾Ðº 2. ÐžÑÐ½Ð¾Ð²Ð½Ð¾Ð¹ ÑÑ†ÐµÐ½Ð°Ñ€Ð¸Ð¹: Ð½Ð°ÑÑ‚Ð¾ÑÑ‰ÐµÐµ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð¼Ð°Ñ€ÐºÐµÑ€Ð°
            // Ð¾Ñ‚Ð½Ð¾ÑÐ¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸ (Ð½Ðµ Ð¿Ñ€Ð¾ÑÑ‚Ð¾ Ð¿Ð¾Ð²Ð¾Ñ€Ð¾Ñ‚).
            bool mirrored = TryMirrorMarkerBySourceLine(document, marker.Id, sourceDetailLine, warnings);

            // ÐŸÐ¾ÑÐ»Ðµ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ Ð¾Ð±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸ÑŽ Ð²Ð¸Ð´Ð°.
            document.Regenerate();

            XYZ directionAfterMirror = GetHorizontalDirection(sourceElevationView.ViewDirection);
            double angleAfterMirror = 0.0;
            if (directionAfterMirror.GetLength() > DirectionEpsilon)
            {
                angleAfterMirror = currentDirection.AngleTo(directionAfterMirror) * 180.0 / Math.PI;
            }

            if (!mirrored)
            {
                AddWarning(
                    warnings,
                    "Ð—ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð¼Ð°Ñ€ÐºÐµÑ€Ð° Ð²Ñ‹Ð¿Ð¾Ð»Ð½Ð¸Ñ‚ÑŒ Ð½Ðµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ. ÐŸÑ€Ð¸Ð¼ÐµÐ½ÐµÐ½ fallback-Ð¿Ð¾Ð²Ð¾Ñ€Ð¾Ñ‚ Ð²Ð¾ÐºÑ€ÑƒÐ³ Ð¼Ð°Ñ€ÐºÐµÑ€Ð°.");

                XYZ targetDirection;
                double perpendicularCheck = Math.Abs(lineDirection.DotProduct(currentDirection));
                if (perpendicularCheck > ParallelDotThreshold)
                {
                    // Ð•ÑÐ»Ð¸ Ð»Ð¸Ð½Ð¸Ñ ÑÐ²Ð½Ð¾ Ð½Ðµ ÑÐ¾Ð²Ð¿Ð°Ð´Ð°ÐµÑ‚ Ñ Ð¾Ð¶Ð¸Ð´Ð°ÐµÐ¼Ð¾Ð¹ Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸ÐµÐ¹ Ñ„Ð°ÑÐ°Ð´Ð°,
                    // Ð´ÐµÐ»Ð°ÐµÐ¼ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ð¿Ð¾Ð²Ð¾Ñ€Ð¾Ñ‚ Ð½Ð° 180Â° Ð¸ Ñ„Ð¸ÐºÑÐ¸Ñ€ÑƒÐµÐ¼ Ð¿Ñ€ÐµÐ´ÑƒÐ¿Ñ€ÐµÐ¶Ð´ÐµÐ½Ð¸Ðµ.
                    targetDirection = -currentDirection;
                    AddWarning(
                        warnings,
                        "Ð’Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð°Ñ Ð»Ð¸Ð½Ð¸Ñ Ð½Ðµ Ð¿ÐµÑ€Ð¿ÐµÐ½Ð´Ð¸ÐºÑƒÐ»ÑÑ€Ð½Ð° Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸ÑŽ Ñ„Ð°ÑÐ°Ð´Ð°. " +
                        "Ð’Ñ‹Ð¿Ð¾Ð»Ð½ÐµÐ½ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð¾Ñ‚ Ð½Ð° 180Â° Ð²Ð¾ÐºÑ€ÑƒÐ³ Ð¼Ð°Ñ€ÐºÐµÑ€Ð°.");
                }
                else
                {
                    targetDirection = ReflectDirectionByLine(currentDirection, lineDirection);
                    if (targetDirection.GetLength() <= DirectionEpsilon)
                    {
                        targetDirection = -currentDirection;
                    }
                }

                XYZ rotationBasePoint;
                string rotationPointSource;
                if (!TryResolveMarkerRotationBasePoint(document, planView, marker, sourceDetailLine, sourceElevationView, out rotationBasePoint, out rotationPointSource))
                {
                    result.Message = "Зеркалирование не выполнено, а fallback-поворот невозможен: не найдена точка вращения маркера.";
                    return result;
                }

                AddWarning(warnings, "Точка разворота маркера определена методом: " + rotationPointSource + ".");

                double rotationAngle = CalculateSignedAngle(currentDirection, targetDirection);
                if (Math.Abs(rotationAngle) <= 1e-6)
                {
                    // ÐšÐ¾Ð¼Ð°Ð½Ð´Ð° Ð¿Ð¾ Ð±Ð¸Ð·Ð½ÐµÑ-Ð¿Ñ€Ð°Ð²Ð¸Ð»Ñƒ Ð´Ð¾Ð»Ð¶Ð½Ð° Ð¸Ð¼ÐµÐ½Ð½Ð¾ "Ñ€Ð°Ð·Ð²Ð¾Ñ€Ð°Ñ‡Ð¸Ð²Ð°Ñ‚ÑŒ",
                    // Ð¿Ð¾ÑÑ‚Ð¾Ð¼Ñƒ Ð¿Ñ€Ð¸ Ð²Ñ‹Ñ€Ð¾Ð¶Ð´ÐµÐ½Ð½Ð¾Ð¼ ÑƒÐ³Ð»Ðµ Ð¿Ñ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ Ð·Ð°Ð´Ð°ÐµÐ¼ 180 Ð³Ñ€Ð°Ð´ÑƒÑÐ¾Ð².
                    rotationAngle = Math.PI;
                }

                // Ð‘Ð»Ð¾Ðº fallback-Ð¿Ð¾Ð²Ð¾Ñ€Ð¾Ñ‚Ð° Ð¼Ð°Ñ€ÐºÐµÑ€Ð° (ÐµÑÐ»Ð¸ mirror Ð½ÐµÐ´Ð¾ÑÑ‚ÑƒÐ¿ÐµÐ½ Ð´Ð»Ñ Ñ‚ÐµÐºÑƒÑ‰ÐµÐ³Ð¾ ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚Ð°).
                Line rotateAxis = Line.CreateBound(rotationBasePoint, rotationBasePoint + XYZ.BasisZ);
                RotateMarkerByChunks(document, marker.Id, rotateAxis, rotationAngle);
                document.Regenerate();

                result.RotationAngleDegrees = Math.Abs(rotationAngle) * 180.0 / Math.PI;
            }
            else
            {
                result.RotationAngleDegrees = angleAfterMirror;
                if (result.RotationAngleDegrees < 1e-6)
                {
                    // Ð”Ð»Ñ Ð¾Ñ‚Ñ‡ÐµÑ‚Ð° ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÐ¼, Ñ‡Ñ‚Ð¾ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð²Ñ‹Ð¿Ð¾Ð»Ð½ÐµÐ½Ð¾.
                    result.RotationAngleDegrees = 180.0;
                }
            }
            result.ResultViewId = sourceElevationView.Id;
            result.ResultViewName = sourceElevationView.Name;

            // Ð‘Ð»Ð¾Ðº 3. Ð•ÑÐ»Ð¸ Ñ„Ð°ÑÐ°Ð´ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½ Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ, ÑÐ¾Ð·Ð´Ð°ÐµÐ¼ Ð½Ð¾Ð²Ñ‹Ð¹ Ð²Ð¸Ð´ Ð¸ ÑÑ‚Ð°Ð²Ð¸Ð¼ ÐµÐ³Ð¾ Ð½Ð° Ñ‚Ñƒ Ð¶Ðµ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸ÑŽ.
            if (sourceViewport != null && sourceViewport.IsValidObject)
            {
                result.IsSourcePlacedOnSheet = true;
                result.SourceViewportId = sourceViewport.Id;

                ViewSheet sourceSheet = document.GetElement(sourceViewport.OwnerViewId) as ViewSheet;
                if (sourceSheet != null)
                {
                    result.SheetNumber = sourceSheet.SheetNumber;
                    result.SheetName = sourceSheet.Name;
                }

                ViewSection replacementView;
                Viewport replacementViewport;
                if (!TryCreateReplacementViewOnSameSheet(
                    document,
                    sourceElevationView,
                    sourceViewport,
                    sourceViewState,
                    warnings,
                    out replacementView,
                    out replacementViewport))
                {
                    result.Message = "Фасад зеркалирован, но не удалось создать и разместить новый вид на листе.";
                    result.IsSuccess = false;
                    return result;
                }

                result.ResultViewId = replacementView.Id;
                result.ResultViewName = replacementView.Name;
                result.ResultViewportId = replacementViewport != null ? replacementViewport.Id : ElementId.InvalidElementId;
                result.IsSuccess = true;
                result.Message = "Фасад успешно зеркалирован. Создан новый вид и размещен на исходном листе.";
                return result;
            }

            // Принудительно восстанавливаем параметры исходного вида после операции,
            // чтобы масштаб/детализация/стиль отображения не менялись.
            ApplyViewState(sourceElevationView, sourceViewState, warnings);

            result.IsSuccess = true;
            result.Message = "Фасад успешно зеркалирован на 180°.";
            return result;
        }

        private bool TryMirrorMarkerBySourceLine(
            Document document,
            ElementId markerId,
            DetailLine sourceDetailLine,
            IList<string> warnings)
        {
            if (document == null || markerId == null || markerId == ElementId.InvalidElementId || sourceDetailLine == null)
            {
                return false;
            }

            Line sourceLine = TryGetStraightLine(sourceDetailLine);
            if (sourceLine == null || sourceLine.Length <= DirectionEpsilon)
            {
                AddWarning(warnings, "Ð—ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð½ÐµÐ²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾: Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð°Ñ Ð»Ð¸Ð½Ð¸Ñ Ð½Ðµ ÑÐ²Ð»ÑÐµÑ‚ÑÑ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹Ð¼ Ð¿Ñ€ÑÐ¼Ñ‹Ð¼ Ð¾Ñ‚Ñ€ÐµÐ·ÐºÐ¾Ð¼.");
                return false;
            }

            XYZ start = sourceLine.GetEndPoint(0);
            XYZ end = sourceLine.GetEndPoint(1);
            XYZ lineDirection = new XYZ(end.X - start.X, end.Y - start.Y, 0.0);
            if (lineDirection.GetLength() <= DirectionEpsilon)
            {
                AddWarning(warnings, "Ð—ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð½ÐµÐ²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾: Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸ Ð½ÐµÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾.");
                return false;
            }

            lineDirection = lineDirection.Normalize();

            // Ð’ÐµÑ€Ñ‚Ð¸ÐºÐ°Ð»ÑŒÐ½Ð°Ñ Ð¿Ð»Ð¾ÑÐºÐ¾ÑÑ‚ÑŒ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ:
            // Ð¿Ñ€Ð¾Ñ…Ð¾Ð´Ð¸Ñ‚ Ñ‡ÐµÑ€ÐµÐ· Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½ÑƒÑŽ Ð»Ð¸Ð½Ð¸ÑŽ, Ð° ÐµÐµ Ð½Ð¾Ñ€Ð¼Ð°Ð»ÑŒ Ð¿ÐµÑ€Ð¿ÐµÐ½Ð´Ð¸ÐºÑƒÐ»ÑÑ€Ð½Ð° Ð»Ð¸Ð½Ð¸Ð¸ Ð² XY.
            XYZ mirrorNormal = new XYZ(-lineDirection.Y, lineDirection.X, 0.0);
            if (mirrorNormal.GetLength() <= DirectionEpsilon)
            {
                AddWarning(warnings, "Ð—ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ Ð½ÐµÐ²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾: Ð½Ðµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾ÑÑ‚Ñ€Ð¾Ð¸Ñ‚ÑŒ Ð½Ð¾Ñ€Ð¼Ð°Ð»ÑŒ Ð¿Ð»Ð¾ÑÐºÐ¾ÑÑ‚Ð¸.");
                return false;
            }

            mirrorNormal = mirrorNormal.Normalize();
            XYZ lineMidpoint = new XYZ(
                (start.X + end.X) / 2.0,
                (start.Y + end.Y) / 2.0,
                (start.Z + end.Z) / 2.0);

            Plane mirrorPlane;
            try
            {
                mirrorPlane = Plane.CreateByNormalAndOrigin(mirrorNormal, lineMidpoint);
            }
            catch (Exception planeException)
            {
                AddWarning(warnings, "ÐžÑˆÐ¸Ð±ÐºÐ° Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð¸Ñ Ð¿Ð»Ð¾ÑÐºÐ¾ÑÑ‚Ð¸ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ: " + planeException.Message);
                return false;
            }

            try
            {
                if (!ElementTransformUtils.CanMirrorElement(document, markerId))
                {
                    AddWarning(warnings, "Revit ÑÐ¾Ð¾Ð±Ñ‰Ð¸Ð», Ñ‡Ñ‚Ð¾ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ñ‹Ð¹ ElevationMarker Ð½ÐµÐ»ÑŒÐ·Ñ Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ Ð² Ð·Ð°Ð´Ð°Ð½Ð½Ð¾Ð¹ Ð¿Ð»Ð¾ÑÐºÐ¾ÑÑ‚Ð¸.");
                    return false;
                }

                List<ElementId> idsToMirror = new List<ElementId> { markerId };
                ElementTransformUtils.MirrorElements(document, idsToMirror, mirrorPlane, false);
                return true;
            }
            catch (Exception mirrorException)
            {
                AddWarning(warnings, "ÐžÑˆÐ¸Ð±ÐºÐ° Ð·ÐµÑ€ÐºÐ°Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ Ð¼Ð°Ñ€ÐºÐµÑ€Ð°: " + mirrorException.Message);
                return false;
            }
        }

        private bool TryResolveMarkerRotationBasePoint(
            Document document,
            View planView,
            ElevationMarker marker,
            DetailLine sourceDetailLine,
            ViewSection sourceElevationView,
            out XYZ point,
            out string sourceDescription)
        {
            point = XYZ.Zero;
            sourceDescription = string.Empty;

            if (marker == null)
            {
                return false;
            }

            // Ð’Ð°Ñ€Ð¸Ð°Ð½Ñ‚ 1. ÐŸÑ€ÑÐ¼Ð¾Ðµ Ð¿Ð¾Ð»ÑƒÑ‡ÐµÐ½Ð¸Ðµ LocationPoint (Ð½Ðµ Ð²ÑÐµÐ³Ð´Ð° Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ð¾ Ð´Ð»Ñ ElevationMarker).
            LocationPoint locationPoint = marker.Location as LocationPoint;
            if (locationPoint != null && locationPoint.Point != null)
            {
                point = locationPoint.Point;
                sourceDescription = "LocationPoint";
                return true;
            }

            // Ð’Ð°Ñ€Ð¸Ð°Ð½Ñ‚ 2. Ð¦ÐµÐ½Ñ‚Ñ€ BoundingBox Ð² Ð°ÐºÑ‚Ð¸Ð²Ð½Ð¾Ð¼ Ð¿Ð»Ð°Ð½Ðµ.
            BoundingBoxXYZ planBox = null;
            if (planView != null)
            {
                planBox = marker.get_BoundingBox(planView);
            }

            if (TryGetBoxCenter(planBox, out point))
            {
                sourceDescription = "BoundingBox(active plan)";
                return true;
            }

            // Ð’Ð°Ñ€Ð¸Ð°Ð½Ñ‚ 3. Ð¦ÐµÐ½Ñ‚Ñ€ Ð¾Ð±Ñ‰ÐµÐ³Ð¾ BoundingBox.
            BoundingBoxXYZ globalBox = marker.get_BoundingBox(null);
            if (TryGetBoxCenter(globalBox, out point))
            {
                sourceDescription = "BoundingBox(global)";
                return true;
            }

            // Ð’Ð°Ñ€Ð¸Ð°Ð½Ñ‚ 4. Origin Ñ„Ð°ÑÐ°Ð´Ð½Ð¾Ð³Ð¾ Ð²Ð¸Ð´Ð°.
            if (sourceElevationView != null && sourceElevationView.Origin != null)
            {
                point = sourceElevationView.Origin;
                sourceDescription = "ViewSection.Origin";
                return true;
            }

            // Ð’Ð°Ñ€Ð¸Ð°Ð½Ñ‚ 5. Ð¡ÐµÑ€ÐµÐ´Ð¸Ð½Ð° Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½Ð¾Ð¹ Ð¸ÑÑ…Ð¾Ð´Ð½Ð¾Ð¹ Ð»Ð¸Ð½Ð¸Ð¸.
            Line sourceLine = TryGetStraightLine(sourceDetailLine);
            if (sourceLine != null)
            {
                XYZ start = sourceLine.GetEndPoint(0);
                XYZ end = sourceLine.GetEndPoint(1);
                point = new XYZ(
                    (start.X + end.X) / 2.0,
                    (start.Y + end.Y) / 2.0,
                    (start.Z + end.Z) / 2.0);
                sourceDescription = "Source detail line midpoint";
                return true;
            }

            return false;
        }

        private bool TryGetBoxCenter(BoundingBoxXYZ box, out XYZ center)
        {
            center = XYZ.Zero;
            if (box == null || box.Min == null || box.Max == null)
            {
                return false;
            }

            center = new XYZ(
                (box.Min.X + box.Max.X) / 2.0,
                (box.Min.Y + box.Max.Y) / 2.0,
                (box.Min.Z + box.Max.Z) / 2.0);
            return true;
        }

        private void RotateMarkerByChunks(Document document, ElementId markerId, Line rotateAxis, double totalAngleRadians)
        {
            if (document == null || markerId == null || markerId == ElementId.InvalidElementId || rotateAxis == null)
            {
                return;
            }

            if (Math.Abs(totalAngleRadians) <= 1e-9)
            {
                return;
            }

            // Ð”Ð»Ñ ElevationMarker Ñƒ Revit ÐµÑÑ‚ÑŒ Ð¸Ð·Ð²ÐµÑÑ‚Ð½Ñ‹Ðµ ÑÐ±Ð¾Ð¸ Ð½Ð° ÑƒÐ³Ð»Ð°Ñ… Ð¾ÐºÐ¾Ð»Ð¾ 180 Ð³Ñ€Ð°Ð´ÑƒÑÐ¾Ð².
            // ÐŸÐ¾Ð²Ð¾Ñ€Ð°Ñ‡Ð¸Ð²Ð°ÐµÐ¼ ÑˆÐ°Ð³Ð°Ð¼Ð¸ Ð½Ðµ Ð±Ð¾Ð»ÐµÐµ 30Â°, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð¿Ð¾Ð²ÐµÐ´ÐµÐ½Ð¸Ðµ Ð±Ñ‹Ð»Ð¾ ÑÑ‚Ð°Ð±Ð¸Ð»ÑŒÐ½ÐµÐµ.
            double maxChunk = Math.PI / 6.0; // 30Â°
            double sign = totalAngleRadians >= 0.0 ? 1.0 : -1.0;
            double remaining = Math.Abs(totalAngleRadians);

            while (remaining > 1e-9)
            {
                double chunk = Math.Min(remaining, maxChunk) * sign;
                ElementTransformUtils.RotateElement(document, markerId, rotateAxis, chunk);
                remaining -= Math.Abs(chunk);
            }
        }

        private bool TryCreateReplacementViewOnSameSheet(
            Document document,
            ViewSection sourceElevationView,
            Viewport sourceViewport,
            ViewStateSnapshot sourceViewState,
            IList<string> warnings,
            out ViewSection replacementView,
            out Viewport replacementViewport)
        {
            replacementView = null;
            replacementViewport = null;

            if (document == null || sourceElevationView == null || sourceViewport == null)
            {
                return false;
            }

            ViewSheet sourceSheet = document.GetElement(sourceViewport.OwnerViewId) as ViewSheet;
            if (sourceSheet == null)
            {
                AddWarning(warnings, "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ Ð»Ð¸ÑÑ‚, Ð½Ð° ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¼ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½ Ð¸ÑÑ…Ð¾Ð´Ð½Ñ‹Ð¹ Ñ„Ð°ÑÐ°Ð´.");
                return false;
            }

            XYZ sourceCenter = sourceViewport.GetBoxCenter();
            ElementId sourceViewportTypeId = sourceViewport.GetTypeId();
            string sourceViewName = sourceElevationView.Name;

            // Ð‘Ð»Ð¾Ðº Ð´ÑƒÐ±Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ Ñ„Ð°ÑÐ°Ð´Ð° Ñ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸ÐµÐ¹.
            ElementId duplicatedViewId = ElementId.InvalidElementId;
            try
            {
                if (sourceElevationView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                {
                    duplicatedViewId = sourceElevationView.Duplicate(ViewDuplicateOption.WithDetailing);
                }
                else if (sourceElevationView.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                {
                    duplicatedViewId = sourceElevationView.Duplicate(ViewDuplicateOption.Duplicate);
                    AddWarning(
                        warnings,
                        "Ð”ÑƒÐ±Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ 'Ð¡ Ð´ÐµÑ‚Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸ÐµÐ¹' Ð½ÐµÐ´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ð¾. Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½ Ð¾Ð±Ñ‹Ñ‡Ð½Ñ‹Ð¹ Duplicate.");
                }
                else
                {
                    AddWarning(warnings, "Ð’Ñ‹Ð±Ñ€Ð°Ð½Ð½Ñ‹Ð¹ Ñ„Ð°ÑÐ°Ð´ Ð½Ðµ Ð¿Ð¾Ð´Ð´ÐµÑ€Ð¶Ð¸Ð²Ð°ÐµÑ‚ Ð´ÑƒÐ±Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ.");
                    return false;
                }
            }
            catch (Exception duplicateException)
            {
                AddWarning(warnings, "ÐžÑˆÐ¸Ð±ÐºÐ° Ð´ÑƒÐ±Ð»Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ Ñ„Ð°ÑÐ°Ð´Ð°: " + duplicateException.Message);
                return false;
            }

            replacementView = document.GetElement(duplicatedViewId) as ViewSection;
            if (replacementView == null)
            {
                AddWarning(warnings, "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ ÑÐ¾Ð·Ð´Ð°Ð½Ð½Ñ‹Ð¹ Ð´ÑƒÐ±Ð»Ð¸ÐºÐ°Ñ‚ Ñ„Ð°ÑÐ°Ð´Ð°.");
                return false;
            }

            // Ð‘Ð»Ð¾Ðº Ð¸Ð¼ÐµÐ½Ð¾Ð²Ð°Ð½Ð¸Ñ: Ð½Ð¾Ð²Ñ‹Ð¹ Ð²Ð¸Ð´ ÑÑ‚Ð°Ñ€Ð°ÐµÐ¼ÑÑ Ð¾ÑÑ‚Ð°Ð²Ð¸Ñ‚ÑŒ Ñ Ð¸ÑÑ…Ð¾Ð´Ð½Ñ‹Ð¼ Ð¸Ð¼ÐµÐ½ÐµÐ¼,
            // Ð° Ð¸ÑÑ…Ð¾Ð´Ð½Ñ‹Ð¹ Ð¿ÐµÑ€ÐµÐ¸Ð¼ÐµÐ½Ð¾Ð²Ñ‹Ð²Ð°ÐµÐ¼ Ð² Ñ€ÐµÐ·ÐµÑ€Ð²Ð½Ð¾Ðµ Ð¸Ð¼Ñ.
            try
            {
                string sourceReservedName = BuildUniqueViewName(document, sourceViewName + "_Ð˜ÑÑ…Ð¾Ð´Ð½Ñ‹Ð¹");
                sourceElevationView.Name = sourceReservedName;

                string replacementName = BuildUniqueViewName(document, sourceViewName);
                replacementView.Name = replacementName;
            }
            catch (Exception renameException)
            {
                AddWarning(warnings, "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ Ð¿Ð¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ Ð¿Ñ€Ð¸Ð¼ÐµÐ½Ð¸Ñ‚ÑŒ ÑÑ…ÐµÐ¼Ñƒ Ð¸Ð¼ÐµÐ½Ð¾Ð²Ð°Ð½Ð¸Ñ: " + renameException.Message);
            }

            if (!Viewport.CanAddViewToSheet(document, sourceSheet.Id, replacementView.Id))
            {
                AddWarning(warnings, "ÐÐ¾Ð²Ñ‹Ð¹ Ñ„Ð°ÑÐ°Ð´ Ð½ÐµÐ»ÑŒÐ·Ñ Ñ€Ð°Ð·Ð¼ÐµÑÑ‚Ð¸Ñ‚ÑŒ Ð½Ð° Ð¸ÑÑ…Ð¾Ð´Ð½Ð¾Ð¼ Ð»Ð¸ÑÑ‚Ðµ.");
                return false;
            }

            try
            {
                replacementViewport = Viewport.Create(document, sourceSheet.Id, replacementView.Id, sourceCenter);
                if (replacementViewport == null)
                {
                    AddWarning(warnings, "ÐÐµ ÑƒÐ´Ð°Ð»Ð¾ÑÑŒ ÑÐ¾Ð·Ð´Ð°Ñ‚ÑŒ viewport Ð´Ð»Ñ Ð½Ð¾Ð²Ð¾Ð³Ð¾ Ñ„Ð°ÑÐ°Ð´Ð°.");
                    return false;
                }

                TryApplyViewportType(replacementViewport, sourceViewportTypeId);

                // Восстанавливаем ключевые настройки вида на новом фасаде.
                ApplyViewState(replacementView, sourceViewState, warnings);

                document.Delete(sourceViewport.Id);
                document.Delete(sourceElevationView.Id);
                return true;
            }
            catch (Exception placementException)
            {
                AddWarning(warnings, "ÐžÑˆÐ¸Ð±ÐºÐ° Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ñ Ð½Ð¾Ð²Ð¾Ð³Ð¾ Ñ„Ð°ÑÐ°Ð´Ð° Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ: " + placementException.Message);
                return false;
            }
        }

        private bool TryResolveElevationFromPlanElement(
            Document document,
            Element element,
            out ViewSection elevationView)
        {
            elevationView = null;

            if (document == null || element == null)
            {
                return false;
            }

            ViewSection selectedView = element as ViewSection;
            if (IsSupportedElevationView(selectedView))
            {
                elevationView = selectedView;
                return true;
            }

            ElevationMarker marker = element as ElevationMarker;
            if (marker != null)
            {
                ViewSection markerView = GetFirstHostedElevationView(document, marker);
                if (markerView != null)
                {
                    elevationView = markerView;
                    return true;
                }
            }

            // Ð’ Ð½ÐµÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ñ… ÑÐ»ÑƒÑ‡Ð°ÑÑ… Ð¿Ñ€Ð¸ Ð²Ñ‹Ð±Ð¾Ñ€Ðµ Ð³Ð¾Ð»Ð¾Ð²Ñ‹ Ñ„Ð°ÑÐ°Ð´Ð° Revit Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ Ð½Ðµ ViewSection,
            // Ð° ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚-Ð°Ð½Ð½Ð¾Ñ‚Ð°Ñ†Ð¸ÑŽ. Ð¢Ð¾Ð³Ð´Ð° Ð¿Ñ€Ð¾Ð±ÑƒÐµÐ¼ Ð²Ñ‹Ð¹Ñ‚Ð¸ Ð½Ð° Ñ„Ð°ÑÐ°Ð´ Ñ‡ÐµÑ€ÐµÐ· Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€ Ð¸Ð¼ÐµÐ½Ð¸ Ð²Ð¸Ð´Ð°.
            Parameter viewNameParameter = element.get_Parameter(BuiltInParameter.VIEW_NAME);
            string viewName = viewNameParameter != null ? viewNameParameter.AsString() : string.Empty;
            if (!string.IsNullOrWhiteSpace(viewName))
            {
                ViewSection resolvedByName = FindElevationViewByName(document, viewName);
                if (resolvedByName != null)
                {
                    elevationView = resolvedByName;
                    return true;
                }
            }

            return false;
        }

        private ViewSection GetFirstHostedElevationView(Document document, ElevationMarker marker)
        {
            if (document == null || marker == null)
            {
                return null;
            }

            int maxIndex = marker.MaximumViewCount;
            if (maxIndex <= 0)
            {
                maxIndex = 4;
            }

            for (int index = 0; index < maxIndex; index++)
            {
                ElementId hostedViewId;
                try
                {
                    hostedViewId = marker.GetViewId(index);
                }
                catch
                {
                    continue;
                }

                if (hostedViewId == null || hostedViewId == ElementId.InvalidElementId)
                {
                    continue;
                }

                ViewSection hostedView = document.GetElement(hostedViewId) as ViewSection;
                if (IsSupportedElevationView(hostedView))
                {
                    return hostedView;
                }
            }

            return null;
        }

        private ViewSection FindElevationViewByName(Document document, string viewName)
        {
            if (document == null || string.IsNullOrWhiteSpace(viewName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSection));
            foreach (Element element in collector)
            {
                ViewSection viewSection = element as ViewSection;
                if (!IsSupportedElevationView(viewSection))
                {
                    continue;
                }

                if (string.Equals(viewSection.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return viewSection;
                }
            }

            return null;
        }

        private bool TryFindElevationMarkerForView(
            Document document,
            ElementId viewId,
            out ElevationMarker marker,
            out int markerIndex)
        {
            marker = null;
            markerIndex = -1;

            if (document == null || viewId == null || viewId == ElementId.InvalidElementId)
            {
                return false;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ElevationMarker));
            foreach (Element element in collector)
            {
                ElevationMarker currentMarker = element as ElevationMarker;
                if (currentMarker == null)
                {
                    continue;
                }

                int maxIndex = currentMarker.MaximumViewCount;
                if (maxIndex <= 0)
                {
                    maxIndex = 4;
                }

                for (int index = 0; index < maxIndex; index++)
                {
                    ElementId hostedViewId;
                    try
                    {
                        hostedViewId = currentMarker.GetViewId(index);
                    }
                    catch
                    {
                        continue;
                    }

                    if (hostedViewId == null || hostedViewId == ElementId.InvalidElementId)
                    {
                        continue;
                    }

                    if (hostedViewId.IntegerValue == viewId.IntegerValue)
                    {
                        marker = currentMarker;
                        markerIndex = index;
                        return true;
                    }
                }
            }

            return false;
        }

        private Viewport FindViewportForView(Document document, ElementId viewId)
        {
            if (document == null || viewId == null || viewId == ElementId.InvalidElementId)
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(Viewport));
            foreach (Element element in collector)
            {
                Viewport viewport = element as Viewport;
                if (viewport == null)
                {
                    continue;
                }

                if (viewport.ViewId != null && viewport.ViewId != ElementId.InvalidElementId &&
                    viewport.ViewId.IntegerValue == viewId.IntegerValue)
                {
                    return viewport;
                }
            }

            return null;
        }

        private bool IsSupportedElevationView(ViewSection viewSection)
        {
            if (viewSection == null)
            {
                return false;
            }

            return viewSection.ViewType == ViewType.Elevation;
        }

        private Line TryGetStraightLine(DetailLine detailLine)
        {
            if (detailLine == null)
            {
                return null;
            }

            Curve curve = detailLine.GeometryCurve;
            if (curve == null)
            {
                LocationCurve locationCurve = detailLine.Location as LocationCurve;
                if (locationCurve != null)
                {
                    curve = locationCurve.Curve;
                }
            }

            return curve as Line;
        }

        private XYZ GetLineDirectionXY(DetailLine detailLine)
        {
            Line line = TryGetStraightLine(detailLine);
            if (line == null)
            {
                return XYZ.Zero;
            }

            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);
            XYZ direction = new XYZ(end.X - start.X, end.Y - start.Y, 0.0);
            if (direction.GetLength() <= DirectionEpsilon)
            {
                return XYZ.Zero;
            }

            return direction.Normalize();
        }

        private XYZ ReflectDirectionByLine(XYZ sourceDirection, XYZ lineDirection)
        {
            XYZ source = GetHorizontalDirection(sourceDirection);
            XYZ axis = GetHorizontalDirection(lineDirection);

            if (source.GetLength() <= DirectionEpsilon || axis.GetLength() <= DirectionEpsilon)
            {
                return XYZ.Zero;
            }

            // Ð¤Ð¾Ñ€Ð¼ÑƒÐ»Ð° Ð¾Ñ‚Ñ€Ð°Ð¶ÐµÐ½Ð¸Ñ Ð²ÐµÐºÑ‚Ð¾Ñ€Ð° Ð¾Ñ‚Ð½Ð¾ÑÐ¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾ Ð¾ÑÐ¸ (Ð»Ð¸Ð½Ð¸Ð¸):
            // reflected = 2 * projection(source, axis) - source
            double projectionLength = source.DotProduct(axis);
            XYZ projection = axis * projectionLength;
            XYZ reflected = projection * 2.0 - source;

            if (reflected.GetLength() <= DirectionEpsilon)
            {
                return XYZ.Zero;
            }

            return reflected.Normalize();
        }

        private XYZ GetHorizontalDirection(XYZ sourceDirection)
        {
            if (sourceDirection == null)
            {
                return XYZ.Zero;
            }

            XYZ horizontal = new XYZ(sourceDirection.X, sourceDirection.Y, 0.0);
            if (horizontal.GetLength() <= DirectionEpsilon)
            {
                return XYZ.Zero;
            }

            return horizontal.Normalize();
        }

        private double CalculateSignedAngle(XYZ fromDirection, XYZ toDirection)
        {
            XYZ from = GetHorizontalDirection(fromDirection);
            XYZ to = GetHorizontalDirection(toDirection);
            if (from.GetLength() <= DirectionEpsilon || to.GetLength() <= DirectionEpsilon)
            {
                return 0.0;
            }

            double angle = from.AngleTo(to);
            double crossZ = from.CrossProduct(to).Z;
            if (crossZ < 0.0)
            {
                angle = -angle;
            }

            return angle;
        }

        private string BuildUniqueViewName(Document document, string baseName)
        {
            string sanitizedBaseName = RevitNameUtils.SanitizeName(baseName, "Ð¤Ð°ÑÐ°Ð´");
            string candidate = sanitizedBaseName;

            int suffix = 1;
            while (DoesViewNameExist(document, candidate))
            {
                candidate = sanitizedBaseName + "_" + suffix;
                suffix++;
            }

            return candidate;
        }

        private bool DoesViewNameExist(Document document, string viewName)
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

        private void TryApplyViewportType(Viewport viewport, ElementId viewportTypeId)
        {
            if (viewport == null || viewportTypeId == null || viewportTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                if (viewport.CanHaveTypeAssigned())
                {
                    viewport.ChangeTypeId(viewportTypeId);
                }
            }
            catch
            {
                // ÐŸÑ€ÐµÑ€Ñ‹Ð²Ð°Ñ‚ÑŒ ÐºÐ¾Ð¼Ð°Ð½Ð´Ñƒ Ð¸Ð·-Ð·Ð° Ñ‚Ð¸Ð¿Ð° viewport Ð½ÐµÐ»ÑŒÐ·Ñ.
            }
        }

        private void AddWarning(IList<string> warnings, string warningText)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warningText))
            {
                return;
            }

            warnings.Add(warningText);
        }

        private ViewStateSnapshot CaptureViewState(ViewSection viewSection)
        {
            ViewStateSnapshot snapshot = new ViewStateSnapshot();
            if (viewSection == null)
            {
                return snapshot;
            }

            snapshot.Scale = viewSection.Scale;
            snapshot.ViewTemplateId = viewSection.ViewTemplateId;
            snapshot.DetailLevel = viewSection.DetailLevel;
            snapshot.DisplayStyle = viewSection.DisplayStyle;

            Parameter farClipParameter = viewSection.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
            if (farClipParameter != null && farClipParameter.StorageType == StorageType.Double)
            {
                snapshot.HasFarClipOffset = true;
                snapshot.FarClipOffset = farClipParameter.AsDouble();
            }

            return snapshot;
        }

        private void ApplyViewState(ViewSection viewSection, ViewStateSnapshot snapshot, IList<string> warnings)
        {
            if (viewSection == null || snapshot == null)
            {
                return;
            }

            try
            {
                if (snapshot.Scale > 0)
                {
                    viewSection.Scale = snapshot.Scale;
                }
            }
            catch (Exception ex)
            {
                AddWarning(warnings, "Не удалось восстановить масштаб вида: " + ex.Message);
            }

            try
            {
                if (snapshot.ViewTemplateId != null && snapshot.ViewTemplateId != ElementId.InvalidElementId)
                {
                    viewSection.ViewTemplateId = snapshot.ViewTemplateId;
                }
            }
            catch (Exception ex)
            {
                AddWarning(warnings, "Не удалось восстановить шаблон вида: " + ex.Message);
            }

            try
            {
                viewSection.DetailLevel = snapshot.DetailLevel;
            }
            catch (Exception ex)
            {
                AddWarning(warnings, "Не удалось восстановить детализацию вида: " + ex.Message);
            }

            try
            {
                viewSection.DisplayStyle = snapshot.DisplayStyle;
            }
            catch (Exception ex)
            {
                AddWarning(warnings, "Не удалось восстановить стиль отображения вида: " + ex.Message);
            }

            if (snapshot.HasFarClipOffset)
            {
                try
                {
                    Parameter farClipParameter = viewSection.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
                    if (farClipParameter != null && !farClipParameter.IsReadOnly && farClipParameter.StorageType == StorageType.Double)
                    {
                        farClipParameter.Set(snapshot.FarClipOffset);
                    }
                }
                catch (Exception ex)
                {
                    AddWarning(warnings, "Не удалось восстановить глубину проецирования вида: " + ex.Message);
                }
            }
        }

        private class ViewStateSnapshot
        {
            public int Scale { get; set; }

            public ElementId ViewTemplateId { get; set; }

            public ViewDetailLevel DetailLevel { get; set; }

            public DisplayStyle DisplayStyle { get; set; }

            public bool HasFarClipOffset { get; set; }

            public double FarClipOffset { get; set; }
        }

        private class PlanElevationSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element == null)
                {
                    return false;
                }

                ViewSection viewSection = element as ViewSection;
                if (viewSection != null)
                {
                    return viewSection.ViewType == ViewType.Elevation;
                }

                if (element is ElevationMarker)
                {
                    return true;
                }

                // ÐŸÐ¾Ð´Ð´ÐµÑ€Ð¶ÐºÐ° Ð²Ñ‹Ð±Ð¾Ñ€Ð° Ð°Ð½Ð½Ð¾Ñ‚Ð°Ñ†Ð¸Ð¾Ð½Ð½Ñ‹Ñ… "Ð³Ð¾Ð»Ð¾Ð²" Ñ„Ð°ÑÐ°Ð´Ð°:
                // ÐµÑÐ»Ð¸ ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚ ÑÐ¾Ð´ÐµÑ€Ð¶Ð¸Ñ‚ Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€ Ð¸Ð¼ÐµÐ½Ð¸ Ð²Ð¸Ð´Ð°, Ð´Ð°ÐµÐ¼ Ð²Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ ÐµÐ³Ð¾.
                Parameter viewNameParameter = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewNameParameter != null && !string.IsNullOrWhiteSpace(viewNameParameter.AsString()))
                {
                    return true;
                }

                return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        private class DetailLineSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is DetailLine;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}


