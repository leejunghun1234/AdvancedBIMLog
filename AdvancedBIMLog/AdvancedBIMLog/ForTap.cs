using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog
{
    [Transaction(TransactionMode.ReadOnly)]
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel panel = application.CreateRibbonPanel(Tab.AddIns, "PostProcessing");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            PushButtonData buttonData = new PushButtonData(
                "Button_1",        // 내부 이름
                "Post Processing",              // 버튼에 표시될 이름
                thisAssemblyPath,
                "AdvancedBIMLog.PostProcessing.Command" // IExternalCommand 클래스 경로
            );

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "도구 설명을 여기에 작성하세요";

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
