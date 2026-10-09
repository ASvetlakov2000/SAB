using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowSelectionService
    {
        public DoorWindowSelectionData PickDoorOrWindow(UIDocument uiDocument)
        {
            if (uiDocument == null || uiDocument.Document == null)
            {
                throw new ArgumentNullException(nameof(uiDocument));
            }

            Document hostDocument = uiDocument.Document;
            Reference reference = TryGetPreselectedReference(uiDocument, hostDocument);
            if (reference == null)
            {
                DoorWindowSelectionFilter filter = new DoorWindowSelectionFilter(hostDocument);
                reference = uiDocument.Selection.PickObject(
                    ObjectType.PointOnElement,
                    filter,
                    "Выберите дверь, окно или витражную стену в рабочей модели либо в связи");
            }

            return CreateSelectionData(hostDocument, reference);
        }

        public IList<DoorWindowSelectionData> GetPreselectedDoorsAndWindows(UIDocument uiDocument)
        {
            List<DoorWindowSelectionData> result = new List<DoorWindowSelectionData>();
            if (uiDocument == null || uiDocument.Document == null)
            {
                return result;
            }

            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null)
            {
                return result;
            }

            foreach (ElementId id in selectedIds)
            {
                Element element = uiDocument.Document.GetElement(id);
                if (IsSupportedElement(element))
                {
                    result.Add(CreateSelectionData(uiDocument.Document, new Reference(element)));
                }
            }

            return result;
        }

        public IList<DoorWindowSelectionData> PickDoorsAndWindows(UIDocument uiDocument)
        {
            if (uiDocument == null || uiDocument.Document == null)
            {
                throw new ArgumentNullException(nameof(uiDocument));
            }

            Document hostDocument = uiDocument.Document;
            DoorWindowSelectionFilter filter = new DoorWindowSelectionFilter(hostDocument);
            IList<Reference> references = uiDocument.Selection.PickObjects(
                ObjectType.PointOnElement,
                filter,
                "Выберите двери, окна и витражные стены в рабочей модели либо в связях и нажмите «Готово»");

            List<DoorWindowSelectionData> result = new List<DoorWindowSelectionData>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (references == null)
            {
                return result;
            }

            for (int i = 0; i < references.Count; i++)
            {
                DoorWindowSelectionData item = CreateSelectionData(hostDocument, references[i]);
                if (item != null && keys.Add(item.SelectionKey))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public IList<DoorWindowSelectionData> PickDoorsAndWindowsByRectangle(UIDocument uiDocument)
        {
            if (uiDocument == null || uiDocument.Document == null)
            {
                throw new ArgumentNullException(nameof(uiDocument));
            }

            Document hostDocument = uiDocument.Document;
            DoorWindowRectangleSelectionFilter filter = new DoorWindowRectangleSelectionFilter();
            IList<Element> elements = uiDocument.Selection.PickElementsByRectangle(
                filter,
                "Обведите рамкой двери, окна и витражные стены в рабочей модели");

            List<DoorWindowSelectionData> result = new List<DoorWindowSelectionData>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (elements == null)
            {
                return result;
            }

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                if (!IsSupportedElement(element))
                {
                    continue;
                }

                DoorWindowSelectionData item = CreateSelectionData(hostDocument, new Reference(element));
                if (item != null && keys.Add(item.SelectionKey))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private Reference TryGetPreselectedReference(UIDocument uiDocument, Document document)
        {
            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count != 1)
            {
                return null;
            }

            Element element = document.GetElement(selectedIds.First());
            return IsSupportedElement(element) ? new Reference(element) : null;
        }

        private DoorWindowSelectionData CreateSelectionData(Document hostDocument, Reference reference)
        {
            if (reference == null)
            {
                throw new InvalidOperationException("Дверь, окно или витражная стена не выбраны.");
            }

            Element sourceElement;
            Document sourceDocument;
            RevitLinkInstance linkInstance = null;
            Transform sourceToHost = Transform.Identity;

            if (IsValidId(reference.LinkedElementId))
            {
                linkInstance = hostDocument.GetElement(reference.ElementId) as RevitLinkInstance;
                if (linkInstance == null)
                {
                    throw new InvalidOperationException("Не удалось определить экземпляр связи.");
                }

                sourceDocument = linkInstance.GetLinkDocument();
                if (sourceDocument == null)
                {
                    throw new InvalidOperationException("Связанный файл не загружен.");
                }

                sourceElement = sourceDocument.GetElement(reference.LinkedElementId);
                sourceToHost = linkInstance.GetTotalTransform();
            }
            else
            {
                sourceDocument = hostDocument;
                sourceElement = hostDocument.GetElement(reference.ElementId);
            }

            if (!IsSupportedElement(sourceElement))
            {
                throw new InvalidOperationException("Выбранный элемент не является дверью, окном или витражной стеной.");
            }

            FamilyInstance familyInstance = sourceElement as FamilyInstance;
            ElementType elementType = sourceDocument.GetElement(sourceElement.GetTypeId()) as ElementType;

            DoorWindowSelectionData data = new DoorWindowSelectionData();
            data.HostDocument = hostDocument;
            data.SourceDocument = sourceDocument;
            data.Element = sourceElement;
            data.ElementType = elementType;
            data.LinkInstance = linkInstance;
            data.SourceToHostTransform = sourceToHost;
            data.CategoryName = sourceElement is Wall && ((Wall)sourceElement).CurtainGrid != null
                ? "Витражи"
                : (sourceElement.Category != null ? sourceElement.Category.Name : string.Empty);
            data.FamilyName = familyInstance != null && familyInstance.Symbol != null
                ? familyInstance.Symbol.FamilyName
                : GetSystemFamilyName(sourceElement, elementType);
            data.TypeName = elementType != null ? elementType.Name : string.Empty;
            data.LinkName = linkInstance != null ? linkInstance.Name : string.Empty;

            DoorWindowGeometryService geometryService = new DoorWindowGeometryService();
            geometryService.PopulateCoordinateSystem(data);
            return data;
        }

        internal static bool IsSupportedElement(Element element)
        {
            if (element == null || element.Category == null)
            {
                return false;
            }

            int categoryId = element.Category.Id.IntegerValue;
            if (categoryId == (int)BuiltInCategory.OST_Doors ||
                categoryId == (int)BuiltInCategory.OST_Windows)
            {
                return true;
            }

            Wall wall = element as Wall;
            return categoryId == (int)BuiltInCategory.OST_Walls &&
                   wall != null && wall.CurtainGrid != null;
        }

        private static string GetSystemFamilyName(Element element, ElementType elementType)
        {
            Wall wall = element as Wall;
            if (wall != null && wall.CurtainGrid != null)
            {
                return "Витражная стена";
            }

            return elementType != null ? elementType.FamilyName : string.Empty;
        }

        private static bool IsValidId(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId && id.IntegerValue != -1;
        }

        private class DoorWindowSelectionFilter : ISelectionFilter
        {
            private readonly Document _hostDocument;

            public DoorWindowSelectionFilter(Document hostDocument)
            {
                _hostDocument = hostDocument;
            }

            public bool AllowElement(Element element)
            {
                return IsSupportedElement(element) || element is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                if (reference == null)
                {
                    return false;
                }

                if (IsValidId(reference.LinkedElementId))
                {
                    RevitLinkInstance link = _hostDocument.GetElement(reference.ElementId) as RevitLinkInstance;
                    Document linkDocument = link != null ? link.GetLinkDocument() : null;
                    return linkDocument != null && IsSupportedElement(linkDocument.GetElement(reference.LinkedElementId));
                }

                return IsSupportedElement(_hostDocument.GetElement(reference.ElementId));
            }
        }

        private class DoorWindowRectangleSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return IsSupportedElement(element);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
