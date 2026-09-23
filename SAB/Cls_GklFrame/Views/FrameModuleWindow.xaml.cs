using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using SAB.GklFrame.ViewModels;
using SAB.UI;

namespace SAB.GklFrame.Views
{
    public partial class FrameModuleWindow : Window
    {
        public FrameModuleWindow(FrameModuleViewModel viewModel)
        {
            InitializeWindowFromXamlFile();
            Title = viewModel.WindowTitle;
            DataContext = viewModel;
            viewModel.RequestClose = CloseWithResult;
            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OpenIcons8Attribution));
            SabWindowBehaviorService.Apply(this);
        }

        private void CloseWithResult(bool accepted)
        {
            DialogResult = accepted;
        }

        private static void OpenIcons8Attribution(object sender, RequestNavigateEventArgs args)
        {
            if (args.Uri != null && args.Uri.AbsoluteUri == "https://icons8.com/icon/l8SFbtVCyPBx/brick-wall")
            {
                Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
                args.Handled = true;
            }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(FrameModuleWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_GklFrame", "Views", "FrameModuleWindow.xaml");
            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна каркаса не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext context = new ParserContext
                {
                    BaseUri = new Uri(xamlPath, UriKind.Absolute)
                };
                Window loadedWindow = XamlReader.Load(stream, context) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить FrameModuleWindow.xaml.");
                }

                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
            }
        }
    }
}
