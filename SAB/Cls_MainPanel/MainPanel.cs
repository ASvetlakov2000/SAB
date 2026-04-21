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
                "Экспорт \nвсех категорий",
                "RevitLibraryBuilder.Commands.ExportTypesSingleFileCommand",
                "SAB.Resources.ExportTypesSingleFileCommand_32.png",
                "SAB.Resources.ExportTypesSingleFileCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportByCategories",
                "Экспорт \nпо категориям",
                "RevitLibraryBuilder.Commands.ExportTypesByCategoryCommand",
                "SAB.Resources.ExportTypesByCategoryCommand_32.png",
                "SAB.Resources.ExportTypesByCategoryCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLineAndFill",
                "Экспорт \nлиний/штриховок",
                "RevitLibraryBuilder.Commands.ExportLineAndFillPatternsCommand",
                "SAB.Resources.ExportLineAndFillPatternsCommand_32.png",
                "SAB.Resources.ExportLineAndFillPatternsCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportTypeNaming",
                "Экспорт \nимен типов",
                "RevitLibraryBuilder.Commands.ExportTypeNamingCommand",
                "SAB.Resources.ExportTypeNamingCommand_32.png",
                "SAB.Resources.ExportTypeNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportMaterialNaming",
                "Экспорт \nимен MTL",
                "RevitLibraryBuilder.Commands.ExportMaterialNamingCommand",
                "SAB.Resources.ExportMaterialNamingCommand_32.png",
                "SAB.Resources.ExportMaterialNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLoadableFamilyThumbnails",
                "Экспорт \nPNG семейств",
                "RevitLibraryBuilder.Commands.ExportLoadableFamilyThumbnailsCommand",
                "SAB.Resources.ExportLoadableFamilyThumbnailsCommand_32.png",
                "SAB.Resources.ExportLoadableFamilyThumbnailsCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportSystemFamilyThumbnailTemplate",
                "Экспорт \nPNG пирогов",
                "RevitLibraryBuilder.Commands.ExportSystemFamilyThumbnailTemplateCommand",
                "SAB.Resources.ExportSystemFamilyThumbnailTemplateCommand_32.png",
                "SAB.Resources.ExportSystemFamilyThumbnailTemplateCommand_16.png");


            libraryPanel.AddSeparator();

            // Блок кнопок импорта наименований и изображений
            SplitButton namingImportSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_NamingImportSplit", "Переименование")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                namingImportSplit,
                "SAB_ImportTypeNaming",
                "Переименовать \nтипы",
                "RevitLibraryBuilder.Commands.ImportTypeNamingCommand",
                "SAB.Resources.ImportTypeNamingCommand_32.png",
                "SAB.Resources.ImportTypeNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                namingImportSplit,
                "SAB_ImportMaterialNaming",
                "Переименовать \nматериалы",
                "RevitLibraryBuilder.Commands.ImportMaterialNamingCommand",
                "SAB.Resources.ImportMaterialNamingCommand_32.png",
                "SAB.Resources.ImportMaterialNamingCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                namingImportSplit,
                "SAB_LoadSystemFamilyTypeImages",
                "Загрузить \nPNG пироги",
                "RevitLibraryBuilder.Commands.LoadSystemFamilyTypeImagesCommand",
                "SAB.Resources.LoadSystemFamilyTypeImagesCommand_32.png",
                "SAB.Resources.LoadSystemFamilyTypeImagesCommand_16.png");

            libraryPanel.AddSeparator();

            // Блок кнопок размещения
            SplitButton placementSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_PlacementSplit", "Размещение")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByPoint",
                "Размещение \nпо точке",
                "RevitLibraryBuilder.Commands.ImportByPointCommand",
                "SAB.Resources.ImportByPointCommand_32.png",
                "SAB.Resources.ImportByPointCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByBoundary",
                "Размещение \nпо границе",
                "RevitLibraryBuilder.Commands.ImportByBoundaryCommand",
                "SAB.Resources.ImportByBoundaryCommand_32.png",
                "SAB.Resources.ImportByBoundaryCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlacementByLine",
                "Размещение \nпо линии",
                "RevitLibraryBuilder.Commands.ImportByLineCommand",
                "SAB.Resources.ImportByLineCommand_32.png",
                "SAB.Resources.ImportByLineCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlaceLegendComponentsByCategories",
                "Расставить \nкомп. легенды",
                "RevitLibraryBuilder.Commands.PlaceLegendComponentsByCategoriesCommand",
                "SAB.Resources.ExportLineAndFillPatternsCommand_32.png",
                "SAB.Resources.ExportLineAndFillPatternsCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlaceLineStyles",
                "Размещение \nлиний",
                "RevitLibraryBuilder.Commands.PlaceLineStylesCommand",
                "SAB.Resources.PlaceLineStylesCommand_32.png",
                "SAB.Resources.PlaceLineStylesCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                placementSplit,
                "SAB_PlaceFillPatterns",
                "Размещение \nштриховок",
                "RevitLibraryBuilder.Commands.PlaceFillPatternsCommand",
                "SAB.Resources.PlaceFillPatternsCommand_32.png",
                "SAB.Resources.PlaceFillPatternsCommand_16.png");

            libraryPanel.AddSeparator();
            

            // Блок кнопок удаления
            Ribbon.AddPushButtonSingle(
                libraryPanel,
                "SAB_DeleteElements",
                "Удаление \nэлементов",
                "RevitLibraryBuilder.Commands.DeleteSelectedTypesAndFamiliesCommand",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_32.png",
                "SAB.Resources.DeleteSelectedTypesAndFamiliesCommand_16.png");

            libraryPanel.AddSeparator();


            // Блок запуска MVP dashboard
            Ribbon.AddPushButtonSingle(
                libraryPanel,
                "SAB_GenerateDashboard",
                "HTML \nпросмотр",
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
