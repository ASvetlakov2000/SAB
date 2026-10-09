using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.ParameterTools
{
    internal static class LevelService
    {
        internal static Level ElementLevel(Element element)
        { return ElementLevel(element, new HashSet<ElementId>()); }
        private static Level ElementLevel(Element element, HashSet<ElementId> visited)
        {
            if (element == null || !visited.Add(element.Id)) return null;
            var level = element.Document.GetElement(element.LevelId) as Level;
            if (level != null) return level;
            var wall = element as Wall;
            if (wall != null)
            {
                level = element.Document.GetElement(wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()) as Level;
                if (level != null) return level;
            }
            var family = element as FamilyInstance;
            if (family != null && family.Host != null && family.Host.Id != element.Id)
            {
                level = ElementLevel(family.Host, visited);
                if (level != null) return level;
            }
            var group = element as Group;
            if (group != null)
            {
                var parameter = group.get_Parameter(BuiltInParameter.GROUP_LEVEL);
                if (parameter != null) level = element.Document.GetElement(parameter.AsElementId()) as Level;
                if (level != null) return level;
            }
            if (element.GroupId != ElementId.InvalidElementId) return ElementLevel(element.Document.GetElement(element.GroupId), visited);
            return null;
        }
    }
}
