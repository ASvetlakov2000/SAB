using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using SAB.InteriorElevations.Commands;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Sheets;
using SAB.InteriorElevations.Utils;
using SAB.Services.PluginResources;

namespace SAB.InteriorElevations.Services.Elevations
{
    public sealed class DirectionPreviewMarker
    {
        public ElementId MarkerId;
        public ElevationLineData Line;
    }

    public sealed class ElevationDirectionPreviewService
    {
        public const string FamilyName = "SAB_Развертки_Направление";
        private const string LegacyFamilyName = "SA_Развертки_Направление";
        private static readonly Guid SchemaId = new Guid("78464ACA-8092-4284-8967-177EAE5D7F5A");
        public string SessionId { get; private set; } = Guid.NewGuid().ToString("N");
        public IList<DirectionPreviewMarker> Markers { get; private set; } = new List<DirectionPreviewMarker>();
        public Document Document { get; private set; }
        public ViewPlan Plan { get; private set; }

        public void Create(Document document, ViewPlan plan, IList<ElevationLineData> lines)
        {
            Document = document; Plan = plan;
            Cleanup(document, null); // Recover owned annotations from an interrupted earlier session.
            using (var transaction = new Transaction(document, "SAB - показать направления развёрток"))
            {
                transaction.Start();
                FamilySymbol symbol = FindSymbol(document);
                if (symbol == null)
                {
                    string path = new PluginFamilyResourceService().GetCommandFamilyFilePaths(typeof(CreateInteriorElevationsCommand))
                        .FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), FamilyName, StringComparison.OrdinalIgnoreCase));
                    if (path == null) throw new InvalidOperationException("Не найдено семейство «" + FamilyName + "». Загрузите его в модель или проверьте ресурсы SAB.");
                    Family family;
                    if (!document.LoadFamily(path, out family)) throw new InvalidOperationException("Не удалось загрузить семейство предпросмотра.");
                    symbol = FindSymbol(document);
                }
                if (symbol == null || symbol.Category == null || symbol.Category.Id.IntegerValue != (int)BuiltInCategory.OST_GenericAnnotation)
                    throw new InvalidOperationException("Для предпросмотра требуется семейство категории «Аннотационные обозначения».");
                if (plan.GetCategoryHidden(new ElementId((int)BuiltInCategory.OST_GenericAnnotation)))
                    throw new InvalidOperationException("На текущем плане скрыты аннотационные обозначения. Включите их и повторите предпросмотр.");
                if (!symbol.IsActive) { symbol.Activate(); document.Regenerate(); }
                var schema = GetSchema(true);
                foreach (var line in lines)
                {
                    // Family origin is at the arrow tip. Preview stays on the source line;
                    // MarkerPoint remains the separately offset position of the real elevation.
                    var marker = document.Create.NewFamilyInstance(line.MidPoint, symbol, plan);
                    SetText(marker, "Номер помещения", line.RoomData != null ? line.RoomData.RoomNumber : string.Empty);
                    SetText(marker, "Номер вида", line.PlannedDetailNumber);
                    var entity = new Entity(schema);
                    entity.Set(schema.GetField("Session"), SessionId);
                    entity.Set(schema.GetField("SourceLine"), document.GetElement(line.LineElementId).UniqueId);
                    marker.SetEntity(entity);
                    Markers.Add(new DirectionPreviewMarker { MarkerId = marker.Id, Line = line });
                    document.Regenerate();
                    AlignMarker(document, marker, -line.InsideNormal);
                }
                if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit не подтвердил создание предпросмотра.");
            }
        }

        public bool Owns(Element element)
        {
            return element != null && element.IsValidObject && IsOwned(element, SessionId);
        }

        public bool IsSourceDocument(Document candidate)
        {
            // Distinct managed wrappers can represent the same open Revit document.
            return Document != null && Document.IsValidObject && candidate != null && candidate.IsValidObject && Document.Equals(candidate);
        }

        public void Flip(ICollection<ElementId> ids)
        {
            Validate(false);
            var selected = Markers.Where(record => ids.Any(id => RevitElementIdUtils.AreEqual(id, record.MarkerId))).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Выберите марки текущего предпросмотра.");
            var originals = selected.Select(record => new { Record = record, Normal = record.Line.InsideNormal, Point = record.Line.MarkerPoint }).ToList();
            try
            {
                using (var transaction = new Transaction(Document, "SAB - сменить сторону развёртки"))
                {
                    transaction.Start();
                    foreach (var record in selected)
                    {
                        var marker = Document.GetElement(record.MarkerId) as FamilyInstance;
                        record.Line.InsideNormal = -record.Line.InsideNormal;
                        record.Line.MarkerPoint = record.Line.MidPoint + record.Line.InsideNormal * record.Line.MarkerDistance;
                        // Rotate about the fixed insertion point at the source-line midpoint.
                        AlignMarker(Document, marker, -record.Line.InsideNormal);
                    }
                    if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit не подтвердил смену стороны.");
                }
            }
            catch
            {
                foreach (var item in originals) { item.Record.Line.InsideNormal = item.Normal; item.Record.Line.MarkerPoint = item.Point; }
                throw;
            }
        }

        public void Validate(bool checkNumbers)
        {
            if (Document == null || !Document.IsValidObject || Plan == null || !Plan.IsValidObject)
                throw new InvalidOperationException("Исходный документ или план закрыт.");
            if (FindOwned(Document, SessionId).Count != Markers.Count)
                throw new InvalidOperationException("Состав временных марок изменён. Отмените предпросмотр и запустите его заново.");
            var usedNumbers = checkNumbers ? new HashSet<string>(new FilteredElementCollector(Document).OfClass(typeof(Viewport))
                .Cast<Viewport>().Select(ElevationDetailNumberService.Read), StringComparer.OrdinalIgnoreCase) : null;
            foreach (var record in Markers)
            {
                var source = Document.GetElement(record.Line.LineElementId) as CurveElement;
                var curve = source != null ? source.GeometryCurve as Line : null;
                if (curve == null || curve.GetEndPoint(0).DistanceTo(record.Line.StartPoint) > 1e-6 ||
                    curve.GetEndPoint(1).DistanceTo(record.Line.EndPoint) > 1e-6)
                    throw new InvalidOperationException("Исходная линия изменена или удалена. Перестройте предпросмотр.");
                var marker = Document.GetElement(record.MarkerId) as FamilyInstance;
                if (marker == null || !Owns(marker)) throw new InvalidOperationException("Временная марка удалена или заменена.");
                if (marker.LookupParameter("Номер вида")?.AsString() != (record.Line.PlannedDetailNumber ?? string.Empty) ||
                    marker.LookupParameter("Номер помещения")?.AsString() != (record.Line.RoomData?.RoomNumber ?? string.Empty))
                    throw new InvalidOperationException("Подпись временной марки изменена. Перестройте предпросмотр для согласования номеров.");
                var location = marker.Location as LocationPoint;
                if (location == null || new XYZ(location.Point.X - record.Line.MidPoint.X, location.Point.Y - record.Line.MidPoint.Y, 0).GetLength() > 1e-5 ||
                    Horizontal(-marker.GetTransform().BasisY).DotProduct(Horizontal(-record.Line.InsideNormal)) < 0.9999)
                    throw new InvalidOperationException("Положение или поворот временной марки изменён вручную. Используйте «Сменить сторону» либо перестройте предпросмотр.");
                if (checkNumbers && usedNumbers.Contains(record.Line.PlannedDetailNumber))
                    throw new InvalidOperationException("Номер «" + record.Line.PlannedDetailNumber + "» уже занят. Перестройте предпросмотр для обновления номеров.");
            }
        }

        public void DeleteMarkers() { Cleanup(Document, SessionId); }

        public static void Cleanup(Document document, string sessionId)
        {
            if (document == null || !document.IsValidObject) return;
            var ids = FindOwned(document, sessionId);
            if (ids.Count == 0) return;
            using (var transaction = new Transaction(document, "SAB - удалить временные направления"))
            {
                transaction.Start(); document.Delete(ids);
                if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Не удалось удалить временные марки направлений.");
            }
        }

        private static List<ElementId> FindOwned(Document document, string sessionId)
        {
            if (GetSchema(false) == null) return new List<ElementId>();
            return new FilteredElementCollector(document).OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_GenericAnnotation).Where(element => IsOwned(element, sessionId))
                .Select(element => element.Id).ToList();
        }

        private static bool IsOwned(Element element, string sessionId)
        {
            var schema = GetSchema(false); if (schema == null) return false;
            var entity = element.GetEntity(schema);
            return entity.IsValid() && (sessionId == null || entity.Get<string>(schema.GetField("Session")) == sessionId);
        }

        private static Schema GetSchema(bool create)
        {
            var schema = Schema.Lookup(SchemaId); if (schema != null || !create) return schema;
            var builder = new SchemaBuilder(SchemaId); builder.SetSchemaName("SAB_ElevationDirectionPreview");
            builder.SetReadAccessLevel(AccessLevel.Public); builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("Session", typeof(string)); builder.AddSimpleField("SourceLine", typeof(string));
            return builder.Finish();
        }

        private static FamilySymbol FindSymbol(Document document)
        {
            return new FilteredElementCollector(document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Where(symbol => symbol.Family != null && (symbol.Family.Name == FamilyName || symbol.Family.Name == LegacyFamilyName))
                .OrderBy(symbol => symbol.Family.Name == FamilyName ? 0 : 1).ThenBy(symbol => symbol.Name, StringComparer.Ordinal).FirstOrDefault();
        }

        private static void SetText(FamilyInstance marker, string name, string value)
        {
            var parameter = marker.LookupParameter(name);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String || !parameter.Set(value ?? string.Empty))
                throw new InvalidOperationException("В семействе предпросмотра нужен доступный экземплярный текстовый параметр «" + name + "».");
        }

        private static XYZ Horizontal(XYZ vector)
        {
            var direction = new XYZ(vector.X, vector.Y, 0);
            if (direction.GetLength() < 1e-9) throw new InvalidOperationException("Некорректное направление марки предпросмотра.");
            return direction.Normalize();
        }

        private static void AlignMarker(Document document, FamilyInstance marker, XYZ target)
        {
            // In the supplied family the arrow points down in its unrotated local frame.
            XYZ current = Horizontal(-marker.GetTransform().BasisY), desired = Horizontal(target);
            double angle = Math.Atan2(current.CrossProduct(desired).Z, current.DotProduct(desired));
            var point = (marker.Location as LocationPoint).Point;
            if (Math.Abs(angle) > 1e-9) ElementTransformUtils.RotateElement(document, marker.Id, Line.CreateBound(point, point + XYZ.BasisZ), angle);
            document.Regenerate();
            if (Horizontal(-marker.GetTransform().BasisY).DotProduct(desired) < 0.9999)
                throw new InvalidOperationException("Не удалось подтвердить направление временной марки.");
        }
    }
}
