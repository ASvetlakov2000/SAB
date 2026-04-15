using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Resolves thumbnail file path for element types and tabular rows.
    /// </summary>
    public static class ThumbnailPathResolverService
    {
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
            List<string> candidateNames = BuildCandidateNames(familyName, typeName);

            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    continue;
                }

                for (int j = 0; j < candidateNames.Count; j++)
                {
                    string name = candidateNames[j];
                    string pngPath = Path.Combine(folder, name + ".png");

                    if (File.Exists(pngPath))
                    {
                        try
                        {
                            return Path.GetFullPath(pngPath);
                        }
                        catch
                        {
                            return pngPath;
                        }
                    }
                }
            }

            return string.Empty;
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

        private static List<string> BuildCandidateNames(string familyName, string typeName)
        {
            List<string> candidates = new List<string>();

            string safeTypeName = MakeSafeFileName(typeName);
            string safeFamilyName = MakeSafeFileName(familyName);
            string safeFamilyType = string.Empty;

            if (!string.IsNullOrWhiteSpace(safeFamilyName) && !string.IsNullOrWhiteSpace(safeTypeName))
            {
                safeFamilyType = MakeSafeFileName(safeFamilyName + "_" + safeTypeName);
            }

            if (!string.IsNullOrWhiteSpace(safeTypeName))
            {
                candidates.Add(safeTypeName);
            }

            if (!string.IsNullOrWhiteSpace(safeFamilyType))
            {
                candidates.Add(safeFamilyType);
            }

            return candidates;
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

            string value = familyNameParameter.AsString();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
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
    }
}
