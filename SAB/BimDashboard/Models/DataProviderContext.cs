using Autodesk.Revit.UI;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Контекст выполнения провайдера: доступ к Revit API и к пути файла.
    /// </summary>
    public class DataProviderContext
    {
        public UIApplication UiApplication { get; set; }

        public string FilePath { get; set; }
    }
}
