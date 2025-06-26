using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Get
{
    internal class GetCenterPoint
    {
        public static string GetWallCenterPoint(Wall wall)
        {
            LocationCurve wallcurve = wall.Location as LocationCurve;
            Curve curve = wallcurve.Curve;
            double endX1 = curve.GetEndPoint(0).X;
            double endY1 = curve.GetEndPoint(0).Y;
            double endZ1 = curve.GetEndPoint(0).Z;
            double endX2 = curve.GetEndPoint(0).X;
            double endY2 = curve.GetEndPoint(0).Y;
            double endZ2 = curve.GetEndPoint(0).Z;

            double centerX = (endX1 + endX2) / 2;
            double centerY = (endY1 + endY2) / 2;
            double centerZ = (endZ1 + endZ2) / 2;
            string centerPoint = $"({centerX}, {centerY}, {centerZ})";

            return centerPoint;
        }
    }
}
