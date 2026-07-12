using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services;
using SAB.BimDashboard.Models;
using SAB.BimDashboard.Services.Data;
using SAB.BimDashboard.Services.Processing;
using SAB.BimDashboard.Services.Reporting;
using SAB.BimDashboard.Services.Viewer;

namespace SAB.BimDashboard.Services
{
    /// <summary>
    /// Builds one HTML catalog from any set of supported CSV files and optional PNG folders.
    /// </summary>
    public class DashboardLaunchService
    {
        public DashboardLaunchResult Launch(UIApplication uiApplication, DashboardLaunchRequest request)
        {
            if (uiApplication == null)
            {
                throw new ArgumentNullException(nameof(uiApplication));
            }

            DashboardLaunchRequest safeRequest = request ?? new DashboardLaunchRequest();
            ResolveMissingImageFolders(safeRequest);
            ConfigureImageFolders(safeRequest);

            DashboardLaunchResult launchResult = new DashboardLaunchResult();
            ProviderResult combinedProviderResult = new ProviderResult();
            combinedProviderResult.ProjectName = BuildProjectName(safeRequest.CsvFilePaths);

            List<string> loadedProfiles = new List<string>();
            CsvDataProvider provider = new CsvDataProvider();
            List<string> uniqueCsvPaths = BuildUniqueExistingPaths(safeRequest.CsvFilePaths, launchResult.Warnings);

            for (int i = 0; i < uniqueCsvPaths.Count; i++)
            {
                string csvPath = uniqueCsvPaths[i];

                try
                {
                    DataProviderContext context = new DataProviderContext
                    {
                        UiApplication = uiApplication,
                        FilePath = csvPath,
                        SourceProfile = DashboardProfileType.SystemFamilies
                    };

                    ProviderResult fileResult = provider.Load(context);

                    if (fileResult == null || fileResult.Records == null || fileResult.Records.Count == 0)
                    {
                        launchResult.Warnings.Add(Path.GetFileName(csvPath) + ": файл не содержит данных для просмотрщика.");
                        continue;
                    }

                    launchResult.LoadedCsvFilesCount++;
                    AddUniqueProfile(loadedProfiles, fileResult.SourceProfile);
                    AddRecords(combinedProviderResult.Records, fileResult.Records, fileResult.SourceProfile);
                    AddWarnings(launchResult.Warnings, fileResult.Warnings, Path.GetFileName(csvPath));
                }
                catch (Exception exception)
                {
                    launchResult.Warnings.Add(Path.GetFileName(csvPath) + ": " + exception.Message);
                }
            }

            launchResult.LoadedRecordsCount = combinedProviderResult.Records.Count;

            if (combinedProviderResult.Records.Count == 0)
            {
                AddEmptyStateRecord(combinedProviderResult.Records, safeRequest);
                launchResult.Warnings.Add("Корректные CSV-данные не загружены. Просмотрщик открыт со страницей состояния.");
            }

            combinedProviderResult.SourceProfile = ResolveCombinedProfile(loadedProfiles);

            AddRelevantImageWarnings(launchResult.Warnings, loadedProfiles, safeRequest);
            AddImageFreshnessWarnings(launchResult.Warnings, loadedProfiles, safeRequest);

            DataMapper dataMapper = new DataMapper();
            DashboardData dashboardData = dataMapper.Map(combinedProviderResult);

            HtmlReportBuilder htmlReportBuilder = new HtmlReportBuilder();
            string htmlPath = htmlReportBuilder.Generate(dashboardData);

            IDashboardViewer viewer = new ExternalBrowserDashboardViewer();
            viewer.Open(htmlPath);

            launchResult.HtmlPath = htmlPath;
            return launchResult;
        }

        // Block responsible for connecting selected PNG folders to the existing thumbnail resolver.
        private static void ConfigureImageFolders(DashboardLaunchRequest request)
        {
            ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(request.SystemFamilyImagesFolder);
            ThumbnailFoldersRuntimeStore.SetLoadableFamilyImagesFolder(request.LoadableFamilyImagesFolder);
            ThumbnailFoldersRuntimeStore.SetLineImagesFolder(request.LineImagesFolder);
            ThumbnailFoldersRuntimeStore.SetFillImagesFolder(request.FillImagesFolder);
            ThumbnailFoldersRuntimeStore.ClearInvalidPaths();
        }

