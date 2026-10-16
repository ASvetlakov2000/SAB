using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Services.Sheets;
using SAB.Services.PluginResources;

static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    static IDictionary<int, string> Numbers(string[] used, int[] indices, string prefix = "", string suffix = "-Р") =>
        ElevationDetailNumberService.ResolveGroupNumbers(used, indices, prefix, suffix);
    static FamilySymbol Symbol(int id, BuiltInCategory category = BuiltInCategory.OST_GenericAnnotation) =>
        new FamilySymbol { Id = new ElementId(id), Category = new Category { Id = new ElementId((int)category) } };
    static void Main()
    {
        Check(Numbers(new[] { "1", "2", "3" }, new[] { 1, 2, 3 })[2] == "2-Р", "Ordinary Revit numbers do not skip sequence");
        var duplicate = Numbers(new[] { "1-Р", "3-Р" }, new[] { 1, 2, 3 });
        Check(duplicate.Values.SequenceEqual(new[] { "1-Р-2", "2-Р-2", "3-Р-2" }), "Collision changes suffix of whole group");
        Check(Numbers(new[] { "9-Р", "8-Р-2" }, new[] { 1 })[1] == "1-Р-3", "Surviving tail reserves group namespace");
        Check(Numbers(Array.Empty<string>(), new[] { 2, 4 })[4] == "4-Р", "Real source indices survive failed view gaps");
        Check(Numbers(new[] { "Р[1]+A" }, new[] { 1, 2 }, "Р[", "]+A")[2] == "Р[2]+A-2", "Affixes are literal including regex characters");
        Check(Numbers(new[] { "1" }, new[] { 1, 2 }, "", "")[1] == "1-2", "Empty affixes have safe collision suffix");
        Check(Numbers(Array.Empty<string>(), new[] { 1 }, "ЭЛВ-", "-А")[1] == "ЭЛВ-1-А", "User prefix and suffix surround numeric sequence");
        Check(Numbers(new[] { "1-р" }, new[] { 1 })[1] == "1-Р-2", "Namespaces compare case insensitively");
        var doc = new Document(); var viewport = new Viewport(); viewport.Number.Value = "7-Р"; doc.Elements.Add(viewport);
        Check(new ElevationDetailNumberService().ReserveGroupNumbers(doc, new[] { 1 }, "", "-Р")[1] == "1-Р-2", "Reservation reads all document viewports");
        ElevationDetailNumberService.Apply(viewport, "ЭЛВ-2-А");
        Check(ElevationDetailNumberService.Read(viewport) == "ЭЛВ-2-А", "Actual writer sets and reads system parameter");
        viewport.Number.IsReadOnly = true;
        try { ElevationDetailNumberService.Apply(viewport, "x"); throw new Exception("Expected error"); }
        catch (InvalidOperationException) { Check(viewport.Number.Value == "ЭЛВ-2-А", "Read-only parameter fails without replacing number"); }
        viewport.Number.IsReadOnly = false; viewport.Number.Reject = true;
        try { ElevationDetailNumberService.Apply(viewport, "x"); throw new Exception("Expected error"); }
        catch (InvalidOperationException) { Check(true, "Rejected parameter write is not reported as success"); }

        var service = new PluginFamilyResourceService(); var warnings = new List<string>();
        var load = typeof(PluginFamilyResourceService).GetMethod("TryLoadFamilyIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        string folder = Path.Combine(AppContext.BaseDirectory, "Families for Plugin", nameof(Program)); Directory.CreateDirectory(folder);
        string plan = Path.Combine(folder, PluginFamilyResourceService.PlanCornerMarkFamilyName + ".rfa");
        string sheet = Path.Combine(folder, PluginFamilyResourceService.SheetCornerMarkFamilyName + ".rfa");
        File.WriteAllText(plan, "test fixture"); File.WriteAllText(sheet, "test fixture");
        var existing = new Document(); existing.Elements.Add(new Family { Name = PluginFamilyResourceService.PlanCornerMarkFamilyName });
        load.Invoke(service, new object[] { existing, plan, warnings, null });
        Check(existing.Loaded.Count == 0, "Existing bundled family is never force reloaded");
        load.Invoke(service, new object[] { existing, Path.Combine(folder, "absent", Path.GetFileName(plan)), warnings, null });
        Check(warnings.Count == 0, "Existing family does not require a disk file");
        var missing = new Document(); load.Invoke(service, new object[] { missing, plan, warnings, null }); load.Invoke(service, new object[] { missing, plan, warnings, null });
        Check(missing.Loaded.Count == 1, "Missing family loads once then skips");
        var custom = new Document(); custom.Elements.Add(Symbol(1)); custom.Elements.Add(Symbol(2));
        service.EnsureCornerMarkFamiliesLoaded(custom, typeof(Program), new ElementId(1), null, new ElementId(2), null, warnings);
        Check(custom.Loaded.Count == 0 && custom.Transactions == 0, "Selected custom families avoid default load and transaction");
        var mixed = new Document(); mixed.Elements.Add(Symbol(1));
        service.EnsureCornerMarkFamiliesLoaded(mixed, typeof(Program), new ElementId(1), null, null, null, warnings);
        Check(mixed.Loaded.SequenceEqual(new[] { PluginFamilyResourceService.SheetCornerMarkFamilyName }), "Only missing sheet role loads; selected custom plan is preserved");
        service.EnsureCornerMarkFamiliesLoaded(custom, typeof(Program), new ElementId(999), new ElementId(1), new ElementId(999), new ElementId(2), warnings);
        Check(custom.Loaded.Count == 0, "Stale preference IDs fall back to valid saved selected types");
        var invalid = new Document(); invalid.Elements.Add(Symbol(1, BuiltInCategory.OST_Walls));
        service.EnsureCornerMarkFamiliesLoaded(invalid, typeof(Program), new ElementId(1), null, null, null, warnings);
        Check(invalid.Loaded.Count == 2, "Wrong category cannot suppress required mark family loads");
        Check(warnings.Count == 0, "Successful family scenarios add no warnings");
        Console.WriteLine("COMPLETED " + checks + " checks; Revit API stub boundary, not actual Revit execution.");
    }
}
