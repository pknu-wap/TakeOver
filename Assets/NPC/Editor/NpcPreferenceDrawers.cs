using UnityEditor;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    internal static class NpcEditorLabels
    {
        internal static readonly string[] Dispositions = { "미지정 (기억 선호 사용)", "선호", "불호" };
        internal static readonly string[] Modes = { "이 NPC의 기본값 사용", "이 항목만 개별 설정", "이 항목은 보너스 없음" };
        internal static readonly string[] Sources = { "행동 우선 · 기억 대체", "기억 (Memory)만" };
        internal static readonly string[] Axes = { "신뢰", "존중", "두려움", "적대감", "의존도", "관심" };
        internal static readonly string[] Fields = { "trust", "respect", "fear", "hostility", "dependency", "interest" };
        internal const float Gap = 3f;
        internal static float Line => EditorGUIUtility.singleLineHeight;
        internal static Rect Next(ref Rect area, float height = -1)
        {
            var line = new Rect(area.x, area.y, area.width, height < 0 ? Line : height);
            area.y += line.height + Gap;
            return line;
        }
        internal static void Property(ref Rect area, SerializedProperty parent, string field, string label)
        {
            var property = parent.FindPropertyRelative(field);
            EditorGUI.PropertyField(Next(ref area, EditorGUI.GetPropertyHeight(property, true)), property, new GUIContent(label), true);
        }
        internal static void AxesField(ref Rect area, SerializedProperty axes)
            => Choice(ref area, axes, "배율 적용 항목", Axes, true);

        internal static void Choice(ref Rect area, SerializedProperty property, string label, string[] names, bool mask = false)
        {
            EditorGUI.BeginProperty(new Rect(area.x, area.y, area.width, Line), new GUIContent(label), property);
            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var value = mask ? EditorGUI.MaskField(Next(ref area), label, property.intValue, names)
                : EditorGUI.Popup(Next(ref area), label, property.intValue, names);
            if (EditorGUI.EndChangeCheck()) property.intValue = mask ? value & (int)NpcPreferenceAxes.All : value;
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(NpcRelationDelta))]
    internal sealed class NpcRelationDeltaDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => property.isExpanded ? (NpcEditorLabels.Line + NpcEditorLabels.Gap) * 4 : NpcEditorLabels.Line;

        public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(area, label, property);
            property.isExpanded = EditorGUI.Foldout(NpcEditorLabels.Next(ref area), property.isExpanded, label, true);
            if (property.isExpanded)
            {
                var oldWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 68f;
                for (var row = 0; row < 3; row++)
                {
                    var line = NpcEditorLabels.Next(ref area);
                    var width = (line.width - 12) / 2;
                    for (var column = 0; column < 2; column++)
                    {
                        var i = row * 2 + column;
                        EditorGUI.PropertyField(new Rect(line.x + column * (width + 12), line.y, width, line.height),
                            property.FindPropertyRelative(NpcEditorLabels.Fields[i]), new GUIContent(NpcEditorLabels.Axes[i]));
                    }
                }
                EditorGUIUtility.labelWidth = oldWidth;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(NpcPreferenceResponse))]
    internal sealed class NpcPreferenceResponseDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return NpcEditorLabels.Line;
            var lines = 4;
            if (property.FindPropertyRelative("enabled").boolValue)
                lines += 3 + (property.FindPropertyRelative("useCustomAxes").boolValue ? 1 : 0);
            return lines * (NpcEditorLabels.Line + NpcEditorLabels.Gap) + 24;
        }

        public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(area, label, property);
            property.isExpanded = EditorGUI.Foldout(NpcEditorLabels.Next(ref area), property.isExpanded, label, true);
            if (property.isExpanded)
            {
                NpcEditorLabels.Choice(ref area, property.FindPropertyRelative("source"), "보너스 기준", NpcEditorLabels.Sources);
                NpcEditorLabels.Property(ref area, property, "enabled", "NPC 기본 보너스 사용");
                if (property.FindPropertyRelative("enabled").boolValue)
                {
                    NpcEditorLabels.Property(ref area, property, "likedMultiplier", "선호 배율");
                    NpcEditorLabels.Property(ref area, property, "dislikedMultiplier", "불호 배율");
                    NpcEditorLabels.Property(ref area, property, "useCustomAxes", "적용 항목 직접 지정");
                    if (property.FindPropertyRelative("useCustomAxes").boolValue)
                        NpcEditorLabels.AxesField(ref area, property.FindPropertyRelative("affectedAxes"));
                }
                EditorGUI.HelpBox(NpcEditorLabels.Next(ref area, 42),
                    "이 SO에만 적용됩니다. 기본 보너스를 꺼도 항목별 '개별 설정'은 적용됩니다.", MessageType.Info);
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(NpcPreferenceEffect))]
    internal sealed class NpcPreferenceEffectDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = NpcEditorLabels.Line;
            if (property.FindPropertyRelative("mode").intValue == (int)NpcPreferenceEffectMode.Custom)
                height += 2 * (NpcEditorLabels.Line + NpcEditorLabels.Gap)
                    + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("relationBonus"), true) + NpcEditorLabels.Gap;
            return height;
        }

        public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(area, label, property);
            var mode = property.FindPropertyRelative("mode");
            var previousMode = mode.intValue;
            NpcEditorLabels.Choice(ref area, mode, "보너스 방식", NpcEditorLabels.Modes);
            if (previousMode != mode.intValue && mode.intValue == (int)NpcPreferenceEffectMode.Custom
                && property.FindPropertyRelative("multiplier").floatValue < 1)
            {
                property.FindPropertyRelative("multiplier").floatValue = 1.5f;
                property.FindPropertyRelative("affectedAxes").intValue = (int)NpcPreferenceAxes.Default;
            }
            if (mode.intValue == (int)NpcPreferenceEffectMode.Custom)
            {
                NpcEditorLabels.Property(ref area, property, "multiplier", "개별 배율 (1=증폭 없음)");
                NpcEditorLabels.AxesField(ref area, property.FindPropertyRelative("affectedAxes"));
                NpcEditorLabels.Property(ref area, property, "relationBonus", "배율 적용 후 더할 고정 변화량");
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(NpcActionPreference))]
    [CustomPropertyDrawer(typeof(NpcMemoryPreference))]
    internal sealed class NpcPreferenceEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return NpcEditorLabels.Line;
            return 3 * (NpcEditorLabels.Line + NpcEditorLabels.Gap)
                + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("rationale"), true) + NpcEditorLabels.Gap
                + (property.FindPropertyRelative("disposition").intValue == 0 ? 0
                    : EditorGUI.GetPropertyHeight(property.FindPropertyRelative("effect"), true) + NpcEditorLabels.Gap);
        }

        public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(area, label, property);
            var action = property.FindPropertyRelative("actionId");
            var disposition = property.FindPropertyRelative("disposition");
            var heading = action != null ? action.stringValue : property.FindPropertyRelative("memoryType").enumDisplayNames[
                Mathf.Clamp(property.FindPropertyRelative("memoryType").enumValueIndex, 0, 8)];
            var direction = disposition.intValue >= 0 && disposition.intValue < 3
                ? NpcEditorLabels.Dispositions[disposition.intValue] : "잘못된 분류";
            property.isExpanded = EditorGUI.Foldout(NpcEditorLabels.Next(ref area), property.isExpanded,
                string.IsNullOrWhiteSpace(heading) ? "새 호불호 항목" : heading + " · " + direction, true);
            if (property.isExpanded)
            {
                NpcEditorLabels.Property(ref area, property, action != null ? "actionId" : "memoryType",
                    action != null ? "행동 ID (이벤트와 동일)" : "기억 유형");
                NpcEditorLabels.Choice(ref area, disposition, "호불호", NpcEditorLabels.Dispositions);
                NpcEditorLabels.Property(ref area, property, "rationale", "이유 / 제작 메모");
                if (disposition.intValue != 0) NpcEditorLabels.Property(ref area, property, "effect", "보너스");
            }
            EditorGUI.EndProperty();
        }
    }
}
