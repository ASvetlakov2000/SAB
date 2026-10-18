"""One-shot approved revision2 UI migration. No build/install/publication."""
from Prepare import *
import copy, shutil

DOC=ROOT/'Docs/Ui/DesignSystem'
BASE=OUT/'production-baseline'
assert json.loads((DOC/'STATUS.json').read_text(encoding='utf-8-sig'))['templateEditorProduction']['productionMigrationAuthorized']
assert (MODULE/'Views/ViewTemplateGraphicsWindow.xaml').read_bytes()==(BASE/'SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml').read_bytes(), 'One-shot migration already applied; do not overwrite current production sources.'

for preview in (HERE/'Views').glob('*.xaml'):
    text=preview.read_text(encoding='utf-8-sig')
    text=text.replace(' · макет, тестовые данные','').replace('Макет · тестовые данные','')
    text=text.replace('CANONICAL_FLUENT_DICTIONARY','../../UI/Styles/SABFluentLight.xaml')
    text=text.replace('clr-namespace:SAB.TemplateEditorPreview;assembly=SAB.TemplateEditor.Preview','clr-namespace:SAB.ViewTemplateGraphics.Views;assembly=SAB')
    root=E.fromstring(text)
    if preview.name=='ViewTemplateGraphicsWindow.xaml':
        style=next(e for e in root.iter(W+'Style') if e.get(X+'Key')=='GraphicsTabControlStyle')
        for setter in list(style):style.remove(setter)
        style.set('BasedOn','{StaticResource SabWrappingTabControlStyle}')
        grid=list(root)[-1];middle=grid.find(W+'Grid');middle.set('Margin','24,16,24,16')
        cards=middle.findall(W+'Border')
        cards[0].set(X+'Name','TemplateCard');cards[1].set(X+'Name','SettingsCard')
        # Keep exactly16 DIP between the visible card and status edges.
        footer=grid.findall(W+'Border')[-1];footer.set('Padding','24,0,24,12');footer.set('BorderThickness','0')
        status=list(footer)[0].find(W+'Border');status.set(X+'Name','SelectionStatusBorder')
    footer=list(root)[-1].findall(W+'Border')[-1];layout=list(footer)[0]
    defs=layout.find(W+'Grid.RowDefinitions');actions=layout.find(W+'Grid')
    row=1 if preview.name=='ViewTemplateGraphicsWindow.xaml' else 0
    if row==1:defs.insert(1,E.Element(W+'RowDefinition',{'Height':'Auto'}));actions.set('Grid.Row','2')
    validation=E.SubElement(layout,W+'TextBlock',{'Grid.Row':str(row),'TextWrapping':'Wrap','Visibility':'Collapsed','Margin':'0,0,0,12','Foreground':'{DynamicResource SabBrush.Error}'})
    validation.set(X+'Name','TemplateEditorValidation')
    help_button=next(e for e in root.iter(W+'Button') if e.get(X+'Name')=='InstructionButton')
    help_button.set('{clr-namespace:SAB.UI.FluentLight;assembly=SAB}Instructions.File','SAB_HTML_Redaktor_shablonov.html')
    (MODULE/'Views'/preview.name).write_text(E.tostring(root,encoding='unicode'),encoding='utf-8-sig')

# Opt-in shared wrapping style derives the existing canonical template.
path=ROOT/'SAB/UI/Styles/SABFluentLight.xaml';root=E.parse(path).getroot()
canonical=next(e for e in root.findall(W+'Style') if e.get('TargetType')=='TabControl')
style=copy.deepcopy(canonical);style.set(X+'Key','SabWrappingTabControlStyle');style.set('BasedOn','{StaticResource {x:Type TabControl}}')
for element in style.iter():
    if element.tag==W+'TabPanel':element.tag=W+'WrapPanel'
root.append(style)
# Insert only the new style, preserving the canonical file's comments/format.
text=path.read_text(encoding='utf-8-sig');snippet=E.tostring(style,encoding='unicode')
path.write_text(text.replace('</ResourceDictionary>',snippet+'\n</ResourceDictionary>'),encoding='utf-8-sig')

