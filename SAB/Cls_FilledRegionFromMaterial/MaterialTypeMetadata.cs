using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace SAB.FilledRegionFromMaterial
{
    internal static class MaterialTypeMetadata
    {
        private static readonly Guid SchemaGuid = new Guid("9D0C36A8-A8BA-4C69-B7AB-46C40940ADDA");
        private const string SchemaName = "SAB_FilledRegionFromMaterial";
        private const string MaterialUniqueIdFieldName = "MaterialUniqueId";

        public static string Read(FilledRegionType type)
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null)
            {
                return null;
            }

            Entity entity = type.GetEntity(schema);
            if (!entity.IsValid())
            {
                return null;
            }

            Field field = schema.GetField(MaterialUniqueIdFieldName);
            return field == null ? null : entity.Get<string>(field);
        }

        public static void Write(FilledRegionType type, string materialUniqueId)
        {
            Schema schema = GetOrCreateSchema();
            var entity = new Entity(schema);
            entity.Set(schema.GetField(MaterialUniqueIdFieldName), materialUniqueId);
            type.SetEntity(entity);
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
            {
                return existing;
            }

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(MaterialUniqueIdFieldName, typeof(string));
            return builder.Finish();
        }
    }
}
