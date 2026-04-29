using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Resolves thumbnail file path for element types, line styles and fill patterns.
    /// </summary>
    public static class ThumbnailPathResolverService
    {
        private const string PiePrefix = "Пирог";

        private static readonly object CacheSyncRoot = new object();
        private static readonly Dictionary<string, Dictionary<string, string>> FolderLookupCache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static void ResetCache()
        {
            lock (CacheSyncRoot)
            {
                FolderLookupCache.Clear();
            }
        }

        public static string ResolveForElementType(ElementType elementType)
        {
            if (elementType == null)
            {
                return string.Empty;
            }

            string familyName = GetFamilyName(elementType);
            string typeName = elementType.Name ?? string.Empty;
            bool isLoadable = elementType is FamilySymbol;

            return ResolveByFamilyAndType(familyName, typeName, isLoadable);
        }

        public static string ResolveByFamilyAndType(string familyName, string typeName, bool? preferLoadable)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();

            string loadableFolder = ThumbnailFoldersRuntimeStore.GetLoadableFamilyImagesFolder();
            string systemFolder = ThumbnailFoldersRuntimeStore.GetSystemFamilyImagesFolder();

            List<string> folders = BuildFolderSearchOrder(preferLoadable, loadableFolder, systemFolder);
            List<string> candidateNames = BuildFamilyTypeCandidateNames(familyName, typeName);

            return ResolveFromFolders(folders, candidateNames);
        }

        public static string ResolveForLineStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
            {
                return string.Empty;
            }

            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
            string lineFolder = ThumbnailFoldersRuntimeStore.GetLineImagesFolder();

            List<string> folders = new List<string> { lineFolder };
            List<string> candidates = BuildSimpleCandidateNames(styleName);

            return ResolveFromFolders(folders, candidates);
        }

        public static string ResolveForFillPattern(string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName))
            {
                return string.Empty;
            }

            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
            string fillFolder = ThumbnailFoldersRuntimeStore.GetFillImagesFolder();

            List<string> folders = new List<string> { fillFolder };
            List<string> candidates = BuildSimpleCandidateNames(patternName);

            return ResolveFromFolders(folders, candidates);
        }

        private static string ResolveFromFolders(List<string> folders, List<string> candidateNames)
        {
            if (folders == null || candidateNames == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    continue;
                }

                string resolvedByDirectPath = TryResolveByDirectPath(folder, candidateNames);

                if (!string.IsNullOrWhiteSpace(resolvedByDirectPath))
                {
                    return resolvedByDirectPath;
                }

                string resolvedByLookup = TryResolveByFolderLookup(folder, candidateNames);

                if (!string.IsNullOrWhiteSpace(resolvedByLookup))
                {
                    return resolvedByLookup;
                }
            }

            return string.Empty;
        }

        private static string TryResolveByDirectPath(string folder, List<string> candidateNames)
        {
            for (int i = 0; i < candidateNames.Count; i++)
            {
                string name = candidateNames[i];

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string safeName = MakeSafeFileName(name);

                if (string.IsNullOrWhiteSpace(safeName))
                {
                    continue;
                }

                string pngPath = TryCombinePath(folder, safeName + ".png");

                if (string.IsNullOrWhiteSpace(pngPath))
                {
                    continue;
                }

                if (File.Exists(pngPath))
                {
                    return NormalizeOutputPath(pngPath);
                }
            }

            return string.Empty;
        }

        private static string TryCombinePath(string folder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            try
            {
                return Path.Combine(folder, fileName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryResolveByFolderLookup(string folder, List<string> candidateNames)
        {
            Dictionary<string, string> lookup = GetOrBuildFolderLookup(folder);

            if (lookup == null || lookup.Count == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < candidateNames.Count; i++)
            {
                string candidate = candidateNames[i];

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string normalizedKey = NormalizeLookupKey(candidate);

                if (string.IsNullOrWhiteSpace(normalizedKey))
                {
                    continue;
                }

                string resolvedPath;

                if (lookup.TryGetValue(normalizedKey, out resolvedPath) && File.Exists(resolvedPath))
                {
                    return NormalizeOutputPath(resolvedPath);
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, string> GetOrBuildFolderLookup(string folder)
        {
            lock (CacheSyncRoot)
            {
                Dictionary<string, string> cached;

                if (FolderLookupCache.TryGetValue(folder, out cached))
                {
                    return cached;
                }
            }

            Dictionary<string, string> built = BuildFolderLookup(folder);

            lock (CacheSyncRoot)
            {
                FolderLookupCache[folder] = built;
            }

            return built;
        }

        private static Dictionary<string, string> BuildFolderLookup(string folder)
        {
            Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return lookup;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);
            }
            catch
            {
                return lookup;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string key = NormalizeLookupKey(fileName);

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = filePath;
                }
            }

            return lookup;
        }

        private static List<string> BuildFolderSearchOrder(bool? preferLoadable, string loadableFolder, string systemFolder)
        {
            List<string> folders = new List<string>();

            if (preferLoadable.HasValue)
            {
                if (preferLoadable.Value)
                {
                    folders.Add(loadableFolder);
                    folders.Add(systemFolder);
                }
                else
                {
                    folders.Add(systemFolder);
                    folders.Add(loadableFolder);
                }
            }
            else
            {
                folders.Add(loadableFolder);
                folders.Add(systemFolder);
            }

            return folders;
        }

        private static List<string> BuildFamilyTypeCandidateNames(string familyName, string typeName)
        {
            List<string> candidates = new List<string>();

            string rawTypeName = (typeName ?? string.Empty).Trim();
            string rawFamilyName = (familyName ?? string.Empty).Trim();

            string safeTypeName = MakeSafeFileName(rawTypeName);
            string safeFamilyName = MakeSafeFileName(rawFamilyName);
            string safeFamilyType = string.Empty;

            if (!string.IsNullOrWhiteSpace(safeFamilyName) && !string.IsNullOrWhiteSpace(safeTypeName))
            {
                safeFamilyType = MakeSafeFileName(safeFamilyName + "_" + safeTypeName);
            }

            AddUnique(candidates, rawTypeName);
            AddUnique(candidates, safeTypeName);
            AddUnique(candidates, safeFamilyType);

            if (!string.IsNullOrWhiteSpace(rawFamilyName) && !string.IsNullOrWhiteSpace(rawTypeName))
            {
                AddUnique(candidates, rawFamilyName + "_" + rawTypeName);
                AddUnique(candidates, PiePrefix + "_" + rawFamilyName + "_" + rawTypeName);
            }

            if (!string.IsNullOrWhiteSpace(safeFamilyName) && !string.IsNullOrWhiteSpace(safeTypeName))
            {
                AddUnique(candidates, PiePrefix + "_" + safeFamilyName + "_" + safeTypeName);
            }

            AddUnique(candidates, PiePrefix + "_" + safeTypeName);

            return candidates;
        }

        private static List<string> BuildSimpleCandidateNames(string value)
        {
            List<string> candidates = new List<string>();
            string raw = (value ?? string.Empty).Trim();
            string safe = MakeSafeFileName(raw);

            AddUnique(candidates, raw);
            AddUnique(candidates, safe);

            return candidates;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static string NormalizeLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace(" ", string.Empty);
            normalized = normalized.Replace("_", string.Empty);
            normalized = normalized.Replace("-", string.Empty);
            normalized = normalized.Replace(".", string.Empty);

            return normalized;
        }

        private static string GetFamilyName(ElementType elementType)
        {
            if (elementType == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(elementType.FamilyName))
            {
                return elementType.FamilyName;
            }

            Parameter familyNameParameter = elementType.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);

            if (familyNameParameter == null)
            {
                return string.Empty;
            }

            return familyNameParameter.AsString() ?? string.Empty;
        }

        private static string MakeSafeFileName(string value)
        {
            string result = value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(result))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidChars.Length; i++)
            {
                result = result.Replace(invalidChars[i], '_');
            }

            return result.Trim();
        }

        private static string NormalizeOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }
    }
}
