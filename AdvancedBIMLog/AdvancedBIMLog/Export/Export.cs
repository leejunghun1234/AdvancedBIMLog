using AdvancedBIMLog.PostProcessing;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Export
{
    [Transaction(TransactionMode.Manual)]
    internal class Export : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elemets)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // Document에 있는 모든 Element 가져오기
            // 각 객체의 카테고리 파악해서
            // Element ID 저장하기 
            // 형식은 Json: wall: {1, 2, 3, 4}
            JObject answer = new JObject
            {
                ["Wall"] = new JArray(),
                ["Curtain Wall"] = new JArray(),
                ["Floor"] = new JArray(),
                ["Ceiling"] = new JArray(),
                ["Column"] = new JArray(),
                ["Structural Column"] = new JArray(),
                ["Window"] = new JArray(),
                ["Door"] = new JArray(),
                ["Railing"] = new JArray(),
                ["Stair"] = new JArray(),
                ["Furniture"] = new JArray(),
                ["Roof"] = new JArray(),

                ["All"] = new JArray()
            };

            JObject answer2 = new JObject();


            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();
            
            foreach (Element elem in collector)
            {
                if (elem.Category == null) continue;
                string cat = elem.Category.Name.ToString();

                if (cat == "Walls")
                {
                    // Volume 이 있으면 Wall에
                    // 없으면 CurtainWall에
                    Wall wall = elem as Wall;
                    var wallCheck = wall.CurtainGrid;

                    JObject wallInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)wallInfo["Parameter"]);
                    wallInfo["Common"]["ElementId"] = elem.Id.ToString();
                    wallInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    wallInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    bool wallIsProfileWall = true;
                    JObject wallCurve = new JObject();

                    Element sketchElem = doc.GetElement(wall.SketchId);
                    if (sketchElem == null)
                    {
                        wallIsProfileWall = false;
                    }

                    if (wallIsProfileWall)
                    {
                        Sketch wallSketch = sketchElem as Sketch;
                    }
                    else
                    {
                        Curve wallLocCrv = (wall.Location as LocationCurve)?.Curve;
                        wallCurve = GetInfo.GetCurveDescription(wallLocCrv);
                    }

                    // Orientation 정보와 구조적 여부 (추후 활용 가능)
                    string orientation = wall.Orientation.ToString();
                    int isStructural = wall.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT).AsInteger();
                    bool isFlipped = wall.Flipped;

                    // JSON 저장
                    wallInfo["Geometry"]["IsProfileWall"] = wallIsProfileWall;
                    wallInfo["Geometry"]["Curve"] = wallCurve;
                    wallInfo["Property"]["Flipped"] = isFlipped;
                    wallInfo["Property"]["Orientation"] = orientation;
                    wallInfo["Property"]["IsStructural"] = isStructural;

                    if (wallCheck == null)
                    {
                        answer2[elem.Id.ToString()] = wallInfo;
                        ((JArray)answer["Wall"]).Add(elem.Id.ToString());
                    }
                    else
                    {
                        answer2[elem.Id.ToString()] = wallInfo;
                        ((JArray)answer["Curtain Wall"]).Add(elem.Id.ToString());
                    }
                }
                else if (cat == "Floors")
                {
                    Floor floor = elem as Floor;
                    if (doc.GetElement(floor.SketchId) is not Sketch floorSketch) continue;

                    JObject floorInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)floorInfo["Parameter"]);
                    floorInfo["Common"]["ElementId"] = elem.Id.ToString();
                    floorInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    floorInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    JObject floorSlopeArrow = [];
                    JObject floorSpanDirection = [];
                    double floorSlope = 0.0;

                    IList<ElementId> floorEids = floorSketch.GetAllElements();
                    foreach (ElementId floorEid in floorEids)
                    {
                        Element subElem = doc.GetElement(floorEid);
                        if (subElem == null) continue;

                        string elemName = subElem.Name;

                        if (elemName == "Slope Arrow" || elemName == "경사 화살표")
                        {
                            Curve curve = (subElem as CurveElement)?.GeometryCurve;
                            if (curve != null)
                                floorSlopeArrow = GetInfo.GetCurveDescription(curve);

                            IList<Parameter> parameters = ((ModelLine)doc.GetElement(floorEid)).GetOrderedParameters();
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    if (param.Definition.Name == "Slope" || param.Definition.Name == "경사")
                                    {
                                        floorSlope = param.AsDouble();
                                        break;
                                    }
                                }
                            }
                        }
                        else if (elemName == "Span Direction Edges" || elemName == "스팬 방향 모서리")
                        {
                            Curve curve = (subElem as CurveElement)?.GeometryCurve;
                            if (curve != null)
                                floorSpanDirection = GetInfo.GetCurveDescription(curve);
                        }
                    }

                    // 프로필
                    JObject floorProfile = GetInfo.GetProfileDescription(floorSketch);
                    floorInfo["Geometry"]["Profile"] = floorProfile;

                    // 경사 화살표
                    floorInfo["Geometry"]["SlopeArrow"] = floorSlopeArrow.HasValues ? floorSlopeArrow : "None";
                    if (floorSlopeArrow.HasValues) floorInfo["Geometry"]["SlopeAngle"] = floorSlope;

                    // 스팬 방향
                    floorInfo["Geometry"]["SpanDirection"] = floorSpanDirection.HasValues ? floorSpanDirection : "None";

                    answer2[elem.Id.ToString()] = floorInfo;
                    ((JArray)answer["Floor"]).Add(elem.Id.ToString());
                }
                else if (cat == "Ceilings")
                {
                    Ceiling ceiling = elem as Ceiling;

                    JObject ceilingInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)ceilingInfo["Parameter"]);
                    ceilingInfo["Common"]["ElementId"] = elem.Id.ToString();
                    ceilingInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    ceilingInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    // Sketch에서 프로필 정보 추출
                    Sketch ceilingSketch = doc.GetElement(ceiling.SketchId) as Sketch;
                    if (ceilingSketch == null) continue;

                    JObject ceilingCurveLoops = GetInfo.GetProfileDescription(ceilingSketch);
                    ceilingInfo["Geometry"]["Profile"] = ceilingCurveLoops;

                    // Slope 정보 초기화
                    JObject ceilingSlopeArrow = new JObject();
                    double ceilingSlope = 0.0;

                    IList<ElementId> ceilingEIds = ceilingSketch.GetAllElements();
                    foreach (ElementId ceilingEId in ceilingEIds)
                    {
                        Element subElem = doc.GetElement(ceilingEId);
                        string name = subElem?.Name ?? "";

                        if (name == "Slope Arrow" || name == "경사 화살표")
                        {
                            if (subElem is CurveElement curveElem)
                            {
                                Curve ceilingCrv = curveElem.GeometryCurve;
                                ceilingSlopeArrow = GetInfo.GetCurveDescription(ceilingCrv);
                            }

                            if (subElem is ModelLine modelLine)
                            {
                                foreach (Parameter param in modelLine.GetOrderedParameters())
                                {
                                    if (param.Definition.Name == "Slope" || param.Definition.Name == "경사")
                                    {
                                        ceilingSlope = param.AsDouble();
                                    }
                                }
                            }
                        }
                    }

                    // geometry에 Slope 정보 기록
                    if (ceilingSlopeArrow.HasValues)
                    {
                        ceilingInfo["Geometry"]["SlopeArrow"] = ceilingSlopeArrow;
                        ceilingInfo["Geometry"]["SlopeAngle"] = ceilingSlope;
                    }
                    else
                    {
                        ceilingInfo["Geometry"]["SlopeArrow"] = "None";
                    }

                    answer2[elem.Id.ToString()] = ceilingInfo;
                    ((JArray)answer["Ceiling"]).Add(elem.Id.ToString());
                }
                else if (cat == "Columns")
                {
                    FamilyInstance column = elem as FamilyInstance;

                    JObject columnInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)columnInfo["Parameter"]);
                    columnInfo["Common"]["ElementId"] = elem.Id.ToString();
                    columnInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    columnInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    JObject columnLocation = GetInfo.GetXYZDescription((column.Location as LocationPoint).Point);
                    columnInfo["Geometry"]["Location"] = columnLocation;

                    answer2[elem.Id.ToString()] = columnInfo;
                    ((JArray)answer["Column"]).Add(elem.Id.ToString());
                }
                else if (cat == "Structural Columns")
                {
                    FamilyInstance column = elem as FamilyInstance;

                    JObject columnInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)columnInfo["Parameter"]);
                    columnInfo["Common"]["ElementId"] = elem.Id.ToString();
                    columnInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    columnInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    JObject columnLocation;
                    if (column.Location as LocationCurve != null)
                    {
                        columnLocation = GetInfo.GetCurveDescription((column.Location as LocationCurve).Curve);
                    }
                    else
                    {
                        columnLocation = GetInfo.GetXYZDescription((column.Location as LocationPoint).Point);
                    }
                    columnInfo["Geometry"]["Location"] = columnLocation;

                    answer2[elem.Id.ToString()] = columnInfo;
                    ((JArray)answer["Structural Column"]).Add(elem.Id.ToString());
                }
                else if (cat == "Windows")
                {
                    FamilyInstance wd = elem as FamilyInstance;

                    JObject wdInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)wdInfo["Parameter"]);
                    wdInfo["Common"]["ElementId"] = elem.Id.ToString();
                    wdInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    wdInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    // Host ID
                    long hostId = wd.Host?.Id?.Value ?? -1;
                    wdInfo["Geometry"]["HostId"] = hostId;

                    // 위치 정보
                    if (wd.Location is LocationPoint locPoint)
                    {
                        JObject location = GetInfo.GetXYZDescription(locPoint.Point);
                        wdInfo["Geometry"]["Location"] = location;
                    }

                    // 방향 뒤집힘 상태
                    wdInfo["Property"]["FlipFacing"] = wd.FacingFlipped;
                    wdInfo["Property"]["FlipHand"] = wd.HandFlipped;

                    answer2[elem.Id.ToString()] = wdInfo;
                    ((JArray)answer["Window"]).Add(elem.Id.ToString());
                }
                else if (cat == "Doors")
                {
                    FamilyInstance wd = elem as FamilyInstance;

                    JObject wdInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)wdInfo["Parameter"]);
                    wdInfo["Common"]["ElementId"] = elem.Id.ToString();
                    wdInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    wdInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    // Host ID
                    long hostId = wd.Host?.Id?.Value ?? -1;
                    wdInfo["Geometry"]["HostId"] = hostId;

                    // 위치 정보
                    if (wd.Location is LocationPoint locPoint)
                    {
                        JObject location = GetInfo.GetXYZDescription(locPoint.Point);
                        wdInfo["Geometry"]["Location"] = location;
                    }

                    // 방향 뒤집힘 상태
                    wdInfo["Property"]["FlipFacing"] = wd.FacingFlipped;
                    wdInfo["Property"]["FlipHand"] = wd.HandFlipped;

                    answer2[elem.Id.ToString()] = wdInfo;
                    ((JArray)answer["Door"]).Add(elem.Id.ToString());
                }
                else if (cat == "Railings")
                {
                    JObject wdInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)wdInfo["Parameter"]);
                    wdInfo["Common"]["ElementId"] = elem.Id.ToString();
                    wdInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    wdInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    answer2[elem.Id.ToString()] = wdInfo;
                    ((JArray)answer["Railing"]).Add(elem.Id.ToString());
                }
                else if (cat == "Stairs")
                {
                    JObject wdInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)wdInfo["Parameter"]);
                    wdInfo["Common"]["ElementId"] = elem.Id.ToString();
                    wdInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    wdInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    answer2[elem.Id.ToString()] = wdInfo;
                    ((JArray)answer["Stair"]).Add(elem.Id.ToString());
                }
                else if (cat == "Furniture")
                {
                    FamilyInstance furniture = elem as FamilyInstance;

                    JObject furnitureInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)furnitureInfo["Parameter"]);
                    furnitureInfo["Common"]["ElementId"] = elem.Id.ToString();
                    furnitureInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    furnitureInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    JObject furnitureLocation = GetInfo.GetXYZDescription((furniture.Location as LocationPoint).Point);
                    furnitureInfo["Geometry"]["Location"] = furnitureLocation;
                    furnitureInfo["Geometry"]["Level"] = furniture.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM).AsValueString();

                    answer2[elem.Id.ToString()] = furnitureInfo;
                    ((JArray)answer["Furniture"]).Add(elem.Id.ToString());
                }
                else if (cat == "Roofs")
                {
                    JObject roofInfo = new JObject
                    {
                        ["Common"] = new JObject(),
                        ["Geometry"] = new JObject(),
                        ["Parameter"] = new JObject(),
                        ["Property"] = new JObject(),
                        ["Parameter"] = new JObject(),
                    };

                    GetInfo.GetParameter(elem, (JObject)roofInfo["Parameter"]);
                    roofInfo["Common"]["ElementId"] = elem.Id.ToString();
                    roofInfo["Common"]["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                    roofInfo["Common"]["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();

                    if (elem is FootPrintRoof fpRoof)
                    {
                        cat = "Roofs: FootPrintRoof";

                        // Sketch 추출
                        ElementClassFilter roofSktFilter = new ElementClassFilter(typeof(Sketch));
                        ICollection<ElementId> sketchIds = fpRoof.GetDependentElements(roofSktFilter);

                        if (sketchIds.Count > 0)
                        {
                            Sketch fpRoofSketch = doc.GetElement(sketchIds.First()) as Sketch;
                            if (fpRoofSketch != null)
                            {
                                JObject fpRoofCurve = GetInfo.GetProfileDescription(fpRoofSketch);
                                roofInfo["Geometry"]["FootPrint"] = fpRoofCurve;
                            }
                        }
                    }
                    else if (elem is ExtrusionRoof extrusionRoof)
                    {
                        cat = "Roofs: ExtrusionRoof";

                        CurveLoop curveLoop = new CurveLoop();
                        ModelCurveArray profileCurves = extrusionRoof.GetProfile();
                        foreach (ModelCurve curve in profileCurves)
                        {
                            curveLoop.Append(curve.GeometryCurve);
                        }

                        // Plane 정보 및 Profile 추출
                        Plane workPlane = curveLoop.GetPlane();
                        JObject planeDesc = GetInfo.GetPlaneDescription(workPlane);
                        JObject profileDesc = GetInfo.GetCurveLoopDescription(curveLoop);

                        roofInfo["Geometry"]["WorkPlane"] = planeDesc;
                        roofInfo["Geometry"]["Profile"] = profileDesc;
                    }

                    ((JArray)answer["Roof"]).Add(elem.Id.ToString());
                    answer2[elem.Id.ToString()] = roofInfo;
                }
                else
                {
                    continue;
                }

                ((JArray)answer["All"]).Add(elem.Id.ToString());
            }
            
            File.WriteAllText("C:\\Users\\dlwjd\\OneDrive\\Desktop\\로그\\최종로그2\\plane\\export.json" +
                "", JsonConvert.SerializeObject(answer, Formatting.Indented), System.Text.Encoding.UTF8);
            File.WriteAllText("C:\\Users\\dlwjd\\OneDrive\\Desktop\\로그\\최종로그2\\plane\\export2.json" +
                "", JsonConvert.SerializeObject(answer2, Formatting.Indented), System.Text.Encoding.UTF8);

            return Result.Succeeded;
        }
    }
}
