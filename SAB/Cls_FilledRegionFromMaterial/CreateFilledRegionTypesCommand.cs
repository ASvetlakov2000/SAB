using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.FilledRegionFromMaterial
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CreateFilledRegionTypesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document.IsFamilyDocument)
            {
                TaskDialog.Show(
                    "Область заливки из материала",
                    "Откройте проект Revit. В редакторе семейств команда не работает.");
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;

            try
            {
                PluginSettings settings = PluginSettingsStorage.Load(document);
                return Run(document, settings);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return Result.Failed;
            }
        }

        internal static Result Run(Document document, PluginSettings settings)
        {
            MaterialScanResult scan = MaterialScanner.Scan(
                document,
                settings.IncludeUnusedMaterials,
                settings.IncludePaintedMaterials);
            IList<FilledRegionType> existingTypes = new FilteredElementCollector(document)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (existingTypes.Count == 0)
            {
                TaskDialog.Show(
                    "Область заливки из материала",
                    "В проекте нет ни одного типа области заливки, который можно использовать как основу.");
                return Result.Cancelled;
            }

            SyncReport report = Synchronize(document, scan, existingTypes, settings);
            TaskDialog.Show("Область заливки из материала", BuildReportText(scan, report, settings));
            return Result.Succeeded;
        }

        private static SyncReport Synchronize(
            Document document,
            MaterialScanResult scan,
            IList<FilledRegionType> existingTypes,
            PluginSettings settings)
        {
            var report = new SyncReport();
            FilledRegionType template = existingTypes.First();
            Dictionary<string, FilledRegionType> typesByName = existingTypes
                .GroupBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, FilledRegionType> typesByMaterialUniqueId = existingTypes
                .Select(type => new { Type = type, MaterialUniqueId = MaterialTypeMetadata.Read(type) })
                .Where(item => !string.IsNullOrWhiteSpace(item.MaterialUniqueId))
                .GroupBy(item => item.MaterialUniqueId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.Ordinal);
            var materialsByTypeId = new Dictionary<int, Material>();

            using (var transaction = new Transaction(document, "SAB: области заливки из материалов"))
            {
                transaction.Start();

                foreach (Material material in scan.Materials)
                {
                    if (settings.SkipMaterialsWithoutCutPattern && !HasCutPattern(material))
                    {
                        report.WithoutCutPattern++;
                        continue;
                    }

                    string typeName = settings.TypeNamePrefix + material.Name;
                    FilledRegionType existingTarget = null;
                    if (!typesByMaterialUniqueId.TryGetValue(material.UniqueId, out existingTarget))
                    {
                        typesByName.TryGetValue(typeName, out existingTarget);
                    }

                    using (var subTransaction = new SubTransaction(document))
                    {
                        subTransaction.Start();

                        try
                        {
                            FilledRegionType target;
                            bool created;
                            if (existingTarget != null)
                            {
                                target = existingTarget;
                                created = false;
                                if (!string.Equals(target.Name, typeName, StringComparison.Ordinal))
                                {
                                    target.Name = typeName;
                                }
                            }
                            else
                            {
                                target = template.Duplicate(typeName) as FilledRegionType;
                                created = true;
                                if (target == null)
                                {
                                    throw new InvalidOperationException("Не удалось создать тип области заливки.");
                                }
                            }

                            ApplyMaterialGraphics(target, material, settings);
                            CopyMappedParameters(
                                material,
                                target,
                                ParameterTargetScope.Type,
                                settings,
                                report);
                            MaterialTypeMetadata.Write(target, material.UniqueId);
                            int targetId = target.Id.IntegerValue;
                            subTransaction.Commit();

                            materialsByTypeId[targetId] = material;
                            if (created)
                            {
                                typesByName.Add(typeName, target);
                                typesByMaterialUniqueId.Add(material.UniqueId, target);
                                report.Created++;
                            }
                            else
                            {
                                report.Updated++;
                            }
                        }
                        catch (Exception exception)
                        {
                            subTransaction.RollBack();
                            report.Errors.Add(material.Name + ": " + exception.Message);
                        }
                    }
                }

                UpdateFilledRegionInstances(document, materialsByTypeId, settings, report);
                transaction.Commit();
            }

            return report;
        }

        private static void UpdateFilledRegionInstances(
            Document document,
            IDictionary<int, Material> materialsByTypeId,
            PluginSettings settings,
            SyncReport report)
        {
            bool hasInstanceMappings = settings.ParameterMappings.Any(mapping =>
                ParameterIdentity.GetTargetScope(mapping.TargetSelectionKey) == ParameterTargetScope.Instance);
            if (!hasInstanceMappings)
            {
                return;
            }

            foreach (FilledRegion region in new FilteredElementCollector(document)
                         .OfClass(typeof(FilledRegion))
                         .Cast<FilledRegion>())
            {
                Material material;
                if (!materialsByTypeId.TryGetValue(region.GetTypeId().IntegerValue, out material))
                {
                    continue;
                }

                CopyMappedParameters(
                    material,
                    region,
                    ParameterTargetScope.Instance,
                    settings,
                    report);
            }
        }

        private static bool HasCutPattern(Material material)
        {
            return material.CutForegroundPatternId != ElementId.InvalidElementId ||
                   material.CutBackgroundPatternId != ElementId.InvalidElementId;
        }

        private static void ApplyMaterialGraphics(
            FilledRegionType target,
            Material material,
            PluginSettings settings)
        {
            if (settings.CopyForegroundPattern)
            {
                ElementId foregroundId = material.CutForegroundPatternId;
                EnsureDraftingPattern(target.Document, foregroundId, "передняя");
                target.ForegroundPatternId = foregroundId;
                if (settings.CopyPatternColors && foregroundId != ElementId.InvalidElementId)
                {
                    target.ForegroundPatternColor = material.CutForegroundPatternColor;
                }
            }

            if (settings.CopyBackgroundPattern)
            {
                ElementId backgroundId = material.CutBackgroundPatternId;
                EnsureDraftingPattern(target.Document, backgroundId, "фоновая");
                target.BackgroundPatternId = backgroundId;
                if (settings.CopyPatternColors && backgroundId != ElementId.InvalidElementId)
                {
                    target.BackgroundPatternColor = material.CutBackgroundPatternColor;
                }
            }

            target.IsMasking = false;
        }

        private static void CopyMappedParameters(
            Material material,
            Element target,
            string requiredScope,
            PluginSettings settings,
            SyncReport report)
        {
            foreach (ParameterMappingSetting mapping in settings.ParameterMappings)
            {
                if (ParameterIdentity.GetTargetScope(mapping.TargetSelectionKey) != requiredScope)
                {
                    continue;
                }

                string error;
                if (ParameterValueCopier.TryCopy(
                    material,
                    mapping.SourceKey,
                    target,
                    ParameterIdentity.GetTargetKey(mapping.TargetSelectionKey),
                    out error))
                {
                    report.ParametersWritten++;
                }
                else if (report.ParameterWarnings.Count < 20)
                {
                    report.ParameterWarnings.Add(target.Name + ": " + error);
                }
            }
        }

        private static void EnsureDraftingPattern(Document document, ElementId patternId, string patternKind)
        {
            if (patternId == ElementId.InvalidElementId)
            {
                return;
            }

            FillPatternElement patternElement = document.GetElement(patternId) as FillPatternElement;
            FillPattern pattern = patternElement == null ? null : patternElement.GetFillPattern();
            if (pattern == null || pattern.Target != FillPatternTarget.Drafting)
            {
                throw new InvalidOperationException(
                    patternKind + " штриховка сечения не является чертёжной и не может использоваться в области заливки");
            }
        }

        private static string BuildReportText(
            MaterialScanResult scan,
            SyncReport report,
            PluginSettings settings)
        {
            var text = new StringBuilder();
            text.AppendLine("Готово.");
            text.AppendLine();
            text.AppendLine("Обработано материалов: " + scan.Materials.Count);
            text.AppendLine("Создано типов: " + report.Created);
            text.AppendLine("Обновлено типов " + settings.TypeNamePrefix + ": " + report.Updated);
            text.AppendLine("Записано значений параметров: " + report.ParametersWritten);
            text.AppendLine("Без штриховки сечения: " + report.WithoutCutPattern);

            if (scan.ScanErrorCount > 0)
            {
                text.AppendLine("Элементов, которые не удалось проверить: " + scan.ScanErrorCount);
            }

            if (report.Errors.Count > 0)
            {
                text.AppendLine("Ошибок: " + report.Errors.Count);
                text.AppendLine();
                text.AppendLine("Первые ошибки:");

                foreach (string error in report.Errors.Take(6))
                {
                    text.AppendLine("• " + error);
                }
            }

            if (report.ParameterWarnings.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Параметры не записаны:");

                foreach (string warning in report.ParameterWarnings.Take(6))
                {
                    text.AppendLine("• " + warning);
                }
            }

            return text.ToString().TrimEnd();
        }

        private sealed class SyncReport
        {
            public SyncReport()
            {
                Errors = new List<string>();
                ParameterWarnings = new List<string>();
            }

            public int Created { get; set; }
            public int Updated { get; set; }
            public int ParametersWritten { get; set; }
            public int WithoutCutPattern { get; set; }
            public IList<string> Errors { get; private set; }
            public IList<string> ParameterWarnings { get; private set; }
        }
    }
}
