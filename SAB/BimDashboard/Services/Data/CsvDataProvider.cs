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

            // Блок отвечает за сбор исходных данных для HTML из CSV.
            TabularDataSet dataSet = _tableReader.Read(context.FilePath);

            List<string> warnings = new List<string>();
            List<UnifiedRecord> records = _tabularRecordMapper.Map(dataSet, warnings);

            if (records.Count == 0)
            {
                throw new InvalidOperationException("CSV прочитан, но данные для dashboard не найдены.");
            }

            string sourceFileName = Path.GetFileName(context.FilePath);

            // Блок обогащения записей метаданными источника.
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
            result.Records = records;
            result.Warnings.AddRange(warnings);
            return result;
        }
    }
}
