using System;
using Autodesk.Revit.DB;

namespace RevitLibraryBuilder.Services.Views
{
    public class FloorPlanViewService
    {
        public ViewPlan Create(Document document, string baseViewName)
        {
            return Create(document, baseViewName, document != null ? document.ActiveView : null);
        }

        public ViewPlan Create(Document document, string baseViewName, View sourceView)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (string.IsNullOrWhiteSpace(baseViewName))
            {
                throw new ArgumentException("View name cannot be empty.", "baseViewName");
            }

            Level targetLevel = GetTargetLevel(document, sourceView);

            if (targetLevel == null)
            {
                return null;
            }

            ViewFamilyType floorPlanType = GetFloorPlanViewFamilyType(document);

            if (floorPlanType == null)
            {
                return null;
            }

            string uniqueViewName = GetUniqueViewName(document, baseViewName);

            // Block responsible for creating a new floor plan view on the selected level
            ViewPlan viewPlan = ViewPlan.Create(document, floorPlanType.Id, targetLevel.Id);

            if (viewPlan == null)
            {
                return null;
            }

            // Block responsible for applying the final unique name
            viewPlan.Name = uniqueViewName;

            return viewPlan;
        }

        // Block responsible for selecting the most suitable level for the new view
        private static Level GetTargetLevel(Document document, View sourceView)
        {
            if (sourceView != null)
            {
                ViewPlan sourcePlanView = sourceView as ViewPlan;

                if (sourcePlanView != null && sourcePlanView.GenLevel != null)
                {
                    return sourcePlanView.GenLevel;
                }

                Parameter levelParameter = sourceView.get_Parameter(BuiltInParameter.PLAN_VIEW_LEVEL);

                if (levelParameter != null && levelParameter.HasValue)
                {
                    ElementId levelId = levelParameter.AsElementId();

                    if (levelId != ElementId.InvalidElementId)
                    {
                        Level levelFromView = document.GetElement(levelId) as Level;

                        if (levelFromView != null)
                        {
                            return levelFromView;
                        }
                    }
                }
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(Level));

            Level firstLevel = null;

            foreach (Element element in collector)
            {
                Level level = element as Level;

                if (level == null)
                {
                    continue;
                }

                if (firstLevel == null)
                {
                    firstLevel = level;
                }

                if (Math.Abs(level.Elevation) < 0.0001)
                {
                    return level;
                }
            }

            return firstLevel;
        }

        private static ViewFamilyType GetFloorPlanViewFamilyType(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewFamilyType));

            foreach (Element element in collector)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;

                if (viewFamilyType != null && viewFamilyType.ViewFamily == ViewFamily.FloorPlan)
                {
                    return viewFamilyType;
                }
            }

            return null;
        }

        // Block responsible for preventing duplicate Revit view names
        private static string GetUniqueViewName(Document document, string baseViewName)
        {
            string candidateName = baseViewName.Trim();
            int index = 1;

            while (ViewNameExists(document, candidateName))
            {
                candidateName = baseViewName.Trim() + " " + index;
                index++;
            }

            return candidateName;
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
    }
}
