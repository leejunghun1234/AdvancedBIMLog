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
    internal class Func
    {
        public static void ExtractLogData(
            JObject log,
            string CorM,
            out JObject common,
            out JObject geometry,
            out JObject parameter,
            out JObject property,
            out JArray layers)
        {
            if (CorM == "C")
            {
                common = (JObject)log["Info"]["Common"];
                geometry = (JObject)log["Info"]["Geometry"];
                parameter = (JObject)log["Info"]["Parameter"];
                property = (JObject)log["Info"]["Property"];
                layers = (JArray)log["Info"]["Layers"];
            }
            else if (CorM == "M")
            {
                common = (JObject)log["Info"]["ModifiedCommon"];
                geometry = (JObject)log["Info"]["ModifiedGeometry"];
                parameter = (JObject)log["Info"]["ModifiedParameter"];
                property = (JObject)log["Info"]["ModifiedProperty"];
                layers = (JArray)log["Info"]["ModifiedLayers"];
            }
            else
            {
                common = null;
                geometry = null;
                parameter = null;
                property = null;
                layers = null;
            }
        }

        public static void ExtractCommonData(
            JObject common,
            out string timestamp,
            out string elementid,
            out string elementcategory,
            out string elementfamily,
            out string elementtype)
        {
            timestamp = common["Timestamp"].ToString();
            elementid = common["ElementId"].ToString().Split("_")[0];
            elementcategory = common["ElementCategory"].ToString();
            elementfamily = common["ElementFamily"].ToString();
            elementtype = common["ElementType"].ToString();
        }

        public static void SetElementParameters(
            Document doc,
            Element element,
            JObject log,
            JObject elementDict)
        {
            JObject parameter;
            Debug.WriteLine(log);
            if (log.ContainsKey("Info"))
            {
                parameter = (JObject)log["Info"]["Parameter"];
            }
            else if (log.ContainsKey("Parameter"))
            {
                parameter = (JObject)log["Parameter"];
            }
            else
            {
                parameter = (JObject)log["ModifiedParameter"];
            }
            
            JObject builtInParams;
            if (parameter.ContainsKey("Built-In"))
            {
                builtInParams = (JObject)parameter["Built-In"];
            }
            else
            {
                builtInParams = parameter;
            }

            foreach (var param in builtInParams)
            {
                string paramName = param.Key;

                if (paramName == "Level_1" || paramName == "Category_1")
                {
                    continue;
                }

                JObject paramValue = (JObject)param.Value;
                // Parameter elemParam = element.LookupParameter(paramName);

                //BuiltInParameter bip = (BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), paramName);
                //Parameter elemParam0 = element.get_Parameter(bip);

                Parameter elemParam = GetParameterByDef(element, paramName);

                if (elemParam != null && !elemParam.IsReadOnly)
                {
                    string storageType = (string)paramValue["StorageType"];
                    JToken valueToken = paramValue["Value"];

                    if (storageType == "String")
                    {
                        elemParam.Set((string)valueToken);
                        continue;
                    }

                    if (storageType == "Double")
                    {
                        elemParam.Set((double)valueToken);
                        continue;
                    }
                    if (storageType == "Integer")
                    {
                        elemParam.Set((int)valueToken);
                        continue;
                    }
                    if (storageType == "ElementId")
                    {
                        long hostIdValue = (long)valueToken;

                        if (elementDict.ContainsKey(valueToken.ToString()))
                        {
                            hostIdValue = elementDict[valueToken.ToString()]["NewId"].ToObject<long>();
                        }
                        // 이 부분도 elementId가 다르지 않을까?
                        elemParam.Set(new ElementId((long)valueToken));
                        continue;
                    }
                }
            }
        }

        public static Parameter GetParameterByDef(Element elem, string parameterName)
        {
            foreach (Parameter p in elem.Parameters)
            {
                InternalDefinition pdef = p.Definition as InternalDefinition;
                var pDefName = pdef.BuiltInParameter.ToString();

                if (pDefName == parameterName)
                {
                    return p;
                }
            }
            return null;
        }

        public static void setElementLog(JObject elementJson, string elementid, JObject log)
        {
            if (elementJson.ContainsKey(elementid))
            {
                ((JArray)elementJson[elementid]).Add(log);
            }
            else
            {
                elementJson[elementid] = new JArray { log };
            }
        }

        public static T getElementType<T>(Document doc, string elementType) where T : ElementType
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(T))
                .ToElements()
                .Cast<T>()
                .FirstOrDefault(et => et.Name == elementType);
        }

        public static FamilySymbol getSymbol(Document doc, string elementfamily, string elementtype)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.FamilyName == elementfamily && fs.Name == elementtype);
        }

        public static Level getLevel(Document doc, string levelName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name == levelName);
        }

        public static Element getElem(Document doc, JObject log, JObject elementDict)
        {
            long elementidLong;
            elementidLong = elementDict[log["ElementId"].ToString()].ToObject<long>();
            ElementId elemId = new ElementId(elementidLong);
            Element elem = doc.GetElement(elemId);
            if (elem == null)
            {
                Debug.WriteLine("elem이 null이 나와");
            }
            return elem;
        }

        public static IList<CurveLoop> GetCurveListDescription(JObject JCurveListObject)
        {
            IList<CurveLoop> curves = new List<CurveLoop>();

            foreach (var JCurve in JCurveListObject)
            {
                if (JCurve.Key.Contains("profile"))
                {
                    CurveLoop cl = GetCurveLoopDescription((JArray)JCurve.Value);
                    curves.Add(cl);
                }
            }

            return curves;
        }

        public static CurveArray GetCurveArrayDescription(JArray JCurveArray)
        {
            CurveArray curvearray = new CurveArray();

            foreach (JObject curve in JCurveArray)
            {
                curvearray.Append(GetCurveDescription(curve));
            }

            return curvearray;
        }

        public static CurveLoop GetCurveLoopDescription(JArray JCurveLoop)
        {
            CurveLoop curveLoop = new CurveLoop();

            foreach (JObject curve in JCurveLoop)
            {
                Curve c = GetCurveDescription(curve);
                curveLoop.Append(c);
            }

            return curveLoop;
        }

        public static XYZ GetXYZPointDescription(JObject geometry)
        {
            JObject points = new JObject(geometry["Point"]);
            XYZ point = new XYZ(
                (double)points["X"],
                (double)points["Y"],
                (double)points["Z"]
                );
            return point;
        }

        public static Autodesk.Revit.DB.Curve GetCurveDescription(JObject geometry)
        {
            JObject curveList = new JObject();
            if (geometry.ContainsKey("Curve"))
            {
                curveList = (JObject)geometry["Curve"];
            }

            else if (geometry.ContainsKey("Location"))
            {
                curveList = (JObject)geometry["Location"];
            }

            else if (geometry.ContainsKey("LocationCurve"))
            {
                curveList = (JObject)geometry["LocationCurve"];
            }
            else
            {
                curveList = geometry;
            }

            string curveType = curveList["Type"].ToString();

            switch (curveType)
            {
                case "Line":
                    JArray endpoint = (JArray)curveList["endPoints"];
                    XYZ startPoint = new XYZ(
                        (double)endpoint[0]["X"],
                        (double)endpoint[0]["Y"],
                        (double)endpoint[0]["Z"]
                        );
                    XYZ endPoint = new XYZ(
                        (double)endpoint[1]["X"],
                        (double)endpoint[1]["Y"],
                        (double)endpoint[1]["Z"]
                        );
                    Autodesk.Revit.DB.Curve line = Autodesk.Revit.DB.Line.CreateBound(startPoint, endPoint);

                    return line;

                case "Arc":

                    XYZ arcCenter = new XYZ(
                        (double)curveList["center"]["X"],
                        (double)curveList["center"]["Y"],
                        (double)curveList["center"]["Z"]
                        );
                    double arcRadius = (double)curveList["radius"];

                    double arcStartAngle;
                    double arcEndAngle;

                    if ((double)curveList["startAngle"] > (double)curveList["endAngle"])
                    {
                        arcStartAngle = (double)curveList["endAngle"];
                        arcEndAngle = (double)curveList["startAngle"];
                    }
                    else
                    {
                        arcStartAngle = (double)curveList["startAngle"];
                        arcEndAngle = (double)curveList["endAngle"];
                    }

                    // 이 부분이 애매해서 혹시 추가로 뽑을 수 있는지 확인
                    XYZ arcXAxis = new XYZ(
                        (double)curveList["xAxis"]["X"],
                        (double)curveList["xAxis"]["Y"],
                        (double)curveList["xAxis"]["Z"]
                        );
                    XYZ arcYAxis = new XYZ(
                        (double)curveList["yAxis"]["X"],
                        (double)curveList["yAxis"]["Y"],
                        (double)curveList["yAxis"]["Z"]
                        );

                    Autodesk.Revit.DB.Curve arc = Autodesk.Revit.DB.Arc.Create(arcCenter, arcRadius, arcStartAngle, arcEndAngle, arcXAxis, arcYAxis);
                    return arc;

                case "Ellipse":
                    XYZ ellipseCenter = new XYZ(
                        (double)curveList["center"]["X"],
                        (double)curveList["center"]["Y"],
                        (double)curveList["center"]["Z"]
                        );
                    double ellipseRadiusX = (double)curveList["radiusX"];
                    double ellipseRadiusY = (double)curveList["radiusY"];
                    XYZ ellipseXDirection = new XYZ(
                        (double)curveList["xDirection"]["X"],
                        (double)curveList["xDirection"]["Y"],
                        (double)curveList["xDirection"]["Z"]
                        );
                    XYZ ellipseYDirection = new XYZ(
                        (double)curveList["yDirection"]["X"],
                        (double)curveList["yDirection"]["Y"],
                        (double)curveList["yDirection"]["Z"]
                        );
                    double ellipseStartParam = (double)curveList["startParameter"];
                    double ellipseEndParam = (double)curveList["endParameter"];


                    Autodesk.Revit.DB.Curve ellipse = Autodesk.Revit.DB.Ellipse.CreateCurve(ellipseCenter, ellipseRadiusX, ellipseRadiusY, ellipseXDirection, ellipseYDirection, ellipseStartParam, ellipseEndParam);
                    return ellipse;

                default: return null;
            }
        }

        // quantity manage
        public static void qNumAdd(JObject elemQ, string elemName)
        {
            if (elemQ.ContainsKey(elemName))
            {
                elemQ[elemName] = (int)elemQ[elemName] + 1;
            }
            else
            {
                elemQ[elemName] = 1;
            }
        }

        public static void qNumSub(JObject elemQ, string elemName)
        {
            if (elemQ.ContainsKey(elemName))
            {
                elemQ[elemName] = (int)elemQ[elemName] - 1;
                if ((int)elemQ[elemName] <= 0)
                {
                    elemQ.Remove(elemName);
                }
            }
        }

        public static double qWidth(JObject property, JObject parameter)
        {
            double width = 0.0;
            if (property.ContainsKey("Width"))
            {
                width = double.Parse(property["Width"].ToString()) * 0.003281;
            }
            else if (property.ContainsKey("Thickness"))
            {
                width = double.Parse(property["Thickness"].ToString());
            }
            else
            {
                width = double.Parse(parameter["Built-In"]["Thickness"]["Value"].ToString());
            }
            return width;
        }

        public static DateTime textToDateTime1(string timestamp)
        {
            DateTime timeStamp = DateTime.ParseExact(
                timestamp,
                "yyyy_MM_dd_HH_mm_ss",
                System.Globalization.CultureInfo.InvariantCulture
            );
            return timeStamp;
        }

        public static DateTime textToDateTime2(string timestamp)
        {
            DateTime timeStamp = DateTime.ParseExact(
                timestamp,
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture
            );
            return timeStamp;
        }
    }
}
