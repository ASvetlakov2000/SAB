using System;
using System.Collections.ObjectModel;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.ViewModels
{
    public class GraphicOverrideEditorViewModel
    {
        public GraphicOverrideEditorViewModel(
            GraphicOverrideData data,
            GraphicOverrideEditorSection section,
            ObservableCollection<NamedIntegerOption> lineWeights,
            ObservableCollection<NamedElementOption> linePatterns,
            ObservableCollection<NamedElementOption> fillPatterns,
            ObservableCollection<NamedDetailLevelOption> detailLevels,
            bool supportsCut,
            bool supportsSurfacePatterns,
            bool supportsTransparency,
            bool supportsDetailLevel)
        {
            Data = data ?? throw new ArgumentNullException("data");
            Section = section;
            if (lineWeights == null) throw new ArgumentNullException("lineWeights");
            if (linePatterns == null) throw new ArgumentNullException("linePatterns");
            if (fillPatterns == null) throw new ArgumentNullException("fillPatterns");
            if (detailLevels == null) throw new ArgumentNullException("detailLevels");

            LineWeights = new ObservableCollection<NamedIntegerOption>();
            LinePatterns = new ObservableCollection<NamedElementOption>();
            FillPatterns = new ObservableCollection<NamedElementOption>();
            DetailLevels = new ObservableCollection<NamedDetailLevelOption>();

            if (Data.HasMixedValues)
            {
                LineWeights.Add(new NamedIntegerOption(GraphicOverrideData.MixedIntegerValue, "Разные значения"));
                LinePatterns.Add(new NamedElementOption(GraphicOverrideData.MixedIntegerValue, "Разные значения"));
                FillPatterns.Add(new NamedElementOption(GraphicOverrideData.MixedIntegerValue, "Разные значения"));
                DetailLevels.Add(new NamedDetailLevelOption(GraphicOverrideData.MixedDetailLevelValue, "Разные значения"));
            }

            CopyOptions(lineWeights, LineWeights);
            CopyOptions(linePatterns, LinePatterns);
            CopyOptions(fillPatterns, FillPatterns);
            CopyOptions(detailLevels, DetailLevels);
            SupportsCut = supportsCut;
            SupportsSurfacePatterns = supportsSurfacePatterns;
            SupportsTransparency = supportsTransparency;
            SupportsDetailLevel = supportsDetailLevel;
        }

        public GraphicOverrideData Data { get; private set; }

        public GraphicOverrideEditorSection Section { get; private set; }

        public string EditorTitle
        {
            get
            {
                switch (Section)
                {
                    case GraphicOverrideEditorSection.ProjectionLines:
                        return "Линии проекции/поверхности";
                    case GraphicOverrideEditorSection.SurfacePatterns:
                        return "Штриховки проекции/поверхности";
                    case GraphicOverrideEditorSection.Transparency:
                        return "Прозрачность поверхности";
                    case GraphicOverrideEditorSection.CutLines:
                        return "Линии сечения";
                    case GraphicOverrideEditorSection.CutPatterns:
                        return "Штриховки сечения";
                    default:
                        return "Переопределение графики";
                }
            }
        }

        public bool ShowProjectionLines
        {
            get { return Section == GraphicOverrideEditorSection.All || Section == GraphicOverrideEditorSection.ProjectionLines; }
        }

        public bool ShowSurfacePatterns
        {
            get { return Section == GraphicOverrideEditorSection.All || Section == GraphicOverrideEditorSection.SurfacePatterns; }
        }

        public bool ShowTransparency
        {
            get { return Section == GraphicOverrideEditorSection.All || Section == GraphicOverrideEditorSection.Transparency; }
        }

        public bool ShowCutLines
        {
            get { return Section == GraphicOverrideEditorSection.All || Section == GraphicOverrideEditorSection.CutLines; }
        }

        public bool ShowCutPatterns
        {
            get { return Section == GraphicOverrideEditorSection.All || Section == GraphicOverrideEditorSection.CutPatterns; }
        }

        public bool ShowAdditional
        {
            get { return Section == GraphicOverrideEditorSection.All; }
        }

        public ObservableCollection<NamedIntegerOption> LineWeights { get; private set; }

        public ObservableCollection<NamedElementOption> LinePatterns { get; private set; }

        public ObservableCollection<NamedElementOption> FillPatterns { get; private set; }

        public ObservableCollection<NamedDetailLevelOption> DetailLevels { get; private set; }

        public bool SupportsCut { get; private set; }

        public bool SupportsSurfacePatterns { get; private set; }

        public bool SupportsTransparency { get; private set; }

        public bool SupportsDetailLevel { get; private set; }

        private static void CopyOptions<T>(ObservableCollection<T> source, ObservableCollection<T> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }
    }
}
