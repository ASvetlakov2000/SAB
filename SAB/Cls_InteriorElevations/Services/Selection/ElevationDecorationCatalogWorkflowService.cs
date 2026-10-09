using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Services.Selection
{
    public class ElevationDecorationCatalogWorkflowService
    {
        private readonly ElevationDecorationCatalogService _catalogService;

        public ElevationDecorationCatalogWorkflowService()
        {
            _catalogService = new ElevationDecorationCatalogService();
        }

        public void Run(UIDocument uiDocument)
        {
            if (uiDocument == null || uiDocument.Document == null)
            {
                return;
            }

            ElevationDecorationCatalog catalog = _catalogService.Load();
            ElevationDecorationCatalogRole currentRole =
                ElevationDecorationCatalogRole.FinishFloor;

            while (true)
            {
                ElevationDecorationCatalogWindow window =
                    new ElevationDecorationCatalogWindow(catalog, currentRole);
                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true ||
                    window.RequestedAction == ElevationDecorationCatalogWindowAction.None)
                {
                    _catalogService.Save(catalog);
                    return;
                }

                currentRole = window.SelectedRole;
                if (window.RequestedAction ==
                    ElevationDecorationCatalogWindowAction.RemoveSelected)
                {
                    _catalogService.Remove(catalog, window.SelectedEntryKey);
                    _catalogService.Save(catalog);
                    continue;
                }

                if (window.RequestedAction ==
                    ElevationDecorationCatalogWindowAction.ClearRole)
                {
                    TaskDialog confirmation = new TaskDialog("SAB Каталог оформления")
                    {
                        MainInstruction = "Очистить выбранную роль?",
                        MainContent = "Будут удалены все типоразмеры роли «" +
                            ElevationDecorationCatalogRoleNames.GetDisplayName(currentRole) +
                            "».",
                        CommonButtons = TaskDialogCommonButtons.Yes |
                            TaskDialogCommonButtons.No,
                        DefaultButton = TaskDialogResult.No
                    };
                    if (confirmation.Show() == TaskDialogResult.Yes)
                    {
                        _catalogService.ClearRole(catalog, currentRole);
                        _catalogService.Save(catalog);
                    }

                    continue;
                }

                bool pickFromLink = window.RequestedAction ==
                    ElevationDecorationCatalogWindowAction.AddLinked;
                IList<Element> selectedElements;
                try
                {
                    selectedElements = PickElements(
                        uiDocument,
                        currentRole,
                        pickFromLink);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    continue;
                }

                int added = _catalogService.AddSelectedTypes(
                    catalog,
                    currentRole,
                    selectedElements);
                _catalogService.Save(catalog);
                ToastNotifier.ShowInfo(
                    "SAB Каталог оформления",
                    added > 0
                        ? "Добавлено типоразмеров: " + added + "."
                        : "Новых типоразмеров не добавлено.");
            }
        }

        private IList<Element> PickElements(
            UIDocument uiDocument,
            ElevationDecorationCatalogRole role,
            bool pickFromLink)
        {
            BuiltInCategory category = _catalogService.GetCategory(role);
            string roleName = ElevationDecorationCatalogRoleNames.GetDisplayName(role);
            IList<Reference> references = uiDocument.Selection.PickObjects(
                pickFromLink ? ObjectType.LinkedElement : ObjectType.Element,
                pickFromLink
                    ? (ISelectionFilter)new LinkedCategorySelectionFilter(
                        uiDocument.Document,
                        category)
                    : new HostCategorySelectionFilter(category),
                "Выберите экземпляры для роли «" + roleName + "» и нажмите Готово");

            if (!pickFromLink)
            {
                return references
                    .Select(reference => uiDocument.Document.GetElement(reference))
                    .Where(element => element != null)
                    .ToList();
            }

            List<Element> result = new List<Element>();
            foreach (Reference reference in references)
            {
                RevitLinkInstance linkInstance = uiDocument.Document.GetElement(
                    reference.ElementId) as RevitLinkInstance;
                Document linkedDocument = linkInstance != null
                    ? linkInstance.GetLinkDocument()
                    : null;
                Element linkedElement = linkedDocument != null
                    ? linkedDocument.GetElement(reference.LinkedElementId)
                    : null;
                if (linkedElement != null)
                {
                    result.Add(linkedElement);
                }
            }

            return result;
        }

        private static bool HasCategory(Element element, BuiltInCategory category)
        {
            return element != null && element.Category != null &&
                   RevitElementIdUtils.GetElementIdValue(element.Category.Id) ==
                   (long)category;
        }

        private class HostCategorySelectionFilter : ISelectionFilter
        {
            private readonly BuiltInCategory _category;

            public HostCategorySelectionFilter(BuiltInCategory category)
            {
                _category = category;
            }

            public bool AllowElement(Element element)
            {
                return HasCategory(element, _category);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        private class LinkedCategorySelectionFilter : ISelectionFilter
        {
            private readonly Document _hostDocument;
            private readonly BuiltInCategory _category;

            public LinkedCategorySelectionFilter(
                Document hostDocument,
                BuiltInCategory category)
            {
                _hostDocument = hostDocument;
                _category = category;
            }

            public bool AllowElement(Element element)
            {
                return element is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                RevitLinkInstance linkInstance = _hostDocument != null && reference != null
                    ? _hostDocument.GetElement(reference.ElementId) as RevitLinkInstance
                    : null;
                Document linkedDocument = linkInstance != null
                    ? linkInstance.GetLinkDocument()
                    : null;
                Element linkedElement = linkedDocument != null
                    ? linkedDocument.GetElement(reference.LinkedElementId)
                    : null;
                return HasCategory(linkedElement, _category);
            }
        }
    }
}
