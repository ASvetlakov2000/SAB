using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Regulations
{
    /// <summary>
    /// Settings object for HTML instruction and regulation launcher.
    /// </summary>
    public class HtmlRegulationLaunchOptions
    {
        public HtmlRegulationLaunchOptions()
        {
            CandidateDirectories = new List<string>();
            SearchPattern = "*.html";
            StartFileNameContains = "IDEOLOGIST_HTML";
            IncludeSubdirectories = false;
        }

        // Блок отвечает за список папок-кандидатов.
        // Используется первая папка, где найден нужный стартовый HTML-файл.
        public List<string> CandidateDirectories { get; private set; }

        // Блок отвечает за маску файлов для поиска HTML.
        public string SearchPattern { get; set; }

        // Блок отвечает за имя или маркер стартового HTML-файла.
        public string StartFileNameContains { get; set; }

        // Блок отвечает за глубину поиска HTML-файлов.
        public bool IncludeSubdirectories { get; set; }

        public SearchOption GetSearchOption()
        {
            return IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        }
    }
}
