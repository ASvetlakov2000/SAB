using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Csv;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RevitLibraryBuilder.Services.Views
{
    /// <summary>
    /// Сервис создания легенд по системным типам и экспорта PNG "пирогов".
    /// </summary>
    public class SystemTypePieLegendExportService
    {
        private const string PiePrefix = "Пирог";
        private const int LegendScale = 5;
        private const int ExportImageWidthPixels = 500;
        private const int BaseLengthMillimeters = 100;
        private const int PieHeightMillimeters = 24;

        private static readonly char[] ProhibitedViewNameCharacters = new char[] { '{', '}', '[', ']', ';', '<', '>', '?', '^', '~' };

        private readonly CsvTableService _csvTableService;

        public SystemTypePieLegendExportService()
        {
            _csvTableService = new CsvTableService();
        }

        public SystemTypePieLegendExportResult Export(UIDocument uiDocument, string outputFolder)
        {
            string stage = "Initialize";

            if (uiDocument == null)
            {
                throw new ArgumentNullException(nameof(uiDocument));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder is empty.");
            }

            try
            {
                stage = "Read Document";
                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    throw new InvalidOperationException("Document or active view is not available.");
                }

                stage = "Prepare Output Folder";
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                stage = "Collect System Types";
                List<SystemTypePieItem> items = CollectSystemTypeItems(document);

                if (items.Count == 0)
                {
                    throw new InvalidOperationException("Системные типы в целевых категориях не найдены.");
                }

                stage = "Find Legend Prototype";
                View legendPrototype = FindLegendPrototype(document);

                if (legendPrototype == null)
                {
                    throw new InvalidOperationException(
                        "В проекте не найден вид Легенда. Создайте вручную хотя бы один вид Легенда и повторите команду.");
                }

                stage = "Resolve Types and Patterns";
                ElementId defaultFilledRegionTypeId = GetDefaultFilledRegionTypeId(document);
                ElementId solidFillPatternId = GetSolidFillPatternId(document);
                List<SystemTypePieItem> preparedItems = new List<SystemTypePieItem>();
                // Блок создания легенд построчно в отдельных транзакциях,
                // чтобы ошибка одного типа не останавливала весь процесс.
                for (int i = 0; i < items.Count; i++)
                {
                    SystemTypePieItem item = items[i];

                    using (Transaction transaction = new Transaction(document, "Create pie legend: " + item.LegendName))
                    {
                        try
                        {
                            stage = "Create Legend: " + item.LegendName;
                            transaction.Start();

                            View legendView = DuplicateLegend(document, legendPrototype, item.LegendName);

                            if (legendView == null)
                            {
                                transaction.RollBack();
                                continue;
                            }

                            ConfigureLegendView(legendView);
                            ClearViewContents(document, legendView);

                            DrawPieForType(document, legendView, item, defaultFilledRegionTypeId, solidFillPatternId);

                            transaction.Commit();

                            item.LegendViewId = legendView.Id;
                            preparedItems.Add(item);
                        }
                        catch (Exception)
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                            {
                                transaction.RollBack();
                            }
                        }
                    }
                }

                stage = "Export PNG";
                int exportedCount = 0;
                int missingExportCount = 0;
                ElementId originalActiveViewId = uiDocument.ActiveView != null ? uiDocument.ActiveView.Id : ElementId.InvalidElementId;

                try
                {
                    for (int i = 0; i < preparedItems.Count; i++)
                    {
                        SystemTypePieItem item = preparedItems[i];
                        View view = document.GetElement(item.LegendViewId) as View;

                        if (view == null || view.IsTemplate)
                        {
                            missingExportCount++;
                            continue;
                        }

                        try
                        {
                            stage = "Export Legend PNG: " + item.LegendName;
                            string exportFileName = SanitizeFileName(item.LegendName);
                            string exportFilePathWithoutExtension = Path.Combine(outputFolder, exportFileName);
                            ExportLegendViewAsPng(uiDocument, view, exportFilePathWithoutExtension);
                            string exportedPngPath = ResolveExportedPngPath(outputFolder, exportFileName);

                        if (string.IsNullOrWhiteSpace(exportedPngPath))
                        {
                            missingExportCount++;
                            continue;
                        }

                            item.ThumbnailRelativePath = MakeRelativeCsvPath(outputFolder, exportedPngPath);
                            exportedCount++;
                        }
                    catch (Exception)
                    {
                        missingExportCount++;
                    }
                }
                }
                finally
                {
                    RestoreActiveView(uiDocument, originalActiveViewId);
                }

                stage = "Write CSV";
                string pieCsvPath = WritePieCsv(outputFolder, preparedItems);
                string loadPlanCsvPath = WriteLoadPlanCsv(outputFolder, preparedItems);

                SystemTypePieLegendExportResult result = new SystemTypePieLegendExportResult();
                result.CreatedLegendCount = preparedItems.Count;
                result.ExportedImageCount = exportedCount;
                result.MissingExportCount = missingExportCount;
                result.PieCsvPath = pieCsvPath;
                result.LoadPlanCsvPath = loadPlanCsvPath;
                result.Errors = new List<string>();
                return result;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Stage: " + stage + ". " + exception.Message, exception);
            }
        }

        // Блок подготовки списка системных типов по целевым категориям.
        private static List<SystemTypePieItem> CollectSystemTypeItems(Document document)
        {
            List<SystemTypePieItem> result = new List<SystemTypePieItem>();
            HashSet<int> categoryIds = BuildTargetCategoryIdSet(document);

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ElementType));

            foreach (Element element in collector)
            {
                ElementType elementType = element as ElementType;

                if (elementType == null || elementType.Category == null)
                {
                    continue;
                }

                if (!categoryIds.Contains(elementType.Category.Id.IntegerValue))
                {
                    continue;
                }

                // В выгрузку пирогов включаем только системные типы.
                if (elementType is FamilySymbol)
                {
                    continue;
                }

                string categoryName = elementType.Category.Name ?? string.Empty;
                string familyName = ResolveFamilyName(elementType, categoryName);
                string typeName = elementType.Name ?? string.Empty;

                string legendName = BuildLegendName(familyName, typeName);
                legendName = SanitizeViewName(legendName);

                if (string.IsNullOrWhiteSpace(legendName))
                {
                    continue;
                }

                SystemTypePieItem item = new SystemTypePieItem();
                item.Category = categoryName;
                item.Family = familyName;
                item.TypeName = typeName;
                item.ElementTypeId = elementType.Id;
                item.LegendName = legendName;
                result.Add(item);
            }

            result.Sort(delegate (SystemTypePieItem left, SystemTypePieItem right)
            {
                int categoryCompare = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);

                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

                int familyCompare = string.Compare(left.Family, right.Family, StringComparison.OrdinalIgnoreCase);

                if (familyCompare != 0)
                {
                    return familyCompare;
                }

                return string.Compare(left.TypeName, right.TypeName, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static HashSet<int> BuildTargetCategoryIdSet(Document document)
        {
            HashSet<int> result = new HashSet<int>();

            // Блок безопасной сборки категорий через имена.
            // Важно: без массивных инициализаторов, чтобы исключить RuntimeHelpers.InitializeArray.
            List<string> categoryNames = new List<string>();
            categoryNames.Add("OST_Ceilings");            // Потолки
            categoryNames.Add("OST_Floors");              // Перекрытия
            categoryNames.Add("OST_Walls");               // Стены
            categoryNames.Add("OST_Roofs");               // Крыши
            categoryNames.Add("OST_RoofSoffit");          // Подшивные доски
            categoryNames.Add("OST_StructuralFoundation"); // Фундамент несущей конструкции

            for (int i = 0; i < categoryNames.Count; i++)
            {
                try
                {
                    BuiltInCategory builtInCategory;

                    if (!Enum.TryParse(categoryNames[i], out builtInCategory))
                    {
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(BuiltInCategory), builtInCategory))
                    {
                        continue;
                    }

                    Category category = Category.GetCategory(document, builtInCategory);

                    if (category != null)
                    {
                        result.Add(category.Id.IntegerValue);
                    }
                }
                catch
                {
                    // Категория может отсутствовать в конкретном шаблоне/версии.
                }
            }

            return result;
        }

        private static string ResolveFamilyName(ElementType elementType, string fallbackCategory)
        {
            if (!string.IsNullOrWhiteSpace(elementType.FamilyName))
            {
                return elementType.FamilyName;
            }

            Parameter familyNameParameter = elementType.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);

            if (familyNameParameter != null)
            {
                string value = familyNameParameter.AsString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.IsNullOrWhiteSpace(fallbackCategory) ? "SystemFamily" : fallbackCategory;
        }

        private static string BuildLegendName(string familyName, string typeName)
        {
            // Формат наименования: Пирог_Семейство_Тип
            // Семейство и Тип передаются из параметров обрабатываемого ElementType.
            string safeFamily = string.IsNullOrWhiteSpace(familyName) ? "Family" : familyName.Trim();
            string safeType = string.IsNullOrWhiteSpace(typeName) ? "Type" : typeName.Trim();
            return PiePrefix + "_" + safeFamily + "_" + safeType;
        }

        private static string SanitizeViewName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result = value.Trim();

            for (int i = 0; i < ProhibitedViewNameCharacters.Length; i++)
            {
                result = result.Replace(ProhibitedViewNameCharacters[i].ToString(), "_");
            }

            char[] invalidFileChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidFileChars.Length; i++)
            {
                result = result.Replace(invalidFileChars[i].ToString(), "_");
            }

            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Trim();
        }

        private static string SanitizeFileName(string value)
        {
            return SanitizeViewName(value);
        }

        private static View FindLegendPrototype(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;

                if (view == null || view.IsTemplate)
                {
                    continue;
                }

                if (view.ViewType == ViewType.Legend)
                {
                    return view;
                }
            }

            return null;
        }

        private static View DuplicateLegend(Document document, View sourceLegend, string baseName)
        {
            ElementId duplicatedId = sourceLegend.Duplicate(ViewDuplicateOption.Duplicate);
            View newLegend = document.GetElement(duplicatedId) as View;

            if (newLegend == null)
            {
                return null;
            }

            string uniqueName = GetUniqueViewName(document, baseName);
            newLegend.Name = uniqueName;

            return newLegend;
        }

        private static void ConfigureLegendView(View legendView)
        {
            if (legendView == null)
            {
                return;
            }

            try
            {
                legendView.Scale = LegendScale;
            }
            catch
            {
            }

            try
            {
                legendView.DetailLevel = ViewDetailLevel.Fine;
            }
            catch
            {
            }

            // Блок отображения: заливка (тонированный режим).
            try
            {
                legendView.DisplayStyle = DisplayStyle.Shading;
            }
            catch
            {
            }
        }

        private static void ClearViewContents(Document document, View view)
        {
            if (document == null || view == null)
            {
                return;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, view.Id);
            ICollection<ElementId> ids = collector.ToElementIds();

            if (ids == null || ids.Count == 0)
            {
                return;
            }

            List<ElementId> toDelete = new List<ElementId>();

            foreach (ElementId id in ids)
            {
                if (id == ElementId.InvalidElementId || id == view.Id)
                {
                    continue;
                }

                Element element = document.GetElement(id);

                if (element == null)
                {
                    continue;
                }

                // Удаляем только безопасные для чистки элементы текущего вида.
                if (element.OwnerViewId != view.Id)
                {
                    continue;
                }

                bool isSafeElement =
                    element is TextElement ||
                    element is FilledRegion ||
                    element is CurveElement;

                if (!isSafeElement)
                {
                    continue;
                }

                toDelete.Add(id);
            }

            if (toDelete.Count > 0)
            {
                document.Delete(toDelete);
            }
        }

        // Блок построения графического "пирога" по слоям конструкции системного типа.
        // Геометрия строится в плоскости XY вида (поведение "план этажа" для заданной длины основы 100 мм).
        private static void DrawPieForType(
            Document document,
            View legendView,
            SystemTypePieItem item,
            ElementId filledRegionTypeId,
            ElementId solidFillPatternId)
        {
            if (filledRegionTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            ElementType elementType = document.GetElement(item.ElementTypeId) as ElementType;

            if (elementType == null)
            {
                return;
            }

            HostObjAttributes hostType = elementType as HostObjAttributes;
            CompoundStructure structure = hostType != null ? hostType.GetCompoundStructure() : null;
            IList<CompoundStructureLayer> layers = structure != null ? structure.GetLayers() : null;

            double baseLengthFt = UnitUtils.ConvertToInternalUnits(BaseLengthMillimeters, UnitTypeId.Millimeters);
            double pieHeightFt = UnitUtils.ConvertToInternalUnits(PieHeightMillimeters, UnitTypeId.Millimeters);
            double startX = 0.0;
            double yBottom = 0.0;
            double yTop = pieHeightFt;

            if (layers == null || layers.Count == 0)
            {
                ElementId regionId = CreateFilledRegionRectangle(
                    document,
                    legendView,
                    filledRegionTypeId,
                    startX,
                    startX + baseLengthFt,
                    yBottom,
                    yTop);

                if (regionId != ElementId.InvalidElementId)
                {
                    ApplyDefaultRegionOverride(legendView, regionId, solidFillPatternId, null);
                }

                return;
            }

            double totalWidth = 0.0;

            for (int i = 0; i < layers.Count; i++)
            {
                totalWidth += Math.Max(0.0, layers[i].Width);
            }

            if (totalWidth <= 0.0)
            {
                totalWidth = layers.Count;
            }

            double currentX = startX;

            for (int i = 0; i < layers.Count; i++)
            {
                CompoundStructureLayer layer = layers[i];
                double widthRatio;

                if (layer.Width > 0.0)
                {
                    widthRatio = layer.Width / totalWidth;
                }
                else
                {
                    widthRatio = 1.0 / layers.Count;
                }

                double segmentLength = baseLengthFt * widthRatio;

                if (i == layers.Count - 1)
                {
                    segmentLength = (startX + baseLengthFt) - currentX;
                }

                if (segmentLength <= 0)
                {
                    continue;
                }

                double left = currentX;
                double right = currentX + segmentLength;

                ElementId regionId = CreateFilledRegionRectangle(
                    document,
                    legendView,
                    filledRegionTypeId,
                    left,
                    right,
                    yBottom,
                    yTop);

                if (regionId != ElementId.InvalidElementId)
                {
                    Color materialColor = ResolveMaterialColor(document, layer.MaterialId);
                    ApplyDefaultRegionOverride(legendView, regionId, solidFillPatternId, materialColor);
                }

                currentX = right;
            }
        }

        private static ElementId CreateFilledRegionRectangle(
            Document document,
            View legendView,
            ElementId filledRegionTypeId,
            double left,
            double right,
            double yBottom,
            double yTop)
        {
            CurveLoop loop = new CurveLoop();
            XYZ p1 = new XYZ(left, yBottom, 0);
            XYZ p2 = new XYZ(right, yBottom, 0);
            XYZ p3 = new XYZ(right, yTop, 0);
            XYZ p4 = new XYZ(left, yTop, 0);

            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p4));
            loop.Append(Line.CreateBound(p4, p1));

            IList<CurveLoop> loops = new List<CurveLoop> { loop };
            FilledRegion region = FilledRegion.Create(document, filledRegionTypeId, legendView.Id, loops);
            return region != null ? region.Id : ElementId.InvalidElementId;
        }

        private static void ApplyDefaultRegionOverride(View legendView, ElementId regionId, ElementId solidFillPatternId, Color materialColor)
        {
            if (legendView == null || regionId == ElementId.InvalidElementId)
            {
                return;
            }

            OverrideGraphicSettings overrideGraphicSettings = new OverrideGraphicSettings();

            if (solidFillPatternId != ElementId.InvalidElementId)
            {
                overrideGraphicSettings.SetSurfaceForegroundPatternId(solidFillPatternId);
                overrideGraphicSettings.SetSurfaceForegroundPatternVisible(true);
                overrideGraphicSettings.SetSurfaceBackgroundPatternId(solidFillPatternId);
                overrideGraphicSettings.SetSurfaceBackgroundPatternVisible(true);
            }

            Color finalColor = materialColor;

            if (finalColor == null || !finalColor.IsValid)
            {
                finalColor = new Color(180, 180, 180);
            }

            overrideGraphicSettings.SetSurfaceForegroundPatternColor(finalColor);
            overrideGraphicSettings.SetSurfaceBackgroundPatternColor(finalColor);
            legendView.SetElementOverrides(regionId, overrideGraphicSettings);
        }

        private static Color ResolveMaterialColor(Document document, ElementId materialId)
        {
            if (materialId == null || materialId == ElementId.InvalidElementId)
            {
                return null;
            }

            Material material = document.GetElement(materialId) as Material;

            if (material == null)
            {
                return null;
            }

            return material.Color;
        }

        private static ElementId GetDefaultFilledRegionTypeId(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));
            Element element = collector.FirstElement();
            return element != null ? element.Id : ElementId.InvalidElementId;
        }

        private static ElementId GetSolidFillPatternId(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FillPatternElement));

            foreach (Element element in collector)
            {
                FillPatternElement fillPatternElement = element as FillPatternElement;

                if (fillPatternElement == null)
                {
                    continue;
                }

                FillPattern pattern = fillPatternElement.GetFillPattern();

                if (pattern != null && pattern.IsSolidFill)
                {
                    return fillPatternElement.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private static string GetUniqueViewName(Document document, string baseName)
        {
            string candidate = baseName;
            int index = 1;

            while (ViewNameExists(document, candidate))
            {
                candidate = baseName + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }

            return candidate;
        }

        private static bool ViewNameExists(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;

                if (view == null || view.IsTemplate)
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

        private static void ExportCurrentWindowAsPng(Document document, string filePathWithoutExtension)
        {
            ImageExportOptions options = new ImageExportOptions();
            options.ExportRange = ExportRange.CurrentView;
            options.ZoomType = ZoomFitType.FitToPage;
            options.PixelSize = ExportImageWidthPixels;
            ConfigureFitDirectionHorizontal(options);
            options.HLRandWFViewsFileType = ImageFileType.PNG;
            options.ShadowViewsFileType = ImageFileType.PNG;
            options.ImageResolution = ImageResolution.DPI_600;
            options.FilePath = filePathWithoutExtension;

            document.ExportImage(options);
        }

        // Блок совместимой настройки ширины изображения: 500 px по горизонтали.
        // Через reflection, чтобы избежать проблем между версиями Revit API.
        private static void ConfigureFitDirectionHorizontal(ImageExportOptions options)
        {
            if (options == null)
            {
                return;
            }

            try
            {
                PropertyInfo fitDirectionProperty = options.GetType().GetProperty("FitDirection", BindingFlags.Public | BindingFlags.Instance);

                if (fitDirectionProperty == null || !fitDirectionProperty.CanWrite)
                {
                    return;
                }

                Type enumType = fitDirectionProperty.PropertyType;
                object horizontalValue = Enum.Parse(enumType, "Horizontal", true);
                fitDirectionProperty.SetValue(options, horizontalValue, null);
            }
            catch
            {
            }
        }

        // Блок экспорта PNG строго через текущее окно.
        private static void ExportLegendViewAsPng(UIDocument uiDocument, View view, string filePathWithoutExtension)
        {
            Document document = uiDocument.Document;

            if (uiDocument.ActiveView == null || uiDocument.ActiveView.Id != view.Id)
            {
                uiDocument.ActiveView = view;
                uiDocument.RefreshActiveView();
                Thread.Sleep(120);
            }

            ExportCurrentWindowAsPng(document, filePathWithoutExtension);
        }

        private static void RestoreActiveView(UIDocument uiDocument, ElementId viewId)
        {
            if (uiDocument == null || viewId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                View originalView = uiDocument.Document.GetElement(viewId) as View;

                if (originalView == null || originalView.IsTemplate)
                {
                    return;
                }

                uiDocument.ActiveView = originalView;
            }
            catch
            {
            }
        }

        private static string ResolveExportedPngPath(string outputFolder, string baseName)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                string exactPath = Path.Combine(outputFolder, baseName + ".png");

                if (File.Exists(exactPath))
                {
                    return exactPath;
                }

                string[] variants = Directory.GetFiles(outputFolder, baseName + "*.png");

                if (variants.Length > 0)
                {
                    Array.Sort(variants, StringComparer.OrdinalIgnoreCase);
                    return variants[variants.Length - 1];
                }

                Thread.Sleep(100);
            }

            return string.Empty;
        }

        private string WritePieCsv(string outputFolder, List<SystemTypePieItem> items)
        {
            string csvPath = Path.Combine(outputFolder, "Пироги_системных_типов.csv");
            List<string> header = new List<string>
            {
                "Category",
                "Family",
                "TypeName",
                "ThumbnailPath",
                "LegendViewName"
            };

            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < items.Count; i++)
            {
                SystemTypePieItem item = items[i];

                rows.Add(new List<string>
                {
                    item.Category,
                    item.Family,
                    item.TypeName,
                    item.ThumbnailRelativePath ?? string.Empty,
                    item.LegendName
                });
            }

            _csvTableService.Write(csvPath, header, rows);
            return csvPath;
        }

        // Блок планирования загрузки в параметр "Изображение типоразмера".
        private string WriteLoadPlanCsv(string outputFolder, List<SystemTypePieItem> items)
        {
            string csvPath = Path.Combine(outputFolder, "План_загрузки_изображений_типоразмеров.csv");
            List<string> header = new List<string>
            {
                "Category",
                "Family",
                "TypeName",
                "ThumbnailPath",
                "TargetParameter",
                "PlannedAction"
            };

            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < items.Count; i++)
            {
                SystemTypePieItem item = items[i];

                rows.Add(new List<string>
                {
                    item.Category,
                    item.Family,
                    item.TypeName,
                    item.ThumbnailRelativePath ?? string.Empty,
                    "Изображение типоразмера",
                    "PlannedLoad"
                });
            }

            _csvTableService.Write(csvPath, header, rows);
            return csvPath;
        }

        private static string MakeRelativeCsvPath(string rootFolder, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || string.IsNullOrWhiteSpace(targetPath))
            {
                return string.Empty;
            }

            Uri rootUri = new Uri(Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri targetUri = new Uri(Path.GetFullPath(targetPath));
            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(targetUri).ToString());
            return relative.Replace('\\', '/');
        }

        private class SystemTypePieItem
        {
            public string Category { get; set; }

            public string Family { get; set; }

            public string TypeName { get; set; }

            public string LegendName { get; set; }

            public ElementId ElementTypeId { get; set; }

            public ElementId LegendViewId { get; set; }

            public string ThumbnailRelativePath { get; set; }
        }
    }

    public class SystemTypePieLegendExportResult
    {
        public int CreatedLegendCount { get; set; }

        public int ExportedImageCount { get; set; }

        public int MissingExportCount { get; set; }

        public string PieCsvPath { get; set; }

        public string LoadPlanCsvPath { get; set; }

        public List<string> Errors { get; set; } = new List<string>();

        public string BuildSummaryText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Создано легенд: " + CreatedLegendCount);
            builder.AppendLine("Экспортировано PNG: " + ExportedImageCount);
            builder.AppendLine("Без PNG: " + MissingExportCount);

            if (!string.IsNullOrWhiteSpace(PieCsvPath))
            {
                builder.AppendLine("CSV пирогов: " + Path.GetFileName(PieCsvPath));
            }

            if (!string.IsNullOrWhiteSpace(LoadPlanCsvPath))
            {
                builder.AppendLine("CSV плана загрузки: " + Path.GetFileName(LoadPlanCsvPath));
            }

            if (Errors != null && Errors.Count > 0)
            {
                builder.AppendLine("Ошибок: " + Errors.Count);
            }

            return builder.ToString();
        }
    }
}

