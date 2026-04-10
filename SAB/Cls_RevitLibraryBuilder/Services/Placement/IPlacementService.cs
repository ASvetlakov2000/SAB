using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Placement
{
    public interface IPlacementService
    {
        void Place(List<ElementTypeCsvModel> elements, Level level);
    }
}