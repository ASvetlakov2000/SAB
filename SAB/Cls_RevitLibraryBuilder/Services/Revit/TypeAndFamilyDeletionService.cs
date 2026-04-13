using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitLibraryBuilder.Services.Revit
{
    public class TypeAndFamilyDeletionService
    {
        public DeletionResult DeleteTypesAndFamilies(
            Document document,
            IList<ElementId> typeIds,
            IList<ElementId> familyIds)
        {
            DeletionResult result = new DeletionResult();

            if (document == null)
            {
                result.Errors.Add("Document is not available.");
                return result;
            }

            if (typeIds == null)
            {
                typeIds = new List<ElementId>();
            }

            if (familyIds == null)
            {
                familyIds = new List<ElementId>();
            }

            // Block responsible for deletion loop of selected types
            for (int i = 0; i < typeIds.Count; i++)
            {
                ElementId id = typeIds[i];

                if (id == null || id == ElementId.InvalidElementId)
                {
                    continue;
                }

                TryDeleteSingleElement(document, id, "Type", result);
            }

            // Block responsible for deletion loop of selected families
            for (int i = 0; i < familyIds.Count; i++)
            {
                ElementId id = familyIds[i];

                if (id == null || id == ElementId.InvalidElementId)
                {
                    continue;
                }

                TryDeleteSingleElement(document, id, "Family", result);
            }

            return result;
        }

        // Block responsible for transaction and error handling of each deletion
        private static void TryDeleteSingleElement(
            Document document,
            ElementId id,
            string groupName,
            DeletionResult result)
        {
            Element element = document.GetElement(id);

            if (element == null)
            {
                return;
            }

            string elementName = element.Name;

            try
            {
                using (Transaction transaction = new Transaction(document, "Delete " + groupName))
                {
                    transaction.Start();
                    document.Delete(id);
                    transaction.Commit();
                }

                result.DeletedCount++;
            }
            catch (Exception exception)
            {
                string error = groupName + " \"" + elementName + "\" was not deleted. " + exception.Message;
                result.Errors.Add(error);
            }
        }
    }

    public class DeletionResult
    {
        public int DeletedCount { get; set; }

        public List<string> Errors { get; private set; } = new List<string>();
    }
}
