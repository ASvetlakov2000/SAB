using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Reports;
using SAB.UI.FluentLight;
using SAB.UI.Notifications;
using V = SAB.UiShowcase.Elevations.Views;
using VM = SAB.UiShowcase.Elevations.ViewModels;
using Control = System.Windows.Controls.Control;
using Point = System.Windows.Point;
using HelpFiles = SAB.UI.FluentLight.Instructions;
using Proposal = SAB.UiShowcase.NotificationProposal;

namespace SAB.UiShowcase.Elevations
{
    // Only the explicitly absent Document/session boundary is simulated. Generated code
    // keeps source models, validation, formula editing, dependencies and result actions.
    public static class ElevationSession
    {
        public static ElevationDecorationCatalog Catalog = CreateCatalog();
        public static Window Owner;
        public static string LastAction = "";
        public static bool HasSelection;
        public static int CropStage;
        public static readonly ObservableCollection<MultiRoomSelectionItem> RoomRows = new ObservableCollection<MultiRoomSelectionItem>();
        static VM.RevitElementOption Option(int id, string name) => new VM.RevitElementOption { Id = new ElementId(id), DisplayName = name };
        public static void PopulateSettings(VM.ElevationSettingsViewModel vm)
        {
            vm.ElevationViewFamilyTypes.Add(Option(101, "Развёртка интерьера"));
            vm.ViewTemplates.Add(Option(-1, "<Не выбран>"));vm.ViewTemplates.Add(Option(102, "АР · Развёртки помещений"));
            vm.TitleBlockTypes.Add(Option(103, "А2 · Альбомная"));vm.ViewportTypes.Add(Option(104, "Заголовок вида · стандартный"));
            vm.PlanCornerMarkTypes.Add(Option(105, "SAB · Марка угла на плане"));vm.SheetCornerMarkTypes.Add(Option(106, "SAB · Марка угла на листе"));
            vm.RoomPlanViewTemplates.Add(Option(-1, "<Не выбран>"));vm.RoomPlanViewTemplates.Add(Option(107, "АР · План-схема помещения"));
            vm.RoomPlanRoomTagTypes.Add(Option(108, "SAB · Марка помещения"));
            vm.ExistingSheets.Add(Option(109, "АР-101 · Развёртки помещений"));vm.ExistingSheets.Add(Option(110, "АР-102 · Развёртки второго этажа"));
            vm.SelectedElevationViewFamilyType=vm.ElevationViewFamilyTypes[0];vm.SelectedViewTemplate=vm.ViewTemplates[1];
            vm.SelectedTitleBlockType=vm.TitleBlockTypes[0];vm.SelectedViewportType=vm.ViewportTypes[0];
            vm.SelectedPlanCornerMarkType=vm.PlanCornerMarkTypes[0];vm.SelectedSheetCornerMarkType=vm.SheetCornerMarkTypes[0];
            vm.SelectedRoomPlanViewTemplate=vm.RoomPlanViewTemplates[1];vm.SelectedRoomPlanRoomTagType=vm.RoomPlanRoomTagTypes[0];
        }
        public static VM.ElevationSettingsViewModel Settings()
        {
            var vm=new VM.ElevationSettingsViewModel(null);
            vm.SetNamingPreviewContexts(new[]{new VM.ElevationNamingPreviewContext{RoomData=Room(201,"101","Переговорная"),StartPointNumber=1,EndPointNumber=2}});
            return vm;
        }
        public static bool PickSheetPlacement(VM.ElevationSettingsViewModel vm,bool accepted)
        {
            if(!accepted)return false;
            vm.SetSheetStartPointFromRevitPoint(new XYZ(150/304.8,200/304.8,0));
            if(vm.UseExistingSheet&&vm.CreateRoomPlanScheme)vm.SetPreviewExistingRoomPlanSelection(new ElementId(701),"План-схема помещения 101 · существующий вид");
            LastAction="Preview: точка листа 150; 200 мм"+(vm.HasExistingRoomPlanSelection?"; план-схема №701 выбрана.":".");
            return true;
        }
        public static RoomData Room(int id,string number,string name)=>new RoomData{RoomElementId=new ElementId(id),LinkInstanceId=ElementId.InvalidElementId,RoomNumber=number,RoomName=name,LevelId=new ElementId(301)};
        public static void FillRoom(MultiRoomSelectionItem item,int id=201,string displayName=null)
        {
            // Four null slots carry counts only; no geometry is fabricated or dereferenced.
            var lines=Enumerable.Repeat<DetailLine>(null,4).ToList();
            item.ApplySelection(new List<List<DetailLine>>{lines},lines,Room(id,(id-100).ToString(),displayName??(id==201?"Переговорная":"Кабинет руководителя")));
        }
        public static ElevationDecorationCatalog CreateCatalog()
        {
            var c=new ElevationDecorationCatalog();
            foreach(ElevationDecorationCatalogRole role in Enum.GetValues(typeof(ElevationDecorationCatalogRole)))
                c.Entries.Add(new ElevationDecorationCatalogEntry{Key="fixture-"+role,Role=role,TypeUniqueId="fixture-type-"+role,SourceDocumentKey="fixture-host",SourceDocumentTitle="Архитектурная модель",FamilyName=ElevationDecorationCatalogRoleNames.GetDisplayName(role),TypeName="Типовой"});
            return c;
        }
        public static void PopulateDecoration(Window w)
        {
            foreach(var c in Logical<ComboBox>(w).Where(x=>x.Name.EndsWith("ComboBox")))
            {
                c.ItemsSource=new List<AnnotationTypeOption>{new AnnotationTypeOption{Id=ElementId.InvalidElementId,DisplayName="<Не выбран>"},new AnnotationTypeOption{Id=new ElementId(401),DisplayName=c.Name.Contains("Level")?"Этаж 01 · 0,000":"АР · " + (c.Name.Contains("Dimension")?"Размеры 2,5 мм":c.Name.Contains("Spot")?"Отметка уровня":"Марка элемента")}};
                c.SelectedIndex=0;
            }
        }
        public static IEnumerable<T> Logical<T>(DependencyObject root) where T:DependencyObject
        {
            if(root is T t)yield return t;
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var x in Logical<T>(child))yield return x;
        }
        public static T Find<T>(Window w,string name) where T:FrameworkElement=>Logical<T>(w).First(x=>x.Name==name);
        public static void Warning(string module,string message)
        {
            LastAction="Проверка: "+message;
            if(Owner?.IsVisible==true)NotificationStack.Show(new WindowInteropHelper(Owner).Handle,new NotificationMessage("SAB · Развёртки","Проверьте настройки",NotificationTone.Warning,message));
        }
        public static void RequestCrop(V.CropByExampleLineCreationWindow w,CropByExampleOperation operation)
        {
            LastAction="Preview ExternalEvent: "+operation;
            if(operation==CropByExampleOperation.PickLine){CropStage=1;w.UpdateStatus("Выбрана линия №501.","",true,false);}
            else if(operation==CropByExampleOperation.CreateView||operation==CropByExampleOperation.PickExistingLineAndCreateView){CropStage=2;w.UpdateStatus("Развёртка-пример создана.","",true,true);}
            else {CropStage=3;w.UpdateStatus("Границы приняты: сверху 3200 мм, снизу 0 мм.","",true,true);Find<Button>(w,"AcceptCropButton").IsEnabled=false;}
        }
        public static void EditCatalog(Window owner,ElevationDecorationCatalog catalog)
        {
            for(;;)
            {
                var w=new V.ElevationDecorationCatalogWindow(catalog,ElevationDecorationCatalogRole.FinishFloor){Owner=owner};
                FitCatalog(w);w.ShowDialog();
                if(w.RequestedAction==V.ElevationDecorationCatalogWindowAction.None)return;
                ApplyCatalog(w,owner);
            }
        }
        public static void ApplyCatalog(V.ElevationDecorationCatalogWindow w,Window owner)
        {
            var role=w.SelectedRole;var action=w.RequestedAction;
            if(action==V.ElevationDecorationCatalogWindowAction.ClearRole)
            {
                if(Confirmation(owner,"Очистить роль каталога?",ElevationDecorationCatalogRoleNames.GetDisplayName(role)))Catalog.Entries.RemoveAll(x=>x.Role==role);
            }
            else if(action==V.ElevationDecorationCatalogWindowAction.RemoveSelected)Catalog.Entries.RemoveAll(x=>x.Key==w.SelectedEntryKey);
            else
            {
                bool linked=action==V.ElevationDecorationCatalogWindowAction.AddLinked;
                string key="picked-"+role+"-"+linked;
                if(!Catalog.Entries.Any(x=>x.Key==key))Catalog.Entries.Add(new ElevationDecorationCatalogEntry{Key=key,Role=role,TypeUniqueId=key,SourceDocumentKey=linked?"fixture-link":"fixture-host",SourceDocumentTitle=linked?"Связанный файл · Интерьеры":"Архитектурная модель",FamilyName=ElevationDecorationCatalogRoleNames.GetDisplayName(role),TypeName="Выбранный тип"});
            }
            LastAction="Каталог: "+action;
        }
        public static void FitCatalog(Window w)
        {
            Action fit=()=>{var list=Find<DataGrid>(w,"EntriesListView");list.Columns[0].Width=new DataGridLength(3,DataGridLengthUnitType.Star);list.Columns[1].Width=new DataGridLength(2,DataGridLengthUnitType.Star);};
            w.Loaded+=(s,e)=>fit();w.SizeChanged+=(s,e)=>fit();
        }
        public static Window Simple(string title,string body,bool question=false)
        {
            var w=new Window{Title="SAB · "+title,Width=680,Height=question?330:560,MinWidth=540,MinHeight=question?300:400,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Background=Brushes.White};
            w.Resources.MergedDictionaries.Add(FluentResources.CreateDictionary());
            var grid=new System.Windows.Controls.Grid();grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            grid.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
            var reportBody=new StackPanel();
            int warningAt=question?-1:body.IndexOf("Предупреждения:",StringComparison.Ordinal);
            string result=warningAt<0?body:body.Substring(0,warningAt).TrimEnd();
            Func<string,bool,Border> framed=(value,warning)=>new Border{Tag=warning?"WarningsFrame":"ReportFrame",BorderBrush=(Brush)FluentResources.Find("SabBrush.Border"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(12),Margin=new Thickness(0,0,0,12),Background=warning?(Brush)FluentResources.Find("SabBrush.TableHeaderBackground"):Brushes.Transparent,Child=new TextBox{Text=value,Style=(Style)FluentResources.Find("SabNotificationTextStyle"),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,Height=double.NaN}};
            reportBody.Children.Add(framed(result,false));
            if(warningAt>=0)reportBody.Children.Add(framed(body.Substring(warningAt),true));
            var report=new ScrollViewer{Content=reportBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,16)};System.Windows.Controls.Grid.SetRow(report,1);grid.Children.Add(report);
            var footer=new DockPanel();System.Windows.Controls.Grid.SetRow(footer,2);grid.Children.Add(footer);
            var help=new Button{Content="Инструкция"};HelpFiles.SetFile(help,"SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html");DockPanel.SetDock(help,Dock.Left);footer.Children.Add(help);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};footer.Children.Add(actions);
            if(question){var no=new Button{Content="Нет",IsCancel=true,IsDefault=true,Margin=new Thickness(8,0,0,0)};no.Click+=(s,e)=>{w.DialogResult=false;};actions.Children.Add(no);}
            var ok=new Button{Content=question?"Да":"Закрыть",IsDefault=!question,Margin=new Thickness(8,0,0,0)};ok.Click+=(s,e)=>{w.DialogResult=question;w.Close();};actions.Children.Add(ok);w.Content=new Border{Background=Brushes.White,Padding=new Thickness(24),Child=grid};return w;
        }
        public static bool Confirmation(Window owner,string title,string body){var w=Simple(title,body,true);w.Owner=owner;return w.ShowDialog()==true;}
    }

    public static class ElevationPreview
    {
        static Window menu;static TextBlock summary;static readonly List<string> log=new List<string>();static string output;
        static readonly string[] names={"ElevationSettingsWindow","ElevationDecorationWindow","ElevationDecorationCatalogWindow","MultiRoomSelectionWindow","ElevationCropAdjustmentWindow","LineGroupSelectionWindow","LineGroupNextActionWindow","CropByExampleActionWindow","CropByExampleLineCreationWindow"};
        public static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject
        {if(root is T t)yield return t;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var x in Visual<T>(VisualTreeHelper.GetChild(root,i)))yield return x;}
        public static void Pump(int ms=220){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Click(Window w,string name)=>ElevationSession.Find<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        static Window New(string n,VM.ElevationSettingsViewModel vm=null)
        {
            switch(n)
            {
                case "ElevationSettingsWindow":return new V.ElevationSettingsWindow(vm??ElevationSession.Settings(),ElevationSession.HasSelection,ElevationSession.HasSelection?"Помещение 101 · Переговорная; линий: 4":"Линии и помещение не выбраны.","");
                case "ElevationDecorationWindow":return new V.ElevationDecorationWindow(null,null);
                case "ElevationDecorationCatalogWindow":var c=new V.ElevationDecorationCatalogWindow(ElevationSession.Catalog,ElevationDecorationCatalogRole.FinishFloor);ElevationSession.FitCatalog(c);return c;
                case "MultiRoomSelectionWindow":return new V.MultiRoomSelectionWindow(ElevationSession.RoomRows);
                case "ElevationCropAdjustmentWindow":return new V.ElevationCropAdjustmentWindow();
                case "LineGroupSelectionWindow":return new V.LineGroupSelectionWindow();
                case "LineGroupNextActionWindow":return new V.LineGroupNextActionWindow(2);
                case "CropByExampleActionWindow":return new V.CropByExampleActionWindow();
                default:return new V.CropByExampleLineCreationWindow(null,null);
            }
        }
        public static Window Menu()
        {
            menu=new Window{Title="SAB · Развёртки · интерактивный макет",Width=840,Height=820,MinWidth=760,MinHeight=660,WindowStartupLocation=WindowStartupLocation.CenterScreen,FontFamily=new FontFamily("Segoe UI"),FontSize=13};menu.Resources.MergedDictionaries.Add(FluentResources.CreateDictionary());menu.SetResourceReference(Control.BackgroundProperty,"SabBrush.WindowBackground");
            ElevationSession.Owner=menu;
            var body=new StackPanel{Margin=new Thickness(24)};body.Children.Add(new TextBlock{Text="Развёртки — комплект окон",FontSize=24,FontWeight=FontWeights.SemiBold});
            body.Children.Add(new TextBlock{Text="Предпросмотр · синтетические данные · Revit не подключён",Margin=new Thickness(0,8,0,20)});
            var flow=new Button{Content="Открыть сценарий создания развёрток",Style=(Style)menu.FindResource("SabPrimaryButtonStyle"),Margin=new Thickness(0,0,0,16)};flow.Click+=(s,e)=>MainFlow();body.Children.Add(flow);
            var labels=new[]{"Настройки развёрток · 4 вкладки","Оформление развёрток · аннотации","Каталог оформления","Список помещений","Смещение границ вида","Выбор групп линий","Следующая группа линий","Обрезка по примеру","Развёртка-пример · немодальный помощник"};
            for(int i=0;i<names.Length;i++){string n=names[i];var b=new Button{Content=labels[i],Margin=new Thickness(0,0,0,8)};b.Click+=(s,e)=>Open(n);body.Children.Add(b);}
            var extras=new WrapPanel{Margin=new Thickness(0,8,0,12)};
            foreach(var label in new[]{"Прогресс","Результат создания","Подтверждение","Уведомления"}){string item=label;var b=new Button{Content=item,Margin=new Thickness(0,0,8,8)};b.Click+=(s,e)=>Extra(item);extras.Children.Add(b);}body.Children.Add(extras);
            summary=new TextBlock{Text="Макет ожидает согласования. Рабочие окна SAB не заменены.",TextWrapping=TextWrapping.Wrap};body.Children.Add(summary);
            var scroller=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};menu.Content=scroller;return menu;
        }
        static bool Open(string n)
        {
            var w=New(n);w.Owner=menu;
            if(n=="CropByExampleLineCreationWindow"){ElevationSession.CropStage=0;w.Show();return false;}
            bool accepted=w.ShowDialog()==true;
            if(w is V.ElevationDecorationCatalogWindow c&&c.RequestedAction!=V.ElevationDecorationCatalogWindowAction.None){ElevationSession.ApplyCatalog(c,menu);return Open(n);}
            if(w is V.MultiRoomSelectionWindow rooms)
            {
                if(rooms.RequestedAction==V.MultiRoomSelectionWindowAction.PickSelection){if(PickGroups())ElevationSession.FillRoom(rooms.RequestedRow,201+ElevationSession.RoomRows.IndexOf(rooms.RequestedRow));return Open(n);}
                return accepted&&rooms.RequestedAction==V.MultiRoomSelectionWindowAction.Transfer&&new SAB.InteriorElevations.ViewModels.MultiRoomSelectionViewModel(ElevationSession.RoomRows).TryValidateTransfer(out _);
            }
            if(w is V.CropByExampleActionWindow crop&&crop.RequestedAction!=V.CropByExampleAction.None){var helper=new V.CropByExampleLineCreationWindow(null,null){Owner=menu};if(crop.RequestedAction==V.CropByExampleAction.CreateLine)helper.SetCreateLineMode();else helper.SetExistingLineMode();helper.Show();if(crop.RequestedAction==V.CropByExampleAction.PickLine)ElevationSession.RequestCrop(helper,CropByExampleOperation.PickExistingLineAndCreateView);}
            if(summary!=null)summary.Text=ElevationSession.LastAction.Length>0?ElevationSession.LastAction:"Окно закрыто. Рабочая модель не изменена.";
            return accepted;
        }
        static bool PickGroups()
        {
            var mode=new V.LineGroupSelectionWindow{Owner=menu};if(mode.ShowDialog()!=true)return false;
            if(mode.IsMultipleGroups)for(int i=1;i<20;i++){var next=new V.LineGroupNextActionWindow(i){Owner=menu};if(next.ShowDialog()!=true)return false;if(!next.IsNextGroupAction)break;}
            ElevationSession.LastAction="Выбор возвращён: помещение 101, 4 линии (демонстрационные данные).";return true;
        }
        static void MainFlow()
        {
            var vm=ElevationSession.Settings();
            for(;;)
            {
                var w=(V.ElevationSettingsWindow)New("ElevationSettingsWindow",vm);w.Owner=menu;w.ShowDialog();
                switch(w.RequestedAction)
                {
                    case V.ElevationSettingsWindowAction.Cancel:return;
                    case V.ElevationSettingsWindowAction.PickSelection:
                        if(vm.IsMultipleRoomsOnSheet){ElevationSession.HasSelection=false;ElevationSession.HasSelection=Open("MultiRoomSelectionWindow");}else if(PickGroups())ElevationSession.HasSelection=true;
                        break;
                    case V.ElevationSettingsWindowAction.PickSheetPoint:ElevationSession.PickSheetPlacement(vm,ElevationSession.Confirmation(menu,"Демонстрационный выбор на листе","Точка: 150; 200 мм.\n"+(vm.UseExistingSheet&&vm.CreateRoomPlanScheme?"План-схема: существующий вид №701.":"")));break;
                    case V.ElevationSettingsWindowAction.PickRoomPlanPoint:vm.UseManualRoomPlanPosition=true;vm.RoomPlanOffsetRightMmText="20";vm.RoomPlanOffsetBottomMmText="20";break;
                    case V.ElevationSettingsWindowAction.PickCropByExample:if(!Open("CropByExampleActionWindow"))break;summary.Text="Открыта отдельная modeless-сессия примера. Основная команда завершена, как в SAB.";return;
                    case V.ElevationSettingsWindowAction.Create:Extra("Результат создания");return;
                }
            }
        }
        static NotificationMessage CreationResult(string scenario)
        {
            var data=new ElevationCreationNotificationData{CreatedViews=4,PlanCornerMarks=4,SheetCornerMarks=8,SheetPlacementRequested=true,HasTargetSheet=true,SheetWasCreated=true,TargetSheet="ELV-101 | Развёртки стен пом.101",PlacedViewports=4};
            if(scenario=="warning")data.Warnings=new[]{"Не удалось разместить план-схему: тип видового экрана не выбран.","Марка угла 3 на плане не размещена: параметр доступен только для чтения."};
            if(scenario=="error"){data.CreatedViews=0;data.FailedViews=4;data.PlanCornerMarks=0;data.SheetCornerMarks=0;data.HasTargetSheet=false;data.PlacedViewports=0;data.FailedViewDetails=new[]{"Развёртка №1: не удалось получить границы вида."};}
            if(scenario=="long")data.Warnings=Enumerable.Range(1,24).Select(i=>"Развёртка №"+i+": длинное предупреждение о невозможности разместить марку угла в помещении с длинным русским названием, требуется проверить выбранный тип семейства.").ToArray();
            return ElevationCreationReportService.BuildNotification(data);
        }
        static Proposal.NotificationStack ShowCreationNotification(string scenario,IntPtr owner)
        {
            var message=CreationResult(scenario);
            return Proposal.NotificationStack.Show(owner,new Proposal.NotificationMessage(message.Module,message.Outcome,(Proposal.NotificationTone)(int)message.Tone,message.Summary,message.ElementIds,message.Support,message.Warnings));
        }
        static void Extra(string item)
        {
            if(item=="Уведомления")
            {
                ShowNotificationProposal(new WindowInteropHelper(menu).Handle);return;
            }
            if(item=="Подтверждение"){ElevationSession.Confirmation(menu,"Очистить роль каталога?","Чистовой пол");return;}
            if(item=="Прогресс")
            {
                var w=new V.CreateViewsAndSheetsProgressWindow(new List<string>{"Строим развёртки по выбранным линиям."},"SAB · Развёртки","Создание развёрток"){Owner=menu};w.Report(new SAB.CreateViewsAndSheets.Models.CreateViewsAndSheetsProgressInfo{Stage="Создание развёрток",Details="Помещение 1 из 1: Спальня. Линия 5 из 12.",CurrentStep=5,TotalSteps=18,CreatedItems=5,ProcessedItems=5,TotalItems=18});w.Show();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};timer.Tick+=(s,e)=>{timer.Stop();w.AllowCloseAndClose();};timer.Start();return;
            }
            ShowCreationNotification("warning",new WindowInteropHelper(menu).Handle);
        }
        static void Check(bool ok,string message){log.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new InvalidOperationException(message);}
        static void Show(Window w){w.ShowInTaskbar=false;w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=20;w.Top=20;w.Show();Pump();}
        static void Shot(Window w,string name,double dpi=96)
        {
            w.UpdateLayout();Pump();var root=(FrameworkElement)w.Content;var b=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*dpi/96),(int)Math.Ceiling(root.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);b.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var stream=File.Create(Path.Combine(output,name+".png")))png.Save(stream);log.Add("CAPTURE "+name+" | "+root.ActualWidth+"x"+root.ActualHeight+" DIP | nativeDpi="+VisualTreeHelper.GetDpi(w).PixelsPerInchX+" renderDpi="+dpi);
        }
        static void FrameChecks(Window w)
        {
            if(w is V.CreateViewsAndSheetsProgressWindow){Check(ElevationSession.Find<ProgressBar>(w,"MainProgressBar").Height==16&&w.ResizeMode==ResizeMode.NoResize,"Incumbent progress16DIP and fixed window retained");return;}
            var help=Visual<Button>(w).Where(x=>x.IsVisible&&Equals(x.Content,"Инструкция")).ToList();Check(help.Count==1&&File.Exists(HelpFiles.Resolve(HelpFiles.GetFile(help[0]))),w.Title+": one footer Help resolves installed module HTML");
            var footer=help[0].TranslatePoint(new Point(0,help[0].ActualHeight),w);Check(footer.Y<=w.ActualHeight,w.Title+": footer remains inside viewport");
            foreach(var table in Visual<DataGrid>(w).Where(x=>x.IsVisible)){Check(table.RowHeight==44&&table.ColumnHeaderHeight==38,"Shared table row/header geometry");Check(table.Parent is Border b&&b.Tag?.ToString()=="CollectionFrame","Table has independent rounded frame");}
            foreach(var table in Visual<DataGrid>(w).Where(x=>x.IsVisible))
            {
                var row=Visual<DataGridRow>(table).FirstOrDefault(r=>r.IsVisible);
                if(row==null)continue;
                foreach(var cell in Visual<DataGridCell>(row).Where(c=>c.IsVisible&&c.Column.Header?.ToString()!="Действия"))
                {
                    var header=Visual<DataGridColumnHeader>(table).FirstOrDefault(h=>h.Column==cell.Column);
                    var value=Visual<TextBlock>(cell).FirstOrDefault(t=>!string.IsNullOrEmpty(t.Text));
                    if(header==null||value==null)continue;
                    bool centered=new[]{"№","Групп линий","Выбрано линий","Статус"}.Contains(cell.Column.Header.ToString());
                    double expected=header.TranslatePoint(new Point(centered?header.ActualWidth/2:header.Padding.Left,0),table).X;
                    double actual=value.TranslatePoint(new Point(centered?value.ActualWidth/2:0,0),table).X;
                    Check(Math.Abs(actual-expected)<=1,"Header/value axis within one DIP: "+cell.Column.Header);
                }
                var actionColumn=table.Columns.FirstOrDefault(c=>Equals(c.Header,"Действия"));
                if(actionColumn!=null&&row!=null)
                {
                    var cell=Visual<DataGridCell>(row).First(c=>c.Column==actionColumn);var buttons=Visual<Button>(cell).Where(c=>c.IsVisible).ToList();
                    var header=Visual<DataGridColumnHeader>(table).First(c=>c.Column==actionColumn);
                    if(buttons.Count>0){double left=buttons.First().TranslatePoint(new Point(),table).X,right=buttons.Last().TranslatePoint(new Point(buttons.Last().ActualWidth,0),table).X;
                    Check(Math.Abs((left+right)/2-header.TranslatePoint(new Point(header.ActualWidth/2,0),table).X)<=1,"Actions header/group center within1DIP");}
                }
            }
            if(w is V.ElevationDecorationWindow)
            {
                Check(!ElevationSession.Logical<TabControl>(w).Any(),"Decoration is one form without tabs");
                var spots=ElevationSession.Find<GroupBox>(w,"SpotSelectionCard");var dims=ElevationSession.Find<GroupBox>(w,"DimensionSelectionCard");var tags=ElevationSession.Find<GroupBox>(w,"TagSelectionCard");
                var sp=spots.TranslatePoint(new Point(),w);var dp=dims.TranslatePoint(new Point(),w);var tp=tags.TranslatePoint(new Point(),w);
                Check(Math.Abs(sp.X-dp.X)<=1&&Math.Abs(dp.Y-sp.Y-spots.ActualHeight-16)<=1,"Spot and dimension cards stack left with16DIP gap");
                Check(Math.Abs(tp.Y-sp.Y)<=1&&tp.X>=sp.X+spots.ActualWidth+15&&Math.Abs(tp.Y+tags.ActualHeight-dp.Y-dims.ActualHeight)<=1,"Tag selection card occupies right column across both rows");
                foreach(var category in new[]{"Wall","Floor","Ceiling","Plinth","Door","Window"})
                {
                    var combo=ElevationSession.Find<ComboBox>(w,category+"TagTypeComboBox");var leader=ElevationSession.Find<CheckBox>(w,category+"LeaderCheckBox");
                    Point end=combo.TranslatePoint(new Point(combo.ActualWidth,combo.ActualHeight/2),w), start=leader.TranslatePoint(new Point(0,leader.ActualHeight/2),w);
                    Check(Math.Abs(start.X-end.X-12)<=1,"Tag dropdown/leader gap12DIP: "+category);
                    Check(Math.Abs(start.Y-end.Y)<=1,"Tag dropdown/leader center axis: "+category);
                }
            }
            if(w is V.ElevationCropAdjustmentWindow)
            {
                var diagram=ElevationSession.Find<System.Windows.Controls.Grid>(w,"ViewBoundaryDiagram");var shape=Visual<System.Windows.Shapes.Rectangle>(diagram).Single();
                Check(shape.StrokeDashArray.Count==2&&shape.Fill is SolidColorBrush fill&&fill.Color.A==0,"View boundary diagram has dashed contour and no fill");
                var center=diagram.TranslatePoint(new Point(diagram.ActualWidth/2,diagram.ActualHeight/2),w);
                foreach(var n in new[]{"Top","Bottom","Left","Right"})
                {
                    var box=ElevationSession.Find<TextBox>(w,n+"TextBox");var label=((StackPanel)box.Parent).Children.OfType<TextBlock>().Single();var at=box.TranslatePoint(new Point(box.ActualWidth/2,box.ActualHeight/2),w);
                    Check(label.TextAlignment==TextAlignment.Center&&box.HorizontalContentAlignment==HorizontalAlignment.Center,"Boundary label and value centered: "+n);
                    Check(n=="Top"?at.Y<center.Y&&Math.Abs(at.X-center.X)<=1:n=="Bottom"?at.Y>center.Y&&Math.Abs(at.X-center.X)<=1:n=="Left"?at.X<center.X:at.X>center.X,"Boundary field on correct side: "+n);
                }
            }
            foreach(var track in Visual<System.Windows.Controls.Grid>(w).Where(x=>Capsule.GetIsGroup(x)&&x.IsVisible))
            {
                Check(track.ActualHeight>=34,"Capsule minimum fits shared track");
                var options=track.Children.OfType<System.Windows.Controls.Panel>().SelectMany(p=>p.Children.OfType<RadioButton>()).ToList();
                if(options.Count>0)
                {
                    Check(options.Count(r=>r.IsChecked==true)==1,"Exactly one capsule option checked: "+string.Join(";",options.Select(r=>r.Content+"="+r.IsChecked)));
                    foreach(var option in options)
                    {
                        var binding=System.Windows.Data.BindingOperations.GetBinding(option,ToggleButton.IsCheckedProperty);
                        if(binding!=null)Check(Equals(option.IsChecked,option.DataContext.GetType().GetProperty(binding.Path.Path).GetValue(option.DataContext)),"Capsule agrees with source VM: "+binding.Path.Path);
                    }
                }
                var indicator=track.Children.OfType<Border>().First(x=>Equals(x.Tag,"CapsuleIndicator"));
                Check(indicator.ActualWidth>0&&indicator.ActualHeight>=30,"Selected capsule has measured moving indicator");
                if(options.Count>0){var selected=options.Single(r=>r.IsChecked==true);Check(Math.Abs(indicator.ActualWidth-selected.ActualWidth)<1&&Math.Abs(indicator.TranslatePoint(new Point(0,0),track).X-selected.TranslatePoint(new Point(0,0),track).X)<1,"Capsule indicator matches checked source option");}
            }
            Check(help[0].Focus()&&help[0].IsKeyboardFocused,w.Title+": Help accepts WPF keyboard focus");
            Check(help[0].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)),w.Title+": WPF next-focus traversal is available");
            if(new[]{"LineGroupSelectionWindow","LineGroupNextActionWindow","CropByExampleActionWindow","ElevationCropAdjustmentWindow"}.Contains(w.GetType().Name))
            {
                var root=(System.Windows.Controls.Grid)w.Content;var card=root.Children.OfType<Border>().Single(b=>System.Windows.Controls.Grid.GetRow(b)==2);
                foreach(var control in Visual<FrameworkElement>(card).Where(c=>c is Button||c is TextBox))
                {
                    var p=control.TranslatePoint(new Point(),card);
                    Check(p.X>=0&&p.Y>=0&&p.X+control.ActualWidth<=card.ActualWidth+.5&&p.Y+control.ActualHeight<=card.ActualHeight+.5,w.Title+": compact field/action bounds fit card: "+control.Name);
                }
            }
        }
        static void ModalAction(Window w,Action action,Action assert)
        {
            w.Loaded+=(s,e)=>w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{action();assert();}));w.ShowDialog();
        }
        public static int Capture(string folder)
        {
            output=folder;Directory.CreateDirectory(output);log.Clear();ElevationSession.HasSelection=true;ElevationSession.Owner=null;
            try
            {
                foreach(string n in names)
                {
                    var w=New(n);Show(w);FrameChecks(w);Shot(w,n+"-normal");
                    var tabs=ElevationSession.Logical<TabControl>(w).FirstOrDefault();
                    if(tabs!=null)for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Pump();FrameChecks(w);Shot(w,n+"-tab"+i);}
                    if(w.ResizeMode!=ResizeMode.NoResize){w.Width=w.MinWidth;w.Height=w.MinHeight;Pump();FrameChecks(w);Shot(w,n+"-minimum");}
                    else Check(w.SizeToContent==SizeToContent.WidthAndHeight,"Choice window sized to content and NoResize");
                    if(tabs!=null)for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Pump();FrameChecks(w);Shot(w,n+"-tab"+i+"-minimum");}
                    if(tabs!=null){tabs.SelectedIndex=0;Pump();foreach(var sc in Visual<ScrollViewer>(w).Where(x=>x.IsVisible&&ScrollEdges.GetEnabled(x)))sc.ScrollToBottom();Shot(w,n+"-minimum-bottom");}
                    if(w is V.ElevationDecorationWindow){ElevationSession.Find<CheckBox>(w,"PlaceTagsCheckBox").IsChecked=true;Visual<GroupBox>(w).Single(c=>Equals(c.Header,"Марки")).BringIntoView();Pump();FrameChecks(w);Shot(w,"decoration-tags-minimum");w.Width=1100;w.Height=880;Pump();Visual<GroupBox>(w).Single(c=>Equals(c.Header,"Марки")).BringIntoView();Pump();FrameChecks(w);Shot(w,"decoration-tags");}
                    if(w is V.MultiRoomSelectionWindow){var table=ElevationSession.Find<DataGrid>(w,"RoomsDataGrid");w.Width=1280;Pump();double textWidth=table.Columns[2].ActualWidth;double[] fixedWidths=table.Columns.Where(c=>!c.Width.IsStar).Select(c=>c.ActualWidth).ToArray();w.Width=1540;Pump();FrameChecks(w);Check(table.Columns[2].ActualWidth>textWidth,"Room text column grows with window");Check(table.Columns.Where(c=>!c.Width.IsStar).Select(c=>c.ActualWidth).SequenceEqual(fixedWidths),"Room numeric/status/actions widths remain fixed");Check(Math.Abs(table.Columns.Sum(c=>c.ActualWidth)-(table.ActualWidth-2))<=2,"Room columns fill viewport to right edge without filler");Shot(w,"rooms-wide");}
                    w.Close();
                }
                var vm=ElevationSession.Settings();ElevationSettings settings;string error;Check(vm.TryBuildSettings(out settings,out error),"Source-derived real settings validator accepts fixture options");
                vm.ViewScaleText="некорректно";Check(!vm.TryBuildSettings(out settings,out error)&&error.Contains("Масштаб"),"Source validator rejects invalid scale");vm.ViewScaleText="50";
                vm.ExistingSheetMode=true;Check(vm.UseExistingSheet&&!vm.CreateSheet&&!vm.CanEditRoomPlanCreationParameters,"Source sheet mode dependencies");vm.DoNotPlaceViewsOnSheetMode=true;Check(!vm.PlaceViewsOnSheet&&!vm.CanPositionRoomPlan,"Source no-sheet dependency");vm.CreateNewSheetMode=true;
                vm.IsMultipleRoomsOnSheet=true;Check(!vm.IsSingleRoomPerSheet,"Source room mode mutual exclusion");vm.PickRoomFromLink=true;Check(!vm.PickRoomFromHost,"Source host/link mutual exclusion");
                var main=(V.ElevationSettingsWindow)New("ElevationSettingsWindow",vm);Show(main);var formula=ElevationSession.Find<RichTextBox>(main,"ElevationNameFormulaEditor");formula.Document.Blocks.Clear();formula.Document.Blocks.Add(new Paragraph(new Run("Длинная русская подпись_[Номер помещения]_[Имя помещения]_[Начальный угол]")));Pump();Check(vm.NamingPreviewItems[0].ViewName.Contains("101")&&vm.NamingPreviewItems[0].ViewName.Contains("Длинная"),"Original RichTextBox handler updates actual naming service result");Shot(main,"settings-long-formula");Shot(main,"settings-render144",144);main.Close();
                var sourceRows=new ObservableCollection<MultiRoomSelectionItem>();var roomVm=new SAB.InteriorElevations.ViewModels.MultiRoomSelectionViewModel(sourceRows);Check(sourceRows.Count==1&&!roomVm.TryValidateTransfer(out error),"Production room VM starts with one invalid blank row");ElevationSession.FillRoom(sourceRows[0]);Check(roomVm.TryValidateTransfer(out error),"Production room VM validates completed fixture row");roomVm.AddRowAfter(sourceRows[0]);ElevationSession.FillRoom(sourceRows[1]);Check(!roomVm.TryValidateTransfer(out error)&&error.Contains("больше одного"),"Production room validator rejects duplicate stable room IDs");roomVm.DeleteRow(sourceRows[1]);roomVm.DeleteRow(sourceRows[0]);Check(sourceRows.Count==1&&!sourceRows[0].IsCompleted,"Delete last room retains original blank-row contract");
                var mode=new V.LineGroupSelectionWindow();ModalAction(mode,()=>Click(mode,"MultipleGroupsButton"),()=>Check(mode.IsMultipleGroups&&mode.DialogResult==true,"Original group action returns multiple=true"));
                var next=new V.LineGroupNextActionWindow(3);ModalAction(next,()=>Click(next,"FinishButton"),()=>Check(!next.IsNextGroupAction&&next.DialogResult==true,"Original finish action returns next=false"));
                var action=new V.CropByExampleActionWindow();ModalAction(action,()=>Click(action,"CreateLineButton"),()=>Check(action.RequestedAction==V.CropByExampleAction.CreateLine,"Original crop action returns CreateLine"));
                var crop=new V.ElevationCropAdjustmentWindow();ModalAction(crop,()=>{ElevationSession.Find<TextBox>(crop,"LeftTextBox").Text="-25.5";Click(crop,"ApplyButton");},()=>Check(crop.SelectedSettings.LeftMm==-25.5,"Original crop parser preserves negative numeric adjustment"));
                var helper=new V.CropByExampleLineCreationWindow(null,null);Show(helper);Check(!ElevationSession.Find<Button>(helper,"CreateViewButton").IsEnabled&&!ElevationSession.Find<Button>(helper,"AcceptCropButton").IsEnabled,"Modeless initial gates");Click(helper,"SelectLineButton");Check(ElevationSession.Find<Button>(helper,"CreateViewButton").IsEnabled,"Synthetic PickLine response enables original source gate");Shot(helper,"crop-line-selected");Click(helper,"CreateViewButton");Check(ElevationSession.Find<Button>(helper,"AcceptCropButton").IsEnabled&&!ElevationSession.Find<Button>(helper,"SelectLineButton").IsEnabled,"Synthetic CreateView response changes original source gates");Shot(helper,"crop-sample-ready");Click(helper,"AcceptCropButton");Check(ElevationSession.CropStage==3,"Synthetic AcceptCrop completes separate session without API");Shot(helper,"crop-accepted");helper.Close();
                var decoration=(V.ElevationDecorationWindow)New("ElevationDecorationWindow");Show(decoration);ElevationSession.Find<CheckBox>(decoration,"PlaceDimensionsCheckBox").IsChecked=true;Pump();Check(ElevationSession.Find<ComboBox>(decoration,"DetailedDimensionTypeComboBox").IsEnabled,"Source decoration dependent controls enabled");Click(decoration,"OkButton");Check(ElevationSession.Find<TextBlock>(decoration,"ValidationStatusTextBlock").Text.Contains("не выбран"),"Original decoration validation detects missing required types");Shot(decoration,"decoration-validation");var tag=ElevationSession.Find<TextBox>(decoration,"TagOffsetTextBox");tag.Text="17.25";var raw=tag.Text;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};timer.Tick+=(s,e)=>{var child=Application.Current.Windows.OfType<V.ElevationDecorationCatalogWindow>().FirstOrDefault(x=>x.IsVisible);if(child!=null){timer.Stop();Click(child,"DoneButton");}};timer.Start();Click(decoration,"ConfigureCatalogButton");Check(tag.Text==raw&&decoration.IsVisible,"Catalog child returns without losing raw unsaved form input");decoration.Close();
                var empty=ElevationSession.Catalog;ElevationSession.Catalog=new ElevationDecorationCatalog();var catalog=(V.ElevationDecorationCatalogWindow)New("ElevationDecorationCatalogWindow");Show(catalog);Check(ElevationSession.Find<DataGrid>(catalog,"EntriesListView").Items.Count==0,"Empty role uses real source collection");Shot(catalog,"catalog-empty");catalog.Close();ElevationSession.Catalog=empty;
                CreationNotificationChecks();var confirm=ElevationSession.Simple("Очистить роль каталога?","Чистовой пол",true);Show(confirm);FrameChecks(confirm);Shot(confirm,"catalog-confirmation");confirm.Close();
                CategoryChecks();
                var proposal=ShowNotificationProposal(IntPtr.Zero);Pump();Check(Math.Abs(proposal.Width-416)<1,"Notification proposal is20percent narrower:416DIP");Check(proposal.Cards.All(c=>Visual<TextBlock>(c).Any(t=>t.Text==c.Message.Outcome&&t.FontSize==16&&t.FontWeight==FontWeights.Bold)),"Notification proposal bold16DIP outcome");Shot(proposal,"notification-proposal");proposal.Close();
                ExtendedStates();
                log.Add("NOT RUN Revit Document/transactions/native selection/ExternalEvent; physical keyboard/accessibility; OS DPI/monitor changes; browser launch");log.Add("COMPLETED");return 0;
            }
            catch(Exception e){log.Add("ERROR "+e);return 1;}
            finally{File.WriteAllLines(Path.Combine(output,"verification.txt"),log);Application.Current.Shutdown();}
        }
        static void CreationNotificationChecks()
        {
            var clean=CreationResult("success");Check(clean.Tone==NotificationTone.Success&&string.IsNullOrWhiteSpace(clean.Warnings),"Successful creation has green outcome and no warning block");
            Check(!clean.Summary.Contains(": 0")&&!clean.Summary.Contains("Не удалось")&&!clean.Summary.Contains("отключено"),"Successful creation summary contains no zero/error/disabled rows");
            var partial=CreationResult("warning");Check(partial.Tone==NotificationTone.Warning&&partial.Warnings.Contains("план-схему")&&partial.Warnings.Contains("Марка угла")&&!partial.Summary.Contains("не размещена"),"All plan and corner mark problems are separate warnings");
            var failed=CreationResult("error");Check(failed.Tone==NotificationTone.Error&&failed.Warnings.Contains("Не удалось создать развёрток: 4")&&string.IsNullOrWhiteSpace(failed.Summary),"All failed creation has red outcome and no false success summary");
            var data=new ElevationCreationNotificationData{CreatedViews=3,FailedViews=1,SheetPlacementRequested=true,HasTargetSheet=true,PlacedViewports=2,UnplacedViews=1,AutomaticFallbackUsed=true,Warnings=new[]{"Марка угла не размещена.","Марка угла не размещена."}};
            var mixed=ElevationCreationReportService.BuildNotification(data);Check(mixed.Tone==NotificationTone.Warning&&mixed.Warnings.Contains("Не удалось создать")&&mixed.Warnings.Contains("Не размещено")&&mixed.Warnings.Contains("компоновка"),"Failed views/unplaced/fallback all affect warning outcome");
            Check(mixed.Warnings.Split(new[]{"Марка угла не размещена."},StringSplitOptions.None).Length==2,"Repeated warning is deduplicated without losing other causes");
            var noPlacement=ElevationCreationReportService.BuildNotification(new ElevationCreationNotificationData{CreatedViews=2});Check(noPlacement.Tone==NotificationTone.Success&&!noPlacement.Summary.Contains("0")&&!noPlacement.Summary.Contains("Лист"),"Intentionally disabled sheet placement emits no problem or zero count");
            var continuation=ElevationCreationReportService.BuildNotification(new ElevationCreationNotificationData{CreatedViews=2,PlacedViewports=2,HasTargetSheet=true,TargetSheet="ELV-01 | Исходный",Sheets=new[]{"ELV-02 | Продолжение"}});Check(continuation.Summary.Contains("Лист с развёртками: ELV-02 | Продолжение"),"Single actual continuation sheet remains visible when different from target");
            var emptyManual=ElevationCreationReportService.BuildNotification(new ElevationCreationNotificationData{CreatedViews=2,SheetPlacementRequested=true,ForcedPlacementUsed=true,ColumnsCount=3});Check(!emptyManual.Summary.Contains("Расстановка:"),"No positive manual-placement claim when zero viewports were placed");
            string copied=null;var actualCard=new NotificationCard(partial,value=>copied=value);var copyWindow=new Window{Content=actualCard,Width=520,SizeToContent=SizeToContent.Height};Show(copyWindow);var copyButton=Visual<Button>(actualCard).Single(b=>Equals(b.Content,"Копировать"));copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(copied==partial.Warnings&&Equals(copyButton.Content,"Скопировано"),"Actual shared warning copy handler copies full warning text");copyWindow.Close();
            var productionStack=NotificationStack.Show(IntPtr.Zero,partial);Pump();Shot(productionStack,"creation-notification-production");productionStack.Close();
            foreach(var scenario in new[]{"success","warning","error","long"})
            {
                var stack=ShowCreationNotification(scenario,IntPtr.Zero);Pump();var card=stack.Cards.Single();
                Check(card.Message.Summary==CreationResult(scenario).Summary&&card.Message.Warnings==CreationResult(scenario).Warnings,"Notification uses actual plugin result composer: "+scenario);
                var status=Visual<TextBlock>(card).Single(t=>t.Text==card.Message.Outcome);Check(status.FontSize==16&&status.FontWeight==FontWeights.Bold,"Creation outcome is emphasized: "+scenario);
                Check(((SolidColorBrush)status.Foreground).Color==((SolidColorBrush)card.FindResource(scenario=="success"?"SabBrush.Success":scenario=="error"?"SabBrush.Error":"SabBrush.Warning")).Color,"Creation outcome color: "+scenario);
                if(scenario!="success")Check(Visual<TextBox>(card).Any(t=>t.Text==card.Message.Warnings&&t.Background is SolidColorBrush b&&b.Color.A>0),"Warnings in filled readable frame: "+scenario);
                Shot(stack,"creation-notification-"+scenario);stack.Close();
            }
        }
        static void CategoryChecks()
        {
            var defaults=new ElevationDecorationSettings();Check(defaults.PlaceFloorSpots&&defaults.PlaceCeilingSpots&&defaults.PlaceSlabSpots,"Legacy constructed settings keep all spot categories");
            var storage=new SAB.InteriorElevations.Services.Settings.ElevationDecorationSettingsStorageService();
            string path=Path.Combine(Path.GetDirectoryName(output),"decoration-settings-test.json");
            typeof(SAB.InteriorElevations.Services.Settings.ElevationDecorationSettingsStorageService).GetField("_settingsFilePath",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(storage,path);
            for(int schema=1;schema<=8;schema++){File.WriteAllText(path,"{\"SchemaVersion\":"+schema+",\"PlaceSpotElevations\":true}");var legacy=storage.LoadSettings();Check(legacy.PlaceFloorSpots&&legacy.PlaceCeilingSpots&&legacy.PlaceSlabSpots,"Schema"+schema+" migration retains all categories");}
            defaults.PlaceSpotElevations=true;defaults.PlaceFloorSpots=false;defaults.PlaceCeilingSpots=true;defaults.PlaceSlabSpots=false;storage.SaveSettings(defaults);var restored=storage.LoadSettings();Check(!restored.PlaceFloorSpots&&restored.PlaceCeilingSpots&&!restored.PlaceSlabSpots,"Schema9 roundtrip retains independent spot choices");Check(restored.RequiredCatalogRoles().SequenceEqual(new[]{ElevationDecorationCatalogRole.Ceiling}),"Ceiling-only spots require only ceiling catalog role");
            var dimensions=new ElevationDecorationSettings{PlaceDimensions=true,PlaceHorizontalDimensions=true};Check(dimensions.RequiredCatalogRoles().SequenceEqual(new[]{ElevationDecorationCatalogRole.FinishWall}),"Horizontal dimensions require FinishWall only");dimensions.PlaceVerticalDimensions=true;Check(new System.Collections.Generic.HashSet<ElevationDecorationCatalogRole>(dimensions.RequiredCatalogRoles()).SetEquals(new[]{ElevationDecorationCatalogRole.FinishWall,ElevationDecorationCatalogRole.FinishFloor,ElevationDecorationCatalogRole.Ceiling,ElevationDecorationCatalogRole.StructuralSlab}),"Both dimension directions require union of their catalog roles");
            var ownership=new SAB.InteriorElevations.Services.Annotations.ElevationAnnotationOwnershipService();var roleGate=ownership.GetType().GetMethod("ShouldDeleteRole",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int mask=0;mask<8;mask++)
            {
                var flags=new ElevationDecorationSettings{PlaceSpotElevations=true,PlaceFloorSpots=(mask&1)!=0,PlaceCeilingSpots=(mask&2)!=0,PlaceSlabSpots=(mask&4)!=0,PlaceFinishFloorSpotElevation=true};
                storage.SaveSettings(flags);var saved=storage.LoadSettings();Check(saved.PlaceFloorSpots==flags.PlaceFloorSpots&&saved.PlaceCeilingSpots==flags.PlaceCeilingSpots&&saved.PlaceSlabSpots==flags.PlaceSlabSpots,"All8category combinations persist: "+mask);
                foreach(var pair in new[]{new{Role="Spot.FinishFloor",Enabled=flags.PlaceFloorSpots},new{Role="Spot.Ceiling",Enabled=flags.PlaceCeilingSpots},new{Role="Spot.StructuralSlab",Enabled=flags.PlaceSlabSpots}})
                    Check((bool)roleGate.Invoke(ownership,new object[]{pair.Role,flags})==pair.Enabled&&(bool)roleGate.Invoke(ownership,new object[]{"Diagnostic."+pair.Role,flags})==pair.Enabled,"Replacement preserves unselected category: "+mask+" "+pair.Role);
                if(mask==0)continue;
                var selected=(V.ElevationDecorationWindow)New("ElevationDecorationWindow");
                ModalAction(selected,()=>{
                    foreach(var combo in ElevationSession.Logical<ComboBox>(selected))combo.SelectedIndex=1;
                    ElevationSession.Find<CheckBox>(selected,"PlaceSpotElevationsCheckBox").IsChecked=true;ElevationSession.Find<CheckBox>(selected,"PlaceTagsCheckBox").IsChecked=false;ElevationSession.Find<CheckBox>(selected,"PlaceDimensionsCheckBox").IsChecked=false;
                    ElevationSession.Find<CheckBox>(selected,"PlaceFloorSpotsCheckBox").IsChecked=flags.PlaceFloorSpots;ElevationSession.Find<CheckBox>(selected,"PlaceCeilingSpotsCheckBox").IsChecked=flags.PlaceCeilingSpots;ElevationSession.Find<CheckBox>(selected,"PlaceSlabSpotsCheckBox").IsChecked=flags.PlaceSlabSpots;
                    Click(selected,"OkButton");
                },()=>Check(selected.SelectedSettings!=null&&selected.SelectedSettings.PlaceFloorSpots==flags.PlaceFloorSpots&&selected.SelectedSettings.PlaceCeilingSpots==flags.PlaceCeilingSpots&&selected.SelectedSettings.PlaceSlabSpots==flags.PlaceSlabSpots,"Original window handler maps category checkboxes to production settings: "+mask));
            }
            var w=(V.ElevationDecorationWindow)New("ElevationDecorationWindow");Show(w);
            foreach(var n in new[]{"Floor","Ceiling","Slab"})ElevationSession.Find<CheckBox>(w,"Place"+n+"SpotsCheckBox").IsChecked=false;
            Click(w,"OkButton");Check(ElevationSession.Find<TextBlock>(w,"ValidationStatusTextBlock").Text.Contains("выберите полы"),"Empty spot categories rejected by real validator");w.Close();
        }
        static Proposal.NotificationStack ShowNotificationProposal(IntPtr owner)
        {
            Proposal.NotificationStack.Show(owner,new Proposal.NotificationMessage("SAB · Параметризация","Завершено",Proposal.NotificationTone.Success,"Обработано элементов: 124 из 124.\nЗаписей параметров: 372."));
            Proposal.NotificationStack.Show(owner,new Proposal.NotificationMessage("SAB · Параметризация","Завершено с пропусками",Proposal.NotificationTone.Warning,"Обработано элементов: 121 из 124.\nУ 3 элементов не удалось записать параметр.","482015, 482016, 482021","Модуль: SAB · Параметризация\nПараметр: SAB_Материал_Описание\nПричина: параметр доступен только для чтения."));
            return Proposal.NotificationStack.Show(owner,new Proposal.NotificationMessage("SAB · Параметризация","Операция отменена",Proposal.NotificationTone.Error,"Revit отменил транзакцию.\nИзменения этой операции не сохранены.",null,"TransactionStatus: RolledBack\nПричина: Revit отменил транзакцию."));
        }
        static void ExtendedStates()
        {
            ElevationSession.RoomRows.Clear();
            for(int i=0;i<24;i++){var row=new MultiRoomSelectionItem();ElevationSession.FillRoom(row,201+i,"Длинное русское название помещения · переговорная проектной группы на втором этаже №"+i);ElevationSession.RoomRows.Add(row);}
            var rooms=(V.MultiRoomSelectionWindow)New("MultiRoomSelectionWindow");rooms.Width=rooms.MinWidth;rooms.Height=rooms.MinHeight;Show(rooms);
            var table=ElevationSession.Find<DataGrid>(rooms,"RoomsDataGrid");table.ScrollIntoView(table.Items[table.Items.Count-1],table.Columns.Last());Pump();FrameChecks(rooms);
            Check(Visual<DataGridRow>(table).Any(r=>r.Item==table.Items[table.Items.Count-1]),"Last room row reachable by bounded table scroll");
            Check(Visual<Button>(table).Where(b=>b.Name=="AddRoomButton"||b.Name=="DeleteRoomButton").All(b=>b.Content is System.Windows.Shapes.Path),"Room icon actions retain vector content");
            Shot(rooms,"rooms-long-last-row-minimum");rooms.Close();ElevationSession.RoomRows.Clear();
            var catalog=(V.ElevationDecorationCatalogWindow)New("ElevationDecorationCatalogWindow");Show(catalog);var role=ElevationSession.Find<ComboBox>(catalog,"RoleComboBox");
            for(int i=0;i<role.Items.Count;i++){role.SelectedIndex=i;Pump();Check(ElevationSession.Find<DataGrid>(catalog,"EntriesListView").Items.Count==1,"Catalog source role filter "+i);}
            catalog.Close();
            var original=new VM.ElevationSettingsViewModel(null);original.ViewScaleText="75";
            var pick=(V.ElevationSettingsWindow)New("ElevationSettingsWindow",original);
            ModalAction(pick,()=>Click(pick,"PickLinesButton"),()=>Check(pick.RequestedAction==V.ElevationSettingsWindowAction.PickSelection,"Original selection action returns request to controller"));
            var reopened=(V.ElevationSettingsWindow)New("ElevationSettingsWindow",original);Show(reopened);FrameChecks(reopened);Check(original.ViewScaleText=="75","Same source VM retains unsaved values across selection return");
            var editor=ElevationSession.Find<RichTextBox>(reopened,"ElevationNameFormulaEditor");editor.Focus();var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(editor),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};editor.RaiseEvent(key);Check(key.Handled,"Original formula Enter handler suppresses newline");reopened.Close();
            ElevationSession.HasSelection=false;var pending=New("ElevationSettingsWindow");Show(pending);FrameChecks(pending);Shot(pending,"settings-selection-pending");pending.Close();ElevationSession.HasSelection=true;
            var progress=new V.CreateViewsAndSheetsProgressWindow(new List<string>{"Строим развёртки по выбранным линиям."},"SAB · Развёртки","Создание развёрток");
            progress.Report(new SAB.CreateViewsAndSheets.Models.CreateViewsAndSheetsProgressInfo{Stage="Создание видов",Details="Помещение 101 · Переговорная",CurrentStep=2,TotalSteps=4,ProcessedItems=2,TotalItems=4,CreatedItems=2});Show(progress);FrameChecks(progress);Shot(progress,"shared-progress");progress.Close();Check(progress.IsVisible,"Original progress blocks premature close");progress.AllowCloseAndClose();Check(!progress.IsVisible,"Original progress completion releases close guard");
            var question=ElevationSession.Simple("Очистить роль каталога?","Чистовой пол",true);Show(question);Check(Visual<Button>(question).Single(b=>b.IsDefault).Content.Equals("Нет"),"Destructive confirmation retains production default No");question.Close();
            ElevationSession.RoomRows.Clear();var selected=new MultiRoomSelectionItem();ElevationSession.FillRoom(selected);ElevationSession.RoomRows.Add(selected);
            DriveRoomDialog(w=>Click(w,"CancelButton"));Check(!Open("MultiRoomSelectionWindow")&&ElevationSession.RoomRows[0].IsCompleted,"Room Cancel preserves rows but does not transfer selection");
            ElevationSession.RoomRows.Add(new MultiRoomSelectionItem());
            DriveRoomDialog(w=>{Click(w,"TransferButton");Check(w.IsVisible,"Blank room blocks original Transfer action");Click(w,"CancelButton");});Check(!Open("MultiRoomSelectionWindow"),"Completed plus blank room cannot confirm selection");
            ElevationSession.RoomRows.RemoveAt(1);DriveRoomDialog(w=>Click(w,"TransferButton"));Check(Open("MultiRoomSelectionWindow"),"Validated source Transfer confirms selection");ElevationSession.RoomRows.Clear();
            var groupTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
            groupTimer.Tick+=(s,e)=>{var mode=Application.Current.Windows.OfType<V.LineGroupSelectionWindow>().FirstOrDefault(w=>w.IsVisible);if(mode!=null)Click(mode,"MultipleGroupsButton");var next=Application.Current.Windows.OfType<V.LineGroupNextActionWindow>().FirstOrDefault(w=>w.IsVisible);if(next!=null){groupTimer.Stop();Click(next,"CancelButton");}};groupTimer.Start();Check(!PickGroups(),"Next-group helper Cancel aborts without native-pick confirmation");
            var existing=new V.CropByExampleLineCreationWindow(null,null);existing.SetExistingLineMode();Show(existing);ElevationSession.RequestCrop(existing,CropByExampleOperation.PickExistingLineAndCreateView);Check(ElevationSession.CropStage==2&&ElevationSession.Find<Button>(existing,"AcceptCropButton").IsEnabled,"Existing-line controller operation automatically reaches sample-ready gate");Shot(existing,"crop-existing-sample-ready");existing.Close();
            var sheetVm=ElevationSession.Settings();sheetVm.ExistingSheetMode=true;sheetVm.SelectedExistingSheet=sheetVm.ExistingSheets[0];
            Check(!sheetVm.HasExistingRoomPlanSelection&&!sheetVm.TryBuildSettings(out _,out _),"Existing sheet requires original plan-selection validator");
            Check(ElevationSession.PickSheetPlacement(sheetVm,true)&&sheetVm.HasExistingRoomPlanSelection&&sheetVm.TryBuildSettings(out _,out _),"Synthetic point plus existing-plan selection satisfies original validator");
            var planId=sheetVm.ExistingRoomPlanViewId;sheetVm.StartXmmText="170";
            Check(!ElevationSession.PickSheetPlacement(sheetVm,false)&&sheetVm.ExistingRoomPlanViewId==planId&&sheetVm.StartXmmText=="170","Cancelled sheet pick preserves previous plan and coordinates");
            var stage=0;VM.ElevationSettingsViewModel retained=null;var flowTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
            flowTimer.Tick+=(s,e)=>{
                var settingsWindow=Application.Current.Windows.OfType<V.ElevationSettingsWindow>().FirstOrDefault(x=>x.IsVisible);
                var cropAction=Application.Current.Windows.OfType<V.CropByExampleActionWindow>().FirstOrDefault(x=>x.IsVisible);
                if(stage==0&&settingsWindow!=null){retained=(VM.ElevationSettingsViewModel)settingsWindow.DataContext;retained.ViewScaleText="75";stage=1;Click(settingsWindow,"PickCropByExampleButton");}
                else if(stage==1&&cropAction!=null){stage=2;Click(cropAction,"CloseButton");}
                else if(stage==2&&settingsWindow!=null){flowTimer.Stop();Check(ReferenceEquals(settingsWindow.DataContext,retained)&&retained.ViewScaleText=="75","Crop action Cancel returns to same settings VM with unsaved input");FrameChecks(settingsWindow);Click(settingsWindow,"CancelButton");}
            };flowTimer.Start();MainFlow();Check(stage==2,"Cancelled crop action completed return through actual preview MainFlow");
        }
        static void DriveRoomDialog(Action<V.MultiRoomSelectionWindow> action)
        {
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};timer.Tick+=(s,e)=>{var w=Application.Current.Windows.OfType<V.MultiRoomSelectionWindow>().FirstOrDefault(x=>x.IsVisible);if(w!=null){timer.Stop();action(w);}};timer.Start();
        }
    }
}


