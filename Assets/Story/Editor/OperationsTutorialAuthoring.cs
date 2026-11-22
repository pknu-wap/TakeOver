using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TakeOver.Story.Editor
{
    public static class OperationsTutorialAuthoring
    {
        [Serializable] private class Draft { public List<StoryBeat> beats; }
        [Serializable] private class BeatPath { public string backgroundPath; }
        [Serializable] private class Paths { public List<BeatPath> beats; }

        [MenuItem("TakeOver/Story/Sync Intro Script Only")]
        public static void Sync()
        {
            string json = File.ReadAllText("Assets/Story/Editor/AuthoringData.json");
            var beats = JsonUtility.FromJson<Draft>(json).beats;
            var paths = JsonUtility.FromJson<Paths>(json).beats;
            for (int i = 0; i < beats.Count; i++)
                beats[i].background = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i].backgroundPath);
            if (beats.Any(beat => beat.background == null || string.IsNullOrWhiteSpace(beat.text)))
                throw new InvalidOperationException("Missing intro script or background.");
            var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>("Assets/Story/Data/IntroSequence.asset");
            sequence.beats = beats;
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            Debug.Log("Synced intro script: " + beats.Count + " beats.");
        }
    }
}
