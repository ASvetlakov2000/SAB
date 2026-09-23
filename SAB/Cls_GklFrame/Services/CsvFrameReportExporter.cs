using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IFrameReportExporter
    {
        IList<string> Export(FrameCalculationResult result, string directoryPath);
    }

    public sealed class CsvFrameReportExporter : IFrameReportExporter
    {
        public IList<string> Export(FrameCalculationResult result, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Не задан каталог экспорта CSV.", "directoryPath");
            }

            Directory.CreateDirectory(directoryPath);
            string profilesPath = Path.Combine(directoryPath, "Profiles.csv");
            string cuttingPlanPath = Path.Combine(directoryPath, "CuttingPlan.csv");
            UTF8Encoding encoding = new UTF8Encoding(true);

            File.WriteAllLines(profilesPath, BuildProfilesLines(result), encoding);
            File.WriteAllLines(cuttingPlanPath, BuildCuttingPlanLines(result), encoding);
            return new[] { profilesPath, cuttingPlanPath };
        }

        private static IEnumerable<string> BuildProfilesLines(FrameCalculationResult result)
        {
            yield return "ProfileType,Purpose,Size,ActualLength,Quantity,TotalLength,PurchaseLength,SourceWall,SourceWallType,Mark";

            var rows = result.Members.GroupBy(item => new
            {
                item.ProfileType,
                item.Purpose,
                item.ProfileSize,
                Length = Math.Round(item.ActualLengthMm, 1),
                item.PurchaseLengthMm,
                item.SourceWallUniqueId,
                item.SourceWallType,
                item.SourceWallMark
            });

            foreach (var row in rows.OrderBy(item => item.Key.ProfileType).ThenBy(item => item.Key.Length))
            {
                yield return Join(
                    row.Key.ProfileType,
                    row.Key.Purpose.ToString(),
                    row.Key.ProfileSize,
                    Number(row.Key.Length),
                    row.Count().ToString(CultureInfo.InvariantCulture),
                    Number(row.Sum(item => item.ActualLengthMm)),
                    Number(row.Key.PurchaseLengthMm),
                    row.Key.SourceWallUniqueId,
                    row.Key.SourceWallType,
                    row.Key.SourceWallMark);
            }
        }

        private static IEnumerable<string> BuildCuttingPlanLines(FrameCalculationResult result)
        {
            yield return "ProfileType,StockLength,BarNumber,PieceNumber,PieceLength,RemainingLength,ReusableRemainder,Waste";
            foreach (CuttingPlan plan in result.CuttingPlans.OrderBy(item => item.ProfileType))
            {
                foreach (StockBarCut bar in plan.Bars)
                {
                    foreach (CutPiece piece in bar.Pieces)
                    {
                        yield return Join(
                            plan.ProfileType,
                            Number(plan.StockLengthMm),
                            bar.BarNumber.ToString(CultureInfo.InvariantCulture),
                            piece.PieceNumber.ToString(CultureInfo.InvariantCulture),
                            Number(piece.LengthMm),
                            Number(bar.RemainingLengthMm),
                            Number(bar.ReusableRemainderMm),
                            Number(bar.WasteMm));
                    }
                }

                foreach (CutPiece piece in plan.CannotBeCutFromSelectedStockLength)
                {
                    yield return Join(
                        plan.ProfileType,
                        Number(plan.StockLengthMm),
                        "CannotBeCutFromSelectedStockLength",
                        piece.PieceNumber.ToString(CultureInfo.InvariantCulture),
                        Number(piece.LengthMm),
                        string.Empty,
                        string.Empty,
                        string.Empty);
                }
            }
        }

        private static string Number(double value)
        {
            return Math.Round(value, 1).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string Join(params string[] values)
        {
            return string.Join(",", values.Select(Escape));
        }

        private static string Escape(string value)
        {
            string safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? "\"" + safe.Replace("\"", "\"\"") + "\""
                : safe;
        }
    }
}
