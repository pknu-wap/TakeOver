using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TakeOver.SaveSystem
{
    /// <summary>저장 파일 읽기·쓰기 담당. 오류는 호출자에게 전달</summary>
    public class SaveFileStorage
    {
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        public string savePath { get; }

        public SaveFileStorage(string directoryPath)
        {
            savePath = Path.Combine(directoryPath, "progress.json");
        }

        public bool exists()
        {
            return File.Exists(savePath);
        }

        public string read()
        {
            JObject data = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(savePath), jsonSettings);
            foreach (JObject section in data["sections"])
            {
                // 파일의 데이터를 파트에 전달할 JSON 문자열로 변환
                JToken payload = section["data"];
                if (payload != null)
                {
                    section["json"] = payload.ToString(Formatting.None);
                    section.Remove("data");
                }
            }
            return data.ToString(Formatting.None);
        }

        public void write(string json)
        {
            JObject data = JsonConvert.DeserializeObject<JObject>(json, jsonSettings);
            foreach (JObject section in data["sections"])
            {
                // 파트의 JSON 문자열을 파일의 실제 데이터로 변환
                string payload = section["json"].Value<string>();
                section["data"] = JsonConvert.DeserializeObject<JToken>(payload, jsonSettings);
                section.Remove("json");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            string temporaryPath = savePath + ".tmp";
            File.WriteAllText(temporaryPath, data.ToString(Formatting.Indented));
            if (File.Exists(savePath))
            {
                File.Replace(temporaryPath, savePath, null);
            }
            else
            {
                File.Move(temporaryPath, savePath);
            }
        }
    }
}
