using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Visualization
{
    [Transaction(TransactionMode.Manual)]
    public class Visualizer : IExternalCommand
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

            string unityExecutablePath = @"C:\Users\dlwjd\OneDrive\Desktop\Unity\Visualization.exe";

            try
            {
                Process.Start(unityExecutablePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("", $"Failed to start Unity: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
