using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class RevitDataService
    {
        private readonly SheetBoundsService _sheetBoundsService;

        public RevitDataService()
        {
            _sheetBoundsService = new SheetBoundsService();
        }

        public List<RevitElementItem> GetDuplicatableViews(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();
            if (document == null)
            {
                return result;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (!IsSupportedSourceView(view))
                {
                    continue;
                }

                RevitElementItem item = new RevitElementItem();
                item.Id = view.Id;
                item.UniqueId = view.UniqueId;
                item.Name = view.Name;
                item.ViewType = view.ViewType;
                result.Add(item);
            }

            SortByName(result);
            return result;
        }

        public List<RevitElementItem> GetViewTemplates(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();
            if (document == null)
            {
                return result;
            }

            RevitElementItem emptyItem = new RevitElementItem();
            emptyItem.Id = ElementId.InvalidElementId;
            emptyItem.Name = "<Не выбран>";
            result.Add(emptyItem);

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null || !view.IsTemplate)
                {
                    continue;
                }

                RevitElementItem item = new RevitElementItem();
                item.Id = view.Id;
                item.UniqueId = view.UniqueId;
                item.Name = view.Name;
                item.ViewType = view.ViewType;
                item.ControlsScale = IsScaleControlledByTemplate(view);
                result.Add(item);
            }

            result.Sort(delegate(RevitElementItem left, RevitElementItem right)
            {
                if (left != null && left.Id == ElementId.InvalidElementId)
                {
                    return -1;
                }

                if (right != null && right.Id == ElementId.InvalidElementId)
                {
                    return 1;
                }

                return string.Compare(
                    left != null ? left.Name : string.Empty,
                    right != null ? right.Name : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        public List<RevitElementItem> GetSheets(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();
            if (document == null)
            {
                return result;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || sheet.IsTemplate)
                {
                    continue;
                }

                SheetBounds bounds;
                _sheetBoundsService.TryGetSheetBounds(document, sheet, out bounds);

                RevitElementItem item = new RevitElementItem();
                item.Id = sheet.Id;
                item.RelatedElementId = _sheetBoundsService.GetTitleBlockTypeId(document, sheet);
                item.UniqueId = sheet.UniqueId;
                item.Name = sheet.SheetNumber + " | " + sheet.Name;
                item.SheetBounds = bounds;
                result.Add(item);
            }

            SortByName(result);
            return result;
        }

        public Dictionary<long, HashSet<long>> GetPlacedViewIdsBySheetId(Document document)
        {
            Dictionary<long, HashSet<long>> result = new Dictionary<long, HashSet<long>>();
            if (document == null)
            {
                return result;
            }

            FilteredElementCollector sheetCollector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element sheetElement in sheetCollector)
            {
                ViewSheet sheet = sheetElement as ViewSheet;
                if (sheet == null || sheet.IsTemplate)
                {
                    continue;
                }

                long sheetKey = RevitElementIdUtils.GetElementIdValue(sheet.Id);
                HashSet<long> placedViewIds = new HashSet<long>();
                CollectPlacedViewIdsFromSheet(document, sheet, placedViewIds);
                result[sheetKey] = placedViewIds;
            }

            return result;
        }

        public List<RevitElementItem> GetViewportTypes(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();
            if (document == null)
            {
                return result;
            }

            Dictionary<long, RevitElementItem> itemsById = new Dictionary<long, RevitElementItem>();
            CollectViewportTypesByCategory(document, itemsById);
            CollectViewportTypesByElementTypeFallback(document, itemsById);

            foreach (RevitElementItem item in itemsById.Values)
            {
                result.Add(item);
            }

            SortByName(result);
            return result;
        }

        private void CollectPlacedViewIdsFromSheet(Document document, ViewSheet sheet, HashSet<long> placedViewIds)
        {
            if (document == null || sheet == null || placedViewIds == null)
            {
                return;
            }

            try
            {
                ICollection<ElementId> viewportIds = sheet.GetAllViewports();
                if (viewportIds != null)
                {
                    foreach (ElementId viewportId in viewportIds)
                    {
                        Viewport viewport = document.GetElement(viewportId) as Viewport;
                        if (viewport != null && viewport.ViewId != null && viewport.ViewId != ElementId.InvalidElementId)
                        {
                            placedViewIds.Add(RevitElementIdUtils.GetElementIdValue(viewport.ViewId));
                        }
                    }
                }
            }
            catch
            {
                // Резервный поиск ниже нужен для листов, где GetAllViewports недоступен.
            }

            FilteredElementCollector viewportCollector = new FilteredElementCollector(document, sheet.Id).OfClass(typeof(Viewport));
            foreach (Element viewportElement in viewportCollector)
            {
                Viewport viewport = viewportElement as Viewport;
                if (viewport != null && viewport.ViewId != null && viewport.ViewId != ElementId.InvalidElementId)
                {
                    placedViewIds.Add(RevitElementIdUtils.GetElementIdValue(viewport.ViewId));
                }
            }
        }

        public List<RevitElementItem> GetTitleBlockTypes(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();
            if (document == null)
            {
                return result;
            }

            Dictionary<long, SheetBounds> knownBoundsByTypeId = CollectKnownTitleBlockBounds(document);

            FilteredElementCollector collector = new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks);

            foreach (Element element in collector)
            {
                FamilySymbol symbol = element as FamilySymbol;
                if (symbol == null)
                {
                    continue;
                }

                RevitElementItem item = new RevitElementItem();
                item.Id = symbol.Id;
                item.UniqueId = symbol.UniqueId;
                item.Name = symbol.FamilyName + " : " + symbol.Name;

                SheetBounds bounds;
                if (knownBoundsByTypeId.TryGetValue(RevitElementIdUtils.GetElementIdValue(symbol.Id), out bounds))
                {
                    item.SheetBounds = bounds;
                }

                result.Add(item);
            }

            SortByName(result);
            return result;
        }

        public List<RevitElementItem> GetSheetBrowserParameters(Document document)
        {
            List<RevitElementItem> result = new List<RevitElementItem>();

            if (document == null)
            {
                return result;
            }

            List<ElementId> browserParameterIds = CollectSheetBrowserOrganizationParameterIds(document, CollectProjectSheets(document));
            HashSet<long> addedParameterIds = new HashSet<long>();

            for (int i = 0; i < browserParameterIds.Count; i++)
            {
                ElementId parameterId = browserParameterIds[i];
                if (IsIgnoredSheetBrowserParameter(document, parameterId))
                {
                    continue;
                }

                long key = RevitElementIdUtils.GetElementIdValue(parameterId);
                if (!addedParameterIds.Add(key))
                {
                    continue;
                }

                result.Add(BuildSheetBrowserParameterItem(document, parameterId));
            }

            return result;
        }

        private bool IsIgnoredSheetBrowserParameter(Document document, ElementId parameterId)
        {
            if (parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return true;
            }

            string parameterName = ResolveBrowserParameterName(document, parameterId);
            return string.Equals(parameterName, "Номер листа", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parameterName, "Sheet Number", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parameterName, "SheetNumber", StringComparison.OrdinalIgnoreCase);
        }

        public Dictionary<long, List<string>> GetSheetBrowserParameterValues(Document document, IList<RevitElementItem> sheetBrowserParameters)
        {
            Dictionary<long, List<string>> result = new Dictionary<long, List<string>>();
            if (document == null || sheetBrowserParameters == null || sheetBrowserParameters.Count == 0)
            {
                return result;
            }

            List<ViewSheet> sheets = CollectProjectSheets(document);
            for (int i = 0; i < sheetBrowserParameters.Count; i++)
            {
                RevitElementItem parameterItem = sheetBrowserParameters[i];
                if (parameterItem == null || parameterItem.Id == null || parameterItem.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                long key = RevitElementIdUtils.GetElementIdValue(parameterItem.Id);
                if (result.ContainsKey(key))
                {
                    continue;
                }

                result.Add(key, CollectSheetParameterValues(sheets, parameterItem.Id));
            }

            return result;
        }

        public HashSet<string> CollectExistingViewNames(Document document)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document == null)
            {
                return names;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null || string.IsNullOrWhiteSpace(view.Name))
                {
                    continue;
                }

                names.Add(view.Name.Trim());
            }

            return names;
        }

        public HashSet<string> CollectExistingSheetNumbers(Document document)
        {
            HashSet<string> numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document == null)
            {
                return numbers;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.SheetNumber))
                {
                    continue;
                }

                numbers.Add(sheet.SheetNumber.Trim());
            }

            return numbers;
        }

        private Dictionary<long, SheetBounds> CollectKnownTitleBlockBounds(Document document)
        {
            Dictionary<long, SheetBounds> result = new Dictionary<long, SheetBounds>();
            if (document == null)
            {
                return result;
            }

            FilteredElementCollector sheetCollector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in sheetCollector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null)
                {
                    continue;
                }

                SheetBounds bounds;
                if (!_sheetBoundsService.TryGetSheetBounds(document, sheet, out bounds))
                {
                    continue;
                }

                ElementId titleBlockTypeId = _sheetBoundsService.GetTitleBlockTypeId(document, sheet);
                long key = RevitElementIdUtils.GetElementIdValue(titleBlockTypeId);
                if (key < 0 || result.ContainsKey(key))
                {
                    continue;
                }

                result.Add(key, bounds);
            }

            return result;
        }

        private List<ViewSheet> CollectProjectSheets(Document document)
        {
            List<ViewSheet> result = new List<ViewSheet>();
            if (document == null)
            {
                return result;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || sheet.IsTemplate)
                {
                    continue;
                }

                result.Add(sheet);
            }

            return result;
        }

        private List<ElementId> CollectSheetBrowserOrganizationParameterIds(Document document, IList<ViewSheet> sheets)
        {
            List<ElementId> result = new List<ElementId>();
            HashSet<long> addedIds = new HashSet<long>();
            if (document == null)
            {
                return result;
            }

            BrowserOrganization browserOrganization = null;
            try
            {
                browserOrganization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(document);
            }
            catch
            {
                return result;
            }

            if (browserOrganization == null)
            {
                return result;
            }

            // Группировка браузера доступна через FolderItemInfo только для листов, которые проходят фильтры текущей организации.
            if (sheets != null)
            {
                for (int i = 0; i < sheets.Count; i++)
                {
                    ViewSheet sheet = sheets[i];
                    if (sheet == null)
                    {
                        continue;
                    }

                    try
                    {
                        IList<FolderItemInfo> folderItems = browserOrganization.GetFolderItems(sheet.Id);
                        if (folderItems == null)
                        {
                            continue;
                        }

                        for (int j = 0; j < folderItems.Count; j++)
                        {
                            FolderItemInfo folderItem = folderItems[j];
                            if (folderItem != null)
                            {
                                AddParameterId(result, addedIds, folderItem.ElementId);
                            }
                        }
                    }
                    catch
                    {
                        // Лист может не проходить фильтры организации браузера. Пробуем следующий лист.
                    }
                }
            }

            try
            {
                AddParameterId(result, addedIds, browserOrganization.SortingParameterId);
            }
            catch
            {
                // В старых/новых версиях Revit сортировка может быть недоступна через это свойство.
            }

            return result;
        }

        private void AddParameterId(IList<ElementId> target, HashSet<long> addedIds, ElementId parameterId)
        {
            if (target == null || addedIds == null || parameterId == null)
            {
                return;
            }

            long key = RevitElementIdUtils.GetElementIdValue(parameterId);
            if (key == -1 || !addedIds.Add(key))
            {
                return;
            }

            target.Add(parameterId);
        }

        private RevitElementItem BuildSheetBrowserParameterItem(Document document, ElementId parameterId)
        {
            RevitElementItem item = new RevitElementItem();
            item.Id = parameterId != null ? parameterId : ElementId.InvalidElementId;
            item.Name = ResolveBrowserParameterName(document, parameterId);
            return item;
        }

        private string ResolveBrowserParameterName(Document document, ElementId parameterId)
        {
            if (parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return string.Empty;
            }

            Element parameterElement = null;
            try
            {
                parameterElement = document != null ? document.GetElement(parameterId) : null;
            }
            catch
            {
                parameterElement = null;
            }

            if (parameterElement != null && !string.IsNullOrWhiteSpace(parameterElement.Name))
            {
                return parameterElement.Name;
            }

            long parameterIdValue = RevitElementIdUtils.GetElementIdValue(parameterId);
            if (parameterIdValue >= int.MinValue && parameterIdValue <= int.MaxValue)
            {
                string builtInParameterName = TryGetBuiltInParameterName((int)parameterIdValue);
                if (!string.IsNullOrWhiteSpace(builtInParameterName))
                {
                    return builtInParameterName;
                }
            }

            return "Параметр " + parameterIdValue;
        }

        private string TryGetBuiltInParameterName(int parameterIdValue)
        {
            try
            {
                BuiltInParameter builtInParameter = (BuiltInParameter)parameterIdValue;
                return LabelUtils.GetLabelFor(builtInParameter);
            }
            catch
            {
                return string.Empty;
            }
        }

        private List<string> CollectSheetParameterValues(IList<ViewSheet> sheets, ElementId parameterId)
        {
            List<string> result = new List<string>();
            HashSet<string> addedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sheets == null || parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return result;
            }

            for (int i = 0; i < sheets.Count; i++)
            {
                ViewSheet sheet = sheets[i];
                Parameter parameter = FindSheetParameterById(sheet, parameterId);
                string valueText = GetParameterValueText(parameter);
                if (string.IsNullOrWhiteSpace(valueText))
                {
                    continue;
                }

                if (addedValues.Add(valueText))
                {
                    result.Add(valueText);
                }
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private Parameter FindSheetParameterById(IList<ViewSheet> sheets, ElementId parameterId)
        {
            if (sheets == null || parameterId == null)
            {
                return null;
            }

            for (int i = 0; i < sheets.Count; i++)
            {
                ViewSheet sheet = sheets[i];
                if (sheet == null || sheet.Parameters == null)
                {
                    continue;
                }

                foreach (Parameter parameter in sheet.Parameters)
                {
                    if (parameter != null && RevitElementIdUtils.AreEqual(parameter.Id, parameterId))
                    {
                        return parameter;
                    }
                }
            }

            return null;
        }

        private Parameter FindSheetParameterById(ViewSheet sheet, ElementId parameterId)
        {
            if (sheet == null || sheet.Parameters == null || parameterId == null)
            {
                return null;
            }

            foreach (Parameter parameter in sheet.Parameters)
            {
                if (parameter != null && RevitElementIdUtils.AreEqual(parameter.Id, parameterId))
                {
                    return parameter;
                }
            }

            return null;
        }

        private string GetParameterValueText(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue)
            {
                return string.Empty;
            }

            try
            {
                if (parameter.StorageType == StorageType.String)
                {
                    return (parameter.AsString() ?? string.Empty).Trim();
                }

                string valueText = parameter.AsValueString();
                if (!string.IsNullOrWhiteSpace(valueText))
                {
                    return valueText.Trim();
                }

                if (parameter.StorageType == StorageType.Integer)
                {
                    return parameter.AsInteger().ToString();
                }

                if (parameter.StorageType == StorageType.Double)
                {
                    return parameter.AsDouble().ToString("0.###", System.Globalization.CultureInfo.CurrentCulture);
                }

                if (parameter.StorageType == StorageType.ElementId)
                {
                    return RevitElementIdUtils.GetElementIdValue(parameter.AsElementId()).ToString();
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private bool IsSupportedSourceView(View view)
        {
            if (view == null || view.IsTemplate)
            {
                return false;
            }

            if (IsUnsupportedViewType(view.ViewType))
            {
                return false;
            }

            try
            {
                return view.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing);
            }
            catch
            {
                return false;
            }
        }

        private bool IsUnsupportedViewType(ViewType viewType)
        {
            string viewTypeName = viewType.ToString();
            return string.Equals(viewTypeName, "DrawingSheet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "ProjectBrowser", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "SystemBrowser", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "Internal", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "Schedule", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "ColumnSchedule", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "PanelSchedule", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(viewTypeName, "Legend", StringComparison.OrdinalIgnoreCase) ||
                   viewType == ViewType.Undefined;
        }

        private void CollectViewportTypesByCategory(Document document, Dictionary<long, RevitElementItem> itemsById)
        {
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Viewports)
                    .WhereElementIsElementType();

                foreach (Element element in collector)
                {
                    ElementType elementType = element as ElementType;
                    AddViewportTypeItem(elementType, itemsById);
                }
            }
            catch
            {
                // В некоторых шаблонах Revit типы Viewport не находятся через прямой фильтр категории.
            }
        }

        private void CollectViewportTypesByElementTypeFallback(Document document, Dictionary<long, RevitElementItem> itemsById)
        {
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(document)
                    .OfClass(typeof(ElementType))
                    .WhereElementIsElementType();

                foreach (Element element in collector)
                {
                    ElementType elementType = element as ElementType;
                    if (!IsLikelyViewportType(elementType))
                    {
                        continue;
                    }

                    AddViewportTypeItem(elementType, itemsById);
                }
            }
            catch
            {
                // Последний fallback нужен только для документов, где прямой фильтр категории пустой.
            }
        }

        private void AddViewportTypeItem(ElementType elementType, Dictionary<long, RevitElementItem> itemsById)
        {
            if (elementType == null || itemsById == null)
            {
                return;
            }

            long key = RevitElementIdUtils.GetElementIdValue(elementType.Id);
            if (key < 0 || itemsById.ContainsKey(key))
            {
                return;
            }

            if (!IsLikelyViewportType(elementType))
            {
                return;
            }

            RevitElementItem item = new RevitElementItem();
            item.Id = elementType.Id;
            item.UniqueId = elementType.UniqueId;
            item.Name = BuildElementTypeName(elementType);
            itemsById.Add(key, item);
        }

        private bool IsLikelyViewportType(ElementType elementType)
        {
            if (elementType == null)
            {
                return false;
            }

            if (elementType.Category != null &&
                elementType.Category.Id != null &&
                elementType.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Viewports)
            {
                return true;
            }

            string categoryName = elementType.Category != null ? elementType.Category.Name : string.Empty;
            string familyName = elementType.FamilyName ?? string.Empty;
            string typeName = elementType.Name ?? string.Empty;

            return ContainsViewportText(categoryName) ||
                   ContainsViewportText(familyName) ||
                   ContainsViewportText(typeName);
        }

        private bool ContainsViewportText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.IndexOf("Viewport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Viewports", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Видовой экран", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Видовые экраны", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildElementTypeName(ElementType elementType)
        {
            if (elementType == null)
            {
                return string.Empty;
            }

            string familyName = elementType.FamilyName ?? string.Empty;
            string typeName = elementType.Name ?? string.Empty;

            if (string.IsNullOrWhiteSpace(familyName) ||
                string.Equals(familyName, typeName, StringComparison.OrdinalIgnoreCase))
            {
                return typeName;
            }

            return familyName + " : " + typeName;
        }

        private bool IsScaleControlledByTemplate(View templateView)
        {
            if (templateView == null || !templateView.IsTemplate)
            {
                return false;
            }

            try
            {
                ICollection<ElementId> nonControlledParameters = templateView.GetNonControlledTemplateParameterIds();
                if (nonControlledParameters == null)
                {
                    return true;
                }

                ElementId viewScaleId = new ElementId((int)BuiltInParameter.VIEW_SCALE);
                ElementId viewScaleMetricId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
                ElementId viewScaleImperialId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_IMPERIAL);

                bool scaleIsNotControlled =
                    nonControlledParameters.Contains(viewScaleId) ||
                    nonControlledParameters.Contains(viewScaleMetricId) ||
                    nonControlledParameters.Contains(viewScaleImperialId);

                return !scaleIsNotControlled;
            }
            catch
            {
                return true;
            }
        }

        private void SortByName(List<RevitElementItem> items)
        {
            items.Sort(delegate(RevitElementItem left, RevitElementItem right)
            {
                return string.Compare(
                    left != null ? left.Name : string.Empty,
                    right != null ? right.Name : string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
