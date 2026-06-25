using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class ViewDuplicationService
    {
        public View DuplicateView(
            Document document,
            View sourceView,
            SheetCreationItem item,
            IList<string> warnings)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (sourceView == null)
            {
                throw new InvalidOperationException("Вид-образец недоступен.");
            }

            if (item == null)
            {
                throw new InvalidOperationException("Данные строки создания отсутствуют.");
            }

            ViewStateSnapshot sourceState = CaptureViewState(sourceView, warnings);

            if (!sourceView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
            {
                throw new InvalidOperationException("Вид \"" + sourceView.Name + "\" нельзя дублировать с детализацией.");
            }

            ElementId duplicatedViewId = sourceView.Duplicate(ViewDuplicateOption.WithDetailing);
            if (duplicatedViewId == null || duplicatedViewId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("Revit API не вернул идентификатор созданного дубля вида.");
            }

            View duplicatedView = document.GetElement(duplicatedViewId) as View;
            if (duplicatedView == null)
            {
                throw new InvalidOperationException("Созданный дубль вида не найден в документе.");
            }

            duplicatedView.Name = item.ViewName;

            ApplyViewTemplate(document, duplicatedView, item.ViewTemplateId, warnings);
            TryApplyScaleRespectTemplate(document, duplicatedView, item.ViewScale, warnings);
            RestoreViewState(document, sourceState, duplicatedView, warnings);
            TryCopyDatumExtents(document, sourceView, duplicatedView, warnings);

            return duplicatedView;
        }

        private void ApplyViewTemplate(Document document, View view, ElementId templateId, IList<string> warnings)
        {
            if (document == null || view == null || templateId == null || templateId == ElementId.InvalidElementId)
            {
                return;
            }

            View templateView = document.GetElement(templateId) as View;
            if (templateView == null || !templateView.IsTemplate)
            {
                AddWarning(warnings, "Шаблон вида не найден. Вид \"" + view.Name + "\" создан без назначения шаблона.");
                return;
            }

            try
            {
                view.ViewTemplateId = templateId;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось назначить шаблон виду \"" + view.Name + "\": " + exception.Message);
            }
        }

        private void TryApplyScaleRespectTemplate(Document document, View view, int requestedScale, IList<string> warnings)
        {
            if (document == null || view == null || requestedScale <= 0)
            {
                return;
            }

            if (IsScaleControlledByTemplate(document, view))
            {
                AddWarning(
                    warnings,
                    "Для вида \"" + view.Name + "\" масштаб задается шаблоном. Значение из таблицы не применено.");
                return;
            }

            try
            {
                view.Scale = requestedScale;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось установить масштаб вида \"" + view.Name + "\": " + exception.Message);
            }
        }

        private bool IsScaleControlledByTemplate(Document document, View view)
        {
            if (document == null || view == null)
            {
                return false;
            }

            ElementId templateId = view.ViewTemplateId;
            if (templateId == null || templateId == ElementId.InvalidElementId)
            {
                return false;
            }

            View templateView = document.GetElement(templateId) as View;
            if (templateView == null || !templateView.IsTemplate)
            {
                return false;
            }

            try
            {
                ICollection<ElementId> nonControlled = templateView.GetNonControlledTemplateParameterIds();
                if (nonControlled == null)
                {
                    return true;
                }

                ElementId scaleParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE);
                ElementId scaleMetricParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC);
                ElementId scaleImperialParameterId = new ElementId((int)BuiltInParameter.VIEW_SCALE_PULLDOWN_IMPERIAL);

                bool scaleIsNonControlled =
                    nonControlled.Contains(scaleParameterId) ||
                    nonControlled.Contains(scaleMetricParameterId) ||
                    nonControlled.Contains(scaleImperialParameterId);

                return !scaleIsNonControlled;
            }
            catch
            {
                return true;
            }
        }

        private ViewStateSnapshot CaptureViewState(View view, IList<string> warnings)
        {
            ViewStateSnapshot snapshot = new ViewStateSnapshot();
            if (view == null)
            {
                return snapshot;
            }

            try
            {
                snapshot.CropBoxActive = view.CropBoxActive;
                snapshot.CropBoxVisible = view.CropBoxVisible;
                snapshot.CropBox = view.CropBox;
                snapshot.HasCropBox = snapshot.CropBox != null;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось считать параметры обрезки вида-образца: " + exception.Message);
            }

            try
            {
                ViewCropRegionShapeManager cropManager = view.GetCropRegionShapeManager();
                if (cropManager != null)
                {
                    IList<CurveLoop> cropShape = cropManager.GetCropShape();
                    if (cropShape != null && cropShape.Count > 0)
                    {
                        snapshot.CropShape = cropShape[0];
                    }
                }
            }
            catch
            {
                // Не каждый тип вида поддерживает произвольную форму crop region.
            }

            CaptureIntegerParameter(view, "VIEWER_ANNOTATION_CROP_ACTIVE", snapshot.AnnotationCropParameterValues);
            CaptureIntegerParameter(view, "VIEWER_ANNOTATION_CROP_VISIBLE", snapshot.AnnotationCropParameterValues);
            CaptureDoubleParameter(view, "VIEWER_ANNOTATION_CROP_TOP_OFFSET", snapshot.AnnotationCropParameterValues);
            CaptureDoubleParameter(view, "VIEWER_ANNOTATION_CROP_BOTTOM_OFFSET", snapshot.AnnotationCropParameterValues);
            CaptureDoubleParameter(view, "VIEWER_ANNOTATION_CROP_LEFT_OFFSET", snapshot.AnnotationCropParameterValues);
            CaptureDoubleParameter(view, "VIEWER_ANNOTATION_CROP_RIGHT_OFFSET", snapshot.AnnotationCropParameterValues);

            return snapshot;
        }

        private void RestoreViewState(Document document, ViewStateSnapshot snapshot, View targetView, IList<string> warnings)
        {
            if (document == null || snapshot == null || targetView == null)
            {
                return;
            }

            try
            {
                targetView.CropBoxActive = snapshot.CropBoxActive;
                targetView.CropBoxVisible = snapshot.CropBoxVisible;
                if (snapshot.HasCropBox && snapshot.CropBox != null)
                {
                    targetView.CropBox = snapshot.CropBox;
                }
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось восстановить прямоугольную обрезку вида \"" + targetView.Name + "\": " + exception.Message);
            }

            if (snapshot.CropShape != null)
            {
                try
                {
                    ViewCropRegionShapeManager cropManager = targetView.GetCropRegionShapeManager();
                    if (cropManager != null)
                    {
                        cropManager.SetCropShape(snapshot.CropShape);
                    }
                }
                catch (Exception exception)
                {
                    AddWarning(warnings, "Не удалось восстановить форму границы обрезки вида \"" + targetView.Name + "\": " + exception.Message);
                }
            }

            RestoreCapturedParameters(targetView, snapshot.AnnotationCropParameterValues, warnings);
        }

        private void TryCopyDatumExtents(Document document, View sourceView, View targetView, IList<string> warnings)
        {
            if (document == null || sourceView == null || targetView == null)
            {
                return;
            }

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(document, sourceView.Id).OfClass(typeof(DatumPlane));
                foreach (Element element in collector)
                {
                    DatumPlane datum = element as DatumPlane;
                    if (datum == null)
                    {
                        continue;
                    }

                    TryCopySingleDatumExtent(datum, sourceView, targetView);
                }
            }
            catch (Exception exception)
            {
                AddWarning(
                    warnings,
                    "Не удалось перенести видоспецифичные положения осей и уровней: " + exception.Message);
            }
        }

        private void TryCopySingleDatumExtent(DatumPlane datum, View sourceView, View targetView)
        {
            if (datum == null || sourceView == null || targetView == null)
            {
                return;
            }

            DatumEnds[] ends = new[] { DatumEnds.End0, DatumEnds.End1 };
            for (int i = 0; i < ends.Length; i++)
            {
                DatumEnds datumEnd = ends[i];
                try
                {
                    DatumExtentType extentType = datum.GetDatumExtentTypeInView(datumEnd, sourceView);
                    datum.SetDatumExtentType(datumEnd, targetView, extentType);

                    IList<Curve> sourceCurves = datum.GetCurvesInView(extentType, sourceView);
                    if (sourceCurves == null)
                    {
                        continue;
                    }

                    for (int curveIndex = 0; curveIndex < sourceCurves.Count; curveIndex++)
                    {
                        Curve curve = sourceCurves[curveIndex];
                        if (curve != null)
                        {
                            datum.SetCurveInView(extentType, targetView, curve);
                        }
                    }
                }
                catch
                {
                    // Для части datum-элементов Revit не позволяет менять видоспецифичную геометрию на целевом виде.
                }
            }
        }

        private void CaptureIntegerParameter(View view, string builtInParameterName, IDictionary<string, ParameterValueSnapshot> target)
        {
            Parameter parameter = GetParameterByBuiltInName(view, builtInParameterName);
            if (parameter == null || parameter.StorageType != StorageType.Integer)
            {
                return;
            }

            ParameterValueSnapshot value = new ParameterValueSnapshot();
            value.StorageType = StorageType.Integer;
            value.IntegerValue = parameter.AsInteger();
            target[builtInParameterName] = value;
        }

        private void CaptureDoubleParameter(View view, string builtInParameterName, IDictionary<string, ParameterValueSnapshot> target)
        {
            Parameter parameter = GetParameterByBuiltInName(view, builtInParameterName);
            if (parameter == null || parameter.StorageType != StorageType.Double)
            {
                return;
            }

            ParameterValueSnapshot value = new ParameterValueSnapshot();
            value.StorageType = StorageType.Double;
            value.DoubleValue = parameter.AsDouble();
            target[builtInParameterName] = value;
        }

        private void RestoreCapturedParameters(View view, IDictionary<string, ParameterValueSnapshot> values, IList<string> warnings)
        {
            if (view == null || values == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ParameterValueSnapshot> pair in values)
            {
                try
                {
                    Parameter parameter = GetParameterByBuiltInName(view, pair.Key);
                    if (parameter == null || parameter.IsReadOnly)
                    {
                        continue;
                    }

                    if (pair.Value.StorageType == StorageType.Integer && parameter.StorageType == StorageType.Integer)
                    {
                        parameter.Set(pair.Value.IntegerValue);
                    }
                    else if (pair.Value.StorageType == StorageType.Double && parameter.StorageType == StorageType.Double)
                    {
                        parameter.Set(pair.Value.DoubleValue);
                    }
                }
                catch (Exception exception)
                {
                    AddWarning(warnings, "Не удалось восстановить параметр обрезки \"" + pair.Key + "\": " + exception.Message);
                }
            }
        }

        private Parameter GetParameterByBuiltInName(View view, string builtInParameterName)
        {
            if (view == null || string.IsNullOrWhiteSpace(builtInParameterName))
            {
                return null;
            }

            try
            {
                object rawValue = Enum.Parse(typeof(BuiltInParameter), builtInParameterName);
                BuiltInParameter builtInParameter = (BuiltInParameter)rawValue;
                return view.get_Parameter(builtInParameter);
            }
            catch
            {
                return null;
            }
        }

        private void AddWarning(IList<string> warnings, string warningText)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warningText))
            {
                return;
            }

            warnings.Add(warningText);
        }

        private class ViewStateSnapshot
        {
            public ViewStateSnapshot()
            {
                AnnotationCropParameterValues = new Dictionary<string, ParameterValueSnapshot>();
            }

            public bool CropBoxActive { get; set; }

            public bool CropBoxVisible { get; set; }

            public bool HasCropBox { get; set; }

            public BoundingBoxXYZ CropBox { get; set; }

            public CurveLoop CropShape { get; set; }

            public Dictionary<string, ParameterValueSnapshot> AnnotationCropParameterValues { get; private set; }
        }

        private class ParameterValueSnapshot
        {
            public StorageType StorageType { get; set; }

            public int IntegerValue { get; set; }

            public double DoubleValue { get; set; }
        }
    }
}
