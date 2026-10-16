using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Geometry;
using SAB.InteriorElevations.Services.Elevations;
using SAB.Services.PluginResources;

static class Program
{
    static int passed;
    static void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
    static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    static void Near(double a,double b) { Assert(Math.Abs(a-b)<1e-8); }
    static void Throws(Action action) { try { action(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
    static ContourSegment S(double x,double y,double a,double b) => new(new(x,y),new(a,b));
    static List<ContourSegment> Rect(double w=4,double h=3) => new(){S(0,0,w,0),S(w,0,w,h),S(w,h,0,h),S(0,h,0,0)};
    static ContourDirectionResult Build(IList<ContourSegment> s,double d=.25,double t=.001) => ContourDirectionGeometry.Build(s,d,t);
    static void Inward(IList<ContourSegment> s,ContourDirectionResult r,double x,double y)
    {
        Assert(r.IsClosed);
        for(int i=0;i<s.Count;i++) { var mid=(s[i].Start+s[i].End)*.5;var n=r.Normals[i];Assert((x-mid.X)*n.X+(y-mid.Y)*n.Y>0); }
    }
    static (Document,ViewPlan,ElevationLineData) Fixture(bool withFamily=true)
    {
        var d=new Document();var plan=new ViewPlan();d.Add(plan);
        if(withFamily) {var f=new Family{Name=ElevationDirectionPreviewService.FamilyName};d.Add(f);d.Add(new FamilySymbol{Family=f,Name="Default"});}
        var c=new CurveElement{GeometryCurve=Line.CreateBound(XYZ.Zero,new XYZ(4,0,0))};d.Add(c);
        var l=new ElevationLineData{LineElementId=c.Id,StartPoint=XYZ.Zero,EndPoint=new XYZ(4,0,0),MidPoint=new XYZ(2,0,0),InsideNormal=XYZ.BasisY,MarkerPoint=new XYZ(2,.25,0),MarkerDistance=.25,PlannedDetailNumber="1-Р",RoomData=new RoomData{RoomNumber="А-101"}};
        return (d,plan,l);
    }
    static (Document,ViewPlan,ElevationLineData,ElevationDirectionPreviewService) Preview()
    {
        var (d,p,l)=Fixture();var s=new ElevationDirectionPreviewService();s.Create(d,p,new[]{l});return(d,p,l,s);
    }
    static FamilyInstance Mark(Document d,ElevationDirectionPreviewService s) => (FamilyInstance)d.GetElement(s.Markers[0].MarkerId);
    static void Main()
    {
        Test("rectangle inward in either winding and arbitrary source order",()=>{
            var s=Rect();Inward(s,Build(s),2,1.5);
            s=new[]{s[2],s[0],s[3],s[1]}.Select(a=>new ContourSegment(a.End,a.Start)).ToList();Inward(s,Build(s),2,1.5);
        });
        Test("concave L follows local boundary without room centre",()=>{
            var s=new[]{S(0,0,4,0),S(4,0,4,1),S(4,1,1,1),S(1,1,1,4),S(1,4,0,4),S(0,4,0,0)};
            var r=Build(s);Near(r.Normals[2].Y,-1);Near(r.Normals[3].X,-1);
        });
        Test("nested cabin is an independent inward contour",()=>{Inward(Rect(10,10),Build(Rect(10,10)),5,5);Inward(Rect(1,1),Build(Rect(1,1)),.5,.5);});
        Test("narrow contour caps marker distance before opposite boundary",()=>{var r=Build(Rect(2,.1));Near(r.MarkerDistances[0],.05);Near(r.MarkerDistances[2],.05);Near(r.MarkerDistances[1],.25);});
        Test("short edge is retained and perpendicular",()=>{var s=new[]{S(0,0,.01,0),S(.01,0,1,0),S(1,0,1,1),S(1,1,0,1),S(0,1,0,0)};var r=Build(s);Assert(r.Normals.Length==5);Near(r.Normals[0].Y,1);});
        Test("joined 0.9mm gap does not skew actual 10mm tangent",()=>{var r=Build(new[]{S(0,0,.01,0),S(.01,.0009,.02,.0009)},.001,.001);Near(r.Normals[1].X,0);Near(Math.Abs(r.Normals[1].Y),1);});
        Test("open chain keeps one logical side despite reversed edges",()=>{var r=Build(new[]{S(0,0,2,0),S(2,2,2,0),S(2,2,0,2)});Assert(!r.IsClosed);Near(r.Normals[0].Y,1);Near(r.Normals[1].X,-1);Near(r.Normals[2].Y,-1);});
        Test("single open line can be previewed",()=>{Assert(!Build(new[]{S(0,0,.01,0)}).IsClosed);});
        Test("three-sided chain uses virtual closure inward in either winding",()=>{
            var s=new[]{S(0,0,4,0),S(4,0,4,3),S(4,3,0,3)};
            foreach(var chain in new[]{s,s.Reverse().Select(e=>new ContourSegment(e.End,e.Start)).ToArray(),new[]{s[2],new ContourSegment(s[0].End,s[0].Start),s[1]}}){
                var r=Build(chain);Assert(!r.IsClosed&&r.UsesVirtualClosure&&r.Normals.Length==3);
                for(int i=0;i<chain.Length;i++){var mid=(chain[i].Start+chain[i].End)*.5;var n=r.Normals[i];Assert((2-mid.X)*n.X+(1.5-mid.Y)*n.Y>0);}
            }
        });
        Test("virtual closure uses free endpoints and includes translated polygon closing area",()=>{var s=new[]{S(100,100,104,100),S(104,100,104,103),S(104,103,100,103)};var r=Build(s);Assert(r.UsesVirtualClosure);Near(r.Normals[0].Y,1);Near(r.Normals[1].X,-1);});
        Test("virtual opposite edge limits marker distance",()=>{var r=Build(new[]{S(0,.1,0,0),S(0,0,2,0),S(2,0,2,.1)});Assert(r.UsesVirtualClosure&&r.Normals.Length==3);Near(r.MarkerDistances[1],.05);});
        Test("virtual crossing keeps manual side fallback for simple original chain",()=>{var r=Build(new[]{S(0,0,4,0),S(4,0,4,4),S(4,4,0,4),S(0,4,0,1),S(0,1,3,1),S(3,1,3,3)});Assert(!r.IsClosed&&!r.UsesVirtualClosure);});
        Test("virtual edge overlapping first segment keeps manual side fallback",()=>{var r=Build(new[]{S(0,0,1,0),S(1,0,1,1),S(1,1,3,1),S(3,1,3,0)});Assert(!r.UsesVirtualClosure);});
        Test("straight open chain never invents an interior",()=>{var r=Build(new[]{S(10,10,11,10),S(11,10,12,10),S(12,10,13,10)});Assert(!r.IsClosed&&!r.UsesVirtualClosure);});
        Test("adjacent backtracking is rejected",()=>Throws(()=>Build(new[]{S(0,0,2,0),S(2,0,1,0)})));
        Test("branch is rejected",()=>Throws(()=>Build(new[]{S(0,0,1,0),S(1,0,2,0),S(1,0,1,1)})));
        Test("self intersection is rejected",()=>Throws(()=>Build(new[]{S(0,0,2,2),S(2,2,0,2),S(0,2,2,0),S(2,0,0,0)})));
        Test("disconnected closed contours must be separate groups",()=>Throws(()=>Build(Rect().Concat(Rect().Select(a=>new ContourSegment(a.Start+new ContourPoint(10,0),a.End+new ContourPoint(10,0)))).ToArray())));
        Test("zero length and invalid coordinates are rejected",()=>{Throws(()=>Build(new[]{S(0,0,0,0)}));Throws(()=>Build(new[]{S(0,0,double.NaN,1)}));});
        Test("measured marker rotation incl 180 degrees uses <=30 degree steps",()=>{
            foreach(var angle in new[]{0,45,90,135,170,180,225,270,315}){
                var (d,p,l)=Fixture();l.InsideNormal=new XYZ(Math.Cos(angle*Math.PI/180),Math.Sin(angle*Math.PI/180),0);
                var m=new ElevationMarkerService();var v=m.CreateElevationForLine(d,p,l,new ElementId(1),50,out var id,new List<string>());
                Assert(v!=null && id.IntegerValue>0 && m.IsDirectionAligned(v,l.InsideNormal));Assert(d.Rotations.All(a=>Math.Abs(a)<=Math.PI/6+1e-9));
            }
        });
        Test("regenerates before reading initially stale ViewDirection",()=>{
            var(d,p,l)=Fixture();d.InitialDirection=XYZ.Zero;d.OnRegenerate=x=>{if(x.Regenerations==1)x.CurrentMarker.View.ViewDirection=new XYZ(1,0,0);};
            Assert(new ElevationMarkerService().CreateElevationForLine(d,p,l,new ElementId(1),50,out _,new List<string>())!=null);
        });
        Test("corrects measured residual instead of blind PI",()=>{
            var(d,p,l)=Fixture();d.OnElevationRotate=(x,a)=>{if(x.Rotations.Count==1)x.CurrentMarker.View.ViewDirection=new XYZ(Math.Cos(a+.1),Math.Sin(a+.1),0);};
            var m=new ElevationMarkerService();var v=m.CreateElevationForLine(d,p,l,new ElementId(1),50,out _,new List<string>());Assert(m.IsDirectionAligned(v,l.InsideNormal));Assert(d.Rotations.Count>3);
        });
        Test("stubborn Revit direction rolls back marker and view",()=>{
            var(d,p,l)=Fixture();int count=d.Elements.Count;d.OnElevationRotate=(x,a)=>x.CurrentMarker.View.ViewDirection=new XYZ(1,0,0);var w=new List<string>();
            var v=new ElevationMarkerService().CreateElevationForLine(d,p,l,new ElementId(1),50,out var id,w);Assert(v==null && id.IntegerValue==-1 && w.Count==1 && d.Elements.Count==count);
        });
        Test("invalid direction leaves no orphan marker/view",()=>{var(d,p,l)=Fixture();int count=d.Elements.Count;l.InsideNormal=XYZ.Zero;Assert(new ElevationMarkerService().CreateElevationForLine(d,p,l,new ElementId(1),50,out _,new List<string>())==null);Assert(count==d.Elements.Count);});
        Test("preview reuses model family and displays exact planned numbers",()=>{var(d,p,l,s)=Preview();Assert(d.LoadCalls==0);Assert(Mark(d,s).LookupParameter("Номер помещения").AsString()=="А-101");Assert(Mark(d,s).LookupParameter("Номер вида").AsString()=="1-Р");Assert(!d.Elements.OfType<ViewSection>().Any());s.Validate(true);});
        Test("missing preview family is loaded only once",()=>{var(d,p,l)=Fixture(false);PluginFamilyResourceService.Paths=new List<string>{"SAB_Развертки_Направление.rfa"};var s=new ElevationDirectionPreviewService();s.Create(d,p,new[]{l});Assert(d.LoadCalls==1);s.DeleteMarkers();new ElevationDirectionPreviewService().Create(d,p,new[]{l});Assert(d.LoadCalls==1);});
        Test("side flip keeps preview tip on line and changes real facade draft",()=>{var(d,p,l,s)=Preview();s.Flip(new[]{s.Markers[0].MarkerId});Near(l.InsideNormal.Y,-1);Near(l.MarkerPoint.Y,-.25);Near(Mark(d,s).Location.Point.Y,0);Assert((-Mark(d,s).GetTransform().BasisY).DotProduct(-l.InsideNormal)>.9999);s.Validate(true);});
        Test("preview anchor is independent of configured real facade offset",()=>{
            foreach(double distance in new[]{0.0,.01,.25,20.0}){
                var(d,p,l)=Fixture();l.MarkerDistance=distance;l.MarkerPoint=l.MidPoint+l.InsideNormal*distance;var real=l.MarkerPoint;
                var s=new ElevationDirectionPreviewService();s.Create(d,p,new[]{l});Near(Mark(d,s).Location.Point.DistanceTo(l.MidPoint),0);Near(l.MarkerPoint.DistanceTo(real),0);s.Validate(true);
            }
        });
        Test("oblique lines and repeated flips keep the tip fixed on the line",()=>{
            foreach(double angle in new[]{0,35,90,180,225,270}){
                var(d,p,l)=Fixture();double a=angle*Math.PI/180;var tangent=new XYZ(Math.Cos(a),Math.Sin(a),0);l.EndPoint=tangent*4;l.MidPoint=l.EndPoint*.5;l.InsideNormal=new XYZ(-tangent.Y,tangent.X,0);l.MarkerPoint=l.MidPoint+l.InsideNormal*l.MarkerDistance;
                ((CurveElement)d.GetElement(l.LineElementId)).GeometryCurve=Line.CreateBound(l.StartPoint,l.EndPoint);
                var s=new ElevationDirectionPreviewService();s.Create(d,p,new[]{l});
                for(int turn=0;turn<4;turn++){s.Flip(new[]{s.Markers[0].MarkerId});Near(Mark(d,s).Location.Point.DistanceTo(l.MidPoint),0);Near(l.MarkerPoint.DistanceTo(l.MidPoint),l.MarkerDistance);s.Validate(true);}
                Assert(!d.Elements.OfType<ViewSection>().Any());
            }
        });
        Test("preview flip never moves the annotation insertion point",()=>{var(d,p,l,s)=Preview();d.FailMove=true;s.Flip(new[]{s.Markers[0].MarkerId});Assert(d.MoveCalls==0);Near(Mark(d,s).Location.Point.DistanceTo(l.MidPoint),0);});
        Test("final elevation uses the real offset after on-line preview flip",()=>{var(d,p,l,s)=Preview();s.Flip(new[]{s.Markers[0].MarkerId});s.DeleteMarkers();var service=new ElevationMarkerService();var view=service.CreateElevationForLine(d,p,l,new ElementId(1),50,out var id,new List<string>());Assert(view!=null);var actual=(ElevationMarker)d.GetElement(id);Near(actual.Point.DistanceTo(l.MarkerPoint),0);Near(actual.Point.DistanceTo(l.MidPoint),.25);Assert(actual.Point.Y<0);});
        Test("failed rotation restores both Revit pose and draft",()=>{var(d,p,l,s)=Preview();d.FailRotate=true;Throws(()=>s.Flip(new[]{s.Markers[0].MarkerId}));Near(l.InsideNormal.Y,1);Near(l.MarkerPoint.Y,.25);Near(Mark(d,s).Location.Point.Y,0);s.Validate(true);});
        Test("failed flip commit restores both Revit pose and draft",()=>{var(d,p,l,s)=Preview();double angle=Mark(d,s).Angle;d.FailCommit=true;Throws(()=>s.Flip(new[]{s.Markers[0].MarkerId}));Near(l.InsideNormal.Y,1);Near(l.MarkerPoint.Y,.25);Near(Mark(d,s).Location.Point.Y,0);Near(Mark(d,s).Angle,angle);});
        Test("cleanup deletes owned instances only and preserves family",()=>{var(d,p,l,s)=Preview();var unrelated=new FamilyInstance{Location=new LocationPoint{Point=XYZ.Zero}};d.Add(unrelated);s.DeleteMarkers();Assert(d.Elements.OfType<FamilyInstance>().Single()==unrelated);Assert(d.Elements.OfType<Family>().Any());});
        Test("failed cleanup keeps session recoverable",()=>{var(d,p,l,s)=Preview();d.FailDelete=true;Throws(s.DeleteMarkers);Assert(d.Elements.OfType<FamilyInstance>().Count()==1);d.FailDelete=false;s.DeleteMarkers();Assert(!d.Elements.OfType<FamilyInstance>().Any());});
        Test("deleted temporary mark is rejected",()=>{var(d,p,l,s)=Preview();d.Elements.Remove(Mark(d,s));Throws(()=>s.Validate(true));});
        Test("copied temporary mark is rejected and both copies cleaned",()=>{var(d,p,l,s)=Preview();var clone=new FamilyInstance{Location=new LocationPoint{Point=l.MarkerPoint},Entities=Mark(d,s).Entities};d.Add(clone);Throws(()=>s.Validate(true));s.DeleteMarkers();Assert(!d.Elements.OfType<FamilyInstance>().Any());});
        Test("manual marker move or rotation is rejected",()=>{var(d,p,l,s)=Preview();var f=Mark(d,s);f.Location.Point+=new XYZ(.01,0,0);Throws(()=>s.Validate(true));f.Location.Point=l.MidPoint;f.Angle+=Math.PI;Throws(()=>s.Validate(true));});
        Test("changed source line invalidates preview",()=>{var(d,p,l,s)=Preview();((Line)((CurveElement)d.GetElement(l.LineElementId)).GeometryCurve).End=new XYZ(5,0,0);Throws(()=>s.Validate(true));});
        Test("occupied confirmed number blocks creation",()=>{var(d,p,l,s)=Preview();var v=new Viewport();v.Number.Value=l.PlannedDetailNumber;d.Add(v);Throws(()=>s.Validate(true));s.Validate(false);});
        Test("edited preview labels cannot disagree with final numbering",()=>{var(d,p,l,s)=Preview();Mark(d,s).LookupParameter("Номер вида").Value="999";Throws(()=>s.Validate(true));});
        Test("missing family parameter rolls back partial preview",()=>{var(d,p,l)=Fixture();int count=d.Elements.Count;d.MissingParameter=true;Throws(()=>new ElevationDirectionPreviewService().Create(d,p,new[]{l}));Assert(d.Elements.Count==count);});
        Test("hidden annotation category blocks invisible preview",()=>{var(d,p,l)=Fixture();p.Hidden=true;Throws(()=>new ElevationDirectionPreviewService().Create(d,p,new[]{l}));Assert(!d.Elements.OfType<FamilyInstance>().Any());});
        Test("crop screen offsets remain correct with reversed endpoints and 180 degree frame",()=>{
            foreach(bool reversed in new[]{false,true})foreach(double sign in new[]{1.0,-1.0}){
                var(d,p,l)=Fixture();if(reversed)(l.StartPoint,l.EndPoint)=(l.EndPoint,l.StartPoint);
                var frame=new Transform{Mapping=v=>new XYZ(v.X*sign,v.Y,v.Z)};
                var view=new ViewSection{CropBox=new BoundingBoxXYZ{Transform=frame,Min=new XYZ(0,0,0),Max=new XYZ(1,1,1)}};d.Add(view);
                var cfg=new ElevationSettings{LeftOffsetMm=30.48,RightOffsetMm=91.44,TopOffsetMm=1000,ViewDepthMm=1000};
                Assert(new ElevationCropService().TryApplyCrop(view,l,cfg,new List<string>()));
                Near(view.CropBox.Min.X,Math.Min(0,4*sign)-.1);Near(view.CropBox.Max.X,Math.Max(0,4*sign)+.3);Assert(view.CropBox.Transform==frame);
            }
        });
        Test("crop far clip uses actual capped marker distance",()=>{var(d,p,l)=Fixture();l.MarkerPoint=l.MidPoint+XYZ.BasisY*.05;var v=new ViewSection{CropBox=new BoundingBoxXYZ{Transform=new Transform(),Min=XYZ.Zero,Max=new XYZ(1,1,1)}};d.Add(v);Assert(new ElevationCropService().TryApplyCrop(v,l,new ElevationSettings{TopOffsetMm=1000,ViewDepthMm=304.8},new List<string>()));Near(v.Far.AsDouble(),1.05);});
        LifecycleTests.Run(Test,Assert,Throws,Fixture);
        Console.WriteLine($"COMPLETED {passed} checks; actual linked production services with Revit API doubles; live Revit/RFA placement NOT RUN.");
    }
}
