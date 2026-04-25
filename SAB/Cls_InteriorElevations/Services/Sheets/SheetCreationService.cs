using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class SheetCreationService
    {
        public ViewSheet CreateSheet(
            Document document,
            ElevationSettings settings,
            RoomData roomData,
            ElevationNamingService namingService)
        {
            if (document == null || settings == null || namingService == null)
            {
                return null;
            }

            if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                return null;
            }

            FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                return null;
            }

            ViewSheet sheet = ViewSheet.Create(document, settings.TitleBlockTypeId);
            if (sheet == null)
            {
                return null;
            }

            // Блок уникальных параметров листа.
            sheet.Name = namingService.GenerateUniqueSheetName(roomData);
            sheet.SheetNumber = namingService.GenerateUniqueSheetNumber(roomData);

            return sheet;
        }
    }
}
