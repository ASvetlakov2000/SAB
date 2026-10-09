namespace SAB.InteriorElevations.Models
{
    public enum PlanLeaderBreakAngleType
    {
        Degrees90 = 90,
        Degrees135 = 135
    }

    public class PlanCornerMarkAlignmentSettings
    {
        public PlanLeaderBreakAngleType LeaderBreakAngle { get; set; }

        public double CornerOffsetMm { get; set; }
    }
}
