using System;
using Autodesk.Revit.DB;
using SAB.ParameterTools.Core;
using ParameterValue = SAB.ParameterTools.Core.ParameterValue;

namespace SAB.ParameterTools
{
    internal static partial class Catalog
    {
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
            // Parameter reads can remain stale until the outer transaction commits.
            // Workflow reacquires and verifies the parameter after that commit.
            if (!accepted) throw new InvalidOperationException("Revit отклонил запись: Parameter.Set вернул False. Ожидалось: «" + value + "». Подробности Revit, если они доступны, приведены в диагностике транзакции.");
        }
    }
}
