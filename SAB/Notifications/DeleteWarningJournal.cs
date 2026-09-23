using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SAB.Notifications
{
    // This legacy dialog does not expose Message. Revit writes its text before raising DialogBoxShowing.
    public static class DeleteWarningJournal
    {
        public static string Read(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(Math.Max(0, stream.Length - 65536), SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream, Encoding.Default, true)) return Parse(reader.ReadToEnd());
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        public static string Parse(string tail)
        {
            int start = tail.LastIndexOf("Error dialog summary", StringComparison.Ordinal);
            if (start < 0) return null;
            string section = tail.Substring(start);
            // Never reuse a summary belonging to an already completed dialog or a different command.
            if (section.Contains("ADialog::doModal stop") || section.Contains("Jrn.Command") ||
                !section.Contains("0 failures, 0 errors, 1 warnings")) return null;
            var match = Regex.Match(section, @"(?:Предупреждение|Warning):\s*(?<text>.*?)\r?\n\s*'\s*- 1 times", RegexOptions.Singleline);
            if (!match.Success) return null;
            string text = Regex.Replace(match.Groups["text"].Value, @"(?m)^\s*'", "").Trim();
            if (!text.StartsWith("Помимо выбранных элементов будут удалены", StringComparison.Ordinal) &&
                !text.StartsWith("In addition to the selected elements", StringComparison.Ordinal)) return null;
            return text;
        }
    }
}
