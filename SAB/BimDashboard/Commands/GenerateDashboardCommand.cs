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
    /// Команда построения HTML dashboard на основе профильного CSV.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class GenerateDashboardCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData == null || commandData.Application == null)
                {
                    message = "Входные данные команды или UIApplication недоступны.";
                    TaskDialog.Show("BIM Dashboard", message);
                    return Result.Failed;
                }

                DataSourceType selectedSourceType;
                DashboardProfileType selectedProfileType;
                string selectedFilePath;

                bool isConfirmed = DataSourceDialogService.ShowDialog(
                    out selectedSourceType,
                    out selectedProfileType,
                    out selectedFilePath);

                if (!isConfirmed)
                {
                    return Result.Cancelled;
                }

                List<IDataProvider> providers = new List<IDataProvider>
                {
                    new CsvDataProvider()
                };

                DataProviderFactory providerFactory = new DataProviderFactory(providers);
                IDataProvider provider = providerFactory.Create(selectedSourceType);

                DataProviderContext context = new DataProviderContext
                {
                    UiApplication = commandData.Application,
                    FilePath = selectedFilePath,
                    SourceProfile = selectedProfileType
                };

                ProviderResult providerResult = provider.Load(context);

                if (providerResult == null)
                {
                    TaskDialog.Show("BIM Dashboard", "Источник данных не вернул результат.");
                    return Result.Cancelled;
                }

                if (providerResult.Records == null)
                {
                    providerResult.Records = new List<UnifiedRecord>();
                }

                DataMapper dataMapper = new DataMapper();
                DashboardData dashboardData = dataMapper.Map(providerResult);

                HtmlReportBuilder htmlReportBuilder = new HtmlReportBuilder();
                string htmlPath = htmlReportBuilder.Generate(dashboardData);

                IDashboardViewer viewer = new ExternalBrowserDashboardViewer();
                viewer.Open(htmlPath);

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
                return Result.Failed;
            }
        }
    }
}
