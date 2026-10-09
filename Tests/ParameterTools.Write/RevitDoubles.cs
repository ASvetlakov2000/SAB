// Simulates deferred visibility of values; does not execute the Revit transaction engine.
namespace Autodesk.Revit.DB
{
    public enum StorageType { None, String, Integer, Double, ElementId }
    public class ForgeTypeId { public string TypeId; }
    public static class SpecTypeId
    {
        public static ForgeTypeId Number = new ForgeTypeId { TypeId = "Number" };
        public static ForgeTypeId Currency = new ForgeTypeId { TypeId = "Currency" };
    }
    public class Definition
    {
        public ForgeTypeId Type = SpecTypeId.Number;
        public ForgeTypeId GetDataType() => Type;
    }
    public class Parameter
    {
        public Definition Definition = new Definition();
        public StorageType StorageType = StorageType.String;
        public bool IsReadOnly, HasValue = true, Accepted = true, ThrowOnSet;
        public int Calls, ReadsAfterSet;
        public string Current, Pending;
        public double Number, PendingNumber;
        public string AsString() { if (Calls > 0) ReadsAfterSet++; return Current; }
        public string AsValueString() => Current;
        public double AsDouble() { if (Calls > 0) ReadsAfterSet++; return Number; }
        public int AsInteger() => (int)AsDouble();
        public bool Set(string value) { Calls++; if (ThrowOnSet) throw new System.InvalidOperationException("API denial"); Pending = value; return Accepted; }
        public bool Set(double value) { Calls++; PendingNumber = value; return Accepted; }
        public bool Set(int value) => Set((double)value);
        public void Commit() { if (Accepted) { Current = Pending; Number = PendingNumber; } }
    }
}
namespace SAB.ParameterTools
{
    internal static partial class Catalog
    {
        internal static string DataType(Autodesk.Revit.DB.Definition d) => d.Type.TypeId;
    }
}
