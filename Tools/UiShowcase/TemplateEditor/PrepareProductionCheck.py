"""Generate test fixtures/harness only; load actual production types from SAB.dll."""
from Prepare import *

dest=OUT/'production/host-source';dest.mkdir(parents=True,exist_ok=True)
source=(HERE/'Preview.cs').read_text(encoding='utf-8-sig')
fixtures=source[source.index('    public static class Fixtures'):source.index('    public static class Program')]
fixtures=fixtures.replace('new CategoryOverrideRow {','new CategoryOverrideRow {SupportsProjectionLines=true,SupportsHalftone=true,')
# Link synthetic capability example overrides the true initializer without duplicate members.
fixtures=fixtures.replace('ParentCategoryIdValue=-1,SupportsProjectionLines=false','ParentCategoryIdValue=-1')
fixtures=fixtures.replace('            data.StartTrackingChanges();','            data.ModelCategories.Rows.Last().SupportsProjectionLines=false;\n            data.StartTrackingChanges();')
fixtures=fixtures.replace('    public static class Fixtures\n    {','''    public static class Fixtures
    {
        public static ViewTemplateGraphicsViewModel CreateViewModel()
        {
            var ctor=typeof(ViewTemplateGraphicsViewModel).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(IList<TemplateSelectionItem>),typeof(Func<IList<TemplateSelectionItem>,ViewTemplateGraphicsData>)},null);
            return (ViewTemplateGraphicsViewModel)ctor.Invoke(new object[]{Templates(),new Func<IList<TemplateSelectionItem>,ViewTemplateGraphicsData>(Build)});
        }''')
header='''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.ViewModels;
using SAB.ViewTemplateGraphics.Views;
namespace SAB.TemplateEditorProductionCheck {
public static class PreviewShell {
    public static IEnumerable<T> Children<T>(DependencyObject node) where T:DependencyObject => TemplateEditorPresentation.Children<T>(node);
}
'''
program='''public static class Program {
    [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    public static string Root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\\..\\..\\.."));
    [STAThread] public static int Main(string[] args) {
        string revit=@"C:\\Program Files\\Autodesk\\Revit 2023";SetDllDirectory(revit);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {var p=Path.Combine(revit,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        try{return Run(args);}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"production-error.txt"),e.ToString());return 1;}
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args) {
        typeof(TemplateEditorPresentation).GetProperty("PersistenceEnabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{if(args.Contains("--shared-only"))Evidence.RunSharedOnly();else Evidence.Run();app.Shutdown();return 0;}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"production-error.txt"),e.ToString());app.Shutdown();return 1;}
    }
}
}'''
(dest/'Fixtures.cs').write_text(header+fixtures+program,encoding='utf-8-sig')
shutil_source=(HERE/'ProductionCases.cs').read_text(encoding='utf-8-sig')
(dest/'ProductionCases.cs').write_text(shutil_source,encoding='utf-8-sig')
text=(HERE/'Evidence.cs').read_text(encoding='utf-8-sig').replace('SAB.TemplateEditorPreview.Models','SAB.ViewTemplateGraphics.Models').replace('SAB.TemplateEditorPreview.ViewModels','SAB.ViewTemplateGraphics.ViewModels').replace('SAB.TemplateEditorPreview.Views','SAB.ViewTemplateGraphics.Views').replace('namespace SAB.TemplateEditorPreview','namespace SAB.TemplateEditorProductionCheck')
text=text.replace(r'Docs\Ui\DesignSystem\screenshots-template-editor\revision-2',r'Docs\Ui\DesignSystem\screenshots-template-editor-production')
text=text.replace('new ViewTemplateGraphicsViewModel(null,Fixtures.Templates(),new Services.ViewTemplateGraphicsDataService())','Fixtures.CreateViewModel()')
text=text.replace('!string.IsNullOrEmpty(PreviewDialogs.LastMessage)','All<TextBlock>(child).Any(x=>x.Name=="TemplateEditorValidation"&&!string.IsNullOrEmpty(x.Text))')
text=text.replace('Scope: source-derived preview; synthetic Document adapter; original models/handlers. Production/Revit not executed.','Scope: actual SAB.dll production Window/VM/model/handlers; synthetic detached DTO loader; Revit operations not executed.')
text=text.replace('        public static void Run()', '''        public static void RunSharedOnly()
        {
            Directory.CreateDirectory(output);
            ProductionCases.Run((result,name)=> {log.Add((result?"PASS ":"FAIL ")+name);if(!result)throw new InvalidOperationException(name);},(window,name)=>Snapshot(window,name));
            log.Add("COMPLETED bounded recapture:8 assertions/1 actual WPF capture; production DLL/MSI unchanged");
            File.WriteAllLines(Path.Combine(output,"shared-regression-recapture.txt"),log);
        }
        public static void Run()''')
text=text.replace('window.Show();Pump();return window;', 'window.Show();window.Width=1780;window.Height=940;Pump();return window;')
text=text.replace('new CategoryOverrideRow {CategoryIdValue=99001', 'new CategoryOverrideRow {SupportsProjectionLines=true,SupportsHalftone=true,CategoryIdValue=99001')
text=text.replace('Check(Application.Current.Resources.Count==0', '''ProductionCases.Run(Check,(w,n)=>Snapshot(w,n));
            Check(typeof(ViewTemplateGraphicsWindow).Assembly==typeof(SAB.UI.FluentLight.Capsule).Assembly,"actual window and shared capsule loaded from production SAB.dll");
            Check(Application.Current.Resources.Count==0''')
text=text.replace('            foreach(var box in All<ComboBox>(window)', '''            if(window is ViewTemplateGraphicsWindow) {
                var card=Named<Border>(window,"SettingsCard");var status=Named<Border>(window,"SelectionStatusBorder");
                Check(Math.Abs(Bounds(status,window).Top-Bounds(card,window).Bottom-16)<1,context+" measured card-to-status gap16");
                Check(Math.Abs(Bounds(Named<Border>(window,"TemplateCard"),window).Bottom-Bounds(card,window).Bottom)<1,context+" both columns share bottom edge");
            }
            foreach(var box in All<ComboBox>(window)''')
# Tall user-reported state and transition back from wrapped tabs.
text=text.replace('            RevisionCases(window,vm,tabs);','''            window.Height=1080;Pump();Geometry(window,"tall user regression");TabGeometry(window,tabs,false);Snapshot(window,"tall-bottom-spacing");
            window.Height=940;Pump();
            RevisionCases(window,vm,tabs);''')
(dest/'Evidence.cs').write_text(text,encoding='utf-8-sig')
project=(HERE/'TemplateEditor.csproj').read_text(encoding='utf-8-sig')
project=project.replace('SAB.TemplateEditor.Preview','SAB.TemplateEditor.ProductionCheck').replace('..\\..\\..\\outputs\\ui-production-preview\\template-editor\\</OutputPath>','..\\..\\..\\outputs\\ui-production-preview\\template-editor\\production\\</OutputPath>')
project=project.replace('template-editor\\obj\\','template-editor\\production-host-obj\\').replace('plugin\\SAB.dll','template-editor\\production\\SAB.dll')
a=project.index('    <Compile Include="Preview.cs"/>');b=project.index('  </ItemGroup>',a)
project=project[:a]+'    <Compile Include="..\\..\\..\\outputs\\ui-production-preview\\template-editor\\production\\host-source\\*.cs" />\n'+project[b:]
(HERE/'ProductionCheck.csproj').write_text(project,encoding='utf-8')
print('Production harness references actual SAB.dll, generates only fixture/test files; geometry persistence disabled by internal process-only flag.')
