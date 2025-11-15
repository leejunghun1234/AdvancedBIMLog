using AdvancedBIMLog;
using AdvancedBIMLog.Get;
using AdvancedBIMLog.Make;
using AdvancedBIMLog.Set;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;

namespace LogShape
{
    [Transaction(TransactionMode.Manual)]
    public class BIMLog : IExternalApplication
    {
        // 기본 정보
        public string userId;
        public string filename;
        public string filenameShort;
        public string creationGUID;

        // 로그 저장할 폴더의 경로
        public string folderPath;
        public string tempFolderPath = "C:\\ProgramData\\Autodesk\\Revit\\ABL_temp";

        // 최종 파일 추출
        //
        // 로그 최종
        public JObject jobject = [];
        // 로그 최종의 저장 경로
        public string jsonFile = "";
        // Time log sub
        public List<string> timelog_sub = [];
        // Time log
        public JObject timelog = [];

        // Time log 파일 이름
        public string jsonTime = "";
        

        // 객체 추적
        //
        // 객체 name 추적
        public List<string> elemList = [];
        // volume 및 location에 따라 객체 추적
        public Dictionary<string, string> volumeCheckDict = [];
        public Dictionary<string, string> locationCheckDict = [];
        // 가능한 객체인지 확인용
        public List<BuiltInCategory> elemCatList = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_Railings,
            BuiltInCategory.OST_StairsRailing,
            BuiltInCategory.OST_Furniture,
        };

        // file GUID 에 따른 로그 최종 파일 추출
        //
        // file GUID에 따른 log 파일
        public Dictionary<string, JObject> fileAndJObject = [];
        // file GUID 에 따른 로그 파일의 저장 경로
        public Dictionary<string, string> fileAndPath = [];
        // file GUID 에 따른 파일 이름 
        public Dictionary<string, string> fileNameList = [];


        // 여러개의 프로젝트 동시에 켰을 때 추적용
        public Dictionary<string, List<string>> elemListList = [];
        public Dictionary<string, List<string>> timeAndList = [];  // GUID와 timeLog_sub
        public Dictionary<string, Dictionary<string, string>> volumeGUID = [];
        public Dictionary<string, Dictionary<string, string>> locationGUID = [];

        // 로그 추출용
        public Dictionary<string, JObject> timeAndJObject = []; // GUID 와 timeLog

