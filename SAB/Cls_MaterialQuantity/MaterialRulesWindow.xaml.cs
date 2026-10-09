using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SAB.MaterialQuantity
{
    public partial class MaterialRulesWindow : Window
    {
        private readonly MaterialRulesViewModel _model;
        private readonly Func<int> _saveRules;
        private bool _isBusy;
        private readonly string _layoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SAB", "MaterialQuantity", "MaterialRulesWindow.layout");

        internal Exception Failure { get; private set; }
        internal bool UpdateRequested { get; private set; }

        internal MaterialRulesWindow(
            MaterialRulesViewModel model,
            Func<int> saveRules)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _saveRules = saveRules ?? throw new ArgumentNullException(nameof(saveRules));
            _contentLoaded = true;
            SAB.Helpers.WpfComponentLoader.Load(this, "Cls_MaterialQuantity/MaterialRulesWindow.xaml");
            DataContext = model;
            LoadLayout();
            Closed += (sender, args) => SaveLayout();
        }

        private void ApplyBulkClick(object sender, RoutedEventArgs args)
        {
            _model.ApplyBulk(MaterialsGrid.SelectedItems.Cast<MaterialRuleRow>());
        }

        private void ToggleUsedMaterialsClick(object sender, RoutedEventArgs args)
        {
            _model.ToggleUsedMaterials();
        }

        private void SaveRulesClick(object sender, RoutedEventArgs args)
        {
            CommitGridEdits();
            Failure = null;
            SetBusy(true);
            try
            {
                ReportProgress(new MaterialQuantityProgressInfo
                {
                    OverallPercentage = 20,
                    IsIndeterminate = true,
                    Stage = "Сохранение правил материалов",
                    Details = "Записываем выбранные единицы подсчёта в проект."
                });
                int saved = _saveRules();
                _model.ReportRulesSaved(saved);
                ReportProgress(new MaterialQuantityProgressInfo
                {
                    OverallPercentage = 100,
                    Stage = "Правила сохранены",
                    Details = saved == 0
                        ? "Изменений для сохранения нет."
                        : "Сохранено правил: " + saved + "."
                });
            }
            catch (Exception exception)
            {
                Failure = exception;
                ReportProgress(new MaterialQuantityProgressInfo
                {
                    OverallPercentage = 0,
                    Stage = "Не удалось сохранить правила",
                    Details = exception.Message
                });
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdateClick(object sender, RoutedEventArgs args)
        {
            CommitGridEdits();
            Failure = null;
            SetBusy(true);
            try
            {
                ReportProgress(new MaterialQuantityProgressInfo
                {
                    IsIndeterminate = true,
                    Stage = "Подготовка обновления",
                    Details = "Сохраняем правила материалов и готовим расчётные записи."
                });
                int saved = _saveRules();
                _model.ReportRulesSaved(saved);
                // Revit document/family work starts only after ShowDialog returns.
                UpdateRequested = true;
                SetBusy(false);
                DialogResult = true;
            }
            catch (Exception exception)
            {
                Failure = exception;
                ReportProgress(new MaterialQuantityProgressInfo
                {
                    OverallPercentage = 0,
                    Stage = "Не удалось обновить",
                    Details = exception.Message
                });
                SetBusy(false);
            }
        }

        private void CommitGridEdits()
        {
            MaterialsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            MaterialsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void CancelClick(object sender, RoutedEventArgs args)
        {
            DialogResult = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs args)
        {
            if (_isBusy)
            {
                args.Cancel = true;
                return;
            }

            base.OnClosing(args);
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;
            MainContent.IsEnabled = !isBusy;
            CancelButton.IsEnabled = !isBusy;
            SaveRulesButton.IsEnabled = !isBusy;
            UpdateButton.IsEnabled = !isBusy;
            ProgressPanel.Visibility = Visibility.Visible;
        }

        private void ReportProgress(MaterialQuantityProgressInfo progress)
        {
            if (progress == null)
            {
                return;
            }

            UpdateProgressBar.IsIndeterminate = progress.IsIndeterminate;
            if (!progress.IsIndeterminate)
            {
                UpdateProgressBar.Value = Math.Max(0, Math.Min(100, progress.OverallPercentage));
            }

            ProgressStatusText.Text = progress.Stage ?? string.Empty;
            ProgressDetailsText.Text = progress.Details ?? string.Empty;
            ProgressItemsText.Text = progress.TotalItems > 0
                ? "Обработано " + Math.Max(0, Math.Min(progress.CurrentItem, progress.TotalItems)) +
                  " из " + progress.TotalItems
                : (progress.IsIndeterminate
                    ? "Операция выполняется"
                    : "Общий прогресс: " + progress.OverallPercentage + "%");
            ProgressCountersText.Text =
                "Создано: " + progress.Created +
                "  ·  Обновлено: " + progress.Updated +
                "  ·  Проверить: " + progress.NeedsReview +
                "  ·  Удалено: " + progress.Obsolete;
            UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
        }

        private void LoadLayout()
        {
            try
            {
                if (!File.Exists(_layoutPath)) return;
                string[] values = File.ReadAllLines(_layoutPath);
                if (values.Length != 2) return;
                double width, height;
                if (double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out width) &&
                    double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out height))
                {
                    Width = Math.Max(MinWidth, Math.Min(1800, width));
                    Height = Math.Max(MinHeight, Math.Min(1100, height));
                }
            }
            catch (IOException) { /* Layout is optional. */ }
            catch (UnauthorizedAccessException) { /* Layout is optional. */ }
        }

        private void SaveLayout()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_layoutPath));
                File.WriteAllLines(_layoutPath, new[]
                {
                    ActualWidth.ToString(CultureInfo.InvariantCulture),
                    ActualHeight.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch (IOException) { /* Layout is optional. */ }
            catch (UnauthorizedAccessException) { /* Layout is optional. */ }
        }
    }
}
