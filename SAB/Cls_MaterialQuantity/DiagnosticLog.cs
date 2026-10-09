using System;
using System.IO;

namespace SAB.MaterialQuantity
{
    internal static class DiagnosticLog
    {
        internal static void Write(string version, string message)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SAB", "MaterialQuantity", version);
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "diagnostics.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never interrupt the Revit command.
            }
        }
    }
}
