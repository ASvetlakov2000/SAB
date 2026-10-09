using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.ParameterTools;
using SAB.ParameterTools.Core;

internal static class Program
{
    private static int _checks, _id=10;
    private static readonly Phase Phase = new Phase { Id=new ElementId(1), Name="Новая" };
    private static BoundingBoxXYZ Box(double z=0, double top=10, double minX=0,double maxX=10) => new BoundingBoxXYZ { Min=new XYZ(minX,0,z), Max=new XYZ(maxX,10,top) };
    private static T Add<T>(Document doc,T e,BuiltInCategory category) where T:Element
    { e.Id=new ElementId(_id++); e.Document=doc; e.Category=new Category { Id=new ElementId((int)category),Name=category.ToString() }; doc.Elements.Add(e); return e; }
    private static Room Room(Document d,BoundingBoxXYZ box=null)
    { return Add(d,new Room { Bounds=box??Box(), RoomSolid=new Solid() },BuiltInCategory.OST_Rooms); }
    private static RoomResult Resolve(Document d, Element e, DoorRoomSide side=DoorRoomSide.RequireUnique)
    { using var resolver=new RoomResolver(new UIDocumentContext { Document=d,Phase=Phase },side); return resolver.Resolve(e); }
    private static void Assert(bool condition,string name)
    { if (!condition) throw new Exception(name); _checks++; Console.WriteLine("PASS: "+name); }
    private static Face Face(double z,bool up)
    {
        var b=Box(z,z); var a=new XYZ(0,0,z); var c=new XYZ(10,10,z);
        return new Face { Origin=a, Normal=new XYZ(0,0,up?1:-1),Bounds=b,
            Triangles={new MeshTriangle { Points=new[] { a,new XYZ(10,0,z),c } },new MeshTriangle { Points=new[] { a,c,new XYZ(0,10,z) } }} };
    }
    private static void Main()
    {
        var d=new Document(); var r=Room(d);
        var f=Add(d,new FamilyInstance { Native=r, Location=new LocationPoint { Point=new XYZ(5,5,-2) } },BuiltInCategory.OST_Doors);
        f.Category.Id=new ElementId(1000);
        Assert(Resolve(d,f).Room==r,"Native family room survives insertion point below room");
        f.Native=null;
        Assert(Resolve(d,f).Status==RoomResolutionStatus.NotFound,"Insertion point below room is never projected to room middle");
        f.Location=new LocationPoint { Point=new XYZ(5,5,5) };
        Assert(Resolve(d,f).Room==r,"Point at actual XYZ finds room without LevelId filtering");
        var upper=Room(d,Box(20,30)); f.CalculationPoint=new XYZ(5,5,25); f.Native=r;
        var conflict=Resolve(d,f);
        Assert(conflict.Status==RoomResolutionStatus.Ambiguous && conflict.Room==null,"Conflicting native room and calculation point do not overwrite parameters");
        Assert(conflict.Technical.Contains(upper.Id.Value.ToString()) && conflict.Technical.Contains("25.00000"),"Diagnostics retain candidate IDs and actual Z");
        f.CalculationPoint=new XYZ(5,5,50);
        Assert(Resolve(d,f).Status==RoomResolutionStatus.Insufficient,"Explicit invalid calculation point needs manual correction");
        f.Category.Id=new ElementId((int)BuiltInCategory.OST_Doors); f.CalculationPoint=null; f.From=r; f.To=upper;
        Assert(Resolve(d,f).Status==RoomResolutionStatus.Ambiguous,"Door with two native rooms needs explicit side");
        Assert(Resolve(d,f,DoorRoomSide.FromRoom).Room==r,"FromRoom policy selects from side");
        Assert(Resolve(d,f,DoorRoomSide.ToRoom).Room==upper,"ToRoom policy selects to side");
        f.To=null;
        Assert(Resolve(d,f,DoorRoomSide.ToRoom).Status==RoomResolutionStatus.NotFound,"Missing selected door side never falls back to opposite side");
        f.To=upper; f.SidePoints=new System.Collections.Generic.List<XYZ> { new XYZ(5,5,5),new XYZ(5,5,25) };
        Assert(Resolve(d,f,DoorRoomSide.ToRoom).Room==upper,"Selected door calculation point is checked independently of other side");
        upper.PhaseId=new ElementId(2);
        Assert(Resolve(d,f,DoorRoomSide.ToRoom).Status==RoomResolutionStatus.NotFound,"Native room from different phase is not accepted");
        upper.PhaseId=Phase.Id;
        f.DesignOption=new DesignOption { Id=new ElementId(90) }; upper.DesignOption=new DesignOption { Id=new ElementId(91) };
        Assert(Resolve(d,f,DoorRoomSide.ToRoom).Status==RoomResolutionStatus.NotFound,"Rooms in mutually exclusive design options are excluded");

        d=new Document(); r=Room(d); var wall=Add(d,new Wall { Bounds=Box() },BuiltInCategory.OST_Walls);
        var boundary=Face(0,true); boundary.Boundaries.Add(new SpatialElementBoundarySubface { SubfaceType=SubfaceType.Side, SpatialBoundaryElement=new LinkElementId { HostElementId=wall.Id } });
        r.RoomSolid.Faces.Add(boundary);
        Assert(Resolve(d,wall).Room==r,"Wall resolves through actual Revit boundary element relationship");
        var second=Room(d); second.RoomSolid.Faces.Add(boundary);
        Assert(Resolve(d,wall).Status==RoomResolutionStatus.Ambiguous,"All sides are considered instead of returning first wall boundary");
        second.GeometryError=true;
        Assert(Resolve(d,wall).Status==RoomResolutionStatus.Insufficient,"Failed adjacent room geometry cannot masquerade as unique match");
        d.Volumes=false;
        Assert(Resolve(d,wall).Status==RoomResolutionStatus.Insufficient && !d.Volumes,"Analysis does not switch on model volume calculations");

        d=new Document(); r=Room(d); r.RoomSolid.Faces.Add(Face(0,false));
        var floor=Add(d,new Floor { Bounds=Box(-1,0), Geometry=new GeometryElement { new Solid { Faces={ Face(0,true) } } } },BuiltInCategory.OST_Floors);
        Assert(Resolve(d,floor).Room==r,"Floor top face touches room bottom at actual elevation");
        floor.Bounds=Box(-6,-5); floor.Geometry=new GeometryElement { new Solid { Faces={Face(-5,true)} } };
        Assert(Resolve(d,floor).Status==RoomResolutionStatus.NotFound,"Floor far below room is not assigned by XY projection");
        floor.Bounds=Box(-1,0); floor.Geometry=new GeometryElement { new Solid { Faces={Face(0,true)} } };
        var obstacle=Add(d,new Wall { Bounds=Box(-.1,.1),Geometry=new GeometryElement { new Solid { Obstructs=true } } },BuiltInCategory.OST_Walls);
        Assert(Resolve(d,floor).Status==RoomResolutionStatus.Blocked,"Foreign construction prevents geometric assignment through it");
        obstacle.PhaseStatus=ElementOnPhaseStatus.Demolished;
        Assert(Resolve(d,floor).Room==r,"Demolished obstruction from current phase does not block contact");
        var ceiling=Add(d,new Ceiling { Bounds=Box(10,11),Geometry=new GeometryElement {new Solid { Faces={Face(10,false)} }} },BuiltInCategory.OST_Ceilings);
        r.RoomSolid.Faces.Add(Face(10,true));
        Assert(Resolve(d,ceiling).Room==r,"Ceiling underside touches room top");
        var roof=Add(d,new RoofBase { Bounds=Box(10,11),Geometry=new GeometryElement {new Solid { Faces={Face(10,false)} }} },BuiltInCategory.OST_Roofs);
        Assert(Resolve(d,roof).Room==r,"Roof underside is supported at actual room height");
        roof.PhaseStatus=ElementOnPhaseStatus.Demolished;
        Assert(Resolve(d,roof).Status==RoomResolutionStatus.NotFound,"Target demolished in current phase cannot obtain a room");
        var tiny=Room(d,new BoundingBoxXYZ {Min=new XYZ(.01,.01,0),Max=new XYZ(.02,.02,10)});
        var tinyBottom=Face(0,false); tinyBottom.Bounds=new BoundingBoxXYZ {Min=new XYZ(.01,.01,0),Max=new XYZ(.02,.02,0)};
        tinyBottom.Triangles.Clear(); tinyBottom.Triangles.Add(new MeshTriangle {Points=new[] {new XYZ(.01,.01,0),new XYZ(.02,.01,0),new XYZ(.01,.02,0)}});
        tiny.RoomSolid.Faces.Add(tinyBottom);
        Assert(Resolve(d,floor).Status==RoomResolutionStatus.Ambiguous,"Reverse contact sampling detects small room between points of a large slab");
        d.Elements.Remove(tiny); r.RoomSolid.Bounds=r.Bounds;
        var finish=Add(d,new Floor {Bounds=Box(.1,.2),Geometry=new GeometryElement {new Solid {Bounds=Box(.1,.2),Faces={Face(.2,true)}}}},BuiltInCategory.OST_Floors);
        Assert(Resolve(d,finish).Room==r,"Non-room-bounding finish floor inside room is detected by volume intersection");
        tiny.RoomSolid.Bounds=tiny.Bounds; d.Elements.Add(tiny);
        Assert(Resolve(d,finish).Status==RoomResolutionStatus.Ambiguous,"Volume intersection detects tiny secondary room even without boundary face samples");
        var annotation=Add(d,new Dimension { Location=new LocationPoint {Point=new XYZ(5,5,5)} },BuiltInCategory.OST_Doors);
        Assert(Resolve(d,annotation).Status==RoomResolutionStatus.Unsupported,"Dimension text position is not used as room ownership");
        using (var resolver=new RoomResolver(new UIDocumentContext {Document=d,Phase=null}))
            Assert(resolver.Resolve(floor).Status==RoomResolutionStatus.Insufficient,"Missing view phase has a distinct diagnostic");
        Console.WriteLine("All "+_checks+" resolver regression checks passed using API doubles; native Revit geometry remains unverified.");
    }
}
