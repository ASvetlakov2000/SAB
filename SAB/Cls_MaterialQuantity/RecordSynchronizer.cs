using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace SAB.MaterialQuantity
{
    internal sealed class SyncReport
    {
        internal int Created;
        internal int Updated;
        internal int NeedsReview;
        internal int Ignored;
        internal int Obsolete;
        internal int HostCount;
        internal int DetailedRecords;
        internal int AggregateRecords;
    }

    internal static class RecordSynchronizer
    {
        private sealed class AggregateRecord
        {
            private readonly HashSet<int> _layerIndexes = new HashSet<int>();
            private bool _thicknessInitialized;
            private bool _hasMixedThickness;

            internal string Key;
            internal string MaterialName;
            internal string MaterialDescription;
            internal string MaterialModel;
            internal string Unit;
            internal string Status;
            internal string SourceCategory;
            internal string SourceType;
            internal double Quantity;
            internal double MaterialVolumeInternal;
            internal double ThicknessInternal;
            internal int SourceCount;

            internal string LayerSummary
            {
                get
                {
                    if (_layerIndexes.Count == 0) return string.Empty;
                    if (_layerIndexes.Count == 1) return _layerIndexes.First().ToString();
                    return "разные";
                }
            }

            internal string SourceSummary
            {
                get { return SourceCount + " источников"; }
            }

            internal double DisplayThickness
            {
                get { return _hasMixedThickness ? 0 : ThicknessInternal; }
            }

            internal void Add(MaterialLayerAuditRecord detail)
            {
                SourceCount++;
                Quantity += detail.Quantity;
                MaterialVolumeInternal += detail.MaterialVolumeInternal;
                _layerIndexes.Add(detail.LayerIndex);

                if (!_thicknessInitialized)
                {
                    ThicknessInternal = detail.ThicknessInternal;
                    _thicknessInitialized = true;
                }
                else if (Math.Abs(ThicknessInternal - detail.ThicknessInternal) > 1e-9)
                {
                    _hasMixedThickness = true;
                }
            }
        }

        internal static SyncReport Run(
            Document project,
            FamilySymbol symbol,
            Action<MaterialQuantityProgressInfo> progress = null)
        {
            var report = new SyncReport();
            var existing = new Dictionary<string, FamilyInstance>(StringComparer.Ordinal);
            var duplicateRecords = new List<FamilyInstance>();

            List<FamilyInstance> allInstances = new FilteredElementCollector(project)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(instance => instance.Symbol != null && instance.Symbol.Family.Id == symbol.Family.Id)
                .ToList();
            for (int instanceIndex = 0; instanceIndex < allInstances.Count; instanceIndex++)
            {
                FamilyInstance instance = allInstances[instanceIndex];
                string key = Read(instance, RecordFields.Key);
                if (string.IsNullOrWhiteSpace(key) || existing.ContainsKey(key))
                    duplicateRecords.Add(instance);
                else
                    existing.Add(key, instance);

                if (ShouldReport(instanceIndex + 1, allInstances.Count))
                {
                    ReportProgress(
                        progress,
                        20 + ScaleProgress(instanceIndex + 1, allInstances.Count, 10),
                        "Проверка существующих записей",
                        "Индексируем агрегированные и устаревшие расчётные записи.",
                        instanceIndex + 1,
                        allInstances.Count,
                        report);
                }
            }

            ICollection<Element> hosts = new FilteredElementCollector(project)
                .WherePasses(new ElementMulticategoryFilter(MaterialUsageCollector.HostCategories.ToList()))
                .WhereElementIsNotElementType().ToElements();
            report.HostCount = hosts.Count;

            var auditRecords = new List<MaterialLayerAuditRecord>();
            var aggregates = new Dictionary<string, AggregateRecord>(StringComparer.Ordinal);
            int processedHosts = 0;
            ReportProgress(
                progress,
                30,
                "Расчёт материалов",
                "Найдено конструкций: " + hosts.Count + ". Подробные строки создаются только в памяти.",
                processedHosts,
                hosts.Count,
                report);

            foreach (Element host in hosts)
            {
                var hostType = project.GetElement(host.GetTypeId()) as HostObjAttributes;
                CompoundStructure compound = hostType == null ? null : hostType.GetCompoundStructure();
                if (compound != null)
                {
                    IList<CompoundStructureLayer> layers = compound.GetLayers();
                    var materialCounts = layers.Where(layer => layer.MaterialId != ElementId.InvalidElementId)
                        .GroupBy(layer => RevitElementId.Key(layer.MaterialId))
                        .ToDictionary(group => group.Key, group => group.Count());

                    for (int index = 0; index < layers.Count; index++)
                    {
                        CompoundStructureLayer layer = layers[index];
                        Material material = project.GetElement(layer.MaterialId) as Material;
                        if (material == null) continue;

                        MaterialLayerAuditRecord detail = Calculate(
                            host,
                            hostType,
                            layer,
                            index,
                            material,
                            materialCounts[RevitElementId.Key(layer.MaterialId)]);
                        auditRecords.Add(detail);

                        if (detail.Status == MaterialUnitResolver.Ignore)
                            report.Ignored++;
                        else if (detail.Status != RecordFields.Ready)
                            report.NeedsReview++;

                        string category = detail.Status == RecordFields.Ready
                            ? string.Empty
                            : detail.SourceCategory;
                        string sourceType = detail.Status == RecordFields.Ready
                            ? string.Empty
                            : detail.SourceType;
                        string aggregateKey = BuildAggregateKey(
                            detail.MaterialId,
                            detail.Unit,
                            detail.Status,
                            category,
                            sourceType);

                        AggregateRecord aggregate;
                        if (!aggregates.TryGetValue(aggregateKey, out aggregate))
                        {
                            aggregate = new AggregateRecord
                            {
                                Key = aggregateKey,
                                MaterialName = detail.MaterialName,
                                MaterialDescription = detail.MaterialDescription,
                                MaterialModel = detail.MaterialModel,
                                Unit = detail.Unit,
                                Status = detail.Status,
                                SourceCategory = category,
                                SourceType = sourceType
                            };
                            aggregates.Add(aggregateKey, aggregate);
                        }

                        aggregate.Add(detail);
                    }
                }

                processedHosts++;
                if (ShouldReport(processedHosts, hosts.Count))
                {
                    string category = host.Category == null ? "Конструкция" : host.Category.Name;
                    ReportProgress(
                        progress,
                        30 + ScaleProgress(processedHosts, hosts.Count, 40),
                        "Расчёт материалов",
                        category + " " + processedHosts + " из " + hosts.Count + ".",
                        processedHosts,
                        hosts.Count,
                        report);
                }
            }

            report.DetailedRecords = auditRecords.Count;
            report.AggregateRecords = aggregates.Count;
            MaterialQuantityAuditStorage.Save(project, auditRecords, hosts.Count);

            Level level = null;
            if (aggregates.Count > 0)
            {
                level = new FilteredElementCollector(project).OfClass(typeof(Level))
                    .Cast<Level>().OrderBy(item => item.Elevation).FirstOrDefault();
                if (level == null)
                    throw new InvalidOperationException("Не найден уровень для размещения агрегированных записей.");
            }

            List<AggregateRecord> orderedAggregates = aggregates.Values
                .OrderBy(item => item.Unit, StringComparer.CurrentCulture)
                .ThenBy(item => item.MaterialName, StringComparer.CurrentCulture)
                .ThenBy(item => item.Status, StringComparer.CurrentCulture)
                .ThenBy(item => item.SourceCategory, StringComparer.CurrentCulture)
                .ThenBy(item => item.SourceType, StringComparer.CurrentCulture)
                .ToList();

            for (int aggregateIndex = 0; aggregateIndex < orderedAggregates.Count; aggregateIndex++)
            {
                AggregateRecord aggregate = orderedAggregates[aggregateIndex];
                FamilyInstance record;
                if (existing.TryGetValue(aggregate.Key, out record))
                {
                    existing.Remove(aggregate.Key);
                    report.Updated++;
                }
                else
                {
                    record = project.Create.NewFamilyInstance(
                        XYZ.Zero, symbol, level, StructuralType.NonStructural);
                    report.Created++;
                }

                WriteAggregate(record, aggregate);

                if (ShouldReport(aggregateIndex + 1, orderedAggregates.Count))
                {
                    ReportProgress(
                        progress,
                        70 + ScaleProgress(aggregateIndex + 1, orderedAggregates.Count, 12),
                        "Запись итогов",
                        "Агрегированная строка " + (aggregateIndex + 1) +
                        " из " + orderedAggregates.Count + ".",
                        aggregateIndex + 1,
                        orderedAggregates.Count,
                        report);
                }
            }

            List<FamilyInstance> obsoleteRecords = existing.Values.Concat(duplicateRecords).ToList();
            for (int obsoleteIndex = 0; obsoleteIndex < obsoleteRecords.Count; obsoleteIndex++)
            {
                FamilyInstance obsolete = obsoleteRecords[obsoleteIndex];
                project.Delete(obsolete.Id);
                report.Obsolete++;

                if (ShouldReport(obsoleteIndex + 1, obsoleteRecords.Count))
                {
                    ReportProgress(
                        progress,
                        82 + ScaleProgress(obsoleteIndex + 1, obsoleteRecords.Count, 3),
                        "Удаление старых записей",
                        "Удаляем послойные экземпляры предыдущей архитектуры.",
                        obsoleteIndex + 1,
                        obsoleteRecords.Count,
                        report);
                }
            }

            return report;
        }

        private static MaterialLayerAuditRecord Calculate(
            Element host,
            HostObjAttributes hostType,
            CompoundStructureLayer layer,
            int layerIndex,
            Material material,
            int materialLayerCount)
        {
            string unit = MaterialUnitResolver.Resolve(material);
            string status;
            double quantity = 0;
            double volume = 0;

            if (unit == MaterialUnitResolver.Ignore)
                status = MaterialUnitResolver.Ignore;
            else if (materialLayerCount > 1)
                status = "Материал повторяется в слоях";
            else if (unit == null)
                status = "Не задана единица материала";
            else if (unit == "м")
                status = "Нужно правило линейного подсчёта";
            else
            {
                try { volume = host.GetMaterialVolume(material.Id); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { volume = 0; }

                if (volume <= 0)
                    status = "Нет расчётного объёма Revit";
                else if (unit == "м²" && layer.Width <= 0)
                    status = "Нулевая толщина слоя";
                else
                {
                    double internalQuantity = unit == "м³" ? volume : volume / layer.Width;
                    ForgeTypeId targetUnit = unit == "м³"
                        ? UnitTypeId.CubicMeters
                        : UnitTypeId.SquareMeters;
                    quantity = UnitUtils.ConvertFromInternalUnits(internalQuantity, targetUnit);
                    status = RecordFields.Ready;
                }
            }

            return new MaterialLayerAuditRecord
            {
                Key = host.UniqueId + "|" + layerIndex,
                SourceId = host.UniqueId,
                SourceCategory = host.Category == null ? string.Empty : host.Category.Name,
                SourceType = hostType == null ? string.Empty : hostType.Name,
                LayerIndex = layerIndex + 1,
                MaterialId = material.UniqueId,
                MaterialName = material.Name,
                MaterialDescription = Read(material, BuiltInParameter.ALL_MODEL_DESCRIPTION),
                MaterialModel = Read(material, BuiltInParameter.ALL_MODEL_MODEL),
                ThicknessInternal = layer.Width,
                MaterialVolumeInternal = volume,
                Unit = unit ?? string.Empty,
                Quantity = quantity,
                Status = status
            };
        }

        private static string BuildAggregateKey(
            string materialId,
            string unit,
            string status,
            string category,
            string sourceType)
        {
            string identity = string.Join(
                "\u001f",
                materialId ?? string.Empty,
                unit ?? string.Empty,
                status ?? string.Empty,
                category ?? string.Empty,
                sourceType ?? string.Empty);

            using (SHA256 hashAlgorithm = SHA256.Create())
            {
                byte[] hash = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(identity));
                return "aggregate-v2|" + BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void WriteAggregate(Element record, AggregateRecord aggregate)
        {
            Write(record, RecordFields.Marker, RecordFields.MarkerValue);
            Write(record, RecordFields.Key, aggregate.Key);
            Write(record, RecordFields.SourceId, aggregate.SourceSummary);
            Write(record, RecordFields.SourceCategory, aggregate.SourceCategory);
            Write(record, RecordFields.SourceType, aggregate.SourceType);
            Write(record, RecordFields.LayerIndex, aggregate.LayerSummary);
            Write(record, RecordFields.MaterialName, aggregate.MaterialName);
            Write(record, RecordFields.LegacyMaterialName, aggregate.MaterialName);
            Write(record, RecordFields.MaterialDescription, aggregate.MaterialDescription);
            Write(record, RecordFields.MaterialModel, aggregate.MaterialModel);
            Write(record, RecordFields.Thickness, aggregate.DisplayThickness);
            Write(record, RecordFields.MaterialVolume, aggregate.MaterialVolumeInternal);
            Write(record, RecordFields.Unit, aggregate.Unit);
            Write(record, RecordFields.Quantity, aggregate.Quantity);
            Write(record, RecordFields.Status, aggregate.Status);
        }

        private static bool ShouldReport(int current, int total)
        {
            if (current <= 1 || current >= total) return true;
            int interval = Math.Max(1, total / 100);
            return current % interval == 0;
        }

        private static int ScaleProgress(int current, int total, int range)
        {
            return total <= 0 ? range : (int)Math.Round((double)range * current / total);
        }

        private static void ReportProgress(
            Action<MaterialQuantityProgressInfo> progress,
            int percentage,
            string stage,
            string details,
            int current,
            int total,
            SyncReport report)
        {
            if (progress == null) return;

            progress(new MaterialQuantityProgressInfo
            {
                OverallPercentage = Math.Max(0, Math.Min(percentage, 100)),
                CurrentItem = current,
                TotalItems = total,
                Stage = stage,
                Details = details,
                Created = report.Created,
                Updated = report.Updated,
                NeedsReview = report.NeedsReview,
                Obsolete = report.Obsolete
            });
        }

        private static string Read(Element element, string name)
        {
            Parameter parameter = element.LookupParameter(name);
            return parameter == null ? null : parameter.AsString();
        }

        private static string Read(Element element, BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter == null ? string.Empty : parameter.AsString() ?? string.Empty;
        }

        private static void Write(Element element, string name, string value)
        {
            Parameter parameter = element.LookupParameter(name);
            if (parameter == null || parameter.IsReadOnly)
                throw new InvalidOperationException("Нет доступного параметра " + name + " у расчётной записи.");
            parameter.Set(value ?? string.Empty);
        }

        private static void Write(Element element, string name, double value)
        {
            Parameter parameter = element.LookupParameter(name);
            if (parameter == null || parameter.IsReadOnly)
                throw new InvalidOperationException("Нет доступного параметра " + name + " у расчётной записи.");
            parameter.Set(value);
        }
    }

    internal static class MaterialUnitResolver
    {
        internal const string RuleParameterName = "SAB_ЕдиницаПодсчета";
        internal const string Auto = "Авто (ADSK)";
        internal const string Ignore = "Не учитывать";
        private static readonly string[] FallbackParameterNames =
        {
            "ADSK_Материал тип подсчета", "ADSK_Единица измерения"
        };

        internal static string Resolve(Material material)
        {
            string explicitRule = ReadExplicit(material);
            if (explicitRule != null) return explicitRule;
            return ResolveFallback(material);
        }

        internal static string ReadExplicit(Material material)
        {
            Parameter parameter = material.LookupParameter(RuleParameterName);
            if (parameter == null) return null;
            return Normalize(parameter.AsString() ?? parameter.AsValueString());
        }

        internal static string ResolveFallback(Material material)
        {
            foreach (string name in FallbackParameterNames)
            {
                Parameter parameter = material.LookupParameter(name);
                if (parameter == null) continue;
                string result = Normalize(parameter.AsString() ?? parameter.AsValueString());
                if (result != null) return result;
            }
            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = value.ToLowerInvariant().Replace(" ", "")
                .Replace("³", "3").Replace("²", "2");
            if (normalized.Contains("неучитывать")) return Ignore;
            if (normalized.Contains("м3") || normalized.Contains("куб") ||
                normalized.Contains("объ") || normalized.Contains("обь")) return "м³";
            if (normalized.Contains("м2") || normalized.Contains("кв") ||
                normalized.Contains("площад")) return "м²";
            if (normalized == "м" || normalized.Contains("пог") || normalized.Contains("длин")) return "м";
            return null;
        }
    }
}
