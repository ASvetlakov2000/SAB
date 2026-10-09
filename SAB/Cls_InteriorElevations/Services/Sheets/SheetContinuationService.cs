using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Services.Elevations;

namespace SAB.InteriorElevations.Services.Sheets
{
    public sealed class SheetContinuationService
    {
        private readonly SheetWorkspaceService _workspace;

        public SheetContinuationService(SheetWorkspaceService workspace)
        {
            _workspace = workspace;
        }
        public ViewSheet Create(Document document, ViewSheet source, string baseName, string baseNumber,
            int part, ElevationNamingService namingService)
        {
            var sourceBlocks = new List<FamilyInstance>();
            foreach (FamilyInstance block in new FilteredElementCollector(document, source.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType()) sourceBlocks.Add(block);
            if (sourceBlocks.Count == 0) throw new InvalidOperationException("На исходном листе нет основной надписи.");
            var sheet = ViewSheet.Create(document, ElementId.InvalidElementId);
            CopyParameters(source, sheet);
            sheet.Name = namingService.GenerateUniqueSheetPartName(baseName, part);
            sheet.SheetNumber = namingService.GenerateUniqueSheetPartNumber(baseNumber, part);
            foreach (var sourceBlock in sourceBlocks)
            {
                var symbol = document.GetElement(sourceBlock.GetTypeId()) as FamilySymbol;
                if (symbol == null) throw new InvalidOperationException("Не найден тип основной надписи.");
                if (!symbol.IsActive) { symbol.Activate(); document.Regenerate(); }
                var sourceLocation = sourceBlock.Location as LocationPoint;
                var block = document.Create.NewFamilyInstance(sourceLocation != null ? sourceLocation.Point : XYZ.Zero, symbol, sheet);
                CopyParameters(sourceBlock, block);
                var location = block.Location as LocationPoint;
                if (sourceLocation != null && location != null && Math.Abs(sourceLocation.Rotation - location.Rotation) > 1e-9)
                    ElementTransformUtils.RotateElement(document, block.Id,
                        Line.CreateBound(location.Point, location.Point + XYZ.BasisZ), sourceLocation.Rotation - location.Rotation);
            }
            // Some templates draw the two frames as detail lines directly on the sheet.
            var frames = _workspace.ReadFrames(document, source);
            var footprints = new SheetFootprintService();
            var frameIds = new List<ElementId>();
            foreach (CurveElement curve in new FilteredElementCollector(document, source.Id).OfClass(typeof(CurveElement)))
                if (footprints.IsFrameEdge(curve.GeometryCurve, frames)) frameIds.Add(curve.Id);
            if (frameIds.Count > 0)
                ElementTransformUtils.CopyElements(source, frameIds, sheet, Transform.Identity, new CopyPasteOptions());
            document.Regenerate();
            return sheet;
        }

        private void CopyParameters(Element source, Element target)
        {
            foreach (Parameter parameter in source.Parameters)
            {
                if (!parameter.HasValue || parameter.IsReadOnly || parameter.Definition == null) continue;
                var builtIn = parameter.Definition as InternalDefinition;
                if (builtIn != null && (builtIn.BuiltInParameter == BuiltInParameter.SHEET_NAME ||
                    builtIn.BuiltInParameter == BuiltInParameter.SHEET_NUMBER ||
                    builtIn.BuiltInParameter == BuiltInParameter.ELEM_TYPE_PARAM)) continue;
                var destination = builtIn != null && builtIn.BuiltInParameter != BuiltInParameter.INVALID
                    ? target.get_Parameter(builtIn.BuiltInParameter) : target.LookupParameter(parameter.Definition.Name);
                if (destination == null || destination.IsReadOnly || destination.StorageType != parameter.StorageType) continue;
                switch (parameter.StorageType)
                {
                    case StorageType.String: destination.Set(parameter.AsString() ?? string.Empty); break;
                    case StorageType.Integer: destination.Set(parameter.AsInteger()); break;
                    case StorageType.Double: destination.Set(parameter.AsDouble()); break;
                    case StorageType.ElementId: destination.Set(parameter.AsElementId()); break;
                }
            }
        }
    }
}
