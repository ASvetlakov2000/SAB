using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using InteriorElevations.Models;
using InteriorElevations.Services;
using InteriorElevations.UI;
using System;
using System.Collections.Generic;
using System.Linq;
//using InteriorElevations.UI;

namespace InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateInteriorElevationsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                //---------------------------------------------------------
                // 1. Пользователь выбирает линии
                //---------------------------------------------------------
                List<DetailLine> selectedLines = LineCollector.CollectSelectedLines(uidoc);
                if (selectedLines.Count == 0)
                {
                    TaskDialog.Show("Информация", "Линии не выбраны.");
                    return Result.Cancelled;
                }

                //---------------------------------------------------------
                // 2. Проверяем что активный вид — план
                //---------------------------------------------------------
                ViewPlan planView = doc.ActiveView as ViewPlan;
                if (planView == null)
                    throw new Exception("Активный вид должен быть планом.");

                //---------------------------------------------------------
                // 3. Получаем тип Elevation
                //---------------------------------------------------------
                ViewFamilyType elevationType =
                    new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(x => x.ViewFamily == ViewFamily.Elevation);

                //---------------------------------------------------------
                // 4. Пользователь выбирает помещение
                //---------------------------------------------------------
                Room pickedRoom = RoomDetector.PickRoom(uidoc);
                if (pickedRoom == null)
                {
                    TaskDialog.Show("Информация", "Помещение не выбрано.");
                    return Result.Cancelled;
                }

                //---------------------------------------------------------
                // 5. Настройки через WPF-панель
                //---------------------------------------------------------
                ElevationSettingsWindow settingsWindow = new ElevationSettingsWindow();
                if (settingsWindow.ShowDialog() != true)
                    return Result.Cancelled;
                ElevationSettings settings = settingsWindow.Settings;

                //---------------------------------------------------------
                // 6. Создаём развертки
                //---------------------------------------------------------
                using (Transaction t = new Transaction(doc, "Create Interior Elevations"))
                {
                    t.Start();

                    int lineIndex = 1;
                    int pointCounter = 1;

                    foreach (DetailLine line in selectedLines)
                    {
                        ElevationCreator.CreateElevationFromLine(
                            doc,
                            planView,
                            elevationType,
                            line,
                            pickedRoom,
                            settings,
                            lineIndex,
                            pointCounter);

                        lineIndex++;
                        pointCounter++;
                    }

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}