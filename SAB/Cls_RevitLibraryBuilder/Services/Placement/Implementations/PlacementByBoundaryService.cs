using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Helpers.Notifications.ToastNotifications;

namespace RevitLibraryBuilder.Services.Placement
{
    /// <summary>
    /// Сервис для размещения элементов по границе
    /// Только для категорий: Перекрытия (Floors) и Потолки (Ceilings)
    /// </summary>
    public class PlacementByBoundaryService : IPlacementService
    {
        private readonly Document _doc;

        // Размер границы: 2000 мм → футы
        private const double BoundarySizeMm = 2000;

        public PlacementByBoundaryService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Основной метод расстановки
        /// </summary>
        public void Place(List<ElementTypeCsvModel> elements, Level level)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Список элементов пуст.");

            if (level == null)
                throw new ArgumentNullException(nameof(level));

            using (Transaction t = new Transaction(_doc, "Placement by Boundary"))
            {
                t.Start();

                double size = BoundarySizeMm / 304.8; // мм → футы
                double offsetX = 0; // смещение по X для следующей границы
                int placedCount = 0;
                List<string> skippedElements = new List<string>();

                foreach (var e in elements.Where(x => x.Include))
                {
                    try
                    {
                        // Определяем категорию и ищем тип
                        ElementId typeId = GetTypeIdForBoundary(e);

                        if (typeId == null)
                        {
                            skippedElements.Add($"{e.TypeName} ({e.Category} — тип не найден или не поддерживается)");
                            continue;
                        }

                        // Создаём прямоугольную границу
                        List<Curve> curves = CreateBoundaryCurves(offsetX, size);

                        // Превращаем линии в CurveLoop
                        CurveLoop loop = CurveLoop.Create(curves);

                        var loops = new List<CurveLoop> { loop };

                        // Создание элемента в зависимости от категории
                        if (e.Category == "Перекрытия") // Floors
                        {
                            Floor.Create(_doc, loops, typeId, level.Id);
                        }
                        else if (e.Category == "Потолки") // Ceilings
                        {
                            Ceiling.Create(_doc, loops, typeId, level.Id);
                        }

                        placedCount++;
                        offsetX += size + 1; // сдвиг следующей границы
                    }
                    catch (Exception ex)
                    {
                        skippedElements.Add($"{e.TypeName} (ошибка: {ex.Message})");
                    }
                }

                t.Commit();

                // Логирование через ToastNotifier
                string logMessage = $"Элементов размещено: {placedCount}";
                if (skippedElements.Count > 0)
                    logMessage += $"\nПропущено: {skippedElements.Count}\n{string.Join("\n", skippedElements)}";

                ToastNotifier.ShowSuccess("Результаты размещения по границе", logMessage, 10);
            }
        }

        /// <summary>
        /// Возвращает ElementId типа элемента для Перекрытия или Потолка
        /// </summary>
        private ElementId GetTypeIdForBoundary(ElementTypeCsvModel e)
        {
            if (e.Category == "Перекрытия") // Floor
            {
                var floorType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(f => f.Name.Equals(e.TypeName, StringComparison.InvariantCultureIgnoreCase));

                return floorType?.Id; // просто возвращаем ElementId
            }

            if (e.Category == "Потолки") // Ceiling
            {
                var ceilingType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(CeilingType))
                    .Cast<CeilingType>()
                    .FirstOrDefault(c => c.Name.Equals(e.TypeName, StringComparison.InvariantCultureIgnoreCase));

                return ceilingType?.Id; // просто возвращаем ElementId
            }

            return null; // все остальные категории игнорируем
        }
        

        /// <summary>
        /// Создаёт прямоугольную границу размером size × size с началом в offsetX
        /// </summary>
        private List<Curve> CreateBoundaryCurves(double offsetX, double size)
        {
            return new List<Curve>
            {
                Line.CreateBound(new XYZ(offsetX, 0, 0), new XYZ(offsetX + size, 0, 0)),
                Line.CreateBound(new XYZ(offsetX + size, 0, 0), new XYZ(offsetX + size, size, 0)),
                Line.CreateBound(new XYZ(offsetX + size, size, 0), new XYZ(offsetX, size, 0)),
                Line.CreateBound(new XYZ(offsetX, size, 0), new XYZ(offsetX, 0, 0))
            };
        }
    }
}