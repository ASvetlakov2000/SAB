using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace SAB.ParameterTools
{
    internal sealed class RoomResult
    {
        internal Room Room;
        internal string Error;
    }
    internal sealed class RoomResolver
    {
        private readonly Phase _phase;
        private readonly List<Room> _rooms;
        internal RoomResolver(UIDocumentContext context)
        {
            _phase = context.Phase;
            _rooms = new FilteredElementCollector(context.Document).OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 1e-8 && _phase != null
                    && r.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == _phase.Id).ToList();
        }
        internal RoomResult Resolve(Element element)
        {
            try { return ResolveCore(element); }
            catch (Exception ex) { return new RoomResult { Error = "Не удалось определить помещение: " + ex.Message }; }
        }
        private RoomResult ResolveCore(Element element)
        {
            if (_phase == null) return new RoomResult { Error = "Не определена фаза активного вида." };
            var level = LevelService.ElementLevel(element);
            var candidates = _rooms.Where(r => level != null && r.LevelId == level.Id).ToList();
            if (candidates.Count == 0) return new RoomResult { Error = "На уровне элемента нет размещённых помещений нужной фазы." };
            var family = element as FamilyInstance;
            if (family != null)
            {
                bool doorOrWindow = Ids.Value(family.Category.Id) == (int)BuiltInCategory.OST_Doors
                    || Ids.Value(family.Category.Id) == (int)BuiltInCategory.OST_Windows;
                var native = (doorOrWindow ? new[] { family.get_FromRoom(_phase), family.get_ToRoom(_phase) }
                    : new[] { family.get_Room(_phase) })
                    .Where(r => r != null && candidates.Any(c => c.Id == r.Id)).GroupBy(r => r.UniqueId).Select(g => g.First()).ToList();
                if (native.Count > 1) return Ambiguous();
                if (doorOrWindow && native.Count == 1) return new RoomResult { Room = native[0] };
                XYZ calculationPoint = family.HasSpatialElementCalculationPoint ? family.GetSpatialElementCalculationPoint()
                    : (family.Location as LocationPoint)?.Point;
                if (calculationPoint != null)
                {
                    var exact = candidates.Where(r => r.IsPointInRoom(calculationPoint)).ToList();
                    return Result(exact);
                }
                if (native.Count == 1) return new RoomResult { Room = native[0] };
            }
            var point = element.Location as LocationPoint;
            if (point != null) return Result(Hits(candidates, new[] { point.Point }));
            var location = element.Location as LocationCurve;
            if (location != null)
            {
                var curve = location.Curve;
                var points = new[] { curve.Evaluate(.15, true), curve.Evaluate(.5, true), curve.Evaluate(.85, true) };
                var centerHits = Hits(candidates, points);
                if (centerHits.Count > 0) return Result(centerHits);
                var wall = element as Wall;
                if (wall == null) return NotFound();
                var sides = new List<XYZ>();
                double offset = wall.Width / 2 + UnitUtils.ConvertToInternalUnits(20, UnitTypeId.Millimeters);
                foreach (double t in new[] { .15, .5, .85 })
                {
                    XYZ mid = curve.Evaluate(t, true); var tangent = curve.ComputeDerivatives(t, true).BasisX;
                    var normal = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                    sides.Add(mid + normal * offset); sides.Add(mid - normal * offset);
                }
                return Result(Hits(candidates, sides));
            }
            // Surface-based elements are sampled on their actual faces rather than a bounding rectangle.
            if (element is Floor || element is Ceiling)
            {
                var samples = new List<XYZ>();
                var geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Medium });
                if (geometry != null) Collect(geometry, samples);
                return Result(Hits(candidates, samples));
            }
            return new RoomResult { Error = "Для этого элемента пока нет однозначного способа автоматического определения помещения." };
        }
        private static void Collect(GeometryElement geometry, IList<XYZ> samples)
        {
            foreach (GeometryObject obj in geometry)
            {
                var instance = obj as GeometryInstance;
                if (instance != null) { Collect(instance.GetInstanceGeometry(), samples); continue; }
                var solid = obj as Solid;
                if (solid == null) continue;
                foreach (Face face in solid.Faces)
                {
                    var planar = face as PlanarFace;
                    if (planar == null || Math.Abs(planar.FaceNormal.Z) < .5) continue;
                    var mesh = face.Triangulate();
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        var triangle = mesh.get_Triangle(i);
                        XYZ a = triangle.get_Vertex(0), b = triangle.get_Vertex(1), c = triangle.get_Vertex(2);
                        samples.Add((a + b + c) / 3);
                        // Move vertices slightly inward to avoid exact room-boundary hits.
                        samples.Add(a * .8 + b * .1 + c * .1);
                        samples.Add(b * .8 + a * .1 + c * .1);
                        samples.Add(c * .8 + a * .1 + b * .1);
                    }
                }
            }
        }
        private static List<Room> Hits(IEnumerable<Room> rooms, IEnumerable<XYZ> samples)
        {
            var points = samples.ToList(); var hits = new List<Room>();
            foreach (var room in rooms)
            {
                var bounds = room.get_BoundingBox(null);
                if (bounds == null) continue;
                // Evaluate XY at a valid interior height of each room, including offset levels.
                double z = (bounds.Min.Z + bounds.Max.Z) / 2;
                if (points.Any(p => p.X >= bounds.Min.X - 1e-5 && p.X <= bounds.Max.X + 1e-5
                    && p.Y >= bounds.Min.Y - 1e-5 && p.Y <= bounds.Max.Y + 1e-5
                    && room.IsPointInRoom(new XYZ(p.X, p.Y, z)))) hits.Add(room);
            }
            return hits;
        }
        private static RoomResult Result(IList<Room> hits)
        { return hits.Count == 1 ? new RoomResult { Room = hits[0] } : hits.Count > 1 ? Ambiguous() : NotFound(); }
        private static RoomResult Ambiguous()
        { return new RoomResult { Error = "Элемент относится к нескольким помещениям. Ускоренный режим не выбирает источник произвольно." }; }
        private static RoomResult NotFound()
        { return new RoomResult { Error = "Не удалось определить помещение элемента по модели." }; }
    }
    internal sealed class UIDocumentContext
    {
        internal Document Document;
        internal Phase Phase;
        internal static UIDocumentContext From(Autodesk.Revit.UI.UIDocument ui)
        {
            var phaseParameter = ui.ActiveView.get_Parameter(BuiltInParameter.VIEW_PHASE);
            return new UIDocumentContext { Document = ui.Document,
                Phase = phaseParameter == null ? null : ui.Document.GetElement(phaseParameter.AsElementId()) as Phase };
        }
    }
}
