using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace SAB.Instructions
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class OpenInstructionsCommand : ExternalCommand
    {
        public override void Execute()
        {
            string error;
            if (InstructionLauncher.TryOpen(InstructionLauncher.IndexFile, out error)) return;
            TaskDialog.Show("Инструкции SAB", error);
            Result = Autodesk.Revit.UI.Result.Cancelled;
        }
    }

    public sealed class InstructionsAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        { return true; }
    }
}
