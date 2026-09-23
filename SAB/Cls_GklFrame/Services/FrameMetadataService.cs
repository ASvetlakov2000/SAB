using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IFrameMetadataService
    {
        void MarkCalculationWall(Wall wall, SourceWallData sourceWall);
        void MarkMullion(Mullion mullion, SourceWallData sourceWall, WallOpeningData opening, FrameMemberPurpose purpose);
        bool IsCalculationFrame(Element element);
        string GetSourceWallUniqueId(Element element);
        string GetSourceOpeningUniqueId(Element element);
        bool TryGetPurpose(Element element, out FrameMemberPurpose purpose);
        Wall FindCalculationWall(Document document, string sourceWallUniqueId);
    }

    public sealed class FrameMetadataService : IFrameMetadataService
    {
        private const string IsCalculationFrameField = "IsCalculationFrame";
        private const string SourceWallElementIdField = "SourceWallElementId";
        private const string SourceWallUniqueIdField = "SourceWallUniqueId";
        private const string SourceOpeningUniqueIdField = "SourceOpeningUniqueId";
        private const string PurposeField = "Purpose";

        public void MarkCalculationWall(Wall wall, SourceWallData sourceWall)
        {
            if (wall == null || sourceWall == null)
            {
                return;
            }

            WriteEntity(
                wall,
                sourceWall.SourceElementId.IntegerValue,
                sourceWall.SourceUniqueId,
                string.Empty,
                FrameMemberPurpose.Custom);
            TryWriteStringParameter(wall, "SAB_SourceWallUniqueId", sourceWall.SourceUniqueId);
            TryWriteIntegerParameter(wall, "SAB_IsCalculationFrame", 1);
        }

        public void MarkMullion(
            Mullion mullion,
            SourceWallData sourceWall,
            WallOpeningData opening,
            FrameMemberPurpose purpose)
        {
            if (mullion == null || sourceWall == null)
            {
                return;
            }

            string openingUniqueId = opening != null ? opening.SourceUniqueId : string.Empty;
            WriteEntity(
                mullion,
                sourceWall.SourceElementId.IntegerValue,
                sourceWall.SourceUniqueId,
                openingUniqueId,
                purpose);
            TryWriteStringParameter(mullion, "SAB_ProfilePurpose", purpose.ToString());
            TryWriteStringParameter(mullion, "SAB_SourceWallUniqueId", sourceWall.SourceUniqueId);
            TryWriteStringParameter(mullion, "SAB_SourceOpeningUniqueId", openingUniqueId);
            TryWriteIntegerParameter(mullion, "SAB_IsCalculationFrame", 1);
        }

        public bool IsCalculationFrame(Element element)
        {
            Entity entity;
            Schema schema;
            return TryGetEntity(element, out entity, out schema) &&
                   entity.Get<bool>(schema.GetField(IsCalculationFrameField));
        }

        public string GetSourceWallUniqueId(Element element)
        {
            return GetString(element, SourceWallUniqueIdField);
        }

        public string GetSourceOpeningUniqueId(Element element)
        {
            return GetString(element, SourceOpeningUniqueIdField);
        }

        public bool TryGetPurpose(Element element, out FrameMemberPurpose purpose)
        {
            purpose = FrameMemberPurpose.Custom;
            string value = GetString(element, PurposeField);
            return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out purpose);
        }

        public Wall FindCalculationWall(Document document, string sourceWallUniqueId)
        {
            if (document == null || string.IsNullOrWhiteSpace(sourceWallUniqueId))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                Wall wall = element as Wall;
                if (wall != null &&
                    IsCalculationFrame(wall) &&
                    string.Equals(GetSourceWallUniqueId(wall), sourceWallUniqueId, StringComparison.Ordinal))
                {
                    return wall;
                }
            }

            return null;
        }

        private static void WriteEntity(
            Element element,
            int sourceWallElementId,
            string sourceWallUniqueId,
            string sourceOpeningUniqueId,
            FrameMemberPurpose purpose)
        {
            Schema schema = GetOrCreateSchema();
            Entity entity = new Entity(schema);
            entity.Set(schema.GetField(IsCalculationFrameField), true);
            entity.Set(schema.GetField(SourceWallElementIdField), sourceWallElementId);
            entity.Set(schema.GetField(SourceWallUniqueIdField), sourceWallUniqueId ?? string.Empty);
            entity.Set(schema.GetField(SourceOpeningUniqueIdField), sourceOpeningUniqueId ?? string.Empty);
            entity.Set(schema.GetField(PurposeField), purpose.ToString());
            element.SetEntity(entity);
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(FrameModuleConstants.MetadataSchemaGuid);
            if (existing != null)
            {
                return existing;
            }

            SchemaBuilder builder = new SchemaBuilder(FrameModuleConstants.MetadataSchemaGuid);
            builder.SetSchemaName("SAB_GklFrame_Metadata");
            builder.SetDocumentation("Связь расчётного каркаса SAB с исходной стеной и назначением профиля.");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(IsCalculationFrameField, typeof(bool));
            builder.AddSimpleField(SourceWallElementIdField, typeof(int));
            builder.AddSimpleField(SourceWallUniqueIdField, typeof(string));
            builder.AddSimpleField(SourceOpeningUniqueIdField, typeof(string));
            builder.AddSimpleField(PurposeField, typeof(string));
            return builder.Finish();
        }

        private static bool TryGetEntity(Element element, out Entity entity, out Schema schema)
        {
            entity = default(Entity);
            schema = Schema.Lookup(FrameModuleConstants.MetadataSchemaGuid);
            if (element == null || schema == null)
            {
                return false;
            }

            entity = element.GetEntity(schema);
            return entity.IsValid();
        }

        private static string GetString(Element element, string fieldName)
        {
            Entity entity;
            Schema schema;
            if (!TryGetEntity(element, out entity, out schema))
            {
                return string.Empty;
            }

            return entity.Get<string>(schema.GetField(fieldName)) ?? string.Empty;
        }

        private static void TryWriteStringParameter(Element element, string parameterName, string value)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
            {
                parameter.Set(value ?? string.Empty);
            }
        }

        private static void TryWriteIntegerParameter(Element element, string parameterName, int value)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Integer)
            {
                parameter.Set(value);
            }
        }
    }
}
