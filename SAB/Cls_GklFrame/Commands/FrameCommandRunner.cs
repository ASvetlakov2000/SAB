using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.GklFrame.Models;
using SAB.GklFrame.Services;
using SAB.GklFrame.ViewModels;
using SAB.GklFrame.Views;

namespace SAB.GklFrame.Commands
{
    internal static class FrameCommandRunner
    {
        public static Result Run(
            ExternalCommandData commandData,
            FrameCommandMode mode,
            ref string message)
        {
            try
            {
                UIApplication uiApplication = commandData != null ? commandData.Application : null;
                UIDocument uiDocument = uiApplication != null ? uiApplication.ActiveUIDocument : null;
                Document document = uiDocument != null ? uiDocument.Document : null;
                if (document == null)
                {
                    message = "Не удалось получить активный документ Revit.";
                    return Result.Failed;
                }

                IFrameMetadataService metadataService = new FrameMetadataService();
                FrameDocumentDataService dataService = new FrameDocumentDataService(metadataService);
                IList<ElementId> sourceWallIds = mode == FrameCommandMode.Generate
                    ? GetSourceWallIds(uiDocument, metadataService)
                    : new List<ElementId>();
                if (mode == FrameCommandMode.Generate && sourceWallIds.Count == 0)
                {
                    return Result.Cancelled;
                }

                IList<Mullion> existingCalculationMullions = dataService.GetCalculationMullions(document);
                if (mode == FrameCommandMode.Calculate && existingCalculationMullions.Count == 0)
                {
                    TaskDialog.Show(FrameModuleConstants.ModuleTitle, "В документе нет расчётных Mullion, созданных системой SAB.");
                    return Result.Cancelled;
                }

                IList<ElementTypeOption> curtainWallTypes = dataService.GetCurtainWallTypes(document);
                if (mode == FrameCommandMode.Generate && curtainWallTypes.Count == 0)
                {
                    TaskDialog.Show(
                        FrameModuleConstants.ModuleTitle,
                        "Для генерации нужен хотя бы один Curtain Wall Type.");
                    return Result.Cancelled;
                }

                FrameSettingsService settingsService = new FrameSettingsService();
                FrameModuleSettings savedSettings = settingsService.Load();
                FrameModuleViewModel viewModel = new FrameModuleViewModel(
                    mode,
                    sourceWallIds.Count,
                    existingCalculationMullions.Count,
                    curtainWallTypes,
                    savedSettings);
                FrameModuleWindow window = new FrameModuleWindow(viewModel);
                new WindowInteropHelper(window).Owner = uiApplication.MainWindowHandle;
                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                FrameGenerationOptions generationOptions;
                FrameCalculationOptions calculationOptions;
                FrameModuleSettings acceptedSettings;
                if (!viewModel.TryBuildOptions(out generationOptions, out calculationOptions, out acceptedSettings))
                {
                    TaskDialog.Show(FrameModuleConstants.ModuleTitle, viewModel.ValidationMessage);
                    return Result.Cancelled;
                }

                try
                {
                    settingsService.Save(acceptedSettings);
                }
                catch
                {
                    // Настройки удобны, но их запись не должна отменять BIM-операцию.
                }

                FrameGenerationResult generationResult = null;
                IMullionGeometryService geometryService = new MullionGeometryService();
                if (mode == FrameCommandMode.Generate)
                {
                    IWallOpeningCollector openingCollector = new WallContourOpeningCollector();
                    ISourceWallAnalyzer wallAnalyzer = new SourceWallAnalyzer(openingCollector);
                    ICurtainWallFactory wallFactory = new CurtainWallFactory(metadataService);
                    IFrameParameterWriter parameterWriter = new FrameParameterWriter();
                    ICurtainGridGenerator gridGenerator = new CurtainGridGenerator(
                        metadataService,
                        geometryService,
                        parameterWriter);
                    IFrameGenerationService generationService = new FrameGenerationService(
                        wallAnalyzer,
                        wallFactory,
                        gridGenerator,
                        metadataService);
                    generationResult = generationService.Generate(document, sourceWallIds, generationOptions);
                }

                IList<Mullion> calculationMullions = dataService.GetCalculationMullions(document);
                IFrameQuantityService quantityService = new FrameQuantityService(
                    metadataService,
                    geometryService,
                    new ProfileMetadataProvider(),
                    new CuttingOptimizationService());
                FrameCalculationResult calculationResult = quantityService.Calculate(
                    document,
                    calculationMullions,
                    calculationOptions);

                IList<string> exportedFiles = new List<string>();
                if (acceptedSettings.ExportCsv)
                {
                    try
                    {
                        exportedFiles = new CsvFrameReportExporter().Export(
                            calculationResult,
                            acceptedSettings.ExportDirectory);
                    }
                    catch (Exception exception)
                    {
                        calculationResult.Errors.Add("Экспорт CSV: " + exception.Message);
                    }
                }

                ShowReport(generationResult, calculationResult, exportedFiles);
                bool hasUsefulResult = calculationResult.Members.Count > 0 ||
                                       (generationResult != null && generationResult.CreatedFrameCount > 0);
                return hasUsefulResult ? Result.Succeeded : Result.Failed;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(FrameModuleConstants.ModuleTitle, exception.ToString());
                return Result.Failed;
            }
        }

