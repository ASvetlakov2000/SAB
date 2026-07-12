using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services
{
    /// <summary>
    /// Locates profile-specific PNG folders near CSV files and evaluates data freshness.
    /// </summary>
    public class ImageFolderDiscoveryService
    {
        // Block with freshness thresholds that can be adjusted later if the export workflow changes.
        private const double WarningDifferenceHours = 2.0;
        private const double OutdatedDifferenceHours = 24.0;
        private const int MaximumCandidateDirectories = 250;
        private const int MaximumPngFilesPerCandidate = 10000;
        private const int MaximumSearchDepth = 2;

        public ImageFolderMatchResult FindBestMatch(
            DashboardProfileType profile,
            IList<string> csvFilePaths)
        {
            string csvFilePath = FindNewestCsvForProfile(profile, csvFilePaths);

            if (string.IsNullOrWhiteSpace(csvFilePath))
            {
                return BuildMissingCsvResult(profile);
            }

            List<string> candidates = BuildCandidateDirectories(profile, csvFilePath);
            FolderCandidate bestCandidate = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                FolderCandidate candidate = AnalyzeCandidate(profile, csvFilePath, candidates[i]);

                if (candidate == null || candidate.PngFileCount == 0)
                {
                    continue;
                }

                if (bestCandidate == null || candidate.Score > bestCandidate.Score)
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate == null)
            {
                ImageFolderMatchResult notFoundResult = BuildNotFoundResult(profile, csvFilePath);
                notFoundResult.IsAutoDetected = true;
                return notFoundResult;
            }

            return BuildResult(
                profile,
                csvFilePath,
                bestCandidate.FolderPath,
                bestCandidate.PngFileCount,
                bestCandidate.NewestPngWriteTime,
                true);
        }

        public ImageFolderMatchResult EvaluateSelectedFolder(
            DashboardProfileType profile,
            IList<string> csvFilePaths,
            string folderPath,
            bool isAutoDetected)
        {
            string csvFilePath = FindNewestCsvForProfile(profile, csvFilePaths);
            string normalizedFolderPath = NormalizePath(folderPath);

            if (string.IsNullOrWhiteSpace(csvFilePath))
            {
                ImageFolderMatchResult missingCsvResult = BuildMissingCsvResult(profile);
                missingCsvResult.FolderPath = normalizedFolderPath;
                missingCsvResult.IsAutoDetected = isAutoDetected;

                if (!string.IsNullOrWhiteSpace(normalizedFolderPath) && Directory.Exists(normalizedFolderPath))
                {
                    FolderScanResult scanWithoutCsv = ScanPngFiles(normalizedFolderPath);
                    missingCsvResult.PngFileCount = scanWithoutCsv.PngFileCount;
                    missingCsvResult.NewestImageModifiedAt = scanWithoutCsv.NewestPngWriteTime;

                    if (!string.IsNullOrWhiteSpace(scanWithoutCsv.ErrorMessage))
                    {
                        missingCsvResult.Status = ImageFolderFreshnessStatus.ReadError;
                        missingCsvResult.Message = "Не удалось проверить папку PNG: " + scanWithoutCsv.ErrorMessage;
                    }
                    else if (scanWithoutCsv.PngFileCount == 0)
                    {
                        missingCsvResult.Status = ImageFolderFreshnessStatus.NotFound;
                        missingCsvResult.Message = "В выбранной папке изображения PNG не найдены.";
                    }
                }

                return missingCsvResult;
            }

            if (string.IsNullOrWhiteSpace(normalizedFolderPath) || !Directory.Exists(normalizedFolderPath))
            {
                ImageFolderMatchResult notFoundResult = BuildNotFoundResult(profile, csvFilePath);
                notFoundResult.FolderPath = normalizedFolderPath;
                notFoundResult.IsAutoDetected = isAutoDetected;
                return notFoundResult;
            }

            FolderScanResult scanResult = ScanPngFiles(normalizedFolderPath);

            if (!string.IsNullOrWhiteSpace(scanResult.ErrorMessage))
            {
                return new ImageFolderMatchResult
                {
                    Profile = profile,
                    CsvFilePath = csvFilePath,
                    FolderPath = normalizedFolderPath,
                    CsvModifiedAt = File.GetLastWriteTime(csvFilePath),
                    IsAutoDetected = isAutoDetected,
                    Status = ImageFolderFreshnessStatus.ReadError,
                    Message = "Не удалось проверить папку PNG: " + scanResult.ErrorMessage
                };
            }

            if (scanResult.PngFileCount == 0 || !scanResult.NewestPngWriteTime.HasValue)
            {
                ImageFolderMatchResult emptyResult = BuildNotFoundResult(profile, csvFilePath);
                emptyResult.FolderPath = normalizedFolderPath;
                emptyResult.IsAutoDetected = isAutoDetected;
                emptyResult.Message = "В выбранной папке изображения PNG не найдены.";
                return emptyResult;
            }

            return BuildResult(
                profile,
                csvFilePath,
                normalizedFolderPath,
                scanResult.PngFileCount,
                scanResult.NewestPngWriteTime.Value,
                isAutoDetected);
        }

        private static string FindNewestCsvForProfile(
            DashboardProfileType profile,
            IList<string> csvFilePaths)
        {
            string result = string.Empty;
            DateTime newestWriteTime = DateTime.MinValue;

            if (csvFilePaths == null)
            {
                return result;
            }

            for (int i = 0; i < csvFilePaths.Count; i++)
            {
                string filePath = NormalizePath(csvFilePaths[i]);
                DashboardProfileType detectedProfile;

                if (string.IsNullOrWhiteSpace(filePath) ||
                    !File.Exists(filePath) ||
                    !DashboardProfileResolver.TryDetectFromFilePath(filePath, out detectedProfile) ||
                    detectedProfile != profile)
                {
                    continue;
                }

                DateTime writeTime;

                try
                {
                    writeTime = File.GetLastWriteTime(filePath);
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(result) || writeTime > newestWriteTime)
                {
                    result = filePath;
                    newestWriteTime = writeTime;
                }
            }

            return result;
        }

        private static List<string> BuildCandidateDirectories(
            DashboardProfileType profile,
            string csvFilePath)
        {
            List<string> result = new List<string>();
            string csvDirectory = Path.GetDirectoryName(csvFilePath) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(csvDirectory) || !Directory.Exists(csvDirectory))
            {
                return result;
            }

            DirectoryInfo csvDirectoryInfo = new DirectoryInfo(csvDirectory);
            string parentDirectory = csvDirectoryInfo.Parent != null
                ? csvDirectoryInfo.Parent.FullName
                : string.Empty;
            string expectedFolderName = GetExpectedFolderName(profile);

            // Expected library structure is checked before the broader neighboring-folder search.
            AddCandidate(result, Path.Combine(csvDirectory, expectedFolderName));

            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                AddCandidate(result, Path.Combine(parentDirectory, expectedFolderName));
            }

            if (profile == DashboardProfileType.LoadableFamilies)
            {
                AddCandidate(result, Path.Combine(csvDirectory, "thumbnails", "loadable"));

                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    AddCandidate(result, Path.Combine(parentDirectory, "thumbnails", "loadable"));
                }
            }

            AddCandidate(result, csvDirectory);
            AddImmediateChildren(result, csvDirectory);

            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                AddImmediateChildren(result, parentDirectory);
            }

            // One additional level covers paths such as thumbnails/loadable without scanning the whole disk.
            List<string> firstLevelSnapshot = new List<string>(result);

            for (int i = 0; i < firstLevelSnapshot.Count && result.Count < MaximumCandidateDirectories; i++)
            {
                if (IsRelevantFolderName(profile, firstLevelSnapshot[i]))
                {
                    AddImmediateChildren(result, firstLevelSnapshot[i]);
                }
            }

            return result;
        }

        private static void AddImmediateChildren(IList<string> result, string parentFolder)
        {
            if (result == null ||
                result.Count >= MaximumCandidateDirectories ||
                string.IsNullOrWhiteSpace(parentFolder) ||
                !Directory.Exists(parentFolder))
            {
                return;
            }

            try
            {
                string[] childDirectories = Directory.GetDirectories(parentFolder, "*", SearchOption.TopDirectoryOnly);

                for (int i = 0; i < childDirectories.Length && result.Count < MaximumCandidateDirectories; i++)
                {
                    AddCandidate(result, childDirectories[i]);
                }
            }
            catch
            {
                // Unreadable neighboring folders are ignored; the user can still select a path manually.
            }
        }

        private static void AddCandidate(IList<string> result, string candidatePath)
        {
            if (result == null || result.Count >= MaximumCandidateDirectories)
            {
                return;
            }

            string normalizedPath = NormalizePath(candidatePath);

            if (string.IsNullOrWhiteSpace(normalizedPath) || !Directory.Exists(normalizedPath))
            {
                return;
            }

            for (int i = 0; i < result.Count; i++)
            {
                if (string.Equals(result[i], normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            result.Add(normalizedPath);
        }

        private static FolderCandidate AnalyzeCandidate(
            DashboardProfileType profile,
            string csvFilePath,
            string candidateFolder)
        {
            FolderScanResult scanResult = ScanPngFiles(candidateFolder);

            if (scanResult.PngFileCount == 0 || !scanResult.NewestPngWriteTime.HasValue)
            {
                return null;
            }

            DateTime csvWriteTime;

            try
            {
                csvWriteTime = File.GetLastWriteTime(csvFilePath);
            }
            catch
            {
                return null;
            }

            double differenceHours = Math.Abs((scanResult.NewestPngWriteTime.Value - csvWriteTime).TotalHours);
            int score = BuildCandidateScore(profile, csvFilePath, candidateFolder, scanResult.PngFileCount, differenceHours);

            return new FolderCandidate
            {
                FolderPath = candidateFolder,
                PngFileCount = scanResult.PngFileCount,
                NewestPngWriteTime = scanResult.NewestPngWriteTime.Value,
                Score = score
            };
        }

        private static int BuildCandidateScore(
            DashboardProfileType profile,
            string csvFilePath,
            string folderPath,
            int pngFileCount,
            double differenceHours)
        {
            int score = 0;
            string leafName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string expectedFolderName = GetExpectedFolderName(profile);

            if (string.Equals(leafName, expectedFolderName, StringComparison.OrdinalIgnoreCase))
            {
                score += 2000;
            }
            else if (IsRelevantFolderName(profile, folderPath))
            {
                score += 800;
            }

            string csvDirectory = Path.GetDirectoryName(csvFilePath) ?? string.Empty;

            if (string.Equals(csvDirectory, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            score += Math.Min(pngFileCount, 200);
            score += Convert.ToInt32(Math.Max(0.0, 200.0 - Math.Min(differenceHours, 200.0)));
            return score;
        }

        private static FolderScanResult ScanPngFiles(string rootFolder)
        {
            FolderScanResult result = new FolderScanResult();
            List<FolderLevel> queue = new List<FolderLevel>();
            queue.Add(new FolderLevel(rootFolder, 0));

            for (int queueIndex = 0;
                 queueIndex < queue.Count && result.PngFileCount < MaximumPngFilesPerCandidate;
                 queueIndex++)
            {
                FolderLevel current = queue[queueIndex];

                try
                {
                    string[] files = Directory.GetFiles(current.FolderPath, "*.png", SearchOption.TopDirectoryOnly);

                    for (int fileIndex = 0;
                         fileIndex < files.Length && result.PngFileCount < MaximumPngFilesPerCandidate;
                         fileIndex++)
                    {
                        DateTime writeTime = File.GetLastWriteTime(files[fileIndex]);
                        result.PngFileCount++;

                        if (!result.NewestPngWriteTime.HasValue || writeTime > result.NewestPngWriteTime.Value)
                        {
                            result.NewestPngWriteTime = writeTime;
                        }
                    }

                    if (current.Depth >= MaximumSearchDepth)
                    {
                        continue;
                    }

                    string[] childDirectories = Directory.GetDirectories(current.FolderPath, "*", SearchOption.TopDirectoryOnly);

                    for (int childIndex = 0; childIndex < childDirectories.Length; childIndex++)
                    {
                        DirectoryInfo childInfo = new DirectoryInfo(childDirectories[childIndex]);

                        if ((childInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        {
                            continue;
                        }

                        queue.Add(new FolderLevel(childDirectories[childIndex], current.Depth + 1));
                    }
                }
                catch (Exception exception)
                {
                    if (queueIndex == 0)
                    {
                        result.ErrorMessage = exception.Message;
                    }
                }
            }

            return result;
        }

        private static ImageFolderMatchResult BuildResult(
            DashboardProfileType profile,
            string csvFilePath,
            string folderPath,
            int pngFileCount,
            DateTime newestPngWriteTime,
            bool isAutoDetected)
        {
            DateTime csvWriteTime = File.GetLastWriteTime(csvFilePath);
            double differenceHours = Math.Abs((newestPngWriteTime - csvWriteTime).TotalHours);
            ImageFolderFreshnessStatus status;
            string freshnessText;

            if (differenceHours > OutdatedDifferenceHours)
            {
                status = ImageFolderFreshnessStatus.Outdated;
                freshnessText = "Изображения устарели относительно CSV.";
            }
            else if (differenceHours >= WarningDifferenceHours)
            {
                status = ImageFolderFreshnessStatus.TimeDifferenceWarning;
                freshnessText = "CSV и PNG созданы с заметной разницей во времени.";
            }
            else
            {
                status = ImageFolderFreshnessStatus.Current;
                freshnessText = "CSV и PNG актуальны друг относительно друга.";
            }

            return new ImageFolderMatchResult
            {
                Profile = profile,
                CsvFilePath = csvFilePath,
                FolderPath = folderPath,
                CsvModifiedAt = csvWriteTime,
                NewestImageModifiedAt = newestPngWriteTime,
                PngFileCount = pngFileCount,
                DifferenceHours = differenceHours,
                IsAutoDetected = isAutoDetected,
                Status = status,
                Message = freshnessText +
                          " CSV: " + csvWriteTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) +
                          "; PNG: " + newestPngWriteTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) +
                          "; разница: " + FormatDifference(differenceHours) + "."
            };
        }

        private static ImageFolderMatchResult BuildMissingCsvResult(DashboardProfileType profile)
        {
            return new ImageFolderMatchResult
            {
                Profile = profile,
                Status = ImageFolderFreshnessStatus.NoMatchingCsv,
                Message = "Профильный CSV не выбран — актуальность PNG пока не проверяется."
            };
        }

        private static ImageFolderMatchResult BuildNotFoundResult(
            DashboardProfileType profile,
            string csvFilePath)
        {
            return new ImageFolderMatchResult
            {
                Profile = profile,
                CsvFilePath = csvFilePath,
                CsvModifiedAt = File.Exists(csvFilePath) ? File.GetLastWriteTime(csvFilePath) : (DateTime?)null,
                Status = ImageFolderFreshnessStatus.NotFound,
                Message = "Автопоиск не нашёл папку с PNG рядом с таблицей. Укажите путь вручную."
            };
        }

        private static string GetExpectedFolderName(DashboardProfileType profile)
        {
            switch (profile)
            {
                case DashboardProfileType.SystemFamilies:
                    return "PNG_Pirogi";
                case DashboardProfileType.LoadableFamilies:
                    return "PNG_Family";
                case DashboardProfileType.Lines:
                    return "PNG_Lines";
                case DashboardProfileType.FillPatterns:
                    return "PNG_Fills";
                default:
                    return "PNG";
            }
        }

        private static bool IsRelevantFolderName(DashboardProfileType profile, string folderPath)
        {
            string normalizedName = (Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty)
                .ToLowerInvariant();

            switch (profile)
            {
                case DashboardProfileType.SystemFamilies:
                    return normalizedName.Contains("pirogi") ||
                           normalizedName.Contains("пирог") ||
                           normalizedName.Contains("system") ||
                           normalizedName.Contains("систем");
                case DashboardProfileType.LoadableFamilies:
                    return normalizedName.Contains("family") ||
                           normalizedName.Contains("loadable") ||
                           normalizedName.Contains("загружа") ||
                           normalizedName.Contains("thumbnail");
                case DashboardProfileType.Lines:
                    return normalizedName.Contains("line") || normalizedName.Contains("лини");
                case DashboardProfileType.FillPatterns:
                    return normalizedName.Contains("fill") || normalizedName.Contains("штрих");
                default:
                    return false;
            }
        }

        private static string FormatDifference(double differenceHours)
        {
            if (differenceHours < 1.0)
            {
                return Math.Round(differenceHours * 60.0, MidpointRounding.AwayFromZero) + " мин";
            }

            return Math.Round(differenceHours, 1, MidpointRounding.AwayFromZero) + " ч";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private class FolderCandidate
        {
            public string FolderPath { get; set; }

            public int PngFileCount { get; set; }

            public DateTime NewestPngWriteTime { get; set; }

            public int Score { get; set; }
        }

        private class FolderScanResult
        {
            public int PngFileCount { get; set; }

            public DateTime? NewestPngWriteTime { get; set; }

            public string ErrorMessage { get; set; }
        }

        private class FolderLevel
        {
            public FolderLevel(string folderPath, int depth)
            {
                FolderPath = folderPath;
                Depth = depth;
            }

            public string FolderPath { get; private set; }

            public int Depth { get; private set; }
        }
    }
}
