using System;
using System.Collections.Generic;
using System.IO;
using SAB.BimDashboard.Models;
using SAB.BimDashboard.Services.Processing;
using SAB.BimDashboard.Services.Tables;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Провайдер CSV-файлов через общий табличный pipeline.
    /// </summary>
    public class CsvDataProvider : IDataProvider
    {
        private readonly ITableReader _tableReader;
        private readonly TabularRecordMapper _tabularRecordMapper;

        public CsvDataProvider()
            : this(new CsvTableReader(), new TabularRecordMapper())
        {
        }

        public CsvDataProvider(ITableReader tableReader, TabularRecordMapper tabularRecordMapper)
        {
            _tableReader = tableReader ?? throw new ArgumentNullException(nameof(tableReader));
            _tabularRecordMapper = tabularRecordMapper ?? throw new ArgumentNullException(nameof(tabularRecordMapper));
        }

        public bool CanHandle(DataSourceType sourceType)
        {
            return sourceType == DataSourceType.Csv;
        }

        public ProviderResult Load(DataProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(context.FilePath))
            {
                throw new ArgumentException("Путь к CSV файлу не задан.");
            }

            if (!_tableReader.CanRead(context.FilePath))
            {
                throw new InvalidOperationException("Выбран неподдерживаемый CSV файл.");
            }

            TabularDataSet dataSet = _tableReader.Read(context.FilePath);
            ValidateProfileHeaders(dataSet, context.SourceProfile);

            List<string> warnings = new List<string>();
            List<UnifiedRecord> records = _tabularRecordMapper.Map(dataSet, warnings);

            if (records.Count == 0)
            {
                throw new InvalidOperationException("CSV прочитан, но данные для dashboard не найдены.");
            }

            string sourceFileName = Path.GetFileName(context.FilePath);
            ResolveThumbnailPaths(records, context.FilePath);
            ThumbnailRuntimePathEnricher.Enrich(records, context.SourceProfile);

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (!record.Fields.ContainsKey("SourceType"))
                {
                    record.Fields["SourceType"] = "CSV";
                }

                if (!record.Fields.ContainsKey("SourceFile"))
                {
                    record.Fields["SourceFile"] = sourceFileName;
                }

                if (!record.Fields.ContainsKey("RecordType"))
                {
                    record.Fields["RecordType"] = "TabularRecord";
                }
            }

            ProviderResult result = new ProviderResult();
            result.ProjectName = Path.GetFileNameWithoutExtension(context.FilePath);
            result.SourceProfile = context.SourceProfile.ToString();
            result.Records = records;
            result.Warnings.AddRange(warnings);
            return result;
        }

        private static void ValidateProfileHeaders(TabularDataSet dataSet, DashboardProfileType profile)
        {
            if (dataSet == null || dataSet.Headers == null)
            {
                throw new InvalidOperationException("CSV не содержит заголовков.");
            }

            List<HeaderGroup> requiredHeaders = GetRequiredHeaders(profile);
            List<string> missingHeaders = new List<string>();

            for (int i = 0; i < requiredHeaders.Count; i++)
            {
                HeaderGroup requiredHeader = requiredHeaders[i];

                if (!HasAnyHeader(dataSet.Headers, requiredHeader))
                {
                    missingHeaders.Add(requiredHeader.DisplayName);
                }
            }

            if (missingHeaders.Count > 0)
            {
                throw new InvalidOperationException(
                    "Выбран не тот источник CSV. Отсутствуют колонки: " + string.Join(", ", missingHeaders));
            }
        }

        private static List<HeaderGroup> GetRequiredHeaders(DashboardProfileType profile)
        {
            if (profile == DashboardProfileType.SystemFamilies)
            {
                return new List<HeaderGroup>
                {
                    new HeaderGroup("Категория", "Категория", "Category"),
                    new HeaderGroup("Семейство", "Семейство", "Family"),
                    new HeaderGroup("Типоразмер", "Типоразмер", "TypeName", "Тип"),
                    new HeaderGroup("Структура", "Структура", "Structure"),
                    new HeaderGroup("Толщина типа, мм", "Толщина типа, мм", "TotalThicknessMm")
                };
            }

            if (profile == DashboardProfileType.LoadableFamilies)
            {
                return new List<HeaderGroup>
                {
                    new HeaderGroup("Категория", "Категория", "Category"),
                    new HeaderGroup("Семейство", "Семейство", "Family"),
                    new HeaderGroup("Типоразмер", "Типоразмер", "TypeName", "Тип")
                };
            }

            if (profile == DashboardProfileType.Lines)
            {
                return new List<HeaderGroup>
                {
                    new HeaderGroup("Наименование", "Наименование", "Name"),
                    new HeaderGroup("Категория", "Категория", "Category"),
                    new HeaderGroup("Вес линии", "Вес линии", "LineWeight"),
                    new HeaderGroup("Цвет", "Цвет", "Color"),
                    new HeaderGroup("Образец", "Образец", "Pattern")
                };
            }

            return new List<HeaderGroup>
            {
                new HeaderGroup("Наименование", "Наименование", "Name"),
                new HeaderGroup("Штриховка переднего плана", "Штриховка переднего плана", "ForegroundPattern"),
                new HeaderGroup("Штриховка заднего плана", "Штриховка заднего плана", "BackgroundPattern"),
                new HeaderGroup("Маскирование", "Маскирование", "IsMasking"),
                new HeaderGroup("Тип штриховки", "Тип штриховки", "Target")
            };
        }

        private static bool HasAnyHeader(List<string> headers, HeaderGroup headerGroup)
        {
            if (headers == null || headerGroup == null || headerGroup.Aliases == null)
            {
                return false;
            }

            for (int aliasIndex = 0; aliasIndex < headerGroup.Aliases.Count; aliasIndex++)
            {
                string alias = NormalizeHeader(headerGroup.Aliases[aliasIndex]);

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                for (int i = 0; i < headers.Count; i++)
                {
                    string header = NormalizeHeader(headers[i]);

                    if (string.Equals(header, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim();
            normalized = normalized.Trim('\uFEFF');
            return normalized;
        }

        // Блок пост-обработки путей миниатюр для корректного рендера в HTML.
        private static void ResolveThumbnailPaths(List<UnifiedRecord> records, string sourceFilePath)
        {
            if (records == null || records.Count == 0 || string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return;
            }

            string sourceDirectory = Path.GetDirectoryName(sourceFilePath);

            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null || record.Fields == null)
                {
                    continue;
                }

                ResolveThumbnailField(record.Fields, "Миниатюра", sourceDirectory);
                ResolveThumbnailField(record.Fields, "ThumbnailPath", sourceDirectory);
                ResolveThumbnailField(record.Fields, "Thumbnail", sourceDirectory);
                ResolveThumbnailField(record.Fields, "IconPath", sourceDirectory);
            }
        }

        private static void ResolveThumbnailField(Dictionary<string, string> fields, string fieldName, string sourceDirectory)
        {
            if (fields == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return;
            }

            string keyToUse = fieldName;
            string rawValue = null;

            if (!fields.TryGetValue(keyToUse, out rawValue))
            {
                foreach (KeyValuePair<string, string> pair in fields)
                {
                    if (!string.Equals(pair.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    keyToUse = pair.Key;
                    rawValue = pair.Value;
                    break;
                }
            }

            if (rawValue == null && !fields.ContainsKey(keyToUse))
            {
                return;
            }

            fields[keyToUse] = ResolveToDisplayPath(rawValue, sourceDirectory);
        }

        private static string ResolveToDisplayPath(string rawPath, string sourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            string path = rawPath.Trim();

            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            Uri absoluteUri;

            if (Uri.TryCreate(path, UriKind.Absolute, out absoluteUri))
            {
                return absoluteUri.AbsoluteUri;
            }

            string fullPath = path;

            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.Combine(sourceDirectory, fullPath.Replace('/', Path.DirectorySeparatorChar));
            }

            try
            {
                fullPath = Path.GetFullPath(fullPath);
                return new Uri(fullPath).AbsoluteUri;
            }
            catch
            {
                return rawPath;
            }
        }

        private class HeaderGroup
        {
            public HeaderGroup(string displayName, params string[] aliases)
            {
                DisplayName = displayName ?? string.Empty;
                Aliases = new List<string>();

                if (aliases == null)
                {
                    return;
                }

                for (int i = 0; i < aliases.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(aliases[i]))
                    {
                        Aliases.Add(aliases[i]);
                    }
                }
            }

            public string DisplayName { get; private set; }

            public List<string> Aliases { get; private set; }
        }
    }
}
