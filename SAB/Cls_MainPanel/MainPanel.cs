using Autodesk.Revit.UI;
using SAB.Helpers;

namespace SAB
{
    public class MainPanel : IExternalApplication
    {
        private const string RibbonTabName = "SAB";
        private const string RibbonPanelName = "Библиотека";
        private const string RegulationsPanelName = "Регламент";
        private const string InteriorElevationsPanelName = "Развертки";
        private const string RoomsPanelName = "Помещения";

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
            RibbonPanel regulationsPanel = application.CreateRibbonPanel(RibbonTabName, RegulationsPanelName);
            RibbonPanel interiorElevationsPanel = application.CreateRibbonPanel(RibbonTabName, InteriorElevationsPanelName);
            RibbonPanel roomsPanel = application.CreateRibbonPanel(RibbonTabName, RoomsPanelName);

            // Блок кнопок экспорта
            SplitButton exportSplit = libraryPanel.AddItem(
                new SplitButtonData("SAB_ExportSplit", "Экспорт")) as SplitButton;

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportSystemFamilies",
                "Экспорт \nсистемных",
                "RevitLibraryBuilder.Commands.ExportSystemFamiliesCommand",
                "SAB.Resources.ExportTypesSingleFileCommand_32.png",
                "SAB.Resources.ExportTypesSingleFileCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLoadableFamilies",
                "Экспорт \nзагружаемых",
                "RevitLibraryBuilder.Commands.ExportLoadableFamiliesCommand",
                "SAB.Resources.ExportTypesByCategoryCommand_32.png",
                "SAB.Resources.ExportTypesByCategoryCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLineStyles",
                "Экспорт \nлиний",
                "RevitLibraryBuilder.Commands.ExportLineStylesCommand",
                "SAB.Resources.PlaceLineStylesCommand_32.png",
                "SAB.Resources.PlaceLineStylesCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportFillPatterns",
                "Экспорт \nштриховок",
                "RevitLibraryBuilder.Commands.ExportFillPatternsCommand",
                "SAB.Resources.PlaceFillPatternsCommand_32.png",
                "SAB.Resources.PlaceFillPatternsCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportLineStylesPreviewPng",
                "Экспорт PNG \nлиний",
                "RevitLibraryBuilder.Commands.ExportLineStylesPreviewPngCommand",
                "SAB.Resources.PlaceLineStylesCommand_32.png",
                "SAB.Resources.PlaceLineStylesCommand_16.png");

            Ribbon.AddPushButtonToSplit(
                exportSplit,
                "SAB_ExportFillPatternsPreviewPng",
                "Экспорт PNG \nштриховок",
                "RevitLibraryBuilder.Commands.ExportFillPatternsPreviewPngCommand",
                "SAB.Resources.PlaceFillPatternsCommand_32.png",
                "SAB.Resources.PlaceFillPatternsCommand_16.png");

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
                "SAB.Resources.PlaceFillPatternsCommand_32.png",
                "SAB.Resources.PlaceFillPatternsCommand_16.png");

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


            //// Блок кнопок регламентов и инструкций в HTML.
            // Блок кнопок регламентов и инструкций в HTML.
            Ribbon.AddPushButtonSingle(
                regulationsPanel,
                "SAB_OpenNamingStandardsHtml",
                "Стандарты \nНаименования",
                "RevitLibraryBuilder.Commands.Regulations.OpenNamingStandardsHtmlCommand",
                "SAB.Resources.OpenNamingStandardsHtmlCommand_32.png",
                "SAB.Resources.OpenNamingStandardsHtmlCommand_16.png");

            // Блок кнопок для плагина внутренних разверток.
            Ribbon.AddPushButtonSingle(
                interiorElevationsPanel,
                "SAB_CreateInteriorElevations",
                "Создать развертки\nпо линии",
                "SAB.InteriorElevations.Commands.CreateInteriorElevationsCommand",
                "SAB.Resources.CreateInteriorElevationsCommand_32.png",
                "SAB.Resources.CreateInteriorElevationsCommand_16.png");

            Ribbon.AddPushButtonSingle(
                interiorElevationsPanel,
                "SAB_FlipElevation180ByLine",
                "Разворот\nразвертки 180",
                "SAB.InteriorElevations.Commands.FlipElevation180ByLineCommand",
                "SAB.Resources.FlipElevation180ByLineCommand_32.png",
                "SAB.Resources.FlipElevation180ByLineCommand_16.png");

            Ribbon.AddPushButtonSingle(
                interiorElevationsPanel,
                "SAB_MoveInteriorElevationViewports",
                "Перенос видов\nна след. лист",
                "SAB.InteriorElevations.Commands.MoveElevationViewportsToNewSheetCommand",
                "SAB.Resources.MoveElevationViewportsToNewSheetCommand_32.png",
                "SAB.Resources.MoveElevationViewportsToNewSheetCommand_16.png");

            Ribbon.AddPushButtonSingle(
                interiorElevationsPanel,
                "SAB_AlignPlanCornerMarks",
                "Выровнять марки\nуглов",
                "SAB.InteriorElevations.Commands.AlignPlanCornerMarksCommand",
                "SAB.Resources.AlignPlanCornerMarksCommand_32.png",
                "SAB.Resources.AlignPlanCornerMarksCommand_16.png");


            // Блок кнопок для проверки геометрии помещений.
            Ribbon.AddPushButtonSingle(
                roomsPanel,
                "SAB_OpenRoomGeometryTools",
                "Проверка геометрии\nпомещений",
                "SAB.RoomGeometryTools.Commands.OpenRoomGeometryToolsCommand",
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
