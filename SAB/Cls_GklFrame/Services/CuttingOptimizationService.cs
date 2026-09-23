using System;
using System.Collections.Generic;
using System.Linq;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface ICuttingOptimizationService
    {
        CuttingPlan Optimize(
            string profileFamily,
            string profileType,
            string profileSize,
            IReadOnlyCollection<FrameMemberData> members,
            double stockLengthMm,
            double cutLossMm,
            double minimumReusableOffcutMm);
    }

    public sealed class CuttingOptimizationService : ICuttingOptimizationService
    {
        public CuttingPlan Optimize(
            string profileFamily,
            string profileType,
            string profileSize,
            IReadOnlyCollection<FrameMemberData> members,
            double stockLengthMm,
            double cutLossMm,
            double minimumReusableOffcutMm)
        {
            if (stockLengthMm <= 0.0)
            {
                throw new ArgumentOutOfRangeException("stockLengthMm", "Закупочная длина должна быть больше нуля.");
            }

            CuttingPlan plan = new CuttingPlan
            {
                ProfileFamily = profileFamily,
                ProfileType = profileType,
                ProfileSize = profileSize,
                StockLengthMm = stockLengthMm
            };

            List<FrameMemberData> ordered = members
                .Where(item => item != null && item.ActualLengthMm > 0.0)
                .OrderByDescending(item => item.ActualLengthMm)
                .ThenBy(item => item.ElementId.IntegerValue)
                .ToList();

            int pieceNumber = 0;
            foreach (FrameMemberData member in ordered)
            {
                pieceNumber++;
                CutPiece piece = new CutPiece
                {
                    SourceElementId = member.ElementId,
                    PieceNumber = pieceNumber,
                    LengthMm = member.ActualLengthMm
                };
                plan.TotalRequiredLengthMm += piece.LengthMm;

                if (piece.LengthMm > stockLengthMm + 1e-6)
                {
                    plan.CannotBeCutFromSelectedStockLength.Add(piece);
                    continue;
                }

                StockBarCut targetBar = null;
                foreach (StockBarCut bar in plan.Bars)
                {
                    // Пропил учитывается только между двумя деталями. Одна деталь,
                    // равная длине хлыста, не объявляется невозможной из-за нулевого остатка.
                    double additionalLoss = bar.Pieces.Count > 0 ? Math.Max(0.0, cutLossMm) : 0.0;
                    if (bar.RemainingLengthMm + 1e-6 >= piece.LengthMm + additionalLoss)
                    {
                        targetBar = bar;
                        break;
                    }
                }

                if (targetBar == null)
                {
                    targetBar = new StockBarCut
                    {
                        BarNumber = plan.Bars.Count + 1,
                        StockLengthMm = stockLengthMm,
                        RemainingLengthMm = stockLengthMm
                    };
                    plan.Bars.Add(targetBar);
                }

                if (targetBar.Pieces.Count > 0)
                {
                    targetBar.CutLossMm += Math.Max(0.0, cutLossMm);
                }

                targetBar.Pieces.Add(piece);
                targetBar.UsedLengthMm += piece.LengthMm;
                targetBar.RemainingLengthMm = Math.Max(
                    0.0,
                    targetBar.StockLengthMm - targetBar.UsedLengthMm - targetBar.CutLossMm);
            }

            foreach (StockBarCut bar in plan.Bars)
            {
                if (bar.RemainingLengthMm >= minimumReusableOffcutMm)
                {
                    bar.ReusableRemainderMm = bar.RemainingLengthMm;
                }
                else
                {
                    bar.WasteMm = bar.RemainingLengthMm;
                }

                plan.TotalUsedLengthMm += bar.UsedLengthMm + bar.CutLossMm;
                plan.TotalReusableRemainderMm += bar.ReusableRemainderMm;
                plan.TotalWasteMm += bar.WasteMm + bar.CutLossMm;
            }

            plan.TotalPurchasedLengthMm = plan.Bars.Count * stockLengthMm;
            plan.UtilizationRatio = plan.TotalPurchasedLengthMm > 0.0
                ? plan.TotalRequiredLengthMm / plan.TotalPurchasedLengthMm
                : 0.0;
            return plan;
        }
    }
}
