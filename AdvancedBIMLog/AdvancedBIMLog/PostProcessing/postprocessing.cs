using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LogShape;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.PostProcessing
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 로그가 저장된 폴더
            var main = new BIMLog();
            string logFolderPath = main.folderPath;

            // 최종 로그가 저장될 폴더
            string finalPath = "C:\\ProgramData\\Autodesk\\Revit\\FinalLog";
            if (!Directory.Exists(finalPath))
            {
                Directory.CreateDirectory(finalPath);
            }

            // 해당 프로젝트 GUID
            string projectGUID = doc.CreationGUID.ToString();

            // 최종으로 합쳐진 json 파일 생성용
            JArray shapeLogs = new JArray();
            JObject timeLogs = new JObject();

            JObject shapeLogsT = [];

            string lastTime = "";

            bool check = false;
            try
            {
                var logFiles = Directory.GetFiles(logFolderPath)
                    .OrderBy(lf => Path.GetFileName(lf))
                    .ToArray();
                foreach (string logFile in logFiles)
                {
                    if (logFile.Contains(projectGUID) && logFile.EndsWith("_saved.json"))
                    {
                        string logstring = File.ReadAllText(logFile);
                        JObject logJObject = JObject.Parse(logstring);

                        // mesh data용 log
                        if (!logFile.Contains("_time"))
                        {
                            if (logFile.EndsWith("u_saved.json"))
                            {
                                shapeLogs.Clear();
                                shapeLogsT.RemoveAll();
                            }

                            JArray shapeLog = (JArray)logJObject["ShapeLog"];
                            foreach (var log in shapeLog)
                            {
                                shapeLogs.Add(log);

                                if ((string)log["CommandType"] == "D") continue;

                                string elemId = (string)log["Info"]["Common"]["ElementId"];

                                var layerClone = log["Info"]["Layers"].DeepClone();
                                JObject forQuantity = new JObject
                                {
                                    ["Layer"] = layerClone,
                                    ["ElementCategory"] = log["Info"]["Common"]["ElementCategory"],
                                    ["ElementFamily"] = log["Info"]["Common"]["ElementFamily"],
                                    ["ElementType"] = log["Info"]["Common"]["ElementType"],
                                };

                                shapeLogsT[elemId] = forQuantity;
                            }
                        }
                        // time data용 log
                        else
                        {
                            if (logFile.EndsWith("u_saved.json"))
                            {
                                check = true;
                                lastTime = "";
                                timeLogs.RemoveAll();
                            }
                            if (check == false)
                            {
                                string starttime = (string)logJObject["StartTime"];
                                timeLogs[starttime] = new JObject
                                {
                                    ["Elements"] = new JArray(),
                                    ["Quantity"] = new JObject(),
                                };

                                check = true;
                            }
                            JObject timeLog = (JObject)logJObject["ShapeLog"];
                            foreach (var p in timeLog)
                            {
                                string key = p.Key;
                                lastTime = key;
                                JArray value = (JArray)p.Value;
                                timeLogs[key] = new JObject
                                {
                                    ["Elements"] = value,
                                    ["Quantity"] = new JObject()
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
                Debug.WriteLine("안된게 있네??");
                Autodesk.Revit.UI.TaskDialog.Show("Error", "Error has been happend in PostProcessing");
                return Result.Failed;
            }

            HashSet<string> wallMaterials = [];
            HashSet<string> floorMaterials = [];
            HashSet<string> ceilingMaterials = [];
            HashSet<string> stColumnMaterials = [];
            HashSet<string> windowNames = [];
            HashSet<string> doorNames = [];
            foreach (var t in timeLogs.Properties())
            {
                JObject quantity = new JObject
                {
                    ["Walls"] = new JObject
                    {
                        ["All Volume"] = 0.0,
                    },
                    ["Curtain Walls"] = new JObject
                    {
                        ["All Numbers"] = 0.0,
                    },
                    ["Floors"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["All Volume"] = 0.0,
                    },
                    ["Ceilings"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["All Volume"] = 0.0
                    },
                    ["Columns"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["Column Length"] = 0.0,
                        ["Column Volume"] = 0.0
                    },
                    ["Structural Columns"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["All Length"] = 0.0,
                    },
                    ["Stairs"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["Stair Length"] = 0.0,
                        ["Stair Run Length"] = 0.0,
                        ["Stair Landing Length"] = 0.0,
                    },
                    ["Railings"] = new JObject
                    {
                        ["All Numbers"] = 0,
                        ["Railing Length"] = 0.0,
                    },
                    ["Windows"] = new JObject
                    {
                        ["All Numbers"] = 0,
                    },
                    ["Doors"] = new JObject
                    {
                        ["All Numbers"] = 0,
                    },
                };

                foreach (string e in t.Value["Elements"])
                {
                    JObject infos = (JObject)shapeLogsT[e];
                    if ((string)infos["ElementCategory"] == "Walls") // Layers = JArray
                    {
                        JObject wallsQ = (JObject)quantity["Walls"];
                        JArray wallInfos = (JArray)infos["Layer"];
                        foreach (var wallInfo in wallInfos)
                        {
                            string materialName = (string)wallInfo["Material Name"];
                            wallMaterials.Add(materialName);
                            string materialFunction = (string)wallInfo["Function"];
                            double materialVolume = (double)wallInfo["Material Volume"];
                            wallsQ["All Volume"] = (double)wallsQ["All Volume"] + (double)materialVolume;
                            if (wallsQ.ContainsKey(materialName))
                            {
                                wallsQ[materialName] = (double)wallsQ[materialName] + materialVolume;
                            }
                            else
                            {
                                wallsQ[materialName] = (double)materialVolume;
                            }
                        }

                    }
                    else if ((string)infos["ElementCategory"] == "Curtain Walls")
                    {
                        JObject ctWallQ = (JObject)quantity["Curtain Walls"];
                        ctWallQ["All Numbers"] = (int)ctWallQ["All Numbers"] + 1;
                    }
                    else if ((string)infos["ElementCategory"] == "Floors") // Layers = JArray
                    {
                        JObject floorsQ = (JObject)quantity["Floors"];
                        floorsQ["All Numbers"] = (int)floorsQ["All Numbers"] + 1;
                        JArray floorInfos = (JArray)infos["Layer"];
                        foreach (var floorInfo in floorInfos)
                        {
                            string materialName = (string)floorInfo["Material Name"];
                            floorMaterials.Add(materialName);
                            string materialFunction = (string)floorInfo["Function"];
                            double materialVolume = (double)floorInfo["Material Volume"];
                            floorsQ["All Volume"] = (double)floorsQ["All Volume"] + (double)materialVolume;
                            if (floorsQ.ContainsKey(materialName))
                            {
                                floorsQ[materialName] = (double)floorsQ[materialName] + materialVolume;
                            }
                            else
                            {
                                floorsQ[materialName] = (double)materialVolume;
                            }
                        }
                    }
                    else if ((string)infos["ElementCategory"] == "Ceilings") // Layers = JArray
                    {
                        JObject ceilingsQ = (JObject)quantity["Ceilings"];
                        ceilingsQ["All Numbers"] = (double)ceilingsQ["All Numbers"] + 1;
                        JArray ceilingInfos = (JArray)infos["Layer"];
                        foreach (var floorInfo in ceilingInfos)
                        {
                            string materialName = (string)floorInfo["Material Name"];
                            ceilingMaterials.Add(materialName);
                            string materialFunction = (string)floorInfo["Function"];
                            double materialVolume = (double)floorInfo["Material Volume"];
                            ceilingsQ["All Volume"] = (double)ceilingsQ["All Volume"] + (double)materialVolume;
                            if (ceilingsQ.ContainsKey(materialName))
                            {
                                ceilingsQ[materialName] = (double)ceilingsQ[materialName] + materialVolume;
                            }
                            else
                            {
                                ceilingsQ[materialName] = (double)materialVolume;
                            }
                        }
                    }
                    else if ((string)infos["ElementCategory"] == "Columns")
                    {
                        JObject columnsQ = (JObject)quantity["Columns"];
                        columnsQ["All Numbers"] = (int)columnsQ["All Numbers"] + 1;
                        columnsQ["Column Length"] = (double)columnsQ["Column Length"] + (double)infos["Layer"]["Column Length"];
                        columnsQ["Column Volume"] = (double)columnsQ["Column Volume"] + (double)infos["Layer"]["Column Volume"];
                    }
                    else if ((string)infos["ElementCategory"] == "Structural Columns")
                    {
                        JObject columnsQ = (JObject)quantity["Structural Columns"];
                        string columnMaterialName = (string)infos["Layer"]["Column Material"];
                        stColumnMaterials.Add(columnMaterialName);
                        columnsQ["All Numbers"] = (int)columnsQ["All Numbers"] + 1;
                        columnsQ["All Length"] = (double)columnsQ["All Length"] + (double)infos["Layer"]["Column Length"];

                        if (columnsQ.ContainsKey(columnMaterialName))
                        {
                            columnsQ[columnMaterialName] = (double)columnsQ[columnMaterialName] + (double)infos["Layer"]["Column Length"];
                        }
                        else
                        {
                            columnsQ[columnMaterialName] = (double)infos["Layer"]["Column Length"];
                        }
                    }
                    else if ((string)infos["ElementCategory"] == "Stairs")
                    {
                        JObject stairsQ = (JObject)quantity["Stairs"];
                        stairsQ["All Numbers"] = (int)stairsQ["All Numbers"] + 1;
                        stairsQ["Stair Length"] = (double)stairsQ["Stair Length"] + (double)infos["Layer"]["Stair Length"];
                        stairsQ["Stair Run Length"] = (double)stairsQ["Stair Run Length"] + (double)infos["Layer"]["Stair Run Length"];
                        stairsQ["Stair Landing Length"] = (double)stairsQ["Stair Landing Length"] + (double)infos["Layer"]["Stair Landing Length"];
                    }
                    else if ((string)infos["ElementCategory"] == "Railings")
                    {
                        JObject railingsQ = (JObject)quantity["Railings"];
                        railingsQ["All Numbers"] = (int)railingsQ["All Numbers"] + 1;
                        railingsQ["Railing Length"] = (double)railingsQ["Railing Length"] + (double)infos["Layer"]["Railing Length"];
                    }
                    else if ((string)infos["ElementCategory"] == "Windows")
                    {
                        JObject windowsQ = (JObject)quantity["Windows"];
                        windowsQ["All Numbers"] = (int)windowsQ["All Numbers"] + 1;
                        string familyType = (string)infos["ElementFamily"] + (string)infos["ElementType"];
                        windowNames.Add(familyType);
                        if (windowsQ.ContainsKey(familyType))
                        {
                            windowsQ[familyType] = (int)windowsQ[familyType] + 1;
                        }
                        else
                        {
                            windowsQ[familyType] = 1;
                        }
                    }
                    else if ((string)infos["ElementCategory"] == "Doors")
                    {
                        JObject doorsQ = (JObject)quantity["Doors"];
                        doorsQ["All Numbers"] = (int)doorsQ["All Numbers"] + 1;
                        string familyType = (string)infos["ElementFamily"] + (string)infos["ElementType"];
                        doorNames.Add(familyType);
                        if (doorsQ.ContainsKey(familyType))
                        {
                            doorsQ[familyType] = (int)doorsQ[familyType] + 1;
                        }
                        else
                        {
                            doorsQ[familyType] = 1;
                        }
                    }
                }

                CheckMaterial(quantity, wallMaterials, "Walls");
                CheckMaterial(quantity, floorMaterials, "Floors");
                CheckMaterial(quantity, ceilingMaterials, "Ceilings");
                CheckMaterial(quantity, stColumnMaterials, "Structural Columns");
                CheckMaterial(quantity, windowNames, "Windows");
                CheckMaterial(quantity, doorNames, "Doors");

                // 이따가 한번 정리하시고
                SortAndUpdate(quantity, "Walls");
                SortAndUpdate(quantity, "Curtain Walls");
                SortAndUpdate(quantity, "Floors");
                SortAndUpdate(quantity, "Ceilings");
                SortAndUpdate(quantity, "Columns");
                SortAndUpdate(quantity, "Structural Columns");
                SortAndUpdate(quantity, "Stairs");
                SortAndUpdate(quantity, "Railings");
                SortAndUpdate(quantity, "Windows");
                SortAndUpdate(quantity, "Doors");

                timeLogs[t.Name]["Quantity"] = quantity;
            }

            foreach (var prop in ((JObject)timeLogs[lastTime]["Quantity"]).Properties())
            {
                string testtesttest = Path.Combine(finalPath, $"{prop.Name}.csv");

                using (StreamWriter writer = new StreamWriter(testtesttest))
                {
                    StringBuilder catToCsv = new StringBuilder();
                    var catValues = timeLogs[lastTime]["Quantity"][prop.Name];
                    List<string> indexChecker = new List<string>();

                    foreach (var prop2 in ((JObject)catValues).Properties())
                    {
                        string columnName = prop2.Name.Replace(", ", "_");
                        indexChecker.Add(columnName);
                        catToCsv.Append(columnName + ", ");
                    }

                    writer.WriteLine(catToCsv.ToString().TrimEnd(',', ' '));

                    foreach (var t in timeLogs.Properties())
                    {
                        StringBuilder catToCsv2 = new StringBuilder();
                        Dictionary<string, string> rowValues = new Dictionary<string, string>();

                        foreach (var ts in ((JObject)timeLogs[t.Name]["Quantity"][prop.Name]).Properties())
                        {
                            string columnName2 = ts.Name.Replace(", ", "_");
                            rowValues[columnName2] = ts.Value.ToString();
                        }

                        foreach (var column in indexChecker)
                        {
                            catToCsv2.Append(rowValues.ContainsKey(column) ? rowValues[column] + ", " : "0, ");
                        }

                        writer.WriteLine(catToCsv2.ToString().TrimEnd(',', ' ').TrimEnd('\n', ' '));
                    }
                }

                // CSV 파일 읽기
                var lines = File.ReadAllLines(testtesttest);
                var csvData = lines.Select(line => line.Split(',')).ToArray();

                // 행과 열 전치
                var transposedData = Transpose(csvData);

                // 새로운 CSV 파일 저장
                string quantCSV = Path.Combine(finalPath, $"{prop.Name}_test.csv");
                File.WriteAllLines(quantCSV, transposedData.Select(row => string.Join(", ", row)));

                string unityPath = Path.Combine("C:\\Users\\dlwjd\\Desktop\\Unity Visualization\\Visualization\\Assets\\StreamingAssets\\", $"{prop.Name}.csv");
                File.WriteAllLines(unityPath, transposedData.Select(row => string.Join(", ", row)));
            }

            string finalShapePath = Path.Combine(finalPath, "AdvancedBIMLog_SL.json");
            string finalTimePath = Path.Combine(finalPath, "AdvancedBIMLog_TL.json");

            string testtest = Path.Combine(finalPath, "testtest.json");
            File.WriteAllText(testtest, JsonConvert.SerializeObject(shapeLogsT, Formatting.Indented), System.Text.Encoding.UTF8);

            File.WriteAllText(finalShapePath, JsonConvert.SerializeObject(shapeLogs, Formatting.Indented), System.Text.Encoding.UTF8);
            File.WriteAllText(finalTimePath, JsonConvert.SerializeObject(timeLogs, Formatting.Indented), System.Text.Encoding.UTF8);

            File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\Unity Visualization\\Visualization\\Assets\\StreamingAssets\\shapeLogs1.json", JsonConvert.SerializeObject(shapeLogs, Formatting.Indented), System.Text.Encoding.UTF8);
            File.WriteAllText("C:\\Users\\dlwjd\\Desktop\\Unity Visualization\\Visualization\\Assets\\StreamingAssets\\timeLogs1.json", JsonConvert.SerializeObject(timeLogs, Formatting.Indented), System.Text.Encoding.UTF8);

            Autodesk.Revit.UI.TaskDialog.Show("Complete", "Complete PostProcessing");

            return Result.Succeeded;
        }

        static string[][] Transpose(string[][] data)
        {
            int rowCount = data.Length;
            int colCount = data[0].Length;

            string[][] transposed = new string[colCount][];

            for (int i = 0; i < colCount; i++)
            {
                transposed[i] = new string[rowCount];
                for (int j = 0; j < rowCount; j++)
                {
                    transposed[i][j] = data[j][i];
                }
            }
            return transposed;
        }

        static void SortAndUpdate(JObject quantity, string key)
        {
            var sortedJObject = new JObject(
                ((JObject)quantity[key]).Properties()
                    .OrderByDescending(p => (double)p.Value)
            );

            quantity[key] = sortedJObject;
        }

        static void CheckMaterial(JObject quantity, HashSet<string> mHash, string key)
        {
            foreach (string m in mHash)
            {
                if (quantity[key][m] == null)
                {
                    quantity[key][m] = 0;
                }
            }
        }
    }
}
