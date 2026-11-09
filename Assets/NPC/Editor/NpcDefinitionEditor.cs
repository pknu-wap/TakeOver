using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TakeOver.NPC.Editor
{
    [CustomEditor(typeof(NpcDefinition)), CanEditMultipleObjects]
    internal sealed class NpcDefinitionEditor : UnityEditor.Editor
    {
        private ReorderableList actions;
        private ReorderableList memories;
        private bool showProfile = true, showBiases, showActions = true, showMemories = true, showPreview;
        private string previewAction = "GoodFaith";
        private bool showStory;
        private NpcMemoryType previewMemory;
        private NpcRelationDelta previewDelta = new NpcRelationDelta { trust = 4, hostility = -4 };

        private void OnEnable()
        {
            var seed = serializedObject.FindProperty("seed");
            seed.FindPropertyRelative("preferenceResponse").isExpanded = true;
            actions = CreateList(seed.FindPropertyRelative("actionPreferences"), true);
            memories = CreateList(seed.FindPropertyRelative("memoryPreferences"), false);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var seed = serializedObject.FindProperty("seed");
            EditorGUILayout.LabelField("NPC 개별 설정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이 에셋의 정보만 편집합니다. 보너스 기준이 '기억만'이면 행동 ID와 관계없이 Memory 선호를 사용합니다.", MessageType.Info);
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("플레이 시작 시 SO의 독립 사본을 사용합니다. 이 에셋을 수정한 결과는 플레이를 다시 시작하면 반영됩니다.", MessageType.Info);
            Field(seed, "npcId", "NPC ID");
            Field(seed, "companyId", "회사 ID");
            Field(seed, "position", "검색용 직책");
            var profile = seed.FindPropertyRelative("profile");
            showProfile = EditorGUILayout.Foldout(showProfile, "소개 · 소속 · 이미지", true);
            if (showProfile)
            {
                Field(profile, "displayName", "이름"); Field(profile, "role", "표시 직책");
                Field(profile, "affiliation", "소속 표시명");
                Field(profile, "standingIllustration", "기본 스탠딩");
                Field(profile, "dimmedStandingIllustration", "어두운 스탠딩");
                Field(profile, "personalitySummary", "성격 · 가치관");
                Field(profile, "gameplayRoleSummary", "게임 내 역할");
            }
            showStory = EditorGUILayout.Foldout(showStory, "메인 스토리 설계 · 제작자 전용", true);
            if (showStory)
            {
                EditorGUILayout.HelpBox("협조·적대의 서사 조건입니다. 신뢰/보수 수치만으로 자동 전향하지 않으며 인물 정보창에 이 조건을 공개하지 않습니다.", MessageType.Info);
                var story = serializedObject.FindProperty("storyDesign");
                Field(story, "stance", "기본 서사 입장");
                Field(story, "personalGoal", "개인 목표"); Field(story, "loyaltyReason", "그룹/회사에 남는 이유");
                Field(story, "innerConflict", "내적 모순"); Field(story, "pressureTactics", "주인공 견제 방식");
                Field(story, "cooperationTrigger", "협조 검토 계기"); Field(story, "requestedTerms", "요구 조건과 대가");
                Field(story, "cooperationLimit", "협조의 한계"); Field(story, "informationScope", "제공 가능한 정보");
                Field(story, "refusalConditions", "거절선"); Field(story, "renewedConflict", "재대립 조건");
                Field(story, "storyArc", "초기·중반·최종 등장 흐름");
            }
            EditorGUILayout.Space();
            Field(seed, "preferenceResponse", "이 NPC의 기본 호불호 보너스");
            if (seed.FindPropertyRelative("preferenceResponse").FindPropertyRelative("source").intValue == (int)NpcPreferenceSource.MemoryOnly)
                EditorGUILayout.HelpBox("현재 이 NPC는 Memory에만 보너스를 적용합니다. 아래 행동별 분류·개별 보너스는 계산에서 제외됩니다.", MessageType.Info);
            EditorGUILayout.HelpBox("배율은 선호 시 유리한 방향, 불호 시 불리한 방향만 증폭합니다. 증가 기준: 신뢰·존중·의존도·관심 / 감소 기준: 두려움·적대감. 고정 변화량은 부호 그대로 더합니다.", MessageType.Info);
            showActions = EditorGUILayout.Foldout(showActions, "행동별 호불호 · 개별 보너스", true);
            if (showActions) DrawList(actions, seed.FindPropertyRelative("actionPreferences"));
            showMemories = EditorGUILayout.Foldout(showMemories, "기억 유형별 호불호 · 개별 보너스", true);
            if (showMemories) DrawList(memories, seed.FindPropertyRelative("memoryPreferences"));
            showBiases = EditorGUILayout.Foldout(showBiases, "사건 기본 변화에 더할 인물별 보정 (고급)", true);
            if (showBiases)
            {
                EditorGUILayout.HelpBox("호불호와 별개로 먼저 더하는 변화량입니다. 이후 호불호 배율 및 개별 고정 보너스가 적용됩니다.", MessageType.Info);
                Field(profile, "anyEventRelationDelta", "모든 사건의 추가 변화량");
                Field(profile, "eventRelationBiases", "기억 유형별 추가 변화량");
            }
            serializedObject.ApplyModifiedProperties();
            foreach (var asset in targets) NpcConfigurationDiagnostics.Show(asset, NpcConfigurationDiagnostics.Definition((NpcDefinition)asset));
            showPreview = EditorGUILayout.Foldout(showPreview, "호불호 계산 미리보기 (실제 상태 변경 없음)", true);
            if (showPreview) DrawPreview();
        }

        private static void Field(SerializedProperty parent, string name, string label)
            => EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label), true);

        private void DrawList(ReorderableList list, SerializedProperty property)
        {
            if (targets.Length == 1) list.DoLayoutList();
            else EditorGUILayout.PropertyField(property, new GUIContent("여러 NPC의 공통 목록"), true);
        }

        private ReorderableList CreateList(SerializedProperty property, bool isAction)
        {
            var list = new ReorderableList(serializedObject, property, true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect,
                isAction ? "행동 ID는 이벤트의 actionId와 동일하게 입력" : "같은 기억 유형은 한 번만 등록");
            list.elementHeightCallback = index => EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(index), true) + 8;
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 3; rect.height -= 6;
                EditorGUI.PropertyField(rect, property.GetArrayElementAtIndex(index), GUIContent.none, true);
            };
            list.onCanAddCallback = _ => isAction || property.arraySize < Enum.GetValues(typeof(NpcMemoryType)).Length;
            list.onAddCallback = _ =>
            {
                var index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                var entry = property.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("disposition").intValue = (int)NpcActionDisposition.Likes;
                entry.FindPropertyRelative("rationale").stringValue = "";
                if (isAction) entry.FindPropertyRelative("actionId").stringValue = "";
                else
                {
                    var used = new System.Collections.Generic.HashSet<int>();
                    for (var i = 0; i < index; i++) used.Add(property.GetArrayElementAtIndex(i).FindPropertyRelative("memoryType").intValue);
                    for (var i = 0; i < Enum.GetValues(typeof(NpcMemoryType)).Length; i++)
                        if (!used.Contains(i)) { entry.FindPropertyRelative("memoryType").intValue = i; break; }
                }
                var effect = entry.FindPropertyRelative("effect");
                effect.FindPropertyRelative("mode").intValue = (int)NpcPreferenceEffectMode.NpcDefault;
                effect.FindPropertyRelative("multiplier").floatValue = 1.5f;
                effect.FindPropertyRelative("affectedAxes").intValue = (int)NpcPreferenceAxes.Default;
                foreach (var field in NpcEditorLabels.Fields) effect.FindPropertyRelative("relationBonus").FindPropertyRelative(field).floatValue = 0;
                entry.isExpanded = true;
                list.index = index;
            };
            return list;
        }

        private void DrawPreview()
        {
            if (targets.Length != 1) { EditorGUILayout.HelpBox("미리보기는 NPC 에셋 하나를 선택하세요.", MessageType.Info); return; }
            previewAction = EditorGUILayout.TextField("행동 ID (없으면 비워두기)", previewAction);
            previewMemory = (NpcMemoryType)EditorGUILayout.EnumPopup("결과 기억 유형", previewMemory);
            previewDelta.trust = EditorGUILayout.FloatField("입력 신뢰", previewDelta.trust);
            previewDelta.respect = EditorGUILayout.FloatField("입력 존중", previewDelta.respect);
            previewDelta.fear = EditorGUILayout.FloatField("입력 두려움", previewDelta.fear);
            previewDelta.hostility = EditorGUILayout.FloatField("입력 적대감", previewDelta.hostility);
            previewDelta.dependency = EditorGUILayout.FloatField("입력 의존도", previewDelta.dependency);
            previewDelta.interest = EditorGUILayout.FloatField("입력 관심", previewDelta.interest);
            var definition = (NpcDefinition)target;
            if (NpcConfigurationDiagnostics.Definition(definition).Count != 0)
            { EditorGUILayout.HelpBox("위의 설정 오류를 먼저 수정하세요.", MessageType.Warning); return; }
            try
            {
                var result = definition.PreviewPreference(previewAction, previewMemory, previewDelta);
                var r = result.relationDelta;
                EditorGUILayout.HelpBox($"{NpcEditorLabels.Dispositions[(int)result.disposition]} · "
                    + (result.usedOverride ? "항목 개별 설정" : "NPC 기본 설정")
                    + (result.enabled ? " · 보너스 활성" : " · 보너스 없음")
                    + $" · 배율 ×{result.multiplier:0.##}\n최종 신뢰 {r.trust:0.##}, 존중 {r.respect:0.##}, 두려움 {r.fear:0.##}"
                    + $", 적대감 {r.hostility:0.##}, 의존도 {r.dependency:0.##}, 관심 {r.interest:0.##}", MessageType.Info);
                EditorGUILayout.LabelField("프로필 고정 보정·관계 상한/하한은 이 미리보기에 포함하지 않습니다.", EditorStyles.wordWrappedMiniLabel);
            }
            catch (ArgumentException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }
    }
}
