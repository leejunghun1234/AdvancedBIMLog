using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit;
using Autodesk.Revit.UI;
using LogShape;
using Newtonsoft.Json.Linq;


namespace AdvancedBIMLog.Set
{
    internal class Set
    {
        // 초반에 한번, log 가 저장될 경로를 설정해주는 BIM_shapeLogDirectory.txt 를 생성함
        // 최종 경로는 "C:\\ProgramData\\Autodesk\\Revit\\BIG_shapeLogDirectory.txt" 가 보관하고 있는 경로를 읽음
        // 절대 경로로 보관
        public static void SetLogPath()
        {
            var main = new BIMLog();

            try
            {
                FileInfo fi = new FileInfo("C:\\ProgramData\\Autodesk\\Revit\\BIG_shapeLogDirectory.txt");
                if (fi.Exists)
                {
                    string logFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                    string pathFile = Path.Combine(logFilePath, "BIG_shapeLogDirectory.txt");
                    using (StreamReader readtext = new StreamReader(pathFile, true))
                    {
                        main.folderPath = readtext.ReadLine();
                    }
                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
                    folderBrowser.Description = "Select a folder to save Revit Modeling shape log path";
                    folderBrowser.ShowNewFolderButton = true;
                    if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        main.folderPath = folderBrowser.SelectedPath;
                        string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                        string pathFile = Path.Combine(LogFilePath, "BIG_shapeLogDirectory.txt");
                        using (StreamWriter writetext = new StreamWriter(pathFile, true))
                        {
                            writetext.WriteLine(main.folderPath);
                        }
                    }
                }
            }
            catch
            {
                Autodesk.Revit.UI.TaskDialog.Show("파일 경로 오류", "파일 경로 오류");
            }
        }
    
        // 동시에 두개의 프로젝트가 켜질 수도 있으니까 그거대비
        public static void SetTempPath(string extension, string creationGUID)
        {
            var main = new BIMLog();

            string elemListListPath = extension + $"\\{creationGUID}_elemListList.json";
            string timeAndListPath = extension + $"\\{creationGUID}_timeAndList.json";
            string volumeGUIDPath = extension + $"\\{creationGUID}_volumeGUID.json";
            string locationGUIDPath = extension + $"\\{creationGUID}_locationGUID.json";
            
            JArray elemListJObject = JArray.FromObject(main.elemListList[creationGUID]);
            JArray timeAndListJObject = JArray.FromObject(main.timeAndList[creationGUID]);
            JObject volumeListJObject = JObject.FromObject(main.volumeGUID[creationGUID]);
            JObject locationListJObject = JObject.FromObject(main.locationGUID[creationGUID]);

            Make.MakeJson.MakeJsonFile(elemListListPath, elemListJObject);
            Make.MakeJson.MakeJsonFile(timeAndListPath, timeAndListJObject);
            Make.MakeJson.MakeJsonFile(volumeGUIDPath, volumeListJObject);
            Make.MakeJson.MakeJsonFile(locationGUIDPath, locationListJObject);
        }
    }
}
