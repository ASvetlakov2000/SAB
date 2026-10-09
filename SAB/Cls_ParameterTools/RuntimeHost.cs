using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;

namespace SAB.ParameterTools
{
    internal sealed class RuntimeHost : IDisposable
    {
        internal static string LogFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SAB", "ParameterTools"); } }
        private readonly Dictionary<Document, string> _activeCorpora = new Dictionary<Document, string>();
        internal bool Busy { get; set; }
        internal HighlightService Highlight { get; } = new HighlightService();
        internal string CurrentCorpus(Document doc)
        { string value; return _activeCorpora.TryGetValue(doc, out value) ? value : null; }
        internal void SetCorpus(Document doc, string value) { _activeCorpora[doc] = value; }
        internal void Forget(Document doc) { _activeCorpora.Remove(doc); Highlight.Forget(doc); }
        internal void Log(Exception ex)
        {
            try
            {
                string folder = LogFolder;
                Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "errors.log"), DateTime.UtcNow.ToString("O") + " " + ex + Environment.NewLine);
            }
            catch { }
        }
        public void Dispose() { _activeCorpora.Clear(); }
    }
}
