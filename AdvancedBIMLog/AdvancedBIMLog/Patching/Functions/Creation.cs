using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Patching.Functions
{
    internal static class Creation
    {
        public static void createElement(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            string cat = (string)log["Info"]["Common"]["ElementCategory"];
            switch (cat)
            {
                case "Walls":
                    createWall(doc, log, elementDict); break;
                case "Floors":
                    createFloor(doc, log, elementDict); break;
                case "Ceilings":
                    createCeiling(doc, log, elementDict); break;
                case "Windows":
                    createWindowOrDoor(doc, log, elementDict); break;
                case "Doors":
                    createWindowOrDoor(doc, log, elementDict); break;
                case "Columns":
                    createColumn(doc, log, elementDict); break;
                case "Structural Columns":
                    createStructuralColumn(doc, log, elementDict); break;
                case "Furniture":
                    createFurniture(doc, log, elementDict); break;
            }
        }

        private static void createWall(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";

            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            WallType wallType = Func.getElementType<WallType>(doc, elementtype);

            Level wallLevel = Func.getLevel(doc, parameter["WALL_BASE_CONSTRAINT"]["ValueString"].ToString());

            Curve wallCurve = Func.GetCurveDescription(geometry);

            bool structural = false;
            if ((string)parameter["WALL_STRUCTURAL_SIGNIFICANT"]["ValueString"] == "Yes")
            {
                structural = true;
            }

            Wall wall = Wall.Create(doc, wallCurve, wallLevel.Id, structural);

            if ((string)property["Flipped"] == "Yes")
            {
                wall.Flip();
            }

            Func.SetElementParameters(doc, wall, (JObject)log["Info"], elementDict);

            elementDict[elementid] = wall.Id.ToString();
        }

        private static void createFloor(
            Autodesk.Revit.DB.Document doc,
            JObject log, JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            // profile
            IList<CurveLoop> profile = Func.GetCurveListDescription((JObject)geometry["Profile"]);

            // FloorType
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
            if (geometry["SlopeArrow"].ToString() != null)
            {
                slopeArrow = Func.GetCurveDescription((JObject)geometry["SlopeArrow"]) as Line;
                slope = (double)geometry["SlopeAngle"];
            }

            Floor floor = Floor.Create(doc, profile, floorType.Id, floorLevel.Id, structural, slopeArrow, slope);

            Func.SetElementParameters(doc, floor, (JObject)log["Info"], elementDict);

            elementDict[elementid] = floor.Id.ToString();
        }

        private static void createCeiling(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            IList<CurveLoop> profile = Func.GetCurveListDescription((JObject)geometry["Profile"]);

            CeilingType ceilingType = Func.getElementType<CeilingType>(doc, elementtype);

            Level ceilingLevel = Func.getLevel(doc, parameter["LEVEL_PARAM"]["ValueString"].ToString());

            Ceiling ceiling = Ceiling.Create(doc, profile, ceilingType.Id, ceilingLevel.Id);

            Func.SetElementParameters(doc, ceiling, log, elementDict);

            elementDict[elementid] = ceiling.Id.ToString();
        }

        private static void createWindowOrDoor(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            FamilySymbol wdSymbol = Func.getSymbol(doc, elementfamily, elementtype);

            XYZ wdLocation = new XYZ(
                (double)geometry["Location"]["X"],
                (double)geometry["Location"]["Y"],
                (double)geometry["Location"]["Z"]
                );

            long hostIdValue = elementDict[geometry["HostId"].ToString()].ToObject<long>();
            ElementId wdHostId = new ElementId(hostIdValue);
            Element wdHost = doc.GetElement(wdHostId);

            if (wdSymbol.IsActive)
            {
                wdSymbol.Activate();
                doc.Regenerate();
            }

            FamilyInstance wd = doc.Create.NewFamilyInstance(
                wdLocation,
                wdSymbol,
                wdHost,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            Func.SetElementParameters(doc, wd, log, elementDict);

            string newWdId = wd.Id.ToString();
            elementDict[elementid] = newWdId;
        }

        private static void createColumn(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            FamilySymbol columnSymbol = Func.getSymbol(doc, elementfamily, elementtype);

            Level columnLevel = Func.getLevel(doc, parameter["FAMILY_BASE_LEVEL_PARAM"]["ValueString"].ToString());

            XYZ columnLocation = new XYZ(
                (double)geometry["Location"]["X"],
                (double)geometry["Location"]["Y"],
                (double)geometry["Location"]["Z"]
                );

            if (columnSymbol.IsActive)
            {
                columnSymbol.Activate();
                doc.Regenerate();
            }

            // StructuralType 확인해서 IsStructure 파악하기
            FamilyInstance column = doc.Create.NewFamilyInstance(columnLocation, columnSymbol, columnLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            Func.SetElementParameters(doc, column, log, elementDict);

            string newColumnId = column.Id.ToString();
            elementDict[elementid] = newColumnId;
        }

        private static void createStructuralColumn(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            FamilySymbol columnSymbol = Func.getSymbol(doc, elementfamily, elementtype);
            Level columnLevel = Func.getLevel(doc, parameter["FAMILY_BASE_LEVEL_PARAM"]["ValueString"].ToString());

            if (columnSymbol.IsActive)
            {
                columnSymbol.Activate();
                doc.Regenerate();
            }
            FamilyInstance column = null;
            if (((JObject)geometry["Location"]).ContainsKey("Type"))
            {
                Curve columnCurve = Func.GetCurveDescription((JObject)geometry);
                column = doc.Create.NewFamilyInstance(columnCurve, columnSymbol, columnLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
            }
            else
            {
                XYZ columnLocation = new XYZ(
                       (double)geometry["Location"]["X"],
                       (double)geometry["Location"]["Y"],
                       (double)geometry["Location"]["Z"]
                );

                column = doc.Create.NewFamilyInstance(columnLocation, columnSymbol, columnLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
            }

            Func.SetElementParameters(doc, column, log, elementDict);

            string newColumnId = column.Id.ToString();
            elementDict[elementid] = newColumnId;
        }

        private static void createFurniture(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            FamilySymbol fSymbol = Func.getSymbol(doc, elementfamily, elementtype);

            XYZ fLocation = new XYZ(
                (double)geometry["Location"]["X"],
                (double)geometry["Location"]["Y"],
                (double)geometry["Location"]["Z"]);

            Level fLevel = Func.getLevel(doc, parameter["FAMILY_LEVEL_PARAM"]["ValueString"].ToString());

            if (fSymbol.IsActive)
            {
                fSymbol.Activate();
                doc.Regenerate();
            }

            FamilyInstance furniture = doc.Create.NewFamilyInstance(fLocation, fSymbol, fLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            elementDict[elementid] = furniture.Id.ToString();
        }



        private static void createStructuralFraming(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            FamilySymbol sfSymbol = Func.getSymbol(doc, elementfamily, elementtype);
            Curve sfCurve = Func.GetCurveDescription(geometry);
            Level sfLevel = Func.getLevel(doc, parameter["Built-In"]["Level"]["ValueString"].ToString());

            if (!sfSymbol.IsActive)
            {
                sfSymbol.Activate();
                doc.Regenerate();
            }

            FamilyInstance sf = doc.Create.NewFamilyInstance(sfCurve, sfSymbol, sfLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);

            Func.SetElementParameters(doc, sf, log, elementDict);

            string newsfId = sf.Id.ToString();
            elementDict[elementid]["NewId"] = newsfId;
        }

        private static void createGrid(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            Line gridCurve = Func.GetCurveDescription(geometry) as Line;
            Grid grid = Grid.Create(doc, gridCurve);

            string newGridId = grid.Id.ToString();
            elementDict[elementid]["NewId"] = newGridId;

            Func.SetElementParameters(doc, grid, log, elementDict);
        }

        private static void createLevel(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            double elevation = (double)geometry["Elevation"];
            string levelName = (string)parameter["Built-In"]["Name"]["Value"];

            Level level = Level.Create(doc, elevation);
            level.Name = levelName;

            Func.SetElementParameters(doc, level, log, elementDict);

            string newWallId = level.Id.ToString();
            elementDict[elementid]["NewId"] = newWallId;
        }

        private static void createStair(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

            StairsType stairsType = Func.getElementType<StairsType>(doc, elementtype);

            string baseLevelName = parameter["Built-In"]["Base Level"]["ValueString"].ToString();
            string topLevelName = parameter["Built-In"]["Top Level"]["ValueString"].ToString();

            Level baseLevel = Func.getLevel(doc, baseLevelName);
            Level topLevel = Func.getLevel(doc, topLevelName);

            using (StairsEditScope newStairsScope = new StairsEditScope(doc, "Start"))
            {
                ElementId newStairsId = newStairsScope.Start(baseLevel.Id, topLevel.Id);
                Stairs stairs = doc.GetElement(newStairsId) as Stairs;

                JArray stairrun = (JArray)geometry["Run"];
                JArray stairlanding = (JArray)geometry["Landing"];

                foreach (JObject s in stairrun)
                {
                    string runCurve = (string)s["Type"];
                    double baseElevation = (double)s["BaseElevation"];
                    double TopElevation = (double)s["TopElevation"];
                    int riserNum = (int)s["RiserNum"];

                    if (runCurve == "Line")
                    {
                        XYZ startPoint = new XYZ(
                            (double)s["endPoints"][0]["X"],
                            (double)s["endPoints"][0]["Y"],
                            (double)s["endPoints"][0]["Z"]
                            );
                        XYZ endPoint = new XYZ(
                            (double)s["endPoints"][1]["X"],
                            (double)s["endPoints"][1]["Y"],
                            (double)s["endPoints"][1]["Z"]
                            );

                        IList<Curve> endCurves = new List<Curve>();
                        foreach (JObject i in (JArray)s["Boundary"]["curveLoop"])
                        {

                        }
                    }
                }
            }
        }

        private static void createRailing(
            Autodesk.Revit.DB.Document doc,
            JObject log,
            JObject elementDict)
        {
            string CorM = "C";
            Func.ExtractLogData(log, CorM, out JObject common, out JObject geometry, out JObject parameter, out JObject property, out JArray layers);
            Func.ExtractCommonData(common, out string timestamp, out string elementid, out string elementcategory, out string elementfamily, out string elementtype);

        }
    }
}
