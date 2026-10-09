using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.ParameterTools.Core
{
    public enum RoomResolutionStatus { Found, NotFound, Ambiguous, Insufficient, Blocked, Unsupported, Error }
    public static class RoomEvidence
    {
        public static RoomResolutionStatus Family(IEnumerable<string> native, IEnumerable<string> points,
            bool explicitCalculationPoint, out string[] candidates)
        {
            var n = Distinct(native); var p = Distinct(points);
            candidates = Distinct(n.Concat(p));
            if (candidates.Length > 1) return RoomResolutionStatus.Ambiguous;
            if (explicitCalculationPoint && p.Length == 0 && n.Length > 0) return RoomResolutionStatus.Insufficient;
            return candidates.Length == 1 ? RoomResolutionStatus.Found : RoomResolutionStatus.NotFound;
        }
        public static string[] Distinct(IEnumerable<string> ids)
        { return (ids ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray(); }
    }
}
