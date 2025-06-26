using AdvancedBIMLog.Set;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;

namespace LogShape
{
    [Transaction(TransactionMode.Manual)]
    public class BIMLog : IExternalApplication
    {
        // 로그 저장할 폴더의 경로
        public string folderPath;

        // 현재 작업중인 프로젝트의 GUID
        public string creationGUID;

        // 최종 파일 추출
        //
        // 로그 최종
        public JObject jobject = [];
        // Time log 파일 이름
        public string jsonTime = "";
        // 로그 최종의 저장 경로
        public string jsonFile = "";

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
            BuiltInCategory.OST_Furniture,
        };

        // file GUID 에 따른 로그 최종 파일 추출
        //
        // file GUID에 따른 log 파일
        public Dictionary<string, JObject> fileAndJObject = [];
        // file GUID 에 따른 로그 파일의 저장 경로
        public Dictionary<string, string> fileAndPath = [];

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
                if (string.IsNullOrEmpty(folderPath)) Set.SetLogPath(); // 파일 경로 지정해주기
                if (folderPath == null) return Result.Failed;                    // 경로 생성 오류시

                application.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(DocumentChangeTracker);
                application.ControlledApplication.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(FailureTracker);
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
                application.ControlledApplication.FailuresProcessing -= new EventHandler<FailuresProcessingEventArgs>(FailureTracker);
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

        public void DocumentChangeTracker(object sender, DocumentChangedEventArgs e)
        {
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            creationGUID = doc.CreationGUID.ToString();

            jobject = fileAndJObject[creationGUID];
            jsonFile = fileAndPath[creationGUID];

            // time 별 timeLog
            List<string> timeLog_sub = timeAndList[creationGUID];
            JObject timeLog = timeAndJObject[creationGUID];

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
                        JObject addS = ExportToMeshJObject(doc, elem, eidString, timestamp, "C");
                        if (addS == null) continue; // mesh 정보를 뽑을 수 없다면 continue

                        // 여기에 정보가 들어갈 수 있도록 -> 이걸 쓰면 되지 않을까
                        JObject addF = Log(doc, "C", eid, eidString, timestamp);
                        if (addF != null)
                        {
                            addS["Info"] = addF;
                        }

                        ((JArray)jobject["ShapeLog"]).Add(addS);

                        elemList.Add(eidString);
                        stlog2.Add(eidString);
                        isChanged = true;

                        if (elem.Category.Name.ToString() == "Walls")
                        {
                            Wall wall = elem as Wall;
                            string wallVolume = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsValueString();
                            volumeCheckDict[eid.ToString()] = wallVolume;

                            string centerPoint = GetWallCenterPoint(wall);
                            locationCheckDict[eid.ToString()] = centerPoint;
                        }
                    }
                }
                catch
                {

                }
            }
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
            if (elem.Category.BuiltInCategory == BuiltInCategory.OST_Railings
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
    }
}

