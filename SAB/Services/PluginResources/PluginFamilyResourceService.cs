using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;

namespace SAB.Services.PluginResources
{
    public class PluginFamilyResourceService
    {
        public const string FamiliesRootFolderName = "Families for Plugin";

        public IList<string> GetCommandFamilyFilePaths(Type commandType)
        {
            List<string> familyFilePaths = new List<string>();

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

            string assemblyFolderPath = GetAssemblyFolderPath();
            if (string.IsNullOrWhiteSpace(assemblyFolderPath))
            {
                return string.Empty;
            }

            return Path.Combine(assemblyFolderPath, FamiliesRootFolderName, commandType.Name);
        }

        public void EnsureCommandFamiliesLoaded(Document document, Type commandType, IList<string> warnings)
        {
            if (document == null || commandType == null)
            {
                AddWarning(warnings, "Не удалось загрузить семейства плагина: документ или класс команды недоступен.");
                return;
            }

            IList<string> familyFilePaths = GetCommandFamilyFilePaths(commandType);
            if (familyFilePaths.Count == 0)
            {
                AddWarning(
                    warnings,
                    "Не найдены встроенные семейства для команды " + commandType.Name + ". Проверьте папку " +
                    FamiliesRootFolderName + ".");
                return;
            }

            Transaction transaction = new Transaction(document, "SAB - загрузить семейства команды");
            try
            {
                transaction.Start();

                for (int index = 0; index < familyFilePaths.Count; index++)
                {
                    TryLoadFamilyIfNeeded(document, familyFilePaths[index], warnings);
                }

                transaction.Commit();
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                AddWarning(warnings, "Не удалось загрузить встроенные семейства команды: " + exception.Message);
            }
        }

        private void TryLoadFamilyIfNeeded(Document document, string familyFilePath, IList<string> warnings)
        {
            if (document == null || string.IsNullOrWhiteSpace(familyFilePath) || !File.Exists(familyFilePath))
            {
                AddWarning(warnings, "Файл семейства не найден: " + familyFilePath);
                return;
            }

            string familyName = Path.GetFileNameWithoutExtension(familyFilePath);
            if (FamilyExists(document, familyName))
            {
                return;
            }

            Family loadedFamily;
            bool loaded = document.LoadFamily(familyFilePath, out loadedFamily);
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

        private string GetAssemblyFolderPath()
        {
            Assembly assembly = typeof(PluginFamilyResourceService).Assembly;
            if (assembly == null || string.IsNullOrWhiteSpace(assembly.Location))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(assembly.Location);
        }

        private void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            warnings.Add(warning);
        }
    }
}
