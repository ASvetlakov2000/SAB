using System;
using System.IO;
using SAB.Instructions;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string label)
    { if (!condition) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
    private static void Main()
    {
        string root = Path.GetFullPath(Path.Combine("outputs", "instructions", "resolve-" + Guid.NewGuid().ToString("N")));
        string installed = Path.Combine(root, "installed", "SAB");
        string docs = Path.Combine(installed, "Docs", "PluginInstructions");
        Directory.CreateDirectory(Path.Combine(docs, "assets"));
        File.WriteAllText(Path.Combine(docs, "assets", "template.css"), "test");
        File.WriteAllText(Path.Combine(docs, InstructionLauncher.IndexFile), "test");
        File.WriteAllText(Path.Combine(docs, InstructionLauncher.ParametersFile), "test");
        Assert(InstructionLauncher.Resolve(installed, InstructionLauncher.IndexFile) == Path.Combine(docs, InstructionLauncher.IndexFile), "Installed catalog opens next to SAB.dll");
        Assert(InstructionLauncher.Resolve(installed, InstructionLauncher.ParametersFile) == Path.Combine(docs, InstructionLauncher.ParametersFile), "Parameter shortcut opens the same installed documentation");
        try { InstructionLauncher.Resolve(installed, "../other.html"); throw new Exception("Unexpected path accepted"); }
        catch (ArgumentException) { Assert(true, "Only known instruction entry points are accepted"); }
        string sourceRoot = Path.Combine(root, "checkout");
        string sourceDocs = Path.Combine(sourceRoot, "Docs", "PluginInstructions");
        string sourceAssembly = Path.Combine(sourceRoot, "SAB", "bin", "Revit2023");
        Directory.CreateDirectory(sourceAssembly); Directory.CreateDirectory(Path.Combine(sourceDocs, "assets"));
        File.WriteAllText(Path.Combine(sourceRoot, "SAB", "SAB.csproj"), "test");
        File.WriteAllText(Path.Combine(sourceDocs, "assets", "template.css"), "test");
        File.WriteAllText(Path.Combine(sourceDocs, InstructionLauncher.IndexFile), "test");
        Assert(InstructionLauncher.Resolve(sourceAssembly, InstructionLauncher.IndexFile) == Path.Combine(sourceDocs, InstructionLauncher.IndexFile), "Source checkout fallback resolves repository documentation");
        File.Delete(Path.Combine(docs, "assets", "template.css"));
        try { InstructionLauncher.Resolve(installed, InstructionLauncher.IndexFile); throw new Exception("Missing style accepted"); }
        catch (FileNotFoundException e) { Assert(e.Message.Contains("Повторно установите SAB"), "Missing installed style explains how to recover"); }
        try { InstructionLauncher.Resolve(Path.Combine(root, "missing"), InstructionLauncher.IndexFile); throw new Exception("Missing HTML accepted"); }
        catch (FileNotFoundException e) { Assert(e.Message.Contains(InstructionLauncher.IndexFile), "Missing catalog reports the exact expected file"); }
        Console.WriteLine("All " + checks + " instruction resolution checks passed.");
    }
}
