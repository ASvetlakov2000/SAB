using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Views
{
    /// <summary>
    /// Service responsible for exporting images for all legend components
    /// placed on active Legend view.
    /// </summary>
    public class LegendComponentImageExportService
    {
        private const string ProblemLegendBaseName = "Пироги_Проблемные типы_Наименования";

        private static readonly HashSet<string> ReservedWindowsFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Runs sequential image export for all legend components on active legend view.
        /// </summary>
        public LegendComponentImageExportResult ExportAllFromActiveLegend(
            UIDocument uiDocument,
            View legendView,
            string outputFolderPath)
        {
            LegendComponentImageExportResult result = new LegendComponentImageExportResult();

            if (uiDocument == null)
            {
                result.FatalError = "UIDocument is not available.";
                return result;
            }

            Document document = uiDocument.Document;

            if (document == null)
            {
                result.FatalError = "Document is not available.";
                return result;
            }

            if (legendView == null || legendView.ViewType != ViewType.Legend)
            {
                result.FatalError = "Active view must be a Legend view.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(outputFolderPath))
            {
                result.FatalError = "Output folder path is empty.";
                return result;
            }

            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
            }

            // Block responsible for collecting all components currently visible on target legend.
            List<Element> legendComponents = CollectLegendComponentsOrdered(document, legendView);
            result.TotalLegendComponentsOnView = legendComponents.Count;

            if (legendComponents.Count == 0)
            {
                result.FatalError = "No OST_LegendComponents were found on active Legend view.";
                return result;
            }

            UIView activeUiView = FindOpenUiView(uiDocument, legendView.Id);

            // Block responsible for sequential export with isolated single component workflow.
            for (int index = 0; index < legendComponents.Count; index++)
            {
                Element legendComponent = legendComponents[index];

                if (legendComponent == null || !legendComponent.IsValidObject)
                {
                    result.AddSkipped("LegendComponent_<invalid>", string.Empty, "Компонент легенды невалиден до начала экспорта.");
                    continue;
                }

                LegendRepresentedTypeInfo typeInfo = ResolveLegendRepresentedTypeInfo(document, legendComponent);
                string originalTypeName = typeInfo.TypeName;

                string invalidNameReason;

                // Блок проверки имени файла: имя типа должно сохраняться без изменений, если символы допустимы в Windows.
                if (!TryValidateWindowsFileName(originalTypeName, out invalidNameReason))
                {
                    result.AddInvalidNameIssue(typeInfo.RepresentedTypeId, originalTypeName, invalidNameReason);
                    result.AddSkipped(originalTypeName, string.Empty, invalidNameReason);
                    continue;
                }

                string expectedFilePath = Path.Combine(outputFolderPath, originalTypeName + ".png");
                string expectedFilePathWithoutExtension = Path.Combine(outputFolderPath, originalTypeName);
                bool temporaryIsolationEnabled = false;

                try
                {
                    HashSet<string> filesBeforeExport = CapturePngFileSnapshot(outputFolderPath);

                    // If file already exists, remove it so current export keeps exact target name.
                    if (File.Exists(expectedFilePath))
                    {
                        File.Delete(expectedFilePath);
                    }

                    using (Transaction isolateTransaction = new Transaction(document, "Temporary isolate legend component"))
                    {
                        isolateTransaction.Start();
                        legendView.IsolateElementsTemporary(new List<ElementId> { legendComponent.Id });
                        isolateTransaction.Commit();
                    }

                    temporaryIsolationEnabled = true;

                    if (uiDocument.ActiveView == null || uiDocument.ActiveView.Id != legendView.Id)
                    {
                        uiDocument.ActiveView = legendView;
                    }

                    uiDocument.RefreshActiveView();

                    if (activeUiView != null)
                    {
                        activeUiView.ZoomToFit();
                    }

                    DateTime exportStartUtc = DateTime.UtcNow;
                    ExportCurrentViewAsPng(document, expectedFilePathWithoutExtension);

                    string exportedPath = ResolveExportedPngPathAfterExport(
                        outputFolderPath,
                        expectedFilePath,
                        exportStartUtc,
                        filesBeforeExport);

                    if (string.IsNullOrWhiteSpace(exportedPath))
                    {
                        throw new InvalidOperationException("Файл PNG не найден после выполнения экспорта.");
                    }

                    string finalPath = EnsureExpectedFileName(exportedPath, expectedFilePath);
                    result.AddExported(originalTypeName, Path.GetFileName(finalPath), finalPath);
                }
                catch (Exception exception)
                {
                    result.AddSkipped(originalTypeName, Path.GetFileName(expectedFilePath), exception.Message);
                }
                finally
                {
                    if (temporaryIsolationEnabled)
                    {
                        TryDisableTemporaryIsolation(document, legendView);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates dedicated legend view that contains only components with problematic type names.
        /// </summary>
        public string CreateProblematicTypesLegendView(
            Document document,
            View sourceLegendView,
            IReadOnlyList<LegendComponentImageProblemNameIssue> issues)
        {
            if (document == null || sourceLegendView == null || sourceLegendView.ViewType != ViewType.Legend)
            {
                return string.Empty;
            }

            if (issues == null || issues.Count == 0)
            {
                return string.Empty;
            }

            HashSet<int> problematicTypeIds = new HashSet<int>();

            for (int i = 0; i < issues.Count; i++)
            {
                LegendComponentImageProblemNameIssue issue = issues[i];

                if (issue == null || issue.RepresentedTypeId == ElementId.InvalidElementId)
                {
                    continue;
                }

                problematicTypeIds.Add(issue.RepresentedTypeId.IntegerValue);
            }

            if (problematicTypeIds.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                using (Transaction transaction = new Transaction(document, "Create problematic legend view"))
                {
                    transaction.Start();

                    ElementId duplicateId = sourceLegendView.Duplicate(ViewDuplicateOption.Duplicate);

                    if (duplicateId == ElementId.InvalidElementId)
                    {
                        transaction.RollBack();
                        return string.Empty;
                    }

                    View duplicateLegend = document.GetElement(duplicateId) as View;

                    if (duplicateLegend == null || duplicateLegend.ViewType != ViewType.Legend)
                    {
                        transaction.RollBack();
                        return string.Empty;
                    }

                    List<ElementId> componentsToDelete = new List<ElementId>();
                    FilteredElementCollector collector = new FilteredElementCollector(document, duplicateLegend.Id);
                    collector.OfCategory(BuiltInCategory.OST_LegendComponents);
                    collector.WhereElementIsNotElementType();

                    foreach (Element component in collector)
                    {
                        if (component == null || !component.IsValidObject)
                        {
                            continue;
                        }

                        ElementId representedTypeId = ResolveRepresentedTypeId(component);

                        if (representedTypeId == ElementId.InvalidElementId ||
                            !problematicTypeIds.Contains(representedTypeId.IntegerValue))
                        {
                            componentsToDelete.Add(component.Id);
                        }
                    }

                    if (componentsToDelete.Count > 0)
                    {
                        document.Delete(componentsToDelete);
                    }

                    duplicateLegend.Name = BuildUniqueLegendViewName(document, ProblemLegendBaseName);
                    transaction.Commit();

                    return duplicateLegend.Name;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Collects and sorts legend components by top-to-bottom order.
        /// </summary>
        private static List<Element> CollectLegendComponentsOrdered(Document document, View legendView)
        {
            List<LegendComponentSortItem> sortItems = new List<LegendComponentSortItem>();

            FilteredElementCollector collector = new FilteredElementCollector(document, legendView.Id);
            collector.OfCategory(BuiltInCategory.OST_LegendComponents);
            collector.WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (element == null || !element.IsValidObject)
                {
                    continue;
                }

                if (element.OwnerViewId != legendView.Id)
                {
                    continue;
                }

                double y = ResolveVerticalCoordinate(element, legendView);

                sortItems.Add(new LegendComponentSortItem
                {
                    Element = element,
                    Vertical = y
                });
            }

            // Block responsible for deterministic export order:
            // first by Y from top to bottom, then by ElementId when Y is equal.
            sortItems.Sort(delegate (LegendComponentSortItem left, LegendComponentSortItem right)
            {
                int verticalCompare = right.Vertical.CompareTo(left.Vertical);

                if (verticalCompare != 0)
                {
                    return verticalCompare;
                }

                return left.Element.Id.IntegerValue.CompareTo(right.Element.Id.IntegerValue);
            });

            List<Element> ordered = new List<Element>();

            for (int i = 0; i < sortItems.Count; i++)
            {
                ordered.Add(sortItems[i].Element);
            }

            return ordered;
        }

        /// <summary>
        /// Resolves vertical coordinate used for deterministic sorting.
        /// </summary>
        private static double ResolveVerticalCoordinate(Element element, View legendView)
        {
            LocationPoint locationPoint = element.Location as LocationPoint;

            if (locationPoint != null)
            {
                return locationPoint.Point.Y;
            }

            BoundingBoxXYZ boundingBox = element.get_BoundingBox(legendView);

            if (boundingBox != null)
            {
                return boundingBox.Max.Y;
            }

            return double.MinValue;
        }

        /// <summary>
        /// Reads represented type info for legend component.
        /// </summary>
        private static LegendRepresentedTypeInfo ResolveLegendRepresentedTypeInfo(Document document, Element legendComponent)
        {
            LegendRepresentedTypeInfo info = new LegendRepresentedTypeInfo();
            info.RepresentedTypeId = ResolveRepresentedTypeId(legendComponent);

            if (info.RepresentedTypeId != ElementId.InvalidElementId)
            {
                ElementType representedType = document.GetElement(info.RepresentedTypeId) as ElementType;

                if (representedType != null && !string.IsNullOrWhiteSpace(representedType.Name))
                {
                    info.TypeName = representedType.Name;
                    return info;
                }
            }

            info.TypeName = "LegendComponent_" + legendComponent.Id.IntegerValue;
            return info;
        }

        /// <summary>
        /// Resolves represented type id from legend component parameter.
        /// </summary>
        private static ElementId ResolveRepresentedTypeId(Element legendComponent)
        {
            if (legendComponent == null || !legendComponent.IsValidObject)
            {
                return ElementId.InvalidElementId;
            }

            Parameter representedParameter = legendComponent.get_Parameter(BuiltInParameter.LEGEND_COMPONENT);

            if (representedParameter == null)
            {
                return ElementId.InvalidElementId;
            }

            if (representedParameter.StorageType == StorageType.ElementId)
            {
                return representedParameter.AsElementId();
            }

            if (representedParameter.StorageType == StorageType.Integer)
            {
                return new ElementId(representedParameter.AsInteger());
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Applies fixed export options:
        /// CurrentView + Zoom 100% + PNG + DPI 600.
        /// </summary>
        private static void ExportCurrentViewAsPng(Document document, string filePathWithoutExtension)
        {
            ImageExportOptions options = new ImageExportOptions();
            options.ExportRange = ExportRange.CurrentView;
            options.ZoomType = ZoomFitType.Zoom;
            options.Zoom = 100;
            options.HLRandWFViewsFileType = ImageFileType.PNG;
            options.ShadowViewsFileType = ImageFileType.PNG;
            options.ImageResolution = ImageResolution.DPI_600;
            options.FilePath = filePathWithoutExtension;

            document.ExportImage(options);
        }

        /// <summary>
        /// Disables temporary isolate mode after each export iteration.
        /// </summary>
        private static void TryDisableTemporaryIsolation(Document document, View legendView)
        {
            try
            {
                using (Transaction resetTransaction = new Transaction(document, "Disable temporary isolate"))
                {
                    resetTransaction.Start();
                    legendView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    resetTransaction.Commit();
                }
            }
            catch
            {
                // Isolation reset should not stop export flow.
            }
        }

        /// <summary>
        /// Returns open UIView for active legend when available.
        /// </summary>
        private static UIView FindOpenUiView(UIDocument uiDocument, ElementId viewId)
        {
            if (uiDocument == null || viewId == ElementId.InvalidElementId)
            {
                return null;
            }

            IList<UIView> openViews = uiDocument.GetOpenUIViews();

            for (int i = 0; i < openViews.Count; i++)
            {
                if (openViews[i].ViewId == viewId)
                {
                    return openViews[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Captures current PNG files before export iteration.
        /// </summary>
        private static HashSet<string> CapturePngFileSnapshot(string folderPath)
        {
            HashSet<string> snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return snapshot;
            }

            string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < files.Length; i++)
            {
                snapshot.Add(files[i]);
            }

            return snapshot;
        }

        /// <summary>
        /// Resolves exported PNG path and tolerates internal Revit renaming.
        /// </summary>
        private static string ResolveExportedPngPathAfterExport(
            string folderPath,
            string expectedFilePath,
            DateTime exportStartUtc,
            HashSet<string> filesBeforeExport)
        {
            if (File.Exists(expectedFilePath))
            {
                return expectedFilePath;
            }

            string[] allPngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
            string newestNewFile = string.Empty;
            DateTime newestWriteTime = DateTime.MinValue;

            for (int i = 0; i < allPngFiles.Length; i++)
            {
                string candidate = allPngFiles[i];

                if (filesBeforeExport.Contains(candidate))
                {
                    continue;
                }

                DateTime writeTime = File.GetLastWriteTimeUtc(candidate);

                if (writeTime < exportStartUtc.AddSeconds(-1))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(newestNewFile) || writeTime > newestWriteTime)
                {
                    newestNewFile = candidate;
                    newestWriteTime = writeTime;
                }
            }

            return newestNewFile;
        }

        /// <summary>
        /// Ensures final file name equals target type name (without auto-renaming leftovers).
        /// </summary>
        private static string EnsureExpectedFileName(string actualPath, string expectedPath)
        {
            if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return expectedPath;
            }

            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }

            File.Move(actualPath, expectedPath);
            return expectedPath;
        }

        /// <summary>
        /// Validates that file name is Windows-compatible without changing source type name.
        /// </summary>
        private static bool TryValidateWindowsFileName(string fileName, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                reason = "Имя типоразмера пустое. Экспорт невозможен.";
                return false;
            }

            string value = fileName;

            if (value.EndsWith(" ", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal))
            {
                reason = "Имя типоразмера заканчивается точкой или пробелом, что запрещено в Windows.";
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            List<string> invalidSymbols = new List<string>();

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (current == invalidChars[j])
                    {
                        string display = current == '\\' ? "\\" : current.ToString();

                        if (!invalidSymbols.Contains(display))
                        {
                            invalidSymbols.Add(display);
                        }

                        break;
                    }
                }
            }

            if (invalidSymbols.Count > 0)
            {
                reason = "Имя типоразмера содержит запрещенные символы Windows: " + string.Join(" ", invalidSymbols) + ".";
                return false;
            }

            if (ReservedWindowsFileNames.Contains(value))
            {
                reason = "Имя типоразмера совпадает с зарезервированным именем Windows: " + value + ".";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds unique view name by appending numeric suffix.
        /// </summary>
        private static string BuildUniqueLegendViewName(Document document, string baseName)
        {
            string candidate = baseName;
            int suffix = 1;

            while (IsViewNameExists(document, candidate))
            {
                candidate = baseName + "_" + suffix;
                suffix++;
            }

            return candidate;
        }

        private static bool IsViewNameExists(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;

                if (view == null)
                {
                    continue;
                }

                if (string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Internal DTO for sorting routine.
        /// </summary>
        private class LegendComponentSortItem
        {
            public Element Element { get; set; }

            public double Vertical { get; set; }
        }

        private class LegendRepresentedTypeInfo
        {
            public ElementId RepresentedTypeId { get; set; }

            public string TypeName { get; set; }
        }
    }

    /// <summary>
    /// Export result DTO for command-level summary.
    /// </summary>
    public class LegendComponentImageExportResult
    {
        private readonly List<string> _exportedFiles = new List<string>();
        private readonly List<string> _skippedDetails = new List<string>();
        private readonly List<LegendComponentImageExportReportItem> _reportItems = new List<LegendComponentImageExportReportItem>();
        private readonly List<LegendComponentImageProblemNameIssue> _problemNameIssues = new List<LegendComponentImageProblemNameIssue>();

        public int TotalLegendComponentsOnView { get; set; }

        public int ExportedCount
        {
            get { return _exportedFiles.Count; }
        }

        public int SkippedCount
        {
            get { return _skippedDetails.Count; }
        }

        public int ProblematicNamesCount
        {
            get { return _problemNameIssues.Count; }
        }

        public string FatalError { get; set; }

        public IReadOnlyList<string> ExportedFiles
        {
            get { return _exportedFiles.AsReadOnly(); }
        }

        public IReadOnlyList<string> SkippedDetails
        {
            get { return _skippedDetails.AsReadOnly(); }
        }

        public IReadOnlyList<LegendComponentImageExportReportItem> ReportItems
        {
            get { return _reportItems.AsReadOnly(); }
        }

        public IReadOnlyList<LegendComponentImageProblemNameIssue> ProblemNameIssues
        {
            get { return _problemNameIssues.AsReadOnly(); }
        }

        public void AddExported(string originalTypeName, string exportedFileName, string filePath)
        {
            _exportedFiles.Add(filePath);

            _reportItems.Add(new LegendComponentImageExportReportItem
            {
                OriginalTypeName = originalTypeName ?? string.Empty,
                ExportedFileName = exportedFileName ?? string.Empty,
                Status = "Exported",
                ErrorText = string.Empty
            });
        }

        public void AddSkipped(string typeName, string exportedFileName, string reason)
        {
            string safeTypeName = typeName ?? string.Empty;
            string safeReason = reason ?? string.Empty;
            _skippedDetails.Add(safeTypeName + " => " + safeReason);

            _reportItems.Add(new LegendComponentImageExportReportItem
            {
                OriginalTypeName = safeTypeName,
                ExportedFileName = exportedFileName ?? string.Empty,
                Status = "Skipped",
                ErrorText = safeReason
            });
        }

        public void AddInvalidNameIssue(ElementId representedTypeId, string typeName, string reason)
        {
            _problemNameIssues.Add(new LegendComponentImageProblemNameIssue
            {
                RepresentedTypeId = representedTypeId,
                TypeName = typeName ?? string.Empty,
                ErrorText = reason ?? string.Empty
            });
        }
    }

    /// <summary>
    /// Report row DTO for image export diagnostics.
    /// </summary>
    public class LegendComponentImageExportReportItem
    {
        public string OriginalTypeName { get; set; }

        public string ExportedFileName { get; set; }

        public string Status { get; set; }

        public string ErrorText { get; set; }
    }

    /// <summary>
    /// DTO for problematic type names that cannot be exported as Windows file names.
    /// </summary>
    public class LegendComponentImageProblemNameIssue
    {
        public ElementId RepresentedTypeId { get; set; }

        public string TypeName { get; set; }

        public string ErrorText { get; set; }
    }
}