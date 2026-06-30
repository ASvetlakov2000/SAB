using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.Services.PluginResources
{
    public class PluginFamilyResourceService
    {
        public const string FamiliesRootFolderName = "Families for Plugin";
        private const string FamiliesManifestFileName = "families.manifest.tsv";
        private const string FamilyCacheRootFolderName = "SAB";
        private const string FamilyCacheFolderName = "FamilyCache";

        public IList<string> GetCommandFamilyFilePaths(Type commandType)
        {
            return GetCommandFamilyFilePaths(commandType, null);
        }

        private IList<string> GetCommandFamilyFilePaths(Type commandType, IList<string> warnings)
        {
            List<string> familyFilePaths = new List<string>();

            if (commandType == null)
            {
                return familyFilePaths;
            }

            string familiesRootFolderPath = GetFamiliesRootFolderPath();
            if (string.IsNullOrWhiteSpace(familiesRootFolderPath) || !Directory.Exists(familiesRootFolderPath))
            {
                return familyFilePaths;
            }

            string manifestPath = Path.Combine(familiesRootFolderPath, FamiliesManifestFileName);
            if (File.Exists(manifestPath))
            {
                AddManifestFamilyFilePaths(
                    familiesRootFolderPath,
                    manifestPath,
                    commandType,
                    familyFilePaths,
                    warnings);

                familyFilePaths.Sort(StringComparer.OrdinalIgnoreCase);
                return familyFilePaths;
            }

            string commandFolderPath = GetCommandFamiliesFolderPath(commandType);
            if (string.IsNullOrWhiteSpace(commandFolderPath) || !Directory.Exists(commandFolderPath))
            {
                return familyFilePaths;
            }

            string[] files = Directory.GetFiles(commandFolderPath, "*.rfa", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < files.Length; index++)
            {
                familyFilePaths.Add(files[index]);
            }

            familyFilePaths.Sort(StringComparer.OrdinalIgnoreCase);
            return familyFilePaths;
        }

        public string GetCommandFamiliesFolderPath(Type commandType)
        {
            if (commandType == null)
            {
                return string.Empty;
            }

            string familiesRootFolderPath = GetFamiliesRootFolderPath();
            if (string.IsNullOrWhiteSpace(familiesRootFolderPath))
            {
                return string.Empty;
            }

            return Path.Combine(familiesRootFolderPath, commandType.Name);
        }

        public void EnsureCommandFamiliesLoaded(Document document, Type commandType, IList<string> warnings)
        {
            List<string> debugMessages = new List<string>();
            int warningsCountBefore = warnings != null ? warnings.Count : 0;

            AddDebug(debugMessages, "Старт загрузки семейств плагина.");
            AddDebug(debugMessages, "Класс команды: " + (commandType != null ? commandType.FullName : "<null>"));

            if (document == null || commandType == null)
            {
                AddWarning(warnings, "Не удалось загрузить семейства плагина: документ или класс команды недоступен.");
                ShowDebugDialog(debugMessages, warningsCountBefore, warnings);
                return;
            }

            if (document.ActiveView == null || !document.ActiveView.IsValidObject)
            {
                AddWarning(warnings, "Не удалось загрузить семейства плагина: активный вид Revit недоступен.");
                ShowDebugDialog(debugMessages, warningsCountBefore, warnings);
                return;
            }

            AddDebug(debugMessages, "Папка семейств: " + GetCommandFamiliesFolderPath(commandType));
            AddDebug(debugMessages, "Манифест: " + Path.Combine(GetFamiliesRootFolderPath(), FamiliesManifestFileName));

            IList<string> familyFilePaths = GetCommandFamilyFilePaths(commandType, warnings);
            AddDebug(debugMessages, "Файлов к загрузке: " + familyFilePaths.Count);

            if (familyFilePaths.Count == 0)
            {
                AddWarning(
                    warnings,
                    "Не найдены встроенные семейства для команды " + commandType.Name + ". Проверьте папку " +
                    FamiliesRootFolderName + ".");
                ShowDebugDialog(debugMessages, warningsCountBefore, warnings);
                return;
            }

            Transaction transaction = new Transaction(document, "SAB - загрузить семейства команды");
            try
            {
                transaction.Start();

                for (int index = 0; index < familyFilePaths.Count; index++)
                {
                    TryLoadFamilyIfNeeded(document, familyFilePaths[index], warnings, debugMessages);
                }

                transaction.Commit();
                ShowDebugDialog(debugMessages, warningsCountBefore, warnings);
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                AddWarning(warnings, "Не удалось загрузить встроенные семейства команды: " + exception.Message);
                AddDebug(debugMessages, "Исключение: " + exception);
                ShowDebugDialog(debugMessages, warningsCountBefore, warnings);
            }
        }

        private void AddManifestFamilyFilePaths(
            string familiesRootFolderPath,
            string manifestPath,
            Type commandType,
            IList<string> familyFilePaths,
            IList<string> warnings)
        {
            try
            {
                string[] lines = File.ReadAllLines(manifestPath, Encoding.UTF8);

                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] parts = line.Split(new char[] { '\t' }, 2);
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    string packageRelativePath = NormalizeManifestRelativePath(parts[0]);
                    string originalRelativePath = NormalizeManifestRelativePath(parts[1]);

                    if (!IsCommandFamilyRelativePath(originalRelativePath, commandType.Name))
                    {
                        continue;
                    }

                    string installedFamilyFilePath = Path.Combine(
                        familiesRootFolderPath,
                        packageRelativePath.Replace('\\', Path.DirectorySeparatorChar));
                    string originalFileName = Path.GetFileName(originalRelativePath);

                    string preparedFamilyFilePath = PrepareFamilyFileForLoading(
                        installedFamilyFilePath,
                        commandType.Name,
                        originalFileName,
                        warnings);

                    if (!string.IsNullOrWhiteSpace(preparedFamilyFilePath))
                    {
                        familyFilePaths.Add(preparedFamilyFilePath);
                    }
                }
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось прочитать манифест семейств: " + exception.Message);
            }
        }

        private string PrepareFamilyFileForLoading(
            string installedFamilyFilePath,
            string commandFolderName,
            string originalFileName,
            IList<string> warnings)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(installedFamilyFilePath) || !File.Exists(installedFamilyFilePath))
                {
                    AddWarning(warnings, "Файл семейства из манифеста не найден: " + installedFamilyFilePath);
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(originalFileName))
                {
                    AddWarning(warnings, "В манифесте семейств указано пустое имя исходного файла.");
                    return string.Empty;
                }

                string safeOriginalFileName = Path.GetFileName(originalFileName);
                string cacheFolderPath = GetFamilyCacheFolderPath(commandFolderName);
                Directory.CreateDirectory(cacheFolderPath);

                string cachedFamilyFilePath = Path.Combine(cacheFolderPath, safeOriginalFileName);
                CopyFamilyFileIfNeeded(installedFamilyFilePath, cachedFamilyFilePath);

                return cachedFamilyFilePath;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось подготовить семейство к загрузке: " + exception.Message);
                return string.Empty;
            }
        }

        private void CopyFamilyFileIfNeeded(string sourceFilePath, string targetFilePath)
        {
            bool shouldCopy = !File.Exists(targetFilePath);

            if (!shouldCopy)
            {
                FileInfo sourceInfo = new FileInfo(sourceFilePath);
                FileInfo targetInfo = new FileInfo(targetFilePath);

                if (sourceInfo.Length != targetInfo.Length ||
                    sourceInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc)
                {
                    shouldCopy = true;
                }
            }

            if (!shouldCopy)
            {
                return;
            }

            File.Copy(sourceFilePath, targetFilePath, true);
            File.SetLastWriteTimeUtc(targetFilePath, File.GetLastWriteTimeUtc(sourceFilePath));
        }

        private void TryLoadFamilyIfNeeded(
            Document document,
            string familyFilePath,
            IList<string> warnings,
            IList<string> debugMessages)
        {
            AddDebug(debugMessages, "Проверка файла: " + familyFilePath);

            if (document == null || string.IsNullOrWhiteSpace(familyFilePath) || !File.Exists(familyFilePath))
            {
                AddWarning(warnings, "Файл семейства не найден: " + familyFilePath);
                AddDebug(debugMessages, "Файл не найден.");
                return;
            }

            string familyName = Path.GetFileNameWithoutExtension(familyFilePath);
            AddDebug(debugMessages, "Ожидаемое имя семейства: " + familyName);

            if (FamilyExists(document, familyName))
            {
                AddDebug(debugMessages, "Семейство уже есть в документе.");
                return;
            }

            Family loadedFamily;
            bool loaded = document.LoadFamily(familyFilePath, out loadedFamily);
            AddDebug(debugMessages, "LoadFamily: " + loaded);
            AddDebug(debugMessages, "Загруженное семейство: " + (loadedFamily != null ? loadedFamily.Name : "<null>"));

            if (!loaded || loadedFamily == null)
            {
                AddWarning(warnings, "Семейство не было загружено: " + familyName);
            }
        }

        private bool FamilyExists(Document document, string familyName)
        {
            if (document == null || string.IsNullOrWhiteSpace(familyName))
            {
                return false;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(Family));
            foreach (Element element in collector)
            {
                Family family = element as Family;
                if (family == null)
                {
                    continue;
                }

                if (string.Equals(family.Name, familyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetFamiliesRootFolderPath()
        {
            string assemblyFolderPath = GetAssemblyFolderPath();
            if (string.IsNullOrWhiteSpace(assemblyFolderPath))
            {
                return string.Empty;
            }

            return Path.Combine(assemblyFolderPath, FamiliesRootFolderName);
        }

        private string GetAssemblyFolderPath()
        {
            Assembly assembly = typeof(PluginFamilyResourceService).Assembly;
            if (assembly == null || string.IsNullOrWhiteSpace(assembly.Location))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(assembly.Location);
        }

        private string GetFamilyCacheFolderPath(string commandFolderName)
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppDataPath))
            {
                localAppDataPath = Path.GetTempPath();
            }

            string installationHash = CreateShortHash(GetAssemblyFolderPath(), 8);

            return Path.Combine(
                localAppDataPath,
                FamilyCacheRootFolderName,
                FamilyCacheFolderName,
                installationHash,
                commandFolderName ?? string.Empty);
        }

        private bool IsCommandFamilyRelativePath(string relativePath, string commandFolderName)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(commandFolderName))
            {
                return false;
            }

            string normalizedRelativePath = NormalizeManifestRelativePath(relativePath);
            int separatorIndex = normalizedRelativePath.IndexOf('\\');
            if (separatorIndex <= 0)
            {
                return false;
            }

            string firstSegment = normalizedRelativePath.Substring(0, separatorIndex);
            return string.Equals(firstSegment, commandFolderName, StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeManifestRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            return relativePath.Trim().Replace('/', '\\');
        }

        private string CreateShortHash(string text, int bytesCount)
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

        private void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            warnings.Add(warning);
        }

        private void AddDebug(IList<string> debugMessages, string message)
        {
            if (debugMessages == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            debugMessages.Add(message);
        }

        private void ShowDebugDialog(
            IList<string> debugMessages,
            int warningsCountBefore,
            IList<string> warnings)
        {
            int warningsCountAfter = warnings != null ? warnings.Count : warningsCountBefore;
            if (warningsCountAfter <= warningsCountBefore)
            {
                return;
            }

            StringBuilder messageBuilder = new StringBuilder();

            if (debugMessages != null)
            {
                for (int index = 0; index < debugMessages.Count; index++)
                {
                    messageBuilder.AppendLine(debugMessages[index]);
                }
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Предупреждения:");

            if (warnings != null)
            {
                for (int index = warningsCountBefore; index < warnings.Count; index++)
                {
                    messageBuilder.AppendLine(warnings[index]);
                }
            }

            string message = messageBuilder.ToString();
            if (message.Length > 3500)
            {
                message = message.Substring(0, 3500) + Environment.NewLine + "...";
            }

            TaskDialog.Show("SAB Debug - загрузка семейств", message);
        }
    }
}
