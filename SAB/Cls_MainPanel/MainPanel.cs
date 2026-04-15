using Autodesk.Revit.UI;
using SAB.Helpers;

namespace SAB
{
    public class MainPanel : IExternalApplication
    {
        private const string RibbonTabName = "SAB";
        private const string RibbonPanelName = "Библиотека";

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

            RibbonPanel libraryPanel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

            // Блок кнопок экспорта
            SplitButton exportSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_ExportSplit", "Экспорт")) as SplitButton;

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
                "SAB.Resources.ExportLineAndFillPatternsCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportTypeNaming",
                "Экспорт имен типов",
                "RevitLibraryBuilder.Commands.ExportTypeNamingCommand",
                "SAB.Resources.ExportTypeNamingCommand_32.png",
                "SAB.Resources.ExportTypeNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportMaterialNaming",
                "Экспорт имен MTL",
                "RevitLibraryBuilder.Commands.ExportMaterialNamingCommand",
                "SAB.Resources.ExportMaterialNamingCommand_32.png",
                "SAB.Resources.ExportMaterialNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLoadableFamilyThumbnails",
                "Экспорт PNG семейств",
                "RevitLibraryBuilder.Commands.ExportLoadableFamilyThumbnailsCommand",
                "SAB.Resources.ExportTypeNamingCommand_32.png",
                "SAB.Resources.ExportTypeNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportSystemFamilyThumbnailTemplate",
                "Экспорт PNG пирогов",
                "RevitLibraryBuilder.Commands.ExportSystemFamilyThumbnailTemplateCommand",
                "SAB.Resources.ExportMaterialNamingCommand_32.png",
                "SAB.Resources.ExportMaterialNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_LoadSystemFamilyTypeImages",
                "Загрузить Type Image",
                "RevitLibraryBuilder.Commands.LoadSystemFamilyTypeImagesCommand",
                "SAB.Resources.ImportMaterialNamingCommand_32.png",
                "SAB.Resources.ImportMaterialNamingCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок кнопок импорта наименований
            SplitButton namingImportSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_NamingImportSplit", "Переименование")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                namingImportSplit,
                "SAB_ImportTypeNaming",
                "Переименовать типы",
                "RevitLibraryBuilder.Commands.ImportTypeNamingCommand",
                "SAB.Resources.ImportTypeNamingCommand_32.png",
                "SAB.Resources.ImportTypeNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                namingImportSplit,
                "SAB_ImportMaterialNaming",
                "Переименовать материалы",
                "RevitLibraryBuilder.Commands.ImportMaterialNamingCommand",
                "SAB.Resources.ImportMaterialNamingCommand_32.png",
                "SAB.Resources.ImportMaterialNamingCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок кнопок размещения
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

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlaceLegendComponentsByCategories",
                "Расставить комп. легенды",
                "RevitLibraryBuilder.Commands.PlaceLegendComponentsByCategoriesCommand",
                "SAB.Resources.ExportLineAndFillPatternsCommand_32.png",
                "SAB.Resources.ExportLineAndFillPatternsCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок кнопок аннотаций
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

            // Блок кнопок удаления
            SplitButton deleteSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_DeleteSplit", "Удаление")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                deleteSplit,
                "SAB_DeleteElements",
                "Удаление элементов",
                "RevitLibraryBuilder.Commands.DeleteSelectedTypesAndFamiliesCommand",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_32.png",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок запуска MVP dashboard
            Ribbon.AddPushButtonSingle(
                libraryPanel,
                "SAB_GenerateDashboard",
                "HTML просмотр",
                "SAB.BimDashboard.Commands.GenerateDashboardCommand",
                "SAB.Resources.GenerateDashboardCommand_32.png",
                "SAB.Resources.GenerateDashboardCommand_16.png");

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
