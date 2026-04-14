using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Revit
{
    /// <summary>
    /// Пакетное применение переименования типоразмеров и семейств по CSV.
    /// </summary>
    public class TypeNamingApplyService
    {
        public TypeNamingApplyResult Apply(Document document, List<TypeNamingCsvModel> rows)
        {
            TypeNamingApplyResult result = new TypeNamingApplyResult();

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            Dictionary<int, ElementId> mappedTypeIds = ResolveTypeIdsByRows(document, rows);

            // Блок последовательного применения строк CSV/XLSX без перестановки порядка
            // Здесь нельзя менять порядок строк, так как переименование идет построчно
            for (int i = 0; i < rows.Count; i++)
            {
                TypeNamingCsvModel row = rows[i];

                if (row == null)
                {
                    continue;
                }

                bool hasFamilyChange = !string.IsNullOrWhiteSpace(row.FamilyNew) &&
                                       !string.Equals((row.FamilyOld ?? string.Empty).Trim(), row.FamilyNew.Trim(), StringComparison.Ordinal);

                bool hasTypeChange = !string.IsNullOrWhiteSpace(row.TypeNameNew) &&
                                     !string.Equals((row.TypeNameOld ?? string.Empty).Trim(), row.TypeNameNew.Trim(), StringComparison.Ordinal);

                if (!hasFamilyChange && !hasTypeChange)
                {
                    continue;
                }

                ElementId mappedId;

                if (!mappedTypeIds.TryGetValue(i, out mappedId) || mappedId == ElementId.InvalidElementId)
                {
                    result.Errors.Add(new NamingErrorCsvModel
                    {
                        OldName = BuildOldLabel(row),
                        NewName = BuildNewLabel(row),
                        ErrorText = "Element type was not found by old values (Category/Family/Type)."
                    });
                    continue;
                }

                ElementType type = document.GetElement(mappedId) as ElementType;

                if (type == null)
                {
                    result.Errors.Add(new NamingErrorCsvModel
                    {
                        OldName = BuildOldLabel(row),
                        NewName = BuildNewLabel(row),
                        ErrorText = "Element type from mapping no longer exists."
                    });
                    continue;
                }

                try
                {
                    bool familyRenamed = false;
                    bool typeRenamed = false;

                    using (Transaction transaction = new Transaction(document, "Apply type naming row " + row.RowIndex))
                    {
                        transaction.Start();

                        if (hasFamilyChange)
                        {
                            RenameFamily(type, row.FamilyNew.Trim());
                            familyRenamed = true;
                        }

                        if (hasTypeChange)
                        {
                            type.Name = row.TypeNameNew.Trim();
                            typeRenamed = true;
                        }

                        transaction.Commit();
                    }

                    if (familyRenamed)
                    {
                        result.RenamedFamiliesCount++;
                    }

                    if (typeRenamed)
                    {
                        result.RenamedTypesCount++;
                    }
                }
                catch (Exception exception)
                {
                    // Блок обработки ошибок дубликатов и других ограничений Revit:
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

        // Блок предварительного сопоставления строк CSV с ElementId по исходным старым значениям
        private static Dictionary<int, ElementId> ResolveTypeIdsByRows(Document document, List<TypeNamingCsvModel> rows)
        {
            Dictionary<int, ElementId> result = new Dictionary<int, ElementId>();
            Dictionary<string, Queue<ElementId>> lookup = BuildTypeLookup(document);

            for (int i = 0; i < rows.Count; i++)
            {
                TypeNamingCsvModel row = rows[i];

                if (row == null)
                {
                    continue;
                }

                string key = BuildTypeKey(row.Category, row.FamilyOld, row.TypeNameOld);

                Queue<ElementId> queue;

                if (lookup.TryGetValue(key, out queue) && queue.Count > 0)
                {
                    result[i] = queue.Dequeue();
                }
                else
                {
                    result[i] = ElementId.InvalidElementId;
                }
            }

            return result;
        }

        private static Dictionary<string, Queue<ElementId>> BuildTypeLookup(Document document)
        {
            Dictionary<string, Queue<ElementId>> lookup = new Dictionary<string, Queue<ElementId>>(StringComparer.OrdinalIgnoreCase);

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ElementType));

            foreach (Element element in collector)
            {
                ElementType elementType = element as ElementType;

                if (elementType == null || elementType.Category == null)
                {
                    continue;
                }

                string key = BuildTypeKey(elementType.Category.Name, elementType.FamilyName, elementType.Name);

                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = new Queue<ElementId>();
                }

                lookup[key].Enqueue(elementType.Id);
            }

            return lookup;
        }

        private static string BuildTypeKey(string categoryName, string familyName, string typeName)
        {
            return (categoryName ?? string.Empty).Trim() + "|" +
                   (familyName ?? string.Empty).Trim() + "|" +
                   (typeName ?? string.Empty).Trim();
        }

        // Блок переименования семейства (только для загружаемых семейств)
        private static void RenameFamily(ElementType type, string newFamilyName)
        {
            FamilySymbol familySymbol = type as FamilySymbol;

            if (familySymbol == null || familySymbol.Family == null)
            {
                throw new InvalidOperationException("Family rename is not supported for this type.");
            }

            if (string.Equals(familySymbol.Family.Name, newFamilyName, StringComparison.Ordinal))
            {
                return;
            }

            familySymbol.Family.Name = newFamilyName;
        }

        private static string BuildOldLabel(TypeNamingCsvModel row)
        {
            return (row.Category ?? string.Empty) + " | " +
                   (row.FamilyOld ?? string.Empty) + " | " +
                   (row.TypeNameOld ?? string.Empty);
        }

        private static string BuildNewLabel(TypeNamingCsvModel row)
        {
            return (row.Category ?? string.Empty) + " | " +
                   (row.FamilyNew ?? string.Empty) + " | " +
                   (row.TypeNameNew ?? string.Empty);
        }
    }

    public class TypeNamingApplyResult
    {
        public TypeNamingApplyResult()
        {
            Errors = new List<NamingErrorCsvModel>();
        }

        public int RenamedFamiliesCount { get; set; }

        public int RenamedTypesCount { get; set; }

        public List<NamingErrorCsvModel> Errors { get; private set; }
    }
}
