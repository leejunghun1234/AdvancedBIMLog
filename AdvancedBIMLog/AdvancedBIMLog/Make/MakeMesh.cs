using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Make
{
    internal class MakeMesh
    {
        public static JObject ExportToMeshJObject(Document doc, Element elem, string elemid, string timestamp, string CorM)
        {
            JObject job = new JObject
            {
                ["ElementId"] = elem.Id.ToString(),
                ["CommandType"] = CorM,
                ["Info"] = new JObject(),
                ["Meshes"] = new JArray()
            };
            string elemCat = elem.Category.Name.ToString();
            BuiltInCategory elemBuiltInCat = elem.Category.BuiltInCategory;

            List<BuiltInCategory> cat1 = new()
            {
                BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Doors,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_Railings,
            };

            // Geometry Options 추출
            Options options = new()
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false,
            };
            if (elemCat == "Railings")
            {
                options.IncludeNonVisibleObjects = true;
            }

            // 객체에 대한 geometry 추출
            GeometryElement geomElem = elem.get_Geometry(options);

            // geometry to json
            if (elemBuiltInCat == BuiltInCategory.OST_Walls && ((Wall)elem).CurtainGrid != null)
            {
                Wall wall = elem as Wall;
                CurtainGrid cg = wall.CurtainGrid;
                if (cg != null)
                {
                    ICollection<ElementId> panels = cg.GetPanelIds();
                    ICollection<ElementId> mullions = cg.GetMullionIds();

                    MakeCurtainWallToText(doc, job, mullions, options);
                    MakeCurtainWallToText(doc, job, panels, options);
                }
            }
            else
            {
                MakeElemToText(doc, job, geomElem);
            }

            return job;
        }

        public static void MakeElemToText(Document doc, JObject job, GeometryElement geomElem)
        {
            if (geomElem == null) return;

            List<string> matList = [];
            Dictionary<string, Tuple<string, int, JArray>> matInfo = [];

            Dictionary<string, Dictionary<string, int>> materialDict = [];
            Dictionary<string, int> indexDict = [];
            Dictionary<string, JArray> verticesDict = [];
            Dictionary<string, JArray> indicesDict = [];

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is GeometryInstance geomInst)
                {
                    ProcessGeometryInstance(doc, job, geomInst, matList, matInfo, materialDict, indexDict, verticesDict, indicesDict);
                }
                else if (geomObj is Solid solid && solid.Volume > 0)
                {
                    ProcessSolid(doc, solid, matList, matInfo, materialDict, indexDict, verticesDict, indicesDict);
                }
            }

            foreach (string mat in matList)
            {
                if (verticesDict[mat].Count == 0) continue;
                JObject meshData = new JObject
                {
                    ["Material"] = matInfo[mat].Item1,
                    ["Color"] = matInfo[mat].Item3,
                    ["Transparency"] = matInfo[mat].Item2,
                    ["Vertices"] = verticesDict[mat],
                    ["Indices"] = indicesDict[mat]
                };
                ((JArray)job["Meshes"]).Add(meshData);
            }
        }

        public static void MakeCurtainWallToText(Document doc, JObject job, ICollection<ElementId> elemIds, Options options)
        {
            var matList = new List<string>();
            var matInfo = new Dictionary<string, Tuple<string, int, JArray>>();
            var materialDict = new Dictionary<string, Dictionary<string, int>>();
            var indexDict = new Dictionary<string, int>();
            var verticesDict = new Dictionary<string, JArray>();
            var indicesDict = new Dictionary<string, JArray>();

            foreach (var elemId in elemIds)
            {
                Element elem = doc.GetElement(elemId);
                GeometryElement geomElem = elem?.get_Geometry(options);
                if (geomElem == null) continue;

                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is not GeometryInstance geomInst) continue;
                    Transform transform = geomInst.Transform;

                    foreach (GeometryObject instObj in geomInst.SymbolGeometry)
                    {
                        if (instObj is Solid solid && solid.Volume > 0)
                        {
                            ProcessSolid(doc, solid, matList, matInfo, materialDict, indexDict, verticesDict, indicesDict, transform);
                        }
                    }
                }
            }

            foreach (string mat in matList)
            {
                if (verticesDict[mat].Count == 0) continue;

                JObject meshData = new JObject
                {
                    ["Material"] = matInfo[mat].Item1,
                    ["Color"] = matInfo[mat].Item3,
                    ["Transparency"] = matInfo[mat].Item2,
                    ["Vertices"] = verticesDict[mat],
                    ["Indices"] = indicesDict[mat],
                };

                ((JArray)job["Meshes"]).Add(meshData);
            }
        }


        static void ProcessGeometryInstance(
            Document doc,
            JObject job,
            GeometryInstance geomInst,
            List<string> matList,
            Dictionary<string, Tuple<string, int, JArray>> matInfo,
            Dictionary<string, Dictionary<string, int>> materialDict,
            Dictionary<string, int> indexDict,
            Dictionary<string, JArray> verticesDict,
            Dictionary<string, JArray> indicesDict)
        {
            Transform t = geomInst.Transform;
            foreach (GeometryObject instObj in geomInst.SymbolGeometry)
            {
                if (instObj is Solid solid && solid.Volume > 0)
                {
                    ProcessSolid(doc, solid, matList, matInfo, materialDict, indexDict, verticesDict, indicesDict, t);
                }
                else if (instObj is GeometryInstance nestedInst)
                {
                    ProcessGeometryInstance(doc, job, nestedInst, matList, matInfo, materialDict, indexDict, verticesDict, indicesDict);
                }
            }
        }

        static void ProcessSolid(
            Document doc,
            Solid solid,
            List<string> matList,
            Dictionary<string, Tuple<string, int, JArray>> matInfo,
            Dictionary<string, Dictionary<string, int>> materialDict,
            Dictionary<string, int> indexDict,
            Dictionary<string, JArray> verticesDict,
            Dictionary<string, JArray> indicesDict,
            Transform transform = null)
        {
            foreach (Face face in solid.Faces)
            {
                Material m = doc.GetElement(face.MaterialElementId) as Material;
                string mName = m?.Name ?? "Default material";
                int mTransparency = m?.Transparency ?? 0;
                JArray mColor = m != null ? new JArray(m.Color.Red, m.Color.Green, m.Color.Blue) : new JArray(128, 128, 128);

                if (!matList.Contains(mName))
                {
                    matList.Add(mName);
                    matInfo[mName] = Tuple.Create(mName, mTransparency, mColor);
                }

                var vertexMap = materialDict.ContainsKey(mName) ? materialDict[mName] : new Dictionary<string, int>();
                var vertexCounter = indexDict.ContainsKey(mName) ? indexDict[mName] : 0;
                var vertices = verticesDict.ContainsKey(mName) ? verticesDict[mName] : new JArray();
                var indices = indicesDict.ContainsKey(mName) ? indicesDict[mName] : new JArray();

                Mesh mesh = face.Triangulate();
                if (mesh == null) continue;

                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    MeshTriangle triangle = mesh.get_Triangle(i);
                    for (int j = 0; j < 3; j++)
                    {
                        XYZ vertex = triangle.get_Vertex(j);
                        if (transform != null) vertex = transform.OfPoint(vertex);
                        string key = $"{vertex.X},{vertex.Y},{vertex.Z}";

                        if (!vertexMap.ContainsKey(key))
                        {
                            vertexMap[key] = vertexCounter++;
                            vertices.Add(new JArray(vertex.X, vertex.Y, vertex.Z));
                        }

                        indices.Add(vertexMap[key]);
                    }
                }

                materialDict[mName] = vertexMap;
                indexDict[mName] = vertexCounter;
                verticesDict[mName] = vertices;
                indicesDict[mName] = indices;
            }
        }
    }
}
