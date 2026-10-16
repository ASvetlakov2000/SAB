using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Views;

static class LifecycleTests
{
    public static void Run(Action<string,Action> test,Action<bool> assert,Action<Action> throws,Func<bool,(Document,ViewPlan,ElevationLineData)> fixture)
    {
        (Document,UIApplication,ElevationDirectionPreviewWindow) Start(Action<UIApplication> create=null)
        {
            var(d,p,l)=fixture(true);var a=new UIApplication{ActiveUIDocument=new UIDocument{Document=d,ActiveView=p}};
            Nice3point.Revit.Toolkit.External.Dispatch.Application=a;
            ElevationDirectionPreviewController.Start(a,p,new[]{l},create??(_=>{}));return(d,a,ElevationDirectionPreviewWindow.Last);
        }
        bool Empty(Document d)=>!d.Elements.OfType<FamilyInstance>().Any();
        void Drain()=>Nice3point.Revit.Toolkit.External.Dispatch.Drain();
        void NewApiWrappers(UIApplication a)
        {
            var ui=a.ActiveUIDocument;var d=ui.Document.NewWrapper();
            a.ActiveUIDocument=new UIDocument{Document=d,ActiveView=new ViewPlan{Document=d,Id=new ElementId(ui.ActiveView.Id.IntegerValue)}};
        }
        test("same native document and plan with fresh API wrappers can create",()=>{bool created=false;var(d,a,w)=Start(_=>created=true);NewApiWrappers(a);assert(!ReferenceEquals(d,a.ActiveUIDocument.Document)&&d.Equals(a.ActiveUIDocument.Document));w.Create();Drain();assert(created&&Empty(d)&&w.Closed);});
        test("fresh document wrappers still reach owned marker selection and flip",()=>{var(d,a,w)=Start();NewApiWrappers(a);var marker=d.Elements.OfType<FamilyInstance>().Single();var selection=a.ActiveUIDocument.Selection;selection.OnPick=(filter)=>{assert(!w.Visible&&filter.AllowElement(marker));return new List<Reference>{new Reference{ElementId=marker.Id}};};w.Flip();Drain();assert(selection.PickCalls==1&&Math.Abs(marker.Location.Point.Y)<1e-8&&(-marker.GetTransform().BasisY).Y>.9999&&w.Visible&&!w.IsBusy);w.Cancel();Drain();});
        test("preselected current marks flip without starting a second selection",()=>{var(d,a,w)=Start();var mark=d.Elements.OfType<FamilyInstance>().Single();a.ActiveUIDocument.Selection.Selected.Add(mark.Id);w.Flip();Drain();assert(a.ActiveUIDocument.Selection.PickCalls==0&&Math.Abs(mark.Location.Point.Y)<1e-8&&(-mark.GetTransform().BasisY).Y>.9999);w.Cancel();Drain();});
        test("selection escape restores window and keeps preview usable",()=>{var(d,a,w)=Start();w.Flip();Drain();assert(a.ActiveUIDocument.Selection.PickCalls==1&&w.Visible&&!w.IsBusy&&w.Status=="Выбор марок отменён.");w.Cancel();Drain();});
        test("different document with same title and element id is rejected before picking",()=>{var(d,a,w)=Start();var original=a.ActiveUIDocument;var other=new Document{Title=d.Title};a.ActiveUIDocument=new UIDocument{Document=other,ActiveView=new ViewPlan{Id=new ElementId(original.ActiveView.Id.IntegerValue)}};w.Flip();Drain();assert(a.ActiveUIDocument.Selection.PickCalls==0&&!w.Closed&&w.Status=="Вернитесь в исходный документ предпросмотра.");a.ActiveUIDocument=original;w.Cancel();Drain();});
        test("different plan in same document is rejected before picking",()=>{var(d,a,w)=Start();var original=a.ActiveUIDocument.ActiveView;a.ActiveUIDocument.ActiveView=new ViewPlan{Id=new ElementId(999),Document=d};w.Flip();Drain();assert(a.ActiveUIDocument.Selection.PickCalls==0&&!w.Closed&&w.Status=="Вернитесь на исходный план предпросмотра.");a.ActiveUIDocument.ActiveView=original;w.Cancel();Drain();});
        test("save event with fresh wrapper still clears owned annotations",()=>{var(d,a,w)=Start();a.Application.Saving(new DocumentSavingEventArgs{Document=d.NewWrapper(),Cancellable=true});assert(Empty(d)&&w.Closed);});
        test("SaveAs event with fresh wrapper still clears owned annotations",()=>{var(d,a,w)=Start();a.Application.SavingAs(new DocumentSavingAsEventArgs{Document=d.NewWrapper(),Cancellable=true});assert(Empty(d)&&w.Closed);});
        test("sync event with fresh wrapper still clears owned annotations",()=>{var(d,a,w)=Start();a.Application.Sync(new DocumentSynchronizingWithCentralEventArgs{Document=d.NewWrapper(),Cancellable=true});assert(Empty(d)&&w.Closed);});
        test("close event with fresh wrapper queues cleanup without DB access",()=>{var(d,a,w)=Start();var proxy=d.NewWrapper();var args=new DocumentClosingEventArgs{Document=proxy,Cancellable=true,DocumentId=9};d.ReadOnlyCallback=true;proxy.ReadOnlyCallback=true;a.Application.Closing(args);assert(args.Cancelled&&!Empty(d));d.ReadOnlyCallback=false;Drain();assert(Empty(d)&&w.Closed);});
        test("modeless Cancel queues API work and removes marks once",()=>{var(d,a,w)=Start();int n=d.Transactions;w.Cancel();assert(w.IsBusy && !w.Closed && d.Transactions==n);Drain();assert(Empty(d)&&w.Closed&&!ElevationDirectionPreviewController.ActivateExisting());});
        test("confirmed Create cleans marks before invoking creation",()=>{bool created=false;var(d,a,w)=Start(app=>{assert(Empty(app.ActiveUIDocument.Document));created=true;});w.Create();Drain();assert(created&&w.Closed&&Empty(d));});
        test("wrong active document blocks create and retains recoverable preview",()=>{var(d,a,w)=Start();var original=a.ActiveUIDocument;a.ActiveUIDocument=new UIDocument{Document=new Document()};w.Create();Drain();assert(!w.Closed&&!Empty(d)&&!w.IsBusy);a.ActiveUIDocument=original;w.Cancel();Drain();});
        test("DocumentClosing read-only callback only cancels and queues cleanup",()=>{var(d,a,w)=Start();int n=d.Transactions;var args=new DocumentClosingEventArgs{Document=d,Cancellable=true,DocumentId=1};d.ReadOnlyCallback=true;a.Application.Closing(args);d.ReadOnlyCallback=false;assert(args.Cancelled && d.Transactions==n && !Empty(d));Drain();assert(Empty(d)&&w.Closed);});
        test("denied close cleanup request unlocks UI for retry",()=>{var(d,a,w)=Start();Nice3point.Revit.Toolkit.External.Dispatch.Deny=true;var args=new DocumentClosingEventArgs{Document=d,Cancellable=true,DocumentId=1};a.Application.Closing(args);assert(args.Cancelled&&!w.IsBusy&&!w.Closed);Nice3point.Revit.Toolkit.External.Dispatch.Deny=false;w.Cancel();Drain();assert(Empty(d));});
        test("save removes marks and cancels preview before persistence",()=>{var(d,a,w)=Start();var args=new DocumentSavingEventArgs{Document=d,Cancellable=true};a.Application.Saving(args);assert(!args.Cancelled&&Empty(d)&&w.Closed);});
        test("SaveAs removes marks and cancels preview",()=>{var(d,a,w)=Start();var args=new DocumentSavingAsEventArgs{Document=d,Cancellable=true};a.Application.SavingAs(args);assert(!args.Cancelled&&Empty(d)&&w.Closed);});
        test("sync removes marks and cancels preview before central save",()=>{var(d,a,w)=Start();var args=new DocumentSynchronizingWithCentralEventArgs{Document=d,Cancellable=true};a.Application.Sync(args);assert(!args.Cancelled&&Empty(d)&&w.Closed);});
        test("cleanup failure cancels saving and keeps UI recoverable",()=>{var(d,a,w)=Start();d.FailDelete=true;var args=new DocumentSavingEventArgs{Document=d,Cancellable=true};a.Application.Saving(args);assert(args.Cancelled&&!w.Closed&&!w.IsBusy&&!Empty(d));d.FailDelete=false;w.Cancel();Drain();assert(Empty(d));});
        test("noncancellable shutdown closes UI after source document closes",()=>{var(d,a,w)=Start();a.Application.Closing(new DocumentClosingEventArgs{Document=d,Cancellable=false,DocumentId=7});assert(!w.Visible);d.IsValidObject=false;a.Application.Closed(new DocumentClosedEventArgs{DocumentId=7});assert(w.Closed&&!ElevationDirectionPreviewController.ActivateExisting());});
        test("only one preview session can be active",()=>{var(d,a,w)=Start();assert(ElevationDirectionPreviewController.ActivateExisting());var(other,p,l)=fixture(true);throws(()=>ElevationDirectionPreviewController.Start(a,p,new[]{l},_=>{}));w.Cancel();Drain();});
    }
}
namespace Autodesk.Revit.DB.Events
{
    public class DocumentEventArgs:EventArgs {public Document Document;public bool Cancellable,Cancelled;public void Cancel(){Cancelled=true;}}
    public class DocumentClosingEventArgs:DocumentEventArgs {public int DocumentId;}
    public class DocumentClosedEventArgs:EventArgs {public int DocumentId;}
    public class DocumentSavingEventArgs:DocumentEventArgs {}
    public class DocumentSavingAsEventArgs:DocumentEventArgs {}
    public class DocumentSynchronizingWithCentralEventArgs:DocumentEventArgs {}
}
namespace Autodesk.Revit.UI
{
    public enum ExternalEventRequest {Accepted,Pending,Denied}
    public class UIApplication {public UIDocument ActiveUIDocument;public IntPtr MainWindowHandle;public Application Application=new Application();}
    public class UIDocument {public Document Document;public ViewPlan ActiveView;public Selection.Selection Selection=new Selection.Selection();}
    public class Application
    {
        public event EventHandler<DocumentClosingEventArgs> DocumentClosing;
        public event EventHandler<DocumentClosedEventArgs> DocumentClosed;
        public event EventHandler<DocumentSavingEventArgs> DocumentSaving;
        public event EventHandler<DocumentSavingAsEventArgs> DocumentSavingAs;
        public event EventHandler<DocumentSynchronizingWithCentralEventArgs> DocumentSynchronizingWithCentral;
        public void Closing(DocumentClosingEventArgs e)=>DocumentClosing?.Invoke(this,e);
        public void Closed(DocumentClosedEventArgs e)=>DocumentClosed?.Invoke(this,e);
        public void Saving(DocumentSavingEventArgs e)=>DocumentSaving?.Invoke(this,e);
        public void SavingAs(DocumentSavingAsEventArgs e)=>DocumentSavingAs?.Invoke(this,e);
        public void Sync(DocumentSynchronizingWithCentralEventArgs e)=>DocumentSynchronizingWithCentral?.Invoke(this,e);
    }
}
namespace Autodesk.Revit.DB {public class Reference {public ElementId ElementId;}}
namespace Autodesk.Revit.UI.Selection
{
    public enum ObjectType{Element}
    public interface ISelectionFilter {bool AllowElement(Element element);bool AllowReference(Reference r,XYZ point);}
    public class Selection
    {
        public List<ElementId> Selected=new List<ElementId>();public int PickCalls;
        public Func<ISelectionFilter,IList<Reference>> OnPick;
        public ICollection<ElementId> GetElementIds()=>Selected;
        public IList<Reference> PickObjects(ObjectType t,ISelectionFilter f,string prompt)
        {PickCalls++;return OnPick!=null?OnPick(f):throw new Autodesk.Revit.Exceptions.OperationCanceledException();}
    }
}
namespace Autodesk.Revit.Exceptions {public class OperationCanceledException:Exception{}}
namespace System.Windows.Interop {public class WindowInteropHelper {public WindowInteropHelper(object w){}public IntPtr Owner;}}
namespace SAB.InteriorElevations.Views
{
    public class ElevationDirectionPreviewWindow
    {
        public static ElevationDirectionPreviewWindow Last;
        public bool IsBusy,Closed,Visible;public string Status;
        public ElevationDirectionPreviewWindow(string text){Last=this;}
        public event Action FlipRequested,CreateRequested,CancelRequested;
        public void Cancel()=>CancelRequested?.Invoke();public void Create()=>CreateRequested?.Invoke();public void Flip()=>FlipRequested?.Invoke();
        public void SetStatus(string text,bool busy=false){Status=text;IsBusy=busy;}
        public void Show(){Visible=true;}public void Hide(){Visible=false;}public void Activate(){}
        public void CloseAfterCleanup(){Closed=true;Visible=false;}
    }
}
namespace Nice3point.Revit.Toolkit.External
{
    public static class Dispatch
    {
        public static UIApplication Application;public static bool Deny;public static Queue<Action> Queue=new Queue<Action>();
        public static void Drain(){while(Queue.Count>0)Queue.Dequeue()();}
    }
    public class ExternalEvent<T>:IDisposable
    {
        readonly Action<UIApplication,T> callback;public ExternalEvent(Action<UIApplication,T> callback){this.callback=callback;}
        public ExternalEventRequest Raise(T value){if(Dispatch.Deny)return ExternalEventRequest.Denied;Dispatch.Queue.Enqueue(()=>callback(Dispatch.Application,value));return ExternalEventRequest.Accepted;}
        public void Dispose(){}
    }
}
namespace Helpers.Notifications.ToastNotifications
{
    public static class ToastNotifier {public static void ShowInfo(string a,string b){}public static void ShowError(string a,string b){}}
}
