using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Views
{
    /// <summary>
    /// Экспортирует PNG по элементам чертежного вида через временную изоляцию.
    /// </summary>
    public class DraftingElementImageExportService
    {
        // Блок параметров длины линий для предпросмотра PNG.
        private const double PreviewLineLengthMillimeters = 2500.0;
        private const double RestoreLineLengthMillimeters = 1000.0;

        private static readonly HashSet<string> TechnicalLineStyleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Эскиз",
            "Вне пределов",
            "<Sketch>",
            "<Beyond>",
            "Sketch",
            "Beyond"
        };

        private static readonly HashSet<string> ReservedWindowsFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public DraftingImageExportResult ExportLineStyles(UIDocument uiDocument, ViewDrafting sourceView, string outputFolder)
        {
            List<Element> elements = CollectLineElements(uiDocument, sourceView);
            HashSet<int> technicalLineElementIds = CollectTechnicalLineElementIds(uiDocument, sourceView);
            List<DetailCurve> editableCurves = CollectEditableLineCurves(elements);

            if (uiDocument != null && uiDocument.Document != null && editableCurves.Count > 0)
            {
                ApplyLineLength(uiDocument.Document, editableCurves, PreviewLineLengthMillimeters);
            }

            try
            {
                return Export(
                    uiDocument,
                    sourceView,
                    elements,
                    ResolveLineStyleName,
                    outputFolder,
                    technicalLineElementIds);
            }
            finally
            {
                if (uiDocument != null && uiDocument.Document != null && editableCurves.Count > 0)
                {
                    ApplyLineLength(uiDocument.Document, editableCurves, RestoreLineLengthMillimeters);
                }
            }
        }

        public DraftingImageExportResult ExportFillPatterns(UIDocument uiDocument, ViewDrafting sourceView, string outputFolder)
        {
            List<Element> elements = CollectFillElements(uiDocument, sourceView);

            return Export(
                uiDocument,
                sourceView,
                elements,
                ResolveFillPatternName,
                outputFolder,
                null);
        }

        private DraftingImageExportResult Export(
            UIDocument uiDocument,
            ViewDrafting sourceView,
            List<Element> elements,
            Func<Element, string> nameResolver,
            string outputFolder,
            HashSet<int> hiddenTechnicalElementIds)
        {
            DraftingImageExportResult result = new DraftingImageExportResult();

            if (uiDocument == null || uiDocument.Document == null)
            {
                result.FatalError = "Активный документ недоступен.";
                return result;
            }

            if (sourceView == null)
            {
                result.FatalError = "Вид для экспорта не найден.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                result.FatalError = "Папка экспорта не задана.";
                return result;
            }

            Directory.CreateDirectory(outputFolder);

            Document document = uiDocument.Document;
            result.TotalCount = elements != null ? elements.Count : 0;

            if (elements == null || elements.Count == 0)
            {
                result.FatalError = "На целевом виде не найдены элементы для экспорта.";
                return result;
            }

            UIView uiView = FindOpenUiView(uiDocument, sourceView.Id);

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];

                if (element == null || !element.IsValidObject)
                {
                    result.AddSkipped("Элемент_невалиден", "Элемент недействителен.");
                    continue;
                }

                string rawName = nameResolver(element);

                if (string.IsNullOrWhiteSpace(rawName))
                {
                    rawName = "Element_" + element.Id.IntegerValue;
                }

                string reason;

                if (!TryValidateWindowsFileName(rawName, out reason))
                {
                    result.AddSkipped(rawName, reason);
                    continue;
                }

                string expectedPath = Path.Combine(outputFolder, rawName + ".png");
                string expectedPathWithoutExtension = Path.Combine(outputFolder, rawName);
                bool isolated = false;

                try
                {
                    HashSet<string> filesBefore = CapturePngFileSnapshot(outputFolder);

                    if (File.Exists(expectedPath))
                    {
                        File.Delete(expectedPath);
                    }

                    using (Transaction isolateTransaction = new Transaction(document, "Temporary isolate element"))
                    {
                        isolateTransaction.Start();
                        sourceView.IsolateElementsTemporary(new List<ElementId> { element.Id });

                        HideTechnicalElementsInTemporaryMode(sourceView, element.Id.IntegerValue, hiddenTechnicalElementIds);
                        isolateTransaction.Commit();
                    }

                    isolated = true;

                    if (uiDocument.ActiveView == null || uiDocument.ActiveView.Id != sourceView.Id)
                    {
                        uiDocument.ActiveView = sourceView;
                    }

                    uiDocument.RefreshActiveView();

                    if (uiView != null)
                    {
                        uiView.ZoomToFit();
                    }

                    DateTime exportStart = DateTime.UtcNow;
                    ExportCurrentViewAsPng(document, expectedPathWithoutExtension);

                    string exportedPath = ResolveExportedPngPathAfterExport(
                        outputFolder,
                        expectedPath,
                        exportStart,
                        filesBefore);

                    if (string.IsNullOrWhiteSpace(exportedPath))
                    {
                        throw new InvalidOperationException("PNG-файл не найден после экспорта.");
                    }

                    string finalPath = EnsureExpectedFileName(exportedPath, expectedPath);
                    result.AddExported(rawName, finalPath);
                }
                catch (Exception exception)
                {
                    result.AddSkipped(rawName, exception.Message);
                }
                finally
                {
                    if (isolated)
                    {
                        TryDisableTemporaryIsolation(document, sourceView);
                    }
                }
            }

            return result;
        }

        private static List<Element> CollectLineElements(UIDocument uiDocument, ViewDrafting sourceView)
        {
            List<ElementSortItem> sortItems = new List<ElementSortItem>();
            Document document = uiDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(document, sourceView.Id)
                .OfClass(typeof(CurveElement));

            foreach (Element element in collector)
            {
                DetailCurve curve = element as DetailCurve;

                if (curve == null)
                {
                    continue;
                }

                if (curve.OwnerViewId != sourceView.Id)
                {
                    continue;
                }

                string styleName = ResolveLineStyleName(curve);

                if (IsTechnicalLineStyle(styleName))
                {
                    continue;
                }

                double x;
                double y;
                ResolveCoordinates(curve, sourceView, out x, out y);

                sortItems.Add(new ElementSortItem
                {
                    Element = curve,
                    X = x,
                    Y = y
                });
            }

            return ConvertSortItemsToElements(sortItems);
        }

        private static HashSet<int> CollectTechnicalLineElementIds(UIDocument uiDocument, ViewDrafting sourceView)
        {
            HashSet<int> ids = new HashSet<int>();
            Document document = uiDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(document, sourceView.Id)
                .OfClass(typeof(CurveElement));

            foreach (Element element in collector)
            {
                DetailCurve curve = element as DetailCurve;

                if (curve == null || curve.OwnerViewId != sourceView.Id)
                {
                    continue;
                }

                string styleName = ResolveLineStyleName(curve);

                if (!IsTechnicalLineStyle(styleName))
                {
                    continue;
                }

                ids.Add(curve.Id.IntegerValue);
            }

            return ids;
        }

        private static List<Element> CollectFillElements(UIDocument uiDocument, ViewDrafting sourceView)
        {
            List<ElementSortItem> sortItems = new List<ElementSortItem>();
            Document document = uiDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(document, sourceView.Id)
                .OfClass(typeof(FilledRegion));

            foreach (Element element in collector)
            {
                FilledRegion region = element as FilledRegion;

                if (region == null)
                {
                    continue;
                }

                if (region.OwnerViewId != sourceView.Id)
                {
                    continue;
                }

                double x;
                double y;
                ResolveCoordinates(region, sourceView, out x, out y);

                sortItems.Add(new ElementSortItem
                {
                    Element = region,
                    X = x,
                    Y = y
                });
            }

            return ConvertSortItemsToElements(sortItems);
        }

        private static List<Element> ConvertSortItemsToElements(List<ElementSortItem> sortItems)
        {
            sortItems.Sort(delegate (ElementSortItem left, ElementSortItem right)
            {
                int yCompare = right.Y.CompareTo(left.Y);

                if (yCompare != 0)
                {
                    return yCompare;
                }

                int xCompare = left.X.CompareTo(right.X);

                if (xCompare != 0)
                {
                    return xCompare;
                }

                return left.Element.Id.IntegerValue.CompareTo(right.Element.Id.IntegerValue);
            });

            List<Element> result = new List<Element>();

            for (int i = 0; i < sortItems.Count; i++)
            {
                result.Add(sortItems[i].Element);
            }

            return result;
        }

        private static List<DetailCurve> CollectEditableLineCurves(List<Element> elements)
        {
            List<DetailCurve> curves = new List<DetailCurve>();

            if (elements == null)
            {
                return curves;
            }

            for (int i = 0; i < elements.Count; i++)
            {
                DetailCurve curve = elements[i] as DetailCurve;

                if (curve == null || !curve.IsValidObject)
                {
                    continue;
                }

                curves.Add(curve);
            }

            return curves;
        }

        private static void ApplyLineLength(Document document, List<DetailCurve> curves, double targetLengthMillimeters)
        {
            if (document == null || curves == null || curves.Count == 0)
            {
                return;
            }

            double targetLength = UnitUtils.ConvertToInternalUnits(targetLengthMillimeters, UnitTypeId.Millimeters);

            using (Transaction transaction = new Transaction(document, "Set preview line length"))
            {
                transaction.Start();

                for (int i = 0; i < curves.Count; i++)
                {
                    DetailCurve curve = curves[i];

                    if (curve == null || !curve.IsValidObject)
                    {
                        continue;
                    }

                    TrySetDetailCurveLength(curve, targetLength);
                }

                transaction.Commit();
            }
        }

        private static void TrySetDetailCurveLength(DetailCurve curve, double targetLengthInternalUnits)
        {
            if (curve == null || !curve.IsValidObject || targetLengthInternalUnits <= 0)
            {
                return;
            }

            Line line = curve.GeometryCurve as Line;

            if (line == null)
            {
                return;
            }

            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);
            XYZ direction = end - start;

            if (direction == null || direction.GetLength() < 1e-9)
            {
                return;
            }

            XYZ normalizedDirection = direction.Normalize();
            XYZ middlePoint = (start + end) * 0.5;
            XYZ halfVector = normalizedDirection * (targetLengthInternalUnits / 2.0);
            XYZ newStart = middlePoint - halfVector;
            XYZ newEnd = middlePoint + halfVector;

            curve.GeometryCurve = Line.CreateBound(newStart, newEnd);
        }

        private static void ResolveCoordinates(Element element, View view, out double x, out double y)
        {
            x = 0;
            y = 0;

            LocationPoint point = element.Location as LocationPoint;

            if (point != null)
            {
                x = point.Point.X;
                y = point.Point.Y;
                return;
            }

            BoundingBoxXYZ box = element.get_BoundingBox(view);

            if (box != null)
            {
                x = (box.Min.X + box.Max.X) / 2.0;
                y = (box.Min.Y + box.Max.Y) / 2.0;
            }
        }

        private static string ResolveLineStyleName(Element element)
        {
            CurveElement curve = element as CurveElement;

            if (curve == null)
            {
                return string.Empty;
            }

            GraphicsStyle style = curve.LineStyle as GraphicsStyle;

            if (style != null && style.GraphicsStyleCategory != null)
            {
                return style.GraphicsStyleCategory.Name ?? string.Empty;
            }

            return curve.LineStyle != null ? (curve.LineStyle.Name ?? string.Empty) : string.Empty;
        }

        private static bool IsTechnicalLineStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
            {
                return false;
            }

            return TechnicalLineStyleNames.Contains(styleName.Trim());
        }

        private static void HideTechnicalElementsInTemporaryMode(
            View view,
            int activeElementId,
            HashSet<int> hiddenTechnicalElementIds)
        {
            if (view == null || hiddenTechnicalElementIds == null || hiddenTechnicalElementIds.Count == 0)
            {
                return;
            }

            List<ElementId> idsToHide = new List<ElementId>();

            foreach (int elementId in hiddenTechnicalElementIds)
            {
                if (elementId == activeElementId)
                {
                    continue;
                }

                idsToHide.Add(new ElementId(elementId));
            }

            if (idsToHide.Count == 0)
            {
                return;
            }

            view.HideElementsTemporary(idsToHide);
        }

        private static string ResolveFillPatternName(Element element)
        {
            FilledRegion region = element as FilledRegion;

            if (region == null)
            {
                return string.Empty;
            }

            ElementType type = region.Document.GetElement(region.GetTypeId()) as ElementType;

            return type != null ? (type.Name ?? string.Empty) : string.Empty;
        }

        private static UIView FindOpenUiView(UIDocument uiDocument, ElementId viewId)
        {
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

        private static void TryDisableTemporaryIsolation(Document document, View view)
        {
            try
            {
                using (Transaction transaction = new Transaction(document, "Disable temporary isolate"))
                {
                    transaction.Start();
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    transaction.Commit();
                }
            }
            catch
            {
                // Не блокируем процесс экспорта, если снять изоляцию не удалось.
            }
        }

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

        private static bool TryValidateWindowsFileName(string fileName, out string reason)
        {
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                reason = "Имя файла пустое.";
                return false;
            }

            string value = fileName;

            if (value.EndsWith(" ", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal))
            {
                reason = "Имя заканчивается точкой или пробелом.";
                return false;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (current == invalidChars[j])
                    {
                        reason = "Имя содержит запрещенный символ Windows: '" + current + "'.";
                        return false;
                    }
                }
            }

            if (ReservedWindowsFileNames.Contains(value))
            {
                reason = "Имя совпадает с зарезервированным именем Windows: " + value + ".";
                return false;
            }

            return true;
        }

        private class ElementSortItem
        {
            public Element Element { get; set; }

            public double X { get; set; }

            public double Y { get; set; }
        }
    }

    public class DraftingImageExportResult
    {
        private readonly List<string> _skippedDetails = new List<string>();

        public int TotalCount { get; set; }

        public int ExportedCount { get; private set; }

        public string FatalError { get; set; }

        public IReadOnlyList<string> SkippedDetails
        {
            get { return _skippedDetails.AsReadOnly(); }
        }

        public int SkippedCount
        {
            get { return _skippedDetails.Count; }
        }

        public void AddExported(string name, string filePath)
        {
            ExportedCount++;
        }

        public void AddSkipped(string name, string reason)
        {
            _skippedDetails.Add((name ?? string.Empty) + " => " + (reason ?? string.Empty));
        }
    }
}
