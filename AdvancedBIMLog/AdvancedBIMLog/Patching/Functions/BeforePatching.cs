using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Patching.Functions
{
    internal class BeforePatching
    {
        public static void preProcessing(
            string logPath,
            DateTime time,
            JObject elementIdDict,
            out JArray selectedElemLog,
            out JArray rlog,
            out JObject newJson)
        {
            selectedElemLog = [];
            rlog = [];
            newJson = [];

            
            using (StreamReader file = File.OpenText(logPath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JArray logs = (JArray)JToken.ReadFrom(reader);
                string hostElem = "";
                string hostTime = "";
                foreach (JObject log in logs)
                {
                    log.Remove("Meshes");
                    string elementId = log["ElementId"].ToString();
                    
                    string command = log["CommandType"].ToString();

                    if ((string)log["Info"]["Common"]["ElementCategory"] == "Curtain Walls") continue;

                    if (command == "D")
                    {
                        if (newJson.ContainsKey(elementId))
                        {
                            JObject reverseDel = (JObject)newJson[elementId].DeepClone();
                            reverseDel["CommandType"] = "C";
                            reverseDel["Info"]["Common"]["Timestamp"] = log["Info"]["Common"]["Timestamp"];
                            rlog.Add(reverseDel);
                        }
                    }
                    else
                    {
                        JObject info = (JObject)log["Info"];
                        JObject common = (JObject)info["Common"];
                        JObject geometry = (JObject)info["Geometry"];
                        JObject property = (JObject)info["Property"];
                        JObject parameter = (JObject)info["Parameter"];

                        if (command == "M")
                        {
                            if (newJson.ContainsKey(elementId))
                            {
                                // 벽체가 움직임으로 인해 발생한 창문의 M은 고려 안하겠다링
                                if ((string)log["Info"]["Common"]["ElementCategory"] == "Walls")
                                {
                                    hostElem = (string)log["ElementId"];
                                    hostTime = (string)log["Info"]["Common"]["Timestamp"];
                                }
                                if ((string)log["Info"]["Common"]["ElementCategory"] == "Windows" ||
                                    (string)log["Info"]["Common"]["ElementCategory"] == "Doors")
                                {
                                    if (log["Info"]["Geometry"]["HostId"].ToString() == hostElem ||
                                        log["Info"]["Common"]["Timestamp"].ToString() == hostTime)
                                    {
                                        hostElem = "";
                                        hostTime = "";

                                        //continue;
                                    }
                                }

                                JObject modifyJson = new JObject
                                {
                                    ["ElementId"] = elementId,
                                    ["CommandType"] = command,
                                    ["Info"] = new JObject
                                    {
                                        ["Common"] = common,
                                        ["ModifiedCommon"] = new JObject(),
                                        ["ModifiedGeometry"] = new JObject(),
                                        ["ModifiedParameter"] = new JObject(),
                                        ["ModifiedProperty"] = new JObject(),
                                    }
                                };

                                if (!JToken.DeepEquals(newJson[elementId]["Info"]["Common"]["ElementFamily"], common["ElementFamily"]) ||
                                    !JToken.DeepEquals(newJson[elementId]["Info"]["Common"]["ElementType"], common["ElementType"]))
                                {
                                    modifyJson["Info"]["ModifiedCommon"]["ElementFamily"] = newJson[elementId]["Info"]["Common"]["ElementFamily"];
                                    modifyJson["Info"]["ModifiedCommon"]["ElementType"] = newJson[elementId]["Info"]["Common"]["ElementType"];
                                }

                                JObject paraJson = (JObject)newJson[elementId]["Info"]["Parameter"];
                                JObject newParameter = (JObject)parameter.DeepClone();
                                foreach (var p in paraJson.Properties())
                                {
                                    if (newParameter.ContainsKey(p.Name))
                                    {
                                        if (p.Value.ToString() != newParameter[p.Name].ToString())
                                        {
                                            modifyJson["Info"]["ModifiedParameter"][p.Name] = paraJson[p.Name];
                                        }
                                        newParameter.Remove(p.Name);
                                    }
                                }

                                JObject proJson = (JObject)newJson[elementId]["Property"];
                                if (proJson != null)
                                {
                                    foreach (var p in proJson.Properties())
                                    {
                                        if (property.ContainsKey(p.Name))
                                        {
                                            if (p.Value.ToString() != property[p.Name].ToString())
                                            {
                                                modifyJson["Info"]["ModifiedProperty"][p.Name] = proJson[p.Name];
                                            }
                                        }
                                    }
                                }

                                JObject geoJson = (JObject)newJson[elementId]["Info"]["Geometry"];
                                foreach (var prop in geoJson.Properties())
                                {
                                    string pname = prop.Name;
                                    JToken pvalue = prop.Value;

                                    if (!geometry.ContainsKey(pname))
                                    {
                                        foreach (var key in geometry.Properties())
                                        {
                                            modifyJson["Info"]["ModifiedGeometry"][key.Name] = geoJson[key.Name];
                                        }
                                        break;
                                    }

                                    if (pvalue.ToString() != geometry[pname].ToString())
                                    {
                                        foreach (var key in geometry.Properties())
                                        {
                                            modifyJson["Info"]["ModifiedGeometry"][key.Name] = geoJson[key.Name];
                                        }

                                        if ((string)common["ElementCategory"] == "Floors" ||
                                            (string)common["ElementCategory"] == "Ceilings")
                                        {
                                            modifyJson["Info"]["ModifiedCommon"]["ElementFamily"] = newJson[elementId]["Info"]["Common"]["ElementFamily"];
                                            modifyJson["Info"]["ModifiedCommon"]["ElementType"] = newJson[elementId]["Info"]["Common"]["ElementType"];

                                            modifyJson["Info"]["ModifiedParameter"] = paraJson;
                                        }

                                        break;
                                    }
                                }

                                // Layer 정보는 필요없어!!

                                if (newJson.ContainsKey(elementId))
                                {
                                    newJson[elementId] = log;
                                }
                                else
                                {
                                    newJson.Add(elementId, log);
                                }

                                rlog.Add(modifyJson);
                            }
                        }
                        else if (command == "C")
                        {
                            newJson[elementId] = log;
                            JObject reverseAdd = (JObject)log.DeepClone();
                            reverseAdd["CommandType"] = "D";
                            //reverseAdd.Remove("Info");

                            rlog.Add(reverseAdd);
                        }
                    }
                }

                foreach (JObject log in rlog)
                {
                    string elementId = (string)log["ElementId"];
                    if (elementIdDict.ContainsKey(elementId))
                    {
                        string timeStampOrigin = (string)log["Info"]["Common"]["Timestamp"];
                        string[] parts = timeStampOrigin.Split('_');
                        string dateTimeString = parts[0] + "-" + parts[1] + "-" + parts[2] + " " + parts[3] + ":" + parts[4] + ":" + parts[5];

                        DateTime dateTime = DateTime.ParseExact(dateTimeString, "yyyy-MM-dd HH:mm:ss", null);

                        if (dateTime <= time) continue;

                        selectedElemLog.Add(log);
                    }
                }
            }
        }
    }
}
