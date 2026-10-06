using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    /// <summary>Inspector에서 설정을 읽어 진단만 한다. 에셋·프리팹·런타임 상태는 변경하지 않는다.</summary>
    internal static class NpcConfigurationDiagnostics
    {
        internal static void Show(UnityEngine.Object asset, List<string> errors)
        {
            foreach (var error in errors)
                EditorGUILayout.HelpBox($"{asset.name}: {error}", MessageType.Error);
        }

        internal static string Id(NpcDefinition definition) => definition == null ? null
            : new SerializedObject(definition).FindProperty("seed").FindPropertyRelative("npcId").stringValue;

        internal static List<string> Definition(NpcDefinition definition)
        {
            var errors = new List<string>();
            var seed = new SerializedObject(definition).FindProperty("seed");
            if (string.IsNullOrWhiteSpace(seed.FindPropertyRelative("npcId").stringValue))
                errors.Add("Npc Id를 입력하세요. 빈 양식은 레지스트리에 등록하지 않습니다.");
            var profile = seed.FindPropertyRelative("profile");
            Delta(profile.FindPropertyRelative("anyEventRelationDelta"), errors, "모든 사건 관계 보정");
            var biases = profile.FindPropertyRelative("eventRelationBiases");
            for (var i = 0; i < biases.arraySize; i++)
            {
                var bias = biases.GetArrayElementAtIndex(i);
                if (!Enum.IsDefined(typeof(NpcMemoryType), bias.FindPropertyRelative("memoryType").intValue))
                    errors.Add($"관계 보정 {i}: 기억 유형이 올바르지 않습니다.");
                Delta(bias.FindPropertyRelative("relationDelta"), errors, $"관계 보정 {i}");
            }
            var preferences = seed.FindPropertyRelative("actionPreferences");
            var actions = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < preferences.arraySize; i++)
            {
                var preference = preferences.GetArrayElementAtIndex(i);
                var action = preference.FindPropertyRelative("actionId").stringValue;
                if (string.IsNullOrWhiteSpace(action) || !actions.Add(action))
                    errors.Add($"선호 {i}: 행동 ID가 비어 있거나 중복됐습니다.");
                if (!Enum.IsDefined(typeof(NpcActionDisposition), preference.FindPropertyRelative("disposition").intValue))
                    errors.Add($"선호 {i}: 선호 분류가 올바르지 않습니다.");
            }
            return errors;
        }

        internal static Tuple<string, int, string> Key(NpcChoiceReactionDefinition reaction)
        {
            var serialized = new SerializedObject(reaction);
            return Tuple.Create(serialized.FindProperty("sourceEventId").stringValue,
                serialized.FindProperty("choiceIndex").intValue, serialized.FindProperty("resultValue").stringValue);
        }

        internal static List<string> Reaction(NpcChoiceReactionDefinition reaction)
        {
            var errors = new List<string>();
            var serialized = new SerializedObject(reaction);
            var key = Key(reaction);
            if (string.IsNullOrWhiteSpace(key.Item1)) errors.Add("Source Event Id를 입력하세요.");
            if (key.Item2 < 0) errors.Add("Choice Index는 0 이상이어야 합니다.");
            // 빈 결과값도 기존 매칭 계약상 허용한다. 임의의 필수값이나 기획 제한을 추가하지 않는다.
            var payload = serialized.FindProperty("npcEvent");
            EventValues(payload, errors, "공통 결과");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var targets = payload.FindPropertyRelative("targetNpcIds");
            if (targets.arraySize == 0) errors.Add("대상 NPC ID를 하나 이상 입력하세요.");
            for (var i = 0; i < targets.arraySize; i++)
            {
                var id = targets.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add($"대상 {i}: NPC ID가 비어 있거나 중복됐습니다.");
            }
            var perTarget = payload.FindPropertyRelative("targetReactions");
            var reactionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < perTarget.arraySize; i++)
            {
                var item = perTarget.GetArrayElementAtIndex(i);
                var id = item.FindPropertyRelative("targetNpcId").stringValue;
                if (string.IsNullOrWhiteSpace(id) || !ids.Contains(id) || !reactionIds.Add(id))
                    errors.Add($"개별 반응 {i}: 대상 목록에 있는 고유 NPC ID가 필요합니다.");
                EventValues(item, errors, $"개별 반응 {i}");
            }
            return errors;
        }

        internal static HashSet<string> RegistryIds(NpcStateRegistry registry, List<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var serialized = new SerializedObject(registry);
            var definitions = serialized.FindProperty("npcDefinitions");
            for (var i = 0; i < definitions.arraySize; i++)
            {
                var definition = definitions.GetArrayElementAtIndex(i).objectReferenceValue as NpcDefinition;
                if (definition == null) { errors.Add($"Npc Definitions {i}: 에셋이 비어 있습니다."); continue; }
                var id = Id(definition);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add($"Npc Definitions {i}: NPC ID가 비어 있거나 중복됐습니다.");
                foreach (var error in Definition(definition)) errors.Add($"{definition.name}: {error}");
            }
            // 구형 인라인 데이터와 SO의 같은 ID는 오류가 아니라 SO 우선 호환 규칙이다.
            var inline = serialized.FindProperty("initialNpcs");
            var inlineIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < inline.arraySize; i++)
            {
                var id = inline.GetArrayElementAtIndex(i).FindPropertyRelative("npcId").stringValue;
                if (string.IsNullOrWhiteSpace(id) || !inlineIds.Add(id)) errors.Add($"Initial Npcs {i}: NPC ID가 비어 있거나 중복됐습니다.");
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            if (definitions.arraySize == 0 && inline.arraySize == 0)
                foreach (var seed in NpcStateRegistry.CreateDefaultNpcRoster()) ids.Add(seed.npcId);
            return ids;
        }

        internal static List<string> Receiver(NpcEventResultReceiver receiver)
        {
            var errors = new List<string>();
            var serialized = new SerializedObject(receiver);
            var registry = serialized.FindProperty("registry").objectReferenceValue as NpcStateRegistry;
            if (registry == null) registry = receiver.GetComponent<NpcStateRegistry>();
            var channel = serialized.FindProperty("eventChannel").objectReferenceValue as NpcEventChannel;
            if (channel == null) channel = receiver.GetComponent<NpcEventChannel>();
            if (channel == null) errors.Add("NPC 이벤트 채널이 필요합니다.");
            var ids = registry == null ? null : RegistryIds(registry, errors);
            if (registry == null) errors.Add("NPC 레지스트리가 필요합니다.");
            var reactions = serialized.FindProperty("reactions");
            var keys = new HashSet<Tuple<string, int, string>>();
            for (var i = 0; i < reactions.arraySize; i++)
            {
                var reaction = reactions.GetArrayElementAtIndex(i).objectReferenceValue as NpcChoiceReactionDefinition;
                if (reaction == null) { errors.Add($"Reactions {i}: 에셋이 비어 있습니다."); continue; }
                if (!keys.Add(Key(reaction))) errors.Add($"Reactions {i}: 같은 이벤트 결과 조건이 중복됐습니다.");
                foreach (var error in Reaction(reaction)) errors.Add($"{reaction.name}: {error}");
                var targets = new SerializedObject(reaction).FindProperty("npcEvent").FindPropertyRelative("targetNpcIds");
                for (var j = 0; ids != null && j < targets.arraySize; j++)
                {
                    var id = targets.GetArrayElementAtIndex(j).stringValue;
                    if (!ids.Contains(id)) errors.Add($"{reaction.name}: 등록되지 않은 대상 NPC '{id}'입니다.");
                }
            }
            return errors;
        }

        private static void EventValues(SerializedProperty item, List<string> errors, string label)
        {
            if (!Enum.IsDefined(typeof(NpcMemoryType), item.FindPropertyRelative("memoryType").intValue)
                || !Enum.IsDefined(typeof(NpcPublicity), item.FindPropertyRelative("publicity").intValue))
                errors.Add($"{label}: 기억 유형 또는 공개 범위가 올바르지 않습니다.");
            var reliability = item.FindPropertyRelative("reliability").floatValue;
            if (float.IsNaN(reliability) || float.IsInfinity(reliability)) errors.Add($"{label}: 신뢰도가 유한한 수가 아닙니다.");
            Delta(item.FindPropertyRelative("relationDelta"), errors, label);
        }

        private static void Delta(SerializedProperty delta, List<string> errors, string label)
        {
            foreach (var field in new[] { "trust", "respect", "fear", "hostility", "dependency", "interest" })
            {
                var value = delta.FindPropertyRelative(field).floatValue;
                if (float.IsNaN(value) || float.IsInfinity(value)) errors.Add($"{label}: {field}에 NaN 또는 무한대가 있습니다.");
            }
        }
    }

    [CustomEditor(typeof(NpcDefinition)), CanEditMultipleObjects]
    internal sealed class NpcDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var asset in targets) NpcConfigurationDiagnostics.Show(asset, NpcConfigurationDiagnostics.Definition((NpcDefinition)asset));
        }
    }

    [CustomEditor(typeof(NpcChoiceReactionDefinition)), CanEditMultipleObjects]
    internal sealed class NpcChoiceReactionDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Npc Event의 Event Id는 수신 시 발생 ID로 교체됩니다.", MessageType.Info);
            foreach (var asset in targets) NpcConfigurationDiagnostics.Show(asset, NpcConfigurationDiagnostics.Reaction((NpcChoiceReactionDefinition)asset));
        }
    }

    [CustomEditor(typeof(NpcStateRegistry)), CanEditMultipleObjects]
    internal sealed class NpcStateRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var asset in targets)
            {
                var errors = new List<string>();
                NpcConfigurationDiagnostics.RegistryIds((NpcStateRegistry)asset, errors);
                NpcConfigurationDiagnostics.Show(asset, errors);
            }
        }
    }

    [CustomEditor(typeof(NpcEventResultReceiver)), CanEditMultipleObjects]
    internal sealed class NpcEventResultReceiverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("반환값 true는 채널 발행 완료입니다. 실제 Memory 추가나 중복 여부를 뜻하지 않습니다.", MessageType.Info);
            foreach (var asset in targets) NpcConfigurationDiagnostics.Show(asset, NpcConfigurationDiagnostics.Receiver((NpcEventResultReceiver)asset));
        }
    }
}
