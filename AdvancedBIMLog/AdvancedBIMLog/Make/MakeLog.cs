using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Make
{
    internal class MakeLog
    {
        public static JObject ExtractLog(Document doc, string cmd, ElementId eid, string eidString, string timestamp)
        {
            JObject job = [];
            Element elem = doc.GetElement(eid);

            JObject common = [];
            common["ElementId"] = eidString;
            common["Timestamp"] = timestamp;
            common["ElementCategory"] = elem.Category.Name.ToString();
            common["ElementFamily"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
            common["ElementType"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();
            JObject geometry = [];
            JObject parameter = [];
            JObject property = [];
            JArray layers = [];

            if (cmd != "C" && cmd != "M") return null;
            else
            {
                string cat = elem.Category.BuiltInCategory.ToString();
                GetInfo.GetParameter(elem, parameter);

                if (cat == "OST_Walls")
                {
                    ExtractWallInformation(doc, elem, geometry, property);
                }
                else if (cat == "OST_Floors")
                {
                    ExtractFloorInformation(doc, elem, geometry, property);
                }
                else if (cat == "OST_Roofs")
                {
                    ExtractRoofInformation(doc, elem, geometry, property, cat);
                }
                else if (cat == "OST_Ceilings")
                {
                    ExtractCeilingInformation(doc, elem, geometry, property);
                }
                else if (cat == "Stairs")
                {
                    // 없엉
                }
                else if (cat == "OST_Windows" || cat == "OST_Doors")
                {
                    ExtractWDInformation(doc, elem, geometry, property);
                }
                else if (cat == "OST_Furniture")
                {
                    ExtractFurnitureInformation(doc, elem, geometry, property);
                }
                else if (cat == "OST_Columns")
                {
                    ExtractColumnInformation(doc, elem, geometry, property);
                }
                else if (cat == "OST_StructuralColumns")
                {
                    ExtractStructuralColumnInformation(doc, elem, geometry, property);
                }

                job["Common"] = common;
                job["Geometry"] = geometry;
                job["Layers"] = layers;
                job["Property"] = property;
                job["Parameter"] = parameter;

                // Layer 정보 추가하기
                GetInfo.GetLayer(doc, elem, job);

                return job;
            }
        }

        static void ExtractWallInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not Wall wall) return;

            // 기본값
            bool wallIsProfileWall = true;
            JObject wallCurve = new JObject();

            // 프로필 벽 여부 판별
            Element sketchElem = doc.GetElement(wall.SketchId);
            if (sketchElem == null)
            {
                wallIsProfileWall = false;
            }

            // 곡선 정보 추출
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
            geometry["IsProfileWall"] = wallIsProfileWall;
            geometry["Curve"] = wallCurve;
            property["Flipped"] = isFlipped;
            property["Orientation"] = orientation;
            property["IsStructural"] = isStructural;
        }

        static void ExtractFloorInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not Floor floor) return;
            if (doc.GetElement(floor.SketchId) is not Sketch floorSketch) return;

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
            geometry["Profile"] = floorProfile;

            // 경사 화살표
            geometry["SlopeArrow"] = floorSlopeArrow.HasValues ? floorSlopeArrow : "None";
            if (floorSlopeArrow.HasValues) geometry["SlopeAngle"] = floorSlope;

            // 스팬 방향
            geometry["SpanDirection"] = floorSpanDirection.HasValues ? floorSpanDirection : "None";
        }

        static void ExtractRoofInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property,
            string cat)
        {
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
                        geometry["FootPrint"] = fpRoofCurve;
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

                geometry["WorkPlane"] = planeDesc;
                geometry["Profile"] = profileDesc;
            }
        }

        static void ExtractCeilingInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not Ceiling ceiling) return;

            // Sketch에서 프로필 정보 추출
            Sketch ceilingSketch = doc.GetElement(ceiling.SketchId) as Sketch;
            if (ceilingSketch == null) return;

            JObject ceilingCurveLoops = GetInfo.GetProfileDescription(ceilingSketch);
            geometry["Profile"] = ceilingCurveLoops;

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
                geometry["SlopeArrow"] = ceilingSlopeArrow;
                geometry["SlopeAngle"] = ceilingSlope;
            }
            else
            {
                geometry["SlopeArrow"] = "None";
            }
        }

        static void ExtractWDInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not FamilyInstance wd) return;

            // Host ID
            long hostId = wd.Host?.Id?.Value ?? -1;
            geometry["HostId"] = hostId;

            // 위치 정보
            if (wd.Location is LocationPoint locPoint)
            {
                JObject location = GetInfo.GetXYZDescription(locPoint.Point);
                geometry["Location"] = location;
            }

            // 방향 뒤집힘 상태
            property["FlipFacing"] = wd.FacingFlipped;
            property["FlipHand"] = wd.HandFlipped;
        }

        static void ExtractFurnitureInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not FamilyInstance furniture) return;

            JObject furnitureLocation = GetInfo.GetXYZDescription((furniture.Location as LocationPoint).Point);
            geometry["Location"] = furnitureLocation;
            geometry["Level"] = furniture.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM).AsValueString();
        }

        static void ExtractColumnInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not FamilyInstance column) return;

            JObject columnLocation = GetInfo.GetXYZDescription((column.Location as LocationPoint).Point);
            geometry["Location"] = columnLocation;
        }

        static void ExtractStructuralColumnInformation(
            Document doc,
            Element elem,
            JObject geometry,
            JObject property)
        {
            if (elem is not FamilyInstance column) return;

            JObject columnLocation;
            if (column.Location as LocationCurve != null)
            {
                columnLocation = GetInfo.GetCurveDescription((column.Location as LocationCurve).Curve);
            }
            else
            {
                columnLocation = GetInfo.GetXYZDescription((column.Location as LocationPoint).Point);
            }
            geometry["Location"] = columnLocation;
        }
    }
}
