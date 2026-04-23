using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Regulations
{
    /// <summary>
    /// Settings object for HTML regulation launcher.
    /// </summary>
    public class HtmlRegulationLaunchOptions
    {
        public HtmlRegulationLaunchOptions()
        {
            CandidateDirectories = new List<string>();
            SearchPattern = "*.html";
            StartFileNameContains = "index";
            IncludeSubdirectories = false;
        }

        // Блок отвечает за список папок-кандидатов.
        // Здесь можно вручную менять порядок путей: используется первая существующая папка.
        public List<string> CandidateDirectories { get; }

        // Блок отвечает за маску файлов для поиска HTML.
        public string SearchPattern { get; set; }

        // Блок отвечает за признак стартового файла (обычно "index").
        public string StartFileNameContains { get; set; }

        // Блок отвечает за глубину поиска HTML-файлов.
        public bool IncludeSubdirectories { get; set; }

        public SearchOption GetSearchOption()
        {
            return IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        }
    }
}
