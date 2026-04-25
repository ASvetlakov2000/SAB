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
                    ElementId markerElementId;
                    ViewSection createdView = _markerService.CreateElevationForLine(
                        document,
                        activePlanView,
                        lineData,
                        settings.ElevationViewFamilyTypeId,
                        settings.ViewScale,
                        out markerElementId,
                        warnings);

                    if (createdView == null)
                    {
                        viewData.IsCreated = false;
                        viewData.FailureReason = "Вид развертки не был создан маркером.";
                        result.FailedViews.Add(viewData);
                        continue;
                    }

                    // Блок именования: последовательность 1-2, 2-3, 3-4 и т.д.
                    int startPointNumber = lineData.Index;
                    int endPointNumber = lineData.Index + 1;

                    string uniqueViewName = _namingService.GenerateUniqueElevationViewName(lineData.RoomData, startPointNumber, endPointNumber);
                    createdView.Name = uniqueViewName;

                    // Масштаб назначаем до шаблона, чтобы шаблон мог переопределить параметр при необходимости.
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
                                    "Не удалось применить шаблон к виду " + uniqueViewName +
                                    ": " + templateException.Message);
                            }
                        }
                    }

                    // После применения шаблона повторно включаем отображение границы обрезки.
                    try
                    {
                        createdView.CropBoxVisible = true;
                    }
                    catch
                    {
                        // Если шаблон заблокировал параметр, оставляем как есть без остановки команды.
                    }

                    viewData.ViewId = createdView.Id;
                    viewData.ViewSection = createdView;
                    viewData.ViewName = uniqueViewName;
                    viewData.IsCreated = true;
                    viewData.CropApplied = cropApplied;
                    viewData.TemplateApplied = templateApplied;
                    viewData.StartCornerNumber = startPointNumber;
                    viewData.EndCornerNumber = endPointNumber;
                    viewData.MarkerElementId = markerElementId;

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
                            "Не удалось создать развертку для линии " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) +
                            ": " + exception.Message);
                    }
                }
            }

            return result;
        }
    }
}
