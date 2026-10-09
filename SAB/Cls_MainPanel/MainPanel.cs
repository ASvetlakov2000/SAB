using Autodesk.Revit.UI;
using SAB.GklFrame;
using SAB.Helpers;
using SAB.SyncReminder;
using System;

namespace SAB
{
    public class MainPanel : IExternalApplication
    {
        private const string RibbonTabName = "SAB";
        private const string RibbonPanelName = "Библиотека";
        private const string SettingsPanelName = "Настройки";
        private const string AlbumPanelName = "Альбом и графика";
        private const string MaterialsPanelName = "Материалы и ведомости";
        private const string ParametersPanelName = "Параметры";
        private const string InteriorElevationsPanelName = "Развертки";
        private const string DoorWindowExplanationsPanelName = "Экспликации";
        private const string FramePanelName = FrameModuleConstants.RibbonPanelName;

        private SyncReminderController _syncReminderController;
        private Notifications.NotificationController _notificationController;

        internal static SyncReminderController CurrentSyncReminderController { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            SAB.UI.SabWindowBehaviorService.Initialize();
            // Блок создания вкладки SAB
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch
            {
                // Вкладка уже существует.
            }

            // Порядок создания панелей определяет их порядок на вкладке Revit.
            RibbonPanel settingsPanel = application.CreateRibbonPanel(RibbonTabName, SettingsPanelName);
            RibbonPanel libraryPanel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
            RibbonPanel albumPanel = application.CreateRibbonPanel(RibbonTabName, AlbumPanelName);
            RibbonPanel materialsPanel = application.CreateRibbonPanel(RibbonTabName, MaterialsPanelName);
            RibbonPanel parametersPanel = application.CreateRibbonPanel(RibbonTabName, ParametersPanelName);
            RibbonPanel interiorElevationsPanel = application.CreateRibbonPanel(RibbonTabName, InteriorElevationsPanelName);
            RibbonPanel doorWindowExplanationsPanel = application.CreateRibbonPanel(RibbonTabName, DoorWindowExplanationsPanelName);
            RibbonPanel framePanel = application.CreateRibbonPanel(RibbonTabName, FramePanelName);

            // Настройки.
            Ribbon.AddPushButtonSingle(
                settingsPanel,
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


            // Создание и удаление видов и листов.
            var albumButton = (SplitButton)albumPanel.AddItem(
                new SplitButtonData("SAB_ViewsAndSheets", "Альбом"));
            albumButton.IsSynchronizedWithCurrentItem = true;
            Ribbon.AddPushButtonToSplit(
                albumButton,
                "SAB_CreateViewsAndSheets",
                "Создать виды\nи листы",
                "SAB.CreateViewsAndSheets.Commands.CreateViewsAndSheetsCommand",
                "SAB.Resources.CreateViewsAndSheets_32.png",
                "SAB.Resources.CreateViewsAndSheets_16.png");

            Ribbon.AddPushButtonToSplit(
                albumButton,
                "SAB_DeleteViewsAndSheets",
                "Удалить виды\nи листы",
                "SAB.CreateViewsAndSheets.Commands.DeleteViewsAndSheetsCommand",
                "SAB.Resources.DeleteViewsAndSheets_32.png",
                "SAB.Resources.DeleteViewsAndSheets_16.png");

            // Графика.
            Ribbon.AddPushButtonSingle(
                albumPanel,
                "SAB_EditViewTemplateGraphics",
                "Редактор\nшаблонов",
                "SAB.ViewTemplateGraphics.Commands.EditViewTemplateGraphicsCommand",
                "SAB.Resources.EditViewTemplateGraphics_32.png",
                "SAB.Resources.EditViewTemplateGraphics_16.png");

            // Материалы и ведомости.
            materialsPanel.AddItem(new PushButtonData(
                "SAB_MaterialQuantity_EditRules", "Пересчет\nматериалов",
                typeof(MainPanel).Assembly.Location, "SAB.MaterialQuantity.EditMaterialRulesCommand")
            {
                ToolTip = "Пересчет материалов",
                LargeImage = Ribbon.GetMaterialQuantityImage(32),
                Image = Ribbon.GetMaterialQuantityImage(16)
            });

            // Области заливки: настройки и выполнение в одном окне.
            Ribbon.AddPushButtonSingle(
                albumPanel,
                "SAB_FilledRegionFromMaterial_Settings",
                "Области заливки\nиз материала",
                "SAB.FilledRegionFromMaterial.SettingsCommand",
                "SAB.Resources.FilledRegionCreate_32.png",
                "SAB.Resources.FilledRegionCreate_16.png");

            // Развертки: основная команда, три инструмента и две команды оформления.
            Ribbon.AddPushButtonSingle(
                interiorElevationsPanel,
                "SAB_CreateInteriorElevations",
                "Создать развертки\nпо линии",
                "SAB.InteriorElevations.Commands.CreateInteriorElevationsCommand",
                "SAB.Resources.CreateInteriorElevationsCommand_32.png",
                "SAB.Resources.CreateInteriorElevationsCommand_16.png");

            interiorElevationsPanel.AddStackedItems(
                Ribbon.CreatePushButtonData("SAB_FlipElevation180ByLine", "Повернуть на 180°",
                    "SAB.InteriorElevations.Commands.FlipElevation180ByLineCommand",
                    "SAB.Resources.FlipElevation180ByLineCommand_32.png", "SAB.Resources.FlipElevation180ByLineCommand_16.png"),
                Ribbon.CreatePushButtonData("SAB_AdjustElevationCrop", "Границы видов",
                    "SAB.InteriorElevations.Commands.AdjustElevationCropCommand",
                    "SAB.Resources.CreateInteriorElevationsCommand_32.png", "SAB.Resources.CreateInteriorElevationsCommand_16.png"),
                Ribbon.CreatePushButtonData("SAB_MoveInteriorElevationViewports", "На следующий лист",
                    "SAB.InteriorElevations.Commands.MoveElevationViewportsToNewSheetCommand",
                    "SAB.Resources.MoveElevationViewportsToNewSheetCommand_32.png", "SAB.Resources.MoveElevationViewportsToNewSheetCommand_16.png"));

            interiorElevationsPanel.AddStackedItems(
                Ribbon.CreatePushButtonData("SAB_DecorateInteriorElevations", "Оформить развертки",
                    "SAB.InteriorElevations.Commands.DecorateInteriorElevationsCommand",
                    "SAB.Resources.CreateInteriorElevationsCommand_32.png", "SAB.Resources.CreateInteriorElevationsCommand_16.png", 24),
                Ribbon.CreatePushButtonData("SAB_ConfigureElevationDecorationCatalog", "Настройки оформления",
                    "SAB.InteriorElevations.Commands.ConfigureElevationDecorationCatalogCommand",
                    "SAB.Resources.CreateInteriorElevationsCommand_32.png", "SAB.Resources.CreateInteriorElevationsCommand_16.png", 24));


            // Блок создания ортогональных видов для экспликаций дверей и окон.
            Ribbon.AddPushButtonSingle(
                doorWindowExplanationsPanel,
                "SAB_CreateDoorWindowViews",
                "Экспликации\nдверей и окон",
                "SAB.DoorWindowExplanations.Commands.CreateDoorWindowViewsCommand",
                "SAB.Resources.CreateDoorWindowViews_32.png",
                "SAB.Resources.CreateDoorWindowViews_16.png");

            // Каркас.
            var frameButton = (SplitButton)framePanel.AddItem(
                new SplitButtonData("SAB_GklFrame", FramePanelName));
            frameButton.IsSynchronizedWithCurrentItem = true;
            Ribbon.AddPushButtonToSplit(
                frameButton,
                "SAB_GenerateGklFrame",
                FrameModuleConstants.GenerateCommandText,
                "SAB.GklFrame.Commands.GenerateGklFrameCommand",
                "SAB.Resources.GklFrameWall_32.png",
                "SAB.Resources.GklFrameWall_16.png");

            Ribbon.AddPushButtonToSplit(
                frameButton,
                "SAB_CalculateGklFrame",
                FrameModuleConstants.CalculateCommandText,
                "SAB.GklFrame.Commands.CalculateGklFrameCommand",
                "SAB.Resources.SyncReminderSettings_32.png",
                "SAB.Resources.SyncReminderSettings_16.png",
                useGrayIcons: true);

            try
            {
                ParameterTools.ParameterToolsModule.Start(application, parametersPanel);
            }
            catch (Exception exception)
            {
                ParameterTools.ParameterToolsModule.Stop(application);
                TaskDialog.Show("SAB — Параметры", "Не удалось запустить модуль параметров:\n" + exception.Message);
            }

            StartSyncReminder(application);
            try
            {
                _notificationController = new Notifications.NotificationController();
                _notificationController.Start(application, settingsPanel);
            }
            catch (Exception exception)
            {
                _notificationController?.Stop(application);
                _notificationController = null;
                TaskDialog.Show("SAB — Уведомления", "Не удалось запустить обработчик уведомлений:\n" + exception.Message);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            ParameterTools.ParameterToolsModule.Stop(application);
            _notificationController?.Stop(application);
            _notificationController = null;
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
