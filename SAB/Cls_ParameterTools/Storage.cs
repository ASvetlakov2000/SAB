using System;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SAB.ParameterTools.Core;
using Profile = SAB.ParameterTools.Core.Profile;

namespace SAB.ParameterTools
{
    internal static class Storage
    {
        private static readonly Guid ProfileGuid = new Guid("e02b1b80-6af3-43eb-81c8-9224116045db");
        private static readonly Guid SourceGuid = new Guid("6d68806d-c63c-46a6-b92d-0a22d3a52250");
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };

        private static Schema GetSchema(Guid guid, string name)
        {
            var schema = Schema.Lookup(guid);
            if (schema != null) return schema;
            var builder = new SchemaBuilder(guid);
            builder.SetSchemaName(name);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("JsonV1", typeof(string));
            return builder.Finish();
        }
        internal static Profile Load(Document doc)
        {
            var schema = Schema.Lookup(ProfileGuid);
            if (schema == null) return Catalog.DefaultProfile(doc);
            var rows = new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Cast<DataStorage>()
                .Where(s => s.GetEntity(schema).IsValid()).ToList();
            if (rows.Count > 1) throw new InvalidOperationException("В проекте найдено несколько профилей SAB. Устраните конфликт профилей.");
            if (rows.Count == 0) return Catalog.DefaultProfile(doc);
            var profile = Json.Deserialize<Profile>(rows[0].GetEntity(schema).Get<string>("JsonV1"));
            if (profile == null || profile.SchemaVersion != 1) throw new InvalidOperationException("Профиль настроек повреждён или имеет неподдерживаемую версию.");
            RuleEngine.ArchiveAdvancedOptions(profile);
            return profile;
        }
        internal static void Save(Document doc, Profile profile)
        {
            var schema = GetSchema(ProfileGuid, "SABParameterToolsProfileV1");
            var rows = new FilteredElementCollector(doc).OfClass(typeof(DataStorage)).Cast<DataStorage>()
                .Where(s => s.GetEntity(schema).IsValid()).ToList();
            if (rows.Count > 1) throw new InvalidOperationException("Найден конфликт профилей настроек.");
            var data = rows.SingleOrDefault() ?? DataStorage.Create(doc);
            data.Name = "SAB — правила заполнения параметров";
            var entity = new Entity(schema);
            entity.Set("JsonV1", Json.Serialize(profile));
            data.SetEntity(entity);
        }
        internal static Provenance Source(Element element)
        {
            var schema = Schema.Lookup(SourceGuid);
            if (schema == null) return new Provenance();
            var entity = element.GetEntity(schema);
            return entity.IsValid() ? Json.Deserialize<Provenance>(entity.Get<string>("JsonV1")) ?? new Provenance() : new Provenance();
        }
        internal static void Source(Element element, Provenance source)
        {
            var schema = GetSchema(SourceGuid, "SABParameterToolsSourceV1");
            var entity = new Entity(schema);
            entity.Set("JsonV1", Json.Serialize(source));
            element.SetEntity(entity);
        }
    }
}
