namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Действие, которое нужно выполнить сразу после открытия окна.
    /// </summary>
    public enum RoomGeometryStartupAction
    {
        None = 0,
        CheckAngles = 1,
        CheckUnplaced = 2,
        CheckAreaChanges = 3,
        CreateAxesForSelectedRoom = 4,
        CreateAxesForActiveViewRooms = 5
    }
}

