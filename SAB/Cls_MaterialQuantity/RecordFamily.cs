using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;

namespace SAB.MaterialQuantity
{
    internal static class RecordFields
    {
        internal const string FamilyName = "SAB_РасчетныйСлой";
        internal const string Marker = "SAB_РасчетнаяЗапись";
        internal const string Key = "SAB_КлючСлоя";
        internal const string SourceId = "SAB_ИсточникUniqueId";
        internal const string SourceCategory = "SAB_КатегорияИсточника";
        internal const string SourceType = "SAB_ТипИсточника";
        internal const string LayerIndex = "SAB_НомерСлоя";
        internal const string LegacyMaterialName = "SAB_Материал";
        internal const string MaterialName = "SAB_Материал_Имя";
        internal const string MaterialDescription = "SAB_Материал_Описание";
        internal const string MaterialModel = "SAB_Материал_Модель";
        internal const string Thickness = "SAB_ТолщинаСлоя";
        internal const string MaterialVolume = "SAB_ОбъемМатериала";
        internal const string Unit = "SAB_Единица";
        internal const string Quantity = "SAB_Количество";
        internal const string Status = "SAB_СтатусРасчета";

        internal const string Ready = "Предварительно V/t";
        internal const string Obsolete = "Устарело";
        internal const string MarkerValue = "МатериалСлоя";
    }

    internal static class RecordFamily
    {
        private sealed class FieldDefinition
        {
            internal string Name;
            internal Guid Id;
            internal FieldDefinition(string name, string id)
            {
                Name = name;
                Id = new Guid(id);
            }
        }

        private static readonly FieldDefinition[] Fields =
        {
            new FieldDefinition(RecordFields.Marker, "bf6aacb2-b34a-4f5c-a4c1-670ecb16d560"),
            new FieldDefinition(RecordFields.Key, "802bc582-99bd-4c56-a365-fe2e7a17ce54"),
            new FieldDefinition(RecordFields.SourceId, "8e0188cf-25ec-49d5-a2e4-f87322029b12"),
            new FieldDefinition(RecordFields.SourceCategory, "962e1085-f77e-4bfa-bb29-803d0127f6f0"),
            new FieldDefinition(RecordFields.SourceType, "09b0fa0a-054b-4d1d-b6c0-46606e8de405"),
            new FieldDefinition(RecordFields.LayerIndex, "15d9778c-475b-4e34-8253-af537946abde"),
            new FieldDefinition(RecordFields.LegacyMaterialName, "22461a5d-fcc3-432a-a637-dbe3253da20e"),
            new FieldDefinition(RecordFields.MaterialName, "886cfdbd-306c-4b68-8af9-46a302ae23d4"),
            new FieldDefinition(RecordFields.MaterialDescription, "18611a4e-01b3-4f0f-a36e-e6910d78e8fa"),
            new FieldDefinition(RecordFields.MaterialModel, "aa99a986-6778-4d42-a821-c7d0c7a8b7b9"),
            new FieldDefinition(RecordFields.Thickness, "53db72f5-bea6-43de-8a25-0288616bdd10"),
            new FieldDefinition(RecordFields.MaterialVolume, "d46e16ae-22e8-4896-9530-43f75ffbc06c"),
            new FieldDefinition(RecordFields.Unit, "a1c455f4-6df6-469e-9a4f-12d51d75aaf6"),
            new FieldDefinition(RecordFields.Quantity, "7646d028-8aaa-416c-bdd2-b00c77919b51"),
            new FieldDefinition(RecordFields.Status, "e67008d5-088b-4dd4-baa8-9d861b051a6d")
        };

        // Prepare the family outside the project transaction: Revit does not need
        // two editable documents open in nested transactions.
        internal static string Prepare(Application application, Document project)
        {
            Family existing = Find(project);
            if (existing != null && (existing.FamilyCategory == null ||
                RevitElementId.Key(existing.FamilyCategory.Id) != (long)BuiltInCategory.OST_GenericModel))
                throw new InvalidOperationException("Имя служебного семейства занято семейством другой категории.");
            return PrepareFamily(application, project, existing);
        }

        // Call inside a transaction on the project document.
        internal static FamilySymbol EnsureLoaded(Document project, string preparedPath)
        {
            Family family = Find(project);

            if (!string.IsNullOrWhiteSpace(preparedPath))
            {
                if (!project.LoadFamily(preparedPath, new PreserveValuesLoadOptions(), out family) || family == null)
                    throw new InvalidOperationException("Не удалось загрузить семейство расчётной записи.");
                project.Regenerate();
            }
            if (family == null)
                throw new InvalidOperationException("Не найдено подготовленное семейство расчётной записи.");

            if (family.FamilyCategory == null ||
                RevitElementId.Key(family.FamilyCategory.Id) != (long)BuiltInCategory.OST_GenericModel)
                throw new InvalidOperationException("Имя служебного семейства занято семейством другой категории.");

            ElementId symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            var symbol = project.GetElement(symbolId) as FamilySymbol;
            if (symbol == null)
                throw new InvalidOperationException("В служебном семействе не найден типоразмер.");
            return symbol;
        }

        private static Family Find(Document project)
        {
            return new FilteredElementCollector(project).OfClass(typeof(Family))
                .Cast<Family>().FirstOrDefault(item => item.Name == RecordFields.FamilyName);
        }

