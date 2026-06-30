using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WixSharp;
using IOFile = System.IO.File;
using WxsFile = WixSharp.File;

namespace WixSharpInstaller
{
    internal static class Program
    {
        // Блок констант для стабильных UpgradeCode.
        private const string UpgradeCode2023 = "F9D69961-6B30-4C1E-A469-0CC9C31EFD8E";
        private const string UpgradeCode2024 = "D7A573D9-4A9A-42A1-8D0D-F552AEEA6B87";
        private const string FamiliesRootFolderName = "Families for Plugin";
        private const string FamiliesManifestFileName = "families.manifest.tsv";
        private const string FamiliesInstallerStagingFolderName = "_families_for_installer";

        private static int Main(string[] args)
        {
            try
            {
                Dictionary<string, string> options = ParseArguments(args);
                string installerRoot = AppDomain.CurrentDomain.BaseDirectory;
                string repositoryRoot = GetOption(options, "--root", Path.GetFullPath(Path.Combine(installerRoot, "..", "..", "..", "..", "..")));

                string binFolder = GetOption(options, "--bin", Path.GetFullPath(Path.Combine(repositoryRoot, "SAB", "bin", "Debug")));
                string outputFolder = GetOption(options, "--out", Path.GetFullPath(Path.Combine(repositoryRoot, "Installer", "output")));
                string explicitVersion = GetOptionRaw(options, "--version", string.Empty);

                if (!Directory.Exists(binFolder))
                {
                    throw new DirectoryNotFoundException("Bin folder not found: " + binFolder);
                }

                string assemblyPath = Path.Combine(binFolder, "SAB.dll");
                if (!IOFile.Exists(assemblyPath))
                {
                    throw new FileNotFoundException("SAB.dll was not found in bin folder.", assemblyPath);
                }

                string addin2023Path = Path.Combine(repositoryRoot, "SAB_2023.addin");
                string addin2024Path = Path.Combine(repositoryRoot, "SAB_2024.addin");

                if (!IOFile.Exists(addin2023Path))
                {
                    throw new FileNotFoundException("SAB_2023.addin was not found.", addin2023Path);
                }

                if (!IOFile.Exists(addin2024Path))
                {
                    throw new FileNotFoundException("SAB_2024.addin was not found.", addin2024Path);
                }

                Directory.CreateDirectory(outputFolder);

                Version installerVersion = ResolveInstallerVersion(assemblyPath, explicitVersion);

                BuildInstallerForYear(
                    year: "2023",
                    addinPath: addin2023Path,
                    repositoryRoot: repositoryRoot,
                    binFolder: binFolder,
                    outputFolder: outputFolder,
                    upgradeCode: new Guid(UpgradeCode2023),
                    version: installerVersion);

                BuildInstallerForYear(
                    year: "2024",
                    addinPath: addin2024Path,
                    repositoryRoot: repositoryRoot,
                    binFolder: binFolder,
                    outputFolder: outputFolder,
                    upgradeCode: new Guid(UpgradeCode2024),
                    version: installerVersion);

                Console.WriteLine("MSI build completed:");
                Console.WriteLine(outputFolder);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Installer build failed:");
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        // Блок сборки MSI для одной версии Revit.
        private static void BuildInstallerForYear(
            string year,
            string addinPath,
            string repositoryRoot,
            string binFolder,
            string outputFolder,
            Guid upgradeCode,
            Version version)
        {
            List<WixEntity> pluginFiles = new List<WixEntity>();

            // Блок добавления файлов плагина из папки bin.
            // Папка Families for Plugin исключается, потому ниже она упаковывается отдельным безопасным способом.
            AddPluginBinContent(pluginFiles, binFolder);

            // Блок добавления HTML-инструкций в инсталлятор.
            // Инструкции размещаются рядом с DLL: ...\SAB\Docs\PluginInstructions\...
            // Важно: если исходная папка не найдена, сборку прерываем с ошибкой.
            string instructionsSourcePath = ResolvePluginInstructionsSourcePath(repositoryRoot);
            Dir pluginInstructionsContentDirectory = BuildDocumentationDirectory(
                instructionsSourcePath,
                "PluginInstructions");

            Dir pluginInstructionsDirectory = null;
            if (pluginInstructionsContentDirectory != null)
            {
                pluginInstructionsDirectory = new Dir("Docs", pluginInstructionsContentDirectory);
            }

            if (pluginInstructionsDirectory == null)
            {
                throw new DirectoryNotFoundException(
                    "Plugin instructions directory was not found or is empty. " +
                    "Expected Docs\\PluginInstructions near repository root.");
            }

            pluginFiles.Add(pluginInstructionsDirectory);

            string familiesSourcePath = ResolveFamiliesForPluginSourcePath(repositoryRoot, binFolder);
            string familiesInstallerSourcePath = PrepareFamiliesForInstallerSourcePath(
                familiesSourcePath,
                outputFolder);
            Dir familiesForPluginDirectory = BuildContentDirectory(
                familiesInstallerSourcePath,
                FamiliesRootFolderName);

            if (familiesForPluginDirectory == null)
            {
                throw new DirectoryNotFoundException(
                    "Families for Plugin directory was not found or is empty. " +
                    "Expected SAB\\Families for Plugin or bin\\Families for Plugin.");
            }

            pluginFiles.Add(familiesForPluginDirectory);

            Dir pluginFilesDirectory = new Dir(@"%AppDataFolder%\Autodesk\Revit\Addins\" + year + @"\SAB",
                pluginFiles.ToArray());

            WxsFile addinFile = new WxsFile(addinPath);

            Project project = new Project(
                "SAB Revit " + year,
                new Dir(@"%AppDataFolder%\Autodesk\Revit\Addins\" + year, addinFile),
                pluginFilesDirectory);

            // ProductCode должен быть новым на каждый MSI-билд.
            // Это гарантирует корректный сценарий обновления установленного плагина через MajorUpgrade.
            project.GUID = Guid.NewGuid();
            project.UpgradeCode = upgradeCode;
            project.Version = version;
            project.Scope = InstallScope.perUser;
            project.OutDir = outputFolder;
            project.OutFileName = "SAB_Revit_" + year;
            project.Platform = Platform.x64;
            project.ControlPanelInfo.Manufacturer = "SAB";
            project.MajorUpgrade = new MajorUpgrade
            {
                AllowSameVersionUpgrades = true,
                DowngradeErrorMessage = "A newer version of SAB is already installed."
            };

            // Block to keep installer build deterministic for same source structure.
            project.ResolveWildCards();

            Compiler.BuildMsi(project);

            string expectedMsiPath = Path.Combine(outputFolder, project.OutFileName + ".msi");

            if (!IOFile.Exists(expectedMsiPath))
            {
                throw new InvalidOperationException("MSI was not generated: " + expectedMsiPath);
            }
        }

        // Блок выбора фактической папки Docs\PluginInstructions.
        // Поддерживаем два варианта корня репозитория, чтобы сборка работала стабильно в разных окружениях.
        private static string ResolvePluginInstructionsSourcePath(string repositoryRoot)
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(repositoryRoot, "Docs", "PluginInstructions"));
            candidates.Add(Path.Combine(repositoryRoot, "SAB", "Docs", "PluginInstructions"));

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException(
                "Plugin instructions source directory was not found.\nChecked:\n - " +
                string.Join("\n - ", candidates));
        }

        // Блок выбора папки Families for Plugin для установки рядом с DLL.
        private static string ResolveFamiliesForPluginSourcePath(string repositoryRoot, string binFolder)
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(repositoryRoot, "SAB", FamiliesRootFolderName));
            candidates.Add(Path.Combine(binFolder, FamiliesRootFolderName));

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate) && DirectoryHasFiles(candidate))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException(
                "Families for Plugin source directory was not found.\nChecked:\n - " +
                string.Join("\n - ", candidates));
        }

        // Блок добавления содержимого bin без повторного включения папки семейств.
        private static void AddPluginBinContent(List<WixEntity> pluginFiles, string binFolder)
        {
            if (pluginFiles == null || string.IsNullOrWhiteSpace(binFolder) || !Directory.Exists(binFolder))
            {
                return;
            }

            string[] files = Directory.GetFiles(binFolder);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                pluginFiles.Add(CreateInstallerFile(filePath, fileName));
            }

            string[] directories = Directory.GetDirectories(binFolder);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            foreach (string directoryPath in directories)
            {
                string directoryName = Path.GetFileName(directoryPath);
                if (string.Equals(directoryName, FamiliesRootFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dir childDirectory = BuildContentDirectory(directoryPath, directoryName);
                if (childDirectory != null)
                {
                    pluginFiles.Add(childDirectory);
                }
            }
        }

        // Блок подготовки семейств к упаковке: MSI получает только ASCII-имена файлов.
        // Оригинальные русские имена сохраняются в UTF-8 манифесте и восстанавливаются перед загрузкой в Revit.
        private static string PrepareFamiliesForInstallerSourcePath(string familiesSourcePath, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(familiesSourcePath) || !Directory.Exists(familiesSourcePath))
            {
                throw new DirectoryNotFoundException("Families source directory was not found: " + familiesSourcePath);
            }

            string stagingRootPath = Path.Combine(outputFolder, FamiliesInstallerStagingFolderName);
            if (Directory.Exists(stagingRootPath))
            {
                Directory.Delete(stagingRootPath, true);
            }

            Directory.CreateDirectory(stagingRootPath);

            List<string> manifestLines = new List<string>();
            manifestLines.Add("# packageRelativePath\toriginalRelativePath");

            HashSet<string> usedPackageRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CopyFamiliesDirectoryForInstaller(
                familiesSourcePath,
                stagingRootPath,
                string.Empty,
                manifestLines,
                usedPackageRelativePaths);

            string manifestPath = Path.Combine(stagingRootPath, FamiliesManifestFileName);
            IOFile.WriteAllLines(manifestPath, manifestLines.ToArray(), new UTF8Encoding(true));

            return stagingRootPath;
        }

        private static void CopyFamiliesDirectoryForInstaller(
            string sourceDirectoryPath,
            string stagingRootPath,
            string relativeDirectoryPath,
            List<string> manifestLines,
            HashSet<string> usedPackageRelativePaths)
        {
            string targetDirectoryPath = string.IsNullOrWhiteSpace(relativeDirectoryPath)
                ? stagingRootPath
                : Path.Combine(stagingRootPath, relativeDirectoryPath);

            Directory.CreateDirectory(targetDirectoryPath);

            string[] files = Directory.GetFiles(sourceDirectoryPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in files)
            {
                string originalFileName = Path.GetFileName(filePath);
                string originalRelativePath = CombineRelativePath(relativeDirectoryPath, originalFileName);
                string packageFileName = originalFileName;

                if (string.Equals(Path.GetExtension(filePath), ".rfa", StringComparison.OrdinalIgnoreCase))
                {
                    packageFileName = BuildSafeFamilyPackageFileName(
                        originalRelativePath,
                        relativeDirectoryPath,
                        usedPackageRelativePaths);
                }

                string packageRelativePath = CombineRelativePath(relativeDirectoryPath, packageFileName);
                string targetFilePath = Path.Combine(targetDirectoryPath, packageFileName);

                IOFile.Copy(filePath, targetFilePath, true);

                if (string.Equals(Path.GetExtension(filePath), ".rfa", StringComparison.OrdinalIgnoreCase))
                {
                    manifestLines.Add(packageRelativePath + "\t" + originalRelativePath);
                }
            }

            string[] directories = Directory.GetDirectories(sourceDirectoryPath);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            foreach (string directoryPath in directories)
            {
                string directoryName = Path.GetFileName(directoryPath);
                string childRelativeDirectoryPath = CombineRelativePath(relativeDirectoryPath, directoryName);

                CopyFamiliesDirectoryForInstaller(
                    directoryPath,
                    stagingRootPath,
                    childRelativeDirectoryPath,
                    manifestLines,
                    usedPackageRelativePaths);
            }
        }

        private static string BuildSafeFamilyPackageFileName(
            string originalRelativePath,
            string relativeDirectoryPath,
            HashSet<string> usedPackageRelativePaths)
        {
            string extension = Path.GetExtension(originalRelativePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".rfa";
            }

            string baseName = "family_" + CreateShortHash(originalRelativePath, 8);
            string packageFileName = baseName + extension.ToLowerInvariant();
            string packageRelativePath = CombineRelativePath(relativeDirectoryPath, packageFileName);

            int duplicateIndex = 2;
            while (usedPackageRelativePaths.Contains(packageRelativePath))
            {
                packageFileName = baseName + "_" + duplicateIndex + extension.ToLowerInvariant();
                packageRelativePath = CombineRelativePath(relativeDirectoryPath, packageFileName);
                duplicateIndex++;
            }

            usedPackageRelativePaths.Add(packageRelativePath);
            return packageFileName;
        }

        // Блок рекурсивной сборки директории документации для включения в MSI.
        private static Dir BuildDocumentationDirectory(string sourceDirectoryPath, string targetDirectoryName)
        {
            return BuildContentDirectory(sourceDirectoryPath, targetDirectoryName);
        }

        // Блок рекурсивной сборки любой контентной папки для включения в MSI.
        private static Dir BuildContentDirectory(string sourceDirectoryPath, string targetDirectoryName)
        {
            return BuildContentDirectory(sourceDirectoryPath, targetDirectoryName, targetDirectoryName);
        }

        private static Dir BuildContentDirectory(
            string sourceDirectoryPath,
            string targetDirectoryName,
            string relativeTargetDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
            {
                return null;
            }

            List<WixEntity> childEntities = new List<WixEntity>();

            // Добавляем файлы текущего уровня.
            string[] files = Directory.GetFiles(sourceDirectoryPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string relativeTargetPath = CombineRelativePath(relativeTargetDirectoryPath, fileName);
                childEntities.Add(CreateInstallerFile(filePath, relativeTargetPath));
            }

            // Рекурсивно добавляем подпапки.
            string[] directories = Directory.GetDirectories(sourceDirectoryPath);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            foreach (string directoryPath in directories)
            {
                string directoryName = Path.GetFileName(directoryPath);
                string childRelativeTargetDirectoryPath = CombineRelativePath(relativeTargetDirectoryPath, directoryName);
                Dir childDirectory = BuildContentDirectory(
                    directoryPath,
                    directoryName,
                    childRelativeTargetDirectoryPath);

                if (childDirectory != null)
                {
                    childEntities.Add(childDirectory);
                }
            }

            if (childEntities.Count == 0)
            {
                return null;
            }

            return new Dir(
                new Id(CreateWixIdentifier("Dir", relativeTargetDirectoryPath)),
                targetDirectoryName,
                childEntities.ToArray());
        }

        private static WxsFile CreateInstallerFile(string sourceFilePath, string relativeTargetPath)
        {
            return new WxsFile(
                new Id(CreateWixIdentifier("File", relativeTargetPath)),
                sourceFilePath);
        }

        private static string CombineRelativePath(string parentRelativePath, string childName)
        {
            if (string.IsNullOrWhiteSpace(parentRelativePath))
            {
                return childName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(childName))
            {
                return parentRelativePath;
            }

            return parentRelativePath + "\\" + childName;
        }

        private static string CreateWixIdentifier(string prefix, string identity)
        {
            string text = identity ?? string.Empty;
            StringBuilder safeTextBuilder = new StringBuilder();

            for (int index = 0; index < text.Length; index++)
            {
                char currentChar = text[index];
                bool isAsciiLetter =
                    (currentChar >= 'A' && currentChar <= 'Z') ||
                    (currentChar >= 'a' && currentChar <= 'z');
                bool isAsciiDigit = currentChar >= '0' && currentChar <= '9';

                if (isAsciiLetter || isAsciiDigit)
                {
                    safeTextBuilder.Append(currentChar);
                }
                else
                {
                    safeTextBuilder.Append('_');
                }
            }

            string safeText = safeTextBuilder.ToString().Trim('_');
            if (string.IsNullOrWhiteSpace(safeText))
            {
                safeText = "Item";
            }

            if (safeText.Length > 42)
            {
                safeText = safeText.Substring(0, 42);
            }

            return prefix + "_" + safeText + "_" + CreateShortHash(text, 8);
        }

        private static string CreateShortHash(string text, int bytesCount)
        {
            if (bytesCount <= 0)
            {
                bytesCount = 8;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] sourceBytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                byte[] hashBytes = sha256.ComputeHash(sourceBytes);
                int length = Math.Min(bytesCount, hashBytes.Length);

                StringBuilder hashBuilder = new StringBuilder(length * 2);
                for (int index = 0; index < length; index++)
                {
                    hashBuilder.Append(hashBytes[index].ToString("x2"));
                }

                return hashBuilder.ToString();
            }
        }

        private static bool DirectoryHasFiles(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            if (Directory.GetFiles(directoryPath).Length > 0)
            {
                return true;
            }

            string[] directories = Directory.GetDirectories(directoryPath);
            foreach (string childDirectory in directories)
            {
                if (DirectoryHasFiles(childDirectory))
                {
                    return true;
                }
            }

            return false;
        }

        private static Version ResolveInstallerVersion(string assemblyPath, string explicitVersion)
        {
            if (!string.IsNullOrWhiteSpace(explicitVersion))
            {
                Version parsedExplicitVersion;
                if (!Version.TryParse(explicitVersion, out parsedExplicitVersion))
                {
                    throw new InvalidOperationException("Invalid --version value: " + explicitVersion + ". Expected format: Major.Minor.Build");
                }

                int explicitBuild = parsedExplicitVersion.Build >= 0 ? parsedExplicitVersion.Build : 0;
                return new Version(parsedExplicitVersion.Major, parsedExplicitVersion.Minor, explicitBuild);
            }

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
            string rawVersion = versionInfo.FileVersion;

            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return new Version(1, 0, 0);
            }

            Version parsedVersion;
            if (!Version.TryParse(rawVersion, out parsedVersion))
            {
                return new Version(1, 0, 0);
            }

            int build = parsedVersion.Build >= 0 ? parsedVersion.Build : 0;
            return new Version(parsedVersion.Major, parsedVersion.Minor, build);
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (args == null)
            {
                return result;
            }

            for (int index = 0; index < args.Length; index++)
            {
                string key = args[index];

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (index + 1 < args.Length)
                {
                    string value = args[index + 1];

                    if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                    {
                        result[key] = value;
                        index++;
                        continue;
                    }
                }

                result[key] = string.Empty;
            }

            return result;
        }

        private static string GetOption(Dictionary<string, string> options, string key, string defaultValue)
        {
            string value;
            if (options != null && options.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return Path.GetFullPath(value);
            }

            return defaultValue;
        }

        private static string GetOptionRaw(Dictionary<string, string> options, string key, string defaultValue)
        {
            string value;
            if (options != null && options.TryGetValue(key, out value))
            {
                return value ?? string.Empty;
            }

            return defaultValue;
        }

    }
}
