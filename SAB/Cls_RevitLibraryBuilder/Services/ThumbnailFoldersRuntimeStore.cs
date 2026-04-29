using System;
using System.IO;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Runtime store for thumbnail folders.
    /// Values live only during current Revit session.
    /// </summary>
    public static class ThumbnailFoldersRuntimeStore
    {
        private static readonly object SyncRoot = new object();

        private static string _systemFamilyImagesFolder = string.Empty;
        private static string _loadableFamilyImagesFolder = string.Empty;
        private static string _lineImagesFolder = string.Empty;
        private static string _fillImagesFolder = string.Empty;

        public static void SetSystemFamilyImagesFolder(string folderPath)
        {
            lock (SyncRoot)
            {
                _systemFamilyImagesFolder = NormalizePath(folderPath);
                ThumbnailPathResolverService.ResetCache();
            }
        }

        public static void SetLoadableFamilyImagesFolder(string folderPath)
        {
            lock (SyncRoot)
            {
                _loadableFamilyImagesFolder = NormalizePath(folderPath);
                ThumbnailPathResolverService.ResetCache();
            }
        }

        public static void SetLineImagesFolder(string folderPath)
        {
            lock (SyncRoot)
            {
                _lineImagesFolder = NormalizePath(folderPath);
                ThumbnailPathResolverService.ResetCache();
            }
        }

        public static void SetFillImagesFolder(string folderPath)
        {
            lock (SyncRoot)
            {
                _fillImagesFolder = NormalizePath(folderPath);
                ThumbnailPathResolverService.ResetCache();
            }
        }

        public static string GetSystemFamilyImagesFolder()
        {
            lock (SyncRoot)
            {
                ClearInvalidPathsInternal();
                return _systemFamilyImagesFolder;
            }
        }

        public static string GetLoadableFamilyImagesFolder()
        {
            lock (SyncRoot)
            {
                ClearInvalidPathsInternal();
                return _loadableFamilyImagesFolder;
            }
        }

        public static string GetLineImagesFolder()
        {
            lock (SyncRoot)
            {
                ClearInvalidPathsInternal();
                return _lineImagesFolder;
            }
        }

        public static string GetFillImagesFolder()
        {
            lock (SyncRoot)
            {
                ClearInvalidPathsInternal();
                return _fillImagesFolder;
            }
        }

        public static void ClearInvalidPaths()
        {
            lock (SyncRoot)
            {
                ClearInvalidPathsInternal();
                ThumbnailPathResolverService.ResetCache();
            }
        }

        private static void ClearInvalidPathsInternal()
        {
            if (!string.IsNullOrWhiteSpace(_systemFamilyImagesFolder) && !Directory.Exists(_systemFamilyImagesFolder))
            {
                _systemFamilyImagesFolder = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_loadableFamilyImagesFolder) && !Directory.Exists(_loadableFamilyImagesFolder))
            {
                _loadableFamilyImagesFolder = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_lineImagesFolder) && !Directory.Exists(_lineImagesFolder))
            {
                _lineImagesFolder = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_fillImagesFolder) && !Directory.Exists(_fillImagesFolder))
            {
                _fillImagesFolder = string.Empty;
            }
        }

        private static string NormalizePath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(folderPath.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Helper for export folder conventions.
    /// </summary>
    public static class ExportFolderRoutingService
    {
        private const string LegacyCategorySubFolderName = "ctg";
        private const string NamingSubFolderName = "name";

        private const string SystemFamiliesFolderName = "ctg_system families";
        private const string LoadableFamiliesFolderName = "ctg_loadable families";
        private const string LineFillFolderName = "ctg_lines-patterns";

        private const string SystemImagesFolderName = "PNG_Pirogi";
        private const string LoadableImagesFolderName = "PNG_Family";
        private const string LineImagesFolderName = "PNG_Lines";
        private const string FillImagesFolderName = "PNG_Fills";

        public static string ResolveSystemFamiliesExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, SystemFamiliesFolderName);
        }

        public static string ResolveLoadableFamiliesExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, LoadableFamiliesFolderName);
        }

        public static string ResolveLineFillExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, LineFillFolderName);
        }

        // Legacy method kept for compatibility with existing commands.
        public static string ResolveCategoryExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, LegacyCategorySubFolderName);
        }

        public static string ResolveNamingExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, NamingSubFolderName);
        }

        public static void ConfigureThumbnailFoldersForSystemFamiliesExport(string systemFolder)
        {
            string rootFolder = ResolveRootFolderForSubFolder(systemFolder, SystemFamiliesFolderName);
            string systemImagesFolder = Path.Combine(rootFolder, SystemImagesFolderName);

            ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(systemImagesFolder);
            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
        }

        public static void ConfigureThumbnailFoldersForLoadableFamiliesExport(string loadableFolder)
        {
            string rootFolder = ResolveRootFolderForSubFolder(loadableFolder, LoadableFamiliesFolderName);
            string loadableImagesFolder = Path.Combine(rootFolder, LoadableImagesFolderName);

            ThumbnailFoldersRuntimeStore.SetLoadableFamilyImagesFolder(loadableImagesFolder);
            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
        }

        public static void ConfigureThumbnailFoldersForLineFillExport(string lineFillFolder)
        {
            string rootFolder = ResolveRootFolderForSubFolder(lineFillFolder, LineFillFolderName);
            string lineImagesFolder = Path.Combine(rootFolder, LineFillFolderName, LineImagesFolderName);
            string fillImagesFolder = Path.Combine(rootFolder, LineFillFolderName, FillImagesFolderName);

            ThumbnailFoldersRuntimeStore.SetLineImagesFolder(lineImagesFolder);
            ThumbnailFoldersRuntimeStore.SetFillImagesFolder(fillImagesFolder);
            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
        }

        // Legacy method kept for compatibility with existing commands.
        public static void ConfigureThumbnailFoldersForCategoryExport(string categoryExportFolder)
        {
            if (string.IsNullOrWhiteSpace(categoryExportFolder))
            {
                return;
            }

            string rootFolder = ResolveRootFolderForSubFolder(categoryExportFolder, LegacyCategorySubFolderName);
            string systemImagesFolder = Path.Combine(rootFolder, SystemImagesFolderName);
            string loadableImagesFolder = Path.Combine(rootFolder, LoadableImagesFolderName);

            ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(systemImagesFolder);
            ThumbnailFoldersRuntimeStore.SetLoadableFamilyImagesFolder(loadableImagesFolder);
            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
        }

        private static string ResolveOrCreateSubFolder(string selectedFolderPath, string expectedSubFolderName)
        {
            string normalizedPath = NormalizePath(selectedFolderPath);

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            string trimmedPath = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leafName = Path.GetFileName(trimmedPath);

            if (string.Equals(leafName, expectedSubFolderName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureDirectoryExists(trimmedPath);
                return trimmedPath;
            }

            string targetPath = Path.Combine(trimmedPath, expectedSubFolderName);
            EnsureDirectoryExists(targetPath);
            return targetPath;
        }

        private static string ResolveRootFolderForSubFolder(string folderPath, string expectedSubFolderName)
        {
            string normalizedPath = NormalizePath(folderPath);

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            string trimmedPath = normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string leafName = Path.GetFileName(trimmedPath);

            if (!string.Equals(leafName, expectedSubFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPath;
            }

            DirectoryInfo parent = Directory.GetParent(trimmedPath);

            if (parent == null || string.IsNullOrWhiteSpace(parent.FullName))
            {
                return trimmedPath;
            }

            return parent.FullName;
        }

        private static void EnsureDirectoryExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        private static string NormalizePath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(folderPath.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
