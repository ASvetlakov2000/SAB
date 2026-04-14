using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Revit
{
    /// <summary>
    /// Пакетное переименование и удаление материалов по CSV.
    /// </summary>
    public class MaterialNamingApplyService
    {
        public MaterialNamingApplyResult Apply(Document document, List<MaterialNamingCsvModel> rows)
        {
            MaterialNamingApplyResult result = new MaterialNamingApplyResult();

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            Dictionary<int, ElementId> mappedMaterialIds = ResolveMaterialIdsByRows(document, rows);

            // Блок последовательного применения строк CSV/XLSX без сортировки
            // Здесь нельзя менять порядок строк, так как операции выполняются построчно
            for (int i = 0; i < rows.Count; i++)
            {
                MaterialNamingCsvModel row = rows[i];

                if (row == null)
                {
                    continue;
                }

                ElementId materialId;

                if (!mappedMaterialIds.TryGetValue(i, out materialId) || materialId == ElementId.InvalidElementId)
                {
                    result.Errors.Add(new NamingErrorCsvModel
                    {
                        OldName = BuildOldLabel(row),
                        NewName = BuildNewLabel(row),
                        ErrorText = "Material was not found by old values."
                    });
                    continue;
                }

                Material material = document.GetElement(materialId) as Material;

                if (material == null)
                {
                    result.Errors.Add(new NamingErrorCsvModel
                    {
                        OldName = BuildOldLabel(row),
                        NewName = BuildNewLabel(row),
                        ErrorText = "Material from mapping no longer exists."
                    });
                    continue;
                }

                try
                {
                    bool deleted = false;
                    bool renamed = false;
                    bool descriptionChanged = false;

                    using (Transaction transaction = new Transaction(document, "Apply material naming row " + row.RowIndex))
                    {
                        transaction.Start();

                        // Блок удаления материала:
                        // удаляем только при явном признаке DeleteMaterial=true
                        if (row.DeleteMaterial)
                        {
                            document.Delete(material.Id);
                            deleted = true;
                        }
                        else
                        {
                            renamed = TryRenameMaterial(material, row.MaterialNameNew);
                            descriptionChanged = TryUpdateDescription(material, row.DescriptionNew);

                            TryUpdateTextParameter(material, BuiltInParameter.ALL_MODEL_MANUFACTURER, "Изготовитель", row.Manufacturer);
                            TryUpdateTextParameter(material, BuiltInParameter.ALL_MODEL_MODEL, "Модель", row.Model);
                            TryUpdateTextParameter(material, BuiltInParameter.KEYNOTE_PARAM, "Ключевая заметка", row.Keynote);
                            TryUpdateTextParameter(material, BuiltInParameter.ALL_MODEL_MARK, "Маркировка", row.Marking);
                        }

                        transaction.Commit();
                    }

                    if (deleted)
                    {
                        result.DeletedMaterialsCount++;
                    }

                    if (renamed)
                    {
                        result.RenamedMaterialsCount++;
                    }

                    if (descriptionChanged)
                    {
                        result.UpdatedDescriptionsCount++;
                    }
                }
                catch (Exception exception)
                {
                    // Блок обработки ошибок зависимостей/дубликатов:
                    // проблемная строка пропускается, процесс продолжается.
                    result.Errors.Add(new NamingErrorCsvModel
                    {
                        OldName = BuildOldLabel(row),
                        NewName = BuildNewLabel(row),
                        ErrorText = exception.Message
                    });
                }
            }

            return result;
        }

        // Блок предварительного сопоставления строк CSV с Material.Id по исходным значениям
        private static Dictionary<int, ElementId> ResolveMaterialIdsByRows(Document document, List<MaterialNamingCsvModel> rows)
        {
            Dictionary<int, ElementId> result = new Dictionary<int, ElementId>();
            Dictionary<string, Queue<ElementId>> byFullKey = new Dictionary<string, Queue<ElementId>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Queue<ElementId>> byNameKey = new Dictionary<string, Queue<ElementId>>(StringComparer.OrdinalIgnoreCase);

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(Material));

            foreach (Element element in collector)
            {
                Material material = element as Material;

                if (material == null)
                {
                    continue;
                }

                string fullKey = BuildMaterialKey(material.Name, GetDescription(material));
                string nameKey = BuildNameKey(material.Name);

                if (!byFullKey.ContainsKey(fullKey))
                {
                    byFullKey[fullKey] = new Queue<ElementId>();
                }

                if (!byNameKey.ContainsKey(nameKey))
                {
                    byNameKey[nameKey] = new Queue<ElementId>();
                }

                byFullKey[fullKey].Enqueue(material.Id);
                byNameKey[nameKey].Enqueue(material.Id);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                MaterialNamingCsvModel row = rows[i];

                if (row == null)
                {
                    continue;
                }

                string fullKey = BuildMaterialKey(row.MaterialNameOld, row.DescriptionOld);
                string nameKey = BuildNameKey(row.MaterialNameOld);

                ElementId mappedId = ElementId.InvalidElementId;

                Queue<ElementId> fullQueue;

                if (byFullKey.TryGetValue(fullKey, out fullQueue) && fullQueue.Count > 0)
                {
                    mappedId = fullQueue.Dequeue();
                    RemoveFromNameQueue(byNameKey, nameKey, mappedId);
                }
                else
                {
                    Queue<ElementId> nameQueue;

                    if (byNameKey.TryGetValue(nameKey, out nameQueue) && nameQueue.Count > 0)
                    {
                        mappedId = nameQueue.Dequeue();
                    }
                }

                result[i] = mappedId;
            }

            return result;
        }

        private static void RemoveFromNameQueue(Dictionary<string, Queue<ElementId>> byNameKey, string nameKey, ElementId usedId)
        {
            Queue<ElementId> nameQueue;

            if (!byNameKey.TryGetValue(nameKey, out nameQueue) || nameQueue.Count == 0)
            {
                return;
            }

            Queue<ElementId> rebuiltQueue = new Queue<ElementId>();

            while (nameQueue.Count > 0)
            {
                ElementId currentId = nameQueue.Dequeue();

                if (currentId.IntegerValue == usedId.IntegerValue)
                {
                    continue;
                }

                rebuiltQueue.Enqueue(currentId);
            }

            byNameKey[nameKey] = rebuiltQueue;
        }

        private static string BuildMaterialKey(string name, string description)
        {
            return BuildNameKey(name) + "|" + (description ?? string.Empty).Trim();
        }

        private static string BuildNameKey(string name)
        {
            return (name ?? string.Empty).Trim();
        }

        private static bool TryRenameMaterial(Material material, string materialNameNew)
        {
            if (string.IsNullOrWhiteSpace(materialNameNew))
            {
                return false;
            }

            string trimmed = materialNameNew.Trim();

            if (string.Equals(material.Name, trimmed, StringComparison.Ordinal))
            {
                return false;
            }

            material.Name = trimmed;
            return true;
        }

        private static bool TryUpdateDescription(Material material, string descriptionNew)
        {
            if (descriptionNew == null)
            {
                return false;
            }

            Parameter descriptionParameter = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);

            if (descriptionParameter == null)
            {
                descriptionParameter = material.LookupParameter("Description");
            }

            if (descriptionParameter == null || descriptionParameter.IsReadOnly)
            {
                return false;
            }

            string currentValue = descriptionParameter.AsString() ?? string.Empty;

            if (string.Equals(currentValue, descriptionNew, StringComparison.Ordinal))
            {
                return false;
            }

            descriptionParameter.Set(descriptionNew);
            return true;
        }

        private static bool TryUpdateTextParameter(Material material, BuiltInParameter builtInParameter, string fallbackName, string newValue)
        {
            Parameter parameter = material.get_Parameter(builtInParameter);

            if (parameter == null)
            {
                parameter = material.LookupParameter(fallbackName);
            }

            if (parameter == null || parameter.IsReadOnly)
            {
                return false;
            }

            string safeValue = newValue ?? string.Empty;
            string currentValue = parameter.AsString() ?? string.Empty;

            if (string.Equals(currentValue, safeValue, StringComparison.Ordinal))
            {
                return false;
            }

            parameter.Set(safeValue);
            return true;
        }

        private static string GetDescription(Material material)
        {
            Parameter descriptionParameter = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);

            if (descriptionParameter == null)
            {
                descriptionParameter = material.LookupParameter("Description");
            }

            if (descriptionParameter == null)
            {
                return string.Empty;
            }

            return descriptionParameter.AsString() ?? string.Empty;
        }

        private static string BuildOldLabel(MaterialNamingCsvModel row)
        {
            return (row.MaterialNameOld ?? string.Empty) + " | " + (row.DescriptionOld ?? string.Empty);
        }

        private static string BuildNewLabel(MaterialNamingCsvModel row)
        {
            if (row.DeleteMaterial)
            {
                return "DELETE";
            }

            return (row.MaterialNameNew ?? string.Empty) + " | " + (row.DescriptionNew ?? string.Empty);
        }
    }

    public class MaterialNamingApplyResult
    {
        public MaterialNamingApplyResult()
        {
            Errors = new List<NamingErrorCsvModel>();
        }

        public int RenamedMaterialsCount { get; set; }

        public int UpdatedDescriptionsCount { get; set; }

        public int DeletedMaterialsCount { get; set; }

        public List<NamingErrorCsvModel> Errors { get; private set; }
    }
}
