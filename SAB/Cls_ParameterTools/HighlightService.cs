using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;

namespace SAB.ParameterTools
{
    internal sealed class HighlightService
    {
        private static readonly Guid StateGuid = new Guid("a46378e7-50a7-4c0e-b6a9-a1b59e369d7f");
        // Persist only properties this module changes; other user overrides remain untouched.
        public sealed class Original
        {
            public string ElementUniqueId { get; set; }
            public int ProjectionColor { get; set; }
            public int CutColor { get; set; }
            public int SurfaceColor { get; set; }
            public int CutFillColor { get; set; }
            public long SurfacePattern { get; set; }
            public long CutPattern { get; set; }
            public bool SurfaceVisible { get; set; }
            public bool CutVisible { get; set; }
            public bool Halftone { get; set; }
        }
        public sealed class State
        {
            public bool WasTemporary { get; set; }
            public List<Original> Elements { get; set; } = new List<Original>();
        }
        private static Schema Schema(bool create)
        {
            var schema = Autodesk.Revit.DB.ExtensibleStorage.Schema.Lookup(StateGuid);
            if (schema != null || !create) return schema;
            var builder = new SchemaBuilder(StateGuid);
            builder.SetSchemaName("SABParameterHighlightV1");
            builder.SetReadAccessLevel(AccessLevel.Public); builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("Json", typeof(string)); return builder.Finish();
        }
        private static int Pack(Color color)
        { return color != null && color.IsValid ? (color.Red << 16) | (color.Green << 8) | color.Blue : -1; }
        private static Color Unpack(int color)
        { return color < 0 ? Color.InvalidColorValue : new Color((byte)(color >> 16), (byte)(color >> 8), (byte)color); }
        private static ElementId Pattern(Document doc, long id)
        { return id < 0 || doc.GetElement(Ids.Create(id)) == null ? ElementId.InvalidElementId : Ids.Create(id); }
        internal int Restore(Document doc, ElementId viewId = null)
        {
            var schema = Schema(false); if (schema == null) return 0;
            var views = viewId == null
                ? new FilteredElementCollector(doc).OfClass(typeof(View)).WherePasses(new ExtensibleStorageFilter(StateGuid)).Cast<View>().ToList()
                : new List<View> { doc.GetElement(viewId) as View };
            int restored = 0;
            foreach (var view in views.Where(v => v != null))
            {
                var entity = view.GetEntity(schema); if (!entity.IsValid()) continue;
                var state = Storage.Json.Deserialize<State>(entity.Get<string>("Json"));
                if (state == null) throw new InvalidOperationException("Не удалось прочитать исходную графику вида «" + view.Name + "».");
                using (var t = new Transaction(doc, "SAB: снять подсветку параметров"))
                {
                    t.Start();
                    if (!state.WasTemporary || view.IsTemporaryViewPropertiesModeEnabled())
                        foreach (var original in state.Elements)
                        {
                            var element = doc.GetElement(original.ElementUniqueId); if (element == null) continue;
                            var graphics = new OverrideGraphicSettings(view.GetElementOverrides(element.Id));
                            graphics.SetProjectionLineColor(Unpack(original.ProjectionColor)).SetCutLineColor(Unpack(original.CutColor));
                            graphics.SetSurfaceForegroundPatternId(Pattern(doc, original.SurfacePattern)).SetSurfaceForegroundPatternColor(Unpack(original.SurfaceColor))
                                .SetSurfaceForegroundPatternVisible(original.SurfaceVisible);
                            graphics.SetCutForegroundPatternId(Pattern(doc, original.CutPattern)).SetCutForegroundPatternColor(Unpack(original.CutFillColor))
                                .SetCutForegroundPatternVisible(original.CutVisible).SetHalftone(original.Halftone);
                            view.SetElementOverrides(element.Id, graphics); restored++;
                        }
                    view.DeleteEntity(schema);
                    if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Исходная графика не восстановлена.");
                }
            }
            return restored;
        }
        internal void Clear(UIDocument ui)
        {
            var schema = Schema(false);
            bool hasSnapshot = schema != null && ui.ActiveView.GetEntity(schema).IsValid();
            int filters = ViewFilterService.Clear(ui);
            int restored = Restore(ui.Document);
            int legacy = hasSnapshot ? 0 : ClearLegacy(ui.Document, ui.ActiveView);
            ui.RefreshActiveView();
            UI.Toast.Show(ui.Application.MainWindowHandle, filters + restored + legacy > 0
                ? "Подсветка снята. Фильтров: " + filters + ". Элементов старой версии: " + (restored + legacy)
                : "Подсветка проверки на этом виде не найдена.");
        }
        private static bool Legacy(Document doc, OverrideGraphicSettings graphics)
        {
            const int oldRed = (235 << 16) | (45 << 8) | 45;
            var pattern = doc.GetElement(graphics.SurfaceForegroundPatternId) as FillPatternElement;
            return Pack(graphics.ProjectionLineColor) == oldRed && Pack(graphics.CutLineColor) == oldRed &&
                Pack(graphics.SurfaceForegroundPatternColor) == oldRed && Pack(graphics.CutForegroundPatternColor) == oldRed &&
                graphics.IsSurfaceForegroundPatternVisible && graphics.IsCutForegroundPatternVisible && !graphics.Halftone &&
                pattern != null && pattern.GetFillPattern().IsSolidFill && graphics.SurfaceForegroundPatternId == graphics.CutForegroundPatternId;
        }
        internal static int ClearLegacy(Document doc, View view)
        {
            if (!view.AreGraphicsOverridesAllowed()) return 0;
            var ids = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType().ToElementIds()
                .Where(id => Legacy(doc, view.GetElementOverrides(id))).ToList();
            if (ids.Count == 0) return 0;
            int found = ids.Count;
            using (var t = new Transaction(doc, "SAB: снять подсветку старой версии"))
            {
                t.Start();
                if (view.IsTemporaryViewPropertiesModeEnabled())
                {
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
                    ids = ids.Where(id => Legacy(doc, view.GetElementOverrides(id))).ToList();
                }
                foreach (var id in ids)
                {
                    var graphics = new OverrideGraphicSettings(view.GetElementOverrides(id));
                    graphics.SetProjectionLineColor(Color.InvalidColorValue).SetCutLineColor(Color.InvalidColorValue);
                    graphics.SetSurfaceForegroundPatternId(ElementId.InvalidElementId).SetSurfaceForegroundPatternColor(Color.InvalidColorValue);
                    graphics.SetCutForegroundPatternId(ElementId.InvalidElementId).SetCutForegroundPatternColor(Color.InvalidColorValue);
                    view.SetElementOverrides(id, graphics);
                }
                if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Не удалось снять старую подсветку.");
            }
            return found;
        }
        internal void Forget(Document doc) { }
    }
}
