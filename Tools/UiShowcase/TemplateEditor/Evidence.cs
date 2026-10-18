using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.TemplateEditorPreview.Models;
using SAB.TemplateEditorPreview.ViewModels;
using SAB.TemplateEditorPreview.Views;

namespace SAB.TemplateEditorPreview
{
    public static class Evidence
    {
        static string output=Path.Combine(Program.Root,@"Docs\Ui\DesignSystem\screenshots-template-editor\revision-2");
        static List<string> log=new List<string>();
        static int captures;
        public static void Pump(int ms=240) { var frame=new DispatcherFrame(); var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(ms) }; timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame); }
        static void Check(bool result,string name) { log.Add((result?"PASS ":"FAIL ")+name); File.WriteAllLines(Path.Combine(output,"verification.txt"),log); if(!result) throw new InvalidOperationException(name); }
        static IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject => PreviewShell.Children<T>(root);
        static T Named<T>(DependencyObject root,string name) where T:FrameworkElement => All<T>(root).First(x=>x.Name==name);
        static Button Tagged(Window window,string tag) => All<Button>(window).First(x=>x.IsVisible && (x.Tag as string)==tag);
        static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
        static void Snapshot(Window window,string name,Popup popup=null)
        {
            Pump();window.UpdateLayout();
            var surface=(FrameworkElement)window.Content;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth),(int)Math.Ceiling(surface.ActualHeight),96,96,PixelFormats.Pbgra32);
            if(popup==null) bitmap.Render(surface);
            else {
                var child=(FrameworkElement)popup.Child;
                Check(popup.IsOpen&&child.IsVisible&&child.ActualHeight>20,"capture contains live Popup child");
                var delta=child.PointToScreen(new Point())-surface.PointToScreen(new Point());
                delta=PresentationSource.FromVisual(surface).CompositionTarget.TransformFromDevice.Transform(delta);
                var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()) {
                    context.DrawRectangle(new VisualBrush(surface),null,new Rect(0,0,surface.ActualWidth,surface.ActualHeight));
                    context.DrawRectangle(new VisualBrush(child),null,new Rect(delta.X,delta.Y,child.ActualWidth,child.ActualHeight));
                }
                bitmap.Render(drawing);
                log.Add("POPUP "+name+" captured actual separate WPF popup at measured offset "+delta+"; no drawn substitute");
            }
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,name+".png")))encoder.Save(file);
            captures++; log.Add("CAPTURE "+name+" visible WPF "+window.ActualWidth+"x"+window.ActualHeight+" DIP; RenderTargetBitmap96; not OS150/Revit");
        }
        static Rect Bounds(FrameworkElement element,Window window) => element.TransformToAncestor(window).TransformBounds(new Rect(new Point(),element.RenderSize));
        static void TabGeometry(Window window,TabControl tabs,bool narrow)
        {
            var indicator=All<Border>(tabs).First(x=>Equals(x.Tag,"CapsuleIndicator"));
            var track=(Grid)VisualTreeHelper.GetParent(indicator);
            var active=(TabItem)tabs.SelectedItem;
            var point=active.TranslatePoint(new Point(),track);var transform=(TranslateTransform)indicator.RenderTransform;
            Check(Math.Abs(transform.X-point.X)<1&&Math.Abs(transform.Y-point.Y)<1,"tab capsule follows active row in two dimensions");
            Check(Math.Abs(indicator.ActualHeight-active.ActualHeight)<1,"tab capsule height equals one tab, not entire wrapped group");
            var positions=tabs.Items.OfType<TabItem>().Select(x=>x.TranslatePoint(new Point(),track)).ToList();
            int rows=positions.Select(x=>Math.Round(x.Y)).Distinct().Count();
            Check(rows==(narrow?2:1),"tab header rows "+rows+" expected "+(narrow?2:1));
            Check(positions.Zip(positions.Skip(1),(a,b)=>b.Y>a.Y||Math.Abs(b.Y-a.Y)<1&&b.X>a.X).All(x=>x),"tab order retained left to right then top to bottom");
            Check(track.Children.OfType<Border>().Count(x=>x.BorderThickness.Left>0)==1,"one shared frame around all tab rows");
        }
        static void Geometry(Window window,string context)
        {
            var help=Named<Button>(window,"InstructionButton");var end=All<Button>(window).First(x=>x.Name=="ApplyButton"||x.Name=="AddButton");
            Check(help.IsVisible&&end.IsVisible&&Bounds(end,window).Bottom<window.ActualHeight,context+" footer reachable");
            Check(All<Button>(window).Count(x=>x.Name=="InstructionButton")==1,context+" one footer help");
            foreach(var table in All<DataGrid>(window).Where(x=>x.IsVisible)) {
                Check(table.ColumnHeaderHeight==38&&table.RowHeight==44,context+" header38 row44");
                var header=All<DataGridColumnHeader>(table).FirstOrDefault(x=>x.IsVisible&&x.Column!=null);
                if(header!=null) Check(Math.Abs(header.ActualHeight-38)<1,context+" measured header38");
                foreach(var h in All<DataGridColumnHeader>(table).Where(x=>x.IsVisible&&x.Column!=null)) {
                    string label=h.Content as string;
                    if(label!=null) {
                        double textWidth=label.Split('\n').Max(line=>new FormattedText(line,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(h.FontFamily,h.FontStyle,h.FontWeight,h.FontStretch),h.FontSize,Brushes.Black,1).WidthIncludingTrailingWhitespace);
                        Check(h.ActualWidth>=textWidth+h.Padding.Left+h.Padding.Right-1,context+" full header "+label.Replace('\n','/'));
                        var cell=All<DataGridCell>(table).FirstOrDefault(c=>c.IsVisible&&c.Column==h.Column);
                        if(cell!=null&&h.HorizontalContentAlignment==HorizontalAlignment.Left&&label!="Категория") {
                            double? axis=null;
                            var textBox=All<TextBox>(cell).FirstOrDefault(x=>x.IsVisible);
                            var combo=All<ComboBox>(cell).FirstOrDefault(x=>x.IsVisible);
                            var button=All<Button>(cell).FirstOrDefault(x=>x.IsVisible);
                            var text=All<TextBlock>(cell).FirstOrDefault(x=>x.IsVisible&&!string.IsNullOrEmpty(x.Text));
                            if(textBox!=null) axis=Bounds(textBox,window).Left+textBox.Padding.Left+textBox.BorderThickness.Left;
                            else if(combo!=null&&combo.Template.FindName("Label",combo) is FrameworkElement comboLabel) axis=Bounds(comboLabel,window).Left;
                            else if(button!=null) {var presenter=All<ContentPresenter>(button).FirstOrDefault(x=>x.IsVisible);if(presenter!=null)axis=Bounds(presenter,window).Left;}
                            else if(text!=null)axis=Bounds(text,window).Left;
                            if(axis.HasValue) Check(Math.Abs(axis.Value-(Bounds(h,window).Left+h.Padding.Left))<=1.01,context+" text/header axis "+label.Replace('\n','/'));
                        }
                    }
                }
                foreach(var bar in All<ScrollBar>(table).Where(x=>x.IsVisible&&x.Orientation==Orientation.Vertical))
                    Check(header!=null&&Bounds(bar,window).Top>=Bounds(header,window).Bottom-1,context+" vertical scrollbar below header");
                Check(Bounds(table,window).Bottom<Bounds(help,window).Top,context+" bounded table before actions");
                var viewport=All<ScrollContentPresenter>(table).FirstOrDefault(x=>x.IsVisible);
                if(viewport!=null) {
                    double columns=table.Columns.Where(x=>x.Visibility==Visibility.Visible).Sum(x=>x.ActualWidth);
                    Check(columns>=viewport.ActualWidth-2,context+" last column fills row viewport (or horizontal overflow)");
                }
                var scroll=All<ScrollViewer>(table).FirstOrDefault(x=>x.IsVisible);
                if(scroll!=null&&(context.StartsWith("normal")||context=="stretch")) Check(scroll.ScrollableWidth<1,context+" normal columns fit without horizontal bar");
            }
            foreach(var box in All<ComboBox>(window).Where(x=>x.IsVisible)) Check(!box.IsDropDownOpen,context+" no spontaneous popup");
            foreach(var box in All<TextBox>(window).Where(x=>x.IsVisible&&x.Name.Contains("Search"))) Check(box.ActualHeight>=34,context+" search min34");
        }
        static bool RoutedKey(UIElement target,Key key)
        {
            var source=PresentationSource.FromVisual(target);
            var preview=new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent };
            target.RaiseEvent(preview);
            if(preview.Handled) {Pump();return true;}
            var down=new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key) { RoutedEvent=Keyboard.KeyDownEvent };
            target.RaiseEvent(down);Pump();return down.Handled;
        }
        static ViewTemplateGraphicsWindow OpenMain(out ViewTemplateGraphicsViewModel vm)
        {
            vm=new ViewTemplateGraphicsViewModel(null,Fixtures.Templates(),new Services.ViewTemplateGraphicsDataService());
            var window=new ViewTemplateGraphicsWindow(vm);window.Show();Pump();return window;
        }
        static void ModalAction(Window owner,Action start,Action<Window> action)
        {
            Exception error=null;
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(350) };
            timer.Tick+=(s,e)=> { timer.Stop();try { var child=Application.Current.Windows.OfType<Window>().Last(w=>w!=owner&&w.Owner==owner);action(child); } catch(Exception ex) {error=ex; foreach(var child in Application.Current.Windows.OfType<Window>().Where(w=>w.Owner==owner).ToList()) child.Close();} };
            timer.Start();start();if(error!=null)throw error;Pump();
        }
        public static void Run()
        {
            Directory.CreateDirectory(output);
            log.Add("Scope: source-derived preview; synthetic Document adapter; original models/handlers. Production/Revit not executed.");
            Check(Application.Current.Resources.Count==0&&Application.Current.Resources.MergedDictionaries.Count==0,"clean Application resources");
            var window=OpenMain(out var vm);var tabs=Named<TabControl>(window,"GraphicsTabControl");
            log.Add("Runtime DPI "+(96*PresentationSource.FromVisual(window).CompositionTarget.TransformToDevice.M11));
            Check(tabs.Items.Count==7&&!tabs.Items.OfType<TabItem>().Any(x=>Equals(x.Header,"Связанные файлы")),"seven tabs; per-link entity removed by user request");Check(!vm.CanApply,"clean Apply disabled");
            var templateList=Named<ListBox>(window,"TemplatesListBox");
            Check(Math.Abs(((FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(templateList)))).ActualWidth-345.6)<1,"template sidebar measured width345.6 increased20percent (pixel rounding tolerance1)");
            Check(Bounds(templateList,window).Left<Bounds(tabs,window).Left&&templateList.ActualHeight>350,"vertical template sidebar left of wide workspace");
            for(int i=0;i<tabs.Items.Count;i++) {
                tabs.SelectedIndex=i;
                var indicator=All<Border>(tabs).First(x=>Equals(x.Tag,"CapsuleIndicator"));
                if(i>0) Check(((TranslateTransform)indicator.RenderTransform).HasAnimatedProperties==SystemParameters.ClientAreaAnimation,"tab "+i+" animation follows system preference");
                Pump();var item=(TabItem)tabs.Items[i];
                var track=(FrameworkElement)VisualTreeHelper.GetParent(indicator);
                Check(Math.Abs(((TranslateTransform)indicator.RenderTransform).X-item.TranslatePoint(new Point(),track).X)<1,"tab "+i+" settled indicator position");
                var body=All<ContentPresenter>(tabs).First(x=>x.Name=="SelectedBody");
                Check(Math.Abs(Bounds(body,window).Top-Bounds(track,window).Bottom-12)<1,"tab "+i+" measured content gap12");
                Check(item.IsVisible&&item.ActualHeight>=40,"tab "+i+" visible min40");TabGeometry(window,tabs,false);Geometry(window,"normal tab"+i);Snapshot(window,"tab-"+i);
            }
            window.Width=window.MinWidth;window.Height=window.MinHeight;Pump();
            for(int i=0;i<tabs.Items.Count;i++) {
                tabs.SelectedIndex=i;Pump();TabGeometry(window,tabs,true);Geometry(window,"minimum tab"+i);Snapshot(window,"minimum-tab-"+i);
                var table=All<DataGrid>(window).FirstOrDefault(x=>x.IsVisible);
                var scroll=table==null?null:All<ScrollViewer>(table).FirstOrDefault(x=>x.IsVisible);
                if(scroll!=null&&scroll.ScrollableWidth>0) {
                    scroll.ScrollToRightEnd();Pump();Check(scroll.HorizontalOffset>=scroll.ScrollableWidth-1,"minimum tab"+i+" native horizontal right end reached");
                    var last=All<DataGridColumnHeader>(table).First(x=>x.Column==table.Columns.Last());
                    Check(Bounds(last,window).Right<=Bounds(table,window).Right+1&&Bounds(last,window).Left>=Bounds(table,window).Left-1,"minimum tab"+i+" last column fully reachable");
                    Snapshot(window,"minimum-tab-"+i+"-right");scroll.ScrollToLeftEnd();Pump();
                }
            }
            tabs.SelectedIndex=0;Pump();
            var properties=All<DataGrid>(window).First(x=>x.IsVisible);properties.ScrollIntoView(vm.GraphicsData.ViewProperties[7]);Pump();
            var propertyEditor=All<TextBox>(properties).First(x=>x.IsVisible&&x.IsEnabled&&!x.IsReadOnly);window.Activate();Pump();Keyboard.Focus(propertyEditor);Pump();
            Check(propertyEditor.IsKeyboardFocused,"real WPF property editor focus");Snapshot(window,"property-editor-focus");
            All<ScrollViewer>(properties).First().ScrollToTop();
            window.Width=1800;window.Height=1000;Pump();Geometry(window,"stretch");Snapshot(window,"stretch");
            window.Width=1780;window.Height=940;
            RevisionCases(window,vm,tabs);
            var list=Named<ListBox>(window,"TemplatesListBox");list.SelectedItems.Add(vm.Templates[0]);list.SelectedItems.Add(vm.Templates[1]);
            Check(list.SelectedItems.Count==2&&vm.TargetTemplateCount==1,"template highlight independent of inclusion");
            var search=Named<TextBox>(window,"TemplateSearchTextBox");search.Text="согласование";Click(Named<Button>(window,"FindTemplatesButton"));
            Check(vm.Templates.Count(x=>x.IsVisibleInList)==1,"template name search source handler");Snapshot(window,"template-search");
            search.Text="фасады";window.Activate();Keyboard.Focus(search);Pump();Check(search.IsKeyboardFocused,"real WPF search focus");
            Check(RoutedKey(search,Key.Enter),"search Enter handled before default Apply");
            Check(vm.Templates.Count(x=>x.IsVisibleInList)==1&&vm.Templates[2].IsVisibleInList,"routed Enter invokes original search");Snapshot(window,"search-enter-focus");
            search.Text="";Click(Named<Button>(window,"FindTemplatesButton"));vm.SelectedTemplateTypeValue="Разрез";Pump();Check(vm.Templates.Count(x=>x.IsVisibleInList)==1,"template type filter");
            vm.SelectedTemplateTypeValue="";vm.SetTargetSelection(new[]{vm.Templates[0],vm.Templates[1]},true);Pump();
            Check(vm.TargetTemplateCount==2&&vm.GraphicsData.ModelCategories.Rows.Any(x=>x.HasMixedTemplateValues),"mixed aggregate fixture");
            tabs.SelectedIndex=1;Pump();Snapshot(window,"mixed-selection");
            var categoryGrid=All<DataGrid>(window).First(x=>x.IsVisible);var category=(CategoryOverrideRow)categoryGrid.Items[0];
            Click(Tagged(window,"ToggleCategory"));Check(!category.IsExpanded,"source category collapse");
            Click(Tagged(window,"ToggleCategory"));Check(category.IsExpanded,"source category expansion");Snapshot(window,"category-expanded");
            categoryGrid.SelectedItems.Add(categoryGrid.Items[0]);categoryGrid.SelectedItems.Add(categoryGrid.Items[1]);Pump();
            var check=All<CheckBox>(categoryGrid).First(x=>x.IsVisible&&(x.Tag as string)=="BatchCategoryVisibility");check.IsChecked=false;check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(((CategoryOverrideRow)categoryGrid.Items[0]).VisibilityState==false&&((CategoryOverrideRow)categoryGrid.Items[1]).VisibilityState==false,"source batch category visibility");
            Check(vm.CanApply&&vm.ModifiedSettingCount>0,"dirty Apply enabled");
            bool changed=vm.SetTargetSelection(new[]{vm.Templates[2]},true);Check(!changed&&vm.TargetTemplateCount==2,"dirty target guard restores original selection");Snapshot(window,"dirty-validation");
            int weight=category.Graphics.ProjectionLineWeight;
            ModalAction(window,()=>Click(Tagged(window,"EditCategoryProjectionLines")),child=> {
                Geometry(child,"projection editor");Snapshot(child,"editor-projection");
                var editor=(GraphicOverrideEditorViewModel)child.DataContext;editor.Data.ProjectionLineWeight=7;Click(Named<Button>(child,"CancelButton"));
            });Check(category.Graphics.ProjectionLineWeight==weight,"graphics Cancel leaves original data");
            ModalAction(window,()=>Click(Tagged(window,"EditCategoryProjectionLines")),child=> {
                var editor=(GraphicOverrideEditorViewModel)child.DataContext;editor.Data.ProjectionLineWeight=7;Click(Named<Button>(child,"ApplyButton"));
            });Check(category.Graphics.ProjectionLineWeight==7,"graphics Apply commits through original handler");
            foreach(GraphicOverrideEditorSection section in Enum.GetValues(typeof(GraphicOverrideEditorSection))) {
                var data=vm.GraphicsData;
                var edit=new GraphicOverrideEditorViewModel(category.Graphics.CloneForEditing(),section,data.LineWeights,data.LinePatterns,data.FillPatterns,data.DetailLevels,true,true,true,true);
                var child=new GraphicOverrideEditorWindow(edit);child.Show();Pump();Geometry(child,"editor "+section);Snapshot(child,"editor-"+section);
                if(section==GraphicOverrideEditorSection.SurfacePatterns) {
                    var combo=All<ComboBox>(child).First(x=>x.IsVisible);Keyboard.Focus(combo);combo.IsDropDownOpen=true;Pump();
                    var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
                    Check(All<TextBlock>(popup.Child).Any(x=>x.IsVisible&&x.Text.Contains("Бетон")),"open popup contains real fill pattern names");
                    Snapshot(child,"editor-popup",popup);
                    RoutedKey(combo,Key.Escape);Check(!combo.IsDropDownOpen,"routed Escape closes real popup");
                    Check(child.IsVisible,"popup Escape keeps editor open");
                }
                child.Width=child.MinWidth;child.Height=child.MinHeight;Pump();Geometry(child,"minimum editor "+section);Snapshot(child,"minimum-editor-"+section);
                child.Close();Pump();
            }
            tabs.SelectedIndex=5;Pump();int count=vm.GraphicsData.Filters.Count(x=>x.IsIncluded);
            ModalAction(window,()=>Click(Named<Button>(window,"AddSelectedFiltersButton")),child=> {
                var picker=(AddFiltersWindow)child;Geometry(child,"filter picker");Snapshot(child,"add-filters");
                child.Width=child.MinWidth;child.Height=child.MinHeight;Pump();Geometry(child,"minimum filter picker");Snapshot(child,"minimum-add-filters");
                Click(Named<Button>(child,"AddButton"));Check(!string.IsNullOrEmpty(PreviewDialogs.LastMessage),"empty filter selection validation");Snapshot(child,"add-filters-validation");
                picker.Items.First(x=>!x.IsAlreadyInTemplate).IsSelected=true;Click(Named<Button>(child,"AddButton"));
            });Check(vm.GraphicsData.Filters.Count(x=>x.IsIncluded)==count+1,"source AddFilters roundtrip");
            var filterGrid=Named<DataGrid>(window,"FiltersDataGrid");var first=(FilterOverrideRow)filterGrid.Items[0];filterGrid.SelectedItem=first;
            Click(Named<Button>(window,"MoveFilterDownButton"));Check(vm.GraphicsData.Filters.IndexOf(first)==1,"source filter reorder");Snapshot(window,"filter-reordered");
            filterGrid.SelectedItem=first;Click(Named<Button>(window,"RemoveSelectedFiltersButton"));Check(!first.IsIncluded&&!first.IsPresentInTable,"source filter remove");
            tabs.SelectedIndex=6;Pump();var workGrid=All<DataGrid>(window).First(x=>x.IsVisible);var workCombo=All<ComboBox>(workGrid).First(x=>x.IsVisible);workCombo.SelectedIndex=1;Pump();Snapshot(window,"workset-edited");
            vm.GraphicsData.Worksets.Clear();tabs.SelectedIndex=6;Pump();Geometry(window,"empty worksets");Snapshot(window,"empty-worksets");
            search.Text="несуществующий шаблон";Click(Named<Button>(window,"FindTemplatesButton"));Snapshot(window,"empty-template-search");
            window.Close();Pump();
            var second=OpenMain(out var secondVm);var list2=Named<ListBox>(second,"TemplatesListBox");list2.ScrollIntoView(secondVm.Templates[5]);Pump();Snapshot(second,"long-template-name");
            secondVm.SetAllVisibleTargets(false);Pump();Check(secondVm.GraphicsData==null&&!secondVm.CanApply,"zero targets disables Apply");Snapshot(second,"zero-targets");second.Close();Pump();
            var finalVm=new ViewTemplateGraphicsViewModel(null,Fixtures.Templates(),new Services.ViewTemplateGraphicsDataService());var final=new ViewTemplateGraphicsWindow(finalVm);
            final.Loaded+=(s,e)=>final.Dispatcher.BeginInvoke(new Action(()=> {finalVm.GraphicsData.ViewProperties[0].ValueText="1 : 50";finalVm.NotifyDataChanged();Click(Named<Button>(final,"ApplyButton"));}));
            Check(final.ShowDialog()==true,"main modal Apply accepts fixture DTO");
            var cancelVm=new ViewTemplateGraphicsViewModel(null,Fixtures.Templates(),new Services.ViewTemplateGraphicsDataService());var cancel=new ViewTemplateGraphicsWindow(cancelVm);
            cancel.Loaded+=(s,e)=>cancel.Dispatcher.BeginInvoke(new Action(()=> {Keyboard.Focus(Named<Button>(cancel,"CancelButton"));AccessKeyManager.ProcessKey(null,"\u001b",false);}));
            Check(cancel.ShowDialog()==false,"WPF Escape access key cancels main dialog");
            log.Add("COMPLETED "+log.Count(x=>x.StartsWith("PASS"))+" PASS / "+captures+" visible WPF captures; Revit/OS-DPI/install NOT RUN");File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
        }
        static void RevisionCases(Window window,ViewTemplateGraphicsViewModel vm,TabControl tabs)
        {
            tabs.SelectedIndex=2;Pump();var annotation=All<DataGrid>(window).First(x=>x.IsVisible);
            foreach(var header in new[]{"Проекция\nШтриховки","Прозрачность","Сечение\nЛинии","Сечение\nШтриховки","Детализация"})
                Check(annotation.Columns.First(x=>Equals(x.Header,header)).Visibility==Visibility.Collapsed,"annotation unsupported column absent: "+header.Replace('\n','/'));
            Check(annotation.Columns.First(x=>Equals(x.Header,"Полутона")).Visibility==Visibility.Visible,"annotation halftone retained");
            var half=All<CheckBox>(annotation).First(x=>x.IsVisible&&Equals(x.Tag,"BatchCategoryHalftone"));
            half.SetCurrentValue(ToggleButton.IsCheckedProperty,(bool?)true);half.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(vm.GraphicsData.AnnotationCategories.Rows[0].Graphics.HalftoneState==true,"annotation halftone source handler editable");
            half.SetCurrentValue(ToggleButton.IsCheckedProperty,(bool?)false);half.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));vm.NotifyDataChanged();Pump();Snapshot(window,"annotation-capabilities");
            tabs.SelectedIndex=3;Pump();var analytical=All<DataGrid>(window).First(x=>x.IsVisible);
            Check(analytical.Columns.First(x=>Equals(x.Header,"Сечение\nЛинии")).Visibility==Visibility.Collapsed,"analytical non-cuttable fixture hides cut column");
            Check(analytical.Columns.First(x=>Equals(x.Header,"Проекция\nШтриховки")).Visibility==Visibility.Visible,"analytical panel capability retains surface column");
            var node=vm.GraphicsData.AnalyticalCategories.Rows[0];var nodeVisual=(DataGridRow)analytical.ItemContainerGenerator.ContainerFromItem(node);
            Check(!All<Button>(nodeVisual).Any(x=>x.IsVisible&&Equals(x.Tag,"EditCategorySurfacePatterns")),"analytical node unsupported surface editor absent");
            Snapshot(window,"analytical-capabilities");
            tabs.SelectedIndex=1;Pump();var grid=All<DataGrid>(window).First(x=>x.IsVisible);var tab=vm.GraphicsData.ModelCategories;
            var root=tab.Rows[0];var child=tab.Rows.First(x=>x.ParentCategoryIdValue==root.CategoryIdValue);
            child.HasChildren=true;child.IsExpanded=true;
            // HasChildren is immutable metadata in production. This stress fixture changes it
            // after loading, so explicitly signal the metadata refresh through its original notifier.
            typeof(NotifyPropertyChangedBase).GetMethod("OnPropertyChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(child,new object[]{"HasChildren"});
            var grandchild=new CategoryOverrideRow {CategoryIdValue=99001,ParentCategoryIdValue=child.CategoryIdValue,Name="Тест вложенности · дополнительная линия",IndentLevel=2,IsVisible=true,AllowsVisibilityControl=true};
            tab.Rows.Insert(tab.Rows.IndexOf(child)+1,grandchild);tab.RefreshRowVisibility();CategoryPresentation.Refresh(grid);Pump();
            Check(child.NameIndent==24&&grandchild.NameIndent==48,"tree measured depth increments24");
            Check(TreeGuides.FollowingSibling(tab,child)&&!TreeGuides.FollowingSibling(tab,grandchild),"tree rail continuation and last sibling termination");
            Check(All<TreeGuides>(grid).Any(x=>x.IsVisible&&x.ActualHeight==44),"tree guides rendered in native rows44");Snapshot(window,"tree-depth-stress");
            Click(Tagged(window,"ToggleCategory"));Check(!child.IsVisibleInTree&&!grandchild.IsVisibleInTree,"parent collapse hides descendants recursively");
            var search=All<TextBox>(window).First(x=>x.IsVisible&&Equals(x.Tag,"CategorySearchInput"));search.Text="Тест вложенности";Click(Tagged(window,"FindCategories"));
            Check(root.IsVisibleInTree&&child.IsVisibleInTree&&grandchild.IsVisibleInTree,"search preserves ancestor path through collapsed root");
            Check(grid.Items.Count==3,"tree search hides unrelated siblings");Snapshot(window,"tree-search-path");
            search.Text="";Click(Tagged(window,"FindCategories"));Click(Tagged(window,"ToggleCategory"));
            tab.Rows.Remove(grandchild);child.HasChildren=false;child.IsExpanded=false;tab.RefreshRowVisibility();CategoryPresentation.Refresh(grid);Pump();
            typeof(NotifyPropertyChangedBase).GetMethod("OnPropertyChanged",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(child,new object[]{"HasChildren"});
            var link=tab.Rows.First(x=>x.CategoryIdValue==(int)Autodesk.Revit.DB.BuiltInCategory.OST_RvtLinks);
            grid.ScrollIntoView(link);Pump();var linkRow=(DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(link);
            Check(All<CheckBox>(linkRow).Any(x=>x.IsVisible&&Equals(x.Tag,"BatchCategoryVisibility")),"global Revit links category visibility retained in Model");Snapshot(window,"global-link-category");
            int linkWeight=link.Graphics.ProjectionLineWeight;
            grid.SelectedItems.Clear();grid.SelectedItems.Add(root);grid.SelectedItems.Add(link);grid.ScrollIntoView(root);Pump();
            ModalAction(window,()=>Click(Tagged(window,"EditCategoryProjectionLines")),editorWindow=> {
                var editor=(GraphicOverrideEditorViewModel)editorWindow.DataContext;editor.Data.ProjectionLineWeight=9;Click(Named<Button>(editorWindow,"ApplyButton"));
            });
            Check(root.Graphics.ProjectionLineWeight==9&&link.Graphics.ProjectionLineWeight==linkWeight,"mixed capability projection batch edits supported row only");
            grid.SelectedItems.Clear();
            tabs.SelectedIndex=3;Pump();analytical=All<DataGrid>(window).First(x=>x.IsVisible);
            var panel=vm.GraphicsData.AnalyticalCategories.Rows.First(x=>x.SupportsSurfacePatterns);int nodePattern=node.Graphics.SurfaceForegroundPatternId;
            analytical.SelectedItems.Add(panel);analytical.SelectedItems.Add(node);analytical.ScrollIntoView(panel);Pump();
            ModalAction(window,()=>Click(Tagged(window,"EditCategorySurfacePatterns")),editorWindow=> {
                var editor=(GraphicOverrideEditorViewModel)editorWindow.DataContext;editor.Data.SurfaceForegroundPatternId=vm.GraphicsData.FillPatterns.First(x=>x.IdValue>0).IdValue;Click(Named<Button>(editorWindow,"ApplyButton"));
            });
            Check(panel.Graphics.SurfaceForegroundPatternId>0&&node.Graphics.SurfaceForegroundPatternId==nodePattern,"mixed capability surface batch edits supported row only");
            analytical.SelectedItems.Clear();tabs.SelectedIndex=1;Pump();grid.SelectedItems.Add(root);grid.SelectedItems.Add(link);
            link.SupportsHalftone=false;bool linkHalf=link.Graphics.Halftone;
            var rootHalf=All<CheckBox>(grid).First(x=>x.IsVisible&&Equals(x.Tag,"BatchCategoryHalftone")&&x.CommandParameter==root);
            rootHalf.SetCurrentValue(ToggleButton.IsCheckedProperty,(bool?)!linkHalf);rootHalf.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(root.Graphics.Halftone!=linkHalf&&link.Graphics.Halftone==linkHalf,"mixed capability halftone batch preserves unsupported row");
            link.SupportsHalftone=true;grid.SelectedItems.Clear();
            All<ScrollViewer>(grid).First().ScrollToTop();Pump();
            // Finish this isolated edit scenario before the target-selection/dirty-guard suite.
            vm.GraphicsData.StartTrackingChanges();vm.NotifyDataChanged();
            Check(!vm.GraphicsData.IsDirty,"capability scenario resets fixture baseline before target selection tests");
        }
    }
}
