using Autodesk.Revit.UI;
using SAB.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SAB.Properties;

namespace SAB
{
    public class MainPanel : IExternalApplication
    {
        private const string RibbonTabName = "SAB";
        private const string RibbonPanelName = "Библиотека";

        // Путь к embedded ресурсам: <DefaultNamespace>.Resources.<FileName>
        private const string EmbeddedResourcePrefix = "SAB.Cls_RevitLibraryBuilder.Resources.";

        public Result OnStartup(UIControlledApplication application)
        {
            // Блок создания вкладки SAB
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch
            {
                // Вкладка уже существует.
            }

         
            // Блок создания панели "Библиотека"
            RibbonPanel libraryPanel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

            // Блок создания SplitButton #1 (Экспорт)
            SplitButton exportSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_ExportSplit", "Экспорт")) as SplitButton;

            // Блок привязки иконок (_32 / _16) из embedded ресурсов
            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportAllCategories",
                "Экспорт всех категорий",
                "RevitLibraryBuilder.Commands.ExportTypesSingleFileCommand",
                "SAB.Resources.ExportTypesSingleFileCommand_32.png",
                "SAB.Resources.ExportTypesSingleFileCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportByCategories",
                "Экспорт по категориям",
                "RevitLibraryBuilder.Commands.ExportTypesByCategoryCommand",
                "SAB.Resources.ExportTypesByCategoryCommand_32.png",
                "SAB.Resources.ExportTypesByCategoryCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLineAndFill",
                "Экспорт линий/штриховок",
                "RevitLibraryBuilder.Commands.ExportLineAndFillPatternsCommand",
                "SAB.Resources.ExportLineAndFillPatternsCommand_32.png",
                "SAB.Resources.ExportLineAndFillPatternsCommand_32.png");

            libraryPanel.AddSeparator();

            // Блок создания SplitButton #2 (Размещение по категории)
            SplitButton placementSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_PlacementSplit", "Размещение")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByPoint",
                "Размещение по точке",
                "RevitLibraryBuilder.Commands.ImportByPointCommand",
               "SAB.Resources.ImportByPointCommand_32.png",
               "SAB.Resources.ImportByPointCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByBoundary",
                "Размещение по границе",
                "RevitLibraryBuilder.Commands.ImportByBoundaryCommand",
                "SAB.Resources.ImportByBoundaryCommand_32.png",
                "SAB.Resources.ImportByBoundaryCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByLine",
                "Размещение по линии",
                "RevitLibraryBuilder.Commands.ImportByLineCommand",
               "SAB.Resources.ImportByLineCommand_32.png",
               "SAB.Resources.ImportByLineCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок создания SplitButton #3 (Линии и штриховки)
            SplitButton annotationSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_AnnotationSplit", "Аннотации")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                annotationSplit,
                "SAB_PlaceLineStyles",
                "Размещение линий",
                "RevitLibraryBuilder.Commands.PlaceLineStylesCommand",
                "SAB.Resources.PlaceLineStylesCommand_32.png",
                "SAB.Resources.PlaceLineStylesCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                annotationSplit,
                "SAB_PlaceFillPatterns",
                "Размещение штриховок",
                "RevitLibraryBuilder.Commands.PlaceFillPatternsCommand",
                "SAB.Resources.PlaceFillPatternsCommand_32.png",
                "SAB.Resources.PlaceFillPatternsCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок создания SplitButton #4 (Удаление)
            SplitButton deleteSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_DeleteSplit", "Удаление")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                deleteSplit,
                "SAB_DeleteElements",
                "Удаление элементов",
                "RevitLibraryBuilder.Commands.DeleteSelectedTypesAndFamiliesCommand",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_32.png",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_16.png");

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        
    }
}
