using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace SAB.FilledRegionFromMaterial
{
    internal static class PluginSettingsStorage
    {
        private static readonly Guid SchemaGuid = new Guid("566CA159-BEEC-4C9A-BE74-49384F0864D8");
        private const string JsonFieldName = "SettingsJson";

        public static PluginSettings Load(Document document)
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null)
            {
                return CreateLegacyDefaults();
            }

            DataStorage storage = FindStorage(document, schema);
            if (storage == null)
            {
                return CreateLegacyDefaults();
            }

            Entity entity = storage.GetEntity(schema);
            string json = entity.Get<string>(schema.GetField(JsonFieldName));
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateLegacyDefaults();
            }

            var serializer = new XmlSerializer(typeof(PluginSettings));
            PluginSettings settings;
            using (var reader = new StringReader(json))
            {
                settings = serializer.Deserialize(reader) as PluginSettings ?? new PluginSettings();
            }
            Normalize(settings);
            return settings;
        }

        public static void Save(Document document, PluginSettings settings)
        {
            Normalize(settings);
            Schema schema = GetOrCreateSchema();
            DataStorage storage = FindStorage(document, schema) ?? DataStorage.Create(document);
            storage.Name = "SAB — Область заливки из материала — настройки";

            var serializer = new XmlSerializer(typeof(PluginSettings));
            string serialized;
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, settings);
                serialized = writer.ToString();
            }

            var entity = new Entity(schema);
            entity.Set(schema.GetField(JsonFieldName), serialized);
            storage.SetEntity(entity);
        }

        private static PluginSettings CreateLegacyDefaults()
        {
            var settings = new PluginSettings();
            settings.ParameterMappings.Add(new ParameterMappingSetting
            {
                SourceKey = ParameterIdentity.FromBuiltIn(BuiltInParameter.ALL_MODEL_MODEL),
                TargetSelectionKey = ParameterTargetScope.Type + "|" +
                                     ParameterIdentity.FromBuiltIn(BuiltInParameter.ALL_MODEL_DESCRIPTION)
            });
            return settings;
        }

        private static void Normalize(PluginSettings settings)
        {
            if (settings.TypeNamePrefix == null)
            {
                settings.TypeNamePrefix = "SAB_";
            }

            if (settings.ParameterMappings == null)
            {
                settings.ParameterMappings = new System.Collections.Generic.List<ParameterMappingSetting>();
            }
        }

        private static DataStorage FindStorage(Document document, Schema schema)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(DataStorage))
                .Cast<DataStorage>()
                .FirstOrDefault(storage => storage.GetEntity(schema).IsValid());
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
            {
                return existing;
            }

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("SAB_FilledRegionFromMaterial_Settings");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(JsonFieldName, typeof(string));
            return builder.Finish();
        }
    }
}
