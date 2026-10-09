using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.ParameterTools.Core;
using Profile = SAB.ParameterTools.Core.Profile;
using ParameterValue = SAB.ParameterTools.Core.ParameterValue;

namespace SAB.ParameterTools
{
    public sealed class CategoryChoice : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        private bool _selected;
        public bool Selected { get { return _selected; } set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Selected")); } }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
    internal static class Catalog
    {
        internal static Profile DefaultProfile(Document doc)
        {
            var p = new Profile();
            p.CategoryIds.AddRange(new[] { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Furniture, BuiltInCategory.OST_GenericModel }.Select(x => (int)x));
            var catalog = Targets(doc);
            string[] names = { "KS_Номер корпуса", "KS_Номер этажа", "KS_Подземная часть (0 или 1)", "KS_Номер помещения", "KS_Наименование" };
            ParameterGroup[] groups = { ParameterGroup.Zone, ParameterGroup.Level, ParameterGroup.Location, ParameterGroup.Room, ParameterGroup.Room };
            for (int i = 0; i < names.Length; i++)
            {
                var matches = catalog.Where(x => x.Name == names[i]).ToList();
                p.Rules.Add(new Rule { Group = groups[i], Target = matches.Count == 1 ? matches[0] : null,
                    RoomField = i == 4 ? RoomField.Name : RoomField.Number });
            }
            p.Levels = Levels(doc);
            var roomCorpus = RoomParameters(doc).Where(x => x.Name == "KS_Номер корпуса").ToList();
            if (roomCorpus.Count == 1) p.RoomCorpusParameter = roomCorpus[0];
            return p;
        }
        internal static List<LevelMapping> Levels(Document doc)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.Elevation).Select(l => new LevelMapping { LevelUniqueId = l.UniqueId, LevelName = l.Name }).ToList();
        }
        internal static List<ParameterRef> Targets(Document doc)
        {
            var parameters = new List<ParameterRef>();
            var it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key as InternalDefinition;
                if (def == null || !(it.Current is InstanceBinding)) continue;
                var shared = doc.GetElement(def.Id) as SharedParameterElement;
                parameters.Add(Reference(def, Ids.Value(def.Id), shared == null ? null : shared.GuidValue.ToString()));
            }
            return parameters.OrderBy(x => x.Name).ThenBy(x => x.Id).ToList();
        }
        internal static List<ParameterRef> RoomParameters(Document doc)
        {
            var ids = new HashSet<long>();
            var it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key as InternalDefinition;
                var binding = it.Current as InstanceBinding;
                if (def != null && binding != null && binding.Categories.Cast<Category>().Any(c => Ids.Value(c.Id) == (int)BuiltInCategory.OST_Rooms))
                    ids.Add(Ids.Value(def.Id));
            }
            return Targets(doc).Where(p => ids.Contains(p.Id)).ToList();
        }
        internal static List<ParameterRef> SourceParameters(Document doc)
        {
            var values = Targets(doc).ToDictionary(p => p.Id);
            var seen = new HashSet<string>();
            foreach (var element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                if (element.Category == null || element.Category.CategoryType != CategoryType.Model || !seen.Add(Ids.Category(element.Category.Id) + ":" + Ids.Value(element.GetTypeId()))) continue;
                foreach (Parameter parameter in element.Parameters)
                {
                    // Some internal Revit parameters have no definition and cannot be offered in a named picker.
                    if (parameter == null) continue;
                    var definition = parameter.Definition;
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Name) || parameter.Id == null) continue;
                    long id = Ids.Value(parameter.Id); if (values.ContainsKey(id)) continue;
                    values[id] = Reference(definition, id, parameter.IsShared ? parameter.GUID.ToString() : null);
                }
            }
            return values.Values.OrderBy(p => p.Name).ThenBy(p => p.Id).ToList();
        }
        internal static List<CategoryChoice> Categories(Document doc, Profile p)
        {
            return doc.Settings.Categories.Cast<Category>()
                .Where(c => c.CategoryType == CategoryType.Model && c.AllowsBoundParameters && Ids.Value(c.Id) < 0
                    && Ids.Value(c.Id) != (int)BuiltInCategory.OST_Rooms)
                .OrderBy(c => c.Name).Select(c => new CategoryChoice { Id = Ids.Category(c.Id), Name = c.Name,
                    Selected = p.CategoryIds.Contains(Ids.Category(c.Id)) }).ToList();
        }
        internal static Parameter Parameter(Element element, ParameterRef reference)
        {
            if (element == null || reference == null) return null;
            if (!string.IsNullOrWhiteSpace(reference.SharedGuid))
                return element.get_Parameter(new Guid(reference.SharedGuid));
            return element.Parameters.Cast<Parameter>().FirstOrDefault(p => Ids.Value(p.Id) == reference.Id);
        }
        internal static ParameterRef Reference(Definition definition, long id, string guid)
        {
            var internalDefinition = definition as InternalDefinition;
            return new ParameterRef { Id = id, Name = definition.Name, SharedGuid = guid,
                DataType = DataType(definition), VariesAcrossGroups = internalDefinition == null ? (bool?)null : internalDefinition.VariesAcrossGroups };
        }
        internal static string DataType(Definition definition)
        {
            var type = definition.GetDataType();
            if (type == null || string.IsNullOrEmpty(type.TypeId)) return "Тип данных не определён";
            try { return LabelUtils.GetLabelForSpec(type); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { return type.TypeId; }
        }
        internal static bool IsGrouped(Element element)
        { return element.GroupId != ElementId.InvalidElementId; }
        internal static bool CanVaryInGroup(Parameter parameter)
        { return (parameter?.Definition as InternalDefinition)?.VariesAcrossGroups == true; }
        internal static string Describe(ParameterRef reference, Parameter parameter = null)
        {
            if (reference == null) return "Параметр не выбран";
            return parameter == null ? reference.Display : Reference(parameter.Definition, Ids.Value(parameter.Id), parameter.IsShared ? parameter.GUID.ToString() : null).Display;
        }
        internal static ParameterValue Read(Parameter p)
        {
            if (p == null) return new ParameterValue();
            if (p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer)
                return new ParameterValue { Exists = true, HasValue = p.HasValue, Numeric = true,
                    Number = p.StorageType == StorageType.Double ? p.AsDouble() : p.AsInteger() };
            return new ParameterValue { Exists = true, HasValue = p.HasValue, Text = p.StorageType == StorageType.String ? p.AsString() : p.StorageType == StorageType.ElementId ? p.AsValueString() : null };
        }
        internal static void ValidateWrite(Parameter p, string expected)
        {
            if (p == null) throw new InvalidOperationException("Параметр с выбранным GUID/ID отсутствует у экземпляра. Проверьте привязку к категории и выбор среди одноимённых параметров в настройках.");
            if (p.IsReadOnly) throw new InvalidOperationException("Параметр доступен только для чтения. Проверьте формулу, ограничения семейства и доступность редактирования элемента в Revit.");
            if (p.StorageType == StorageType.String) return;
            double n;
            if ((p.StorageType != StorageType.Integer && p.StorageType != StorageType.Double) || !RuleEngine.TryNumber(expected, out n))
                throw new InvalidOperationException("Значение «" + expected + "» несовместимо с типом «" + DataType(p.Definition) + "» (" + p.StorageType + "). Для числового параметра введите число; ссылки на элементы не поддерживаются.");
            if (p.StorageType == StorageType.Integer && (Math.Abs(n - Math.Round(n)) > 1e-8 || n > int.MaxValue || n < int.MinValue))
                throw new InvalidOperationException("Для целочисленного параметра требуется целое число.");
            if (p.StorageType == StorageType.Double)
            {
                var type = p.Definition.GetDataType();
                if (type != SpecTypeId.Number && type != SpecTypeId.Currency)
                    throw new InvalidOperationException("Числовые правила поддерживают тип «Число» или «Денежная единица»; размерные параметры требуют преобразования единиц.");
            }
        }
        internal static void Write(Parameter p, string value)
        {
            ValidateWrite(p, value);
            if (RuleEngine.Compare(Read(p), Resolution.Known(value)).Status == CheckStatus.Valid) return;
            double n;
            bool accepted = false;
            if (p.StorageType == StorageType.String) accepted = p.Set(value);
            else if (RuleEngine.TryNumber(value, out n))
            {
                if (p.StorageType == StorageType.Integer) accepted = p.Set((int)Math.Round(n));
                else accepted = p.Set(n);
            }
            var check = RuleEngine.Compare(Read(p), Resolution.Known(value));
            if (check.Status != CheckStatus.Valid) throw new InvalidOperationException("Revit не сохранил значение: Parameter.Set вернул " + accepted + ". Сейчас: «" + check.Actual + "»; ожидалось: «" + value + "». Проверьте ограничения параметра и права редактирования; точная причина, если Revit её предоставил, указана в диагностике транзакции.");
        }
    }
}
