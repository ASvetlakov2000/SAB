using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Sheets
{
    public sealed class SheetWorkspaceService
    {
        private sealed class FamilyLines
        {
            public long TypeId;
            public readonly List<SheetFrameLine> Lines = new List<SheetFrameLine>();
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        }

        private readonly List<FamilyLines> _families = new List<FamilyLines>();

        // Title blocks are annotations: their project Geometry property can be null.
        // EditFamily must run between project transactions; only copied coordinates are cached.
        public void PrepareFamilyGeometry(Document document, ViewSheet sheet)
        {
            if (document.IsModifiable) throw new InvalidOperationException("Чтение семейства рамки требует закрытой транзакции проекта.");
            _families.Clear();
            foreach (FamilyInstance instance in new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType())
            {
                var entry = new FamilyLines { TypeId = RevitElementIdUtils.GetElementIdValue(instance.GetTypeId()) };
                ReadFamilyLines(instance, Transform.Identity, entry.Lines, entry.Values, 0);
                _families.Add(entry);
                SheetLayoutDiagnostics.Write("TitleBlock " + instance.Symbol.Family.Name + "/" + instance.Symbol.Name +
                    ": family lines=" + entry.Lines.Count);
            }
        }

        public SheetFrames ReadFrames(Document document, ViewSheet sheet)
        {
            document.Regenerate();
            var lines = new List<SheetFrameLine>();
            foreach (FamilyInstance titleBlock in new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType())
            {
                var cached = _families.Find(f => Matches(f, titleBlock));
                if (cached != null)
                    foreach (var line in cached.Lines)
                        AddSegment(new XYZ(line.X1, line.Y1, 0), new XYZ(line.X2, line.Y2, 0), titleBlock.GetTransform(), lines);
                else CollectLines(titleBlock.get_Geometry(new Options { View = sheet }), Transform.Identity, lines);
            }
            foreach (CurveElement element in new FilteredElementCollector(document, sheet.Id).OfClass(typeof(CurveElement)))
                AddLine(element.GeometryCurve, Transform.Identity, lines);
            SheetLayoutDiagnostics.Write("Sheet " + sheet.SheetNumber + ": frame segments=" + lines.Count);
            return new SheetFrameDetectionService().Detect(lines, UnitConversionUtils.MillimetersToFeet(0.2));
        }

        public SheetRectangle ReadPlanWorkspace(Document document, ViewSheet sheet, double startX, double startY,
            out IList<SheetRectangle> reserved)
        {
            reserved = new List<SheetRectangle>();
            try
            {
                var frames = ReadFrames(document, sheet);
                reserved = frames.ReservedAreas;
                return frames.GetWorkspace(startX, startY);
            }
            catch (Exception exception) { SheetLayoutDiagnostics.Write("Plan reference uses title-block bounds: " + exception.Message); }
            SheetRectangle area = null;
            var footprints = new SheetFootprintService();
            foreach (Element titleBlock in new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType())
            {
                var bounds = footprints.FromBoundingBox(titleBlock.get_BoundingBox(sheet));
                if (bounds != null) area = area == null ? bounds : area.Union(bounds);
            }
            if (area != null)
                try { return new SheetFrames { Inner = area }.GetWorkspace(startX, startY); } catch (InvalidOperationException) { }
            return area;
        }

        private bool Matches(FamilyLines entry, FamilyInstance instance)
        {
            if (entry.TypeId != RevitElementIdUtils.GetElementIdValue(instance.GetTypeId())) return false;
            foreach (var value in entry.Values)
            {
                var parts = value.Key.Split(new[] { ':' }, 2);
                var parameter = (parts[0] == "I" ? (Element)instance : instance.Symbol).LookupParameter(parts[1]);
                if (ScalarValue(parameter) != value.Value) return false;
            }
            return true;
        }

        private void ReadFamilyLines(FamilyInstance instance, Transform transform,
            IList<SheetFrameLine> lines, IDictionary<string, string> values, int depth)
        {
            if (depth > 8) throw new InvalidOperationException("Слишком много уровней вложенности в семействе рамки.");
            Document familyDocument = instance.Document.EditFamily(instance.Symbol.Family);
            try
            {
                var manager = familyDocument.FamilyManager;
                using (var transaction = new Transaction(familyDocument, "Измерить рамку SAB"))
                {
                    transaction.Start();
                    foreach (FamilyType type in manager.Types)
                        if (type.Name == instance.Symbol.Name) { manager.CurrentType = type; break; }
                    if (manager.CurrentType == null) manager.NewType("SAB_Measurement");
                    foreach (FamilyParameter parameter in manager.Parameters)
                    {
                        if (parameter.IsReadOnly || parameter.IsDeterminedByFormula || parameter.IsReporting ||
                            (parameter.StorageType != StorageType.Integer && parameter.StorageType != StorageType.Double)) continue;
                        var source = (parameter.IsInstance ? (Element)instance : instance.Symbol).LookupParameter(parameter.Definition.Name);
                        if (source == null || !source.HasValue || source.StorageType != parameter.StorageType) continue;
                        if (parameter.StorageType == StorageType.Integer) manager.Set(parameter, source.AsInteger());
                        else manager.Set(parameter, source.AsDouble());
                        if (values != null) values[(parameter.IsInstance ? "I:" : "T:") + parameter.Definition.Name] = ScalarValue(source);
                    }
                    familyDocument.Regenerate();
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Не удалось вычислить геометрию семейства рамки.");
                }
                foreach (CurveElement curve in new FilteredElementCollector(familyDocument).OfClass(typeof(CurveElement)))
                {
                    var visible = curve.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM);
                    if (visible != null && visible.HasValue && visible.AsInteger() == 0) continue;
                    AddLine(curve.GeometryCurve, transform, lines);
                }
                foreach (FamilyInstance nested in new FilteredElementCollector(familyDocument).OfClass(typeof(FamilyInstance)))
                {
                    var visible = nested.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM);
                    if (visible != null && visible.HasValue && visible.AsInteger() == 0) continue;
                    var nestedTransform = transform.Multiply(nested.GetTransform());
                    if (nested.Symbol.Family.IsEditable)
                        ReadFamilyLines(nested, nestedTransform, lines, null, depth + 1);
                    else CollectLines(nested.get_Geometry(new Options()), transform, lines);
                }
            }
            finally { familyDocument.Close(false); }
        }

        private static string ScalarValue(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue) return null;
            if (parameter.StorageType == StorageType.Integer) return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
            if (parameter.StorageType == StorageType.Double) return parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture);
            return null;
        }

        private void CollectLines(GeometryElement geometry, Transform transform, IList<SheetFrameLine> lines)
        {
            if (geometry == null) return;
            foreach (GeometryObject item in geometry)
            {
                var instance = item as GeometryInstance;
                if (instance != null)
                    CollectLines(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform), lines);
                else if (item is PolyLine)
                {
                    var points = ((PolyLine)item).GetCoordinates();
                    for (int i = 1; i < points.Count; i++) AddSegment(points[i - 1], points[i], transform, lines);
                }
                else AddLine(item as Curve, transform, lines);
            }
        }

        private void AddLine(Curve curve, Transform transform, IList<SheetFrameLine> lines)
        {
            if (!(curve is Line) || !curve.IsBound) return;
            AddSegment(curve.GetEndPoint(0), curve.GetEndPoint(1), transform, lines);
        }

        private void AddSegment(XYZ a, XYZ b, Transform transform, IList<SheetFrameLine> lines)
        {
            XYZ first = transform.OfPoint(a);
            XYZ last = transform.OfPoint(b);
            lines.Add(new SheetFrameLine { X1 = first.X, Y1 = first.Y, X2 = last.X, Y2 = last.Y });
        }
    }

    internal static class SheetLayoutDiagnostics
    {
        internal static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "SAB", "InteriorElevations"); } }

        internal static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(Path.Combine(DirectoryPath, "sheet-layout.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message + Environment.NewLine);
            }
            catch { /* Diagnostics cannot interrupt placement. */ }
        }
    }

    public sealed class SheetFootprintService
    {
        public SheetRectangle Measure(Document document, ViewSheet sheet, Viewport viewport, IList<ElementId> markIds)
        {
            document.Regenerate();
            SheetRectangle bounds = FromOutline(viewport.GetBoxOutline());
            if (bounds == null) throw new InvalidOperationException("Не удалось измерить видовой экран.");
            var label = FromOutline(viewport.GetLabelOutline());
            if (label != null) bounds = bounds.Union(label);
            // The sheet bounding box also includes the title line and view annotations.
            var viewportBounds = FromBoundingBox(viewport.get_BoundingBox(sheet));
            if (viewportBounds != null) bounds = bounds.Union(viewportBounds);
            if (markIds != null)
                foreach (var id in markIds)
                {
                    var mark = document.GetElement(id);
                    var markBounds = mark == null ? null : FromBoundingBox(mark.get_BoundingBox(sheet));
                    if (markBounds == null) throw new InvalidOperationException("Не удалось измерить марку угла на листе.");
                    bounds = bounds.Union(markBounds);
                }
            return bounds;
        }

        public IList<SheetRectangle> ReadObstacles(Document document, ViewSheet sheet, SheetFrames frames, ISet<long> excludedViews = null)
        {
            var result = new List<SheetRectangle>();
            foreach (Element element in new FilteredElementCollector(document, sheet.Id).WhereElementIsNotElementType())
            {
                if (element.Category == null || element.Category.Id == new ElementId(BuiltInCategory.OST_TitleBlocks) ||
                    element is View || element is SketchPlane) continue;
                var curve = element as CurveElement;
                if (curve != null && IsFrameEdge(curve.GeometryCurve, frames)) continue;
                var viewport = element as Viewport;
                if (viewport != null && excludedViews != null && excludedViews.Contains(RevitElementIdUtils.GetElementIdValue(viewport.ViewId))) continue;
                var bounds = viewport != null ? Measure(document, sheet, viewport, null)
                    : FromBoundingBox(element.get_BoundingBox(sheet));
                if (bounds != null && bounds.Intersects(frames.Inner)) result.Add(bounds);
            }
            return result;
        }

        public bool IsFrameEdge(Curve curve, SheetFrames frames)
        {
            if (!(curve is Line) || !curve.IsBound) return false;
            var a = curve.GetEndPoint(0); var b = curve.GetEndPoint(1);
            double tolerance = UnitConversionUtils.MillimetersToFeet(0.2);
            foreach (var frame in new[] { frames.Outer, frames.Inner })
            {
                if (Math.Abs(a.X - b.X) < tolerance &&
                    (Math.Abs(a.X - frame.Left) < tolerance || Math.Abs(a.X - frame.Right) < tolerance)) return true;
                if (Math.Abs(a.Y - b.Y) < tolerance &&
                    (Math.Abs(a.Y - frame.Top) < tolerance || Math.Abs(a.Y - frame.Bottom) < tolerance)) return true;
            }
            return false;
        }

        private SheetRectangle FromOutline(Outline outline)
        {
            if (outline == null) return null; // Sheet outlines are planar; only X/Y extents matter.
            var a = outline.MinimumPoint; var b = outline.MaximumPoint;
            if (b.X - a.X <= 1e-9 || b.Y - a.Y <= 1e-9) return null;
            return new SheetRectangle(a.X, a.Y, b.X, b.Y);
        }

        public SheetRectangle FromBoundingBox(BoundingBoxXYZ box)
        {
            if (box == null) return null;
            double left = double.MaxValue, bottom = double.MaxValue, right = double.MinValue, top = double.MinValue;
            foreach (double x in new[] { box.Min.X, box.Max.X })
                foreach (double y in new[] { box.Min.Y, box.Max.Y })
                    foreach (double z in new[] { box.Min.Z, box.Max.Z })
                    {
                        XYZ point = box.Transform.OfPoint(new XYZ(x, y, z));
                        left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                        bottom = Math.Min(bottom, point.Y); top = Math.Max(top, point.Y);
                    }
            if (right - left <= 1e-9 || top - bottom <= 1e-9) return null;
            return new SheetRectangle(left, bottom, right, top);
        }
    }
}