        // Block responsible for the same nearby-folder fallback used by the preparation window.
        private static void ResolveMissingImageFolders(DashboardLaunchRequest request)
        {
            if (request == null)
            {
                return;
            }

            ImageFolderDiscoveryService discoveryService = new ImageFolderDiscoveryService();

            if (string.IsNullOrWhiteSpace(request.SystemFamilyImagesFolder) ||
                !Directory.Exists(request.SystemFamilyImagesFolder))
            {
                request.SystemFamilyImagesFolder = GetDiscoveredFolder(
                    discoveryService,
                    DashboardProfileType.SystemFamilies,
                    request.CsvFilePaths);
            }

            if (string.IsNullOrWhiteSpace(request.LoadableFamilyImagesFolder) ||
                !Directory.Exists(request.LoadableFamilyImagesFolder))
            {
                request.LoadableFamilyImagesFolder = GetDiscoveredFolder(
                    discoveryService,
                    DashboardProfileType.LoadableFamilies,
                    request.CsvFilePaths);
            }

            if (string.IsNullOrWhiteSpace(request.LineImagesFolder) ||
                !Directory.Exists(request.LineImagesFolder))
            {
                request.LineImagesFolder = GetDiscoveredFolder(
                    discoveryService,
                    DashboardProfileType.Lines,
                    request.CsvFilePaths);
            }

            if (string.IsNullOrWhiteSpace(request.FillImagesFolder) ||
                !Directory.Exists(request.FillImagesFolder))
            {
                request.FillImagesFolder = GetDiscoveredFolder(
                    discoveryService,
                    DashboardProfileType.FillPatterns,
                    request.CsvFilePaths);
            }
        }

        private static string GetDiscoveredFolder(
            ImageFolderDiscoveryService discoveryService,
            DashboardProfileType profile,
            IList<string> csvFilePaths)
        {
            ImageFolderMatchResult result = discoveryService.FindBestMatch(profile, csvFilePaths);
            return result != null && result.PngFileCount > 0 && Directory.Exists(result.FolderPath)
                ? result.FolderPath
                : string.Empty;
        }

        private static List<string> BuildUniqueExistingPaths(IList<string> sourcePaths, IList<string> warnings)
        {
            List<string> result = new List<string>();

            if (sourcePaths == null)
            {
                return result;
            }

            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string sourcePath = sourcePaths[i];

                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    continue;
                }

                string fullPath;

                try
                {
                    fullPath = Path.GetFullPath(sourcePath.Trim());
                }
                catch
                {
                    warnings.Add("Некорректный путь к CSV: " + sourcePath);
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    warnings.Add("CSV-файл не найден: " + fullPath);
                    continue;
                }

                bool alreadyAdded = false;

                for (int existingIndex = 0; existingIndex < result.Count; existingIndex++)
                {
                    if (string.Equals(result[existingIndex], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    result.Add(fullPath);
                }
            }

            return result;
        }

        private static void AddRecords(
            IList<UnifiedRecord> destination,
            IList<UnifiedRecord> source,
            string sourceProfile)
        {
            if (destination == null || source == null)
            {
                return;
            }

            string profileDisplayName = GetProfileDisplayName(sourceProfile);

            for (int i = 0; i < source.Count; i++)
            {
                UnifiedRecord record = source[i];

                if (record == null)
                {
                    continue;
                }

                if (record.Fields == null)
                {
                    record.Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                record.Fields["Профиль"] = profileDisplayName;
                record.Fields["ProfileKey"] = sourceProfile ?? string.Empty;
                destination.Add(record);
            }
        }

        private static void AddEmptyStateRecord(IList<UnifiedRecord> records, DashboardLaunchRequest request)
        {
            if (records == null)
            {
                return;
            }

            UnifiedRecord record = new UnifiedRecord();
            record.Name = "Данные не загружены";
            record.Count = 1;
            record.Fields["Статус"] = "CSV-файлы не выбраны или не прочитаны";
            record.Fields["Подсказка"] = "Добавьте один или несколько CSV в режиме «Просмотрщик» и запустите его повторно.";
            record.Fields["Загружено папок PNG"] = CountSelectedImageFolders(request).ToString();
            record.Fields["SourceType"] = "ViewerPreparation";
            record.Fields["RecordType"] = "Status";
            record.Fields["ProfileKey"] = "Empty";
            records.Add(record);
        }

        private static int CountSelectedImageFolders(DashboardLaunchRequest request)
        {
            if (request == null)
            {
                return 0;
            }

            int count = 0;
            count += Directory.Exists(request.SystemFamilyImagesFolder) ? 1 : 0;
            count += Directory.Exists(request.LoadableFamilyImagesFolder) ? 1 : 0;
            count += Directory.Exists(request.LineImagesFolder) ? 1 : 0;
            count += Directory.Exists(request.FillImagesFolder) ? 1 : 0;
            return count;
        }

        private static void AddUniqueProfile(IList<string> profiles, string sourceProfile)
        {
            if (profiles == null || string.IsNullOrWhiteSpace(sourceProfile))
            {
                return;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i], sourceProfile, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            profiles.Add(sourceProfile);
        }

        private static string ResolveCombinedProfile(IList<string> profiles)
        {
            if (profiles == null || profiles.Count == 0)
            {
                return "Empty";
            }

            if (profiles.Count == 1)
            {
                return profiles[0];
            }

            return "Combined";
        }

        private static string BuildProjectName(IList<string> csvPaths)
        {
            if (csvPaths == null || csvPaths.Count == 0)
            {
                return "Подготовка просмотрщика";
            }

            if (csvPaths.Count == 1)
            {
                return Path.GetFileNameWithoutExtension(csvPaths[0]) ?? "Каталог";
            }

            return "Сводный каталог — " + csvPaths.Count + " CSV";
        }

        private static void AddWarnings(IList<string> destination, IList<string> source, string prefix)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(source[i]))
                {
                    continue;
                }

