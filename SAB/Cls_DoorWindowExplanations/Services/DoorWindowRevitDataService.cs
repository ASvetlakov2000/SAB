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

            IEnumerable<ElementType> types = new FilteredElementCollector(document)
                .OfClass(typeof(ElementType))
                .OfCategory(BuiltInCategory.OST_Viewports)
                .Cast<ElementType>()
                .OrderBy(type => type.Name);

            foreach (ElementType type in types)
            {
                result.Add(new DoorWindowNamedElementItem(type.Id, type.Name));
            }

            return result;
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
    }
}
