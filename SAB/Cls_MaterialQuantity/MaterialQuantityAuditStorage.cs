using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;

namespace SAB.MaterialQuantity
{
    internal sealed class MaterialLayerAuditRecord
    {
        public string Key { get; set; }
        public string SourceId { get; set; }
        public string SourceCategory { get; set; }
        public string SourceType { get; set; }
        public int LayerIndex { get; set; }
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public string MaterialDescription { get; set; }
        public string MaterialModel { get; set; }
        public double ThicknessInternal { get; set; }
        public double MaterialVolumeInternal { get; set; }
        public string Unit { get; set; }
        public double Quantity { get; set; }
        public string Status { get; set; }
    }

    internal static class MaterialQuantityAuditStorage
    {
        private static readonly Guid SchemaGuid =
            new Guid("7D35E920-40B8-4DD4-8CD2-B8092C096708");

        private const string VersionField = "FormatVersion";
        private const string UpdatedUtcField = "UpdatedUtc";
        private const string HostCountField = "HostCount";
        private const string LayerCountField = "LayerCount";
        private const string PayloadField = "PayloadGzipBase64";
        private const int CurrentVersion = 1;

        internal static void Save(
            Document document,
            IList<MaterialLayerAuditRecord> records,
            int hostCount)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (records == null) throw new ArgumentNullException(nameof(records));

            Schema schema = GetOrCreateSchema();
            List<DataStorage> storages = FindStorages(document, schema);
            DataStorage storage = storages.FirstOrDefault() ?? DataStorage.Create(document);
            storage.Name = "SAB — материалы по слоям — подробный расчёт";

            foreach (DataStorage duplicate in storages.Skip(1))
                document.Delete(duplicate.Id);

            var entity = new Entity(schema);
            entity.Set(schema.GetField(VersionField), CurrentVersion);
            entity.Set(schema.GetField(UpdatedUtcField), DateTime.UtcNow.ToString("O"));
            entity.Set(schema.GetField(HostCountField), hostCount);
            entity.Set(schema.GetField(LayerCountField), records.Count);
            entity.Set(schema.GetField(PayloadField), Compress(JsonConvert.SerializeObject(records)));
            storage.SetEntity(entity);
        }

        internal static IList<MaterialLayerAuditRecord> Load(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return new List<MaterialLayerAuditRecord>();

            DataStorage storage = FindStorages(document, schema).FirstOrDefault();
            if (storage == null) return new List<MaterialLayerAuditRecord>();

            Entity entity = storage.GetEntity(schema);
            string payload = entity.Get<string>(schema.GetField(PayloadField));
            if (string.IsNullOrWhiteSpace(payload))
                return new List<MaterialLayerAuditRecord>();

            return JsonConvert.DeserializeObject<List<MaterialLayerAuditRecord>>(Decompress(payload))
                   ?? new List<MaterialLayerAuditRecord>();
        }

        private static List<DataStorage> FindStorages(Document document, Schema schema)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .Where(item => item.GetEntity(schema).IsValid())
                .ToList();
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null) return existing;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("SAB_MaterialQuantity_Audit");
            builder.SetDocumentation(
                "Подробные послойные результаты расчёта материалов без создания зеркальных элементов модели.");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(VersionField, typeof(int));
            builder.AddSimpleField(UpdatedUtcField, typeof(string));
            builder.AddSimpleField(HostCountField, typeof(int));
            builder.AddSimpleField(LayerCountField, typeof(int));
            builder.AddSimpleField(PayloadField, typeof(string));
            return builder.Finish();
        }

        private static string Compress(string value)
        {
            byte[] source = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    gzip.Write(source, 0, source.Length);

                return Convert.ToBase64String(output.ToArray());
            }
        }

        private static string Decompress(string value)
        {
            byte[] source = Convert.FromBase64String(value);
            using (var input = new MemoryStream(source))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
    }
}
