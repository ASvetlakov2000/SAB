using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Commands;
using RevitLibraryBuilder.Models;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Routes a Library Builder window request to the existing production command.
    /// The dispatcher does not create transactions because every existing operation
    /// preserves its own validation and transaction boundaries.
    /// </summary>
    public class LibraryCommandDispatcher
    {
        public Result Execute(
            LibraryToolId toolId,
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            IExternalCommand command = CreateCommand(toolId);

            if (command == null)
            {
                message = "Не найдена команда для выбранной операции библиотеки: " + toolId;
                return Result.Failed;
            }

            return command.Execute(commandData, ref message, elements);
        }

        // Block responsible for the explicit mapping between window operations and legacy commands.
        private static IExternalCommand CreateCommand(LibraryToolId toolId)
        {
            switch (toolId)
            {
                case LibraryToolId.ExportSystemFamilies:
                    return new ExportSystemFamiliesCommand();
                case LibraryToolId.ExportLoadableFamilies:
                    return new ExportLoadableFamiliesCommand();
                case LibraryToolId.ExportLoadableFamilyThumbnails:
                    return new ExportLoadableFamilyThumbnailsCommand();
                case LibraryToolId.ExportTypeNaming:
                    return new ExportTypeNamingCommand();
                case LibraryToolId.ImportTypeNaming:
                    return new ImportTypeNamingCommand();
                case LibraryToolId.ExportMaterialNaming:
                    return new ExportMaterialNamingCommand();
                case LibraryToolId.ImportMaterialNaming:
                    return new ImportMaterialNamingCommand();
                case LibraryToolId.ExportLineStyles:
                    return new ExportLineStylesCommand();
                case LibraryToolId.PlaceLineStyles:
                    return new PlaceLineStylesCommand();
                case LibraryToolId.ExportLineStylesPreviewPng:
                    return new ExportLineStylesPreviewPngCommand();
                case LibraryToolId.ExportFillPatterns:
                    return new ExportFillPatternsCommand();
                case LibraryToolId.PlaceFillPatterns:
                    return new PlaceFillPatternsCommand();
                case LibraryToolId.ExportFillPatternsPreviewPng:
                    return new ExportFillPatternsPreviewPngCommand();
                case LibraryToolId.PlaceLegendComponentsByCategories:
                    return new PlaceLegendComponentsByCategoriesCommand();
                case LibraryToolId.ExportSystemFamilyThumbnailTemplate:
                    return new ExportSystemFamilyThumbnailTemplateCommand();
                case LibraryToolId.LoadSystemFamilyTypeImages:
                    return new LoadSystemFamilyTypeImagesCommand();
                case LibraryToolId.ImportByPoint:
                    return new ImportByPointCommand();
                case LibraryToolId.ImportByLine:
                    return new ImportByLineCommand();
                case LibraryToolId.ImportByBoundary:
                    return new ImportByBoundaryCommand();
                case LibraryToolId.DeleteSelectedTypesAndFamilies:
                    return new DeleteSelectedTypesAndFamiliesCommand();
                case LibraryToolId.GenerateDashboard:
                    return new SAB.BimDashboard.Commands.GenerateDashboardCommand();
                default:
                    throw new ArgumentOutOfRangeException("toolId", toolId, "Неизвестная операция библиотеки.");
            }
        }
    }
}
