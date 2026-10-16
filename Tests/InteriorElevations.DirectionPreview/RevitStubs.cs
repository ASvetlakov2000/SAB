using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory { OST_GenericAnnotation = -1 }
    public enum BuiltInParameter { VIEWPORT_DETAIL_NUMBER, VIEWER_BOUND_OFFSET_FAR }
    public enum StorageType { String, Integer }
    public enum TransactionStatus { Started, Committed, RolledBack }
    public class ElementId
    {
        public static ElementId InvalidElementId = new ElementId(-1);
        public int IntegerValue;
        public ElementId(int id) { IntegerValue = id; }
    }
    public class XYZ
    {
        public double X, Y, Z;
        public XYZ(double x, double y, double z) { X=x; Y=y; Z=z; }
        public static XYZ Zero => new XYZ(0,0,0);
        public static XYZ BasisY => new XYZ(0,1,0);
        public static XYZ BasisZ => new XYZ(0,0,1);
        public double GetLength() => Math.Sqrt(X*X+Y*Y+Z*Z);
        public XYZ Normalize() => this*(1/GetLength());
        public double DistanceTo(XYZ other) => (this-other).GetLength();
        public double DotProduct(XYZ b) => X*b.X+Y*b.Y+Z*b.Z;
        public XYZ CrossProduct(XYZ b) => new XYZ(Y*b.Z-Z*b.Y, Z*b.X-X*b.Z, X*b.Y-Y*b.X);
        public static XYZ operator +(XYZ a,XYZ b) => new XYZ(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static XYZ operator -(XYZ a,XYZ b) => new XYZ(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static XYZ operator -(XYZ a) => a*(-1);
        public static XYZ operator *(XYZ a,double b) => new XYZ(a.X*b,a.Y*b,a.Z*b);
    }
    public class Curve { }
    public class Line : Curve
    {
        public XYZ Start, End;
        public static Line CreateBound(XYZ a,XYZ b) => new Line { Start=a, End=b };
        public XYZ GetEndPoint(int i) => i==0?Start:End;
    }
    public class LocationPoint { public XYZ Point; }
    public class Transform
    {
        public XYZ BasisY;public Func<XYZ,XYZ> Mapping;
        public Transform Inverse=>this;
        public XYZ OfPoint(XYZ p)=>Mapping==null?p:Mapping(p);
    }
    public class BoundingBoxXYZ {public XYZ Min,Max;public Transform Transform;}
    public class Parameter
    {
        public string Value;
        public double DoubleValue;
        public bool IsReadOnly, Reject;
        public StorageType StorageType = StorageType.String;
        public string AsString() => Value;
        public bool Set(string value) { if (Reject) return false; Value=value; return true; }
        public double AsDouble()=>DoubleValue;
        public bool Set(double value){DoubleValue=value;return true;}
    }
    public class Category { public ElementId Id = new ElementId((int)BuiltInCategory.OST_GenericAnnotation); }
    public class Element
    {
        public ElementId Id;
        public string Name, UniqueId = Guid.NewGuid().ToString();
        public bool IsValidObject = true;
        public Category Category = new Category();
        public Document Document;
        public Dictionary<Guid, ExtensibleStorage.Entity> Entities = new Dictionary<Guid, ExtensibleStorage.Entity>();
        public void SetEntity(ExtensibleStorage.Entity entity) { Entities[entity.Schema.Id]=entity; }
        public ExtensibleStorage.Entity GetEntity(ExtensibleStorage.Schema schema) => Entities.TryGetValue(schema.Id,out var e)?e:new ExtensibleStorage.Entity(null);
    }
    public class Family : Element { }
    public class FamilySymbol : Element
    {
        public Family Family;
        public bool IsActive;
        public void Activate() { IsActive=true; }
    }
    public class CurveElement : Element { public Curve GeometryCurve; }
    public class ViewPlan : Element
    {
        public bool Hidden;
        public bool GetCategoryHidden(ElementId id) => Hidden;
    }
    public class Viewport : Element
    {
        public Parameter Number = new Parameter();
        public Parameter get_Parameter(BuiltInParameter id) => Number;
    }
    public class ViewSection : Element
    {
        public XYZ ViewDirection;public bool CropBoxActive,CropBoxVisible;public BoundingBoxXYZ CropBox;
        public Parameter Far=new Parameter();public Parameter get_Parameter(BuiltInParameter p)=>Far;
    }
    public class FamilyInstance : Element
    {
        public LocationPoint Location;
        public double Angle;
        public Dictionary<string,Parameter> Parameters = new Dictionary<string,Parameter>();
        public Parameter LookupParameter(string name) => Parameters.TryGetValue(name,out var p)?p:null;
        public Transform GetTransform() => new Transform { BasisY=new XYZ(-Math.Sin(Angle),Math.Cos(Angle),0) };
    }
    public class ElevationMarker : Element
    {
        public XYZ Point;
        public ViewSection View;
        public bool IsAvailableIndex(int i) => i==0;
        public static ElevationMarker CreateElevationMarker(Document doc,ElementId type,XYZ point,int scale)
        { var m=new ElevationMarker{Point=point};doc.Add(m);return m; }
        public ViewSection CreateElevation(Document doc,ElementId plan,int index)
        { View=new ViewSection { ViewDirection=doc.InitialDirection };doc.Add(View);doc.CurrentMarker=this;return View; }
    }
    public class Document
    {
        public Guid SessionIdentity=Guid.NewGuid();
        public string Title="Same project title";
        public override bool Equals(object other)=>other is Document document && SessionIdentity==document.SessionIdentity;
        public override int GetHashCode()=>SessionIdentity.GetHashCode();
        public Document NewWrapper()=>new Document{SessionIdentity=SessionIdentity,Elements=Elements};
        public bool IsValidObject=true, FailDelete, FailMove, FailRotate, MissingParameter, FailCommit, ReadOnlyCallback;
        public int MoveCalls;
        public int LoadCalls, Regenerations, Rollbacks, Transactions;
        public XYZ InitialDirection=new XYZ(1,0,0);
        public ElevationMarker CurrentMarker;
        public List<Element> Elements = new List<Element>();
        public List<double> Rotations = new List<double>();
        public Action<Document,double> OnElevationRotate;
        public Action<Document> OnRegenerate;
        private int next=10;
        public Element GetElement(ElementId id) => Elements.FirstOrDefault(e=>e.Id.IntegerValue==id.IntegerValue);
        public void Add(Element e) { e.Id=new ElementId(next++);e.Document=this;Elements.Add(e); }
        public void Delete(ICollection<ElementId> ids)
        { if(FailDelete)throw new InvalidOperationException("Delete blocked");Elements.RemoveAll(e=>ids.Any(id=>id.IntegerValue==e.Id.IntegerValue)); }
        public void Regenerate() { Regenerations++;OnRegenerate?.Invoke(this); }
        public Factory Create => new Factory(this);
        public bool LoadFamily(string path,out Family family)
        {LoadCalls++;family=new Family {Name=ElevationFamilyName};Add(family);Add(new FamilySymbol {Name="Default",Family=family});return true;}
        public const string ElevationFamilyName="SAB_Развертки_Направление";
    }
    public class Factory
    {
        readonly Document doc;
        public Factory(Document doc){this.doc=doc;}
        public FamilyInstance NewFamilyInstance(XYZ point,FamilySymbol symbol,ViewPlan view)
        {
            var f=new FamilyInstance {Location=new LocationPoint{Point=point}};
            f.Parameters["Номер помещения"]=new Parameter();
            if(!doc.MissingParameter)f.Parameters["Номер вида"]=new Parameter();
            doc.Add(f);return f;
        }
    }
    public static class ElementTransformUtils
    {
        static XYZ Rotate(XYZ v,double a)=>new XYZ(v.X*Math.Cos(a)-v.Y*Math.Sin(a),v.X*Math.Sin(a)+v.Y*Math.Cos(a),v.Z);
        public static void RotateElement(Document doc,ElementId id,Line axis,double angle)
        {
            if(doc.FailRotate)throw new InvalidOperationException("Rotation blocked");
            if(doc.GetElement(id) is FamilyInstance f) f.Angle+=angle;
            else if(doc.GetElement(id) is ElevationMarker m)
            {doc.Rotations.Add(angle);m.View.ViewDirection=Rotate(m.View.ViewDirection,angle);doc.OnElevationRotate?.Invoke(doc,angle);}
        }
        public static void MoveElement(Document doc,ElementId id,XYZ delta)
        {doc.MoveCalls++;if(doc.FailMove)throw new InvalidOperationException("Move blocked");var f=(FamilyInstance)doc.GetElement(id);f.Location.Point+=delta;}
    }
    public class Transaction : IDisposable
    {
        readonly Document doc;List<Element> original;Dictionary<FamilyInstance,(XYZ,double)> poses;TransactionStatus status;
        public Transaction(Document doc,string name=""){this.doc=doc;}
        public void Start()
        {if(doc.ReadOnlyCallback)throw new InvalidOperationException("Read-only event context");doc.Transactions++;original=doc.Elements.ToList();poses=original.OfType<FamilyInstance>().ToDictionary(f=>f,f=>(f.Location.Point,f.Angle));status=TransactionStatus.Started;}
        public TransactionStatus Commit(){if(doc.FailCommit){RollBack();return status;}status=TransactionStatus.Committed;return status;}
        public void RollBack(){doc.Elements=original;foreach(var item in poses){item.Key.Location.Point=item.Value.Item1;item.Key.Angle=item.Value.Item2;}doc.Rollbacks++;status=TransactionStatus.RolledBack;}
        public void Dispose(){if(status==TransactionStatus.Started)RollBack();}
    }
    public class SubTransaction : Transaction {public SubTransaction(Document doc):base(doc){} }
    public class FilteredElementCollector : IEnumerable<Element>
    {
        readonly Document doc;Type type;
        public FilteredElementCollector(Document doc){this.doc=doc;}
        public FilteredElementCollector OfClass(Type t){type=t;return this;}
        public FilteredElementCollector OfCategory(BuiltInCategory category)=>this;
        public IEnumerator<Element> GetEnumerator()=>doc.Elements.Where(e=>type.IsInstanceOfType(e)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
namespace Autodesk.Revit.DB.ExtensibleStorage
{
    public enum AccessLevel { Public }
    public class Field {public string Name;}
    public class Schema
    {
        public Guid Id;static readonly Dictionary<Guid,Schema> all=new Dictionary<Guid,Schema>();
        public static Schema Lookup(Guid id)=>all.TryGetValue(id,out var s)?s:null;
        public static Schema Add(Guid id){var s=new Schema{Id=id};all[id]=s;return s;}
        public Field GetField(string name)=>new Field{Name=name};
    }
    public class SchemaBuilder
    {
        readonly Guid id;public SchemaBuilder(Guid id){this.id=id;}
        public void SetSchemaName(string name){} public void SetReadAccessLevel(AccessLevel a){} public void SetWriteAccessLevel(AccessLevel a){}
        public void AddSimpleField(string name,Type type){} public Schema Finish()=>Schema.Add(id);
    }
    public class Entity
    {
        public Schema Schema;readonly Dictionary<string,object> data=new Dictionary<string,object>();
        public Entity(Schema schema){Schema=schema;} public bool IsValid()=>Schema!=null;
        public void Set<T>(Field field,T value){data[field.Name]=value;} public T Get<T>(Field field)=>(T)data[field.Name];
    }
}
namespace SAB.InteriorElevations.Models
{
    public class RoomData {public string RoomNumber;}
    public class ElevationSettings {public double LeftOffsetMm,RightOffsetMm,TopOffsetMm,BottomOffsetMm,ViewDepthMm;}
}
namespace SAB.InteriorElevations.Commands {public class CreateInteriorElevationsCommand{} }
namespace SAB.InteriorElevations.Utils
{
    public static class UnitConversionUtils {public static double MillimetersToFeet(double n)=>n/304.8;public static double FeetToMillimeters(double n)=>n*304.8;}
    public static class RevitElementIdUtils
    {public static long GetElementIdValue(Autodesk.Revit.DB.ElementId id)=>id.IntegerValue;public static bool AreEqual(Autodesk.Revit.DB.ElementId a,Autodesk.Revit.DB.ElementId b)=>a.IntegerValue==b.IntegerValue;}
}
namespace SAB.Services.PluginResources
{
    public class PluginFamilyResourceService {public static List<string> Paths=new List<string>();public IList<string> GetCommandFamilyFilePaths(Type type)=>Paths;}
}
