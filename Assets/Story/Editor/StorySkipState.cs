using System.Linq;
using System.Text.RegularExpressions;
using TakeOver.NPC;
using TakeOver.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeOver.Story.Editor
{
    internal static class StorySkipState
    {
        internal static void Seed(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="Game")return;
            FixedDaySeed.changeSeed(90210);
            Random.InitState(90210);
        }
        internal static string Capture()
        {
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            var trading=Object.FindFirstObjectByType<TradingSaveAdapter>();
            var ap=Object.FindFirstObjectByType<ActionPointService>();
            var clock=Object.FindFirstObjectByType<GameTurnClock>();
            var npcs=string.Join("\n",registry.States.OrderBy(s=>s.npcId).Select(s=>JsonUtility.ToJson(s)));
            // Only per-playthrough occurrence GUIDs vary; NPC IDs, choices, effects and decay must match.
            npcs=Regex.Replace(npcs,"intro-[a-f0-9]{32}-","intro-INSTANCE-");
            return "turn="+clock.CurrentTurn+";AP="+ap.Current+"\n"+trading.captureJson()+"\n"+npcs;
        }
    }
}
