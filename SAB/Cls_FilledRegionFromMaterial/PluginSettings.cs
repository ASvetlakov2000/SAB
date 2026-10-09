using System.Collections.Generic;

namespace SAB.FilledRegionFromMaterial
{
    public sealed class PluginSettings
    {
        public PluginSettings()
        {
            TypeNamePrefix = "SAB_";
            IncludeUnusedMaterials = false;
            IncludePaintedMaterials = true;
            CopyForegroundPattern = true;
            CopyBackgroundPattern = true;
            CopyPatternColors = true;
            SkipMaterialsWithoutCutPattern = true;
            ParameterMappings = new List<ParameterMappingSetting>();
        }

        public string TypeNamePrefix { get; set; }
        public bool IncludeUnusedMaterials { get; set; }
        public bool IncludePaintedMaterials { get; set; }
        public bool CopyForegroundPattern { get; set; }
        public bool CopyBackgroundPattern { get; set; }
        public bool CopyPatternColors { get; set; }
        public bool SkipMaterialsWithoutCutPattern { get; set; }
        public List<ParameterMappingSetting> ParameterMappings { get; set; }
    }

    public sealed class ParameterMappingSetting
    {
        public string SourceKey { get; set; }
        public string TargetSelectionKey { get; set; }
    }
}
