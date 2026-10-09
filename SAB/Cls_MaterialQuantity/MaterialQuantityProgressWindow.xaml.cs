using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace SAB.MaterialQuantity
{
    public partial class MaterialQuantityProgressWindow : Window
    {
        private bool _allowClose;

        internal MaterialQuantityProgressWindow()
        {
            _contentLoaded = true;
            SAB.Helpers.WpfComponentLoader.Load(this, "Cls_MaterialQuantity/MaterialQuantityProgressWindow.xaml");
            Closing += OnClosing;
        }

        internal void Report(MaterialQuantityProgressInfo progress)
        {
            if (progress == null)
            {
                return;
            }

            StageTextBlock.Text = string.IsNullOrWhiteSpace(progress.Stage)
                ? "Выполнение операции"
                : progress.Stage;
            DetailsTextBlock.Text = string.IsNullOrWhiteSpace(progress.Details)
                ? "Операция выполняется."
                : progress.Details;

            MainProgressBar.IsIndeterminate = progress.IsIndeterminate;
            if (progress.IsIndeterminate)
            {
                ItemsTextBlock.Text = "Операция выполняется";
                PercentTextBlock.Text = string.Empty;
            }
            else
            {
                int percentage = Math.Max(0, Math.Min(progress.OverallPercentage, 100));
                double percent = percentage;
                MainProgressBar.Value = percent;
                if (progress.TotalItems > 0)
                {
                    int total = Math.Max(1, progress.TotalItems);
                    int current = Math.Max(0, Math.Min(progress.CurrentItem, total));
                    ItemsTextBlock.Text = "Обработано " + current + " из " + total;
                }
                else
                {
                    ItemsTextBlock.Text = "Общий прогресс";
                }

                PercentTextBlock.Text = Math.Round(percent).ToString("0") + "%";
            }

            CountersTextBlock.Text =
                "Создано: " + progress.Created +
                "  ·  Обновлено: " + progress.Updated +
                "  ·  Проверить: " + progress.NeedsReview +
                "  ·  Удалено: " + progress.Obsolete;

            UpdateLayout();
            // Paint progress without pumping input inside a Revit transaction.
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));
        }

        internal void AllowCloseAndClose()
        {
            _allowClose = true;
            Close();
        }

        private void OnClosing(object sender, CancelEventArgs eventArgs)
        {
            if (!_allowClose)
            {
                eventArgs.Cancel = true;
            }
        }
    }
}
