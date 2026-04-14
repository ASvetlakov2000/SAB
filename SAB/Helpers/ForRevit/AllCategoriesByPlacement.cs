using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB
{
    internal static class AllCategoriesByPlacement
    {
        // Словарь категорий по типу размещения (инициализируется безопасно для разных версий Revit)
        public static readonly Dictionary<string, BuiltInCategory[]> CategoriesByPlacement = BuildCategories();

        private static Dictionary<string, BuiltInCategory[]> BuildCategories()
        {
            Dictionary<string, BuiltInCategory[]> result = new Dictionary<string, BuiltInCategory[]>();

            try
            {
                result["LineBased"] = BuildGroup(
                    "OST_Walls",
                    "OST_Columns",
                    "OST_StructuralColumns",
                    "OST_Railings",
                    "OST_StairsRailing",
                    "OST_Ramps",
                    "OST_PipeCurves",
                    "OST_DuctCurves",
                    "OST_CableTray",
                    "OST_Conduit");

                result["ContourBased"] = BuildGroup(
                    "OST_Floors",
                    "OST_Ceilings",
                    "OST_Roofs",
                    "OST_FabricAreas",
                    "OST_MassFloor",
                    "OST_Areas",
                    "OST_RoomSeparationLines");

                result["HostBased"] = BuildGroup(
                    "OST_Doors",
                    "OST_Windows");

                result["PointBased"] = BuildGroup(
                    "OST_Furniture",
                    "OST_Casework",
                    "OST_MechanicalEquipment",
                    "OST_PlumbingFixtures",
                    "OST_LightingFixtures",
                    "OST_ElectricalEquipment",
                    "OST_ElectricalFixtures",
                    "OST_CommunicationDevices",
                    "OST_FireAlarmDevices",
                    "OST_DataDevices",
                    "OST_NurseCallDevices",
                    "OST_SecurityDevices",
                    "OST_SpecialityEquipment",
                    "OST_LightingDevices",
                    "OST_Parking",
                    "OST_Cameras");

                result["MassBased"] = BuildGroup(
                    "OST_Mass",
                    "OST_GenericModel",
                    "OST_Entourage");

                result["Annotation"] = BuildGroup(
                    "OST_TextNotes",
                    "OST_DetailComponents",
                    "OST_KeynoteTags",
                    "OST_Dimensions",
                    "OST_Tags",
                    "OST_ColorFillLegends");

                result["Structural"] = BuildGroup(
                    "OST_StructuralFoundation",
                    "OST_Rebar");

                result["Helper"] = BuildGroup(
                    "OST_Levels",
                    "OST_Grids",
                    "OST_DesignOptions",
                    "OST_Viewports",
                    "OST_Sheets",
                    "OST_Views",
                    "OST_Schedules",
                    "OST_Materials");
            }
            catch
            {
                // В случае неожиданных ошибок возвращаем то, что удалось собрать.
            }

            return result;
        }

        private static BuiltInCategory[] BuildGroup(params string[] categoryNames)
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>();

            for (int i = 0; i < categoryNames.Length; i++)
            {
                string name = categoryNames[i];

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                try
                {
                    BuiltInCategory category;

                    if (!Enum.TryParse(name, out category))
                    {
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(BuiltInCategory), category))
                    {
                        continue;
                    }

                    categories.Add(category);
                }
                catch
                {
                    // Категория недоступна в текущей версии Revit, пропускаем.
                }
            }

            return categories.ToArray();
        }
    }
}
