using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SAB.UI
{
    public static class SabWindowAnimationService
    {
        private static readonly DependencyProperty ButtonAnimationStateProperty =
            DependencyProperty.RegisterAttached(
                "ButtonAnimationState",
                typeof(ButtonAnimationState),
                typeof(SabWindowAnimationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty DataGridAnimationAttachedProperty =
            DependencyProperty.RegisterAttached(
                "DataGridAnimationAttached",
                typeof(bool),
                typeof(SabWindowAnimationService),
                new PropertyMetadata(false));

        private static readonly DependencyProperty ExpanderAnimationAttachedProperty =
            DependencyProperty.RegisterAttached(
                "ExpanderAnimationAttached",
                typeof(bool),
                typeof(SabWindowAnimationService),
                new PropertyMetadata(false));

        public static void AttachWindowAnimations(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.Dispatcher.BeginInvoke(
                new Action(delegate
                {
                    AttachButtonAnimations(window);
                    AttachExpanderAnimations(window);
                    AttachDataGridAnimations(window);
                }),
                DispatcherPriority.ContextIdle);
        }

        public static void PulseElement(FrameworkElement element)
        {
            if (element == null || !element.IsVisible)
            {
                return;
            }

            TranslateTransform translateTransform = EnsureTranslateTransform(element);
            element.Opacity = 0.72;
            translateTransform.Y = 2.0;

            DoubleAnimation opacityAnimation = CreateDoubleAnimation(1.0, 170);
            DoubleAnimation yAnimation = CreateDoubleAnimation(0.0, 170);

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, yAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        public static void PulseButton(ButtonBase button)
        {
            if (button == null || !button.IsVisible || !button.IsEnabled)
            {
                return;
            }

            ScaleTransform scaleTransform = EnsureScaleTransform(button);
            DoubleAnimation scaleUpAnimation = CreateDoubleAnimation(1.025, 95);
            scaleUpAnimation.AutoReverse = true;

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation, HandoffBehavior.SnapshotAndReplace);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpAnimation.Clone(), HandoffBehavior.SnapshotAndReplace);
        }

        private static void AttachButtonAnimations(DependencyObject root)
        {
            IList<Button> buttons = FindVisualChildren<Button>(root);
            for (int i = 0; i < buttons.Count; i++)
            {
                AttachButtonAnimation(buttons[i]);
            }
        }

        private static void AttachButtonAnimation(Button button)
        {
            if (button == null || GetButtonAnimationState(button) != null)
            {
                return;
            }

            ButtonAnimationState state = new ButtonAnimationState();
            state.ScaleTransform = EnsureScaleTransform(button);
            SetButtonAnimationState(button, state);

            button.MouseEnter += Button_MouseEnter;
            button.MouseLeave += Button_MouseLeave;
            button.PreviewMouseLeftButtonDown += Button_PreviewMouseLeftButtonDown;
            button.PreviewMouseLeftButtonUp += Button_PreviewMouseLeftButtonUp;
            button.Unloaded += Button_Unloaded;
        }

        private static void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !button.IsEnabled)
            {
                return;
            }

            AnimateButtonScale(button, 1.012, 90);
        }

        private static void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            AnimateButtonScale(button, 1.0, 110);
        }

        private static void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !button.IsEnabled)
            {
                return;
            }

            AnimateButtonScale(button, 0.985, 70);
        }

        private static void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !button.IsEnabled)
            {
                return;
            }

            AnimateButtonScale(button, button.IsMouseOver ? 1.012 : 1.0, 90);
        }

        private static void Button_Unloaded(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            button.MouseEnter -= Button_MouseEnter;
            button.MouseLeave -= Button_MouseLeave;
            button.PreviewMouseLeftButtonDown -= Button_PreviewMouseLeftButtonDown;
            button.PreviewMouseLeftButtonUp -= Button_PreviewMouseLeftButtonUp;
            button.Unloaded -= Button_Unloaded;
            SetButtonAnimationState(button, null);
        }

        private static void AnimateButtonScale(Button button, double targetScale, int milliseconds)
        {
            ButtonAnimationState state = GetButtonAnimationState(button);
            if (state == null || state.ScaleTransform == null)
            {
                return;
            }

            DoubleAnimation animation = CreateDoubleAnimation(targetScale, milliseconds);
            state.ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation, HandoffBehavior.SnapshotAndReplace);
            state.ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone(), HandoffBehavior.SnapshotAndReplace);
        }

        private static void AttachExpanderAnimations(DependencyObject root)
        {
            IList<Expander> expanders = FindVisualChildren<Expander>(root);
            for (int i = 0; i < expanders.Count; i++)
            {
                Expander expander = expanders[i];
                if (GetExpanderAnimationAttached(expander))
                {
                    continue;
                }

                expander.Loaded += Expander_Loaded;
                expander.Expanded += Expander_Expanded;
                expander.Collapsed += Expander_Collapsed;
                expander.Unloaded += Expander_Unloaded;
                SetExpanderAnimationAttached(expander, true);

                ScheduleExpanderArrowEntrance(expander, i * 28);
            }
        }

        private static void Expander_Loaded(object sender, RoutedEventArgs e)
        {
            Expander expander = sender as Expander;
            if (expander == null)
            {
                return;
            }

            ScheduleExpanderArrowEntrance(expander, 0);
        }

        private static void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            Expander expander = sender as Expander;
            if (expander == null)
            {
                return;
            }

            ScheduleExpanderArrowPulse(expander);

            FrameworkElement contentElement = expander.Content as FrameworkElement;
            if (contentElement != null)
            {
                PulseElement(contentElement);
            }
        }

        private static void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            ScheduleExpanderArrowPulse(sender as Expander);
        }

        private static void Expander_Unloaded(object sender, RoutedEventArgs e)
        {
            Expander expander = sender as Expander;
            if (expander == null)
            {
                return;
            }

            expander.Loaded -= Expander_Loaded;
            expander.Expanded -= Expander_Expanded;
            expander.Collapsed -= Expander_Collapsed;
            expander.Unloaded -= Expander_Unloaded;
            SetExpanderAnimationAttached(expander, false);
        }

        private static void ScheduleExpanderArrowEntrance(Expander expander, int delayMilliseconds)
        {
            if (expander == null)
            {
                return;
            }

            expander.Dispatcher.BeginInvoke(
                new Action(delegate
                {
                    AnimateExpanderArrow(expander, 0.72, 1.0, 170, delayMilliseconds);
                }),
                DispatcherPriority.ContextIdle);
        }

        private static void ScheduleExpanderArrowPulse(Expander expander)
        {
            if (expander == null)
            {
                return;
            }

            expander.Dispatcher.BeginInvoke(
                new Action(delegate
                {
                    AnimateExpanderArrow(expander, 0.82, 1.0, 130, 0);
                }),
                DispatcherPriority.Background);
        }

        private static void AnimateExpanderArrow(
            Expander expander,
            double fromScale,
            double toScale,
            int durationMilliseconds,
            int delayMilliseconds)
        {
            Path arrow = FindExpanderArrow(expander);
            if (arrow == null || !arrow.IsVisible)
            {
                return;
            }

            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform scaleTransform = EnsureScaleTransform(arrow);
            scaleTransform.ScaleX = fromScale;
            scaleTransform.ScaleY = fromScale;

            DoubleAnimation scaleAnimation = CreateDoubleAnimation(toScale, durationMilliseconds);
            if (delayMilliseconds > 0)
            {
                scaleAnimation.BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds);
            }

            DoubleAnimation opacityAnimation = CreateDoubleAnimation(1.0, durationMilliseconds);
            if (delayMilliseconds > 0)
            {
                opacityAnimation.BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds);
            }

            arrow.Opacity = 0.68;
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation, HandoffBehavior.SnapshotAndReplace);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone(), HandoffBehavior.SnapshotAndReplace);
            arrow.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        private static Path FindExpanderArrow(Expander expander)
        {
            IList<Path> paths = FindVisualChildren<Path>(expander);
            if (paths == null || paths.Count == 0)
            {
                return null;
            }

            return paths[0];
        }

        private static void AttachDataGridAnimations(DependencyObject root)
        {
            IList<DataGrid> dataGrids = FindVisualChildren<DataGrid>(root);
            for (int i = 0; i < dataGrids.Count; i++)
            {
                DataGrid dataGrid = dataGrids[i];
                if (GetDataGridAnimationAttached(dataGrid))
                {
                    continue;
                }

                dataGrid.LoadingRow += DataGrid_LoadingRow;
                dataGrid.Unloaded += DataGrid_Unloaded;
                SetDataGridAnimationAttached(dataGrid, true);
            }
        }

        private static void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e == null || e.Row == null)
            {
                return;
            }

            DataGridRow row = e.Row;
            AttachButtonAnimations(row);
        }

        private static void DataGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            DataGrid dataGrid = sender as DataGrid;
            if (dataGrid == null)
            {
                return;
            }

            dataGrid.LoadingRow -= DataGrid_LoadingRow;
            dataGrid.Unloaded -= DataGrid_Unloaded;
            SetDataGridAnimationAttached(dataGrid, false);
        }

        private static ScaleTransform EnsureScaleTransform(FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            ScaleTransform directScaleTransform = element.RenderTransform as ScaleTransform;
            if (directScaleTransform != null)
            {
                return directScaleTransform;
            }

            TransformGroup transformGroup = element.RenderTransform as TransformGroup;
            if (transformGroup != null)
            {
                for (int i = 0; i < transformGroup.Children.Count; i++)
                {
                    ScaleTransform childScaleTransform = transformGroup.Children[i] as ScaleTransform;
                    if (childScaleTransform != null)
                    {
                        return childScaleTransform;
                    }
                }

                ScaleTransform newScaleTransform = new ScaleTransform(1.0, 1.0);
                transformGroup.Children.Add(newScaleTransform);
                return newScaleTransform;
            }

            Transform existingTransform = element.RenderTransform;
            TransformGroup newTransformGroup = new TransformGroup();
            if (existingTransform != null && existingTransform != Transform.Identity)
            {
                newTransformGroup.Children.Add(existingTransform);
            }

            ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
            newTransformGroup.Children.Add(scaleTransform);
            element.RenderTransform = newTransformGroup;
            return scaleTransform;
        }

        private static TranslateTransform EnsureTranslateTransform(FrameworkElement element)
        {
            TranslateTransform directTranslateTransform = element.RenderTransform as TranslateTransform;
            if (directTranslateTransform != null)
            {
                return directTranslateTransform;
            }

            TransformGroup transformGroup = element.RenderTransform as TransformGroup;
            if (transformGroup != null)
            {
                for (int i = 0; i < transformGroup.Children.Count; i++)
                {
                    TranslateTransform childTranslateTransform = transformGroup.Children[i] as TranslateTransform;
                    if (childTranslateTransform != null)
                    {
                        return childTranslateTransform;
                    }
                }

                TranslateTransform newTranslateTransform = new TranslateTransform();
                transformGroup.Children.Add(newTranslateTransform);
                return newTranslateTransform;
            }

            Transform existingTransform = element.RenderTransform;
            TransformGroup newTransformGroup = new TransformGroup();
            if (existingTransform != null && existingTransform != Transform.Identity)
            {
                newTransformGroup.Children.Add(existingTransform);
            }

            TranslateTransform translateTransform = new TranslateTransform();
            newTransformGroup.Children.Add(translateTransform);
            element.RenderTransform = newTransformGroup;
            return translateTransform;
        }

        private static DoubleAnimation CreateDoubleAnimation(double toValue, int milliseconds)
        {
            DoubleAnimation animation = new DoubleAnimation();
            animation.To = toValue;
            animation.Duration = TimeSpan.FromMilliseconds(milliseconds);
            animation.EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            return animation;
        }

        private static IList<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            List<T> result = new List<T>();
            FillVisualChildren(parent, result);
            return result;
        }

        private static void FillVisualChildren<T>(DependencyObject parent, IList<T> result)
            where T : DependencyObject
        {
            if (parent == null || result == null)
            {
                return;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null)
                {
                    result.Add(typedChild);
                }

                FillVisualChildren(child, result);
            }
        }

        private static ButtonAnimationState GetButtonAnimationState(DependencyObject element)
        {
            return element != null ? (ButtonAnimationState)element.GetValue(ButtonAnimationStateProperty) : null;
        }

        private static void SetButtonAnimationState(DependencyObject element, ButtonAnimationState state)
        {
            if (element != null)
            {
                element.SetValue(ButtonAnimationStateProperty, state);
            }
        }

        private static bool GetDataGridAnimationAttached(DependencyObject element)
        {
            return element != null && (bool)element.GetValue(DataGridAnimationAttachedProperty);
        }

        private static void SetDataGridAnimationAttached(DependencyObject element, bool value)
        {
            if (element != null)
            {
                element.SetValue(DataGridAnimationAttachedProperty, value);
            }
        }

        private static bool GetExpanderAnimationAttached(DependencyObject element)
        {
            return element != null && (bool)element.GetValue(ExpanderAnimationAttachedProperty);
        }

        private static void SetExpanderAnimationAttached(DependencyObject element, bool value)
        {
            if (element != null)
            {
                element.SetValue(ExpanderAnimationAttachedProperty, value);
            }
        }

        private class ButtonAnimationState
        {
            public ScaleTransform ScaleTransform { get; set; }
        }
    }
}
