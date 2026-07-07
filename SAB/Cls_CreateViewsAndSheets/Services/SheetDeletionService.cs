using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetDeletionService
    {
        public SheetDeletionResult Execute(Document document, IList<SheetDeletionItem> items)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("Нет выбранных листов для удаления.");
            }

            SheetDeletionResult result = new SheetDeletionResult();
            HashSet<long> deletedViewIds = new HashSet<long>();

            using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Удаление листов и видов"))
            {
                transactionGroup.Start();

                try
                {
                    using (Transaction transaction = new Transaction(document, "Удалить листы и виды"))
                    {
                        transaction.Start();

                        for (int i = 0; i < items.Count; i++)
                        {
                            DeleteSingleItem(document, items[i], deletedViewIds, result);
                        }

                        transaction.Commit();
                    }

                    transactionGroup.Assimilate();
                }
                catch
                {
                    transactionGroup.RollBack();
                    throw;
                }
            }

            return result;
        }

        private void DeleteSingleItem(
            Document document,
            SheetDeletionItem item,
            HashSet<long> deletedViewIds,
            SheetDeletionResult result)
        {
            if (item == null)
            {
                return;
            }

            ViewSheet sheet = document.GetElement(item.SheetId) as ViewSheet;
            if (sheet == null)
            {
                result.Warnings.Add("Строка " + item.RowNumber + ": лист уже не найден в проекте.");
            }
            else
            {
                try
                {
                    document.Delete(sheet.Id);
                    result.DeletedSheetsCount++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException exception)
                {
                    result.Warnings.Add("Строка " + item.RowNumber + ": лист \"" + sheet.SheetNumber + " | " + sheet.Name + "\" не был удален. " + exception.Message);
                }
            }

            for (int i = 0; i < item.PlacedViewIds.Count; i++)
            {
                ElementId viewId = item.PlacedViewIds[i];
                if (viewId == null || viewId == ElementId.InvalidElementId)
                {
                    continue;
                }

                long viewKey = RevitElementIdUtils.GetElementIdValue(viewId);
                if (!deletedViewIds.Add(viewKey))
                {
                    continue;
                }

                View view = document.GetElement(viewId) as View;
                if (view == null)
                {
                    continue;
                }

                if (view.IsTemplate)
                {
                    result.Warnings.Add("Вид \"" + view.Name + "\" является шаблоном и не был удален.");
                    continue;
                }

                try
                {
                    document.Delete(view.Id);
                    result.DeletedViewsCount++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException exception)
                {
                    result.Warnings.Add("Вид \"" + view.Name + "\" не был удален. " + exception.Message);
                }
            }
        }
    }
}
