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
            }
        }

        public static void SetLoadableFamilyImagesFolder(string folderPath)
        {
            lock (SyncRoot)
            {
                _loadableFamilyImagesFolder = NormalizePath(folderPath);
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
}
