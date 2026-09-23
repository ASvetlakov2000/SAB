using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public sealed class FrameDocumentDataService
    {
        private readonly IFrameMetadataService _metadataService;

        public FrameDocumentDataService(IFrameMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        public IList<ElementTypeOption> GetCurtainWallTypes(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(item => item.Kind == WallKind.Curtain)
                .OrderBy(item => item.Name)
                .Select(item => new ElementTypeOption(item.Id, item.Name))
                .ToList();
        }

        public IList<ElementTypeOption> GetMullionTypes(Document document)
        {
            // MullionType и Mullion не поддерживаются нативным ElementClassFilter
            // в Revit 2023, хотя доступны как CLR-типы. Сначала фильтруем категорию.
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                .WhereElementIsElementType()
                .OfType<MullionType>()
                .OrderBy(item => item.Name)
                .Select(item => new ElementTypeOption(item.Id, item.Name))
                .ToList();
        }

        public IList<Mullion> GetCalculationMullions(Document document)
        {
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                .WhereElementIsNotElementType()
                .OfType<Mullion>()
                .Where(item => _metadataService.IsCalculationFrame(item))
                .ToList();
        }
    }

    internal sealed class FrameMullionTypeSet
    {
        public MullionType Stud { get; set; }
        public MullionType StudInverted { get; set; }
        public MullionType Track { get; set; }
        public MullionType TrackInverted { get; set; }
        public MullionType DoorStud { get; set; }
        public MullionType DoorStudInverted { get; set; }
    }

    internal static class FrameMullionTypeResolver
    {
        private sealed class KeepDestinationTypesHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }

        public static FrameMullionTypeSet Resolve(
            Document document,
            double coreWidthInternal,
            bool hasDoors,
            Document catalogDocument)
        {
            double widthMm = RevitUnitService.InternalToMillimeters(coreWidthInternal);
            int width = new[] { 50, 75, 100 }
                .Where(candidate => Math.Abs(widthMm - candidate) <= 1.0)
                .DefaultIfEmpty(0)
                .First();
            if (width == 0)
            {
                throw new InvalidOperationException(
                    "Толщина сердцевины " + Math.Round(widthMm, 2) +
                    " мм не соответствует поддерживаемым профилям 50, 75 или 100 мм.");
            }

            Dictionary<string, MullionType> types = GetMullionTypesByName(document);

            string suffix = width.ToString();
            List<string> requiredNames = new List<string>
            {
                "SA_Knauf_ПС_" + suffix,
                "SA_Knauf_ПС_" + suffix + "_Инв",
                "SA_Knauf_ПН_" + suffix,
                "SA_Knauf_ПН_" + suffix + "_Инв"
            };
            if (hasDoors)
            {
                requiredNames.Add("SA_Knauf_ПС_UA_" + suffix);
                requiredNames.Add("SA_Knauf_ПС_UA_" + suffix + "_Инв");
            }

            List<string> missing = requiredNames.Where(name => !types.ContainsKey(name)).ToList();
            if (missing.Count > 0 && catalogDocument != null && catalogDocument != document)
            {
                Dictionary<string, MullionType> catalogTypes = GetMullionTypesByName(catalogDocument);
                List<ElementId> idsToCopy = missing
                    .Where(catalogTypes.ContainsKey)
                    .Select(name => catalogTypes[name].Id)
                    .ToList();
                if (idsToCopy.Count > 0)
                {
                    CopyPasteOptions copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new KeepDestinationTypesHandler());
                    ElementTransformUtils.CopyElements(
                        catalogDocument,
                        idsToCopy,
                        document,
                        Transform.Identity,
                        copyOptions);
                    document.Regenerate();
                    types = GetMullionTypesByName(document);
                    missing = requiredNames.Where(name => !types.ContainsKey(name)).ToList();
                }
            }

            if (missing.Count > 0)
            {
                string available = catalogDocument != null
                    ? string.Join(", ", GetMullionTypesByName(catalogDocument).Keys
                        .Where(name => name.IndexOf("Knauf", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(name => name)
                        .Take(24))
                    : "каталог не открыт";
                throw new InvalidOperationException(
                    "Для сердцевины " + suffix + " мм не найдены типы импостов: " +
                    string.Join(", ", missing) + ". В каталоге: " + available + ".");
            }

            return new FrameMullionTypeSet
            {
                Stud = types[requiredNames[0]],
                StudInverted = types[requiredNames[1]],
                Track = types[requiredNames[2]],
                TrackInverted = types[requiredNames[3]],
                DoorStud = hasDoors ? types[requiredNames[4]] : null,
                DoorStudInverted = hasDoors ? types[requiredNames[5]] : null
            };
        }

        private static Dictionary<string, MullionType> GetMullionTypesByName(Document document)
        {
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_CurtainWallMullions)
                .WhereElementIsElementType()
                .OfType<MullionType>()
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
