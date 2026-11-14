using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TakeOver.NPC
{
    public class NpcProfilePanelView : MonoBehaviour
    {
        [Serializable]
        public struct NpcSpriteOverride
        {
            public string npcNameOrId;
            public Sprite sprite;
            public float portraitOffsetY;
        }

        [Header("Data Source")]
        [SerializeField] private NpcStateRegistry registry;
        [SerializeField] private List<Sprite> standingSprites = new List<Sprite>();
        [SerializeField] private List<NpcSpriteOverride> manualSpriteOverrides = new List<NpcSpriteOverride>();

        [Header("Left NPC List")]
        [SerializeField] private Transform npcListContent;
        [SerializeField] private NpcListItemView npcListItemPrefab;
        [SerializeField] private ScrollRect npcListScrollRect;
        [SerializeField] private Button scrollDownButton;

        [Header("Center Standing Illustration")]
        [SerializeField] private Image standingIllustrationImage;

        [Header("Right Profile Card")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private NpcRelationGaugeView hostilityTrustGauge;
        [SerializeField] private NpcRelationGaugeView fearRespectGauge;
        [SerializeField] private NpcRelationGaugeView dependencyInterestGauge;
        [SerializeField] private TextMeshProUGUI introHeaderText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Controls")]
        [SerializeField] private bool closeOnEscape = false;

        private sealed class NpcEntryData
        {
            public string id;
            public string displayName;
            public string subtitle;
            public string description;
            public Sprite sprite;
            public float portraitOffsetY;
            public float hostilityTrust;
            public float fearRespect;
            public float dependencyInterest;
        }

        private static readonly (string key, string name, string subtitle, string desc, float defaultPortraitOffsetY, string[] keywords)[] CoreNpcOrder =
        {
            (
                "yoon-hajin",
                "윤하진",
                "외부 거래망 · M&A 브로커",
                "거래 수수료와 협상 신뢰를 중시한다. 거래를 늘리는 상대를 선호하며, 약속을 깨면 정보 정확도를 낮춘다.\n\n비공개 매물과 거래 상대를 연결한다. 주가조작 제안 사건의 발신자이기도 하다.",
                28f,
                new[] { "hajin", "yoonhajin", "yunhajin", "윤하진", "하진", "broker" }
            ),
            (
                "jung-sua",
                "정수아",
                "탐사보도 기자",
                "데이터와 팩트 검증을 최우선으로 삼으며, 기업의 비자금과 불투명한 지배구조를 추적한다.\n\n시장의 이면을 파헤치는 날카로운 보도로 여론과 주가에 큰 파장을 일으킨다.",
                0f,
                new[] { "sua", "sooah", "sooa", "jungsua", "jeongsua", "정수아", "수아", "journalist", "reporter" }
            ),
            (
                "kang-junhyuk",
                "강준혁",
                "대기업 · 전략투자본부장",
                "대규모 인수합병 시장의 핵심 의사결정자로, 압도적인 자본력을 바탕으로 협상한다.\n\n그룹의 신사업 확장과 경영권 방어를 총괄한다.",
                0f,
                new[] { "junhyuk", "junhyeok", "joonhyuk", "kangjunhyuk", "gangjunhyuk", "강준혁", "준혁" }
            ),
            (
                "park-sanghyun",
                "박상현",
                "금융감독 · 조사관",
                "불공정 거래와 내부자 정보 유출을 주시하는 원칙주의자다.\n\n시장 질서를 해치는 움직임이 포착되면 즉각 조사에 착수한다.",
                0f,
                new[] { "sanghyun", "sanghyeon", "parksanghyun", "parksanghyeon", "박상현", "상현", "inspector" }
            ),
            (
                "kim-taeseong",
                "김태성",
                "정치권 · 정책 보좌관",
                "여론과 규제 흐름에 민감하게 반응하며, 실리적인 거래를 선호한다.\n\n입법 동향과 정책 리스크 정보를 다루는 핵심 연결고리다.",
                0f,
                new[] { "taeseong", "taesung", "kimtaeseong", "kimtaesung", "김태성", "태성", "aide", "policy" }
            )
        };

        private readonly List<NpcEntryData> _entries = new List<NpcEntryData>();
        private readonly List<NpcListItemView> _spawnedItems = new List<NpcListItemView>();
        private int _selectedIndex;

        private void Awake()
        {
            NormalizeParentCanvasScale();

            if (scrollDownButton != null)
            {
                scrollDownButton.onClick.RemoveListener(OnClickScrollDown);
                scrollDownButton.onClick.AddListener(OnClickScrollDown);
            }
        }

        private void OnEnable()
        {
            NormalizeParentCanvasScale();
            Refresh();
        }

        private void Update()
        {
            if (!closeOnEscape) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
#endif
        }

        public void Open()
        {
            gameObject.SetActive(true);
            NormalizeParentCanvasScale();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (gameObject.activeSelf) Close();
            else Open();
        }

        private void NormalizeParentCanvasScale()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                CanvasScaler scaler = parentCanvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = parentCanvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 좌측 리스트 영역
            Transform leftArea = transform.Find("LeftNpcListArea");
            if (leftArea is RectTransform leftRect)
            {
                leftRect.sizeDelta = new Vector2(260f, 0f);
                leftRect.anchoredPosition = new Vector2(64f, 0f);
                Transform sv = leftArea.Find("NpcScrollView");
                if (sv is RectTransform svRect)
                {
                    svRect.offsetMin = new Vector2(0f, 160f);
                    svRect.offsetMax = new Vector2(0f, -195f);
                }
            }

            // 중앙 스탠딩 일러스트 (머리가 상단 바 아래에 온전히 들어오도록 보정)
            if (standingIllustrationImage != null)
            {
                RectTransform standRect = standingIllustrationImage.rectTransform;
                standRect.anchorMin = new Vector2(0.35f, 0f);
                standRect.anchorMax = new Vector2(0.35f, 0f);
                standRect.pivot = new Vector2(0.5f, 0f);
                standRect.sizeDelta = new Vector2(820f, 1340f);
                standRect.anchoredPosition = new Vector2(0f, -430f);
            }

            // 우측 프로필 카드
            Transform rightCard = transform.Find("RightProfileCard");
            if (rightCard is RectTransform cardRect)
            {
                cardRect.sizeDelta = new Vector2(650f, 750f);
                cardRect.anchoredPosition = new Vector2(-100f, -32f);
            }
        }

        public void Refresh()
        {
            EnsureReferences();
            BuildEntries();
            RebuildListUI();

            if (_entries.Count > 0)
            {
                SelectIndex(Mathf.Clamp(_selectedIndex, 0, _entries.Count - 1));
            }
        }

        private void EnsureReferences()
        {
            if (registry == null)
            {
                registry = FindFirstObjectByType<NpcStateRegistry>();
            }

            if (npcListContent == null)
            {
                Transform found = transform.Find("LeftNpcListArea/NpcScrollView/Viewport/Content");
                if (found != null) npcListContent = found;
            }

#if UNITY_EDITOR
            if (npcListItemPrefab == null)
            {
                npcListItemPrefab = AssetDatabase.LoadAssetAtPath<NpcListItemView>("Assets/NPC/Prefabs/NpcListItemPrefab.prefab");
            }
            if (standingSprites == null || standingSprites.Count == 0)
            {
                LoadStandingSpritesInEditor();
            }
#endif
        }

        private void BuildEntries()
        {
            _entries.Clear();

            var rawEntries = new List<NpcEntryData>();
            if (registry != null)
            {
                TryExtractFromRegistry(registry, rawEntries);
            }

            var usedSprites = new HashSet<Sprite>();
            foreach (var core in CoreNpcOrder)
            {
                NpcEntryData matched = rawEntries.FirstOrDefault(r => MatchesCoreNpc(r, core.key, core.keywords));
                if (matched == null)
                {
                    matched = new NpcEntryData
                    {
                        id = "npc-" + core.key,
                        displayName = core.name,
                        subtitle = core.subtitle,
                        description = core.desc,
                        portraitOffsetY = core.defaultPortraitOffsetY,
                        hostilityTrust = 0f,
                        fearRespect = 0f,
                        dependencyInterest = 0f
                    };
                }
                else
                {
                    if (string.IsNullOrEmpty(matched.displayName) || matched.displayName.StartsWith("npc-", StringComparison.OrdinalIgnoreCase))
                        matched.displayName = core.name;
                    if (string.IsNullOrEmpty(matched.subtitle))
                        matched.subtitle = core.subtitle;
                    if (string.IsNullOrEmpty(matched.description))
                        matched.description = core.desc;
                    if (Mathf.Approximately(matched.portraitOffsetY, 0f))
                        matched.portraitOffsetY = core.defaultPortraitOffsetY;
                }

                if (!TryApplyManualOverride(matched, out Sprite ovSp))
                {
                    Sprite sp = FindSpriteByKeywords(core.keywords, usedSprites);
                    if (sp != null)
                    {
                        matched.sprite = sp;
                        usedSprites.Add(sp);
                    }
                }
                else if (ovSp != null)
                {
                    usedSprites.Add(ovSp);
                }

                _entries.Add(matched);
            }

            foreach (Sprite extraSp in standingSprites.Where(s => s != null && !usedSprites.Contains(s)))
            {
                string normSp = NormalizeToken(extraSp.name);
                NpcEntryData extraRaw = rawEntries.FirstOrDefault(r =>
                    !_entries.Contains(r) &&
                    (NormalizeToken(r.id).Contains(normSp) || normSp.Contains(NormalizeToken(r.id))));

                if (extraRaw != null)
                {
                    extraRaw.sprite = extraSp;
                    usedSprites.Add(extraSp);
                    _entries.Add(extraRaw);
                }
            }
        }

        private static bool MatchesCoreNpc(NpcEntryData entry, string coreKey, string[] keywords)
        {
            string normSearch = NormalizeToken($"{entry.id} {entry.displayName}");
            if (normSearch.Contains(NormalizeToken(coreKey))) return true;
            return Array.Exists(keywords, k => normSearch.Contains(NormalizeToken(k)));
        }

        private Sprite FindSpriteByKeywords(string[] keywords, HashSet<Sprite> usedSprites)
        {
            if (standingSprites == null || standingSprites.Count == 0) return null;
            return standingSprites.FirstOrDefault(s =>
            {
                if (s == null || usedSprites.Contains(s)) return false;
                string normSprite = NormalizeToken(s.name);
                return Array.Exists(keywords, k => normSprite.Contains(NormalizeToken(k)));
            });
        }

        private bool TryApplyManualOverride(NpcEntryData entry, out Sprite matchedSprite)
        {
            matchedSprite = null;
            if (manualSpriteOverrides == null) return false;
            string normId = NormalizeToken(entry.id);
            string normName = NormalizeToken(entry.displayName);

            foreach (var ov in manualSpriteOverrides)
            {
                if (ov.sprite == null || string.IsNullOrWhiteSpace(ov.npcNameOrId)) continue;
                string key = NormalizeToken(ov.npcNameOrId);
                if ((!string.IsNullOrEmpty(normId) && (normId.Contains(key) || key.Contains(normId))) ||
                    (!string.IsNullOrEmpty(normName) && (normName.Contains(key) || key.Contains(normName))))
                {
                    entry.sprite = ov.sprite;
                    entry.portraitOffsetY = ov.portraitOffsetY;
                    matchedSprite = ov.sprite;
                    return true;
                }
            }
            return false;
        }

        private void TryExtractFromRegistry(NpcStateRegistry targetRegistry, List<NpcEntryData> outList)
        {
            Type regType = targetRegistry.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            IEnumerable collection = null;
            foreach (PropertyInfo prop in regType.GetProperties(flags))
            {
                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                {
                    collection = prop.GetValue(targetRegistry) as IEnumerable;
                    if (collection != null) break;
                }
            }

            if (collection == null)
            {
                foreach (FieldInfo field in regType.GetFields(flags))
                {
                    if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
                    {
                        collection = field.GetValue(targetRegistry) as IEnumerable;
                        if (collection != null) break;
                    }
                }
            }

            if (collection == null) return;

            foreach (object item in collection)
            {
                if (item == null) continue;
                object stateObj = item;
                if (item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    stateObj = item.GetType().GetProperty("Value")?.GetValue(item);
                }
                if (stateObj == null) continue;

                object defObj = ReadObjectMember(stateObj, "definition", "Definition", "profile", "Profile", "data", "Data", "meta", "Meta") ?? stateObj;

                string id = ReadStringMember(stateObj, "npcId", "id", "Id", "NpcId")
                            ?? ReadStringMember(defObj, "npcId", "id", "Id", "NpcId");
                string dName = ReadStringMember(defObj, "displayName", "koreanName", "npcName", "name", "DisplayName", "Name")
                               ?? ReadStringMember(stateObj, "displayName", "koreanName", "npcName", "DisplayName");

                if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(dName)) continue;

                string role = ReadStringMember(defObj, "subtitle", "role", "title", "affiliation", "Role", "Subtitle")
                              ?? ReadStringMember(stateObj, "subtitle", "role", "title", "affiliation", "Role", "Subtitle");
                string desc = ReadStringMember(defObj, "description", "bio", "summary", "intro", "Description", "Bio")
                              ?? ReadStringMember(stateObj, "description", "bio", "summary", "intro", "Description", "Bio");

                object relObj = ReadObjectMember(stateObj, "relation", "relationState", "Relation", "RelationState") ?? stateObj;
                float ht = NormalizeAxis(ReadFloatMember(relObj, "hostilityTrust", "trust", "HostilityTrust", "Trust"));
                float fr = NormalizeAxis(ReadFloatMember(relObj, "fearRespect", "respect", "FearRespect", "Respect"));
                float di = NormalizeAxis(ReadFloatMember(relObj, "dependencyInterest", "interest", "DependencyInterest", "Interest"));

                outList.Add(new NpcEntryData
                {
                    id = string.IsNullOrEmpty(id) ? dName : id,
                    displayName = dName,
                    subtitle = role,
                    description = desc,
                    sprite = null,
                    hostilityTrust = ht,
                    fearRespect = fr,
                    dependencyInterest = di
                });
            }
        }

        private static string NormalizeToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw.Replace("-", string.Empty)
                      .Replace("_", string.Empty)
                      .Replace(" ", string.Empty)
                      .ToLowerInvariant();
        }

        private void RebuildListUI()
        {
            if (npcListContent == null) return;

            for (int i = npcListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(npcListContent.GetChild(i).gameObject);
            }
            _spawnedItems.Clear();

            if (npcListItemPrefab == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                int capturedIndex = i;
                NpcEntryData data = _entries[i];
                NpcListItemView item = Instantiate(npcListItemPrefab, npcListContent);
                item.gameObject.SetActive(true);

                BindListItem(item, data, () => SelectIndex(capturedIndex));
                _spawnedItems.Add(item);
            }
        }

        private void SelectIndex(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            _selectedIndex = index;

            NpcEntryData data = _entries[index];

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                UpdateListItemSelection(_spawnedItems[i], i == index);
            }

            if (standingIllustrationImage != null)
            {
                standingIllustrationImage.sprite = data.sprite;
                standingIllustrationImage.enabled = data.sprite != null;
            }

            if (nameText != null) nameText.text = data.displayName;
            if (subtitleText != null) subtitleText.text = data.subtitle;
            if (introHeaderText != null) introHeaderText.text = "인물 소개";
            if (descriptionText != null) descriptionText.text = data.description;

            UpdateGauge(hostilityTrustGauge, data.hostilityTrust);
            UpdateGauge(fearRespectGauge, data.fearRespect);
            UpdateGauge(dependencyInterestGauge, data.dependencyInterest);
        }

        private static void BindListItem(NpcListItemView item, NpcEntryData data, Action onClick)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = item.GetType();

            TextMeshProUGUI tmp = t.GetField("nameText", flags)?.GetValue(item) as TextMeshProUGUI
                                  ?? item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = data.displayName;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            Image portrait = t.GetField("portraitImage", flags)?.GetValue(item) as Image;
            if (portrait != null && data.sprite != null)
            {
                portrait.sprite = data.sprite;
                if (!Mathf.Approximately(data.portraitOffsetY, 0f))
                {
                    RectTransform pr = portrait.rectTransform;
                    pr.offsetMin = new Vector2(pr.offsetMin.x, pr.offsetMin.y + data.portraitOffsetY);
                    pr.offsetMax = new Vector2(pr.offsetMax.x, pr.offsetMax.y + data.portraitOffsetY);
                }
            }

            Button btn = t.GetField("button", flags)?.GetValue(item) as Button ?? item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        private static void UpdateListItemSelection(NpcListItemView item, bool selected)
        {
            if (item == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = item.GetType();

            Image bg = t.GetField("backgroundImage", flags)?.GetValue(item) as Image ?? item.GetComponentInChildren<Image>();
            if (bg != null)
            {
                bg.color = selected
                    ? new Color(0.043f, 0.145f, 0.302f, 1f)
                    : new Color(0.88f, 0.87f, 0.85f, 0.96f);

                if (bg.gameObject != item.gameObject)
                {
                    RectTransform pillRect = bg.rectTransform;
                    pillRect.offsetMax = new Vector2(selected ? 0f : -34f, 0f);
                }
            }

            TextMeshProUGUI tmp = t.GetField("nameText", flags)?.GetValue(item) as TextMeshProUGUI
                                  ?? item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.color = selected ? Color.white : new Color(0.11f, 0.11f, 0.11f, 1f);
            }
        }

        private static void UpdateGauge(NpcRelationGaugeView gauge, float normalizedValue)
        {
            if (gauge == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = gauge.GetType();

            MethodInfo setVal = t.GetMethod("SetValue", flags) ?? t.GetMethod("SetNormalizedValue", flags);
            if (setVal != null && setVal.GetParameters().Length == 1)
            {
                setVal.Invoke(gauge, new object[] { normalizedValue });
                return;
            }

            RectTransform fill = t.GetField("activeFillRect", flags)?.GetValue(gauge) as RectTransform;
            RectTransform tick = t.GetField("indicatorTickRect", flags)?.GetValue(gauge) as RectTransform;
            float clamped = Mathf.Clamp(normalizedValue, -1f, 1f);
            float anchorX = 0.5f + clamped * 0.5f;

            if (fill != null)
            {
                fill.anchorMin = new Vector2(Mathf.Min(0.5f, anchorX), 0f);
                fill.anchorMax = new Vector2(Mathf.Max(0.5f, anchorX), 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            if (tick != null)
            {
                tick.anchorMin = new Vector2(anchorX, 0.5f);
                tick.anchorMax = new Vector2(anchorX, 0.5f);
                tick.anchoredPosition = Vector2.zero;
            }
        }

        private void OnClickScrollDown()
        {
            if (npcListScrollRect != null)
            {
                npcListScrollRect.verticalNormalizedPosition = Mathf.Clamp01(npcListScrollRect.verticalNormalizedPosition - 0.25f);
            }
        }

        private static float NormalizeAxis(float raw)
        {
            if (Mathf.Abs(raw) > 1.001f)
            {
                return Mathf.Clamp(raw / 100f, -1f, 1f);
            }
            return Mathf.Clamp(raw, -1f, 1f);
        }

        private static object ReadObjectMember(object target, params string[] names)
        {
            if (target == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = target.GetType();
            foreach (string n in names)
            {
                PropertyInfo p = t.GetProperty(n, flags);
                if (p != null) return p.GetValue(target);
                FieldInfo f = t.GetField(n, flags);
                if (f != null) return f.GetValue(target);
            }
            return null;
        }

        private static string ReadStringMember(object target, params string[] names)
        {
            return ReadObjectMember(target, names) as string;
        }

        private static float ReadFloatMember(object target, params string[] names)
        {
            object val = ReadObjectMember(target, names);
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is double d) return (float)d;
            return 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            LoadStandingSpritesInEditor();
        }

        private void LoadStandingSpritesInEditor()
        {
            const string folder = "Assets/NPC/Art/Standing";
            if (!AssetDatabase.IsValidFolder(folder)) return;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            if (guids.Length == 0) return;

            standingSprites = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
#endif
    }
}