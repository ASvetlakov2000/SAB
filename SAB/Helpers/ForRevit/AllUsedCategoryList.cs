using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB
{
    internal static class AllUsedCategoryList
    {
        // Единый список категорий для экспорта (собирается динамически и безопасно для Revit 2023/2024)
        public static readonly IEnumerable<BuiltInCategory> categoryList = BuildCategoryList();

        private static List<BuiltInCategory> BuildCategoryList()
        {
            List<BuiltInCategory> result = new List<BuiltInCategory>();

            try
            {
                // Блок с категориями, который можно безопасно дополнять для экспорта
                AddCategorySafe(result, "OST_Walls");
                AddCategorySafe(result, "OST_Floors");
                AddCategorySafe(result, "OST_Ceilings");
                AddCategorySafe(result, "OST_Columns");
                AddCategorySafe(result, "OST_StructuralColumns");
                AddCategorySafe(result, "OST_Roofs");
                AddCategorySafe(result, "OST_Doors");
                AddCategorySafe(result, "OST_Windows");
                AddCategorySafe(result, "OST_Stairs");
                AddCategorySafe(result, "OST_StairsRailing");
                AddCategorySafe(result, "OST_Ramps");
                AddCategorySafe(result, "OST_Furniture");
                AddCategorySafe(result, "OST_CurtainWallMullions");
                AddCategorySafe(result, "OST_CurtainWallPanels");
                AddCategorySafe(result, "OST_GenericModel");
                AddCategorySafe(result, "OST_MechanicalEquipment");
                AddCategorySafe(result, "OST_PipeCurves");
                AddCategorySafe(result, "OST_PipeFitting");
                AddCategorySafe(result, "OST_DuctCurves");
                AddCategorySafe(result, "OST_DuctFitting");
                AddCategorySafe(result, "OST_PlumbingFixtures");
                AddCategorySafe(result, "OST_LightingFixtures");
                AddCategorySafe(result, "OST_ElectricalEquipment");
                AddCategorySafe(result, "OST_ElectricalFixtures");
                AddCategorySafe(result, "OST_CableTray");
                AddCategorySafe(result, "OST_Conduit");
                AddCategorySafe(result, "OST_Casework");
                AddCategorySafe(result, "OST_CommunicationDevices");
                AddCategorySafe(result, "OST_FireAlarmDevices");
                AddCategorySafe(result, "OST_DataDevices");
                AddCategorySafe(result, "OST_NurseCallDevices");
                AddCategorySafe(result, "OST_SecurityDevices");
                AddCategorySafe(result, "OST_FurnitureSystems");
                AddCategorySafe(result, "OST_SpecialityEquipment");
                AddCategorySafe(result, "OST_LightingDevices");
                AddCategorySafe(result, "OST_Parking");
                AddCategorySafe(result, "OST_Railings");
                AddCategorySafe(result, "OST_Topography");
                AddCategorySafe(result, "OST_Entourage");
                AddCategorySafe(result, "OST_Mass");
                AddCategorySafe(result, "OST_MassFloor");
                AddCategorySafe(result, "OST_AnalyticalNodes");
                AddCategorySafe(result, "OST_Rebar");
                AddCategorySafe(result, "OST_Areas");
                AddCategorySafe(result, "OST_Rooms");
                AddCategorySafe(result, "OST_FabricAreas");
                AddCategorySafe(result, "OST_FabricReinforcement");
                AddCategorySafe(result, "OST_FabricationPipework");
                AddCategorySafe(result, "OST_FabricationDuctwork");
                AddCategorySafe(result, "OST_MEPSpaces");
                AddCategorySafe(result, "OST_PointClouds");
                AddCategorySafe(result, "OST_RoomSeparationLines");
                AddCategorySafe(result, "OST_Grids");
                AddCategorySafe(result, "OST_Levels");
                AddCategorySafe(result, "OST_DesignOptions");
                AddCategorySafe(result, "OST_Viewports");
                AddCategorySafe(result, "OST_Sheets");
                AddCategorySafe(result, "OST_Views");
                AddCategorySafe(result, "OST_Schedules");
                AddCategorySafe(result, "OST_Materials");
                AddCategorySafe(result, "OST_Tags");
                AddCategorySafe(result, "OST_Revisions");
                AddCategorySafe(result, "OST_StructuralFoundation");
                AddCategorySafe(result, "OST_Cameras");
                AddCategorySafe(result, "OST_TextNotes");
                AddCategorySafe(result, "OST_DetailComponents");
                AddCategorySafe(result, "OST_KeynoteTags");
                AddCategorySafe(result, "OST_Dimensions");
                AddCategorySafe(result, "OST_Coordination_Model");
                AddCategorySafe(result, "OST_ColorFillLegends");
                AddCategorySafe(result, "OST_Parts");
                AddCategorySafe(result, "OST_Assemblies");
            }
            catch
            {
                // В случае неожиданных ошибок возвращаем уже собранную часть списка.
            }

            return result;
        }

        private static void AddCategorySafe(List<BuiltInCategory> target, string categoryName)
        {
            if (target == null || string.IsNullOrWhiteSpace(categoryName))
            {
                return;
            }

            try
            {
                BuiltInCategory category;

                if (!Enum.TryParse(categoryName, out category))
                {
                    return;
                }

                if (!Enum.IsDefined(typeof(BuiltInCategory), category))
                {
                    return;
                }

                if (!target.Contains(category))
                {
                    target.Add(category);
                }
            }
            catch
            {
                // Категория отсутствует в текущей версии Revit.
            }
        }
    }
}
