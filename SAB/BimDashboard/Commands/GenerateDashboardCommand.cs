using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.BimDashboard.Models;
using SAB.BimDashboard.Services.Data;
using SAB.BimDashboard.Services.Processing;
using SAB.BimDashboard.Services.Reporting;
using SAB.BimDashboard.Services.Viewer;
using SAB.BimDashboard.UI;

namespace SAB.BimDashboard.Commands
{
    /// <summary>
    /// Основная команда MVP: выбор источника, построение HTML dashboard и открытие в браузере.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class GenerateDashboardCommand : IExternalCommand
    {
        // Блок debug-режима: при необходимости можно выключить подробный поток выполнения.
        private static readonly bool IsDebugMode = true;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ShowDebug("Шаг 1", "Запуск GenerateDashboardCommand.");

                if (commandData == null || commandData.Application == null)
                {
                    message = "ExternalCommandData или UIApplication недоступны.";
                    TaskDialog.Show("BIM Dashboard", message);
                    return Result.Failed;
                }

                // Блок выбора источника данных пользователем.
                DataSourceType selectedSourceType;
                string selectedFilePath;
                bool isConfirmed = DataSourceDialogService.ShowDialog(out selectedSourceType, out selectedFilePath);

                ShowDebug("Шаг 2", "Диалог выбора источника закрыт.\n" +
                                    "isConfirmed = " + isConfirmed + "\n" +
                                    "selectedSourceType = " + selectedSourceType + "\n" +
                                    "selectedFilePath = " + (selectedFilePath ?? string.Empty));

                if (!isConfirmed)
                {
                    return Result.Cancelled;
                }

                // Блок создания фабрики провайдеров и выбора нужной реализации.
                List<IDataProvider> providers = new List<IDataProvider>
                {
                    new RevitDataProvider(),
                    new CsvDataProvider(),
                    new ExcelDataProvider()
                };

                DataProviderFactory providerFactory = new DataProviderFactory(providers);
                IDataProvider provider = providerFactory.Create(selectedSourceType);

                ShowDebug("Шаг 3", "Выбран провайдер: " + provider.GetType().FullName);

                DataProviderContext context = new DataProviderContext
                {
                    UiApplication = commandData.Application,
                    FilePath = selectedFilePath
                };

                // Блок чтения источника и получения универсальных записей.
                ProviderResult providerResult = provider.Load(context);

                ShowDebug("Шаг 4", "Источник загружен.\n" +
                                    "ProjectName = " + providerResult.ProjectName + "\n" +
                                    "Records.Count = " + (providerResult.Records != null ? providerResult.Records.Count : 0) + "\n" +
                                    "Warnings.Count = " + (providerResult.Warnings != null ? providerResult.Warnings.Count : 0));

                if (providerResult == null || providerResult.Records == null || providerResult.Records.Count == 0)
                {
                    TaskDialog.Show("BIM Dashboard", "Источник данных не вернул записей для построения dashboard.");
                    return Result.Cancelled;
                }

                // Блок преобразования данных в единую dashboard-модель.
                DataMapper dataMapper = new DataMapper();
                DashboardData dashboardData = dataMapper.Map(providerResult);

                string firstRowPreview = string.Empty;
                if (dashboardData.Rows != null && dashboardData.Rows.Count > 0 && dashboardData.Rows[0] != null)
                {
                    firstRowPreview = string.Join(" | ", dashboardData.Rows[0]);
                }

                ShowDebug("Шаг 5", "Модель для HTML сформирована.\n" +
                                    "Columns.Count = " + (dashboardData.Columns != null ? dashboardData.Columns.Count : 0) + "\n" +
                                    "Rows.Count = " + (dashboardData.Rows != null ? dashboardData.Rows.Count : 0) + "\n" +
                                    "FirstRow = " + firstRowPreview);

                // Блок генерации HTML и открытия результата пользователю.
                HtmlReportBuilder htmlReportBuilder = new HtmlReportBuilder();
                string htmlPath = htmlReportBuilder.Generate(dashboardData);

                ShowDebug("Шаг 6", "HTML сгенерирован.\nhtmlPath = " + htmlPath);

                IDashboardViewer viewer = new ExternalBrowserDashboardViewer();
                viewer.Open(htmlPath);

                ShowDebug("Шаг 7", "Dashboard открыт во внешнем браузере.");

                // Блок информирования о предупреждениях чтения данных.
                if (providerResult.Warnings != null && providerResult.Warnings.Count > 0)
                {
                    string warningText = string.Join("\n", providerResult.Warnings);
                    TaskDialog.Show("BIM Dashboard", "Dashboard построен, но есть предупреждения:\n\n" + warningText);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("BIM Dashboard", "Ошибка построения dashboard:\n\n" + exception.Message);
                ShowDebug("Ошибка", exception.ToString());
                return Result.Failed;
            }
        }

        private static void ShowDebug(string step, string text)
        {
            if (!IsDebugMode)
            {
                return;
            }

            TaskDialog.Show("BIM Dashboard DEBUG", step + "\n\n" + text);
        }
    }
}