        // 로그 경로 추출
        public Dictionary<string, string> timeAndPath = []; // GUID 와 timeLog path

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath)) SetLogPath(); // 파일 경로 지정해주기
                if (folderPath == null) return Result.Failed;                    // 경로 생성 오류시

                application.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(DocumentChangeTracker);
                //application.ControlledApplication.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(FailureTracker);
                application.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(DocumentOpenedTracker);
                application.ControlledApplication.DocumentCreated += new EventHandler<DocumentCreatedEventArgs>(DocumentCreatedTracker);
                application.ControlledApplication.DocumentClosing += new EventHandler<DocumentClosingEventArgs>(DocumentClosingTracker);
                application.ControlledApplication.DocumentSavedAs += new EventHandler<DocumentSavedAsEventArgs>(DocumentSavedAsTracker);
                application.ControlledApplication.DocumentSaving += new EventHandler<DocumentSavingEventArgs>(DocumentSavingTracker);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                application.ControlledApplication.DocumentChanged -= new EventHandler<DocumentChangedEventArgs>(DocumentChangeTracker);
                //application.ControlledApplication.FailuresProcessing -= new EventHandler<FailuresProcessingEventArgs>(FailureTracker);
                application.ControlledApplication.DocumentOpened -= new EventHandler<DocumentOpenedEventArgs>(DocumentOpenedTracker);
                application.ControlledApplication.DocumentCreated -= new EventHandler<DocumentCreatedEventArgs>(DocumentCreatedTracker);
                application.ControlledApplication.DocumentClosing -= new EventHandler<DocumentClosingEventArgs>(DocumentClosingTracker);
                application.ControlledApplication.DocumentSavedAs -= new EventHandler<DocumentSavedAsEventArgs>(DocumentSavedAsTracker);
                application.ControlledApplication.DocumentSaving -= new EventHandler<DocumentSavingEventArgs>(DocumentSavingTracker);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        void DocumentChangeTracker(object sender, DocumentChangedEventArgs e)
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            creationGUID = doc.CreationGUID.ToString();

            jobject = fileAndJObject[creationGUID];
            jsonFile = fileAndPath[creationGUID];

            // time 별 timeLog
            timelog_sub = timeAndList[creationGUID];
            timelog = timeAndJObject[creationGUID];

            jsonTime = timeAndPath[creationGUID];
            elemList = elemListList[creationGUID]; // elemList 불러오기
            volumeCheckDict = volumeGUID[creationGUID];
            locationCheckDict = locationGUID[creationGUID];

            string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            string filename = Path.GetFileNameWithoutExtension(doc.PathName);
            
            // 변경된 객체 추적 -> 여기서는 굳이 selection 된 객체만을 대상으로 할 필요 없이 전부 해야하는거지
            ICollection<ElementId> addedElements = e.GetAddedElementIds();
            ICollection<ElementId> modifiedElements = e.GetModifiedElementIds();
            ICollection<ElementId> deletedElements = e.GetDeletedElementIds();

            bool isChanged = false;
            if (addedElements != null)
            {
                try
                {
                    foreach (ElementId eid in addedElements)
                    {
                        Element elem = doc.GetElement(eid);
                        if (!CheckElemPossible(doc, elem)) continue; // elemCategory 안에 해당되는 element인지 확인하고 만약 아니라면 continue
                        
                        string eidString = $"{eid.ToString()}_1";
                        JObject addS = MakeMesh.ExportToMeshJObject(doc, elem, eidString, timestamp, "C");
                        if (addS == null) continue; // mesh 정보를 뽑을 수 없다면 continue

                        // 여기에 정보가 들어갈 수 있도록 -> 이걸 쓰면 되지 않을까
                        JObject addF = MakeLog.ExtractLog(doc, "C", eid, eidString, timestamp);
                        if (addF != null)
                        {
                            addS["Info"] = addF;
                        }
                        
                        ((JArray)jobject["ShapeLog"]).Add(addS);

                        elemList.Add(eidString);
                        timelog_sub.Add(eidString);
                        isChanged = true;

                        if (elem.Category.Name.ToString() == "Walls")
                        {
                            Wall wall = elem as Wall;
                            string wallVolume = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsValueString();
                            volumeCheckDict[eid.ToString()] = wallVolume;
                            
                            string centerPoint = GetCenterPoint.GetWallCenterPoint(wall);
                            locationCheckDict[eid.ToString()] = centerPoint;
                        }
                    }
                }
                catch
                {

                }
            }
            if (modifiedElements != null)
            {
                foreach (ElementId eid in modifiedElements)
                {
                    try
                    {
                        Element elem = doc.GetElement(eid);
                        if (!CheckElemPossible(doc, elem)) continue;  // elemCategory 안에 해당되는 element인지 확인하고 만약 아니라면 continue

                        if (elem.Category.Name == "Walls" && ((Wall)elem).CurtainGrid == null)
                        {
                            Wall wall = elem as Wall;

                            string wallVolume = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsValueString();
                            bool volumeCheck = volumeCheckDict[eid.ToString()] == wallVolume;

                            string centerPoint = GetCenterPoint.GetWallCenterPoint(wall);
                            bool locationCheck = locationCheckDict[eid.ToString()] == centerPoint;
                            if (volumeCheck && locationCheck)
                            {
                                continue;
                            }

                            // 이건 벽의 위치 혹은 volume이 변했다는 뜻
                            locationCheckDict[eid.ToString()] = centerPoint;
                            volumeCheckDict[eid.ToString()] = wallVolume;
                        }

                        int i = 1;
                        string eidString = $"{eid.ToString()}_{i}";
                        while (elemList.Contains(eidString))
                        {
                            timelog_sub.Remove(eidString);
                            i++;
                            eidString = $"{eid.ToString()}_{i}";
                        }
                        //JObject modiS;
                        //if ((modiS = exportToMeshJObject(doc, elem, eidString, timestamp, "M")) == null) continue;
                        string modinum = eidString.Split('_')[1];
                        JObject modiS = new JObject();
                        if (modinum == "1")
                        {
                            modiS = MakeMesh.ExportToMeshJObject(doc, elem, eidString, timestamp, "C");
                        }
                        else
                        {
                            modiS = MakeMesh.ExportToMeshJObject(doc, elem, eidString, timestamp, "M");
                        }
                        if (modiS == null) continue;

                        // 여기에 정보가 들어갈 수 있도록 -> 이걸 쓰면 되지 않을까
                        JObject modiF = MakeLog.ExtractLog(doc, "M", eid, eidString, timestamp);
                        if (modiF != null)
                        {
                            modiS["Info"] = modiF;
                        }

                        ((JArray)jobject["ShapeLog"]).Add(modiS);
                        elemList.Add(eidString);
                        timelog_sub.Add(eidString);
                        isChanged = true;
                    }
                    catch
                    {

                    }
                }
            }

            if (deletedElements != null)
            {
                foreach (ElementId eid in deletedElements)
                {
                    try
                    {
                        string eidString = $"{eid.ToString()}_1";
                        int i = 1;

                        bool isElemIn = false;
                        while (elemList.Contains(eidString))
                        {
                            isElemIn = true;
                            timelog_sub.Remove(eidString);
                            i++;
                            eidString = $"{eid.ToString()}_{i}";
                        }
                        if (isElemIn)
                        {
                            JObject delS = new JObject
                            {
                                ["ElementId"] = eid.ToString(),
                                ["CommandType"] = "D",
                                ["Info"] = new JObject
                                {
                                    ["Common"] = new JObject
                                    {
                                        ["Timestamp"] = timestamp,
                                    }
                                }
                            };
                            ((JArray)jobject["ShapeLog"]).Add(delS);
                        }

                        isChanged = true;
                    }
                    catch 
                    {

                    }
                }
            }

            if (isChanged)
            {
                jobject["Saved"] = "False";
                
                MakeJson.MakeJsonFile(jsonFile, jobject);

                JArray jarray = new JArray(timelog_sub);
                timelog["ShapeLog"][timestamp] = jarray.DeepClone();
                MakeJson.MakeJsonFile(jsonTime, jobject);
            }
        
            // 아무것도 안바뀌었다면 Saved = False로 바꿔줘야해
        }

        void DocumentCreatedTracker(object sender, DocumentCreatedEventArgs e)
        {
            Document doc = e.Document;
            SetProjectInfo(doc);
            fileNameList[creationGUID] = filename;
            var startTime = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            jsonFile = Path.Combine(folderPath, startTime + $"_{creationGUID}" + $"_{doc.Title}" + ".json");
            jsonTime = Path.Combine(folderPath, startTime + $"_{creationGUID}" + $"_{doc.Title}" + "_time.json");

            JObject newJObject = new JObject
            {
                ["UserId"] = userId,
                ["Filename"] = filename,
                ["CreationGUID"] = creationGUID,
                ["StartTime"] = startTime,
                ["EndTime"] = "",
                ["Saved"] = "False",
                ["ShapeLog"] = new JArray()
            };
            JObject newTimeJObject = new JObject
            {
                ["UserId"] = userId,
                ["Filename"] = filename,
                ["CreationGUID"] = creationGUID,
                ["StartTime"] = startTime,
                ["EndTime"] = "",
                ["Saved"] = "False",
                ["ShapeLog"] = new JObject()
            };

            fileAndPath[creationGUID] = jsonFile;
            fileAndJObject[creationGUID] = newJObject;

            timeAndPath[creationGUID] = jsonTime;
            timeAndJObject[creationGUID] = newTimeJObject;

            // 이 때는 처음 create 하니까 만드는게 맞는데 open을 할 때에는 불러와야지. 저장할 때 같이 이 파일 저장하고
            elemListList[creationGUID] = new List<string>();
            timeAndList[creationGUID] = new List<string>();

            volumeGUID[creationGUID] = new Dictionary<string, string>();
            locationGUID[creationGUID] = new Dictionary<string, string>();

            MakeJson.MakeJsonFile(jsonFile, newJObject);
            MakeJson.MakeJsonFile(jsonTime, newTimeJObject);
        }

        void DocumentOpenedTracker(object sender, DocumentOpenedEventArgs e)
        {
            Document doc = e.Document;
            SetProjectInfo(doc);
            fileNameList[creationGUID] = doc.PathName.ToString();
            var startTime = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            jsonFile = Path.Combine(folderPath, startTime + $"_{creationGUID}" + $"_{doc.Title}" + ".json");
            jsonTime = Path.Combine(folderPath, startTime + $"_{creationGUID}" + $"_{doc.Title}" + "_time.json");

            JObject newJObject = new JObject
            {
                ["UserId"] = userId,
                ["Filename"] = filenameShort,
                ["CreationGUID"] = creationGUID,
                ["StartTime"] = startTime,
                ["EndTime"] = "",
                ["Saved"] = "False",
                ["ShapeLog"] = new JArray()
            };
            JObject newTimeJObject = new JObject
            {
                ["UserId"] = userId,
                ["Filename"] = filenameShort,
                ["CreationGUID"] = creationGUID,
                ["StartTime"] = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss"),
                ["EndTime"] = "",
                ["Saved"] = "False",
                ["ShapeLog"] = new JObject()
            };

            fileAndPath[creationGUID] = jsonFile;
            fileAndJObject[creationGUID] = newJObject;

            timeAndPath[creationGUID] = jsonTime;
            timeAndJObject[creationGUID] = newTimeJObject;

            MakeJson.MakeJsonFile(jsonFile, newJObject);
            MakeJson.MakeJsonFile(jsonTime, newTimeJObject);

            string elemListListPath = tempFolderPath + $"\\{creationGUID}_elemListList.json";
            string timeAndListPath = tempFolderPath + $"\\{creationGUID}_timeAndList.json";
            string volumeGUIDPath = tempFolderPath + $"\\{creationGUID}_volumeGUID.json";
            string locationGUIDPath = tempFolderPath + $"\\{creationGUID}_locationGUID.json";

            string elemListListString = File.ReadAllText(elemListListPath);
            string timeAndListString = File.ReadAllText(timeAndListPath);
            string volumeGUIDString = File.ReadAllText(volumeGUIDPath);
            string locationGUIDString = File.ReadAllText(locationGUIDPath);

            JArray elemListListJArray = JArray.Parse(elemListListString);
            JArray timeAndListJArray = JArray.Parse(timeAndListString);
            JObject volumeGUIDJObject = JObject.Parse(volumeGUIDString);
            JObject locationGUIDJObject = JObject.Parse(locationGUIDString);

            elemListList[creationGUID] = elemListListJArray.ToObject<List<string>>();
            timeAndList[creationGUID] = timeAndListJArray.ToObject<List<string>>();

            volumeGUID[creationGUID] = volumeGUIDJObject.ToObject<Dictionary<string, string>>();
            locationGUID[creationGUID] = locationGUIDJObject.ToObject<Dictionary<string, string>>();
        }

        void FailureTracker(object sender, FailuresProcessingEventArgs e)
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc != null)
            {
                Document doc = uidoc.Document;
                string user = doc.Application.Username;
                string filename = doc.PathName;
                string filenameShort = Path.GetFileNameWithoutExtension(filename);

                FailuresAccessor failuresAccessor = e.GetFailuresAccessor();
                IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
            }
        }

        void DocumentClosingTracker(object sender, DocumentClosingEventArgs e)
        {
            Document doc = e.Document;
            SetProjectInfo(doc);

            JObject sl = fileAndJObject[creationGUID];
            JObject tl = timeAndJObject[creationGUID];

            var endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            sl["EndTime"] = endTime;
            tl["EndTime"] = endTime;

            string saved = sl["Saved"].ToString();
            if (saved == "" || saved == "False")
            {
                MakeJson.MakeJsonFile(jsonFile, sl);
                MakeJson.MakeJsonFile(jsonTime, tl);
            }
        }

        void DocumentSavedAsTracker(object sender, DocumentSavedAsEventArgs e)
        {
            Document doc = e.Document;

            string filename = doc.PathName;
            string filenameShort = Path.GetFileNameWithoutExtension (filename);

            string extension = GetProjectInfo(doc);

            jsonFile = extension + $"_{doc.Title}_saved.json";
            jsonTime = extension + $"_{doc.Title}_time_saved.json";

            var savedTime = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

            jobject["Filename"] = filenameShort;
            jobject["Saved"] = "True";
            jobject["EndTime"] = savedTime;

            timelog["Filename"] = filenameShort;
            timelog["Saved"] = "True";
            timelog["EndTime"] = savedTime;

            if (fileNameList[$"{doc.CreationGUID}"] != filename)
            {
                MakeJson.MakeJsonFile(jsonFile, jobject);
                MakeJson.MakeJsonFile(jsonTime, timelog);

                SetTempPath(tempFolderPath, doc.CreationGUID.ToString());
            }
        }

        void DocumentSavingTracker(object sender, DocumentSavingEventArgs e)
        {
            Document doc = e.Document;

            string extension = GetProjectInfo(doc);

            jsonFile = extension + $"_{doc.Title}_saved.json";
            jsonTime = extension + $"_{doc.Title}_time_saved.json";

            var savedTime = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");

            jobject["Filename"] = filenameShort;
            jobject["Saved"] = "True";
            jobject["EndTime"] = savedTime;

            timelog["Filename"] = filenameShort;
            timelog["Saved"] = "True";
            timelog["EndTime"] = savedTime;

            MakeJson.MakeJsonFile(jsonFile, jobject);
            MakeJson.MakeJsonFile(jsonTime, timelog);

            SetTempPath(tempFolderPath, doc.CreationGUID.ToString());
        }

        public bool CheckElemPossible(Document doc, Element elem)
        {
            if (elem == null || doc.GetElement(elem.GetTypeId()) == null) return false;
            try
            {
                if (elem.Category == null) return false;
                if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Walls)
                {
                    Wall wall = elem as Wall;
                    if (wall.CurtainGrid != null)
                    {
                        // 커튼월일 때 가능하게 해야지
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Stairs
                && ((Stairs)elem).IsInEditMode() == false) return true;
            if (elem.Category.Name.ToString() == "Railings"
                && ((Railing)elem).TopRail.ToString() != "-1") return true;
            if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Cornices) return true;

            double? elemVolume;
            try
            {
                elemVolume = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble();
                if (elemVolume == null || elemVolume < 0.001) return false;
            }
            catch
            {
                return false;
            }
            if (!elemCatList.Contains(elem.Category.BuiltInCategory)) return false;
            return true;
        }
    
        void SetLogPath()
        {
            try
            {
                FileInfo fi = new FileInfo("C:\\ProgramData\\Autodesk\\Revit\\ABL_Directory.txt");
                if (fi.Exists)
                {
                    string logFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                    string pathFile = Path.Combine(logFilePath, "ABL_Directory.txt");
                    using (StreamReader readtext = new StreamReader(pathFile, true))
                    {
                        folderPath = readtext.ReadLine();
                    }
                }
                else
                {
                    System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
                    folderBrowser.Description = "Select a folder to save Revit Modeling shape log path";
                    folderBrowser.ShowNewFolderButton = true;
                    if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        folderPath = folderBrowser.SelectedPath;
                        string LogFilePath = "C:\\ProgramData\\Autodesk\\Revit";
                        string pathFile = Path.Combine(LogFilePath, "ABL_Directory.txt");
                        using (StreamWriter writetext = new StreamWriter(pathFile, true))
                        {
                            writetext.WriteLine(folderPath);
                        }
                    }
                }

                if (!Directory.Exists(tempFolderPath))
                {
                    Debug.WriteLine(tempFolderPath);
                    Directory.CreateDirectory(tempFolderPath);
                }
            }
            catch
            {
                Autodesk.Revit.UI.TaskDialog.Show("파일 경로 오류", "파일 경로 오류");
            }
        }

        void SetTempPath(string extension, string creationGUID)
        {
            string elemListListPath = extension + $"\\{creationGUID}_elemListList.json";
            string timeAndListPath = extension + $"\\{creationGUID}_timeAndList.json";
            string volumeGUIDPath = extension + $"\\{creationGUID}_volumeGUID.json";
            string locationGUIDPath = extension + $"\\{creationGUID}_locationGUID.json";

            JArray elemListJObject = JArray.FromObject(elemListList[creationGUID]);
            JArray timeAndListJObject = JArray.FromObject(timeAndList[creationGUID]);
            JObject volumeListJObject = JObject.FromObject(volumeGUID[creationGUID]);
            JObject locationListJObject = JObject.FromObject(locationGUID[creationGUID]);

            MakeJson.MakeJsonFile(elemListListPath, elemListJObject);
            MakeJson.MakeJsonFile(timeAndListPath, timeAndListJObject);
            MakeJson.MakeJsonFile(volumeGUIDPath, volumeListJObject);
            MakeJson.MakeJsonFile(locationGUIDPath, locationListJObject);
        }

        void SetProjectInfo(Document doc)
        {
            userId = doc.Application.Username;
            filename = doc.PathName;
            creationGUID = doc.CreationGUID.ToString();
            filenameShort = Path.GetFileNameWithoutExtension(filename);
        }

        string GetProjectInfo(Document doc)
        {
            userId = doc.Application.Username;
            string filename = doc.PathName;
            BasicFileInfo info = BasicFileInfo.Extract(filename);

            DocumentVersion v = info.GetDocumentVersion();
            string projectId = v.VersionGUID.ToString();

            jsonFile = fileAndPath[$"{doc.CreationGUID}"];
            jobject = fileAndJObject[$"{doc.CreationGUID}"];
            timelog = timeAndJObject[$"{doc.CreationGUID}"];
            timelog_sub = timeAndList[$"{doc.CreationGUID}"];

            string index = folderPath + "\\" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + $"_{doc.CreationGUID}";
            string extension = jsonFile.Substring(0, index.Length);

            return extension;
        }
    }
}