        private sealed class PreserveValuesLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
                out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return true;
            }
        }

        private static string PrepareFamily(Application application, Document project, Family existing)
        {
            string cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB", "MaterialQuantity", application.VersionNumber);
            Directory.CreateDirectory(cache);
            string rfaPath = Path.Combine(cache, RecordFields.FamilyName + ".rfa");

            Document familyDocument = existing == null
                ? OpenCachedFamilyOrTemplate(application, rfaPath)
                : project.EditFamily(existing);
            if (familyDocument == null)
                throw new InvalidOperationException("Revit не открыл документ расчётного семейства.");

            string previousSharedParametersFile = application.SharedParametersFilename;
            try
            {
                var existingParameters = familyDocument.FamilyManager.Parameters
                    .Cast<FamilyParameter>()
                    .ToDictionary(parameter => parameter.Definition.Name, StringComparer.Ordinal);
                var missing = new List<FieldDefinition>();
                foreach (FieldDefinition field in Fields)
                {
                    FamilyParameter parameter;
                    if (!existingParameters.TryGetValue(field.Name, out parameter))
                        missing.Add(field);
                    else if (!parameter.IsShared || parameter.GUID != field.Id || !parameter.IsInstance)
                        throw new InvalidOperationException("Несовместимый параметр расчётного семейства: " + field.Name);
                }
                if (existing != null && missing.Count == 0) return null;
                if (existing == null && missing.Count == 0) return rfaPath;

                DiagnosticLog.Write(application.VersionNumber, "Family parameters to add: " + missing.Count);
                IDictionary<string, ExternalDefinition> definitions = OpenDefinitions(application);
                DiagnosticLog.Write(application.VersionNumber, "Shared definitions ready");
                using (var transaction = new Transaction(familyDocument, "Параметры расчётного слоя"))
                {
                    transaction.Start();
                    foreach (FieldDefinition field in missing)
                    {
                        DiagnosticLog.Write(application.VersionNumber, "Adding family parameter: " + field.Name);
#if REVIT2024
                        familyDocument.FamilyManager.AddParameter(definitions[field.Name], GroupTypeId.Data, true);
#else
                        familyDocument.FamilyManager.AddParameter(definitions[field.Name], BuiltInParameterGroup.PG_DATA, true);
#endif
                    }
                    DiagnosticLog.Write(application.VersionNumber, "Committing family parameters");
                    transaction.Commit();
                }
                DiagnosticLog.Write(application.VersionNumber, "Saving family: " + rfaPath);
                familyDocument.SaveAs(rfaPath, new SaveAsOptions { OverwriteExistingFile = true });
                DiagnosticLog.Write(application.VersionNumber, "Family saved");
            }
            finally
            {
                application.SharedParametersFilename = previousSharedParametersFile;
                familyDocument.Close(false);
            }
            return rfaPath;
        }

        private static Document OpenCachedFamilyOrTemplate(Application application, string path)
        {
            // Reusing a family saved in this Revit version avoids repeatedly
            // upgrading the legacy Generic Model template during a UI command.
            if (File.Exists(path))
            {
                bool currentFormat;
                using (BasicFileInfo info = BasicFileInfo.Extract(path))
                    currentFormat = info.Format == application.VersionNumber;
                if (currentFormat)
                {
                    Document cached = application.OpenDocumentFile(path);
                    if (!cached.IsFamilyDocument || cached.OwnerFamily.FamilyCategory == null ||
                        RevitElementId.Key(cached.OwnerFamily.FamilyCategory.Id) !=
                        (long)BuiltInCategory.OST_GenericModel)
                    {
                        cached.Close(false);
                        throw new InvalidOperationException("Кэш расчётного семейства имеет неверную категорию.");
                    }
                    DiagnosticLog.Write(application.VersionNumber, "Using current-version cached family: " + path);
                    return cached;
                }
            }
            return application.NewFamilyDocument(FindTemplate(application));
        }

        private static IDictionary<string, ExternalDefinition> OpenDefinitions(Application application)
        {
            string path = SabSharedParameterFile.GetPath();
            application.SharedParametersFilename = path;
            DefinitionFile file = application.OpenSharedParameterFile();
            if (file == null)
                throw new InvalidOperationException("Не удалось открыть файл общих параметров: " + path);
            DefinitionGroup group = file.Groups.get_Item("SAB_РасчетМатериалов");
            if (group == null)
                throw new InvalidOperationException("В едином файле отсутствует группа SAB_РасчетМатериалов.");
            var result = new Dictionary<string, ExternalDefinition>(StringComparer.Ordinal);
            foreach (FieldDefinition field in Fields)
            {
                ExternalDefinition definition = group.Definitions.get_Item(field.Name) as ExternalDefinition;
                if (definition == null || definition.GUID != field.Id)
                    throw new InvalidOperationException("Несовместимый общий параметр: " + field.Name);
                result.Add(field.Name, definition);
            }
            return result;
        }

        private static string FindTemplate(Application application)
        {
            var roots = new[]
            {
                application.FamilyTemplatePath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Autodesk", "RVT " + application.VersionNumber, "Family Templates")
            };
            foreach (string root in roots.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)))
            {
                foreach (string name in new[]
                {
                    "Metric_Generic_Model-RUS.rft",
                    "Metric_Generic_Model-ENU.rft",
                    "Metric Generic Model.rft",
                    "Generic Model.rft"
                })
                {
                    string found = Directory.GetFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null) return found;
                }
            }
            throw new FileNotFoundException("Не найден шаблон Generic Model.rft для Revit " + application.VersionNumber);
        }
    }
}
