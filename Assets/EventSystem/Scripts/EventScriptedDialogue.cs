using System.Collections.Generic;
using System.Linq;
using TakeOver.NPC;
using UnityEngine;

namespace TakeOver.Events
{
    public sealed partial class EventSystemController
    {
        private readonly List<EventDialogueLine> scriptLines = new List<EventDialogueLine>();
        private int scriptIndex;
        private bool scriptedResult;
        private string scriptStartNpc;
        private IReadOnlyList<EventChoice> ActiveChoices => activeInfo.definition.Variant(activeInfo.companyId)?.choices ?? activeInfo.definition.getChoices();
        private IReadOnlyList<EventDialogueLine> ActiveOpening => activeInfo.definition.Variant(activeInfo.companyId)?.openingLines ?? activeInfo.definition.OpeningLines;

        private void BeginScriptedNegotiation()
        {
            scriptLines.Clear(); scriptLines.AddRange(ActiveOpening);
            scriptIndex = 0; scriptedResult = false; scriptStartNpc = null;
            PresentScriptLine();
        }

        private void PresentScriptLine()
        {
            if (activeInfo == null || scriptIndex >= scriptLines.Count) return;
            var line = scriptLines[scriptIndex];
            var registry = FindFirstObjectByType<NpcStateRegistry>();
            string npcId = ScriptNpcAt(scriptIndex);
            NpcStateRegistry.NpcSeed seed = null;
            var profile = registry != null && registry.TryGetProfile(npcId, out seed) ? seed.profile : null;
            bool choosing = !scriptedResult && scriptIndex == scriptLines.Count - 1 && !activeInfo.definition.IsLinearDialogue;
            negotiationView.ShowDialogue(new DialoguePresentation
            {
                speaker = line.player ? EventNegotiationView.ProtagonistName() : line.narration ? ""
                    : npcId == activeInfo.speakerNpcId ? activeInfo.speakerLabel
                    : profile != null ? seed.GetSpeakerLabel() : activeInfo.speakerLabel,
                text = activeInfo.resolve(line.text), player = line.player, narration = line.narration,
                npcStanding = profile?.standingIllustration ?? (profile != null ? NpcInfoPanel.FallbackStanding : null),
                npcDimmed = profile?.dimmedStandingIllustration,
                background = activeInfo.definition.getNegotiationBackground(),
                choices = choosing ? Enumerable.Range(0, negotiationChoices.Count).Select(getNegotiationChoiceText).ToList() : null,
                choiceDescriptions = choosing ? Enumerable.Range(0, negotiationChoices.Count).Select(getNegotiationChoiceDescription).ToList() : null
            }, AdvanceScriptLine, confirmChoice, SkipScriptLine,
                () => activeInfo != null && !choosing && (!activeInfo.definition.IsLinearDialogue || scriptedResult || scriptIndex < scriptLines.Count - 1),
                fadeSkip: true);
            negotiationView.SetDialogueInput(true);
        }

        private string ScriptNpcAt(int index)
        {
            if (index < 0 || index >= scriptLines.Count) return scriptStartNpc;
            int scene = scriptLines[index].scene;
            for (int i = index; i >= 0 && scriptLines[i].scene == scene; i--)
            {
                var line = scriptLines[i];
                if (!line.player && !line.narration)
                    return string.IsNullOrEmpty(line.npcId) ? activeInfo.speakerNpcId : line.npcId;
            }
            return scene == scriptLines[0].scene ? scriptStartNpc : null;
        }

        private void AdvanceScriptLine()
        {
            if (activeInfo == null) return;
            if (!scriptedResult && scriptIndex == scriptLines.Count - 1 && activeInfo.definition.IsLinearDialogue)
            { confirmChoice(0); return; }
            if (++scriptIndex < scriptLines.Count) PresentScriptLine();
            else if (scriptedResult) closeResult();
        }

        private void SkipScriptLine()
        {
            if (activeInfo == null) return;
            if (scriptedResult) { closeResult(); return; }
            scriptIndex = scriptLines.Count - 1;
            PresentScriptLine();
        }

        private void ShowScriptedCompanyChoices(string playerLine)
        {
            buildNegotiationChoices(true);
            if (negotiationChoices.Count == 0) { screenState = ScreenState.Result; closeResult(); return; }
            scriptStartNpc = ScriptNpcAt(scriptIndex) ?? activeInfo.speakerNpcId;
            screenState = ScreenState.CompanyChoice; scriptedResult = false; scriptIndex = 0; scriptLines.Clear();
            scriptLines.Add(new EventDialogueLine { player = true, text = playerLine });
            scriptLines.Add(new EventDialogueLine { npcId = activeInfo.speakerNpcId, text = activeInfo.definition.getCompanyQuestion() });
            PresentScriptLine();
        }

        private void ShowScriptedResult(EventOutcome outcome, string playerLine, string selectedCompany = null)
        {
            scriptStartNpc = ScriptNpcAt(scriptIndex) ?? activeInfo.speakerNpcId;
            scriptedResult = true; scriptIndex = 0; scriptLines.Clear();
            if (outcome.ResultLines.Count > 0) scriptLines.AddRange(outcome.ResultLines);
            else
            {
                scriptLines.Add(new EventDialogueLine { player = true, text = playerLine });
                scriptLines.Add(new EventDialogueLine { npcId = activeInfo.speakerNpcId, text = outcome.getResultDialogue() });
            }
            if (!string.IsNullOrEmpty(selectedCompany))
                for (int i = 0; i < scriptLines.Count; i++)
                {
                    var line = scriptLines[i];
                    scriptLines[i] = new EventDialogueLine { npcId = line.npcId, player = line.player, narration = line.narration, scene = line.scene, text = line.text.Replace("{company}", selectedCompany) };
                }
            PresentScriptLine();
        }
    }
}
