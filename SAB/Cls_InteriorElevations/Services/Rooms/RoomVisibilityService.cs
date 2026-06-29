using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Services.Rooms
{
    public class RoomVisibilityService
    {
        public void EnsureRoomsAndObjectVisible(Document document, View activeView, IList<string> warnings)
        {
            if (document == null || activeView == null)
            {
                AddWarning(warnings, "Не удалось включить видимость помещений: документ или активный вид недоступен.");
                return;
            }

            Category roomsCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);
            if (roomsCategory == null)
            {
                AddWarning(warnings, "Не удалось найти категорию 'Помещения' в проекте.");
                return;
            }

            Category roomObjectSubCategory = FindRoomObjectSubCategory(roomsCategory);

            Transaction transaction = new Transaction(document, "SAB - включить видимость помещений");
            try
            {
                transaction.Start();

                // Блок включения категории на самом виде и, при наличии, на шаблоне вида.
                // Это важно, если видимость категории управляется шаблоном.
                TryShowCategory(activeView, roomsCategory.Id, warnings, "Помещения");
                TryShowCategory(activeView, roomObjectSubCategory != null ? roomObjectSubCategory.Id : null, warnings, "Помещения / Объект");

                View viewTemplate = GetViewTemplate(document, activeView);
                if (viewTemplate != null)
                {
                    TryShowCategory(viewTemplate, roomsCategory.Id, warnings, "Помещения в шаблоне вида");
                    TryShowCategory(viewTemplate, roomObjectSubCategory != null ? roomObjectSubCategory.Id : null, warnings, "Помещения / Объект в шаблоне вида");
                }

                transaction.Commit();
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                AddWarning(warnings, "Не удалось включить видимость помещений на активном виде: " + exception.Message);
            }
        }

        private bool TryShowCategory(View view, ElementId categoryId, IList<string> warnings, string categoryName)
        {
            if (view == null || categoryId == null || categoryId == ElementId.InvalidElementId)
            {
                return false;
            }

            try
            {
                if (!view.CanCategoryBeHidden(categoryId))
                {
                    return false;
                }

                if (!view.GetCategoryHidden(categoryId))
                {
                    return true;
                }

                view.SetCategoryHidden(categoryId, false);
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Категорию '" + categoryName + "' не удалось включить: " + exception.Message);
                return false;
            }
        }

        private View GetViewTemplate(Document document, View view)
        {
            if (document == null || view == null)
            {
                return null;
            }

            ElementId templateId = view.ViewTemplateId;
            if (templateId == null || templateId == ElementId.InvalidElementId)
            {
                return null;
            }

            View template = document.GetElement(templateId) as View;
            if (template == null || !template.IsTemplate)
            {
                return null;
            }

            return template;
        }

        private Category FindRoomObjectSubCategory(Category roomsCategory)
        {
            if (roomsCategory == null || roomsCategory.SubCategories == null)
            {
                return null;
            }

            foreach (Category subCategory in roomsCategory.SubCategories)
            {
                if (subCategory == null || string.IsNullOrWhiteSpace(subCategory.Name))
                {
                    continue;
                }

                string normalizedName = NormalizeCategoryName(subCategory.Name);
                if (string.Equals(normalizedName, "объект", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedName, "обьект", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedName, "объекты", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedName, "object", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalizedName, "objects", StringComparison.OrdinalIgnoreCase))
                {
                    return subCategory;
                }
            }

            return null;
        }

        private string NormalizeCategoryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim();
        }

        private void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            warnings.Add(warning);
        }
    }
}
