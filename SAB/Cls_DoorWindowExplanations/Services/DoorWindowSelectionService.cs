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
                    "Выберите экземпляр двери или окна в рабочей модели либо в связи");
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
                if (IsDoorOrWindow(element))
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
                "Выберите двери и окна в рабочей модели либо в связях и нажмите «Готово»");

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

        private Reference TryGetPreselectedReference(UIDocument uiDocument, Document document)
        {
            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count != 1)
            {
                return null;
            }

            Element element = document.GetElement(selectedIds.First());
            return IsDoorOrWindow(element) ? new Reference(element) : null;
        }

        private DoorWindowSelectionData CreateSelectionData(Document hostDocument, Reference reference)
        {
            if (reference == null)
            {
                throw new InvalidOperationException("Дверь или окно не выбраны.");
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

            if (!IsDoorOrWindow(sourceElement))
            {
                throw new InvalidOperationException("Выбранный элемент не является дверью или окном.");
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
            data.CategoryName = sourceElement.Category != null ? sourceElement.Category.Name : string.Empty;
            data.FamilyName = familyInstance != null && familyInstance.Symbol != null
                ? familyInstance.Symbol.FamilyName
                : string.Empty;
            data.TypeName = elementType != null ? elementType.Name : string.Empty;
            data.LinkName = linkInstance != null ? linkInstance.Name : string.Empty;

            DoorWindowGeometryService geometryService = new DoorWindowGeometryService();
            geometryService.PopulateCoordinateSystem(data);
            return data;
        }

        internal static bool IsDoorOrWindow(Element element)
        {
            if (element == null || element.Category == null)
            {
                return false;
            }

            int categoryId = element.Category.Id.IntegerValue;
            return categoryId == (int)BuiltInCategory.OST_Doors ||
                   categoryId == (int)BuiltInCategory.OST_Windows;
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
                return IsDoorOrWindow(element) || element is RevitLinkInstance;
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
                    return linkDocument != null && IsDoorOrWindow(linkDocument.GetElement(reference.LinkedElementId));
                }

                return IsDoorOrWindow(_hostDocument.GetElement(reference.ElementId));
            }
        }
    }
}
