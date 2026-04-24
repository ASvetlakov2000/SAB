using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Geometry;
using SAB.InteriorElevations.Services.Reports;
using SAB.InteriorElevations.Services.Rooms;
using SAB.InteriorElevations.Services.Selection;
using SAB.InteriorElevations.Services.Sheets;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateInteriorElevationsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication.ActiveUIDocument;
                if (uiDocument == null)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Active UI document is not available.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                if (document == null)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Active Revit document is not available.");
                    return Result.Failed;
                }

                View activeView = document.ActiveView;
                if (!IsSupportedPlanView(activeView))
                {
                    TaskDialog.Show("SAB Interior Elevations", "Active view must be a floor plan or ceiling plan.");
                    return Result.Cancelled;
                }

                ViewPlan activePlanView = activeView as ViewPlan;
                if (activePlanView == null)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Active plan view is invalid.");
                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();

                // Block 1: selection and line ordering by stable ElementId.
                DetailLineSelectionService selectionService = new DetailLineSelectionService();
                DetailLineSelectionResult selectionResult = selectionService.GetSelectedLines(uiDocument, activeView);

                AppendWarnings(warnings, selectionResult.Warnings);

                if (selectionResult.Lines.Count == 0)
                {
                    TaskDialog.Show(
                        "SAB Interior Elevations",
                        "No valid detail lines selected. Select detail lines in the active plan view and run the command again.");

                    return Result.Cancelled;
                }

                // Block 2: settings window (MVVM).
                ElevationSettingsViewModel settingsViewModel = new ElevationSettingsViewModel(document);
                ElevationSettingsWindow settingsWindow = new ElevationSettingsWindow(settingsViewModel);

                bool? dialogResult = settingsWindow.ShowDialog();
                if (!dialogResult.HasValue || dialogResult.Value == false)
                {
                    return Result.Cancelled;
                }

                ElevationSettings settings = settingsWindow.SelectedSettings;
                if (settings == null)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Settings were not returned from the dialog window.");
                    return Result.Cancelled;
                }

                string settingsValidationMessage;
                if (!ValidateSettings(document, settings, out settingsValidationMessage))
                {
                    TaskDialog.Show("SAB Interior Elevations", settingsValidationMessage);
                    return Result.Cancelled;
                }

                // Block 3: geometry model conversion.
                ElevationGeometryService elevationGeometryService = new ElevationGeometryService();
                List<ElevationLineData> elevationLines = elevationGeometryService.BuildElevationLineData(selectionResult.Lines, warnings);
                if (elevationLines.Count == 0)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Selected lines do not contain valid linear geometry.");
                    return Result.Cancelled;
                }

                // Block 4: room detection.
                RoomDetectionService roomDetectionService = new RoomDetectionService();
                RoomData roomData;
                if (!roomDetectionService.TryDetectRoom(document, elevationLines, out roomData, warnings))
                {
                    TaskDialog.Show(
                        "SAB Interior Elevations",
                        "Room could not be detected from selected lines. Ensure the contour is inside a valid room and run the command again.");

                    return Result.Cancelled;
                }

                // Block 5: orientation rule (point 0 = left, point 1 = right, view direction = inside room).
                LineOrientationService lineOrientationService = new LineOrientationService();
                bool orientationAssigned = lineOrientationService.TryAssignInsideNormals(document, elevationLines, roomData, settings.MarkerOffsetMm, warnings);
                if (!orientationAssigned)
                {
                    TaskDialog.Show("SAB Interior Elevations", "Failed to resolve inside normals for selected lines.");
                    return Result.Cancelled;
                }

                ElevationNamingService namingService = new ElevationNamingService(document);
                ElevationMarkerService markerService = new ElevationMarkerService();
                ElevationCropService cropService = new ElevationCropService();

                ElevationViewCreationService viewCreationService = new ElevationViewCreationService(
                    markerService,
                    cropService,
                    namingService);

                SheetCreationService sheetCreationService = new SheetCreationService();
                ViewportPlacementService viewportPlacementService = new ViewportPlacementService();
                ElevationCreationReportService reportService = new ElevationCreationReportService();

                ElevationViewCreationResult creationResult;
                ViewSheet createdSheet = null;
                int placedViewportCount = 0;

                using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Interior Elevations"))
                {
                    transactionGroup.Start();

                    using (Transaction transaction = new Transaction(document, "Create Interior Elevations"))
                    {
                        transaction.Start();

                        creationResult = viewCreationService.CreateElevationViews(
                            document,
                            activePlanView,
                            elevationLines,
                            settings,
                            warnings);

                        if (settings.CreateSheet && creationResult.CreatedViews.Count > 0)
                        {
                            createdSheet = sheetCreationService.CreateSheet(document, settings, roomData, namingService);

                            if (createdSheet != null)
                            {
                                placedViewportCount = viewportPlacementService.PlaceViewsOnSheet(
                                    document,
                                    createdSheet,
                                    creationResult.CreatedViews,
                                    settings.SheetLayoutSettings,
                                    warnings);
                            }
                            else
                            {
                                warnings.Add("Sheet creation was enabled, but the sheet was not created.");
                            }
                        }

                        transaction.Commit();
                    }

                    transactionGroup.Assimilate();
                }

                reportService.ShowFinalReport(
                    selectionResult.Lines.Count,
                    creationResult,
                    createdSheet,
                    placedViewportCount,
                    warnings);

                return creationResult.CreatedViews.Count > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("SAB Interior Elevations", "Unexpected error: " + exception.Message);
                return Result.Failed;
            }
        }

        private bool IsSupportedPlanView(View view)
        {
            if (view == null)
            {
                return false;
            }

            return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan;
        }

        private void AppendWarnings(List<string> target, IList<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private bool ValidateSettings(Document document, ElevationSettings settings, out string validationMessage)
        {
            validationMessage = string.Empty;

            if (settings.ElevationViewFamilyTypeId == null || settings.ElevationViewFamilyTypeId == ElementId.InvalidElementId)
            {
                validationMessage = "Elevation ViewFamilyType is not selected.";
                return false;
            }

            ViewFamilyType viewFamilyType = document.GetElement(settings.ElevationViewFamilyTypeId) as ViewFamilyType;
            if (viewFamilyType == null || viewFamilyType.ViewFamily != ViewFamily.Elevation)
            {
                validationMessage = "Selected elevation ViewFamilyType is invalid.";
                return false;
            }

            if (settings.ViewTemplateId != null && settings.ViewTemplateId != ElementId.InvalidElementId)
            {
                View templateView = document.GetElement(settings.ViewTemplateId) as View;
                if (templateView == null || !templateView.IsTemplate)
                {
                    validationMessage = "Selected view template does not exist or is not a template.";
                    return false;
                }
            }

            if (settings.ViewScale <= 0)
            {
                validationMessage = "View scale must be greater than zero.";
                return false;
            }

            if (settings.ViewDepthMm <= 0)
            {
                validationMessage = "View depth must be greater than zero.";
                return false;
            }

            if (settings.MarkerOffsetMm <= 0)
            {
                validationMessage = "Marker offset must be greater than zero.";
                return false;
            }

            if (settings.TopOffsetMm < 0 || settings.BottomOffsetMm < 0 || settings.LeftOffsetMm < 0 || settings.RightOffsetMm < 0)
            {
                validationMessage = "Crop offsets must be zero or greater.";
                return false;
            }

            if (settings.CreateSheet)
            {
                if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
                {
                    validationMessage = "Sheet creation is enabled, but title block type is not selected.";
                    return false;
                }

                FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
                if (titleBlockType == null)
                {
                    validationMessage = "Selected title block type does not exist.";
                    return false;
                }

                if (settings.SheetLayoutSettings == null)
                {
                    validationMessage = "Sheet layout settings are not defined.";
                    return false;
                }

                if (settings.SheetLayoutSettings.ColumnsCount <= 0)
                {
                    validationMessage = "Columns count must be greater than zero.";
                    return false;
                }

                if (settings.SheetLayoutSettings.StepXmm <= 0 || settings.SheetLayoutSettings.StepYmm <= 0)
                {
                    validationMessage = "Sheet step values must be greater than zero.";
                    return false;
                }
            }

            return true;
        }
    }
}
