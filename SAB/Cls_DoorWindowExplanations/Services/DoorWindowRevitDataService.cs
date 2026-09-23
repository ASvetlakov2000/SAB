using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowRevitDataService
    {
        public IList<DoorWindowViewTemplateItem> GetSectionViewTemplates(Document document)
        {
            List<DoorWindowViewTemplateItem> templates = new List<DoorWindowViewTemplateItem>();
            templates.Add(new DoorWindowViewTemplateItem(ElementId.InvalidElementId, "(Без шаблона)"));

            if (document == null)
            {
                return templates;
            }

            IEnumerable<View> sectionTemplates = new FilteredElementCollector(document)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(view => view.IsTemplate && view.ViewType == ViewType.Section)
                .OrderBy(view => view.Name);

            foreach (View template in sectionTemplates)
            {
                templates.Add(new DoorWindowViewTemplateItem(template.Id, template.Name));
            }

            return templates;
        }

        public ElementId GetSectionViewFamilyTypeId(Document document)
        {
            ViewFamilyType type = new FilteredElementCollector(document)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(item => item.ViewFamily == ViewFamily.Section);

            if (type == null)
            {
                throw new System.InvalidOperationException("В проекте не найден тип вида для разрезов.");
            }

            return type.Id;
        }

        public IList<DoorWindowNamedElementItem> GetTitleBlockTypes(Document document)
        {
            List<DoorWindowNamedElementItem> result = new List<DoorWindowNamedElementItem>();
            if (document == null)
            {
                return result;
            }

            IEnumerable<FamilySymbol> symbols = new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .OrderBy(symbol => symbol.FamilyName)
                .ThenBy(symbol => symbol.Name);

            foreach (FamilySymbol symbol in symbols)
            {
                result.Add(new DoorWindowNamedElementItem(
                    symbol.Id,
                    symbol.FamilyName + " : " + symbol.Name));
            }

            return result;
        }

        public IList<DoorWindowNamedElementItem> GetViewportTypes(Document document)
        {
            List<DoorWindowNamedElementItem> result = new List<DoorWindowNamedElementItem>();
            result.Add(new DoorWindowNamedElementItem(ElementId.InvalidElementId, "(Тип по умолчанию)"));
            if (document == null)
            {
                return result;
            }

            Dictionary<int, DoorWindowNamedElementItem> itemsById =
                new Dictionary<int, DoorWindowNamedElementItem>();

            try
            {
                FilteredElementCollector categoryCollector = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Viewports)
                    .WhereElementIsElementType();

                foreach (Element element in categoryCollector)
                {
                    AddViewportTypeItem(element as ElementType, itemsById);
                }
            }
            catch
            {
                // В отдельных шаблонах прямой фильтр категории Viewport недоступен.
            }

            try
            {
                FilteredElementCollector fallbackCollector = new FilteredElementCollector(document)
                    .OfClass(typeof(ElementType))
                    .WhereElementIsElementType();

                foreach (Element element in fallbackCollector)
                {
                    AddViewportTypeItem(element as ElementType, itemsById);
                }
            }
            catch
            {
                // В списке останется тип Revit по умолчанию.
            }

            List<DoorWindowNamedElementItem> collectedItems = itemsById.Values
                .OrderBy(item => item.DisplayName)
                .ToList();
            for (int index = 0; index < collectedItems.Count; index++)
            {
                result.Add(collectedItems[index]);
            }

            return result;
        }

        private void AddViewportTypeItem(
            ElementType viewportType,
            IDictionary<int, DoorWindowNamedElementItem> itemsById)
        {
            if (!IsLikelyViewportType(viewportType) || itemsById == null)
            {
                return;
            }

            int idValue = viewportType.Id.IntegerValue;
            if (idValue < 0 || itemsById.ContainsKey(idValue))
            {
                return;
            }

            string familyName = viewportType.FamilyName ?? string.Empty;
            string displayName = string.IsNullOrWhiteSpace(familyName) ||
                                 string.Equals(familyName, viewportType.Name, System.StringComparison.OrdinalIgnoreCase)
                ? viewportType.Name
                : familyName + " : " + viewportType.Name;

            itemsById.Add(
                idValue,
                new DoorWindowNamedElementItem(viewportType.Id, viewportType.Name, displayName));
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

            string normalized = text.Trim().ToLowerInvariant();
            return normalized.Contains("viewport") ||
                   normalized.Contains("view title") ||
                   normalized.Contains("видовой экран") ||
                   normalized.Contains("заголовок вида");
        }

        public DoorWindowViewTemplateItem FindTemplate(
            IList<DoorWindowViewTemplateItem> templates,
            string templateName)
        {
            if (templates == null || templates.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                if (string.Equals(templates[i].Name, templateName, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    return templates[i];
                }
            }

            return templates[0];
        }

        public DoorWindowNamedElementItem FindNamedItem(
            IList<DoorWindowNamedElementItem> items,
            string itemName)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Name, itemName, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    return items[i];
                }
            }

            return items[0];
        }

        public DoorWindowNamedElementItem FindNamedItem(
            IList<DoorWindowNamedElementItem> items,
            int itemIdValue,
            string itemName)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            if (itemIdValue >= 0)
            {
                for (int index = 0; index < items.Count; index++)
                {
                    DoorWindowNamedElementItem item = items[index];
                    if (item != null && item.Id != null && item.Id.IntegerValue == itemIdValue)
                    {
                        return item;
                    }
                }
            }

            return FindNamedItem(items, itemName);
        }
    }
}
