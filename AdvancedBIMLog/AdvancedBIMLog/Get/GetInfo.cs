using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LogShape;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog
{
    internal class GetInfo
    {
        public static List<string> paramChecker = new List<string>
        {
            "Image", "IFC", "Phase", "Cross", "Related", "Location Line", "Mark", "Design", "Options",
            "Association", "Ifc", "Mass", "Room Bounding", "Moves", "Grid"
        };

        public static bool IsValid(string input)
        {
            return paramChecker.Any(keyword => input.ToLower().Contains(keyword.ToLower()));
        }

        public static void GetParameter(Element elem, JObject parameter)
        {
            ParameterSet ps = elem.Parameters;
            foreach (Parameter p in ps)
            {
                string pName = p.Definition.Name;
                if (IsValid(pName)) continue;

                InternalDefinition pDef = p.Definition as InternalDefinition;
                var pDefName = pDef.BuiltInParameter;
                bool checkNull = p.HasValue;
                string pAsValueString = p.AsValueString();
                var storageType = p.StorageType;
                string pStorageType = "";
                if (checkNull)
                {
                    dynamic pValue = null;

                    if (storageType == StorageType.String)
                    {
                        pStorageType = "String";
                        pValue = p.AsString();
                        
                    }
                    else if (storageType == StorageType.Double)
                    {
                       pStorageType = "Double";
                       pValue = p.AsDouble();
                    }
                    else if (storageType == StorageType.Integer)
                    {
                        pStorageType = "Integer";
                        pValue = p.AsInteger();
                    }
                    else if (storageType == StorageType.ElementId)
                    {
                        pStorageType = "Integer";
                        pValue = p.AsElementId().Value;
                    }
                    else
                    {
                        pStorageType = "None";
                        pValue = p.AsString();
                    }

                    JObject pp = new()
                    {
                        ["StorageType"] = pStorageType,
                        ["Value"] = pValue,
                        ["ValueString"] = pAsValueString,
                    };
                    try
                    {
                        parameter.Add($"{pDefName}", pp);

                    }
                    catch
                    {

                    }
                }
                
            }
        }

        public static void GetLayer(Document doc, Element elem, JObject job)
        {
            var elemType = doc.GetElement(elem.GetTypeId()) as HostObjAttributes;
            var layerList = elemType?.GetCompoundStructure()?.GetLayers();
            if (layerList != null)
            {
                double elemVolume = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                double elemWidth = elemType.GetCompoundStructure().GetWidth();
                
                JArray layerInfo = [];
                foreach (CompoundStructureLayer layer in layerList)
                {
                    string func = layer.Function.ToString();
                    double width = layer.Width;
                    double materialVolume = (width / elemWidth) * elemVolume;
                    long materialId = layer.MaterialId.Value;
                    string materialName = "Default";
                    if (materialId != -1)
                    {
                        materialName = doc.GetElement(layer.MaterialId).Name; 
                    }
                    JObject layerinfo = new()
                    {
                        ["Material Name"] = materialName,
                        ["Function"] = func,
                        ["Material Volume"] = materialVolume,
                    };
                    layerInfo.Add(layerinfo);
                }
                job["Layers"] = layerInfo;
            }
            else
            {
                // 창문, 문일 때 
                if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Stairs)
                {
                    double stairRunLength = 0.0;
                    // width 는 필요할까 싶어 일단 넣어놔~
                    double stairRunWidht = 0.0;
                    double stairLandingLength = 0.0;
                    double stairLandingThickness = 0.0;

                    Stairs stair = elem as Stairs;
                    var stRunIds = stair.GetStairsRuns();
                    var stLandingIds = stair.GetStairsLandings();
                    foreach (ElementId stRunId in stRunIds)
                    {
                        StairsRun stRun = doc.GetElement(stRunId) as StairsRun;
                        stairRunWidht = stRun.ActualRunWidth;
                        CurveLoop stRunPath = stRun.GetStairsPath();
                        foreach (Curve stRunPathCurve in stRunPath)
                        {
                            stairRunLength += stRunPathCurve.Length;
                        }
                    }
                    foreach (ElementId stLandingId in stLandingIds)
                    {
                        StairsLanding stLanding = doc.GetElement(stLandingId) as StairsLanding;
                        stairLandingThickness = stLanding.Thickness;
                        CurveLoop stLandingPath = stLanding.GetStairsPath();
                        foreach (Curve stLandingPathCurve in stLandingPath)
                        {
                            stairLandingLength += stLandingPathCurve.Length;
                        }
                    }
                    JObject layerInfo = new JObject
                    {
                        ["Stair Length"] = stairRunLength + stairLandingLength,
                        ["Stair Run Length"] = stairRunLength,
                        ["Stair Landing Length"] = stairLandingLength,
                        ["Stair Run Width"] = stairRunWidht,
                        ["Stair Landing Thckness"] = stairLandingThickness
                    };
                    job["Layers"] = layerInfo;
                }
                else if (elem.Category.BuiltInCategory == BuiltInCategory.OST_StairsRailing)
                {
                    double railingLength = 0.0;
                    Railing railing = elem as Railing;
                    var railingPath = railing.GetPath();
                    foreach (Curve railingPathCurve in railingPath)
                    {
                        railingLength += railingPathCurve.Length;
                    }

                    JObject layerInfo = new JObject
                    {
                        ["Railing Length"] = railingLength
                    };
                    job["Layers"] = layerInfo;
                }
                else if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Columns)
                {
                    FamilyInstance columns = elem as FamilyInstance;
                    ElementId columnBaseLevelId = columns.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId();
                    ElementId columnTopLevelId = columns.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();
                    double columnBaseOffset = columns.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble();
                    double columnTopOffset = columns.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).AsDouble();
                    double columnVolume = columns.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();

                    Level columnBaseLevel = doc.GetElement(columnBaseLevelId) as Level;
                    Level columnTopLevel = doc.GetElement(columnTopLevelId) as Level;

                    double baseLevelElevation = columnBaseLevel.Elevation;
                    double topLevelElevation = columnTopLevel.Elevation;

                    double columnHeight = (topLevelElevation - columnTopOffset) - (baseLevelElevation + columnBaseOffset);

                    JObject layerInfo = new JObject
                    {
                        ["Column Length"] = columnHeight,
                        ["Column Volume"] = columnVolume,
                    };
                    job["Layers"] = layerInfo;
                }
                else if (elem.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns)
                {
                    FamilyInstance columns = elem as FamilyInstance;
                    double columnLength = columns.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();
                    double columnVolume = columns.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                    string columnMaterialName = ((Material)doc.GetElement(columns.StructuralMaterialId)).Name;
                    JObject layerInfo = new JObject
                    {
                        ["Column Material"] = columnMaterialName,
                        ["Column Length"] = columnLength,
                        ["Column Volume"] = columnVolume,
                    };
                    job["Layers"] = layerInfo;
                }
                else if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Walls)
                {
                    Wall wall = elem as Wall;
                    var panels = wall.CurtainGrid.GetPanelIds();

                    JObject layerInfo = new JObject
                    {
                        ["Number of Panels"] = panels.Count
                    };
                    job["Common"]["ElementCategory"] = "Curtain Walls";
                    job["Layers"] = new JObject();
                }
                // 커튼월일 때 -> 유리 패널 면적으로
                //else
                //{
                //    job.Property("Layers").Remove();
                //}
            }

        }

        public static JObject GetProfileDescription(Sketch sketch)
        {
            CurveArrArray crvArrArr = sketch.Profile;

            
            JObject profileJObject = new JObject();
            int i = 1;
            

            foreach (CurveArray CrvArr in crvArrArr)
            {
                JArray profileJArray = new JArray();
                profileJObject[$"profile_{i++}"] = profileJArray;
                foreach (Curve Crv in CrvArr)
                {
                    JObject description = GetCurveDescription(Crv);
                    profileJArray.Add(description);
                }
            }
            return profileJObject;
        }

        public static JObject GetCurveLoopDescription(CurveLoop curveLoop)
        {
            List<Curve> crvList = curveLoop.ToList();

            JArray jarray = new JArray();
            JObject jobject = new JObject();
            jobject["curveLoop"] = jarray;

            foreach (Curve crv in crvList)
            {
                JObject description = GetCurveDescription(crv);
                jarray.Add(description);
            }
            return jobject;
        }

        public static JObject GetCurveListDescription(List<Curve> crvList)
        {
            JArray jarray = new JArray();
            JObject jobject = new JObject();
            jobject["curveList"] = jarray;

            foreach (Curve crv in crvList)
            {
                JObject description = GetCurveDescription(crv);
                jarray.Add(description);
            }
            return jobject;
        }

        public static dynamic GetCurveDescription(Curve crv)
        {
            JObject JCurve = new JObject();
            string typ = crv.GetType().Name;

            switch (typ)
            {
                case "Line":
                    // = new LineDescription() { Type = typ, StartPoint = "\"" + crv.GetEndPoint(0).ToString().Replace(" ", String.Empty) + "\"", EndPoint = "\"" + crv.GetEndPoint(1).ToString().Replace(" ", String.Empty) + "\"" };
                    JCurve.Add("Type", typ);
                    JCurve.Add("endPoints", new JArray(
                        new JObject(
                            new JProperty("X", crv.GetEndPoint(0).X),
                            new JProperty("Y", crv.GetEndPoint(0).Y),
                            new JProperty("Z", crv.GetEndPoint(0).Z)
                            ),
                        new JObject(
                            new JProperty("X", crv.GetEndPoint(1).X),
                            new JProperty("Y", crv.GetEndPoint(1).Y),
                            new JProperty("Z", crv.GetEndPoint(1).Z)
                            )
                        ));
                    break;

                case "Arc":

                    var arc = crv as Arc;
                    var arcCen = arc.Center;
                    var arcNorm = arc.Normal;
                    var rad = arc.Radius;
                    var arcXAxis = arc.XDirection;
                    var arcYAxis = arc.YDirection;
                    var plane = Plane.CreateByNormalAndOrigin(arcNorm, arcCen);
                    var startDir = (arc.GetEndPoint(0) - arcCen).Normalize();
                    var endDir = (arc.GetEndPoint(1) - arcCen).Normalize();
                    var startAngle = arcXAxis.AngleOnPlaneTo(startDir, arcNorm);
                    var endAngle = arcXAxis.AngleOnPlaneTo(endDir, arcNorm);

                    JCurve.Add("Type", typ);
                    JCurve.Add("center", new JObject(
                        new JProperty("X", arcCen.X),
                        new JProperty("Y", arcCen.Y),
                        new JProperty("Z", arcCen.Z)
                        ));
                    JCurve.Add("radius", rad);
                    JCurve.Add("startAngle", startAngle);
                    JCurve.Add("endAngle", endAngle);
                    JCurve.Add("xAxis", new JObject(
                        new JProperty("X", arcXAxis.X),
                        new JProperty("Y", arcXAxis.Y),
                        new JProperty("Z", arcXAxis.Z)
                        ));
                    JCurve.Add("yAxis", new JObject(
                        new JProperty("X", arcYAxis.X),
                        new JProperty("Y", arcYAxis.Y),
                        new JProperty("Z", arcYAxis.Z)
                        ));

                    //description = new ArcDescription() { Type = typ, Center = "\"" + arcCen.ToString().Replace(" ", String.Empty) + "\"", Radius = rad, StartAngle = startAngle, EndAngle = endAngle, xAxis = "\"" + arcXAxis.ToString().Replace(" ", String.Empty) + "\"", yAxis = "\"" + arcYAxis.ToString().Replace(" ", String.Empty) + "\"" };

                    break;
                case "Ellipse":
                    // 24.4.19. 작업 중
                    var ellip = crv as Ellipse;
                    var cen = ellip.Center;
                    var xRad = ellip.RadiusX;
                    var yRad = ellip.RadiusY;
                    var xAxis = ellip.XDirection;
                    var yAxis = ellip.YDirection;
                    var startParam = ellip.GetEndParameter(0);
                    var endParam = ellip.GetEndParameter(1);
                    JCurve.Add("type", typ);
                    JCurve.Add("center", new JObject(
                        new JProperty("X", cen.X),
                        new JProperty("Y", cen.Y),
                        new JProperty("Z", cen.Z)
                        ));
                    JCurve.Add("radiusX", xRad);
                    JCurve.Add("radiusY", yRad);
                    JCurve.Add("xDirection", new JObject(
                        new JProperty("X", xAxis.X),
                        new JProperty("Y", xAxis.Y),
                        new JProperty("Z", xAxis.Z)
                        ));
                    JCurve.Add("yDirection", new JObject(
                        new JProperty("X", yAxis.X),
                        new JProperty("Y", yAxis.Y),
                        new JProperty("Z", yAxis.Z)
                        ));
                    JCurve.Add("startParameter", startParam);
                    JCurve.Add("endParameter", endParam);

                    break;

                case "HermiteSpline":

                    var herSpl = crv as HermiteSpline;
                    var contPts = herSpl.ControlPoints;
                    Int32 tangentCount = (herSpl.Tangents.Count - 1);
                    var startTangents = herSpl.Tangents[0].Normalize();
                    var endTangents = herSpl.Tangents[tangentCount].Normalize();
                    JArray hJarray = new JArray();
                    for (int i = 0; i < contPts.Count; i++)
                    {
                        XYZ pt = contPts[i];
                        hJarray.Add(new JObject(
                            new JProperty("X", pt.X),
                            new JProperty("y", pt.Y),
                            new JProperty("Z", pt.Z)
                            ));
                    }
                    var periodic = herSpl.IsPeriodic;
                    JCurve.Add("controlPoints", hJarray);
                    JCurve.Add("isPeriodic", periodic);
                    JCurve.Add("startTagents", new JObject(
                        new JProperty("X", startTangents.X),
                        new JProperty("Y", startTangents.Y),
                        new JProperty("Z", startTangents.Z)
                        ));
                    JCurve.Add("endTagents", new JObject(
                        new JProperty("X", endTangents.X),
                        new JProperty("Y", endTangents.Y),
                        new JProperty("Z", endTangents.Z)
                        ));

                    break;


                case "CylindricalHelix":

                    var cylinHelix = crv as CylindricalHelix;
                    var basePoint = cylinHelix.BasePoint;
                    var radius = cylinHelix.Radius;
                    var xVector = cylinHelix.XVector;
                    var zVector = cylinHelix.ZVector;
                    var pitch = cylinHelix.Pitch;

                    var cylPlane = Plane.CreateByNormalAndOrigin(zVector, basePoint);
                    var cylStartDir = (cylinHelix.GetEndPoint(0) - basePoint).Normalize();
                    var cylEndDir = (cylinHelix.GetEndPoint(1) - basePoint).Normalize();

                    var cylStartAngle = cylStartDir.AngleOnPlaneTo(xVector, zVector);
                    var cylEndAngle = cylEndDir.AngleOnPlaneTo(xVector, zVector);

                    //description = new CylindricalHelixDescription() { Type = typ, BasePoint = "\"" + basePoint.ToString().Replace(" ", String.Empty) + "\"", Radius = radius, xVector = "\"" + xVector.ToString().Replace(" ", String.Empty) + "\"", zVector = "\"" + zVector.ToString().Replace(" ", String.Empty) + "\"", Pitch = pitch, StartAngle = cylStartAngle, EndAngle = cylEndAngle };

                    break;

                case "NurbSpline":

                    var nurbsSpl = crv as NurbSpline;
                    var degree = nurbsSpl.Degree;

                    string knots = "\"";
                    for (int i = 0; i < nurbsSpl.Knots.OfType<double>().ToList().Count; i++)
                    {
                        double knot = nurbsSpl.Knots.OfType<double>().ToList()[i];
                        if (i != 0)
                        {
                            knots += ";";
                        }
                        knots += knot;
                    }
                    knots += "\"";

                    string nurbsCtrlPts = "\"";
                    for (int i = 0; i < nurbsSpl.CtrlPoints.Count; i++)
                    {
                        XYZ pt = nurbsSpl.CtrlPoints[i];
                        if (i != 0)
                        {
                            nurbsCtrlPts += ";";
                        }
                        nurbsCtrlPts += pt.ToString().Replace(" ", String.Empty);
                    }
                    nurbsCtrlPts += "\"";

                    string weights = "\"";
                    for (int i = 0; i < nurbsSpl.Weights.OfType<double>().ToList().Count; i++)
                    {
                        double weight = nurbsSpl.Weights.OfType<double>().ToList()[i];
                        if (i != 0)
                        {
                            weights += ";";
                        }
                        weights += weight;
                    }
                    weights += "\"";


                    //description = new NurbSplineDescription() { Type = typ, Degree = degree, Knots = knots, ControlPoints = nurbsCtrlPts, Weights = weights };

                    break;

                default:
                    break;

            }
            return JCurve;
            //return description;
        }
        public static JObject GetPlaneDescription(Plane plane)
        {
            JObject job = new JObject();
            job.Add("planeOrigin", new JObject(
                new JProperty("X", plane.Origin.X),
                new JProperty("Y", plane.Origin.Z),
                new JProperty("Z", plane.Origin.Y)
                ));
            job.Add("planeXVec", new JObject(
                new JProperty("X", plane.XVec.X),
                new JProperty("Y", plane.XVec.Z),
                new JProperty("Z", plane.XVec.Y)
                ));
            job.Add("planeYVec", new JObject(
                new JProperty("X", plane.YVec.X),
                new JProperty("Y", plane.YVec.Z),
                new JProperty("Z", plane.YVec.Y)
                ));

            return job;
        }
        public static JObject GetXYZDescription(XYZ xyz)
        {

            JObject jobject = new JObject(
                new JProperty("X", xyz.X),
                new JProperty("Y", xyz.Y),
                new JProperty("Z", xyz.Z)
            );
            return jobject;
        }
    }
}
