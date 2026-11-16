using System;
using System.IO;
using System.Linq;
using TakeOver.NPC;
using UnityEditor;
using UnityEngine;

public static class NpcStandingVerification
{
    public static void Run()
    {
        var path = Path.GetFullPath("../tmp/mob-art-check/standing-results.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "");
        try
        {
            void Check(bool ok, string text)
            {
                File.AppendAllText(path, (ok ? "PASS: " : "FAIL: ") + text + "\n");
                if (!ok) throw new Exception(text);
            }
            foreach (NpcMobAppearance appearance in Enum.GetValues(typeof(NpcMobAppearance)))
            {
                var seed = new NpcStateRegistry.NpcSeed { companyId = "haneul-food" };
                seed.profile.mobAppearance = appearance;
                NpcStandingCatalog.ApplyFallback(seed);
                Check(seed.profile.standingIllustration != null && seed.profile.dimmedStandingIllustration != null
                    && seed.profile.standingIllustration != seed.profile.dimmedStandingIllustration, appearance + " normal and supplied dim pair imported and selected");
                var named = Resources.Load<Sprite>("NPC/MobStanding/Mob_Male_Standing");
                seed.profile.standingIllustration = named;
                seed.profile.dimmedStandingIllustration = null;
                NpcStandingCatalog.ApplyFallback(seed);
                Check(seed.profile.standingIllustration == named && seed.profile.dimmedStandingIllustration == null, "Dedicated standing never mixed with unrelated dim art: " + appearance);
            }
            foreach (var company in new[] { "polaris-finance", "daeseong-industry", "sinseong-development", "blueharbor-retail", "aurora-foodtech", "apex", "APEX", "apex-holdings", "external-network", "" })
            {
                var seed = new NpcStateRegistry.NpcSeed { companyId = company };
                NpcStandingCatalog.ApplyFallback(seed);
                Check(seed.profile.standingIllustration == null && seed.profile.dimmedStandingIllustration == null, "No mob for reserved/external company: " + company);
            }
            var pending = new NpcStateRegistry.NpcSeed { companyId = "future-company" };
            pending.profile.affiliation = "APEX 내부인원";
            Check(!NpcStandingCatalog.CanUseMob(pending), "APEX affiliation reserved before canonical company ID exists");
            var registry = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NPC/Prefabs/NpcLogic.prefab").GetComponent<NpcStateRegistry>();
            var definitions = new SerializedObject(registry).FindProperty("npcDefinitions");
            Check(definitions.arraySize == 30, "All 30 registered profiles retained");
            var namedCount = 0; var reservedCount = 0;
            for (int i = 0; i < definitions.arraySize; i++)
            {
                var definition = (NpcDefinition)definitions.GetArrayElementAtIndex(i).objectReferenceValue;
                var source = new SerializedObject(definition).FindProperty("seed").FindPropertyRelative("profile");
                var seed = definition.CreateRuntimeSeed();
                var original = source.FindPropertyRelative("standingIllustration").objectReferenceValue;
                if (original != null) { namedCount++; Check(seed.profile.standingIllustration == original, "Named art retained: " + seed.npcId); }
                else { reservedCount++; Check(!NpcStandingCatalog.CanUseMob(seed) && seed.profile.standingIllustration == null, "Pending story art retained: " + seed.npcId); }
            }
            Check(namedCount == 14 && reservedCount == 16, "14 named illustrations and 16 reserved story characters unchanged");
            File.AppendAllText(path, "COMPLETE\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.AppendAllText(path, error + "\n"); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
