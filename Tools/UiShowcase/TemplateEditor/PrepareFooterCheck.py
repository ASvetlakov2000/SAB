"""Build detached DTO fixtures only; footer check opens the actual SAB.dll class."""
from pathlib import Path
root=Path(__file__).resolve().parents[3]
here=Path(__file__).resolve().parent
out=root/'outputs/ui-production-preview/template-editor/footer/host-source'
out.mkdir(parents=True,exist_ok=True)
source=(here/'Preview.cs').read_text(encoding='utf-8-sig')
fixtures=source[source.index('    public static class Fixtures'):source.index('    public static class Program')]
fixtures=fixtures.replace('    public static class Fixtures\n    {','''    public static class Fixtures
    {
        public static ViewTemplateGraphicsViewModel CreateViewModel()
        {
            var ctor=typeof(ViewTemplateGraphicsViewModel).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(IList<TemplateSelectionItem>),typeof(Func<IList<TemplateSelectionItem>,ViewTemplateGraphicsData>)},null);
            return (ViewTemplateGraphicsViewModel)ctor.Invoke(new object[]{Templates(),new Func<IList<TemplateSelectionItem>,ViewTemplateGraphicsData>(Build)});
        }''')
header='''using System;using System.Collections.Generic;using System.Linq;using System.Reflection;
using Autodesk.Revit.DB;using SAB.ViewTemplateGraphics.Models;using SAB.ViewTemplateGraphics.ViewModels;
namespace SAB.TemplateEditorFooterCheck {
'''
(out/'Fixtures.cs').write_text(header+fixtures+'}',encoding='utf-8')
