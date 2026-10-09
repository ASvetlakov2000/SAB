using System;
using System.Diagnostics;
using System.IO;

namespace SAB.Instructions
{
    internal static class InstructionLauncher
    {
        internal const string IndexFile = "IDEOLOGIST_HTML_Instruktsii.html";
        internal const string ParametersFile = "IDEOLOGIST_HTML_Zapolnenie_parametrov.html";

        internal static string Resolve(string assemblyDirectory, string fileName)
        {
            if (fileName != IndexFile && fileName != ParametersFile)
                throw new ArgumentException("Неизвестная инструкция SAB.", nameof(fileName));
            string directory = Path.Combine(assemblyDirectory, "Docs", "PluginInstructions");
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                // A source checkout keeps the same documentation at repository level.
                var parent = new DirectoryInfo(assemblyDirectory);
                for (int depth = 0; parent != null && depth < 4; depth++, parent = parent.Parent)
                    if (File.Exists(Path.Combine(parent.FullName, "SAB.csproj")))
                    {
                        directory = Path.Combine(parent.Parent.FullName, "Docs", "PluginInstructions");
                        path = Path.Combine(directory, fileName);
                        break;
                    }
            }
            if (!File.Exists(path) || !File.Exists(Path.Combine(directory, "assets", "template.css")))
                throw new FileNotFoundException("Не найдены локальные HTML-инструкции или файл оформления. Повторно установите SAB для вашей версии Revit. Ожидаемая инструкция:\n" + path);
            return path;
        }

        internal static bool TryOpen(string fileName, out string error)
        {
            try
            {
                string path = Resolve(Path.GetDirectoryName(typeof(InstructionLauncher).Assembly.Location), fileName);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "Не удалось открыть инструкцию SAB.\n\n" + exception.Message
                    + "\n\nHTML открывается приложением по умолчанию Windows. Проверьте, что для файлов .html выбран браузер.";
                return false;
            }
        }
    }
}
