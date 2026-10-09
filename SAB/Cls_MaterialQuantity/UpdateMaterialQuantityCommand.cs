using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.MaterialQuantity
{
    [Transaction(TransactionMode.Manual)]
    public sealed class UpdateMaterialQuantityCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document.IsFamilyDocument)
            {
                message = "Откройте проект Revit, а не семейство.";
                return Result.Failed;
            }

            try
            {
                Document project = uiDocument.Document;
                SyncReport report = MaterialQuantityUpdateWorkflow.RunWithProgressWindow(
                    commandData.Application, project);

                TaskDialog.Show(
                    "SAB — материалы и ведомости",
                    MaterialQuantityUpdateWorkflow.BuildSummary(report, 0));
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write(commandData.Application.Application.VersionNumber,
                    "Update failed: " + exception);
                message = exception.Message;
                return Result.Failed;
            }
        }
    }

    internal static class MaterialQuantityUpdateWorkflow
    {
        internal static SyncReport RunWithProgressWindow(UIApplication application, Document project)
        {
            var window = new MaterialQuantityProgressWindow();
            new WindowInteropHelper(window).Owner = application.MainWindowHandle;
            try
            {
                window.Show();
                return Run(application, project, window.Report);
            }
            finally
            {
                try { window.AllowCloseAndClose(); }
                catch (Exception exception)
                {
                    DiagnosticLog.Write(project.Application.VersionNumber,
                        "Progress window close failed: " + exception);
                }
                MaterialQuantityUiRefresh.Queue(application);
            }
        }

        internal static SyncReport Run(
            UIApplication application,
            Document project,
            Action<MaterialQuantityProgressInfo> progress)
        {
            Report(
                progress,
                5,
                "Подготовка служебного семейства",
                "Проверяем семейство агрегированных строк и общие параметры.",
                true,
                null);
            DiagnosticLog.Write(project.Application.VersionNumber,
                "Update started; DLL=" + typeof(MaterialQuantityUpdateWorkflow).Assembly.Location);
            string preparedFamily = RecordFamily.Prepare(application.Application, project);
            DiagnosticLog.Write(project.Application.VersionNumber, "Family prepared");

            SyncReport report;
            using (var group = new TransactionGroup(project, "SAB: обновить материалы по слоям"))
            {
                group.Start();
                using (var transaction = new Transaction(project, "Расчётные записи и спецификации"))
                {
                    transaction.Start();
                    Report(
                        progress,
                        15,
                        "Загрузка служебного семейства",
                        "Подключаем семейство расчётной записи к проекту.",
                        false,
                        null);
                    FamilySymbol symbol = RecordFamily.EnsureLoaded(project, preparedFamily);
                    DiagnosticLog.Write(project.Application.VersionNumber, "Family loaded");
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        project.Regenerate();
                    }

                    Report(
                        progress,
                        20,
                        "Подготовка расчётных записей",
                        "Собираем конструкции и прежние расчётные записи.",
                        false,
                        null);
                    report = RecordSynchronizer.Run(project, symbol, progress);
                    DiagnosticLog.Write(project.Application.VersionNumber, "Records synchronized");

                    Report(
                        progress,
                        85,
                        "Обновление спецификаций",
                        "Подготовлено спецификаций: 0 из 2.",
                        false,
                        report);
                    RecordSchedules.Ensure(
                        project,
                        delegate(int current, int total, string scheduleName)
                        {
                            int percentage = 85 + (int)Math.Round(10d * current / total);
                            Report(
                                progress,
                                percentage,
                                "Обновление спецификаций",
                                "Спецификация " + current + " из " + total +
                                ": " + scheduleName + ".",
                                false,
                                report,
                                current,
                                total);
                        });
                    DiagnosticLog.Write(project.Application.VersionNumber, "Schedules ensured");
                    Report(
                        progress,
                        97,
                        "Сохранение изменений",
                        "Фиксируем агрегаты, подробный аудит и спецификации.",
                        true,
                        report);
                    transaction.Commit();
                }
                group.Assimilate();
            }

            Report(
                progress,
                100,
                "Готово",
                "Материалы и спецификации обновлены.",
                false,
                report,
                1,
                1);
            return report;
        }

        internal static string BuildSummary(SyncReport report, int savedRules)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            return "Сохранено правил материалов: " + savedRules + "\n" +
                "Проверено конструкций: " + report.HostCount + "\n" +
                "Рассчитано послойных строк: " + report.DetailedRecords + "\n" +
                "Агрегированных элементов в модели: " + report.AggregateRecords + "\n" +
                "  создано: " + report.Created + "\n" +
                "  обновлено: " + report.Updated + "\n" +
                "Требуют проверки: " + report.NeedsReview + "\n" +
                "Не учитываются по правилу: " + report.Ignored + "\n" +
                "Удалено старых элементов: " + report.Obsolete + "\n\n" +
                "Подробный расчёт сохранён компактно в данных проекта, без отдельных " +
                "элементов на каждый слой. Количество в неоднозначных случаях не подставляется.";
        }

        private static void Report(
            Action<MaterialQuantityProgressInfo> progress,
            int percentage,
            string stage,
            string details,
            bool isIndeterminate,
            SyncReport report,
            int currentItem = 0,
            int totalItems = 0)
        {
            if (progress == null)
            {
                return;
            }

            progress(new MaterialQuantityProgressInfo
            {
                OverallPercentage = Math.Max(0, Math.Min(percentage, 100)),
                IsIndeterminate = isIndeterminate,
                CurrentItem = currentItem,
                TotalItems = totalItems,
                Stage = stage,
                Details = details,
                Created = report == null ? 0 : report.Created,
                Updated = report == null ? 0 : report.Updated,
                NeedsReview = report == null ? 0 : report.NeedsReview,
                Obsolete = report == null ? 0 : report.Obsolete
            });
        }
    }
}