path=ROOT/'SAB/UI/FluentLight/Presentation.cs';text=path.read_text(encoding='utf-8-sig')
text=text.replace('indicator.Height = Math.Max(30, grid.ActualHeight - 4);','indicator.VerticalAlignment = VerticalAlignment.Top;\n            indicator.Height = Math.Max(30, active.ActualHeight);')
text=text.replace('new CornerRadius(grid.ActualHeight / 2)','new CornerRadius(grid.ActualHeight > active.ActualHeight + 5 ? 22 : grid.ActualHeight / 2)')
text=text.replace('Math.Abs((double)indicator.GetValue(TargetProperty) - target) < .1','Math.Abs((double)indicator.GetValue(TargetProperty) - target) < .1 && Math.Abs((double)indicator.GetValue(TargetYProperty) - pos.Y) < .1')
text=text.replace('indicator.SetValue(TargetProperty, target);','indicator.SetValue(TargetProperty, target);\n            indicator.SetValue(TargetYProperty, pos.Y);')
text=text.replace('else { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = target; }','else { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = target; }\n            if (animate && SystemParameters.ClientAreaAnimation) transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(pos.Y, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });\n            else { transform.BeginAnimation(TranslateTransform.YProperty, null); transform.Y = pos.Y; }')
text=text.replace('private static readonly DependencyProperty TargetProperty =', 'private static readonly DependencyProperty TargetYProperty = DependencyProperty.RegisterAttached("TargetY", typeof(double), typeof(Capsule), new PropertyMetadata(double.NaN));\n        private static readonly DependencyProperty TargetProperty =')
path.write_text(text,encoding='utf-8-sig')

text=(HERE/'Presentation.cs').read_text(encoding='utf-8-sig')
start=text.index('    // Geometry adapter');end=text.index('    public sealed class SearchActiveConverter')
text=text[:start]+text[end:]
text=text.replace('namespace SAB.TemplateEditorPreview','namespace SAB.ViewTemplateGraphics.Views').replace('using SAB.TemplateEditorPreview.Models;','using SAB.ViewTemplateGraphics.Models;').replace('PreviewShell.Children','TemplateEditorPresentation.Children')
(MODULE/'Views/CategoryPresentation.cs').write_text(text,encoding='utf-8-sig')

path=MODULE/'Models/ViewTemplateGraphicsModels.cs';text=path.read_text(encoding='utf-8-sig').replace('Math.Max(0, IndentLevel) * 18.0','Math.Max(0, IndentLevel) * 24.0').replace('public bool AllowsVisibilityControl { get; set; }','public bool AllowsVisibilityControl { get; set; }\n        public bool SupportsProjectionLines { get; set; }\n        public bool SupportsHalftone { get; set; }');path.write_text(text,encoding='utf-8-sig')
for path in (MODULE/'Views').glob('*.xaml.cs'):
    text=path.read_text(encoding='utf-8-sig').replace('using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;','using RevitTaskDialog = SAB.ViewTemplateGraphics.Views.TemplateEditorPresentation;')
    text=text.replace('DataContext = _viewModel;','DataContext = _viewModel;\n            TemplateEditorPresentation.Attach(this);').replace('DataContext = this;','DataContext = this;\n            TemplateEditorPresentation.Attach(this);')
    if path.name=='ViewTemplateGraphicsWindow.xaml.cs':
        text=text.replace('List<CategoryOverrideRow> selectedRows = GetSelectedRows(button, categoryRow);','List<CategoryOverrideRow> selectedRows = GetSelectedRows(button, categoryRow);\n                    selectedRows.RemoveAll(row => !CategoryPresentation.SupportsEditor(row, GetEditorSection(action)));')
        text=text.replace('selected[i].Graphics.HalftoneState = value;','if (selected[i].SupportsHalftone) selected[i].Graphics.HalftoneState = value;',1)
    if path.name=='GraphicOverrideEditorWindow.xaml.cs':text=text.replace('return 700.0;','return 560.0;').replace('return 460.0;','return 420.0;')
    path.write_text(text,encoding='utf-8-sig')

path=ROOT/'SAB/SAB.csproj';text=path.read_text(encoding='utf-8-sig')
anchor='    <Compile Include="Cls_ViewTemplateGraphics\\Models\\ViewTemplateGraphicsModels.cs" />'
text=text.replace(anchor,anchor+'\n    <Compile Include="Cls_ViewTemplateGraphics\\Views\\CategoryPresentation.cs" />\n    <Compile Include="Cls_ViewTemplateGraphics\\Views\\TemplateEditorPresentation.cs" />\n    <Compile Include="Cls_ViewTemplateGraphics\\Services\\CategoryCapabilities.cs" />')
path.write_text(text,encoding='utf-8-sig')
print('Approved UI migrated; source baseline retained. Build and actual DLL verification still required.')
