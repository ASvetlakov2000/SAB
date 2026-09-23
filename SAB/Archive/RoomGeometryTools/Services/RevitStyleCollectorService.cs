using Autodesk.Revit.DB;
using SAB.RoomGeometryTools.Models;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис сбора стилей Revit для UI.
    /// </summary>
    public class RevitStyleCollectorService
    {
        public IList<RevitStyleItem> GetDetailLineStyles(Document document)
        {
            List<RevitStyleItem> result = new List<RevitStyleItem>();

            if (document == null)
            {
                return result;
            }

            Category linesCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null || linesCategory.SubCategories == null)
            {
                return result;
            }

            foreach (Category subCategory in linesCategory.SubCategories)
            {
                if (subCategory == null)
                {
                    continue;
                }

                GraphicsStyle graphicsStyle = subCategory.GetGraphicsStyle(GraphicsStyleType.Projection);
                if (graphicsStyle == null)
                {
                    continue;
                }

                result.Add(new RevitStyleItem
                {
                    ElementId = graphicsStyle.Id,
                    Name = graphicsStyle.Name
                });
            }

            result.Sort(delegate (RevitStyleItem left, RevitStyleItem right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        public IList<RevitStyleItem> GetAngularDimensionStyles(Document document)
        {
            List<RevitStyleItem> result = new List<RevitStyleItem>();

            if (document == null)
            {
                return result;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(DimensionType));

            foreach (Element element in collector)
            {
                DimensionType dimensionType = element as DimensionType;
                if (dimensionType == null)
                {
                    continue;
                }

                bool isAngular = false;

                try
                {
                    isAngular = dimensionType.StyleType == DimensionStyleType.Angular;
                }
                catch
                {
                    isAngular = false;
                }

                if (!isAngular)
                {
                    continue;
                }

                result.Add(new RevitStyleItem
                {
                    ElementId = dimensionType.Id,
                    Name = dimensionType.Name
                });
            }

            result.Sort(delegate (RevitStyleItem left, RevitStyleItem right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        public RevitStyleItem ResolveDefaultAxisStyle(IList<RevitStyleItem> styles)
        {
            if (styles == null || styles.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < styles.Count; i++)
            {
                if (string.Equals(styles[i].Name, "SA_Ось помещения", StringComparison.OrdinalIgnoreCase))
                {
                    return styles[i];
                }
            }

            return styles[0];
        }

        public RevitStyleItem ResolveDefaultAngularStyle(IList<RevitStyleItem> styles)
        {
            if (styles == null || styles.Count == 0)
            {
                return null;
            }

            return styles[0];
        }
    }
}

