using System;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Reporting
{
    /// <summary>
    /// Преобразование DashboardData в явную HTML view-model.
    /// </summary>
    public class HtmlDashboardViewModelBuilder
    {
        public HtmlDashboardViewModel Build(DashboardData dashboardData)
        {
            if (dashboardData == null)
            {
                throw new ArgumentNullException(nameof(dashboardData));
            }

            HtmlDashboardViewModel htmlModel = new HtmlDashboardViewModel();
            htmlModel.CatalogName = string.IsNullOrWhiteSpace(dashboardData.CatalogName) ? "RevitLibraryBuilder" : dashboardData.CatalogName;
            htmlModel.SourceName = string.IsNullOrWhiteSpace(dashboardData.SourceName) ? "Не указан" : dashboardData.SourceName;
            htmlModel.SourceFormat = string.IsNullOrWhiteSpace(dashboardData.SourceFormat) ? "Не определен" : dashboardData.SourceFormat;
            htmlModel.SourceProfile = dashboardData.SourceProfile ?? string.Empty;
            htmlModel.GeneratedAt = dashboardData.GeneratedAt;
            htmlModel.Summary = dashboardData.Summary ?? new SummaryData();

            if (dashboardData.Columns != null)
            {
                htmlModel.Columns.AddRange(dashboardData.Columns);
            }

            if (dashboardData.Rows != null)
            {
                for (int i = 0; i < dashboardData.Rows.Count; i++)
                {
                    if (dashboardData.Rows[i] == null)
                    {
                        continue;
                    }

                    htmlModel.Rows.Add(new System.Collections.Generic.List<string>(dashboardData.Rows[i]));
                }
            }

            return htmlModel;
        }
    }
}
