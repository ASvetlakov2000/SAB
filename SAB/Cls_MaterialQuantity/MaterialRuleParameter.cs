using System;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace SAB.MaterialQuantity
{
    internal static class MaterialRuleParameter
    {
        internal static readonly Guid Id = new Guid("eb5427cb-7280-40d5-ae6b-7ff70321922d");

        // Call only after the user confirms changes, inside a project transaction.
        internal static void EnsureBound(Application application, Document project)
        {
            SharedParameterElement collision = new FilteredElementCollector(project)
                .OfClass(typeof(SharedParameterElement))
                .Cast<SharedParameterElement>()
                .FirstOrDefault(item => item.Name == MaterialUnitResolver.RuleParameterName && item.GuidValue != Id);
            if (collision != null)
                throw new InvalidOperationException("В проекте уже есть параметр с именем " +
                    MaterialUnitResolver.RuleParameterName + " и другим GUID. Автоматическая замена запрещена.");

            Category materialCategory = Category.GetCategory(project, BuiltInCategory.OST_Materials);
            if (materialCategory == null || !materialCategory.AllowsBoundParameters)
                throw new InvalidOperationException("В этом проекте нельзя привязать параметр к материалам.");

            Material probe = new FilteredElementCollector(project).OfClass(typeof(Material))
                .Cast<Material>().FirstOrDefault();
            if (probe != null && probe.get_Parameter(Id) != null) return;

            ExternalDefinition definition = GetDefinition(application);
            CategorySet categories = application.Create.NewCategorySet();
            categories.Insert(materialCategory);
            InstanceBinding binding = application.Create.NewInstanceBinding(categories);
            bool inserted = project.ParameterBindings.Insert(definition, binding, GroupTypeId.Data);
            if (!inserted)
                throw new InvalidOperationException("Параметр единицы подсчёта уже привязан иначе. " +
                    "Проверьте его настройки вручную, существующие связи не изменены.");
            project.Regenerate();
        }

        private static ExternalDefinition GetDefinition(Application application)
        {
            string path = SabSharedParameterFile.GetPath();

            string previous = application.SharedParametersFilename;
            try
            {
                application.SharedParametersFilename = path;
                DefinitionFile file = application.OpenSharedParameterFile();
                if (file == null)
                    throw new InvalidOperationException("Не удалось открыть файл общих параметров: " + path);
                DefinitionGroup group = file.Groups.get_Item("SAB_Материалы");
                if (group == null)
                    throw new InvalidOperationException("В едином файле отсутствует группа SAB_Материалы.");
                ExternalDefinition definition = group.Definitions
                    .get_Item(MaterialUnitResolver.RuleParameterName) as ExternalDefinition;
                if (definition == null || definition.GUID != Id)
                    throw new InvalidOperationException("GUID параметра материала не совпадает со стандартом плагина.");
                return definition;
            }
            finally
            {
                application.SharedParametersFilename = previous;
            }
        }
    }
}
