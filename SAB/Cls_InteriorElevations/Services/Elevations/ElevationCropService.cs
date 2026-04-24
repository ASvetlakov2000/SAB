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
                // Block responsible for turning crop region on and hiding crop boundary in final views.
                viewSection.CropBoxActive = true;
                viewSection.CropBoxVisible = false;

                BoundingBoxXYZ cropBox = viewSection.CropBox;
                if (cropBox == null || cropBox.Transform == null)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Crop box is unavailable for view " + viewSection.Name + ".");
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
                double depthFeet = UnitConversionUtils.MillimetersToFeet(settings.ViewDepthMm);

                double minimumSizeFeet = UnitConversionUtils.MillimetersToFeet(10.0);

                double minX = Math.Min(startLocal.X, endLocal.X) - leftOffsetFeet;
                double maxX = Math.Max(startLocal.X, endLocal.X) + rightOffsetFeet;
                if (maxX - minX < minimumSizeFeet)
                {
                    maxX = minX + minimumSizeFeet;
                }

                double minY = -bottomOffsetFeet;
                double maxY = topOffsetFeet;
                if (maxY - minY < minimumSizeFeet)
                {
                    maxY = minY + minimumSizeFeet;
                }

                double currentMinZ = Math.Min(cropBox.Min.Z, cropBox.Max.Z);
                double minZ = currentMinZ;
                double maxZ = currentMinZ + Math.Max(depthFeet, minimumSizeFeet);

                BoundingBoxXYZ newCropBox = new BoundingBoxXYZ();
                newCropBox.Transform = cropBox.Transform;
                newCropBox.Min = new XYZ(minX, minY, minZ);
                newCropBox.Max = new XYZ(maxX, maxY, maxZ);

                viewSection.CropBox = newCropBox;

                // Block responsible for controlling the far clipping depth.
                Parameter farClipOffsetParameter = viewSection.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
                if (farClipOffsetParameter != null && !farClipOffsetParameter.IsReadOnly)
                {
                    farClipOffsetParameter.Set(Math.Max(depthFeet, minimumSizeFeet));
                }

                return true;
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Crop setup failed for line " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) + ": " + exception.Message);
                }

                return false;
            }
        }
    }
}
