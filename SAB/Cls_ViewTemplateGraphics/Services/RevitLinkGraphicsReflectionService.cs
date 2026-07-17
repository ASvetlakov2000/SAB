using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.Services
{
    public class RevitLinkGraphicsReflectionService
    {
        private readonly Type _settingsType;
        private readonly MethodInfo _getLinkOverridesMethod;
        private readonly MethodInfo _setLinkOverridesMethod;
        private readonly PropertyInfo _linkVisibilityTypeProperty;
        private readonly PropertyInfo _linkedViewIdProperty;

        public RevitLinkGraphicsReflectionService()
        {
            Assembly revitApiAssembly = typeof(View).Assembly;
            _settingsType = revitApiAssembly.GetType("Autodesk.Revit.DB.RevitLinkGraphicsSettings", false);
            if (_settingsType == null)
            {
                return;
            }

            _getLinkOverridesMethod = typeof(View).GetMethod(
                "GetLinkOverrides",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(ElementId) },
                null);

            _setLinkOverridesMethod = typeof(View).GetMethod(
                "SetLinkOverrides",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(ElementId), _settingsType },
                null);

            _linkVisibilityTypeProperty = _settingsType.GetProperty(
                "LinkVisibilityType",
                BindingFlags.Public | BindingFlags.Instance);

            _linkedViewIdProperty = _settingsType.GetProperty(
                "LinkedViewId",
                BindingFlags.Public | BindingFlags.Instance);
        }

        public bool IsSupported
        {
            get
            {
                return _settingsType != null &&
                       _getLinkOverridesMethod != null &&
                       _setLinkOverridesMethod != null &&
                       _linkVisibilityTypeProperty != null &&
                       _linkVisibilityTypeProperty.CanRead &&
                       _linkVisibilityTypeProperty.CanWrite &&
                       _linkedViewIdProperty != null &&
                       _linkedViewIdProperty.CanRead &&
                       _linkedViewIdProperty.CanWrite;
            }
        }

        public List<NamedStringOption> GetVisibilityTypeOptions()
        {
            List<NamedStringOption> result = new List<NamedStringOption>();
            if (!IsSupported || !_linkVisibilityTypeProperty.PropertyType.IsEnum)
            {
                return result;
            }

            string[] names = Enum.GetNames(_linkVisibilityTypeProperty.PropertyType);
            for (int i = 0; i < names.Length; i++)
            {
                result.Add(new NamedStringOption(names[i], GetVisibilityTypeDisplayName(names[i])));
            }

            return result;
        }

        public void ReadOverrides(View view, ElementId linkElementId, RevitLinkInfo row)
        {
            if (view == null || linkElementId == null || row == null)
            {
                return;
            }

            row.IsApiSupported = IsSupported;
            if (!IsSupported)
            {
                return;
            }

            object settings = null;
            try
            {
                settings = _getLinkOverridesMethod.Invoke(view, new object[] { linkElementId });
                if (settings == null)
                {
                    return;
                }

                object visibilityType = _linkVisibilityTypeProperty.GetValue(settings, null);
                row.VisibilityTypeName = visibilityType != null ? visibilityType.ToString() : string.Empty;

                ElementId linkedViewId = _linkedViewIdProperty.GetValue(settings, null) as ElementId;
                row.LinkedViewIdValue = linkedViewId != null
                    ? linkedViewId.IntegerValue
                    : ElementId.InvalidElementId.IntegerValue;
            }
            catch (TargetInvocationException exception)
            {
                Exception actualException = exception.InnerException ?? exception;
                throw new InvalidOperationException(
                    "Не удалось прочитать настройки связанной модели для элемента Id " + linkElementId.IntegerValue + ".\n" + actualException.Message,
                    actualException);
            }
            finally
            {
                DisposeSettings(settings);
            }
        }

        public int ApplyOverrides(View view, RevitLinkInfo row)
        {
            if (view == null || row == null || !row.IsModified)
            {
                return 0;
            }

            if (!IsSupported)
            {
                throw new InvalidOperationException(
                    "Текущая версия Revit API не поддерживает программное изменение графики связанных моделей.");
            }

            if (!string.IsNullOrWhiteSpace(row.VisibilityTypeName) &&
                row.VisibilityTypeName.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "Режим отображения связи «Пользовательские» доступен для чтения, но не поддерживается методом SetLinkOverrides в Revit API 2024.");
            }

            ElementId linkElementId = new ElementId(row.LinkElementIdValue);
            object settings = null;
            try
            {
                settings = _getLinkOverridesMethod.Invoke(view, new object[] { linkElementId });
                if (settings == null)
                {
                    settings = Activator.CreateInstance(_settingsType);
                }

                int changedPropertyCount = 0;
                if (row.IsVisibilityTypeModified)
                {
                    object visibilityTypeValue = Enum.Parse(
                        _linkVisibilityTypeProperty.PropertyType,
                        row.VisibilityTypeName,
                        false);
                    _linkVisibilityTypeProperty.SetValue(settings, visibilityTypeValue, null);
                    changedPropertyCount++;
                }

                if (row.IsLinkedViewModified)
                {
                    _linkedViewIdProperty.SetValue(settings, new ElementId(row.LinkedViewIdValue), null);
                    changedPropertyCount++;
                }

                _setLinkOverridesMethod.Invoke(view, new object[] { linkElementId, settings });
                return changedPropertyCount;
            }
            catch (TargetInvocationException exception)
            {
                Exception actualException = exception.InnerException ?? exception;
                throw new InvalidOperationException(
                    "Не удалось применить настройки связи «" + row.Name + "».\n" + actualException.Message,
                    actualException);
            }
            finally
            {
                DisposeSettings(settings);
            }
        }

        private static void DisposeSettings(object settings)
        {
            IDisposable disposable = settings as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private static string GetVisibilityTypeDisplayName(string enumName)
        {
            if (string.Equals(enumName, "ByHostView", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enumName, "HostView", StringComparison.OrdinalIgnoreCase))
            {
                return "По основному виду";
            }

            if (string.Equals(enumName, "ByLinkedView", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(enumName, "LinkedView", StringComparison.OrdinalIgnoreCase))
            {
                return "По связанному виду";
            }

            if (enumName.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Пользовательские (только чтение API)";
            }

            if (enumName.IndexOf("None", StringComparison.OrdinalIgnoreCase) >= 0 ||
                enumName.IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Без переопределения";
            }

            return enumName;
        }
    }
}