        private static IList<ElementId> GetSourceWallIds(
            UIDocument uiDocument,
            IFrameMetadataService metadataService)
        {
            SourceWallSelectionFilter filter = new SourceWallSelectionFilter(metadataService);
            List<ElementId> selected = uiDocument.Selection.GetElementIds()
                .Where(id => filter.AllowElement(uiDocument.Document.GetElement(id)))
                .ToList();
            if (selected.Count > 0)
            {
                return selected;
            }

            IList<Reference> references = uiDocument.Selection.PickObjects(
                ObjectType.Element,
                filter,
                "Выберите прямолинейные Basic Wall для расчётного каркаса SAB");
            return references.Select(reference => reference.ElementId).Distinct().ToList();
        }

        private static void ShowReport(
            FrameGenerationResult generation,
            FrameCalculationResult calculation,
            IList<string> exportedFiles)
        {
            StringBuilder text = new StringBuilder();
            if (generation != null)
            {
                text.AppendLine("Выбрано стен: " + generation.RequestedWallCount);
                text.AppendLine("Создано каркасов: " + generation.CreatedFrameCount);
                text.AppendLine("Пропущено: " + generation.SkippedWallCount);
                text.AppendLine("Найдено дверей: " + generation.DoorCount);
                text.AppendLine("Всего проёмов: " + generation.OpeningCount);
            }

            text.AppendLine("Профилей в расчёте: " + calculation.Members.Count);
            text.AppendLine("Групп профилей: " + calculation.ProfileSummaries.Count);
            text.AppendLine("Закупочных хлыстов: " + calculation.CuttingPlans.Sum(item => item.Bars.Count));

            int studs = calculation.Members.Count(item => item.Purpose == FrameMemberPurpose.Stud);
            int jambs = calculation.Members.Count(item => item.Purpose == FrameMemberPurpose.DoorJamb);
            int headers = calculation.Members.Count(item => item.Purpose == FrameMemberPurpose.DoorHeader);
            int openingJambs = calculation.Members.Count(item => item.Purpose == FrameMemberPurpose.OpeningJamb);
            int openingHorizontals = calculation.Members.Count(item =>
                item.Purpose == FrameMemberPurpose.OpeningHeader || item.Purpose == FrameMemberPurpose.OpeningSill);
            text.AppendLine("Вертикальных стоек: " + studs);
            text.AppendLine("Дверных стоек: " + jambs);
            text.AppendLine("Дверных перемычек: " + headers);
            text.AppendLine("Стоек прочих проёмов: " + openingJambs);
            text.AppendLine("Горизонтальных профилей прочих проёмов: " + openingHorizontals);

            foreach (ProfileSummary summary in calculation.ProfileSummaries.Take(8))
            {
                text.AppendLine(
                    summary.ProfileType + " / " + summary.ProfileSize + ": " +
                    summary.MemberCount + " деталей, " + summary.StockBarCount + " хлыстов, " +
                    Math.Round(summary.UtilizationRatio * 100.0, 1) + "% использования");
            }

            List<string> warnings = new List<string>();
            if (generation != null)
            {
                warnings.AddRange(generation.Warnings);
                warnings.AddRange(generation.Errors);
            }

            warnings.AddRange(calculation.Warnings);
            warnings.AddRange(calculation.Errors);
            if (warnings.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Предупреждения и ошибки (" + warnings.Count + "):");
                foreach (string warning in warnings.Take(10))
                {
                    text.AppendLine("• " + warning);
                }

                if (warnings.Count > 10)
                {
                    text.AppendLine("• Ещё: " + (warnings.Count - 10));
                }
            }

            if (exportedFiles != null && exportedFiles.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("CSV:");
                foreach (string file in exportedFiles)
                {
                    text.AppendLine(file);
                }
            }

            TaskDialog.Show(FrameModuleConstants.ModuleTitle, text.ToString().TrimEnd());
        }
    }
}
