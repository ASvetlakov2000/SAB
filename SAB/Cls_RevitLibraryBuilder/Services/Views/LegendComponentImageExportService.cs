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
                    result.AddSkipped("LegendComponent_<invalid>", "LegendComponent_invalid", "Legend component is invalid before export.");
                    continue;
                }

                string originalExportName = ResolveLegendComponentExportName(document, legendComponent);
                bool wasNameSanitized;
                string normalizedBaseName = NormalizeExportFileName(originalExportName, out wasNameSanitized);

                if (string.IsNullOrWhiteSpace(normalizedBaseName))
                {
                    normalizedBaseName = "LegendComponent_" + legendComponent.Id.IntegerValue;
                    bool ignored;
                    normalizedBaseName = NormalizeExportFileName(normalizedBaseName, out ignored);
                }

                string uniquePngPath = BuildUniquePngPath(outputFolderPath, normalizedBaseName);
                string intendedBaseName = Path.GetFileNameWithoutExtension(uniquePngPath);
                string exportFilePathWithoutExtension = Path.Combine(outputFolderPath, intendedBaseName);

                bool temporaryIsolationEnabled = false;
                HashSet<string> filesBeforeExport = CapturePngFileSnapshot(outputFolderPath);

                try
                {
                    // Block responsible for turning on temporary isolation for one component.
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
                    ExportCurrentViewAsPng(document, exportFilePathWithoutExtension);

                    // Block responsible for finding real exported file path even if Revit changed file name.
                    string exportedPath = ResolveExportedPngPathAfterExport(
                        outputFolderPath,
                        intendedBaseName,
                        exportStartUtc,
                        filesBeforeExport);

                    if (string.IsNullOrWhiteSpace(exportedPath))
                    {
                        throw new InvalidOperationException("PNG file was not found after export.");
                    }

                    result.AddExported(originalExportName, intendedBaseName, exportedPath, wasNameSanitized);
                }
                catch (Exception exception)
                {
                    result.AddSkipped(originalExportName, intendedBaseName, exception.Message);
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
        /// Reads represented type for legend component and returns export file name.
        /// </summary>
        private static string ResolveLegendComponentExportName(Document document, Element legendComponent)
        {
            Parameter representedParameter = legendComponent.get_Parameter(BuiltInParameter.LEGEND_COMPONENT);

            if (representedParameter != null)
            {
                ElementId representedTypeId = ElementId.InvalidElementId;

                if (representedParameter.StorageType == StorageType.ElementId)
                {
                    representedTypeId = representedParameter.AsElementId();
                }
                else if (representedParameter.StorageType == StorageType.Integer)
                {
                    representedTypeId = new ElementId(representedParameter.AsInteger());
                }

                if (representedTypeId != ElementId.InvalidElementId)
                {
                    ElementType representedType = document.GetElement(representedTypeId) as ElementType;

                    if (representedType != null && !string.IsNullOrWhiteSpace(representedType.Name))
                    {
                        return representedType.Name;
                    }
                }
            }

            return "LegendComponent_" + legendComponent.Id.IntegerValue;
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
        /// Creates unique PNG path by adding suffixes if file already exists.
        /// </summary>
        private static string BuildUniquePngPath(string folderPath, string baseName)
        {
            bool ignored;
            string sanitizedBaseName = NormalizeExportFileName(baseName, out ignored);

            if (string.IsNullOrWhiteSpace(sanitizedBaseName))
            {
                sanitizedBaseName = "LegendComponent";
            }

            int suffix = 0;

            while (true)
            {
                string name = suffix == 0
                    ? sanitizedBaseName
                    : sanitizedBaseName + "_" + suffix;

                string candidatePath = Path.Combine(folderPath, name + ".png");

                if (!File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                suffix++;
            }
        }

        /// <summary>
        /// Captures current file snapshot before export.
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
        /// Resolves exported PNG path and tolerates Revit filename changes.
        /// </summary>
        private static string ResolveExportedPngPathAfterExport(
            string folderPath,
            string intendedBaseName,
            DateTime exportStartUtc,
            HashSet<string> filesBeforeExport)
        {
            string exactPath = Path.Combine(folderPath, intendedBaseName + ".png");

            if (File.Exists(exactPath))
            {
                return exactPath;
            }

            string[] allPngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

            string newestNewFile = string.Empty;
            DateTime newestWriteTime = DateTime.MinValue;

            // Block responsible for finding files newly created by current export iteration.
            for (int i = 0; i < allPngFiles.Length; i++)
            {
                string candidate = allPngFiles[i];

                if (filesBeforeExport != null && filesBeforeExport.Contains(candidate))
                {
                    continue;
                }

                DateTime writeTime = File.GetLastWriteTimeUtc(candidate);

                if (writeTime < exportStartUtc.AddSeconds(-1))
                {
                    continue;
                }

                if (newestNewFile.Length == 0 || writeTime > newestWriteTime)
                {
                    newestNewFile = candidate;
                    newestWriteTime = writeTime;
                }
            }

            if (!string.IsNullOrWhiteSpace(newestNewFile))
            {
                return newestNewFile;
            }

            string[] prefixedCandidates = Directory.GetFiles(folderPath, intendedBaseName + "*.png", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < prefixedCandidates.Length; i++)
            {
                string candidate = prefixedCandidates[i];
                DateTime writeTime = File.GetLastWriteTimeUtc(candidate);

                if (writeTime < exportStartUtc.AddSeconds(-1))
                {
                    continue;
                }

                if (newestNewFile.Length == 0 || writeTime > newestWriteTime)
                {
                    newestNewFile = candidate;
                    newestWriteTime = writeTime;
                }
            }

            return newestNewFile;
        }

        /// <summary>
        /// Normalizes export file names for Windows file system and Revit image export behavior.
        /// </summary>
        private static string NormalizeExportFileName(string rawName, out bool wasSanitized)
        {
            string original = rawName ?? string.Empty;
            string value = original.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                wasSanitized = !string.IsNullOrWhiteSpace(original);
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = value.ToCharArray();

            for (int i = 0; i < buffer.Length; i++)
            {
                char current = buffer[i];

                bool replaceWithUnderscore = false;

                if (current == '.')
                {
                    // Block responsible for preventing Revit file name truncation on dots in base file name.
                    replaceWithUnderscore = true;
                }
                else
                {
                    for (int j = 0; j < invalidChars.Length; j++)
                    {
                        if (current == invalidChars[j])
                        {
                            replaceWithUnderscore = true;
                            break;
                        }
                    }
                }

                if (replaceWithUnderscore)
                {
                    buffer[i] = '_';
                }
            }

            string normalized = new string(buffer).Trim();

            while (normalized.Contains("__"))
            {
                normalized = normalized.Replace("__", "_");
            }

            normalized = normalized.Trim(' ', '.');

            if (normalized.Length > 150)
            {
                normalized = normalized.Substring(0, 150).Trim(' ', '.');
            }

            if (ReservedWindowsFileNames.Contains(normalized))
            {
                normalized += "_file";
            }

            wasSanitized = !string.Equals(original.Trim(), normalized, StringComparison.Ordinal);
            return normalized;
        }

        /// <summary>
        /// Internal DTO for sorting routine.
        /// </summary>
        private class LegendComponentSortItem
        {
            public Element Element { get; set; }

            public double Vertical { get; set; }
        }
    }

    /// <summary>
    /// Export result DTO for command-level summary.
    /// </summary>
    public class LegendComponentImageExportResult
    {
        private readonly List<string> _exportedFiles = new List<string>();
        private readonly List<string> _skippedDetails = new List<string>();
        private readonly List<string> _renamedDetails = new List<string>();
        private readonly List<LegendComponentImageExportReportItem> _reportItems = new List<LegendComponentImageExportReportItem>();

        public int TotalLegendComponentsOnView { get; set; }

        public int ExportedCount
        {
            get { return _exportedFiles.Count; }
        }

        public int SkippedCount
        {
            get { return _skippedDetails.Count; }
        }

        public int RenamedCount
        {
            get { return _renamedDetails.Count; }
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

        public IReadOnlyList<string> RenamedDetails
        {
            get { return _renamedDetails.AsReadOnly(); }
        }

        public IReadOnlyList<LegendComponentImageExportReportItem> ReportItems
        {
            get { return _reportItems.AsReadOnly(); }
        }

        public void AddExported(string originalName, string normalizedName, string filePath, bool wasNameSanitized)
        {
            _exportedFiles.Add(filePath);

            string exportedFileName = Path.GetFileName(filePath) ?? string.Empty;

            if (wasNameSanitized)
            {
                _renamedDetails.Add((originalName ?? string.Empty) + " -> " + exportedFileName);
            }

            _reportItems.Add(new LegendComponentImageExportReportItem
            {
                OriginalTypeName = originalName ?? string.Empty,
                NormalizedFileName = normalizedName ?? string.Empty,
                ExportedFileName = exportedFileName,
                ExportedFilePath = filePath ?? string.Empty,
                Status = "Exported",
                ErrorText = wasNameSanitized ? "File name was normalized for safe export." : string.Empty
            });
        }

        public void AddSkipped(string itemName, string normalizedName, string reason)
        {
            string safeName = itemName ?? string.Empty;
            string safeReason = reason ?? string.Empty;

            _skippedDetails.Add(safeName + " => " + safeReason);

            _reportItems.Add(new LegendComponentImageExportReportItem
            {
                OriginalTypeName = safeName,
                NormalizedFileName = normalizedName ?? string.Empty,
                ExportedFileName = string.Empty,
                ExportedFilePath = string.Empty,
                Status = "Skipped",
                ErrorText = safeReason
            });
        }
    }

    /// <summary>
    /// Report row DTO for image export diagnostics.
    /// </summary>
    public class LegendComponentImageExportReportItem
    {
        public string OriginalTypeName { get; set; }

        public string NormalizedFileName { get; set; }

        public string ExportedFileName { get; set; }

        public string ExportedFilePath { get; set; }

        public string Status { get; set; }

        public string ErrorText { get; set; }
    }
}