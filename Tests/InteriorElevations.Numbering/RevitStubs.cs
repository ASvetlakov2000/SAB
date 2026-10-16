using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Autodesk.Revit.DB
{
    public enum BuiltInParameter { VIEWPORT_DETAIL_NUMBER }
    public enum BuiltInCategory { OST_GenericAnnotation = -2000150, OST_Walls = -2000011 }
    public enum StorageType { String }
    public enum TransactionStatus { Started, Committed, RolledBack }
    public class ElementId
    {
        public static ElementId InvalidElementId = new ElementId(-1);
        public int IntegerValue { get; }
        public ElementId(int value) { IntegerValue = value; }
    }
    public class Category { public ElementId Id; }
    public class Element
    {
        public ElementId Id;
        public bool IsValidObject = true;
        public Category Category;
        public string Name;
    }
    public class Family : Element { }
    public class FamilySymbol : Element { }
    public class View : Element { }
    public class Parameter
    {
        public string Value;
        public bool IsReadOnly;
        public bool Reject;
        public StorageType StorageType => StorageType.String;
        public string AsString() => Value;
        public bool Set(string value) { if (Reject) return false; Value = value; return true; }
    }
    public class Viewport : Element
    {
        public Parameter Number = new Parameter();
        public Parameter get_Parameter(BuiltInParameter parameter) => Number;
    }
    public class Document
    {
        public View ActiveView = new View();
        public List<Element> Elements = new List<Element>();
        public List<string> Loaded = new List<string>();
        public int Transactions;
        public Element GetElement(ElementId id) => Elements.FirstOrDefault(e => e.Id?.IntegerValue == id.IntegerValue);
        public bool LoadFamily(string path, out Family family)
        {
            Loaded.Add(Path.GetFileNameWithoutExtension(path));
            family = new Family { Name = Loaded.Last() }; Elements.Add(family); return true;
        }
    }
    public class FilteredElementCollector : IEnumerable<Element>
    {
        readonly Document document; Type type;
        public FilteredElementCollector(Document document) { this.document = document; }
        public FilteredElementCollector OfClass(Type value) { type = value; return this; }
        public IEnumerator<Element> GetEnumerator() => document.Elements.Where(e => type.IsInstanceOfType(e)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class Transaction
    {
        readonly Document document; TransactionStatus status;
        public Transaction(Document document, string name) { this.document = document; }
        public void Start() { document.Transactions++; status = TransactionStatus.Started; }
        public void Commit() { status = TransactionStatus.Committed; }
        public void RollBack() { status = TransactionStatus.RolledBack; }
        public TransactionStatus GetStatus() => status;
    }
}
namespace Autodesk.Revit.UI { public static class TaskDialog { public static void Show(string title, string text) { } } }
