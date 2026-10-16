using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Reports;
using SAB.UI.Notifications;
using V = SAB.InteriorElevations.Views;
using VM = SAB.InteriorElevations.ViewModels;
using Session = SAB.UiShowcase.Elevations.ElevationSession;
using Point = System.Windows.Point;

namespace SAB.UiShowcase
{
    // Actual SAB.dll constructors and installed loose XAML. Only option data is supplied by this host.
    internal static class RedesignProduction
    {
        static readonly List<string> Log = new List<string>();
        static string output;
        static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject => Elevations.ElevationPreview.Visual<T>(root);
        static T Find<T>(Window w,string name) where T:FrameworkElement => Session.Find<T>(w,name);
        static void Check(bool ok,string message) { Log.Add((ok?"PASS ":"FAIL ")+message); if(!ok)throw new InvalidOperationException(message); }
        static void Pump() => Elevations.ElevationPreview.Pump(160);
        static void Show(Window w) { w.ShowInTaskbar=false;w.Show();Pump();w.UpdateLayout(); }
        static void Shot(Window w,string name)
        {
            var body=(FrameworkElement)w.Content;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth),(int)Math.Ceiling(body.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(body);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(output,name+".png")))png.Save(stream);
            Log.Add("CAPTURE "+name+" | "+body.ActualWidth+"x"+body.ActualHeight+" DIP | actualType="+w.GetType().FullName+" | actualAssembly="+w.GetType().Assembly.GetName().Name);
        }
        internal static VM.ElevationSettingsViewModel Settings()
        {
            var vm=new VM.ElevationSettingsViewModel(null);
            foreach(var name in new[]{"ElevationViewFamilyTypes","ViewTemplates","TitleBlockTypes","ViewportTypes","PlanCornerMarkTypes","SheetCornerMarkTypes","RoomPlanViewTemplates","RoomPlanRoomTagTypes","ExistingSheets"})
            {
                var options=(System.Collections.ObjectModel.ObservableCollection<VM.RevitElementOption>)vm.GetType().GetProperty(name).GetValue(vm);
                options.Add(new VM.RevitElementOption{Id=new ElementId(401),DisplayName=name=="ExistingSheets"?"АР-101 · Развёртки помещений":"АР · Типовой образец"});
                var selected=vm.GetType().GetProperty("Selected"+name.TrimEnd('s'));
                if(selected!=null)selected.SetValue(vm,options[0]);
            }
            vm.SelectedElevationViewFamilyType=vm.ElevationViewFamilyTypes[0];vm.SelectedViewTemplate=vm.ViewTemplates[0];vm.SelectedTitleBlockType=vm.TitleBlockTypes[0];vm.SelectedViewportType=vm.ViewportTypes[0];vm.SelectedPlanCornerMarkType=vm.PlanCornerMarkTypes[0];vm.SelectedSheetCornerMarkType=vm.SheetCornerMarkTypes[0];vm.SelectedRoomPlanViewTemplate=vm.RoomPlanViewTemplates[0];vm.SelectedRoomPlanRoomTagType=vm.RoomPlanRoomTagTypes[0];
            vm.SetNamingPreviewContexts(new[]{new VM.ElevationNamingPreviewContext{RoomData=Session.Room(201,"101","Переговорная"),StartPointNumber=1,EndPointNumber=2}});
            return vm;
        }
        static V.ElevationDecorationWindow Decoration(int mask=7)
        {
            var w=new V.ElevationDecorationWindow(null,new ElevationDecorationSettings{PlaceSpotElevations=true,PlaceFloorSpots=(mask&1)!=0,PlaceCeilingSpots=(mask&2)!=0,PlaceSlabSpots=(mask&4)!=0,PlaceTags=false,PlaceDimensions=false,FloorSpotElevationTypeId=new ElementId(401),OverheadSpotElevationTypeId=new ElementId(401)});
            Session.PopulateDecoration(w);
            typeof(V.ElevationDecorationWindow).GetField("_catalog",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,Session.CreateCatalog());
            foreach(var c in Session.Logical<ComboBox>(w))c.SelectedIndex=1;
            return w;
        }
        static void Common(Window w)
        {
            Check(w.GetType().Assembly==typeof(V.ElevationSettingsWindow).Assembly,"Actual production class: "+w.GetType().Name);
            var help=Visual<Button>(w).Where(x=>x.IsVisible&&Equals(x.Content,"Инструкция")).ToList();
            Check(help.Count==1&&File.Exists(SAB.UI.FluentLight.Instructions.Resolve(SAB.UI.FluentLight.Instructions.GetFile(help[0]))),"One local instruction link: "+w.Title);
            Check(help[0].Focus()&&help[0].IsKeyboardFocused,"Actual footer keyboard focus: "+w.Title);
            foreach(var tab in Visual<TabItem>(w).Where(x=>x.IsVisible))Check(tab.ActualHeight>=40&&tab.FontSize==14,"Actual tab40DIP/text14");
            foreach(var grid in Visual<DataGrid>(w).Where(x=>x.IsVisible))Check(grid.Parent is Border border&&border.Tag?.ToString()=="CollectionFrame","Actual collection has own border");
        }
        static void Modal(Window w,string button,Action before,Action after)
        {
            w.Loaded+=(s,e)=>w.Dispatcher.BeginInvoke(new Action(()=>{before?.Invoke();Find<Button>(w,button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}),DispatcherPriority.ApplicationIdle);
            w.ShowDialog();after();
        }
        internal static int Run(string directory)
        {
            output=directory;Directory.CreateDirectory(output);
            string pref=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SAB","WindowSizes");
            var originals=Directory.Exists(pref)?Directory.GetFiles(pref,"InteriorElevations*.json").ToDictionary(p=>p,File.ReadAllBytes):new Dictionary<string,byte[]>();
            try
            {
                var command=new SAB.InteriorElevations.Commands.CreateInteriorElevationsCommand();Check(string.IsNullOrEmpty((string)command.GetType().GetMethod("GetDefaultWindowInfoText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(command,null)),"Actual command does not inject instructions into settings window");
                var vm=Settings();var main=new V.ElevationSettingsWindow(vm,true,"101 · Переговорная · 4 линии","");Show(main);Common(main);
                var tabs=Find<TabControl>(main,"SettingsTabControl");
                for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Pump();Common(main);Shot(main,"settings-tab-"+i);}
                tabs.SelectedIndex=1;Pump();var target=Find<GroupBox>(main,"TargetSheetCard");Check(!target.IsVisible,"No-sheet mode hides entire target sheet card");
                vm.CreateNewSheetMode=true;Pump();Check(target.IsVisible,"New-sheet mode restores target sheet card");Shot(main,"settings-sheet-new");
                vm.ExistingSheetMode=true;Pump();Check(target.IsVisible,"Existing-sheet mode restores target sheet selector");Shot(main,"settings-sheet-existing");
                vm.DoNotPlaceViewsOnSheetMode=true;Pump();Check(!target.IsVisible,"Switching back to no-sheet removes empty card");
                tabs.SelectedIndex=0;main.Width=main.MinWidth;main.Height=main.MinHeight;Pump();Common(main);Shot(main,"settings-minimum");
                var formula=Find<RichTextBox>(main,"ElevationNameFormulaEditor");formula.Document.Blocks.Clear();formula.Document.Blocks.Add(new Paragraph(new Run("Длинная русская подпись_[Номер помещения]_[Имя помещения]_[Начальный угол]")));Pump();Check(vm.NamingPreviewItems[0].ViewName.Contains("Длинная")&&vm.NamingPreviewItems[0].ViewName.Contains("101"),"Actual formula handler updates real naming service");Shot(main,"settings-long-formula");main.Close();
                var reopened=new V.ElevationSettingsWindow(vm,true,"101 · 4 линии","");Show(reopened);Check(!vm.CornerMarksOnlyCornerNumber||Find<RadioButton>(reopened,"CornerMarksOnlyCornerNumberRadio").IsChecked==true,"Actual reopened instance retains mark choice");reopened.Close();
                var decoration=Decoration();Show(decoration);Common(decoration);
                Check(!Visual<TabControl>(decoration).Any(),"Actual decoration has one scrolling form");
                var spots=Find<GroupBox>(decoration,"SpotSelectionCard");var dims=Find<GroupBox>(decoration,"DimensionSelectionCard");var tags=Find<GroupBox>(decoration,"TagSelectionCard");
                var sp=spots.TranslatePoint(new Point(),decoration);var dp=dims.TranslatePoint(new Point(),decoration);var tp=tags.TranslatePoint(new Point(),decoration);
                Check(Math.Abs(sp.X-dp.X)<=1&&Math.Abs(dp.Y-sp.Y-spots.ActualHeight-16)<=1&&Math.Abs(tp.Y-sp.Y)<=1,"Actual three annotation selection cards");
                Shot(decoration,"decoration-normal");decoration.Width=decoration.MinWidth;decoration.Height=decoration.MinHeight;Pump();Common(decoration);Shot(decoration,"decoration-minimum");
                foreach(var sv in Visual<ScrollViewer>(decoration).Where(x=>x.TemplatedParent==null))sv.ScrollToEnd();Pump();
                foreach(string category in new[]{"Wall","Floor","Ceiling","Plinth","Door","Window"})
                {
                    var combo=Find<ComboBox>(decoration,category+"TagTypeComboBox");var leader=Find<CheckBox>(decoration,category+"LeaderCheckBox");
                    var a=combo.TranslatePoint(new Point(combo.ActualWidth,combo.ActualHeight/2),decoration);var b=leader.TranslatePoint(new Point(0,leader.ActualHeight/2),decoration);
                    Check(Math.Abs(b.X-a.X-12)<=1&&Math.Abs(b.Y-a.Y)<=1,"Actual dropdown/leader12DIP: "+category);
                }
                Shot(decoration,"decoration-tags");Find<TextBox>(decoration,"TagOffsetTextBox").Text="17,";Find<CheckBox>(decoration,"PlaceCeilingSpotsCheckBox").IsChecked=false;var draft=decoration.CaptureDraft();decoration.Close();
                var restored=Decoration();restored.RestoreDraft(draft);Show(restored);Check(Find<TextBox>(restored,"TagOffsetTextBox").Text=="17,"&&Find<CheckBox>(restored,"PlaceCeilingSpotsCheckBox").IsChecked==false,"Actual catalog roundtrip preserves unfinished raw text and category choice");restored.Close();
                for(int mask=1;mask<8;mask++)
                {
                    var w=Decoration(mask);Modal(w,"OkButton",null,()=>Check(w.SelectedSettings!=null&&w.SelectedSettings.PlaceFloorSpots==((mask&1)!=0)&&w.SelectedSettings.PlaceCeilingSpots==((mask&2)!=0)&&w.SelectedSettings.PlaceSlabSpots==((mask&4)!=0),"Actual production UI→settings category mask "+mask));
                }
                var catalog=new V.ElevationDecorationCatalogWindow(Session.CreateCatalog(),ElevationDecorationCatalogRole.FinishFloor);Show(catalog);Common(catalog);Shot(catalog,"catalog-normal");catalog.Width=catalog.MinWidth;catalog.Height=catalog.MinHeight;Pump();Shot(catalog,"catalog-minimum");catalog.Close();
                Session.RoomRows.Clear();for(int i=0;i<24;i++){var item=new MultiRoomSelectionItem();Session.FillRoom(item,201+i,"Очень длинное русское имя помещения №"+(i+1));Session.RoomRows.Add(item);}
                var rooms=new V.MultiRoomSelectionWindow(Session.RoomRows);Show(rooms);Common(rooms);var table=Find<DataGrid>(rooms,"RoomsDataGrid");double before=table.Columns[2].ActualWidth;var fixedWidths=table.Columns.Where(c=>!c.Width.IsStar).Select(c=>c.ActualWidth).ToArray();rooms.Width=1540;Pump();Check(table.Columns[2].ActualWidth>before&&fixedWidths.SequenceEqual(table.Columns.Where(c=>!c.Width.IsStar).Select(c=>c.ActualWidth)),"Actual resize grows text columns and retains numeric/actions widths");Shot(rooms,"rooms-wide");rooms.Width=rooms.MinWidth;rooms.Height=rooms.MinHeight;Pump();table.ScrollIntoView(table.Items[table.Items.Count-1]);Pump();Shot(rooms,"rooms-last-row-minimum");rooms.Close();Session.RoomRows.Clear();
                var windows=new Window[]{new V.ElevationCropAdjustmentWindow(),new V.LineGroupSelectionWindow(),new V.LineGroupNextActionWindow(2),new V.CropByExampleActionWindow(),new V.CropByExampleLineCreationWindow(null,null)};
                foreach(var w in windows)
                {
                    Show(w);Common(w);if(!(w is V.CropByExampleLineCreationWindow))Check(w.ResizeMode==ResizeMode.NoResize&&w.SizeToContent==SizeToContent.WidthAndHeight,"Actual compact content-sized dialog: "+w.Title);
                    if(w is V.ElevationCropAdjustmentWindow){var rect=Visual<System.Windows.Shapes.Rectangle>(Find<System.Windows.Controls.Grid>(w,"ViewBoundaryDiagram")).Single();Check(rect.StrokeDashArray.Count==2&&((SolidColorBrush)rect.Fill).Color.A==0,"Actual dashed transparent spatial boundary");}
                    Shot(w,w.GetType().Name);w.Close();
                }
                var adjust=new V.ElevationCropAdjustmentWindow();Modal(adjust,"ApplyButton",()=>Find<TextBox>(adjust,"LeftTextBox").Text="-25.5",()=>Check(adjust.SelectedSettings.LeftMm==-25.5,"Actual boundary apply retains negative signed value"));
                var mode=new V.LineGroupSelectionWindow();Modal(mode,"MultipleGroupsButton",null,()=>Check(mode.IsMultipleGroups,"Actual line group action"));
                var next=new V.LineGroupNextActionWindow(2);Modal(next,"FinishButton",null,()=>Check(!next.IsNextGroupAction,"Actual next group finish action"));
                foreach(string state in new[]{"success","warning","error"})
                {
                    var data=new ElevationCreationNotificationData{CreatedViews=state=="error"?0:4,FailedViews=state=="error"?4:0,PlacedViewports=state=="error"?0:4,Warnings=new List<string>()};if(state!="success")data.Warnings.Add("Не удалось расставить марку угла: тип марки не выбран.");
                    var message=ElevationCreationReportService.BuildNotification(data);var notification=NotificationStack.Show(IntPtr.Zero,message);Pump();
                    Check(Math.Abs(notification.Width-416)<=1,"Actual common notification width416: "+state);
                    Check(Visual<TextBlock>(notification).Any(t=>t.Text==message.Outcome&&t.FontSize==16&&t.FontWeight==FontWeights.Bold),"Actual common status16Bold: "+state);Shot(notification,"notification-"+state);notification.Close();
                }
                Log.Add("NOT RUN Revit Document geometry, transactions, interactive picking and ExternalEvent dispatch; physical keyboard/OS monitor transitions.");
                Log.Add("COMPLETED");return 0;
            }
            catch(Exception e){Log.Add("ERROR "+e);return 1;}
            finally
            {
                foreach(Window w in Application.Current.Windows.Cast<Window>().ToArray())w.Close();
                if(Directory.Exists(pref))foreach(string p in Directory.GetFiles(pref,"InteriorElevations*.json"))if(!originals.ContainsKey(p))File.Delete(p);
                foreach(var item in originals)File.WriteAllBytes(item.Key,item.Value);
                File.WriteAllLines(Path.Combine(output,"verification.txt"),Log);
            }
        }
    }
}
