using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

// Small synthetic saves are source-controlled; root QA outputs are disposable.
public static class CampaignTestFixtures
{
    public static string PathFor(string name) => Path.Combine("Tools/StoryExpansion/Fixtures", name);
    public static void InstallCompletedSave(string saveDirectory)
    {
        Directory.CreateDirectory(saveDirectory);
        File.Copy(PathFor("completed-v6-progress.json"), Path.Combine(saveDirectory, "progress.json"), true);
        InstallCheckpoint("early-b2-start.json", "early-b2", saveDirectory);
    }
    public static void InstallCheckpoint(string fixture, string mission, string saveDirectory)
    {
        var data = JObject.Parse(File.ReadAllText(PathFor(fixture)));
        string run = (string)data["sections"].Single(s => (string)s["id"] == "events")["data"]["campaignRunId"];
        if (!Regex.IsMatch(run ?? "", "^[a-f0-9]{32}$") || !Regex.IsMatch(mission, "^[a-z0-9-]+$")) throw new InvalidDataException("Invalid synthetic fixture identifiers");
        string root = Path.GetFullPath(saveDirectory);
        string target = Path.GetFullPath(Path.Combine(root, "checkpoints", run, mission, "progress.json"));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Fixture target outside isolated save root");
        Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(PathFor(fixture), target, true);
    }
}
