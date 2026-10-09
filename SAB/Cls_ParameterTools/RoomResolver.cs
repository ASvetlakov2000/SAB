using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.ParameterTools.Core;

namespace SAB.ParameterTools
{
    internal sealed class RoomResult
    {
        internal Room Room;
        internal string Error;
        internal string Technical;
        internal RoomResolutionStatus Status;
    }

    // Read-only session: all analysis must finish before document transactions start.
    internal sealed class RoomResolver : IDisposable
    {
        private readonly Document _document;
        private readonly Phase _phase;
        private readonly DoorRoomSide _doorSide;
        private readonly bool _volumes;
        private readonly List<RoomData> _rooms;
        private readonly SpatialElementGeometryCalculator _calculator;
        private readonly Dictionary<ElementId, List<Solid>> _obstacles = new Dictionary<ElementId, List<Solid>>();
        private readonly double _probe = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Millimeters);
        private const int MaxSamples = 10000;
        private sealed class RoomData
        {
            internal Room Room;
            internal BoundingBoxXYZ Bounds;
            internal SpatialElementGeometryResults Results;
            internal Solid Solid;
            internal readonly HashSet<ElementId> Sides = new HashSet<ElementId>();
            internal readonly HashSet<ElementId> Tops = new HashSet<ElementId>();
            internal bool Loaded;
            internal string Error;
        }
        private sealed class Probe { internal XYZ Origin, Point, Normal; }

