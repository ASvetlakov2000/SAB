using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowViewIsolationService
    {
        public void IsolateSelection(
            DoorWindowSelectionData selection,
            DoorWindowViewCreationResult views,
            IList<string> warnings)
        {
            if (selection == null || views == null)
            {
                return;
            }

            if (selection.IsLinked)
            {
                AddWarning(warnings, selection, "изоляция элемента из связанного файла пока недоступна");
                return;
            }

            Wall wall = selection.Element as Wall;
            CurtainGrid curtainGrid = wall != null ? wall.CurtainGrid : null;
            HashSet<ElementId> elementIds = new HashSet<ElementId>();
            if (selection.Element != null)
            {
                elementIds.Add(selection.Element.Id);
            }

            if (curtainGrid != null)
            {
                HashSet<ElementId> curtainContentIds = new HashSet<ElementId>();
                AddIds(curtainGrid.GetPanelIds(), curtainContentIds);
                AddIds(curtainGrid.GetMullionIds(), curtainContentIds);
                AddIds(curtainGrid.GetUGridLineIds(), curtainContentIds);
                AddIds(curtainGrid.GetVGridLineIds(), curtainContentIds);
                ExpandCurtainSystemContent(selection.SourceDocument, curtainContentIds);
                foreach (ElementId contentId in curtainContentIds)
                {
                    elementIds.Add(contentId);
                }
            }

            IsolateInView(views.TopView, elementIds, selection, warnings);
            IsolateInView(views.FrontView, elementIds, selection, warnings);
            IsolateInView(views.SectionView, elementIds, selection, warnings);
        }

        private void AddIds(ICollection<ElementId> sourceIds, ISet<ElementId> targetIds)
        {
            if (sourceIds == null || targetIds == null)
            {
                return;
            }

            foreach (ElementId id in sourceIds)
            {
                if (id != null && id != ElementId.InvalidElementId)
                {
                    targetIds.Add(id);
                }
            }
        }

        private void ExpandCurtainSystemContent(Document document, ISet<ElementId> targetIds)
        {
            if (document == null || targetIds == null || targetIds.Count == 0)
            {
                return;
            }

            Queue<ElementId> pendingIds = new Queue<ElementId>(targetIds);
            HashSet<ElementId> processedIds = new HashSet<ElementId>();
            while (pendingIds.Count > 0)
            {
                ElementId currentId = pendingIds.Dequeue();
                if (currentId == null ||
                    currentId == ElementId.InvalidElementId ||
                    !processedIds.Add(currentId))
                {
                    continue;
                }

                Element element = document.GetElement(currentId);
                if (element == null)
                {
                    continue;
                }

                FamilyInstance familyInstance = element as FamilyInstance;
                if (familyInstance != null)
                {
                    AddAndQueue(GetDependentElementIds(element), targetIds, pendingIds);
                    AddAndQueue(GetSubComponentIds(familyInstance), targetIds, pendingIds);
                }
                else if (!(element is Wall))
                {
                    AddAndQueue(GetDependentElementIds(element), targetIds, pendingIds);
                }
            }
        }

        private ICollection<ElementId> GetDependentElementIds(Element element)
        {
            try
            {
                return element.GetDependentElements(null);
            }
            catch
            {
                return new List<ElementId>();
            }
        }

        private ICollection<ElementId> GetSubComponentIds(FamilyInstance familyInstance)
        {
            try
            {
                return familyInstance.GetSubComponentIds();
            }
            catch
            {
                return new List<ElementId>();
            }
        }

        private void AddAndQueue(
            ICollection<ElementId> sourceIds,
            ISet<ElementId> targetIds,
            Queue<ElementId> pendingIds)
        {
            if (sourceIds == null)
            {
                return;
            }

            foreach (ElementId id in sourceIds)
            {
                if (id != null &&
                    id != ElementId.InvalidElementId &&
                    targetIds.Add(id))
                {
                    pendingIds.Enqueue(id);
                }
            }
        }

        private void IsolateInView(
            View view,
            ICollection<ElementId> elementIdsToKeep,
            DoorWindowSelectionData selection,
            IList<string> warnings)
        {
            if (view == null || elementIdsToKeep == null || elementIdsToKeep.Count == 0)
            {
                return;
            }

            try
            {
                EnsureCurtainSystemCategoriesVisible(view, selection, warnings);
                HashSet<ElementId> keepIds = new HashSet<ElementId>(elementIdsToKeep);
                List<ElementId> idsToHide = new FilteredElementCollector(view.Document, view.Id)
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(element => ShouldHideElement(element, view, keepIds))
                    .Select(element => element.Id)
                    .Distinct()
                    .ToList();

                if (idsToHide.Count == 0)
                {
                    return;
                }

                HidePermanently(view, idsToHide, selection, warnings);
            }
            catch (Exception exception)
            {
                AddWarning(
                    warnings,
                    selection,
                    "не удалось изолировать элемент на виде «" + view.Name + "»: " + exception.Message);
            }
        }

        private void EnsureCurtainSystemCategoriesVisible(
            View view,
            DoorWindowSelectionData selection,
            IList<string> warnings)
        {
            BuiltInCategory[] categories =
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_CurtainWallPanels,
                BuiltInCategory.OST_CurtainWallMullions,
                BuiltInCategory.OST_CurtainGrids,
                BuiltInCategory.OST_CurtainGridsWall,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Windows
            };

            foreach (BuiltInCategory builtInCategory in categories)
            {
                Category category = Category.GetCategory(view.Document, builtInCategory);
                if (category == null)
                {
                    continue;
                }

                try
                {
                    if (view.CanCategoryBeHidden(category.Id) && view.GetCategoryHidden(category.Id))
                    {
                        view.SetCategoryHidden(category.Id, false);
                    }
                }
                catch (Exception exception)
                {
                    AddWarning(
                        warnings,
                        selection,
                        "на виде «" + view.Name + "» не включена категория «" + category.Name +
                        "»: " + exception.Message);
                }
            }
        }

        private bool ShouldHideElement(Element element, View view, ISet<ElementId> keepIds)
        {
            if (element == null || keepIds.Contains(element.Id))
            {
                return false;
            }

            Category category = element.Category;
            if (category == null || category.CategoryType != CategoryType.Model)
            {
                return false;
            }

            try
            {
                return !element.IsHidden(view) && element.CanBeHidden(view);
            }
            catch
            {
                return false;
            }
        }

        private void HidePermanently(
            View view,
            IList<ElementId> idsToHide,
            DoorWindowSelectionData selection,
            IList<string> warnings)
        {
            const int batchSize = 256;
            for (int offset = 0; offset < idsToHide.Count; offset += batchSize)
            {
                List<ElementId> batch = idsToHide
                    .Skip(offset)
                    .Take(batchSize)
                    .ToList();
                try
                {
                    view.HideElements(batch);
                }
                catch
                {
                    foreach (ElementId id in batch)
                    {
                        try
                        {
                            view.HideElements(new List<ElementId> { id });
                        }
                        catch (Exception exception)
                        {
                            AddWarning(
                                warnings,
                                selection,
                                "на виде «" + view.Name + "» не скрыт элемент " +
                                id.IntegerValue + ": " + exception.Message);
                        }
                    }
                }
            }
        }

        private void AddWarning(
            IList<string> warnings,
            DoorWindowSelectionData selection,
            string message)
        {
            if (warnings != null)
            {
                warnings.Add((selection != null ? selection.DisplayName.Replace("\n", " ") : "Витраж") + ": " + message + ".");
            }
        }
    }
}
