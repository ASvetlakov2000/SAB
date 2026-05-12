namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Операции UI, которые должны выполняться в Revit API-контексте через ExternalEvent.
    /// </summary>
    public enum RoomGeometryUiOperation
    {
        None = 0,
        CheckAngles = 1,
        CheckUnplacedRooms = 2,
        CheckAreaChanges = 3,
        ShowProblematicAngles = 4,
        CreateAxesForSelectedRoom = 5,
        CreateAxesForActiveViewRooms = 6
    }
}

