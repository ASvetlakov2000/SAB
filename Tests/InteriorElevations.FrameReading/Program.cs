using System;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Services.Sheets;

// API doubles exercise the production reader's lifecycle and coordinate handling.
// Real Revit behavior is validated separately by target builds and in-model testing.
internal static class Program
{
    private static int checks;
    private static void Main()
    {
        var project = new Document();
        var sheet = new ViewSheet { Id = new ElementId(10), SheetNumber = "1" };
        var definition = new Document();
        var format = new FamilyParameter { Definition = new Definition { Name = "Формат А" }, IsInstance = true, StorageType = StorageType.Integer };
        definition.FamilyManager.Parameters.Add(format);
        definition.FamilyManager.Types.Add(new FamilyType { Name = "Другой" });
        definition.FamilyManager.Types.Add(new FamilyType { Name = "Основной" });
        definition.OnRegenerate = () =>
        {
            definition.Elements.Clear();
            double width = definition.FamilyManager.LastInteger == 2 ? 594 / 304.8 : 420 / 304.8;
            Frame(definition, 0, 0, width, 420 / 304.8);
            Frame(definition, 20 / 304.8, 5 / 304.8, width - 5 / 304.8, 415 / 304.8);
        };
        var family = new Family { Name = "Штамп", DefinitionDocument = definition };
        var symbol = new FamilySymbol { Id = new ElementId(100), Name = "Основной", Family = family };
        var block = Block(project, symbol, sheet.Id, 2);
        block.InstanceTransform = new Transform { Offset = new XYZ(-594 / 304.8, 0, 0) };
        project.Elements.Add(block);
        var reader = new SheetWorkspaceService();
        project.IsModifiable = true;
        Failure(() => reader.PrepareFamilyGeometry(project, sheet), "Reject EditFamily inside project transaction");
        project.IsModifiable = false;
        reader.PrepareFamilyGeometry(project, sheet);
        Check(definition.ClosedWithoutSaving, "Family copy closed without saving or loading into project");
        Check(definition.FamilyManager.CurrentType.Name == symbol.Name, "Selected family type evaluated");
        Check(definition.FamilyManager.LastInteger == 2, "Actual instance format passed to family manager");
        project.IsModifiable = true;
        var frames = reader.ReadFrames(project, sheet);
        Check(Close(frames.Outer.Width * 304.8, 594), "Null project geometry uses evaluated family curves");
        Check(Close(frames.Outer.Left * 304.8, -594), "Instance translation applied exactly once");
        Check(Close(frames.Inner.Left * 304.8, -574), "Asymmetric inner frame preserved");
        Check(block.GeometryReads == 0, "Cached reader does not request null annotation geometry");
        System.Collections.Generic.IList<SheetRectangle> reserved;
        var planArea = reader.ReadPlanWorkspace(project, sheet, -560 / 304.8, 400 / 304.8, out reserved);
        var expectedArea = frames.GetWorkspace(-560 / 304.8, 400 / 304.8);
        Check(Close(planArea.Right, expectedArea.Right) && Close(planArea.Bottom, expectedArea.Bottom), "Plan click and placement share mirrored workspace reference");
        var continuation = new ViewSheet { Id = new ElementId(11), SheetNumber = "2" };
        var copied = Block(project, symbol, continuation.Id, 2);
        copied.InstanceTransform = block.InstanceTransform;
        project.Elements.Add(copied);
        var nextFrames = reader.ReadFrames(project, continuation);
        Check(Close(nextFrames.Inner.Width, frames.Inner.Width), "Copied sheet reuses same evaluated format");
        Check(project.EditFamilyCalls == 1, "Continuation requires no EditFamily inside placement transaction");
        var nestedDefinition = new Document();
        Frame(nestedDefinition, 0, 0, 300 / 304.8, 200 / 304.8);
        Frame(nestedDefinition, 5 / 304.8, 5 / 304.8, 295 / 304.8, 195 / 304.8);
        var nestedSymbol = new FamilySymbol { Id = new ElementId(101), Name = "Вложенная рамка",
            Family = new Family { DefinitionDocument = nestedDefinition } };
        var containerDefinition = new Document();
        containerDefinition.Elements.Add(new FamilyInstance { Document = containerDefinition, Symbol = nestedSymbol,
            InstanceTransform = new Transform { Offset = new XYZ(10 / 304.8, 20 / 304.8, 0) } });
        var container = new FamilySymbol { Id = new ElementId(102), Name = "Контейнер",
            Family = new Family { DefinitionDocument = containerDefinition } };
        var nestedProject = new Document();
        var nestedBlock = Block(nestedProject, container, sheet.Id, 2);
        nestedBlock.InstanceTransform = new Transform { Offset = new XYZ(-310 / 304.8, -20 / 304.8, 0) };
        nestedProject.Elements.Add(nestedBlock);
        var nestedReader = new SheetWorkspaceService();
        nestedReader.PrepareFamilyGeometry(nestedProject, sheet);
        nestedProject.IsModifiable = true;
        var nestedFrames = nestedReader.ReadFrames(nestedProject, sheet);
        Check(Close(nestedFrames.Outer.Left * 304.8, -300) && Close(nestedFrames.Outer.Bottom, 0), "Nested and sheet transforms combined once");
        Check(nestedDefinition.ClosedWithoutSaving && containerDefinition.ClosedWithoutSaving, "Nested family documents closed without saving");
        var viewport = new Viewport { Box = new Outline { MinimumPoint = new XYZ(1, 2, 0), MaximumPoint = new XYZ(3, 4, 0), IsEmpty = true } };
        var bounds = new SheetFootprintService().Measure(project, sheet, viewport, null);
        Check(Close(bounds.Width, 2) && Close(bounds.Height, 2), "Planar viewport measured from X/Y even with empty 3D outline flag");
        Console.WriteLine("PASS: " + checks + " frame-reading regression checks (API doubles; no live Revit execution)");
    }
    private static FamilyInstance Block(Document project, FamilySymbol symbol, ElementId sheet, int format)
    {
        var block = new FamilyInstance { Document = project, Symbol = symbol, OwnerViewId = sheet,
            Category = new Category { Id = new ElementId(BuiltInCategory.OST_TitleBlocks) } };
        block.Parameters["Формат А"] = new Parameter { StorageType = StorageType.Integer, IntegerValue = format };
        return block;
    }
    private static void Frame(Document document, double left, double bottom, double right, double top)
    {
        var points = new[] { new XYZ(left, bottom, 0), new XYZ(right, bottom, 0), new XYZ(right, top, 0), new XYZ(left, top, 0) };
        for (int i = 0; i < 4; i++) document.Elements.Add(new CurveElement { GeometryCurve = new Line(points[i], points[(i + 1) % 4]) });
    }
    private static bool Close(double a, double b) => Math.Abs(a - b) < 1e-7;
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static void Failure(Action action, string label)
    {
        bool failed = false;
        try { action(); } catch (InvalidOperationException) { failed = true; }
        Check(failed, label);
    }
}
