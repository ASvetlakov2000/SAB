using SAB.RoomGeometryTools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис экспорта отчетов room geometry tools в CSV.
    /// </summary>
    public class RoomReportService
    {
        public string ExportCsv(
            IList<RoomAngleIssue> angleIssues,
            IList<RoomPlacementIssue> placementIssues,
            IList<RoomAreaChangeIssue> areaIssues,
            IList<RoomAxisCreationResult> axisResults)
        {
            string filePath = SelectReportFilePath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Тип проверки,Уровень,Номер помещения,Имя помещения,ElementId,Параметр 1,Параметр 2,Параметр 3,Параметр 4,Статус,Сообщение");

            AppendAngleRows(builder, angleIssues);
            AppendPlacementRows(builder, placementIssues);
            AppendAreaRows(builder, areaIssues);
            AppendAxisRows(builder, axisResults);

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(true));
            return filePath;
        }

        private static string SelectReportFilePath()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить отчет проверки помещений";
                dialog.Filter = "CSV (*.csv)|*.csv|Все файлы (*.*)|*.*";
                dialog.FileName = "Отчет_проверки_помещений_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                dialog.AddExtension = true;
                dialog.DefaultExt = "csv";

                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
            }
        }

        private static void AppendAngleRows(StringBuilder builder, IList<RoomAngleIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                RoomAngleIssue issue = issues[i];
                string line = BuildRow(
                    "Проблемные углы",
                    issue.LevelName,
                    issue.RoomNumber,
                    issue.RoomName,
                    issue.RoomId.IntegerValue.ToString(),
                    issue.ActualAngleDegrees.ToString("0.######"),
                    issue.DeviationFrom90Degrees.ToString("0.######"),
                    string.Empty,
                    string.Empty,
                    "Проблема",
                    issue.Message);
                builder.AppendLine(line);
            }
        }

        private static void AppendPlacementRows(StringBuilder builder, IList<RoomPlacementIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                RoomPlacementIssue issue = issues[i];
                string line = BuildRow(
                    "Неразмещенные помещения",
                    issue.LevelName,
                    issue.RoomNumber,
                    issue.RoomName,
                    issue.RoomId.IntegerValue.ToString(),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Проблема",
                    issue.Message);
                builder.AppendLine(line);
            }
        }

        private static void AppendAreaRows(StringBuilder builder, IList<RoomAreaChangeIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                RoomAreaChangeIssue issue = issues[i];
                string line = BuildRow(
                    "Изменение площади",
                    issue.LevelName,
                    issue.RoomNumber,
                    issue.RoomName,
                    issue.RoomId.IntegerValue.ToString(),
                    issue.ApprovedAreaSquareMeters.ToString("0.###"),
                    issue.CurrentAreaSquareMeters.ToString("0.###"),
                    issue.DeltaAreaSquareMeters.ToString("0.###"),
                    issue.DeltaPercent.ToString("0.###"),
                    "Проблема",
                    issue.Message);
                builder.AppendLine(line);
            }
        }

        private static void AppendAxisRows(StringBuilder builder, IList<RoomAxisCreationResult> results)
        {
            if (results == null)
            {
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                RoomAxisCreationResult result = results[i];
                string line = BuildRow(
                    "Оси помещений",
                    result.LevelName,
                    result.RoomNumber,
                    result.RoomName,
                    result.RoomId != null ? result.RoomId.IntegerValue.ToString() : string.Empty,
                    result.CreatedAxisCount.ToString(),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    result.IsSuccess ? "Успех" : "Пропущено",
                    result.Message);
                builder.AppendLine(line);
            }
        }

        private static string BuildRow(
            string checkType,
            string level,
            string roomNumber,
            string roomName,
            string elementId,
            string parameter1,
            string parameter2,
            string parameter3,
            string parameter4,
            string status,
            string message)
        {
            return string.Join(",",
                Escape(checkType),
                Escape(level),
                Escape(roomNumber),
                Escape(roomName),
                Escape(elementId),
                Escape(parameter1),
                Escape(parameter2),
                Escape(parameter3),
                Escape(parameter4),
                Escape(status),
                Escape(message));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}

