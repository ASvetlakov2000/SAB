// Deterministic boxes/planes for regression tests. These doubles do not validate the Revit kernel.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Autodesk.Revit.DB
{
    public class ElementId : IEquatable<ElementId>
    {
        public int Value; public ElementId(int v) { Value = v; }
        public static ElementId InvalidElementId = new ElementId(-1);
        public bool Equals(ElementId o) => o != null && Value == o.Value;
        public override bool Equals(object o) => Equals(o as ElementId);
        public override int GetHashCode() => Value;
        public static bool operator ==(ElementId a, ElementId b) => Equals(a, b);
        public static bool operator !=(ElementId a, ElementId b) => !Equals(a, b);
    }
    public enum BuiltInCategory { OST_Rooms, OST_Doors, OST_Windows, OST_Walls, OST_Floors, OST_Ceilings, OST_Roofs }
    public enum BuiltInParameter { ROOM_PHASE, VIEW_PHASE }
    public enum ViewDetailLevel { Medium, Fine }
    public enum SpatialElementBoundaryLocation { Finish }
    public enum SubfaceType { Side, Top, Bottom }
    public enum ElementOnPhaseStatus { Existing, New, Demolished, None }
    public class Category { public ElementId Id; public string Name; }
    public class Parameter { public ElementId Id; public ElementId AsElementId() => Id; }
    public class Document { public List<Element> Elements = new List<Element>(); public bool Volumes = true; public Element GetElement(ElementId id) => Elements.FirstOrDefault(e => e.Id == id); }
    public class Element
    {
        public ElementId Id, GroupId = ElementId.InvalidElementId;
        public string UniqueId => "uid-" + Id.Value;
        public string Name = "test";
        public Document Document;
        public Category Category;
        public object Location;
        public BoundingBoxXYZ Bounds;
        public GeometryElement Geometry;
        public DesignOption DesignOption;
        public bool ViewSpecific;
        public ElementOnPhaseStatus PhaseStatus = ElementOnPhaseStatus.New;
        public ElementId PhaseId = new ElementId(1);
        public virtual Parameter get_Parameter(BuiltInParameter p) => new Parameter { Id = PhaseId };
        public BoundingBoxXYZ get_BoundingBox(object view) => Bounds;
        public GeometryElement get_Geometry(Options o) => Geometry;
        public ElementOnPhaseStatus GetPhaseStatus(ElementId id) => PhaseStatus;
        public bool HasPhases() => true;
    }
    public class DesignOption : Element { }
    public class Phase : Element { }
    public class Dimension : Element { }
    public class Wall : Element { }
    public class Floor : Element { }
    public class Ceiling : Element { }
    public class RoofBase : Element { }
    public class RevitLinkInstance : Element { }
    public class FamilyInstance : Element
    {
        public Architecture.Room Native, From, To;
        public XYZ CalculationPoint;
        public List<XYZ> SidePoints;
        public bool HasSpatialElementCalculationPoint => CalculationPoint != null;
        public bool HasSpatialElementFromToCalculationPoints => SidePoints != null;
        public XYZ GetSpatialElementCalculationPoint() => CalculationPoint;
        public IList<XYZ> GetSpatialElementFromToCalculationPoints() => SidePoints;
        public Architecture.Room get_Room(Phase p) => Native;
        public Architecture.Room get_FromRoom(Phase p) => From;
        public Architecture.Room get_ToRoom(Phase p) => To;
    }
    public class LocationPoint { public XYZ Point; }
    public class Options { public ViewDetailLevel DetailLevel; }
    public class AreaVolumeSettings { public bool ComputeVolumes; public static AreaVolumeSettings GetAreaVolumeSettings(Document d) => new AreaVolumeSettings { ComputeVolumes = d.Volumes }; }
    public class UnitTypeId { public static UnitTypeId Millimeters = new UnitTypeId(); }
    public static class UnitUtils { public static double ConvertToInternalUnits(double n, UnitTypeId u) => n / 304.8; }
    public class XYZ
    {
        public double X, Y, Z; public XYZ(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static XYZ operator +(XYZ a, XYZ b) => new XYZ(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static XYZ operator -(XYZ a, XYZ b) => new XYZ(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static XYZ operator *(XYZ a, double k) => new XYZ(a.X*k,a.Y*k,a.Z*k);
        public static XYZ operator /(XYZ a, double k) => a*(1/k);
        public double DotProduct(XYZ b) => X*b.X+Y*b.Y+Z*b.Z;
        public double Length => Math.Sqrt(DotProduct(this));
        public XYZ Normalize() => this/Length;
    }
    public class UV { public XYZ Point; }
    public class BoundingBoxXYZ
    {
        public XYZ Min, Max;
        public bool Contains(XYZ p) => p.X>=Min.X && p.X<=Max.X && p.Y>=Min.Y && p.Y<=Max.Y && p.Z>=Min.Z && p.Z<=Max.Z;
        public bool Overlaps(BoundingBoxXYZ b) => Min.X<=b.Max.X && Max.X>=b.Min.X && Min.Y<=b.Max.Y && Max.Y>=b.Min.Y && Min.Z<=b.Max.Z && Max.Z>=b.Min.Z;
    }
    public class Outline : BoundingBoxXYZ { public Outline(XYZ min,XYZ max) { Min=min; Max=max; } }
    public class BoundingBoxIntersectsFilter { public Outline Bounds; public BoundingBoxIntersectsFilter(Outline b) { Bounds=b; } }
    public class FilteredElementCollector : IEnumerable<Element>
    {
        private IEnumerable<Element> _items;
        public FilteredElementCollector(Document d) { _items=d.Elements; }
        public FilteredElementCollector OfCategory(BuiltInCategory c) { _items=_items.Where(e => e.Category?.Id.Value==(int)c); return this; }
        public FilteredElementCollector WhereElementIsNotElementType() => this;
        public FilteredElementCollector WherePasses(BoundingBoxIntersectsFilter f) { _items=_items.Where(e => e.Bounds != null && e.Bounds.Overlaps(f.Bounds)); return this; }
        public IEnumerator<Element> GetEnumerator() => _items.GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class GeometryObject { }
    public class GeometryElement : List<GeometryObject> { }
    public class GeometryInstance : GeometryObject { public GeometryElement Geometry; public GeometryElement GetInstanceGeometry() => Geometry; }
    public class IntersectionResult { public UV UVPoint; public XYZ XYZPoint; public double Distance; }
    public class Face
    {
        public XYZ Normal, Origin; public BoundingBoxXYZ Bounds; public List<MeshTriangle> Triangles = new List<MeshTriangle>();
        public List<SpatialElementBoundarySubface> Boundaries = new List<SpatialElementBoundarySubface>();
        public Mesh Triangulate() => new Mesh { Triangles=Triangles };
        public XYZ ComputeNormal(UV uv) => Normal;
        public bool IsInside(UV uv) => Bounds.Contains(uv.Point);
        public IntersectionResult Project(XYZ p)
        {
            double d=(p-Origin).DotProduct(Normal); var q=p-Normal*d;
            return new IntersectionResult { XYZPoint=q, UVPoint=new UV { Point=q }, Distance=Math.Abs(d) };
        }
    }
    public class Mesh { public List<MeshTriangle> Triangles; public int NumTriangles => Triangles.Count; public MeshTriangle get_Triangle(int i) => Triangles[i]; }
    public class MeshTriangle { public XYZ[] Points; public XYZ get_Vertex(int i) => Points[i]; }
    public class Solid : GeometryObject, IDisposable
    {
        public BoundingBoxXYZ Bounds;
        public List<Face> Faces = new List<Face>(); public double Volume=1;
        public bool Obstructs;
        public SolidCurveIntersection IntersectWithCurve(Line l,SolidCurveIntersectionOptions o) => new SolidCurveIntersection { SegmentCount=Obstructs?1:0 };
        public void Dispose() { }
    }
    public enum BooleanOperationsType { Intersect }
    public static class BooleanOperationsUtils
    {
        public static Solid ExecuteBooleanOperation(Solid a,Solid b,BooleanOperationsType type)
        {
            if (a.Bounds==null || b.Bounds==null) return new Solid {Volume=0};
            var x=Math.Max(0,Math.Min(a.Bounds.Max.X,b.Bounds.Max.X)-Math.Max(a.Bounds.Min.X,b.Bounds.Min.X));
            var y=Math.Max(0,Math.Min(a.Bounds.Max.Y,b.Bounds.Max.Y)-Math.Max(a.Bounds.Min.Y,b.Bounds.Min.Y));
            var z=Math.Max(0,Math.Min(a.Bounds.Max.Z,b.Bounds.Max.Z)-Math.Max(a.Bounds.Min.Z,b.Bounds.Min.Z));
            return new Solid {Volume=x*y*z};
        }
    }
    public class Line : IDisposable { public static Line CreateBound(XYZ a,XYZ b) => new Line(); public void Dispose() { } }
    public class SolidCurveIntersectionOptions : IDisposable { public void Dispose() { } }
    public class SolidCurveIntersection : IDisposable { public int SegmentCount; public void Dispose() { } }
    public class SpatialElementBoundaryOptions { public SpatialElementBoundaryLocation SpatialElementBoundaryLocation; }
    public class LinkElementId { public ElementId HostElementId = ElementId.InvalidElementId; }
    public class SpatialElementBoundarySubface { public LinkElementId SpatialBoundaryElement; public SubfaceType SubfaceType; }
    public class SpatialElementGeometryResults : IDisposable
    {
        public Solid Solid; public Solid GetGeometry() => Solid;
        public IList<SpatialElementBoundarySubface> GetBoundaryFaceInfo(Face f) => f.Boundaries;
        public void Dispose() { }
    }
    public class SpatialElementGeometryCalculator : IDisposable
    {
        public static int Calls;
        public SpatialElementGeometryCalculator(Document d, SpatialElementBoundaryOptions o) { }
        public SpatialElementGeometryResults CalculateSpatialElementGeometry(Architecture.Room r)
        { Calls++; if (r.GeometryError) throw new InvalidOperationException("fake room geometry failure"); return new SpatialElementGeometryResults { Solid=r.RoomSolid }; }
        public void Dispose() { }
    }
}
namespace Autodesk.Revit.DB.Architecture
{
    public class Room : Autodesk.Revit.DB.Element
    {
        public double Area=100; public string Number="101"; public Autodesk.Revit.DB.Solid RoomSolid;
        public bool GeometryError;
        public bool IsPointInRoom(Autodesk.Revit.DB.XYZ p) => Bounds.Contains(p);
    }
}
namespace Autodesk.Revit.UI { public class UIDocument { public Autodesk.Revit.DB.Element ActiveView; public Autodesk.Revit.DB.Document Document; } }
namespace SAB.ParameterTools { internal static class Ids { internal static long Value(Autodesk.Revit.DB.ElementId id) => id.Value; } }
