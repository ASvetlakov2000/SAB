using System.Text;
using Helpers.Notifications.ToastNotifications;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services.Reports
{
    public class DoorWindowCreationReportService
    {
        public void ShowFinalReport(
            int selectedElementsCount,
            DoorWindowBatchCreationResult creationResult)
        {
            int createdViewsCount = 0;
            if (creationResult != null)
            {
                for (int index = 0; index < creationResult.ViewGroups.Count; index++)
                {
                    DoorWindowViewCreationResult group = creationResult.ViewGroups[index];
                    createdViewsCount += group != null ? group.GetViews().Count : 0;
                }
            }

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Отчет SAB по созданию экспликаций дверей, окон и витражей");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Выбрано элементов: " + selectedElementsCount);
            reportBuilder.AppendLine("Создано видов: " + createdViewsCount);
            reportBuilder.AppendLine("Создано размеров: " + (creationResult != null ? creationResult.DimensionsCreated : 0));
            reportBuilder.AppendLine("Записано изображений в параметры: " + (creationResult != null ? creationResult.ImagesAssigned : 0));

            if (creationResult != null && creationResult.Sheet != null)
            {
                reportBuilder.AppendLine(
                    "Создан лист: " + creationResult.Sheet.SheetNumber + " | " + creationResult.Sheet.Name);
                reportBuilder.AppendLine("Размещено видовых экранов: " + createdViewsCount);
            }
            else
            {
                reportBuilder.AppendLine("Лист: не создан");
                reportBuilder.AppendLine("Размещено видовых экранов: 0");
            }

            if (creationResult != null && creationResult.ExportedImagePaths.Count > 0)
            {
                reportBuilder.AppendLine("Экспортировано PNG: " + creationResult.ExportedImagePaths.Count);
                reportBuilder.AppendLine("Папка PNG: " + creationResult.ImageOutputFolder);
            }

            if (creationResult != null && creationResult.Warnings.Count > 0)
            {
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("Предупреждения: " + creationResult.Warnings.Count);
                int warningLimit = System.Math.Min(5, creationResult.Warnings.Count);
                for (int index = 0; index < warningLimit; index++)
                {
                    reportBuilder.AppendLine("• " + creationResult.Warnings[index]);
                }

                if (creationResult.Warnings.Count > warningLimit)
                {
                    reportBuilder.AppendLine("• Ещё: " + (creationResult.Warnings.Count - warningLimit));
                }
            }
            if (createdViewsCount > 0)
            {
                ToastNotifier.ShowSuccess(
                    "SAB Экспликации дверей, окон и витражей",
                    reportBuilder.ToString(),
                    15);
            }
            else
            {
                ToastNotifier.ShowWarning(
                    "SAB Экспликации дверей, окон и витражей",
                    reportBuilder.ToString(),
                    15);
            }
        }
    }
}
