using System.IO;

namespace SAB.MaterialQuantity
{
    internal static class SabSharedParameterFile
    {
        internal const string FileName = "SAB_ОбщиеПараметры.txt";

        internal static string GetPath()
        {
            string assemblyDirectory = Path.GetDirectoryName(
                typeof(SabSharedParameterFile).Assembly.Location);
            string path = Path.Combine(
                assemblyDirectory ?? string.Empty,
                "SharedParameters",
                FileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Не найден единый файл общих параметров SAB: " + path,
                    path);
            }

            return path;
        }
    }
}