        internal RoomResolver(UIDocumentContext context, DoorRoomSide doorSide = DoorRoomSide.RequireUnique)
        {
            _document = context.Document; _phase = context.Phase; _doorSide = doorSide;
            _volumes = AreaVolumeSettings.GetAreaVolumeSettings(_document).ComputeVolumes;
            _rooms = new FilteredElementCollector(_document).OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 1e-8 && _phase != null
                    && r.get_Parameter(BuiltInParameter.ROOM_PHASE)?.AsElementId() == _phase.Id)
                .Select(r => new RoomData { Room = r, Bounds = r.get_BoundingBox(null) }).ToList();
            _calculator = new SpatialElementGeometryCalculator(_document, new SpatialElementBoundaryOptions
                { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish });
        }
        public void Dispose()
        {
            foreach (var room in _rooms) room.Results?.Dispose();
            _calculator.Dispose();
        }
        internal RoomResult Resolve(Element element)
        {
            var trace = new StringBuilder();
            trace.AppendLine("Элемент: " + Ids.Value(element.Id) + " / " + element.UniqueId);
            trace.AppendLine("Категория: " + element.Category?.Name + "; группа: " + Ids.Value(element.GroupId));
            trace.AppendLine("Фаза: " + (_phase == null ? "не определена" : _phase.Name + " / " + Ids.Value(_phase.Id)));
            trace.AppendLine("Расчёт объёмов: " + (_volumes ? "включён" : "выключен") + "; помещения: текущий документ.");
            try { return ResolveCore(element, trace); }
            catch (Exception ex)
            {
                trace.AppendLine(ex.ToString());
                return Finish(RoomResolutionStatus.Error, "Ошибка анализа помещения: " + ex.Message + ". Выберите помещение вручную.", null, trace);
            }
        }
        private RoomResult ResolveCore(Element element, StringBuilder trace)
        {
            if (_phase == null) return Finish(RoomResolutionStatus.Insufficient, "На активном виде не задана фаза. Укажите фазу вида или используйте план с нужной фазой.", null, trace);
            var rooms = _rooms.Where(r => CompatibleOptions(element, r.Room)).ToList();
            trace.AppendLine("Размещённых помещений фазы: " + _rooms.Count + "; совместимых по варианту: " + rooms.Count);
            if (rooms.Count == 0) return Finish(RoomResolutionStatus.NotFound,
                "Нет размещённых помещений нужной фазы и варианта. Проверьте фазу и наличие помещений в текущем документе; помещения из связей пока не поддерживаются.", null, trace);
            if (element is Dimension || element.ViewSpecific) return Finish(RoomResolutionStatus.Unsupported,
                "У аннотации нет однозначной пространственной принадлежности. Выберите помещение вручную.", null, trace);
            if (!ExistsInPhase(element)) return Finish(RoomResolutionStatus.NotFound,
                "Элемент не существует в фазе активного вида (ещё не создан или уже снесён). Проверьте фазу вида.", null, trace);
            var family = element as FamilyInstance;
            if (family != null) return Family(family, rooms, trace);
            if (element is Wall || element is Floor || element is Ceiling || element is RoofBase) return Surface(element, rooms, trace);
            var point = element.Location as LocationPoint;
            if (point != null) return Result(PointHits(rooms, new[] { point.Point }, trace), trace,
                "Точка размещения не попала ни в одно помещение на своей фактической высоте.");
            return Finish(RoomResolutionStatus.Unsupported, "Для этой категории автоматическое определение не поддерживается. Выберите помещение вручную.", null, trace);
        }
        private RoomResult Family(FamilyInstance family, List<RoomData> rooms, StringBuilder trace)
        {
            bool opening = family.Category != null && (Ids.Value(family.Category.Id) == (int)BuiltInCategory.OST_Doors
                || Ids.Value(family.Category.Id) == (int)BuiltInCategory.OST_Windows);
            if (opening)
            {
                Room from = family.get_FromRoom(_phase), to = family.get_ToRoom(_phase);
                trace.AppendLine("Метод: FromRoom / ToRoom. Политика: " + _doorSide);
                trace.AppendLine("Из: " + Label(from) + "; в: " + Label(to));
                var points = family.HasSpatialElementFromToCalculationPoints
                    ? family.GetSpatialElementFromToCalculationPoints().ToList() : new List<XYZ>();
                if (_doorSide != DoorRoomSide.RequireUnique)
                {
                    int index = _doorSide == DoorRoomSide.FromRoom ? 0 : 1;
                    Room chosen = index == 0 ? from : to;
                    return FamilyResult(Allowed(rooms, new[] { chosen }), points.Count == 2
                        ? PointHits(rooms, new[] { points[index] }, trace) : new List<Room>(), points.Count == 2, trace);
                }
                return FamilyResult(Allowed(rooms, new[] { from, to }), PointHits(rooms, points, trace), false, trace);
            }
            trace.AppendLine("Метод: Room + точка расчёта / размещения семейства.");
            var native = Allowed(rooms, new[] { family.get_Room(_phase) });
            bool explicitPoint = family.HasSpatialElementCalculationPoint;
            XYZ point = explicitPoint ? family.GetSpatialElementCalculationPoint() : (family.Location as LocationPoint)?.Point;
            trace.AppendLine("Штатный Room: " + string.Join("; ", native.Select(Label)) + "; расчётная точка: " + explicitPoint);
            return FamilyResult(native, PointHits(rooms, point == null ? new XYZ[0] : new[] { point }, trace), explicitPoint, trace);
        }
        private RoomResult FamilyResult(List<Room> native, List<Room> points, bool explicitPoint, StringBuilder trace)
        {
            string[] ids;
            var status = RoomEvidence.Family(native.Select(r => r.UniqueId), points.Select(r => r.UniqueId), explicitPoint, out ids);
            var hits = native.Concat(points).GroupBy(r => r.UniqueId).Select(g => g.First()).ToList();
            if (status == RoomResolutionStatus.Insufficient) return Finish(status,
                "Штатная связь с помещением есть, но расчётная точка в него не попала. Проверьте точку расчёта семейства или выберите помещение вручную.", hits, trace);
            return Result(hits, trace, "Помещение не найдено по штатной связи и фактическим точкам семейства. Проверьте точки расчёта, фазу и сторону двери/окна либо выберите помещение вручную.");
        }
        private RoomResult Surface(Element element, List<RoomData> rooms, StringBuilder trace)
        {
            if (!_volumes) return Finish(RoomResolutionStatus.Insufficient,
                "Для автоматической проверки стен, перекрытий, потолков и крыш включите расчёт площадей и объёмов помещений в Revit либо выберите помещение вручную. SAB эту настройку не меняет.", null, trace);
            var bounds = element.get_BoundingBox(null);
            if (bounds == null) return Finish(RoomResolutionStatus.Insufficient, "Не удалось получить габариты элемента. Выберите помещение вручную.", null, trace);
            // LevelId is not an exclusion: all contact checks use actual world XYZ.
            var nearby = rooms.Where(r => r.Bounds == null || Overlap(bounds, r.Bounds, _probe)).ToList();
            trace.AppendLine("Метод: реальные границы, пересечение объёмов и контакты граней; помещений в 3D-габаритах: " + nearby.Count);
            var geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
            var targetSolids = new List<Solid>(); CollectSolids(geometry, targetSolids);
            var hits = new List<Room>();
            foreach (var room in nearby)
            {
                Load(room);
                if (room.Error != null) { trace.AppendLine(Label(room.Room) + ": " + room.Error); continue; }
                var boundaryIds = element is Wall ? room.Sides : room.Tops;
                if (!(element is Floor) && boundaryIds.Contains(element.Id)) hits.Add(room.Room);
                // Non-room-bounding finish floors/walls may be inside the room rather than on its boundary.
                foreach (var solid in targetSolids)
                    using (var overlap = BooleanOperationsUtils.ExecuteBooleanOperation(solid, room.Solid, BooleanOperationsType.Intersect))
                        if (overlap.Volume > 1e-9) {
                            hits.Add(room.Room); trace.AppendLine(Label(room.Room) + ": физическое пересечение объёмов (фут³) " + overlap.Volume.ToString("G", CultureInfo.InvariantCulture)); break;
                        }
            }
            if (nearby.Any(r => r.Error != null)) return Finish(RoomResolutionStatus.Insufficient,
                "Не рассчитаны границы части помещений рядом с элементом. Однозначность не подтверждена; подробности в отчёте. Выберите помещение вручную.", hits, trace);
            var probes = new List<Probe>(); var faces = new List<Face>();
            CollectProbes(geometry, element, probes, faces);
            trace.AppendLine("Точек на гранях: " + probes.Count + "; длина проверки: 2 мм.");
            if (probes.Count > MaxSamples) return Finish(RoomResolutionStatus.Insufficient,
                "Геометрия слишком сложная для полного автоматического анализа. Выберите помещение вручную.", hits, trace);
            if (probes.Count == 0 && hits.Count == 0) return Finish(RoomResolutionStatus.Insufficient,
                "Не получены пригодные поверхности элемента для проверки контакта. Выберите помещение вручную.", null, trace);
            var blockers = Blockers(element, bounds);
            bool blocked = false;
            foreach (var room in nearby.Where(r => r.Solid != null))
            {
                var reverse = ContactProbes(room.Solid, faces);
                if (reverse.Count > MaxSamples) return Finish(RoomResolutionStatus.Insufficient,
                    "Геометрия контактов слишком сложная для полного анализа. Выберите помещение вручную.", hits, trace);
                trace.AppendLine(Label(room.Room) + ": обратных точек контакта " + reverse.Count);
                foreach (var probe in probes.Concat(reverse))
                {
                    if (!Inside(room.Bounds, probe.Point) || !room.Room.IsPointInRoom(probe.Point) || !Touches(room.Solid, probe)) continue;
                    var obstacle = FirstBlocker(blockers, probe);
                    if (obstacle != null)
                    {
                        blocked = true; trace.AppendLine("Препятствие / неподдерживаемая связь " + Ids.Value(obstacle.Id) + " перед " + Label(room.Room));
                        break;
                    }
                    hits.Add(room.Room); break;
                }
            }
            foreach (var probe in probes.Take(12)) trace.AppendLine("Проверка XYZ (футы): " + Coordinates(probe.Origin) + " → " + Coordinates(probe.Point));
            if (blocked) return Finish(RoomResolutionStatus.Blocked,
                "Между поверхностью и помещением есть другая конструкция либо связанная модель, геометрия которой не проверена. Выберите помещение вручную; ID приведён в подробностях.", hits, trace);
            return Result(hits, trace, "На фактической высоте элемента не найден контакт с помещением. Проверьте смещения, границы и фазу либо выберите помещение вручную.");
        }
        private void Load(RoomData room)
        {
            if (room.Loaded) return; room.Loaded = true;
            try
            {
                room.Results = _calculator.CalculateSpatialElementGeometry(room.Room);
                room.Solid = room.Results.GetGeometry();
                foreach (Face face in room.Solid.Faces)
                    foreach (var sub in room.Results.GetBoundaryFaceInfo(face))
                    {
                        var id = sub.SpatialBoundaryElement.HostElementId;
                        if (id == ElementId.InvalidElementId) continue;
                        if (sub.SubfaceType == SubfaceType.Side) room.Sides.Add(id);
                        if (sub.SubfaceType == SubfaceType.Top) room.Tops.Add(id);
                    }
            }
            catch (Exception ex) { room.Error = ex.ToString(); }
        }
        private void CollectProbes(GeometryElement geometry, Element element, List<Probe> probes, List<Face> faces)
        {
            if (geometry == null) return;
            foreach (GeometryObject obj in geometry)
            {
                if (probes.Count > MaxSamples) return;
                var instance = obj as GeometryInstance;
                if (instance != null) { CollectProbes(instance.GetInstanceGeometry(), element, probes, faces); continue; }
                var solid = obj as Solid;
                if (solid == null || solid.Volume < 1e-9) continue;
                foreach (Face face in solid.Faces)
                {
                    var mesh = face.Triangulate();
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        var tri = mesh.get_Triangle(i);
                        XYZ a = tri.get_Vertex(0), b = tri.get_Vertex(1), c = tri.get_Vertex(2);
                        foreach (XYZ point in new[] { (a + b + c) / 3, a * .8 + b * .1 + c * .1, b * .8 + a * .1 + c * .1, c * .8 + a * .1 + b * .1 })
                        {
                            var projected = face.Project(point);
                            if (projected == null || !face.IsInside(projected.UVPoint)) continue;
                            XYZ normal = face.ComputeNormal(projected.UVPoint).Normalize();
                            if (element is Wall ? Math.Abs(normal.Z) > .5 : element is Floor ? normal.Z < .5 : normal.Z > -.5) continue;
                            if (!faces.Contains(face)) faces.Add(face);
                            XYZ origin = projected.XYZPoint;
                            probes.Add(new Probe { Origin = origin, Normal = normal, Point = origin + normal * _probe });
                            if (probes.Count > MaxSamples) return;
                        }
                    }
                }
            }
        }
        private bool Touches(Solid room, Probe probe)
        {
            foreach (Face face in room.Faces)
            {
                var projection = face.Project(probe.Origin);
                if (projection != null && projection.Distance <= _probe && face.IsInside(projection.UVPoint)
                    && face.ComputeNormal(projection.UVPoint).DotProduct(probe.Normal) < -.9) return true;
            }
            return false;
        }
        // Also sample room faces: a small room must not disappear between samples of a large slab.
        private List<Probe> ContactProbes(Solid room, List<Face> targets)
        {
            var probes = new List<Probe>();
            foreach (Face face in room.Faces)
            {
                var mesh = face.Triangulate();
                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    var t = mesh.get_Triangle(i);
                    XYZ a = t.get_Vertex(0), b = t.get_Vertex(1), c = t.get_Vertex(2);
                    foreach (var point in new[] { (a + b + c) / 3, a * .8 + b * .1 + c * .1, b * .8 + a * .1 + c * .1, c * .8 + a * .1 + b * .1 })
                    {
                        var onRoom = face.Project(point);
                        if (onRoom == null || !face.IsInside(onRoom.UVPoint)) continue;
                        XYZ outward = face.ComputeNormal(onRoom.UVPoint);
                        foreach (Face target in targets)
                        {
                            var projection = target.Project(onRoom.XYZPoint);
                            if (projection == null || projection.Distance > _probe || !target.IsInside(projection.UVPoint)) continue;
                            XYZ normal = target.ComputeNormal(projection.UVPoint).Normalize();
                            if (normal.DotProduct(outward) > -.9) continue;
                            probes.Add(new Probe { Origin = projection.XYZPoint, Normal = normal, Point = projection.XYZPoint + normal * _probe });
                            if (probes.Count > MaxSamples) return probes;
                        }
                    }
                }
            }
            return probes;
        }
        private List<Element> Blockers(Element target, BoundingBoxXYZ bounds)
        {
            XYZ pad = new XYZ(_probe, _probe, _probe);
            return new FilteredElementCollector(_document).WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(new Outline(bounds.Min - pad, bounds.Max + pad)))
                .Where(e => e.Id != target.Id && (e is Wall || e is Floor || e is Ceiling || e is RoofBase || e is RevitLinkInstance)
                    && CompatibleOptions(target, e) && ExistsInPhase(e)).ToList();
        }
        private Element FirstBlocker(List<Element> blockers, Probe probe)
        {
            foreach (var element in blockers)
            {
                // Do not pretend an unanalysed link is transparent.
                if (element is RevitLinkInstance)
                {
                    if (Inside(element.get_BoundingBox(null), probe.Point)) return element;
                    continue;
                }
                List<Solid> solids;
                if (!_obstacles.TryGetValue(element.Id, out solids))
                {
                    solids = new List<Solid>(); CollectSolids(element.get_Geometry(new Options()), solids);
                    _obstacles.Add(element.Id, solids);
                }
                using (var line = Line.CreateBound(probe.Origin, probe.Point))
                using (var options = new SolidCurveIntersectionOptions())
                    foreach (var solid in solids)
                        using (var intersection = solid.IntersectWithCurve(line, options))
                            if (intersection.SegmentCount > 0) return element;
            }
            return null;
        }
        private static void CollectSolids(GeometryElement geometry, List<Solid> solids)
        {
            if (geometry == null) return;
            foreach (GeometryObject obj in geometry)
            {
                var instance = obj as GeometryInstance;
                if (instance != null) CollectSolids(instance.GetInstanceGeometry(), solids);
                var solid = obj as Solid;
                if (solid != null && solid.Volume > 1e-9) solids.Add(solid);
            }
        }
        private bool ExistsInPhase(Element e)
        {
            if (!e.HasPhases()) return true;
            var status = e.GetPhaseStatus(_phase.Id);
            return status == ElementOnPhaseStatus.Existing || status == ElementOnPhaseStatus.New;
        }
        private List<Room> PointHits(List<RoomData> rooms, IEnumerable<XYZ> samples, StringBuilder trace)
        {
            var points = samples.ToList();
            foreach (var point in points) trace.AppendLine("Точка XYZ (футы): " + Coordinates(point));
            return rooms.Where(r => points.Any(p => Inside(r.Bounds, p) && r.Room.IsPointInRoom(p))).Select(r => r.Room).ToList();
        }
        private static List<Room> Allowed(List<RoomData> rooms, IEnumerable<Room> native)
        { return native.Where(r => r != null && rooms.Any(c => c.Room.Id == r.Id)).GroupBy(r => r.UniqueId).Select(g => g.First()).ToList(); }
        private static bool CompatibleOptions(Element element, Element candidate)
        {
            var option = element.DesignOption; var other = candidate.DesignOption;
            return other == null || option != null && option.Id == other.Id;
        }
        private static bool Overlap(BoundingBoxXYZ a, BoundingBoxXYZ b, double pad)
        { return a.Min.X <= b.Max.X + pad && a.Max.X >= b.Min.X - pad && a.Min.Y <= b.Max.Y + pad && a.Max.Y >= b.Min.Y - pad && a.Min.Z <= b.Max.Z + pad && a.Max.Z >= b.Min.Z - pad; }
        private static bool Inside(BoundingBoxXYZ box, XYZ p)
        { return box == null || p.X >= box.Min.X - 1e-7 && p.X <= box.Max.X + 1e-7 && p.Y >= box.Min.Y - 1e-7 && p.Y <= box.Max.Y + 1e-7 && p.Z >= box.Min.Z - 1e-7 && p.Z <= box.Max.Z + 1e-7; }
        private static string Coordinates(XYZ p)
        { return string.Format(CultureInfo.InvariantCulture, "({0:F5}; {1:F5}; {2:F5})", p.X, p.Y, p.Z); }
        private static string Label(Room room)
        { return room == null ? "нет" : room.Number + " «" + room.Name + "» [ID " + Ids.Value(room.Id) + "]"; }
        private static RoomResult Result(IEnumerable<Room> rooms, StringBuilder trace, string missing)
        {
            var hits = rooms.GroupBy(r => r.UniqueId).Select(g => g.First()).ToList();
            return Finish(hits.Count == 1 ? RoomResolutionStatus.Found : hits.Count > 1 ? RoomResolutionStatus.Ambiguous : RoomResolutionStatus.NotFound,
                hits.Count > 1 ? "Найдено несколько помещений: " + string.Join("; ", hits.Take(8).Select(Label)) + ". Выберите помещение вручную или задайте сторону двери/окна."
                    : hits.Count == 0 ? missing : null, hits, trace);
        }
        private static RoomResult Finish(RoomResolutionStatus status, string error, IEnumerable<Room> rooms, StringBuilder trace)
        {
            var hits = (rooms ?? Enumerable.Empty<Room>()).GroupBy(r => r.UniqueId).Select(g => g.First()).ToList();
            trace.AppendLine("Результат: " + status + "; кандидаты: " + string.Join("; ", hits.Select(Label)));
            return new RoomResult { Status = status, Room = status == RoomResolutionStatus.Found ? hits.Single() : null,
                Error = error, Technical = trace.ToString() };
        }
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
