using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationCropService
    {
        public bool TryApplyCrop(ViewSection viewSection, ElevationLineData lineData, ElevationSettings settings, IList<string> warnings)
        {
            if (viewSection == null || lineData == null || settings == null)
            {
                return false;
            }

            try
            {
                // Блок включения обрезки и скрытия рамки обрезки.
                viewSection.CropBoxActive = true;
                viewSection.CropBoxVisible = true;

                BoundingBoxXYZ cropBox = viewSection.CropBox;
                if (cropBox == null || cropBox.Transform == null)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Для вида " + viewSection.Name + " недоступна рамка обрезки.");
                    }

                    return false;
                }

                Transform inverse = cropBox.Transform.Inverse;
                XYZ startLocal = inverse.OfPoint(lineData.StartPoint);
                XYZ endLocal = inverse.OfPoint(lineData.EndPoint);

                double leftOffsetFeet = UnitConversionUtils.MillimetersToFeet(settings.LeftOffsetMm);
                double rightOffsetFeet = UnitConversionUtils.MillimetersToFeet(settings.RightOffsetMm);
                double topOffsetFeet = UnitConversionUtils.MillimetersToFeet(settings.TopOffsetMm);
                double bottomOffsetFeet = UnitConversionUtils.MillimetersToFeet(settings.BottomOffsetMm);

                double minimumSizeFeet = UnitConversionUtils.MillimetersToFeet(10.0);

                // Ширина по правилу точки 0-1: start это левая точка, end - правая.
                double minX = startLocal.X - leftOffsetFeet;
                double maxX = endLocal.X + rightOffsetFeet;
                EnsureMinMax(ref minX, ref maxX, minimumSizeFeet);

                // Высота относительно средней высоты линии.
                double lineCenterY = (startLocal.Y + endLocal.Y) / 2.0;
                double minY = lineCenterY - bottomOffsetFeet;
                double maxY = lineCenterY + topOffsetFeet;
                EnsureMinMax(ref minY, ref maxY, minimumSizeFeet);

                // По глубине оставляем существующие границы CropBox и управляем глубиной через Far Clip Offset.
                double minZ = Math.Min(cropBox.Min.Z, cropBox.Max.Z);
                double maxZ = Math.Max(cropBox.Min.Z, cropBox.Max.Z);
                EnsureMinMax(ref minZ, ref maxZ, minimumSizeFeet);

                BoundingBoxXYZ newCropBox = new BoundingBoxXYZ();
                newCropBox.Transform = cropBox.Transform;
                newCropBox.Min = new XYZ(minX, minY, minZ);
                newCropBox.Max = new XYZ(maxX, maxY, maxZ);
                viewSection.CropBox = newCropBox;

                // Блок управления глубиной проецирования по выбранной линии.
                ApplyFarClipOffset(viewSection, settings.ViewDepthMm, settings.MarkerOffsetMm, warnings);

                return true;
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось настроить обрезку для линии " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) + ": " + exception.Message);
                }

                return false;
            }
        }

        private void ApplyFarClipOffset(ViewSection viewSection, double requestedDepthMm, double facadeOffsetMm, IList<string> warnings)
        {
            Parameter farClipOffsetParameter = viewSection.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
            if (farClipOffsetParameter == null || farClipOffsetParameter.IsReadOnly)
            {
                if (warnings != null)
                {
                    warnings.Add("Параметр 'Смещение дальнего предела секущего диапазона' недоступен для вида " + viewSection.Name + ".");
                }

                return;
            }

            double requestedProjectionDepthFeet = UnitConversionUtils.MillimetersToFeet(requestedDepthMm);
            double facadeOffsetFeet = UnitConversionUtils.MillimetersToFeet(facadeOffsetMm);
            double requestedTotalFarClipOffsetFeet = requestedProjectionDepthFeet + facadeOffsetFeet;
            double minimumFarClipOffsetFeet = GetMinimumFarClipOffset(viewSection, farClipOffsetParameter);

            double finalFarClipOffsetFeet = requestedTotalFarClipOffsetFeet;
            if (finalFarClipOffsetFeet < minimumFarClipOffsetFeet)
            {
                finalFarClipOffsetFeet = minimumFarClipOffsetFeet;
                if (warnings != null)
                {
                    warnings.Add(
                        "Запрошенная итоговая глубина для вида " + viewSection.Name +
                        " меньше минимально допустимой. Применено минимальное значение " +
                        UnitConversionUtils.FeetToMillimeters(minimumFarClipOffsetFeet).ToString("F0") + " мм.");
                }
            }

            farClipOffsetParameter.Set(finalFarClipOffsetFeet);
            viewSection.Document.Regenerate();
        }

        private double GetMinimumFarClipOffset(ViewSection viewSection, Parameter farClipOffsetParameter)
        {
            double safeTinyOffset = UnitConversionUtils.MillimetersToFeet(1.0);

            try
            {
                double originalValue = farClipOffsetParameter.AsDouble();
                farClipOffsetParameter.Set(safeTinyOffset);
                viewSection.Document.Regenerate();

                double minimumValue = farClipOffsetParameter.AsDouble();

                farClipOffsetParameter.Set(originalValue);
                viewSection.Document.Regenerate();

                if (minimumValue <= 1e-9)
                {
                    return safeTinyOffset;
                }

                return minimumValue;
            }
            catch
            {
                return Math.Max(farClipOffsetParameter.AsDouble(), safeTinyOffset);
            }
        }

        private void EnsureMinMax(ref double minValue, ref double maxValue, double minimumSize)
        {
            if (minValue > maxValue)
            {
                double temp = minValue;
                minValue = maxValue;
                maxValue = temp;
            }

            if (maxValue - minValue < minimumSize)
            {
                maxValue = minValue + minimumSize;
            }
        }

    }
}
