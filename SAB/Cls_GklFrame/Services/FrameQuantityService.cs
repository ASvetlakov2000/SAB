using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IFrameQuantityService
    {
        FrameCalculationResult Calculate(
            Document document,
            IEnumerable<Mullion> mullions,
            FrameCalculationOptions options);
    }

    public sealed class FrameQuantityService : IFrameQuantityService
    {
        private readonly IFrameMetadataService _metadataService;
        private readonly IMullionGeometryService _geometryService;
        private readonly IProfileMetadataProvider _profileMetadataProvider;
        private readonly ICuttingOptimizationService _cuttingService;

        public FrameQuantityService(
            IFrameMetadataService metadataService,
            IMullionGeometryService geometryService,
            IProfileMetadataProvider profileMetadataProvider,
            ICuttingOptimizationService cuttingService)
        {
            _metadataService = metadataService;
            _geometryService = geometryService;
            _profileMetadataProvider = profileMetadataProvider;
            _cuttingService = cuttingService;
        }

        public FrameCalculationResult Calculate(
            Document document,
            IEnumerable<Mullion> mullions,
            FrameCalculationOptions options)
        {
            FrameCalculationResult result = new FrameCalculationResult();
            HashSet<string> uniqueIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (Mullion mullion in mullions ?? Enumerable.Empty<Mullion>())
            {
                if (mullion == null || !_metadataService.IsCalculationFrame(mullion))
                {
                    continue;
                }

                if (!uniqueIds.Add(mullion.UniqueId))
                {
                    result.Warnings.Add("Дублированный Mullion: " + mullion.Id.IntegerValue + ".");
                    continue;
                }

                FrameMemberPurpose purpose;
                if (!_metadataService.TryGetPurpose(mullion, out purpose) || purpose == FrameMemberPurpose.Custom)
                {
                    result.Warnings.Add("Mullion " + mullion.Id.IntegerValue + " не имеет определённого ProfilePurpose.");
                    continue;
                }

                try
                {
                    double lengthInternal = _geometryService.GetLength(mullion);
                    if (lengthInternal <= 1e-9)
                    {
                        result.Warnings.Add("Mullion " + mullion.Id.IntegerValue + " имеет длину <= 0.");
                        continue;
                    }

                    ProfileMetadata metadata = _profileMetadataProvider.GetProfileMetadata(mullion, purpose);
                    if (metadata == null || string.IsNullOrWhiteSpace(metadata.ProfileType) || string.IsNullOrWhiteSpace(metadata.ProfileSize))
                    {
                        result.Warnings.Add("Mullion " + mullion.Id.IntegerValue + " имеет неизвестный ProfileType/ProfileSize.");
                        continue;
                    }

                    string sourceWallUniqueId = _metadataService.GetSourceWallUniqueId(mullion);
                    if (string.IsNullOrWhiteSpace(sourceWallUniqueId))
                    {
                        result.Warnings.Add("Mullion " + mullion.Id.IntegerValue + " не связан с исходной стеной SAB.");
                        continue;
                    }

                    Wall sourceWall = document.GetElement(sourceWallUniqueId) as Wall;
                    double purchaseLength = options.GetPurchaseLengthMm(metadata.ProfileFamily);
                    if (purchaseLength <= 0.0)
                    {
                        result.Warnings.Add("Для профиля " + metadata.ProfileType + " не задана закупочная длина.");
                        continue;
                    }

                    double lengthMm = RevitUnitService.InternalToMillimeters(lengthInternal);
                    FrameMemberData member = new FrameMemberData
                    {
                        ElementId = mullion.Id,
                        UniqueId = mullion.UniqueId,
                        SourceWallUniqueId = sourceWallUniqueId,
                        SourceOpeningUniqueId = _metadataService.GetSourceOpeningUniqueId(mullion),
                        SourceWallType = sourceWall != null && sourceWall.WallType != null ? sourceWall.WallType.Name : string.Empty,
                        SourceWallMark = sourceWall != null ? ReadString(sourceWall, BuiltInParameter.ALL_MODEL_MARK) : string.Empty,
                        SourceLevel = sourceWall != null && document.GetElement(sourceWall.LevelId) != null
                            ? document.GetElement(sourceWall.LevelId).Name
                            : string.Empty,
                        Purpose = purpose,
                        ProfileFamily = metadata.ProfileFamily,
                        ProfileType = metadata.ProfileType,
                        ProfileSize = metadata.ProfileSize,
                        NominalWidthMm = metadata.NominalWidthMm,
                        NominalFlangeHeightMm = metadata.NominalFlangeHeightMm,
                        NominalThicknessMm = metadata.NominalThicknessMm,
                        ActualLengthInternal = lengthInternal,
                        ActualLengthMm = lengthMm,
                        PurchaseLengthMm = purchaseLength
                    };
                    result.Members.Add(member);

                    if (lengthMm > purchaseLength + 1e-6)
                    {
                        result.Warnings.Add(
                            "Mullion " + mullion.Id.IntegerValue + " длиной " +
                            Math.Round(lengthMm, 1) + " мм не помещается в хлыст " +
                            Math.Round(purchaseLength, 1) + " мм.");
                    }
                }
                catch (Exception exception)
                {
                    result.Errors.Add("Mullion " + mullion.Id.IntegerValue + ": " + exception.Message);
                }
            }

            IEnumerable<IGrouping<string, FrameMemberData>> groups = result.Members.GroupBy(
                item => string.Join("|", item.ProfileFamily, item.ProfileType, item.ProfileSize, item.PurchaseLengthMm));

            foreach (IGrouping<string, FrameMemberData> group in groups)
            {
                List<FrameMemberData> members = group.ToList();
                FrameMemberData sample = members[0];
                CuttingPlan plan = null;
                if (options.PerformCutting)
                {
                    plan = _cuttingService.Optimize(
                        sample.ProfileFamily,
                        sample.ProfileType,
                        sample.ProfileSize,
                        members,
                        sample.PurchaseLengthMm,
                        options.CutLossMm,
                        options.MinimumReusableOffcutMm);
                    result.CuttingPlans.Add(plan);
                }

                result.ProfileSummaries.Add(new ProfileSummary
                {
                    ProfileFamily = sample.ProfileFamily,
                    ProfileType = sample.ProfileType,
                    ProfileSize = sample.ProfileSize,
                    MemberCount = members.Count,
                    TotalActualLengthMm = members.Sum(item => item.ActualLengthMm),
                    PurchaseLengthMm = sample.PurchaseLengthMm,
                    StockBarCount = plan != null ? plan.Bars.Count : 0,
                    TotalPurchasedLengthMm = plan != null ? plan.TotalPurchasedLengthMm : 0.0,
                    TotalWasteMm = plan != null ? plan.TotalWasteMm : 0.0,
                    TotalReusableRemainderMm = plan != null ? plan.TotalReusableRemainderMm : 0.0,
                    UtilizationRatio = plan != null ? plan.UtilizationRatio : 0.0
                });
            }

            return result;
        }

        private static string ReadString(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element.get_Parameter(parameterId);
            return parameter != null ? parameter.AsString() ?? string.Empty : string.Empty;
        }
    }
}
