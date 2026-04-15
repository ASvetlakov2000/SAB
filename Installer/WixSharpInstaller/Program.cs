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
        // Блок констант для предсказуемых идентификаторов обновления MSI.
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

                Version installerVersion = ResolveInstallerVersion(assemblyPath);

                BuildInstallerForYear(
                    year: "2023",
                    addinPath: addin2023Path,
                    binFolder: binFolder,
                    outputFolder: outputFolder,
                    upgradeCode: new Guid(UpgradeCode2023),
                    version: installerVersion);

                BuildInstallerForYear(
                    year: "2024",
                    addinPath: addin2024Path,
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
            string binFolder,
            string outputFolder,
            Guid upgradeCode,
            Version version)
        {
            Dir pluginFilesDirectory = new Dir(@"%AppDataFolder%\Autodesk\Revit\Addins\" + year + @"\SAB",
                new Files(Path.Combine(binFolder, "*.*")));

            WxsFile addinFile = new WxsFile(addinPath);

            Project project = new Project(
                "SAB Revit " + year,
                new Dir(@"%AppDataFolder%\Autodesk\Revit\Addins\" + year, addinFile),
                pluginFilesDirectory);

            project.GUID = BuildProductGuid(year);
            project.UpgradeCode = upgradeCode;
            project.Version = version;
            project.Scope = InstallScope.perUser;
            project.OutDir = outputFolder;
            project.OutFileName = "SAB_Revit_" + year;
            project.Platform = Platform.x64;
            project.ControlPanelInfo.Manufacturer = "SAB";
            project.MajorUpgrade = new MajorUpgrade
            {
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

        private static Guid BuildProductGuid(string year)
        {
            if (year == "2023")
            {
                return new Guid("C6D697EC-297E-481E-BEFC-7B4D9432131A");
            }

            if (year == "2024")
            {
                return new Guid("2F26B9D4-70A0-41A4-95A3-0BBAC35A16C8");
            }

            throw new InvalidOperationException("Unsupported installer year: " + year);
        }

        private static Version ResolveInstallerVersion(string assemblyPath)
        {
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
    }
}
