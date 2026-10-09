using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SAB.InteriorElevations.Models
{
    public enum ElevationDecorationCatalogRole
    {
        FinishFloor = 0,
        Subfloor = 1,
        StructuralSlab = 2,
        FinishWall = 3,
        Ceiling = 4,
        Door = 5,
        Window = 6,
        Plinth = 7
    }

    public class ElevationDecorationCatalog
    {
        public int SchemaVersion { get; set; } = 1;

        public List<ElevationDecorationCatalogEntry> Entries { get; set; } =
            new List<ElevationDecorationCatalogEntry>();
    }

    public class ElevationDecorationCatalogEntry
    {
        public string Key { get; set; }

        public ElevationDecorationCatalogRole Role { get; set; }

        public long CategoryId { get; set; }

        public string SourceDocumentKey { get; set; }

        public string SourceDocumentTitle { get; set; }

        public string TypeUniqueId { get; set; }

        public string FamilyName { get; set; }

        public string TypeName { get; set; }

        [JsonIgnore]
        public string RoleDisplayName
        {
            get { return ElevationDecorationCatalogRoleNames.GetDisplayName(Role); }
        }

        [JsonIgnore]
        public string TypeDisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(FamilyName)
                    ? TypeName ?? string.Empty
                    : FamilyName + " : " + (TypeName ?? string.Empty);
            }
        }
    }

    public class ElevationDecorationCatalogRoleOption
    {
        public ElevationDecorationCatalogRole Role { get; set; }

        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName ?? string.Empty;
        }
    }

    public static class ElevationDecorationCatalogRoleNames
    {
        public static string GetDisplayName(ElevationDecorationCatalogRole role)
        {
            switch (role)
            {
                case ElevationDecorationCatalogRole.FinishFloor:
                    return "Чистовой пол";
                case ElevationDecorationCatalogRole.Subfloor:
                    return "Черновой пол";
                case ElevationDecorationCatalogRole.StructuralSlab:
                    return "ЖБ-основание / перекрытие";
                case ElevationDecorationCatalogRole.FinishWall:
                    return "Чистовая отделка стен";
                case ElevationDecorationCatalogRole.Ceiling:
                    return "Потолок";
                case ElevationDecorationCatalogRole.Door:
                    return "Дверь";
                case ElevationDecorationCatalogRole.Window:
                    return "Окно";
                case ElevationDecorationCatalogRole.Plinth:
                    return "Плинтус";
                default:
                    return role.ToString();
            }
        }
    }
}
