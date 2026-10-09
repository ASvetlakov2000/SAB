using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowImageService
    {
        private const int ImageDpi = 300;

        public void ExportAndAssignImages(
            Document document,
            IList<DoorWindowSelectionData> selections,
            IList<DoorWindowViewCreationResult> viewGroups,
            DoorWindowViewSettings settings,
            DoorWindowBatchCreationResult result)
        {
            if (document == null || selections == null || viewGroups == null || settings == null || result == null)
            {
                return;
            }

            string outputFolder = CreateOutputFolder(document);
            result.ImageOutputFolder = outputFolder;
            List<ExportedElementImage> exported = new List<ExportedElementImage>();
            int count = Math.Min(selections.Count, viewGroups.Count);
            for (int index = 0; index < count; index++)
            {
                DoorWindowSelectionData selection = selections[index];
                ViewSection imageView = viewGroups[index] != null
                    ? (settings.WorkflowMode == DoorWindowWorkflowMode.ImageOnly
                        ? viewGroups[index].GetSingleView(settings.SingleViewKind)
                        : viewGroups[index].FrontView)
                    : null;
                if (selection == null || imageView == null)
                {
                    continue;
                }

                try
                {
                    string filePath = ExportFrontView(document, selection, imageView, settings, outputFolder);
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        exported.Add(new ExportedElementImage(selection, filePath));
                        result.ExportedImagePaths.Add(filePath);
                    }
                }
                catch (Exception exception)
                {
                    result.Warnings.Add(GetElementLabel(selection) + ": PNG не создан — " + exception.Message);
                }
            }

            if (exported.Count == 0)
            {
                return;
            }

            using (Transaction transaction = new Transaction(document, "SAB Запись изображений экспликаций"))
            {
                transaction.Start();
                HashSet<int> assignedTypeIds = new HashSet<int>();
                for (int index = 0; index < exported.Count; index++)
                {
                    ExportedElementImage item = exported[index];
                    if (item.Selection.IsLinked)
                    {
                        result.Warnings.Add(GetElementLabel(item.Selection) +
                            ": PNG сохранён, но параметр элемента связи изменить нельзя.");
                        continue;
                    }

                    Element target = item.Selection.IsCurtainWall
                        ? item.Selection.Element
                        : item.Selection.ElementType;
                    if (target == null)
                    {
                        result.Warnings.Add(GetElementLabel(item.Selection) + ": не найден элемент для записи изображения.");
                        continue;
                    }

                    if (!item.Selection.IsCurtainWall && !assignedTypeIds.Add(target.Id.IntegerValue))
                    {
                        result.Warnings.Add(GetElementLabel(item.Selection) +
                            ": PNG сохранён отдельно, параметр типа уже заполнен по первому экземпляру этого типа.");
                        continue;
                    }

                    using (SubTransaction subTransaction = new SubTransaction(document))
                    {
                        subTransaction.Start();
                        try
                        {
                            Parameter parameter = FindTargetParameter(target, item.Selection, settings);
                            if (parameter == null)
                            {
                                string parameterName = item.Selection.IsCurtainWall
                                    ? settings.CurtainWallInstanceImageParameterName
                                    : settings.DoorWindowTypeImageParameterName;
                                result.Warnings.Add(GetElementLabel(item.Selection) +
                                    ": не найден доступный параметр изображения «" + parameterName + "».");
                                subTransaction.RollBack();
                                continue;
                            }

                            ElementId imageTypeId = CreateImageType(document, item.FilePath);
                            if (imageTypeId == null || imageTypeId == ElementId.InvalidElementId)
                            {
                                result.Warnings.Add(GetElementLabel(item.Selection) + ": Revit не импортировал PNG как тип изображения.");
                                subTransaction.RollBack();
                                continue;
                            }

                            parameter.Set(imageTypeId);
                            subTransaction.Commit();
                            result.ImagesAssigned++;
                        }
                        catch (Exception exception)
                        {
                            if (subTransaction.GetStatus() == TransactionStatus.Started)
                            {
                                subTransaction.RollBack();
                            }

                            result.Warnings.Add(GetElementLabel(item.Selection) +
                                ": изображение не записано в параметр — " + exception.Message);
                        }
                    }
                }

                transaction.Commit();
            }
        }

        private string ExportFrontView(
            Document document,
            DoorWindowSelectionData selection,
            ViewSection view,
            DoorWindowViewSettings settings,
            string outputFolder)
        {
            string baseName = SanitizeFileName(view.Name + "_" + selection.ElementIdText);
            string expectedPath = BuildUniquePath(outputFolder, baseName, ".png");
            string prefix = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(expectedPath));
            HashSet<string> filesBefore = new HashSet<string>(
                Directory.GetFiles(outputFolder, "*.png", SearchOption.TopDirectoryOnly),
                StringComparer.OrdinalIgnoreCase);
            DateTime exportStartUtc = DateTime.UtcNow;

            ImageExportOptions options = new ImageExportOptions();
            options.ExportRange = ExportRange.SetOfViews;
            options.SetViewsAndSheets(new List<ElementId> { view.Id });
            options.ZoomType = ZoomFitType.FitToPage;
            options.FitDirection = FitDirectionType.Vertical;
            options.PixelSize = GetImageHeightPixels(settings.ImageHeightPaperMm);
            options.HLRandWFViewsFileType = ImageFileType.PNG;
            options.ShadowViewsFileType = ImageFileType.PNG;
            options.ImageResolution = ImageResolution.DPI_300;
            options.FilePath = prefix;
            document.ExportImage(options);

            string actualPath = ResolveExportedPath(outputFolder, expectedPath, exportStartUtc, filesBefore);
            if (string.IsNullOrWhiteSpace(actualPath))
            {
                throw new InvalidOperationException("Revit завершил экспорт без выходного PNG-файла.");
            }

            if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(actualPath, expectedPath);
            }

            return expectedPath;
        }

        internal static int GetImageHeightPixels(double heightPaperMm)
        {
            if (double.IsNaN(heightPaperMm) || double.IsInfinity(heightPaperMm) ||
                heightPaperMm < 10.0 || heightPaperMm > 500.0)
            {
                throw new ArgumentOutOfRangeException(nameof(heightPaperMm),
                    "Высота изображения на листе должна быть от 10 до 500 мм.");
            }

            return (int)Math.Round(heightPaperMm * ImageDpi / 25.4,
                MidpointRounding.AwayFromZero);
        }

        private Parameter FindTargetParameter(
            Element target,
            DoorWindowSelectionData selection,
            DoorWindowViewSettings settings)
        {
            string configuredName = selection.IsCurtainWall
                ? settings.CurtainWallInstanceImageParameterName
                : settings.DoorWindowTypeImageParameterName;
            Parameter parameter = !string.IsNullOrWhiteSpace(configuredName)
                ? target.LookupParameter(configuredName)
                : null;

            return parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.ElementId
                ? parameter
                : null;
        }

        private ElementId CreateImageType(Document document, string filePath)
        {
            ImageTypeOptions options = BuildImageTypeOptions(filePath);
            if (options == null)
            {
                return ElementId.InvalidElementId;
            }

            try
            {
                options.Resolution = ImageDpi;
                ImageType imageType = ImageType.Create(document, options);
                if (imageType == null)
                {
                    return ElementId.InvalidElementId;
                }

                try
                {
                    imageType.Name = Path.GetFileNameWithoutExtension(filePath);
                }
                catch
                {
                    // Revit сам назначит допустимое уникальное имя.
                }

                return imageType.Id;
            }
            finally
            {
                options.Dispose();
            }
        }

        private ImageTypeOptions BuildImageTypeOptions(string sourceImagePath)
        {
            Type optionsType = typeof(ImageTypeOptions);
            ConstructorInfo constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool), typeof(ImageTypeSource) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourceImagePath, false, ImageTypeSource.Import }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourceImagePath, false }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string) });
            return constructor != null
                ? constructor.Invoke(new object[] { sourceImagePath }) as ImageTypeOptions
                : null;
        }

        private string ResolveExportedPath(
            string folderPath,
            string expectedPath,
            DateTime exportStartUtc,
            ISet<string> filesBefore)
        {
            if (File.Exists(expectedPath))
            {
                return expectedPath;
            }

            return Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly)
                .Where(path => !filesBefore.Contains(path))
                .Where(path => File.GetLastWriteTimeUtc(path) >= exportStartUtc.AddSeconds(-1))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private string CreateOutputFolder(Document document)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetTempPath();
            }
            string documentName = SanitizeFileName(document != null ? document.Title : "Проект Revit");
            string sessionName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folder = Path.Combine(root, "SAB", "Экспликации", documentName, sessionName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private string BuildUniquePath(string folder, string baseName, string extension)
        {
            string candidate = Path.Combine(folder, baseName + extension);
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, baseName + "_" + suffix + extension);
                suffix++;
            }

            return candidate;
        }

        private string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Экспликация" : value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidCharacter, '_');
            }

            return result.Length <= 120 ? result : result.Substring(0, 120);
        }

        private string GetElementLabel(DoorWindowSelectionData selection)
        {
            if (selection == null)
            {
                return "Элемент";
            }

            return (selection.CategoryName ?? "Элемент") + " " + selection.ElementIdText;
        }

        private class ExportedElementImage
        {
            public ExportedElementImage(DoorWindowSelectionData selection, string filePath)
            {
                Selection = selection;
                FilePath = filePath;
            }

            public DoorWindowSelectionData Selection { get; private set; }

            public string FilePath { get; private set; }
        }
    }
}
