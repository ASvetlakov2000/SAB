using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationViewCreationResult
    {
        public ElevationViewCreationResult()
        {
            CreatedViews = new List<ElevationViewData>();
            FailedViews = new List<ElevationViewData>();
        }

        public List<ElevationViewData> CreatedViews { get; private set; }

        public List<ElevationViewData> FailedViews { get; private set; }
    }

    public class ElevationViewCreationService
    {
        private readonly ElevationMarkerService _markerService;
        private readonly ElevationCropService _cropService;
        private readonly ElevationNamingService _namingService;

        public ElevationViewCreationService(
            ElevationMarkerService markerService,
            ElevationCropService cropService,
            ElevationNamingService namingService)
        {
            _markerService = markerService;
            _cropService = cropService;
            _namingService = namingService;
        }

        public ElevationViewCreationResult CreateElevationViews(
            Document document,
            ViewPlan activePlanView,
            IList<ElevationLineData> elevationLines,
            ElevationSettings settings,
            IList<string> warnings)
        {
            ElevationViewCreationResult result = new ElevationViewCreationResult();

            if (document == null || activePlanView == null || elevationLines == null || settings == null)
            {
                return result;
            }

            for (int i = 0; i < elevationLines.Count; i++)
            {
                ElevationLineData lineData = elevationLines[i];
                ElevationViewData viewData = new ElevationViewData();
                viewData.SourceLineId = lineData.LineElementId;
                viewData.Index = lineData.Index;

                try
                {
                    ViewSection createdView = _markerService.CreateElevationForLine(
                        document,
                        activePlanView,
                        lineData,
                        settings.ElevationViewFamilyTypeId,
                        settings.ViewScale,
                        warnings);

                    if (createdView == null)
                    {
                        viewData.IsCreated = false;
                        viewData.FailureReason = "Elevation view was not created by ElevationMarker.";
                        result.FailedViews.Add(viewData);
                        continue;
                    }

                    // Block responsible for naming order and uniqueness.
                    string uniqueViewName = _namingService.GenerateUniqueElevationViewName(lineData.RoomData, lineData.Index, settings);
                    createdView.Name = uniqueViewName;

                    // Block responsible for assigning user-defined scale before optional template lock.
                    if (settings.ViewScale > 0)
                    {
                        createdView.Scale = settings.ViewScale;
                    }

                    bool cropApplied = _cropService.TryApplyCrop(createdView, lineData, settings, warnings);

                    bool templateApplied = false;
                    if (settings.ViewTemplateId != null && settings.ViewTemplateId != ElementId.InvalidElementId)
                    {
                        try
                        {
                            createdView.ViewTemplateId = settings.ViewTemplateId;
                            templateApplied = true;
                        }
                        catch (Exception templateException)
                        {
                            if (warnings != null)
                            {
                                warnings.Add(
                                    "Template was not applied to view " + uniqueViewName +
                                    ": " + templateException.Message);
                            }
                        }
                    }

                    viewData.ViewId = createdView.Id;
                    viewData.ViewSection = createdView;
                    viewData.ViewName = uniqueViewName;
                    viewData.IsCreated = true;
                    viewData.CropApplied = cropApplied;
                    viewData.TemplateApplied = templateApplied;

                    result.CreatedViews.Add(viewData);
                }
                catch (Exception exception)
                {
                    viewData.IsCreated = false;
                    viewData.FailureReason = exception.Message;
                    result.FailedViews.Add(viewData);

                    if (warnings != null)
                    {
                        warnings.Add(
                            "Failed to create elevation for line " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) +
                            ": " + exception.Message);
                    }
                }
            }

            return result;
        }
    }
}
