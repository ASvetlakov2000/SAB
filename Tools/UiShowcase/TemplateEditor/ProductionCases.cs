using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.Views;

namespace SAB.TemplateEditorProductionCheck
{
    public static class ProductionCases
    {
        public static void Run(Action<bool,string> check,Action<Window,string> snapshot)
        {
            var caps=typeof(ViewTemplateGraphicsWindow).Assembly.GetType("SAB.ViewTemplateGraphics.Services.CategoryCapabilities");
            var create=caps.GetMethod("FromMetadata",BindingFlags.NonPublic|BindingFlags.Static);
            var apply=caps.GetMethod("ApplyTo");
            Func<bool,bool,bool,bool,bool,CategoryGraphicsGroup,object> metadata=(g,o,v,c,r,k)=>create.Invoke(null,new object[]{g,o,v,c,r,k});
            var row=new CategoryOverrideRow();
            apply.Invoke(metadata(true,true,true,true,true,CategoryGraphicsGroup.Model),new object[]{row,false});
            check(row.SupportsCut&&row.SupportsDetailLevel&&row.SupportsProjectionLines,"production capability model root starts editable");
            apply.Invoke(metadata(true,false,true,true,true,CategoryGraphicsGroup.Model),new object[]{row,true});
            check(row.AllowsVisibilityControl&&!row.SupportsProjectionLines&&!row.SupportsHalftone&&!row.SupportsCut&&!row.SupportsDetailLevel,"production aggregate intersection disables graphics but preserves independent visibility");
            apply.Invoke(metadata(true,true,true,true,true,CategoryGraphicsGroup.Model),new object[]{row,true});
            check(!row.SupportsProjectionLines,"production aggregate intersection never restores a disabled capability");
            apply.Invoke(metadata(true,true,true,true,false,CategoryGraphicsGroup.Model),new object[]{row,false});
            check(row.SupportsCut&&!row.SupportsDetailLevel,"production subcategory detail omitted independently of cuttable metadata");
            apply.Invoke(metadata(true,true,true,false,true,CategoryGraphicsGroup.AnalyticalModel),new object[]{row,false});
            check(!row.SupportsCut&&!row.SupportsDetailLevel&&row.SupportsProjectionLines&&row.SupportsSurfacePatterns,"production analytical policy retains projection/surface and rejects noncuttable/detail");
            apply.Invoke(metadata(true,true,true,false,true,CategoryGraphicsGroup.Annotation),new object[]{row,false});
            check(row.SupportsHalftone&&!row.SupportsSurfacePatterns&&!row.SupportsTransparency,"production annotation policy preserves halftone");

            var window=new Window {Width=500,Height=280};
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"UI\Styles\SABFluentLight.xaml"),UriKind.Absolute)});
            var surface=new Grid {Background=(Brush)window.FindResource("SabBrush.WindowBackground")};window.Content=surface;
            var layout=new StackPanel {Margin=new Thickness(16)};surface.Children.Add(layout);
            // Existing default TabControl style stays opt-out; only the requested module wraps.
            var tabs=new TabControl();tabs.Items.Add(new TabItem {Header="Первая",Content="Данные"});tabs.Items.Add(new TabItem {Header="Вторая",Content="Данные"});layout.Children.Add(tabs);
            var group=new Grid {HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,16,0,0)};
            SAB.UI.FluentLight.Capsule.SetIsGroup(group,true);
            group.Children.Add(new Border {CornerRadius=new CornerRadius(17)});
            var indicator=new Border {Tag="CapsuleIndicator",HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Background=(Brush)window.FindResource("SabBrush.Accent")};group.Children.Add(indicator);
            var choices=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(2)};
            var first=new RadioButton {Content="Первый",IsChecked=true,GroupName="Regression"};var second=new RadioButton {Content="Второй",GroupName="Regression"};choices.Children.Add(first);choices.Children.Add(second);group.Children.Add(choices);layout.Children.Add(group);
            window.Show();Evidence.Pump();
            var defaultIndicator=TemplateEditorPresentation.Children<Border>(tabs).First(x=>Equals(x.Tag,"CapsuleIndicator"));
            var active=(TabItem)tabs.Items[0];var track=(Grid)VisualTreeHelper.GetParent(defaultIndicator);var point=active.TranslatePoint(new Point(),track);var transform=(TranslateTransform)defaultIndicator.RenderTransform;
            check(Math.Abs(transform.Y-point.Y)<1&&Math.Abs(defaultIndicator.ActualHeight-active.ActualHeight)<1,"shared default tab capsule single-row geometry retained");
            second.IsChecked=true;Evidence.Pump();point=second.TranslatePoint(new Point(),group);transform=(TranslateTransform)indicator.RenderTransform;
            check(Math.Abs(transform.X-point.X)<1&&Math.Abs(transform.Y-point.Y)<1&&Math.Abs(indicator.ActualHeight-second.ActualHeight)<1,"shared radio capsule XY/height retained after selection");
            snapshot(window,"shared-capsule-regression");window.Close();Evidence.Pump();
        }
    }
}
