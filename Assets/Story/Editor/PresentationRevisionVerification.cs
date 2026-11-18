using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TakeOver.Events;
using TakeOver.NPC;
using TakeOver.Help;
using UnityEditor;
using UnityEngine;

public static class PresentationRevisionVerification
{
    public static void Run()
    {
        var rows = new List<string>();
        void Check(bool value, string label)
        {
            rows.Add((value ? "PASS: " : "FAIL: ") + label);
            if (!value) throw new InvalidDataException(label);
        }
        try
        {
            var portraits = new HashSet<Sprite>(AssetDatabase.FindAssets("t:NpcDefinition", new[] { "Assets/NPC/Data/Profiles" })
                .Select(g => AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(g)).CreateRuntimeSeed().profile)
                .SelectMany(p => new[] { p.standingIllustration, p.dimmedStandingIllustration }).Where(s => s != null));
            var definitions = AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/EventSystem/Data", "Assets/Story/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var definition in definitions)
            {
                Check(!portraits.Contains(definition.getCardImageFor(definition.getSpeakerNpcId())) && !portraits.Contains(definition.getImageFor(definition.getSpeakerNpcId())), "Event art is illustration, not standing: " + definition.getId());
                var variants = new SerializedObject(definition).FindProperty("companyVariants");
                var choices = definition.getChoices().ToList();
                for (int i = 0; i < variants.arraySize; i++)
                {
                    string company = variants.GetArrayElementAtIndex(i).FindPropertyRelative("companyId").stringValue;
                    choices.AddRange(definition.Variant(company).choices);
                }
                foreach (var choice in choices)
                {
                    Check(!string.IsNullOrWhiteSpace(choice.getPlayerLine()) && choice.getPlayerLine() != choice.getText(), "Caption and spoken player reply differ: " + definition.getId());
                    Check(choice.getText() != "확인" && choice.getText() != "확인한다", "No empty acknowledgement choice: " + definition.getId());
                    if (choice.ResultLines.Count > 0)
                        Check(choice.ResultLines[0].player && (choice.ResultLines[0].text == choice.getPlayerLine() || definition.getChoiceGeneration() == EventChoiceGeneration.OwnedCompanies), "Result starts with authored spoken player reply: " + definition.getId());
                }
                if (definition.IsLinearDialogue) Check(choices.Count == 1, "Linear dialogue retains exactly one internal completion result: " + definition.getId());
            }
            var plan = EarlyCampaignPlan.Load();
            foreach (var ending in plan.endings)
            {
                Check(ending.lines.Count >= 13 && ending.lines.All(l => l.narration && !l.player && string.IsNullOrEmpty(l.npcId)), "Full ending is narration without actors: " + ending.signal);
                Check(ending.lines.Select(l => l.scene).Distinct().Count() == 3 && ending.lines.All(l => !l.text.Contains("%")), "Three ending acts without progress percentages: " + ending.signal);
            }
            Check(Resources.Load<CampaignEndingVisuals>("Story/EndingVisuals").backgrounds.All(sprite => sprite != null), "All ending backgrounds are in build resources");
            Check(HelpTopics.Items.All(topic => topic.text.Length < 320 && !System.Text.RegularExpressions.Regex.IsMatch(topic.text, @"\d+일차|\d+%")), "Help stays short and has no company schedule/threshold lists");
            rows.Add("COMPLETE");
            Directory.CreateDirectory("../QA_PresentationRevision");
            File.WriteAllLines("../QA_PresentationRevision/presentation-content.txt", rows);
            CampaignIntegrationVerification.Run();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("../QA_PresentationRevision");
            File.WriteAllLines("../QA_PresentationRevision/presentation-content.txt", rows.Concat(new[] { "FAIL: " + error }));
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
