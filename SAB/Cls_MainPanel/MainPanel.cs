using Autodesk.Revit.UI;
using SAB.Helpers;
using SAB.SyncReminder;
using System;

namespace SAB
{
    public class MainPanel : IExternalApplication
    {
        private const string RibbonTabName = "SAB";
        private const string RibbonPanelName = "Библиотека";
        private const string RegulationsPanelName = "Регламент";
        private const string InteriorElevationsPanelName = "Развертки";
        private const string RoomsPanelName = "Помещения";
        private const string InfoPanelName = "Инфо";
        private const string StructurePanelName = "Листы";

        private SyncReminderController _syncReminderController;

        internal static SyncReminderController CurrentSyncReminderController { get; private set; }

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

            RibbonPanel infoPanel = application.CreateRibbonPanel(RibbonTabName, InfoPanelName);
            RibbonPanel libraryPanel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
            RibbonPanel regulationsPanel = application.CreateRibbonPanel(RibbonTabName, RegulationsPanelName);
            RibbonPanel interiorElevationsPanel = application.CreateRibbonPanel(RibbonTabName, InteriorElevationsPanelName);
            RibbonPanel roomsPanel = application.CreateRibbonPanel(RibbonTabName, RoomsPanelName);
            RibbonPanel structurePanel = application.CreateRibbonPanel(RibbonTabName, StructurePanelName);


            /// Блок кнопок с инструкциями.
            Ribbon.AddPushButtonSingle(
                infoPanel,
                "SAB_OpenPluginInstructionsHtml",
                "Инструкции",
                "RevitLibraryBuilder.Commands.Regulations.OpenPluginInstructionsHtmlCommand",
                "SAB.Resources.OpenPluginInstructionsHtmlCommand_32.png",
                "SAB.Resources.OpenPluginInstructionsHtmlCommand_16.png");

            Ribbon.AddPushButtonSingle(
                infoPanel,
                "SAB_SyncReminderSettings",
                "Таймер\nсинхронизации",
                "SAB.SyncReminder.SyncReminderSettingsCommand",
                "SAB.Resources.SyncReminderSettings_32.png",
                "SAB.Resources.SyncReminderSettings_16.png");


            // Единая точка входа для всех операций RevitLibraryBuilder.
            Ribbon.AddPushButtonSingle(
                libraryPanel,
                "SAB_OpenLibraryBuilder",
                "Библиотека",
                "RevitLibraryBuilder.Commands.OpenLibraryBuilderCommand",
                "SAB.Resources.ExportTypesSingleFileCommand_32.png",
                "SAB.Resources.ExportTypesSingleFileCommand_16.png");


            /// Блок кнопок регламентов и инструкций в HTML.
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


            /// Блок кнопок для структуры проекта.
            Ribbon.AddPushButtonSingle(
            structurePanel,
                "SAB_CreateViewsAndSheets",
                "Создать виды\nи листы",
                "SAB.CreateViewsAndSheets.Commands.CreateViewsAndSheetsCommand",
                "SAB.Resources.CreateViewsAndSheets_32.png",
                "SAB.Resources.CreateViewsAndSheets_16.png");

            Ribbon.AddPushButtonSingle(
            structurePanel,
                "SAB_DeleteViewsAndSheets",
                "Удалить виды\nи листы",
                "SAB.CreateViewsAndSheets.Commands.DeleteViewsAndSheetsCommand",
                "SAB.Resources.DeleteViewsAndSheets_32.png",
                "SAB.Resources.DeleteViewsAndSheets_16.png");

            Ribbon.AddPushButtonSingle(
            structurePanel,
                "SAB_EditViewTemplateGraphics",
                "Редактор шаблонов\nвидов",
                "SAB.ViewTemplateGraphics.Commands.EditViewTemplateGraphicsCommand",
                "SAB.Resources.CreateViewsAndSheets_32.png",
                "SAB.Resources.CreateViewsAndSheets_16.png");

            StartSyncReminder(application);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            StopSyncReminder();
            return Result.Succeeded;
        }

        private void StartSyncReminder(UIControlledApplication application)
        {
            try
            {
                _syncReminderController = new SyncReminderController(application);
                CurrentSyncReminderController = _syncReminderController;
                _syncReminderController.Start();
            }
            catch (Exception exception)
            {
                CurrentSyncReminderController = null;
                _syncReminderController = null;

                TaskDialog.Show(
                    "SAB Sync Reminder Startup Debug",
                    "Step: start sync reminder inside SAB.MainPanel\n" +
                    "Revit version: " + GetRevitVersionText(application) + "\n" +
                    "Exception:\n" +
                    exception);
            }
        }

        private void StopSyncReminder()
        {
            try
            {
                if (_syncReminderController != null)
                {
                    _syncReminderController.Stop();
                    _syncReminderController = null;
                }

                CurrentSyncReminderController = null;
            }
            catch (Exception exception)
            {
                TaskDialog.Show("SAB Sync Reminder Shutdown Debug", exception.ToString());
            }
        }

        private static string GetRevitVersionText(UIControlledApplication application)
        {
            if (application == null || application.ControlledApplication == null)
            {
                return "unknown";
            }

            try
            {
                return application.ControlledApplication.VersionName +
                       " / " +
                       application.ControlledApplication.VersionNumber +
                       " / " +
                       application.ControlledApplication.VersionBuild;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
