using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            pluginFiles.Add(new Files(Path.Combine(binFolder, "*.*")));

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
            Dir familiesForPluginDirectory = BuildContentDirectory(
                familiesSourcePath,
                "Families for Plugin");

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
            candidates.Add(Path.Combine(binFolder, "Families for Plugin"));
            candidates.Add(Path.Combine(repositoryRoot, "SAB", "Families for Plugin"));

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

        // Блок рекурсивной сборки директории документации для включения в MSI.
        private static Dir BuildDocumentationDirectory(string sourceDirectoryPath, string targetDirectoryName)
        {
            return BuildContentDirectory(sourceDirectoryPath, targetDirectoryName);
        }

        // Блок рекурсивной сборки любой контентной папки для включения в MSI.
        private static Dir BuildContentDirectory(string sourceDirectoryPath, string targetDirectoryName)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath) || !Directory.Exists(sourceDirectoryPath))
            {
                return null;
            }

            List<WixEntity> childEntities = new List<WixEntity>();

            // Добавляем файлы текущего уровня.
            string[] files = Directory.GetFiles(sourceDirectoryPath);
            foreach (string filePath in files)
            {
                childEntities.Add(new WxsFile(filePath));
            }

            // Рекурсивно добавляем подпапки.
            string[] directories = Directory.GetDirectories(sourceDirectoryPath);
            foreach (string directoryPath in directories)
            {
                string directoryName = Path.GetFileName(directoryPath);
                Dir childDirectory = BuildDocumentationDirectory(directoryPath, directoryName);

                if (childDirectory != null)
                {
                    childEntities.Add(childDirectory);
                }
            }

            return new Dir(targetDirectoryName, childEntities.ToArray());
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
