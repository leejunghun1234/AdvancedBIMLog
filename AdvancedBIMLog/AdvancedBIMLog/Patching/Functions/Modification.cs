using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Patching.Functions
{
    internal class Modification
    {
        public static void modifyElement(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string cat = (string)log["Info"]["Common"]["ElementCategory"];

            switch (cat)
            {
                case "Walls":
                    modifyWall(doc, log, elementDict); break;
                case "Floors":
                    modifyFloor(doc, log, elementDict); break;
                case "Ceilings":
                    modifyCeiling(doc, log, elementDict); break;
                case "Windows":
                    modifyWindowOrDoor(doc, log, elementDict); break;
                case "Doors":
                    modifyWindowOrDoor(doc, log, elementDict); break;
                case "Columns":
                    modifyColumn(doc, log, elementDict); break;
                case "Structural Columns":
                    modifyStructuralColumn(doc, log, elementDict); break;
                case "Structural Framing":
                    modifyStructuralFraming(doc, log, elementDict); break;
                case "Furniture":
                    modifyFurniture(doc, log, elementDict); break;

                    //case "Grid":
                    //    modifyGrid(doc, log, elementDict); break;
                    //case "Levels":
                    //    modifyLevel(doc, log, elementDict); break;
                    //case "Stairs":
                    //    Debug.WriteLine("계단");
                    //    break;
                    //// createStair(doc, log, elementDict); break;
                    //case "Railings":
                    //    Debug.WriteLine("레일");
                    //    break;
                    //    // createRailing(doc, log, elementDict); break;
            }
        }

        private static void modifyWall(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            Wall wall = Func.getElem(doc, log, elementDict) as Wall;
            if (wall != null)
            {
                if (common != null && common.Count != 0)
                {
                    WallType wallType = Func.getElementType<WallType>(doc, common["ElementType"].ToString());
                    wall.WallType = wallType;
                }

                if (geometry != null && geometry.Count != 0)
                {
                    Curve wallCurve = Func.GetCurveDescription(geometry);
                    LocationCurve locationCurve = wall.Location as LocationCurve;
                    locationCurve.Curve = wallCurve;
                }

                if (parameter != null && parameter.Count != 0)
                {
                    Func.SetElementParameters(doc, wall, (JObject)log["Info"], elementDict);
                }

                if (property != null && property.Count != 0)
                {
                    if (property.ContainsKey("Flipped")) wall.Flip();
                }

                if (layers != null && layers.Count != 0) { }
            }
            else
            {
                Debug.WriteLine("심각한 오류 발생~~");
            }
        }

        private static void modifyFloor(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            Floor floor = Func.getElem(doc, log, elementDict) as Floor;

            if (geometry != null && geometry.Count != 0)
            {
                // 우선 기존 꺼를 지우고~
                doc.Delete(floor.Id);

                IList<CurveLoop> profile = Func.GetCurveListDescription((JObject)geometry["Profile"]);

                // FloorType
                string elementtype = (string)common["ElementType"];
                FloorType floorType = Func.getElementType<FloorType>(doc, elementtype);

                // Level
                Level floorLevel = Func.getLevel(doc, parameter["LEVEL_PARAM"]["ValueString"].ToString());

                bool structural = false;
                if ((string)parameter["FLOOR_PARAM_IS_STRUCTURAL"]["ValueString"] == "Yes")
                {
                    structural = true;
                }

                Line slopeArrow = null;
                double slope = 0;
                if (geometry["SlopeArrow"].ToString() != "None")
                {
                    slopeArrow = Func.GetCurveDescription((JObject)geometry["SlopeArrow"]) as Line;
                    slope = (double)geometry["SlopeAngle"];
                }

                Floor newFloor = Floor.Create(doc, profile, floorType.Id, floorLevel.Id, structural, slopeArrow, slope);

                Func.SetElementParameters(doc, newFloor, (JObject)log["Info"], elementDict);

                string elementid = (string)log["ElementId"];
                elementDict[elementid] = newFloor.Id.ToString();

                return;
            }

            if (common != null && common.Count != 0)
            {
                FloorType floorType = Func.getElementType<FloorType>(doc, (string)common["ElementType"]);
                floor.FloorType = floorType;
            }

            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, floor, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {
                // slope 정보가 여기 들어가면 되겠네유
            }
        }

        private static void modifyCeiling(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            Ceiling ceiling = Func.getElem(doc, log, elementDict) as Ceiling;

            if (geometry != null && geometry.Count != 0)
            {
                // 우선 기존 꺼를 지우고~
                doc.Delete(ceiling.Id);

                IList<CurveLoop> profile = Func.GetCurveListDescription((JObject)geometry["CurveLoops"]);

                // FloorType
                string elementtype = (string)common["ElementType"];
                CeilingType ceilingType = Func.getElementType<CeilingType>(doc, elementtype);

                // Level
                Level ceilingLevel = Func.getLevel(doc, parameter["LEVEL_PARAM"]["ValueString"].ToString());

                Line slopeArrow = null;
                double slope = 0;
                if (geometry["SlopeArrow"].ToString() != "None")
                {
                    slopeArrow = Func.GetCurveDescription((JObject)geometry["SlopeArrow"]) as Line;
                    slope = (double)geometry["SlopeAngle"];
                }

                Ceiling newCeiling = Ceiling.Create(doc, profile, ceilingType.Id, ceilingLevel.Id, slopeArrow, slope);

                Func.SetElementParameters(doc, newCeiling, (JObject)log["Info"], elementDict);

                string elementid = (string)log["ElementId"];
                elementDict[elementid] = newCeiling.Id.ToString();

                return;
            }
            if (common != null && common.Count != 0)
            {
                CeilingType ceilingType = Func.getElementType<CeilingType>(doc, (string)common["ElementType"]);
                ceiling.ChangeTypeId(ceilingType.Id);
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, ceiling, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {

            }
        }

        private static void modifyWindowOrDoor(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            FamilyInstance wd = Func.getElem(doc, log, elementDict) as FamilyInstance;

            if (common != null && common.Count != 0)
            {
                string elementfamily = common["ElementFamily"].ToString();
                string elementtype = common["ElementType"].ToString();
                FamilySymbol wdSymbol = Func.getSymbol(doc, elementfamily, elementtype);

                if (!wdSymbol.IsActive)
                {
                    wdSymbol.Activate();
                    doc.Regenerate();
                }

                wd.Symbol = wdSymbol;
            }
            if (geometry != null && geometry.Count != 0)
            {
                LocationPoint locationPoint = wd.Location as LocationPoint;
                XYZ wdLocation = new XYZ(
                    (double)geometry["Location"]["X"],
                    (double)geometry["Location"]["Y"],
                    (double)geometry["Location"]["Z"]);
                locationPoint.Point = wdLocation;
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, wd, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {
                if (property.ContainsKey("FlipFacing")) wd.flipFacing();
                if (property.ContainsKey("FlipHand")) wd.flipHand();
            }
        }

        private static void modifyColumn(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            FamilyInstance column = Func.getElem(doc, log, elementDict) as FamilyInstance;

            if (common != null && common.Count != 0)
            {
                string elementfamily = common["ElementFamily"].ToString();
                string elementtype = common["ElementType"].ToString();
                FamilySymbol columnSymbol = Func.getSymbol(doc, elementfamily, elementtype);

                if (!columnSymbol.IsActive)
                {
                    columnSymbol.Activate();
                    doc.Regenerate();
                }

                column.Symbol = columnSymbol;
            }
            if (geometry != null && geometry.Count != 0)
            {
                LocationPoint locationPoint = column.Location as LocationPoint;

                XYZ columnLocation = new XYZ(
                    (double)geometry["Location"]["X"],
                    (double)geometry["Location"]["Y"],
                    (double)geometry["Location"]["Z"]);

                locationPoint.Point = columnLocation;
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, column, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {

            } // 없어
            if (layers != null && layers.Count != 0)
            {

            }     // 없어
        }

        private static void modifyStructuralColumn(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            FamilyInstance structuralColumn = Func.getElem(doc, log, elementDict) as FamilyInstance;

            if (common != null && common.Count != 0)
            {
                string elementfamily = common["ElementFamily"].ToString();
                string elementtype = common["ElementType"].ToString();
                FamilySymbol scSymbol = Func.getSymbol(doc, elementfamily, elementtype);

                if (!scSymbol.IsActive)
                {
                    scSymbol.Activate();
                    doc.Regenerate();
                }

                structuralColumn.Symbol = scSymbol;
            }
            if (geometry != null && geometry.Count != 0)
            {
                if (((JObject)geometry["Location"]).ContainsKey("Type"))
                {
                    LocationCurve locationCurve = structuralColumn.Location as LocationCurve;
                    Curve columnCurve = Func.GetCurveDescription((JObject)geometry);
                    locationCurve.Curve = columnCurve;
                }
                else
                {
                    LocationPoint locationPoint = structuralColumn.Location as LocationPoint;
                    XYZ columnLocation = new XYZ(
                        (double)geometry["Location"]["X"],
                        (double)geometry["Location"]["Y"],
                        (double)geometry["Location"]["Z"]);
                    locationPoint.Point = columnLocation;
                }
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, structuralColumn, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {

            } // 없엉
            if (layers != null && layers.Count != 0)
            {

            }     // 없엉
        }

        private static void modifyStructuralFraming(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            FamilyInstance sf = Func.getElem(doc, log, elementDict) as FamilyInstance;

            if (common != null && common.Count != 0)
            {
                string elementfamily = common["ElementFamily"].ToString();
                string elementtype = common["ElementType"].ToString();
                FamilySymbol sfSymbol = Func.getSymbol(doc, elementfamily, elementtype);
                if (!sfSymbol.IsActive)
                {
                    sfSymbol.Activate();
                    doc.Regenerate();
                }
                sf.Symbol = sfSymbol;
            }
            if (geometry != null && geometry.Count != 0)
            {
                LocationCurve locationCurve = sf.Location as LocationCurve;
                Curve beamCurve = Func.GetCurveDescription(geometry);
                locationCurve.Curve = beamCurve;
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, sf, (JObject)log["Info"], elementDict);
            }
            if (property != null && property.Count != 0)
            {

            } // 없엉
            if (layers != null && layers.Count != 0)
            {

            }     // 없엉
        }

        private static void modifyFurniture(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            FamilyInstance furni = Func.getElem(doc, log, elementDict) as FamilyInstance;

            if (common != null && common.Count != 0)
            {
                string elementfamily = common["ElementFamily"].ToString();
                string elementtype = common["ElementType"].ToString();
                FamilySymbol fSymbol = Func.getSymbol(doc, elementfamily, elementtype);

                if (!fSymbol.IsActive)
                {
                    fSymbol.Activate();
                    doc.Regenerate();
                }

                furni.Symbol = fSymbol;
            }
            if (geometry != null && geometry.Count != 0)
            {
                LocationPoint locationPoint = furni.Location as LocationPoint;
                XYZ fLocation = new XYZ(
                    (double)geometry["Location"]["X"],
                    (double)geometry["Location"]["Y"],
                    (double)geometry["Location"]["Z"]);
                locationPoint.Point = fLocation;

                Level fLevel = Func.getLevel(doc, (string)geometry["Level"]);
                furni.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM).Set(fLevel.Id);
            }
        }

        private static void modifyGrid(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            Grid grid = Func.getElem(doc, log, elementDict) as Grid;

            if (common != null && common.Count != 0)
            {

            }
            if (geometry != null && geometry.Count != 0)
            {
                LocationCurve locationCurve = grid.Location as LocationCurve;
                Curve gridCurve = Func.GetCurveDescription(geometry);

                locationCurve.Curve = gridCurve;
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, grid, log, elementDict);
            }
            if (property != null && property.Count != 0)
            {

            }
            if (layers != null && layers.Count != 0)
            {

            }
        }

        private static void modifyLevel(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string CorM = "M";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);

            Level level = Func.getElem(doc, log, elementDict) as Level;

            if (common != null && common.Count != 0)
            {

            }  // 없어, head 를 넣을 수는 있긴 한데 일단 ㅇㅋ
            if (geometry != null && geometry.Count != 0)
            {
                double elevation = (double)geometry["Elevation"];
                level.Elevation = elevation;
            }
            if (parameter != null && parameter.Count != 0)
            {
                Func.SetElementParameters(doc, level, log, elementDict);
            }
            if (property != null && property.Count != 0)
            {

            }
            if (layers != null && layers.Count != 0)
            {

            }
        }
    }
}
