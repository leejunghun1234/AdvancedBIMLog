using AdvancedBIMLog.Patching.Functions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdvancedBIMLog.Patching
{
    [Transaction(TransactionMode.Manual)]
    internal class Patching : IExternalCommand
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

            FileInfo fi = new FileInfo(@"C:\ProgramData\Autodesk\Revit\BIG_RollBack Directory.txt");
            string folderPath = "";
            if (fi.Exists)
            {
                string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                string pathFile = Path.Combine(LogFilePath, "BIG_RollBack Directory.txt");
                using (StreamReader readtext = new StreamReader(pathFile, true))
                {
                    string readText = readtext.ReadLine();
                    folderPath = readText;
                }
            }
            else
            {
                FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
                folderBrowser.Description = "Select a download folder";
                folderBrowser.ShowNewFolderButton = true;
                if (folderBrowser.ShowDialog() == DialogResult.OK)
                {
                    folderPath = folderBrowser.SelectedPath;
                    string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                    string pathFile = Path.Combine(LogFilePath, "BIG_RollBack Directory.txt");
                    using (StreamWriter writetext = new StreamWriter(pathFile, true))
                    {
                        writetext.WriteLine(folderPath);
                    }
                }
            }

            string patchingElemListPath = GetFilteredFiles(folderPath);

            DateTime time;
            JObject elementIdDict = new JObject();

            using (StreamReader file = File.OpenText(patchingElemListPath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject logs = (JObject)JToken.ReadFrom(reader);
                string originTime = (string)logs["Time"];
                string[] partTime = originTime.Split('\n');
                string dateTimeString = partTime[0] + " " + partTime[1];
                DateTime dateTime = DateTime.ParseExact(dateTimeString, "dd/MM/yyyy HH:mm:ss", null);
                time = dateTime;

                foreach (var eid in logs["Elements"])
                {
                    elementIdDict[eid.ToString()] = eid.ToString();
                }
            }

            // 이거 직접 선택하게 바꿔줘야겠지... 
            string logPath = @"C:\ProgramData\Autodesk\Revit\FinalLog\AdvancedBIMLog_SL.json";
            BeforePatching.preProcessing(
                logPath,
                time,
                elementIdDict,

                // 시간까지 모두 고려 한거
                out JArray selectedElemLog,

                // 그냥 거꾸로 가기 로그
                out JArray rlog,

                // 이전꺼 비교용 -> 실제로는 필요없어ㅣㅇㅅ
                out JObject newJson);

            //File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\tester\\test1.json" +
            //    "", JsonConvert.SerializeObject(rlog, Formatting.Indented), System.Text.Encoding.UTF8);
            //File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\tester\\test2.json" +
            //    "", JsonConvert.SerializeObject(selectedElemLog.Reverse(), Formatting.Indented), System.Text.Encoding.UTF8);
            //File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\tester\\test3.json" +
            //    "", JsonConvert.SerializeObject(elementIdDict, Formatting.Indented), System.Text.Encoding.UTF8);

            JArray sortedSelectedElemLog = new JArray(selectedElemLog.Reverse());
            foreach (JObject log in selectedElemLog.Reverse())
            {
                Transaction tx = new Transaction(doc, "start");
                tx.Start();

                try
                {
                    string cmd = (string)log["CommandType"];
                    if (cmd == "C")
                    {
                        Creation.createElement(doc, log, elementIdDict);
                    }
                    else if (cmd == "M")
                    {
                        Modification.modifyElement(doc, log, elementIdDict);
                    }
                    else if (cmd == "D")
                    {
                        Deletion.deleteElement(doc, log, elementIdDict);
                    }
                    tx.Commit();
                }
                catch
                {

                    tx.Commit();

                    Debug.WriteLine($"오류가 발생한 객체: {(string)log["ElementId"]}");
                }
            }

            //File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\tester\\test4.json" +
            //    "", JsonConvert.SerializeObject(elementIdDict, Formatting.Indented), System.Text.Encoding.UTF8);

            return Result.Succeeded;
        }

        // web 에서 받은 파일 중 가장 최신꺼
        // 이래 하는거 보다는 파일을 선택하게 하는게 제일 낫지 않나는 이런거 고민할 필요가 있나
        public string GetFilteredFiles(string folderPath)
        {
            string[] allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);

            List<string> patchlist = [];
            foreach (string f in allFiles)
            {
                if (!f.Contains(".json")) continue;
                if (!f.Contains("patch")) continue;

                patchlist.Add(f);
            }

            string newestFile = patchlist
                .OrderByDescending(f => File.GetCreationTime(f))
                .FirstOrDefault();

            return newestFile;
        }
    }
}
