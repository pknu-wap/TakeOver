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
            var startups = Object.FindFirstObjectByType<TakeOver.Startups.StartupSystem>();
            if (startups != null)
            {
                var field = typeof(TakeOver.Startups.StartupSystem).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var original = (TakeOver.Startups.StartupSettings)field.GetValue(startups);
                if (original != null)
                {
                    var fixture = Object.Instantiate(original); fixture.hideFlags = HideFlags.DontSave;
                    fixture.randomSeed = 90210; field.SetValue(startups, fixture);
                }
            }
        }
        internal static string Capture()
        {
            var registry=Object.FindFirstObjectByType<NpcStateRegistry>();
            var trading=Object.FindFirstObjectByType<TradingSaveAdapter>();
            var ap=Object.FindFirstObjectByType<ActionPointService>();
            var clock=Object.FindFirstObjectByType<GameTurnClock>();
            var playerMemories=Object.FindFirstObjectByType<TakeOver.PlayerMemories.PlayerMemoryService>();
            var npcs=string.Join("\n",registry.States.OrderBy(s=>s.npcId).Select(s=>JsonUtility.ToJson(s)));
            // Only per-playthrough occurrence GUIDs vary; NPC IDs, choices, effects and decay must match.
            npcs=Regex.Replace(npcs,"intro-[a-f0-9]{32}-","intro-INSTANCE-");
            return "turn="+clock.CurrentTurn+";AP="+ap.Current+"\n"+trading.captureJson()+"\n"+npcs+"\nplayer-memories="+playerMemories.captureJson();
        }
    }
}
