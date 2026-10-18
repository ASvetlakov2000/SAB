using System;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.Services
{
    internal sealed class CategoryCapabilities
    {
        public bool Visibility { get; private set; }
        public bool Projection { get; private set; }
        public bool Halftone { get; private set; }
        public bool Cut { get; private set; }
        public bool Surface { get; private set; }
        public bool Transparency { get; private set; }
        public bool Detail { get; private set; }

        static bool Probe(Func<bool> read) { try { return read(); } catch { return false; } }

        public static CategoryCapabilities Read(View view, ElementId categoryId, CategoryGraphicsGroup group)
        {
            try { return Read(view, Category.GetCategory(view.Document, categoryId), group); }
            catch { return new CategoryCapabilities(); }
        }

        public static CategoryCapabilities Read(View view, Category category, CategoryGraphicsGroup group)
        {
            if (view == null || category == null) return new CategoryCapabilities();
            return FromMetadata(
                Probe(() => view.AreGraphicsOverridesAllowed()),
                Probe(() => view.IsCategoryOverridable(category.Id)),
                Probe(() => category.get_AllowsVisibilityControl(view) && view.CanCategoryBeHidden(category.Id)),
                Probe(() => category.IsCuttable),
                Probe(() => category.Parent == null), group);
        }

        // Public API exposes common/cut capabilities. Surface/transparency remain the
        // existing SAB group policy; this is not a per-property Revit capability probe.
        internal static CategoryCapabilities FromMetadata(bool graphics, bool overridable, bool visibility,
            bool cuttable, bool root, CategoryGraphicsGroup group)
        {
            bool editable = graphics && overridable;
            bool model = group == CategoryGraphicsGroup.Model;
            bool analytical = group == CategoryGraphicsGroup.AnalyticalModel;
            return new CategoryCapabilities {
                Visibility = visibility,
                Projection = editable,
                Halftone = editable,
                Cut = editable && cuttable && (model || analytical),
                Surface = editable && group != CategoryGraphicsGroup.Annotation,
                Transparency = editable && (model || analytical),
                Detail = editable && model && root
            };
        }

        public void ApplyTo(CategoryOverrideRow row, bool intersect)
        {
            row.AllowsVisibilityControl = Visibility && (!intersect || row.AllowsVisibilityControl);
            row.SupportsProjectionLines = Projection && (!intersect || row.SupportsProjectionLines);
            row.SupportsHalftone = Halftone && (!intersect || row.SupportsHalftone);
            row.SupportsCut = Cut && (!intersect || row.SupportsCut);
            row.SupportsSurfacePatterns = Surface && (!intersect || row.SupportsSurfacePatterns);
            row.SupportsTransparency = Transparency && (!intersect || row.SupportsTransparency);
            row.SupportsDetailLevel = Detail && (!intersect || row.SupportsDetailLevel);
        }
    }
}
