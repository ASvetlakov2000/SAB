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
                    result.AddSkipped("LegendComponent_<invalid>", "Legend component is invalid before export.");
                    continue;
                }

                string exportName = ResolveLegendComponentExportName(document, legendComponent);
                string safeFileName = SanitizeFileNamePart(exportName);

                if (string.IsNullOrWhiteSpace(safeFileName))
                {
                    safeFileName = "LegendComponent_" + legendComponent.Id.IntegerValue;
                }

                string uniquePngPath = BuildUniquePngPath(outputFolderPath, safeFileName);
                string exportFilePathWithoutExtension = Path.Combine(
                    outputFolderPath,
                    Path.GetFileNameWithoutExtension(uniquePngPath));

                bool temporaryIsolationEnabled = false;

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

                    // If Revit saved file using alternate naming convention, resolve actual output.
                    string exportedPath = ResolveExportedPngPath(
                        outputFolderPath,
                        Path.GetFileNameWithoutExtension(uniquePngPath),
                        exportStartUtc);

                    if (string.IsNullOrWhiteSpace(exportedPath))
                    {
                        throw new InvalidOperationException("PNG file was not found after export.");
                    }

                    result.AddExported(exportedPath);
                }
                catch (Exception exception)
                {
                    result.AddSkipped(exportName, exception.Message);
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
            string sanitizedBaseName = SanitizeFileNamePart(baseName);

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
        /// Resolves exported PNG path if Revit appended additional parts to file name.
        /// </summary>
        private static string ResolveExportedPngPath(
            string folderPath,
            string baseFileNameWithoutExtension,
            DateTime exportStartUtc)
        {
            string exactPath = Path.Combine(folderPath, baseFileNameWithoutExtension + ".png");

            if (File.Exists(exactPath))
            {
                return exactPath;
            }

            string[] candidateFiles = Directory.GetFiles(folderPath, baseFileNameWithoutExtension + "*.png");

            if (candidateFiles.Length > 0)
            {
                string newestCandidate = string.Empty;
                DateTime newestWriteTime = DateTime.MinValue;

                for (int i = 0; i < candidateFiles.Length; i++)
                {
                    DateTime writeTime = File.GetLastWriteTimeUtc(candidateFiles[i]);

                    if (writeTime < exportStartUtc.AddSeconds(-1))
                    {
                        continue;
                    }

                    if (newestCandidate.Length == 0 || writeTime > newestWriteTime)
                    {
                        newestCandidate = candidateFiles[i];
                        newestWriteTime = writeTime;
                    }
                }

                if (!string.IsNullOrWhiteSpace(newestCandidate))
                {
                    return newestCandidate;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Sanitizes file name part against OS-forbidden characters.
        /// </summary>
        private static string SanitizeFileNamePart(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = rawName.ToCharArray();

            for (int i = 0; i < buffer.Length; i++)
            {
                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (buffer[i] == invalidChars[j])
                    {
                        buffer[i] = '_';
                        break;
                    }
                }
            }

            return new string(buffer).Trim();
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

        public int TotalLegendComponentsOnView { get; set; }

        public int ExportedCount
        {
            get { return _exportedFiles.Count; }
        }

        public int SkippedCount
        {
            get { return _skippedDetails.Count; }
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

        public void AddExported(string filePath)
        {
            _exportedFiles.Add(filePath);
        }

        public void AddSkipped(string itemName, string reason)
        {
            _skippedDetails.Add(itemName + " => " + reason);
        }
    }
}
