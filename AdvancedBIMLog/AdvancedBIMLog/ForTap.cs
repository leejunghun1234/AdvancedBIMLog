using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AdvancedBIMLog
{
    [Transaction(TransactionMode.ReadOnly)]
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            //RibbonPanel panel = application.CreateRibbonPanel(Tab.AddIns, "Design Patching");
            RibbonPanel panel = application.CreateRibbonPanel(Tab.AddIns, "Design Visualizer");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            PushButtonData buttonData = new PushButtonData(
                "Button_1",        // 내부 이름
                "  전처리  ",              // 버튼에 표시될 이름
                thisAssemblyPath,
                "AdvancedBIMLog.PostProcessing.Command" // IExternalCommand 클래스 경로
            );

            PushButtonData buttonData1_2 = new PushButtonData(
                "Button_1_2",        // 내부 이름
                "  시각화  ",              // 버튼에 표시될 이름
                thisAssemblyPath,
                "AdvancedBIMLog.Visualization.Visualizer" // IExternalCommand 클래스 경로
            );

            PushButtonData buttonData2 = new PushButtonData(
                "Button_2",
                "Patching",
                thisAssemblyPath,
                "AdvancedBIMLog.Patching.Patching"
            );

            PushButtonData buttonData3 = new PushButtonData(
                "Button_3",
                "Export Element",
                thisAssemblyPath,
                "AdvancedBIMLog.Export.Export"
            );

            PushButton button1 = panel.AddItem(buttonData) as PushButton;
            button1.ToolTip = "도구 설명을 여기에 작성하세요";
            button1.LargeImage = GetImageSource(@"C:\Users\dlwjd\Desktop\gitTest\AdvancedBIMLog\AdvancedBIMLog\AdvancedBIMLog\Resources\hama1 (1).png");

            PushButton button1_2 = panel.AddItem(buttonData1_2) as PushButton;
            button1_2.ToolTip = "도구 설명을 여기에 작성하세요";
            button1_2.LargeImage = GetImageSource(@"C:\Users\dlwjd\Desktop\gitTest\AdvancedBIMLog\AdvancedBIMLog\AdvancedBIMLog\Resources\hama1 (2).png");

            PushButton button2 = panel.AddItem(buttonData2) as PushButton;
            button2.ToolTip = "도구 설명을 여기다가 쓰면 돼";
            button2.LargeImage = GetImageSource(@"C:\Users\dlwjd\Desktop\gitTest\AdvancedBIMLog\AdvancedBIMLog\AdvancedBIMLog\Resources\hama1 (3).png");

            PushButton button3 = panel.AddItem(buttonData3) as PushButton;
            button3.ToolTip = "도구 설명을 여기다가 쓰면 돼";
            button3.LargeImage = GetImageSource(@"C:\Users\dlwjd\Desktop\gitTest\AdvancedBIMLog\AdvancedBIMLog\AdvancedBIMLog\Resources\hama1.png");

            //PushButtonData buttonData3 = new PushButtonData(
            //    "Button_3",
            //    "Join Element",
            //    thisAssemblyPath,
            //    "AdvancedBIMLog.Test"
            //);

            //PushButton button3 = panel.AddItem(buttonData3) as PushButton;
            //button3.ToolTip = "도구 설명을 여기다가 쓰면 돼";
            //button3.LargeImage = GetImageSource(@"C:\Users\dlwjd\Desktop\gitTest\AdvancedBIMLog\AdvancedBIMLog\AdvancedBIMLog\Resources\hama1.png");

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        
        private ImageSource GetImageSource(string imagePath)
        {
            if (System.IO.File.Exists(imagePath))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            return null;
        }
    }
}
