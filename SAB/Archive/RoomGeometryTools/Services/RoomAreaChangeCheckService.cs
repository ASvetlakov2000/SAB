using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис проверки изменения площади помещений.
    /// </summary>
    public class RoomAreaChangeCheckService
    {
        public const string ApprovedAreaParameterName = "SA_Помещения_Утвержденная площадь";

        public IList<RoomAreaChangeIssue> CheckRooms(IList<Room> rooms, double thresholdPercent, out string warningMessage)
        {
            warningMessage = string.Empty;
            List<RoomAreaChangeIssue> issues = new List<RoomAreaChangeIssue>();

            if (rooms == null || rooms.Count == 0)
            {
                return issues;
            }

            bool parameterExists = IsApprovedParameterExists(rooms);

            if (!parameterExists)
            {
                warningMessage = "Не найден параметр \"SA_Помещения_Утвержденная площадь\". Добавьте параметр к категории Помещения и повторите проверку.";
                return issues;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                Level level = room.Document.GetElement(room.LevelId) as Level;
                string levelName = level != null ? level.Name : "Без уровня";
                string roomNumber = RevitParameterUtils.GetRoomNumber(room);
                string roomName = RevitParameterUtils.GetRoomName(room);

                Parameter approvedParameter = room.LookupParameter(ApprovedAreaParameterName);
                if (approvedParameter == null)
                {
                    issues.Add(CreateIssue(room, levelName, roomNumber, roomName, 0, 0, 0, 0, "Параметр утвержденной площади отсутствует у помещения."));
                    continue;
                }

                double approvedSquareMeters;
                if (!TryReadApprovedAreaSquareMeters(approvedParameter, out approvedSquareMeters))
                {
                    issues.Add(CreateIssue(room, levelName, roomNumber, roomName, 0, 0, 0, 0, "Параметр утвержденной площади не заполнен или имеет неверный формат."));
                    continue;
                }

                if (approvedSquareMeters <= 1e-9)
                {
                    issues.Add(CreateIssue(room, levelName, roomNumber, roomName, approvedSquareMeters, 0, 0, 0, "Утвержденная площадь должна быть больше нуля."));
                    continue;
                }

                double currentSquareMeters = RevitUnitUtils.InternalAreaToSquareMeters(room.Area);
                double delta = currentSquareMeters - approvedSquareMeters;
                double deltaPercent = Math.Abs(delta) / approvedSquareMeters * 100.0;

                if (deltaPercent > thresholdPercent)
                {
                    string message = "Отклонение площади превышает допустимое значение.";
                    issues.Add(CreateIssue(
                        room,
                        levelName,
                        roomNumber,
                        roomName,
                        approvedSquareMeters,
                        currentSquareMeters,
                        delta,
                        deltaPercent,
                        message));
                }
            }

            return issues;
        }

        private static bool IsApprovedParameterExists(IList<Room> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                Parameter parameter = room.LookupParameter(ApprovedAreaParameterName);
                if (parameter != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadApprovedAreaSquareMeters(Parameter parameter, out double approvedSquareMeters)
        {
            approvedSquareMeters = 0.0;

            if (parameter == null)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                double internalArea = parameter.AsDouble();
                approvedSquareMeters = RevitUnitUtils.InternalAreaToSquareMeters(internalArea);
                return approvedSquareMeters > 1e-9;
            }

            if (parameter.StorageType == StorageType.String)
            {
                string raw = parameter.AsString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                raw = raw.Trim().Replace(',', '.');
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out approvedSquareMeters))
                {
                    return approvedSquareMeters > 1e-9;
                }
            }

            string valueString = parameter.AsValueString();
            if (!string.IsNullOrWhiteSpace(valueString))
            {
                valueString = valueString.Replace("м²", string.Empty).Replace("m²", string.Empty).Trim().Replace(',', '.');
                if (double.TryParse(valueString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out approvedSquareMeters))
                {
                    return approvedSquareMeters > 1e-9;
                }
            }

            return false;
        }

        private static RoomAreaChangeIssue CreateIssue(
            Room room,
            string levelName,
            string roomNumber,
            string roomName,
            double approvedArea,
            double currentArea,
            double delta,
            double deltaPercent,
            string message)
        {
            return new RoomAreaChangeIssue
            {
                RoomId = room != null ? room.Id : ElementId.InvalidElementId,
                LevelName = levelName ?? "Без уровня",
                RoomNumber = roomNumber ?? string.Empty,
                RoomName = roomName ?? string.Empty,
                ApprovedAreaSquareMeters = approvedArea,
                CurrentAreaSquareMeters = currentArea,
                DeltaAreaSquareMeters = delta,
                DeltaPercent = deltaPercent,
                Message = message ?? string.Empty
            };
        }
    }
}

