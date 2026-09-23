using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IProfileMetadataProvider
    {
        ProfileMetadata GetProfileMetadata(Mullion mullion, FrameMemberPurpose purpose);
    }

    public sealed class ProfileMetadataProvider : IProfileMetadataProvider
    {
        private readonly IDictionary<string, ProfileMetadata> _fallbackByFamily;

        public ProfileMetadataProvider()
        {
            _fallbackByFamily = new Dictionary<string, ProfileMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "CW",
                    new ProfileMetadata
                    {
                        ProfileFamily = "CW",
                        ProfileType = "CW75",
                        ProfileSize = "75x50x0.6",
                        NominalWidthMm = 75.0,
                        NominalFlangeHeightMm = 50.0,
                        NominalThicknessMm = 0.6
                    }
                },
                {
                    "UW",
                    new ProfileMetadata
                    {
                        ProfileFamily = "UW",
                        ProfileType = "UW75",
                        ProfileSize = "75x40x0.6",
                        NominalWidthMm = 75.0,
                        NominalFlangeHeightMm = 40.0,
                        NominalThicknessMm = 0.6
                    }
                }
            };
        }

        public ProfileMetadata GetProfileMetadata(Mullion mullion, FrameMemberPurpose purpose)
        {
            string family = IsTrackPurpose(purpose) ? "UW" : "CW";
            ProfileMetadata fallback = _fallbackByFamily[family];
            Element type = mullion.Document.GetElement(mullion.GetTypeId());
            string typeName = type != null ? type.Name : fallback.ProfileType;
            Match widthMatch = Regex.Match(typeName, @"_(50|75|100)(?:_|$)", RegexOptions.IgnoreCase);
            double widthMm = widthMatch.Success ? double.Parse(widthMatch.Groups[1].Value) : fallback.NominalWidthMm;

            return new ProfileMetadata
            {
                ProfileFamily = ReadString(mullion, type, "SAB_ProfileFamily", family),
                ProfileType = ReadString(mullion, type, "SAB_ProfileType", typeName),
                ProfileSize = ReadString(mullion, type, "SAB_ProfileSize", widthMm.ToString("0") + " мм"),
                NominalWidthMm = ReadLengthMm(mullion, type, "SAB_NominalWidth", widthMm),
                NominalFlangeHeightMm = ReadLengthMm(mullion, type, "SAB_NominalFlangeHeight", 0.0),
                NominalThicknessMm = ReadLengthMm(mullion, type, "SAB_NominalThickness", 0.0)
            };
        }

        private static bool IsTrackPurpose(FrameMemberPurpose purpose)
        {
            return purpose == FrameMemberPurpose.BottomTrack ||
                   purpose == FrameMemberPurpose.TopTrack;
        }

        private static string ReadString(Element instance, Element type, string name, string fallback)
        {
            Parameter parameter = instance.LookupParameter(name);
            if (parameter == null && type != null)
            {
                parameter = type.LookupParameter(name);
            }

            string value = parameter != null && parameter.StorageType == StorageType.String
                ? parameter.AsString()
                : null;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static double ReadLengthMm(Element instance, Element type, string name, double fallback)
        {
            Parameter parameter = instance.LookupParameter(name);
            if (parameter == null && type != null)
            {
                parameter = type.LookupParameter(name);
            }

            return parameter != null && parameter.StorageType == StorageType.Double && parameter.AsDouble() > 0.0
                ? RevitUnitService.InternalToMillimeters(parameter.AsDouble())
                : fallback;
        }
    }
}
