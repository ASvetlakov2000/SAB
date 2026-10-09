using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory { OST_TitleBlocks }
    public enum BuiltInParameter { IS_VISIBLE_PARAM }
    public enum StorageType { None, Integer, Double, String, ElementId }
    public enum TransactionStatus { Started, Committed }
    public class ElementId
    {
        public int IntegerValue;
        public ElementId(int value) { IntegerValue = value; }
        public ElementId(BuiltInCategory value) { IntegerValue = (int)value; }
        public static bool operator ==(ElementId a, ElementId b) => ReferenceEquals(a, b) || a is not null && b is not null && a.IntegerValue == b.IntegerValue;
        public static bool operator !=(ElementId a, ElementId b) => !(a == b);
        public override bool Equals(object value) => value is ElementId other && this == other;
        public override int GetHashCode() => IntegerValue;
    }
    public class XYZ
    {
        public double X, Y, Z;
        public XYZ(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
    public class Transform
    {
        public XYZ Offset = new XYZ(0, 0, 0);
        public static Transform Identity => new Transform();
        public XYZ OfPoint(XYZ point) => new XYZ(point.X + Offset.X, point.Y + Offset.Y, point.Z + Offset.Z);
        public Transform Multiply(Transform other) => new Transform { Offset = OfPoint(other.Offset) };
    }
    public class GeometryObject { }
    public class GeometryElement : List<GeometryObject> { }
    public class Curve : GeometryObject
    {
        public bool IsBound = true;
        public XYZ[] Points;
        public XYZ GetEndPoint(int index) => Points[index];
    }
    public class Line : Curve { public Line(XYZ a, XYZ b) { Points = new[] { a, b }; } }
    public class PolyLine : GeometryObject { public IList<XYZ> GetCoordinates() => new List<XYZ>(); }
    public class GeometryInstance : GeometryObject
    {
        public Transform Transform = Transform.Identity;
        public GeometryElement GetSymbolGeometry() => new GeometryElement();
    }
    public class Outline
    {
        public bool IsEmpty;
        public XYZ MinimumPoint, MaximumPoint;
    }
    public class BoundingBoxXYZ { public XYZ Min, Max; public Transform Transform = Transform.Identity; }
    public class Options { public View View; }
    public class Category { public ElementId Id; }
    public class Definition { public string Name; }
    public class Parameter
    {
        public bool HasValue = true;
        public StorageType StorageType;
        public int IntegerValue;
        public double DoubleValue;
        public int AsInteger() => IntegerValue;
        public double AsDouble() => DoubleValue;
    }
    public class Element
    {
        public ElementId Id = new ElementId(1), OwnerViewId;
        public Category Category;
        public Document Document;
        public Dictionary<string, Parameter> Parameters = new Dictionary<string, Parameter>();
        public Parameter LookupParameter(string name) => Parameters.TryGetValue(name, out var parameter) ? parameter : null;
        public Parameter get_Parameter(BuiltInParameter id) => null;
        public virtual GeometryElement get_Geometry(Options options) => null;
        public virtual BoundingBoxXYZ get_BoundingBox(View view) => null;
    }
    public class CurveElement : Element { public Curve GeometryCurve; }
    public class View : Element { }
    public class ViewSheet : View { public string SheetNumber; }
    public class SketchPlane : Element { }
    public class Viewport : Element
    {
        public ElementId ViewId { get; set; }
        public Outline Box;
        public Outline GetBoxOutline() => Box;
        public Outline GetLabelOutline() => null;
    }
    public class Family : Element { public string Name; public bool IsEditable = true; public Document DefinitionDocument; }
    public class FamilySymbol : Element { public string Name; public Family Family; }
    public class FamilyInstance : Element
    {
        public FamilySymbol Symbol;
        public Transform InstanceTransform = Transform.Identity;
        public int GeometryReads;
        public ElementId GetTypeId() => Symbol.Id;
        public Transform GetTransform() => InstanceTransform;
        public override GeometryElement get_Geometry(Options options) { GeometryReads++; return null; }
    }
    public class FamilyParameter
    {
        public bool IsInstance, IsReadOnly, IsDeterminedByFormula, IsReporting;
        public StorageType StorageType;
        public Definition Definition;
    }
    public class FamilyType { public string Name; }
    public class FamilyManager
    {
        public FamilyType CurrentType;
        public List<FamilyType> Types = new List<FamilyType>();
        public List<FamilyParameter> Parameters = new List<FamilyParameter>();
        public int LastInteger;
        public void NewType(string name) { CurrentType = new FamilyType { Name = name }; }
        public void Set(FamilyParameter parameter, int value) { LastInteger = value; }
        public void Set(FamilyParameter parameter, double value) { }
    }
    public class Document
    {
        public bool IsModifiable, ClosedWithoutSaving;
        public int EditFamilyCalls;
        public Action OnRegenerate;
        public List<Element> Elements = new List<Element>();
        public FamilyManager FamilyManager = new FamilyManager();
        public void Regenerate() { if (!IsModifiable) throw new InvalidOperationException("Regenerate needs a transaction"); OnRegenerate?.Invoke(); }
        public Document EditFamily(Family family)
        {
            if (IsModifiable) throw new InvalidOperationException("EditFamily cannot run inside a project transaction");
            EditFamilyCalls++; return family.DefinitionDocument;
        }
        public Element GetElement(ElementId id) => Elements.FirstOrDefault(e => e.Id == id);
        public void Close(bool save) { ClosedWithoutSaving = !save; }
    }
    public class Transaction : IDisposable
    {
        private Document document;
        public Transaction(Document value, string name) { document = value; }
        public void Start() { document.IsModifiable = true; }
        public TransactionStatus Commit() { document.IsModifiable = false; return TransactionStatus.Committed; }
        public void Dispose() { document.IsModifiable = false; }
    }
    public class FilteredElementCollector : IEnumerable<Element>
    {
        private IEnumerable<Element> elements;
        public FilteredElementCollector(Document document) { elements = document.Elements; }
        public FilteredElementCollector(Document document, ElementId view) { elements = document.Elements.Where(e => e.OwnerViewId == view); }
        public FilteredElementCollector OfCategory(BuiltInCategory category) { elements = elements.Where(e => e.Category?.Id == new ElementId(category)); return this; }
        public FilteredElementCollector OfClass(Type type) { elements = elements.Where(e => type.IsInstanceOfType(e)); return this; }
        public FilteredElementCollector WhereElementIsNotElementType() => this;
        public IEnumerator<Element> GetEnumerator() => elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
namespace SAB.InteriorElevations.Utils
{
    public static class UnitConversionUtils { public static double MillimetersToFeet(double value) => value / 304.8; }
    public static class RevitElementIdUtils { public static long GetElementIdValue(Autodesk.Revit.DB.ElementId id) => id.IntegerValue; }
}
