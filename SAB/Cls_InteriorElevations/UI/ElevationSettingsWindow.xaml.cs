using System.Windows;
using InteriorElevations.Models;

namespace InteriorElevations.UI
{
    public partial class ElevationSettingsWindow : Window
    {
        public ElevationSettings Settings { get; private set; }

        public ElevationSettingsWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Settings = new ElevationSettings
            {
                CropOffsetTop = double.TryParse(TopOffsetTextBox.Text, out double top) ? top : 2500,
                CropOffsetBottom = double.TryParse(BottomOffsetTextBox.Text, out double bottom) ? bottom : 0,
                CropOffsetSide = double.TryParse(SideOffsetTextBox.Text, out double side) ? side : 100,
                CropOffsetLine = double.TryParse(LineOffsetTextBox.Text, out double line) ? line : 150,
                ViewNameFormat = (ViewNameTemplateComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString()
                    ?? "Развертка пом {0}_{1}-{2}"
            };
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}