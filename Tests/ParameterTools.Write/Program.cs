using System;
using Autodesk.Revit.DB;
using SAB.ParameterTools;
using SAB.ParameterTools.Core;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
    private static bool Fails(Action action, string text)
    { try { action(); return false; } catch (InvalidOperationException e) { return e.Message.Contains(text); } }
    private static void Main()
    {
        var p = new Parameter { Current = "6-19" };
        Catalog.Write(p, "6-19 этаж");
        Assert(p.Pending == "6-19 этаж" && p.Current == "6-19", "Accepted deferred ADSK floor write is not rolled back by a stale read");
        Assert(p.ReadsAfterSet == 0, "No parameter value is read immediately after Set");
        p.Commit();
        Assert(RuleEngine.Compare(Catalog.Read(p), Resolution.Known("6-19 этаж")).Status == CheckStatus.Valid, "Committed deferred text passes final verification");
        p = new Parameter { Current = "6-19 этаж" };
        Catalog.Write(p, "6-19 этаж");
        Assert(p.Calls == 0, "Already matching value avoids Set");
        p = new Parameter { Current = "6-19", Accepted = false };
        Assert(Fails(() => Catalog.Write(p, "6-19 этаж"), "Set вернул False"), "Actual Set rejection remains an error");
        p = new Parameter { Current = "6-19", IsReadOnly = true };
        Assert(Fails(() => Catalog.Write(p, "6-19 этаж"), "только для чтения") && p.Calls == 0, "Read-only parameter is rejected before Set");
        p = new Parameter { Current = "6-19", ThrowOnSet = true };
        Assert(Fails(() => Catalog.Write(p, "6-19 этаж"), "API denial"), "Real API exception is preserved");
        p = new Parameter { StorageType = StorageType.Integer, Number = 5 };
        Catalog.Write(p, "0");
        Assert(p.ReadsAfterSet == 0 && p.PendingNumber == 0, "Deferred integer zero is accepted without stale read");
        p.Commit();
        Assert(RuleEngine.Compare(Catalog.Read(p), Resolution.Known("0")).Status == CheckStatus.Valid, "Committed integer zero verifies");
        p = new Parameter { StorageType = StorageType.Double, Number = 1 };
        Catalog.Write(p, "2,5"); p.Commit();
        Assert(RuleEngine.Compare(Catalog.Read(p), Resolution.Known("2,5")).Status == CheckStatus.Valid, "Deferred decimal verifies after commit");
        p = new Parameter { Current = "6-19" }; Catalog.Write(p, "6-19 этаж"); p.Commit(); p.Current = "6-19";
        Assert(RuleEngine.Compare(Catalog.Read(p), Resolution.Known("6-19 этаж")).Status == CheckStatus.Mismatch, "An updater reverting the value remains a final verification failure");
        Console.WriteLine("Passed " + checks + " write checks.");
    }
}
