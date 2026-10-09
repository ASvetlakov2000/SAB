using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Settings
{
    public class ElevationDecorationCatalogService
    {
        private readonly string _catalogFilePath;

        public ElevationDecorationCatalogService()
        {
            string appDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            _catalogFilePath = Path.Combine(
                appDataPath,
                "SAB",
                "InteriorElevations",
                "decoration-catalog.json");
        }

        public ElevationDecorationCatalog Load()
        {
            if (!File.Exists(_catalogFilePath))
            {
                return new ElevationDecorationCatalog();
            }

            ElevationDecorationCatalog catalog = JsonConvert.DeserializeObject<ElevationDecorationCatalog>(
                File.ReadAllText(_catalogFilePath));
            if (catalog == null || catalog.SchemaVersion != 1)
            {
                return new ElevationDecorationCatalog();
            }

            catalog.Entries = catalog.Entries ?? new List<ElevationDecorationCatalogEntry>();
            return catalog;
        }

        public void Save(ElevationDecorationCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            catalog.SchemaVersion = 1;
            catalog.Entries = catalog.Entries ?? new List<ElevationDecorationCatalogEntry>();
            Directory.CreateDirectory(Path.GetDirectoryName(_catalogFilePath));
            File.WriteAllText(
                _catalogFilePath,
                JsonConvert.SerializeObject(catalog, Formatting.Indented));
        }

        public int AddSelectedTypes(
            ElevationDecorationCatalog catalog,
            ElevationDecorationCatalogRole role,
            IEnumerable<Element> elements)
        {
            if (catalog == null || elements == null)
            {
                return 0;
            }

            int added = 0;
            foreach (Element element in elements.Where(item => item != null))
            {
                Document document = element.Document;
                ElementType type = document != null
                    ? document.GetElement(element.GetTypeId()) as ElementType
                    : null;
                if (document == null || type == null || element.Category == null)
                {
                    continue;
                }

                long categoryId = RevitElementIdUtils.GetElementIdValue(element.Category.Id);
                string documentKey = GetDocumentKey(document);
                string entryKey = BuildEntryKey(role, documentKey, type.UniqueId);
                if (catalog.Entries.Any(entry =>
                    string.Equals(entry.Key, entryKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                FamilySymbol symbol = type as FamilySymbol;
                catalog.Entries.Add(new ElevationDecorationCatalogEntry
                {
                    Key = entryKey,
                    Role = role,
                    CategoryId = categoryId,
                    SourceDocumentKey = documentKey,
                    SourceDocumentTitle = document.Title,
                    TypeUniqueId = type.UniqueId,
                    FamilyName = symbol != null ? symbol.FamilyName : type.FamilyName,
                    TypeName = type.Name
                });
                added++;
            }

            catalog.Entries = catalog.Entries
                .OrderBy(entry => entry.Role)
                .ThenBy(entry => entry.SourceDocumentTitle)
                .ThenBy(entry => entry.FamilyName)
                .ThenBy(entry => entry.TypeName)
                .ToList();
            return added;
        }

        public bool Matches(
            ElevationDecorationCatalog catalog,
            ElevationDecorationCatalogRole role,
            Element element)
        {
            if (catalog == null || catalog.Entries == null || element == null ||
                element.Document == null || element.Category == null)
            {
                return false;
            }

            ElementType type = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (type == null)
            {
                return false;
            }

            string documentKey = GetDocumentKey(element.Document);
            long categoryId = RevitElementIdUtils.GetElementIdValue(element.Category.Id);
            return catalog.Entries.Any(entry =>
                entry.Role == role &&
                entry.CategoryId == categoryId &&
                string.Equals(
                    entry.SourceDocumentKey,
                    documentKey,
                    StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(
                     entry.TypeUniqueId,
                     type.UniqueId,
                     StringComparison.OrdinalIgnoreCase) ||
                 (string.Equals(entry.TypeName, type.Name, StringComparison.Ordinal) &&
                  string.Equals(
                      entry.FamilyName ?? string.Empty,
                      type.FamilyName ?? string.Empty,
                      StringComparison.Ordinal))));
        }

        public bool HasRole(
            ElevationDecorationCatalog catalog,
            ElevationDecorationCatalogRole role)
        {
            return catalog != null && catalog.Entries != null &&
                   catalog.Entries.Any(entry => entry.Role == role);
        }

        public void Remove(ElevationDecorationCatalog catalog, string entryKey)
        {
            if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(entryKey))
            {
                return;
            }

            catalog.Entries.RemoveAll(entry =>
                string.Equals(entry.Key, entryKey, StringComparison.OrdinalIgnoreCase));
        }

        public void ClearRole(
            ElevationDecorationCatalog catalog,
            ElevationDecorationCatalogRole role)
        {
            if (catalog == null || catalog.Entries == null)
            {
                return;
            }

            catalog.Entries.RemoveAll(entry => entry.Role == role);
        }

        public BuiltInCategory GetCategory(ElevationDecorationCatalogRole role)
        {
            switch (role)
            {
                case ElevationDecorationCatalogRole.FinishFloor:
                case ElevationDecorationCatalogRole.Subfloor:
                case ElevationDecorationCatalogRole.StructuralSlab:
                    return BuiltInCategory.OST_Floors;
                case ElevationDecorationCatalogRole.FinishWall:
                    return BuiltInCategory.OST_Walls;
                case ElevationDecorationCatalogRole.Ceiling:
                    return BuiltInCategory.OST_Ceilings;
                case ElevationDecorationCatalogRole.Door:
                    return BuiltInCategory.OST_Doors;
                case ElevationDecorationCatalogRole.Window:
                    return BuiltInCategory.OST_Windows;
                case ElevationDecorationCatalogRole.Plinth:
                    return BuiltInCategory.OST_StairsRailing;
                default:
                    return BuiltInCategory.INVALID;
            }
        }

        private string GetDocumentKey(Document document)
        {
            if (document == null)
            {
                return string.Empty;
            }

            ProjectInfo projectInfo = document.ProjectInformation;
            if (projectInfo != null && !string.IsNullOrWhiteSpace(projectInfo.UniqueId))
            {
                return projectInfo.UniqueId;
            }

            return !string.IsNullOrWhiteSpace(document.PathName)
                ? document.PathName
                : document.Title;
        }

        private string BuildEntryKey(
            ElevationDecorationCatalogRole role,
            string documentKey,
            string typeUniqueId)
        {
            return ((int)role).ToString() + "|" +
                   (documentKey ?? string.Empty) + "|" +
                   (typeUniqueId ?? string.Empty);
        }
    }
}
