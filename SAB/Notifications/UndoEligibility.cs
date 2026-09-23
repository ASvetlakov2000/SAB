using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.Notifications
{
    public static class UndoEligibility
    {
        public static bool Matches(long capturedRevision, long previousRevision, string failureTransaction,
            IEnumerable<string> committedNames, bool deletionRequested, int deletedCount)
        {
            if (capturedRevision != previousRevision) return false;
            return deletionRequested ? deletedCount > 0 :
                !string.IsNullOrEmpty(failureTransaction) && committedNames.Contains(failureTransaction);
        }
    }
}
