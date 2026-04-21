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
            if (!string.IsNullOrWhiteSpace(_systemFamilyImagesFolder) &&
                !Directory.Exists(_systemFamilyImagesFolder))
            {
                _systemFamilyImagesFolder = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_loadableFamilyImagesFolder) &&
                !Directory.Exists(_loadableFamilyImagesFolder))
            {
                _loadableFamilyImagesFolder = string.Empty;
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
    /// Helper for export folder conventions:
    /// - "ctg" for category exports
    /// - "name" for naming exports
    /// Also configures thumbnail runtime folders for category exports.
    /// </summary>
    public static class ExportFolderRoutingService
    {
        private const string CategorySubFolderName = "ctg";
        private const string NamingSubFolderName = "name";
        private const string SystemImagesFolderName = "PNG_Pirogi";
        private const string LoadableImagesFolderName = "PNG_Family";

        public static string ResolveCategoryExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, CategorySubFolderName);
        }

        public static string ResolveNamingExportFolder(string selectedFolderPath)
        {
            return ResolveOrCreateSubFolder(selectedFolderPath, NamingSubFolderName);
        }

        public static void ConfigureThumbnailFoldersForCategoryExport(string categoryExportFolder)
        {
            if (string.IsNullOrWhiteSpace(categoryExportFolder))
            {
                return;
            }

            string rootFolder = ResolveRootFolderForSubFolder(categoryExportFolder, CategorySubFolderName);
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
