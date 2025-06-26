using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Mesh
{
    internal class Mesh
    {
        class MaterialData
        {
            public string Name;
            public int Transparency;
            public JArray Color;
            public Dictionary<string, int> VertexMap = new();
            public int VertexCounter = 0;
            public JArray Vertices = new();
            public JArray Indices = new();
        }

        Dictionary<string, MaterialData> materialDataDict = new();

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

            Dictionary<string, Dictionary<string, int>> materialDict = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> indexDict = [];
            Dictionary<string, JArray> verticesDict = [];
            Dictionary<string, JArray> indicesDict = [];


        }

        void MakeCurtainWalltoText(Document doc, JObject job, ICollection<ElementId> elemIds, Options options)
        {

        }
    }
}
