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
        private string lastScriptNpc;
        private IReadOnlyList<EventChoice> ActiveChoices => activeInfo.definition.Variant(activeInfo.companyId)?.choices ?? activeInfo.definition.getChoices();
        private IReadOnlyList<EventDialogueLine> ActiveOpening => activeInfo.definition.Variant(activeInfo.companyId)?.openingLines ?? activeInfo.definition.OpeningLines;

        private void BeginScriptedNegotiation()
        {
            scriptLines.Clear(); scriptLines.AddRange(ActiveOpening);
            scriptIndex = 0; scriptedResult = false; lastScriptNpc = activeInfo.speakerNpcId;
            PresentScriptLine();
        }

        private void PresentScriptLine()
        {
            if (activeInfo == null || scriptIndex >= scriptLines.Count) return;
            var line = scriptLines[scriptIndex];
            if (!string.IsNullOrEmpty(line.npcId)) lastScriptNpc = line.npcId;
            var registry = FindFirstObjectByType<NpcStateRegistry>();
            var profile = registry != null && registry.TryGetProfile(lastScriptNpc, out var seed) ? seed.profile : null;
            bool choosing = !scriptedResult && scriptIndex == scriptLines.Count - 1;
            negotiationView.ShowDialogue(new DialoguePresentation
            {
                speaker = line.player ? "주인공" : line.narration ? "" : profile?.displayName ?? activeInfo.speakerName,
                text = activeInfo.resolve(line.text), player = line.player, narration = line.narration,
                npcStanding = profile?.standingIllustration, npcDimmed = profile?.dimmedStandingIllustration,
                background = activeInfo.definition.getNegotiationBackground(),
                choices = choosing ? Enumerable.Range(0, negotiationChoices.Count).Select(getNegotiationChoiceText).ToList() : null,
                choiceDescriptions = choosing ? Enumerable.Range(0, negotiationChoices.Count).Select(getNegotiationChoiceDescription).ToList() : null
            }, AdvanceScriptLine, confirmChoice, SkipScriptLine, () => !choosing);
            negotiationView.SetDialogueInput(true);
        }

        private void AdvanceScriptLine()
        {
            if (activeInfo == null) return;
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
            screenState = ScreenState.CompanyChoice; scriptedResult = false; scriptIndex = 0; scriptLines.Clear();
            scriptLines.Add(new EventDialogueLine { player = true, text = playerLine });
            scriptLines.Add(new EventDialogueLine { npcId = activeInfo.speakerNpcId, text = activeInfo.definition.getCompanyQuestion() });
            PresentScriptLine();
        }

        private void ShowScriptedResult(EventOutcome outcome, string playerLine, string selectedCompany = null)
        {
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
                    scriptLines[i] = new EventDialogueLine { npcId = line.npcId, player = line.player, narration = line.narration, text = line.text.Replace("{company}", selectedCompany) };
                }
            PresentScriptLine();
        }
    }
}