                destination.Add((string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + ": ") + source[i]);
            }
        }

        private static void AddRelevantImageWarnings(
            IList<string> warnings,
            IList<string> profiles,
            DashboardLaunchRequest request)
        {
            if (warnings == null || profiles == null || request == null)
            {
                return;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                string profile = profiles[i];

                if (string.Equals(profile, DashboardProfileType.SystemFamilies.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    AddImageFolderWarning(warnings, request.SystemFamilyImagesFolder, "системных типов");
                }
                else if (string.Equals(profile, DashboardProfileType.LoadableFamilies.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    AddImageFolderWarning(warnings, request.LoadableFamilyImagesFolder, "загружаемых семейств");
                }
                else if (string.Equals(profile, DashboardProfileType.Lines.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    AddImageFolderWarning(warnings, request.LineImagesFolder, "стилей линий");
                }
                else if (string.Equals(profile, DashboardProfileType.FillPatterns.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    AddImageFolderWarning(warnings, request.FillImagesFolder, "штриховок");
                }
            }
        }

        private static void AddImageFolderWarning(IList<string> warnings, string folderPath, string subject)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                warnings.Add("Папка PNG для " + subject + " не выбрана. Таблица откроется без части превью.");
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    warnings.Add("В папке PNG для " + subject + " изображения не найдены.");
                }
            }
            catch (Exception exception)
            {
                warnings.Add("Не удалось прочитать папку PNG для " + subject + ": " + exception.Message);
            }
        }

        private static void AddImageFreshnessWarnings(
            IList<string> warnings,
            IList<string> profiles,
            DashboardLaunchRequest request)
        {
            if (warnings == null || profiles == null || request == null)
            {
                return;
            }

            ImageFolderDiscoveryService discoveryService = new ImageFolderDiscoveryService();
            AddImageFreshnessWarning(
                warnings,
                profiles,
                discoveryService,
                DashboardProfileType.SystemFamilies,
                request.CsvFilePaths,
                request.SystemFamilyImagesFolder);
            AddImageFreshnessWarning(
                warnings,
                profiles,
                discoveryService,
                DashboardProfileType.LoadableFamilies,
                request.CsvFilePaths,
                request.LoadableFamilyImagesFolder);
            AddImageFreshnessWarning(
                warnings,
                profiles,
                discoveryService,
                DashboardProfileType.Lines,
                request.CsvFilePaths,
                request.LineImagesFolder);
            AddImageFreshnessWarning(
                warnings,
                profiles,
                discoveryService,
                DashboardProfileType.FillPatterns,
                request.CsvFilePaths,
                request.FillImagesFolder);
        }

        private static void AddImageFreshnessWarning(
            IList<string> warnings,
            IList<string> loadedProfiles,
            ImageFolderDiscoveryService discoveryService,
            DashboardProfileType profile,
            IList<string> csvFilePaths,
            string folderPath)
        {
            if (!ContainsProfile(loadedProfiles, profile) ||
                string.IsNullOrWhiteSpace(folderPath) ||
                !Directory.Exists(folderPath))
            {
                return;
            }

            ImageFolderMatchResult result = discoveryService.EvaluateSelectedFolder(
                profile,
                csvFilePaths,
                folderPath,
                false);

            if (result.Status == ImageFolderFreshnessStatus.TimeDifferenceWarning ||
                result.Status == ImageFolderFreshnessStatus.Outdated ||
                result.Status == ImageFolderFreshnessStatus.ReadError)
            {
                warnings.Add(DashboardProfileResolver.GetDisplayName(profile) + ": " + result.Message);
            }
        }

        private static bool ContainsProfile(IList<string> profiles, DashboardProfileType profile)
        {
            string expected = profile.ToString();

            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetProfileDisplayName(string profile)
        {
            if (string.Equals(profile, DashboardProfileType.SystemFamilies.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Системные семейства";
            }

            if (string.Equals(profile, DashboardProfileType.LoadableFamilies.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Загружаемые семейства";
            }

            if (string.Equals(profile, DashboardProfileType.Lines.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Линии";
            }

            if (string.Equals(profile, DashboardProfileType.FillPatterns.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Штриховки";
            }

            return string.IsNullOrWhiteSpace(profile) ? "Не определён" : profile;
        }
    }
}
