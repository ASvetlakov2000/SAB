using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IFrameGenerationService
    {
        FrameGenerationResult Generate(
            Document document,
            ICollection<ElementId> sourceWallIds,
            FrameGenerationOptions options);
    }

    public sealed class FrameGenerationService : IFrameGenerationService
    {
        private readonly ISourceWallAnalyzer _wallAnalyzer;
        private readonly ICurtainWallFactory _wallFactory;
        private readonly ICurtainGridGenerator _gridGenerator;
        private readonly IFrameMetadataService _metadataService;

        public FrameGenerationService(
            ISourceWallAnalyzer wallAnalyzer,
            ICurtainWallFactory wallFactory,
            ICurtainGridGenerator gridGenerator,
            IFrameMetadataService metadataService)
        {
            _wallAnalyzer = wallAnalyzer;
            _wallFactory = wallFactory;
            _gridGenerator = gridGenerator;
            _metadataService = metadataService;
        }

        public FrameGenerationResult Generate(
            Document document,
            ICollection<ElementId> sourceWallIds,
            FrameGenerationOptions options)
        {
            FrameGenerationResult result = new FrameGenerationResult
            {
                RequestedWallCount = sourceWallIds != null ? sourceWallIds.Count : 0
            };

            if (document == null || sourceWallIds == null || sourceWallIds.Count == 0)
            {
                result.Errors.Add("Не выбраны исходные стены.");
                return result;
            }

            Document catalog = null;
            bool closeCatalog = false;
            string catalogPath = Path.Combine(
                Path.GetDirectoryName(typeof(FrameGenerationService).Assembly.Location),
                "SAB_Кнауф профиля.rvt");
            try
            {
                if (File.Exists(catalogPath))
                {
                    try
                    {
                        catalog = document.Application.Documents.Cast<Document>().FirstOrDefault(item =>
                            !string.IsNullOrWhiteSpace(item.PathName) &&
                            string.Equals(Path.GetFullPath(item.PathName), Path.GetFullPath(catalogPath),
                                StringComparison.OrdinalIgnoreCase));
                        if (catalog == null)
                        {
                            catalog = document.Application.OpenDocumentFile(catalogPath);
                            closeCatalog = true;
                        }
                    }
                    catch (Exception exception)
                    {
                        result.Warnings.Add("Не удалось открыть каталог импостов: " + exception.Message);
                    }
                }

                options.MullionCatalogDocument = catalog;
                bool hasCommittedChanges = false;
                using (TransactionGroup group = new TransactionGroup(document, "SAB — создание расчётного каркаса"))
                {
                    group.Start();
                    foreach (ElementId sourceWallId in sourceWallIds)
                    {
                        Wall sourceWall = document.GetElement(sourceWallId) as Wall;
                        SourceWallData sourceWallData;
                        string analysisError;
                        bool analyzed;
                        try
                        {
                            analyzed = _wallAnalyzer.TryAnalyze(
                                sourceWall, out sourceWallData, out analysisError);
                        }
                        catch (Exception exception)
                        {
                            result.SkippedWallCount++;
                            result.Errors.Add("Стена " + sourceWallId.IntegerValue +
                                ", анализ контура: " + exception.Message);
                            continue;
                        }

                        if (!analyzed)
                        {
                            result.SkippedWallCount++;
                            result.Errors.Add("Стена " + sourceWallId.IntegerValue + ": " + analysisError);
                            continue;
                        }

                        if (!options.ProcessDoors)
                        {
                            sourceWallData.Openings = new List<WallOpeningData>();
                        }

                        if (_metadataService.FindCalculationWall(document, sourceWallData.SourceUniqueId) != null)
                        {
                            result.SkippedWallCount++;
                            result.Warnings.Add("Стена " + sourceWallId.IntegerValue + ": Already generated.");
                            continue;
                        }

                        using (Transaction transaction = new Transaction(
                            document,
                            "SAB — каркас стены " + sourceWallId.IntegerValue))
                        {
                            try
                            {
                                transaction.Start();
                                Wall calculationWall = _wallFactory.Create(document, sourceWallData, options);
                                GeneratedFrameData generated = _gridGenerator.Generate(
                                    document,
                                    calculationWall,
                                    sourceWallData,
                                    options);
                                transaction.Commit();

                                result.GeneratedFrames.Add(generated);
                                result.OpeningCount += options.ProcessDoors ? sourceWallData.Openings.Count : 0;
                                result.DoorCount += options.ProcessDoors
                                    ? sourceWallData.Openings.Count(item => item.OpeningType == OpeningType.Door)
                                    : 0;
                                hasCommittedChanges = true;
                            }
                            catch (Exception exception)
                            {
                                if (transaction.GetStatus() == TransactionStatus.Started)
                                {
                                    transaction.RollBack();
                                }

                                result.Errors.Add(
                                    "Стена " + sourceWallId.IntegerValue +
                                    ", операция создания каркаса: " + exception.Message);
                            }
                        }
                    }

                    if (hasCommittedChanges)
                    {
                        group.Assimilate();
                    }
                    else
                    {
                        group.RollBack();
                    }
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add("Каталог профилей или генерация: " + exception.Message);
            }
            finally
            {
                options.MullionCatalogDocument = null;
                if (closeCatalog && catalog != null)
                {
                    try
                    {
                        catalog.Close(false);
                    }
                    catch (Exception exception)
                    {
                        result.Warnings.Add("Не удалось закрыть каталог импостов: " + exception.Message);
                    }
                }
            }

            return result;
        }
    }
}
