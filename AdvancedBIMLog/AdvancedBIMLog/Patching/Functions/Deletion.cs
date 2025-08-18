using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Patching.Functions
{
    internal class Deletion
    {
        public static void deleteElement(Autodesk.Revit.DB.Document doc, JObject log, JObject elementDict)
        {
            Element elem = Func.getElem(doc, log, elementDict);

            doc.Delete(elem.Id);
        }
    }
}
