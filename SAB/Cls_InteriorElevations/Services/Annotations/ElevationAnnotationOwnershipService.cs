using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Annotations
{
    public class ElevationAnnotationOwnershipService
    {
        private static readonly Guid SchemaGuid = new Guid("7F8B5D5C-7D03-4D7F-9814-2A1C3FE54B6D");
        private const string SchemaName = "SABInteriorElevationDecoration";
        private const string ViewIdFieldName = "ViewId";
        private const string RoleFieldName = "Role";

        public void MarkOwned(Element element, ElementId viewId, string role)
        {
            if (element == null || viewId == null)
            {
                return;
            }

            Schema schema = GetOrCreateSchema();
            Entity entity = new Entity(schema);
            entity.Set<ElementId>(schema.GetField(ViewIdFieldName), viewId);
            entity.Set<string>(schema.GetField(RoleFieldName), role ?? string.Empty);
            element.SetEntity(entity);
        }

        public int DeleteOwnedAnnotations(
            Document document,
            ElementId viewId,
            ElevationDecorationSettings settings)
        {
            if (document == null || viewId == null || settings == null)
            {
                return 0;
            }

            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null)
            {
                return 0;
            }

            List<ElementId> idsToDelete = new List<ElementId>();
            IList<Element> viewElements = new FilteredElementCollector(document, viewId)
                .WhereElementIsNotElementType()
                .ToElements();

            for (int index = 0; index < viewElements.Count; index++)
            {
                Element element = viewElements[index];
                Entity entity = element.GetEntity(schema);
                if (!entity.IsValid())
                {
                    continue;
                }

                ElementId ownerViewId = entity.Get<ElementId>(schema.GetField(ViewIdFieldName));
                if (ownerViewId == null || !RevitElementIdUtils.AreEqual(ownerViewId, viewId))
                {
                    continue;
                }

                string role = entity.Get<string>(schema.GetField(RoleFieldName)) ?? string.Empty;
                bool delete = ShouldDeleteRole(role, settings);
                if (delete)
                {
                    idsToDelete.Add(element.Id);
                }
            }

            if (idsToDelete.Count == 0)
            {
                return 0;
            }

            ICollection<ElementId> deletedIds = document.Delete(idsToDelete);
            return deletedIds != null ? deletedIds.Count : 0;
        }

        private bool ShouldDeleteRole(
            string storedRole,
            ElevationDecorationSettings settings)
        {
            string role = storedRole ?? string.Empty;
            if (role.StartsWith("Diagnostic.", StringComparison.Ordinal))
            {
                role = role.Substring("Diagnostic.".Length);
            }

            if (role.StartsWith("Spot", StringComparison.Ordinal))
            {
                return settings.PlaceSpotElevations;
            }

            if (role.StartsWith("Tag.Wall", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlaceWallTags;
            if (role.StartsWith("Tag.Floor", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlaceFloorTags;
            if (role.StartsWith("Tag.Ceiling", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlaceCeilingTags;
            if (role.StartsWith("Tag.Plinth", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlacePlinthTags;
            if (role.StartsWith("Tag.Door", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlaceDoorTags;
            if (role.StartsWith("Tag.Window", StringComparison.Ordinal))
                return settings.PlaceTags && settings.PlaceWindowTags;

            if (role.StartsWith("Dimension.Width", StringComparison.Ordinal))
            {
                return settings.PlaceDimensions && settings.PlaceHorizontalDimensions;
            }

            if (role.StartsWith("Dimension", StringComparison.Ordinal))
            {
                return settings.PlaceDimensions && settings.PlaceVerticalDimensions;
            }

            return false;
        }

        private Schema GetOrCreateSchema()
        {
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null)
            {
                return schema;
            }

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(ViewIdFieldName, typeof(ElementId));
            builder.AddSimpleField(RoleFieldName, typeof(string));
            return builder.Finish();
        }
    }
}
