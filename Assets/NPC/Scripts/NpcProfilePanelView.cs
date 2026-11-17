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
        #region Serialized Types / Fields

        [Serializable]
        public struct NpcSpriteOverride
        {
            public string npcNameOrId;
            public Sprite sprite;
            public float portraitOffsetY;
            public float standingHeight;
            public float standingOffsetY;
            public float standingOffsetX;
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

        #endregion

        #region Constants / Static Data

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Canvas
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const float ScreenHeight = 1080f;

        // Left list (1920x1080 기준)
        private const float ListLeft = 48f;
        private const float ListWidth = 296f;
        private const float ListTopMargin = 229f;
        private const float ListPaddingTop = 24f;      // 선택 항목의 머리가 튀어나올 여백
        private const float ListPaddingBottom = 6f;
        private const int VisibleRows = 6;
        private const float RowWidth = 288f;
        private const float RowHeight = 96f;
        private const float SelectedRowHeight = 107f;
        private const float RowSpacing = 2f;
        private const float ListViewportHeight = ListPaddingTop + ListPaddingBottom
            + RowHeight * (VisibleRows - 1) + SelectedRowHeight + RowSpacing * (VisibleRows - 1); // 627
        private const float ListBottomMargin = ScreenHeight - ListTopMargin - ListViewportHeight;
        private const float ScrollStep = 0.35f;
        private static readonly Vector2 ScrollButtonPos = new Vector2(115f, 180f);

        // List item: pill 하나 안에 초상화 + 이름
        private const float PillWidthNormal = 240f;
        private const float PillWidthSelected = 283f;
        private const float PillHeightNormal = 73f;
        private const float PillHeightSelected = 95f;
        private const float PopupExtra = 24f;          // 선택 시 머리가 pill 위로 올라오는 높이
        private const float NameXNormal = 165f;
        private const float NameXSelected = 196f;
        private const float NameWidth = 130f;
        private const float NameHeight = 40f;

        // 초상화 프레이밍 (어긋나면 이 값들로 조정)
        private const float PortraitBaseWidth = 330f;
        private const float PortraitBaseHeight = 540f;
        private const float HeadTopPadding = 50f;       // 자동 측정 실패 시 쓰는 대체값(1.0배 기준)
        private const float PortraitScaleNormal = 1.0f;
        private const float PortraitScaleSelected = 1.35f;
        private const float HeadCenterXNormal = 42f;    // pill 왼쪽 끝에서 머리 중심까지
        private const float HeadCenterXSelected = 66f;
        private const float HeadMarginNormal = 6f;      // 머리 꼭대기와 pill 위쪽 사이 여유
        private const float HeadMarginSelected = 4f;    // 머리 꼭대기와 팝업 영역 위쪽 사이 여유

        private const string ClipperName = "PortraitClipper";
        // 초상화가 안 보이거나 클리핑이 깨지면 false (사각 클리핑으로 대체)
        private static readonly bool UseStencilMask = true;

        // Gauge
        private const float GaugeTickSize = 16f;

        // Colors
        private static readonly Color PillSelected = new Color(0.043f, 0.145f, 0.302f, 1f);
        private static readonly Color PillNormal = new Color(0.89f, 0.90f, 0.92f, 1f);
        private static readonly Color NameSelected = Color.white;
        private static readonly Color NameNormal = new Color(0.10f, 0.14f, 0.22f, 1f);
        private static readonly Color GaugeTrack = new Color(0.81f, 0.86f, 0.92f, 0.95f);
        private static readonly Color GaugeFill = new Color(0.11f, 0.25f, 0.46f, 1f);
        private static readonly Color GaugeTick = new Color(0.06f, 0.17f, 0.34f, 1f);
        private static readonly Color GaugeLabel = new Color(0.36f, 0.42f, 0.50f, 1f);

        private readonly struct CharacterIdentity
        {
            public readonly bool IsFemale;
            public readonly string[] IdTokens;
            public readonly string[] SpriteTokens;

            public CharacterIdentity(bool isFemale, string[] idTokens, string[] spriteTokens)
            {
                IsFemale = isFemale;
                IdTokens = idTokens;
                SpriteTokens = spriteTokens;
            }
        }

        // 배열 순서 = 목록 표시 순서
        private static readonly CharacterIdentity[] Identities =
        {
            new(true,  new[] { "hajin", "윤하진", "하진" },                new[] { "hajin", "yoonhajin", "yunhajin" }),
            new(true,  new[] { "sua", "sooah", "정수아", "수아" },          new[] { "sua", "sooah", "jungsua", "jeongsua" }),
            new(false, new[] { "junhyuk", "junhyeok", "강준혁", "준혁" },   new[] { "junhyuk", "junhyeok", "kangjunhyuk" }),
            new(false, new[] { "sanghyun", "sanghyeon", "박상현", "상현" }, new[] { "sanghyun", "sanghyeon", "parksanghyun" }),
            new(false, new[] { "taeseong", "taesung", "김태성", "태성" },   new[] { "taeseong", "taesung", "kimtaeseong", "kimtaesung" }),
            new(false, new[] { "mingyu", "이민규", "민규" },                new[] { "mingyu", "leemingyu" }),
            new(true,  new[] { "jia", "jiah", "서지아", "지아" },           new[] { "jia", "jiah", "seojia", "suhjia" }),
            new(true,  new[] { "harin", "김하린", "하린" },                 new[] { "harin", "kimharin" }),
            new(false, new[] { "dosik", "doshik", "박도식", "도식" },       new[] { "dosik", "doshik", "parkdosik" })
        };

        // 스프라이트 매칭 시 무시할 일반 토큰
        private static readonly HashSet<string> IgnoredNameTokens = new HashSet<string>
        {
            "npc", "ceo", "cfo", "cto", "broker", "investor", "institutional", "politician",
            "kim", "park", "lee", "kang", "jung", "yoon", "seo"
        };

        private static Sprite _circleSprite;
        private static Sprite _popupSprite;
        private static Sprite _chevronSprite;
        private static readonly Dictionary<Sprite, HeadFrame> HeadFrameCache = new Dictionary<Sprite, HeadFrame>();
        private static Sprite _pillSprite;

        #endregion

        #region Runtime Data

        private sealed class NpcEntryData
        {
            public string id;
            public string displayName;
            public string subtitle;
            public string description;
            public Sprite sprite;
            public float portraitOffsetY;
            public float standingHeight;
            public float standingOffsetY;
            public float standingOffsetX;
            public float hostilityTrust;
            public float fearRespect;
            public float dependencyInterest;
        }

        private readonly List<NpcEntryData> _entries = new List<NpcEntryData>();
        private readonly List<NpcListItemView> _spawnedItems = new List<NpcListItemView>();
        private int _selectedIndex;

        #endregion

        #region Unity Messages / Public API

        private void Awake()
        {
            ApplyCanvasScaler();
            ApplyLayout();
            BindScrollDownButton();
        }

        private void OnEnable()
        {
            ApplyCanvasScaler();
            Refresh();
        }

        private void LateUpdate()
        {
            ApplyStandingFraming();
        }

        private void Update()
        {
            if (!closeOnEscape || !WasEscapePressed()) return;
            Close();
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public void Open()
        {
            // 비활성 → 활성 전환 시 OnEnable 이 Refresh 를 수행한다.
            if (gameObject.activeSelf)
            {
                ApplyCanvasScaler();
                Refresh();
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void Close() => gameObject.SetActive(false);

        public void Toggle()
        {
            if (gameObject.activeSelf) Close();
            else Open();
        }

        public void Refresh()
        {
            EnsureReferences();
            ApplyLayout();
            BuildEntries();
            RebuildListUI();

            if (_entries.Count > 0)
            {
                SelectIndex(Mathf.Clamp(_selectedIndex, 0, _entries.Count - 1));
            }
        }

        #endregion

        #region Small UI Helpers

        private static void SetRect(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }

        private static void SetRect(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
            => SetRect(r, anchor, anchor, pivot, size, pos);

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);

        private static void PlaceTopCenter(TextMeshProUGUI text, float width, float height, float y)
            => SetRect(text.rectTransform, TopCenter, TopCenter, new Vector2(width, height), new Vector2(0f, y));

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        #endregion

        #region Layout: Canvas / Left List

        private void ApplyCanvasScaler()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(parentCanvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void ApplyLayout()
        {
            ConfigureLeftNpcListLayout();
            ConfigureRightProfileCardLayout();
            ApplyStandingFraming();
        }

        private void ConfigureLeftNpcListLayout()
        {
            Transform leftArea = transform.Find("LeftNpcListArea");
            if (leftArea is not RectTransform leftRect) return;

            SetRect(leftRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(ListWidth, 0f), new Vector2(ListLeft, 0f));

            ConfigureScrollView(leftArea.Find("NpcScrollView"));
            ConfigureListContent();
            ConfigureScrollDownButton(leftArea);
        }

        private void ConfigureScrollView(Transform sv)
        {
            if (sv is not RectTransform svRect) return;

            svRect.anchorMin = Vector2.zero;
            svRect.anchorMax = Vector2.one;
            svRect.offsetMin = new Vector2(0f, ListBottomMargin);
            svRect.offsetMax = new Vector2(0f, -ListTopMargin);

            if (npcListScrollRect == null) npcListScrollRect = sv.GetComponent<ScrollRect>();
            if (npcListScrollRect != null)
            {
                npcListScrollRect.vertical = true;
                npcListScrollRect.horizontal = false;
                npcListScrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            if (sv.Find("Viewport") is RectTransform viewport)
            {
                Stretch(viewport);
                if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                {
                    viewport.gameObject.AddComponent<RectMask2D>();
                }
            }
        }

        private void ConfigureListContent()
        {
            if (npcListContent == null) return;

            if (npcListContent is RectTransform contentRect)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = TopCenter;
                contentRect.anchoredPosition = Vector2.zero;
            }

            VerticalLayoutGroup vlg = npcListContent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = RowSpacing;
                vlg.padding = new RectOffset(0, 0, (int)ListPaddingTop, (int)ListPaddingBottom);
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            ContentSizeFitter csf = npcListContent.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void ConfigureScrollDownButton(Transform leftArea)
        {
            if (scrollDownButton == null)
            {
                Transform btnTf = leftArea.Find("ScrollDownButton") ?? leftArea.Find("DownArrowButton");
                if (btnTf != null) scrollDownButton = btnTf.GetComponent<Button>();
            }
            if (scrollDownButton == null) return;

            BindScrollDownButton();

            RectTransform btnRect = scrollDownButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                SetRect(btnRect, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(64f, 44f), ScrollButtonPos);
            }

            // 배경은 투명(클릭 영역만 유지), 흰색 쉐브론만 표시
            Image btnBg = GetOrAdd<Image>(scrollDownButton.gameObject);
            btnBg.sprite = null;
            btnBg.color = new Color(1f, 1f, 1f, 0f);
            btnBg.raycastTarget = true;
            scrollDownButton.targetGraphic = btnBg;

            Image chevron = GetOrCreateChevron(scrollDownButton.transform);

            foreach (Image img in scrollDownButton.GetComponentsInChildren<Image>(true))
            {
                if (img != btnBg && img != chevron) img.enabled = false;
            }
            foreach (TextMeshProUGUI tmp in scrollDownButton.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                tmp.enabled = false;
            }
        }

        private static Image GetOrCreateChevron(Transform parent)
        {
            Transform existing = parent.Find("ChevronIcon");
            Image chevron;
            if (existing == null)
            {
                var go = new GameObject("ChevronIcon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                chevron = go.GetComponent<Image>();
            }
            else
            {
                chevron = GetOrAdd<Image>(existing.gameObject);
            }

            chevron.sprite = GetChevronSprite();
            chevron.color = new Color(1f, 1f, 1f, 0.95f);
            chevron.preserveAspect = true;
            chevron.raycastTarget = false;
            SetRect(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(46f, 24f), Vector2.zero);
            return chevron;
        }

        private void BindScrollDownButton()
        {
            if (scrollDownButton == null) return;
            scrollDownButton.onClick.RemoveListener(OnClickScrollDown);
            scrollDownButton.onClick.AddListener(OnClickScrollDown);
        }

        private void OnClickScrollDown()
        {
            if (npcListScrollRect == null) return;
            npcListScrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(npcListScrollRect.verticalNormalizedPosition - ScrollStep);
        }

        #endregion

        #region Layout: Right Profile Card / Gauges

        private void ConfigureRightProfileCardLayout()
        {
            Transform rightCard = transform.Find("RightProfileCard");
            if (rightCard is RectTransform cardRect)
            {
                cardRect.sizeDelta = new Vector2(720f, 770f);
                cardRect.anchoredPosition = new Vector2(-64f, -32f);

                Transform oldDot = rightCard.Find("SubtitleDividerDot");
                if (oldDot != null) oldDot.gameObject.SetActive(false);
            }

            if (nameText != null)
            {
                PlaceTopCenter(nameText, 600f, 56f, -72f);
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.fontSize = 38f;
                nameText.fontStyle = FontStyles.Bold;
                nameText.color = new Color(0.10f, 0.14f, 0.22f, 1f);
            }

            if (subtitleText != null)
            {
                PlaceTopCenter(subtitleText, 600f, 34f, -130f);
                subtitleText.alignment = TextAlignmentOptions.Center;
                subtitleText.fontSize = 20f;
                subtitleText.color = new Color(0.32f, 0.37f, 0.45f, 1f);
            }

            ConfigureGaugeLayout(hostilityTrustGauge, -220f);
            ConfigureGaugeLayout(fearRespectGauge, -306f);
            ConfigureGaugeLayout(dependencyInterestGauge, -392f);

            if (introHeaderText != null)
            {
                PlaceTopCenter(introHeaderText, 580f, 36f, -478f);
                introHeaderText.alignment = TextAlignmentOptions.Center;
                introHeaderText.fontSize = 23f;
                introHeaderText.fontStyle = FontStyles.Bold;
                introHeaderText.color = new Color(0.12f, 0.16f, 0.24f, 1f);
            }

            if (descriptionText != null)
            {
                PlaceTopCenter(descriptionText, 540f, 185f, -528f);
                descriptionText.alignment = TextAlignmentOptions.Top;
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Overflow;
                descriptionText.fontSize = 19f;
                descriptionText.lineSpacing = 12f;
                descriptionText.color = new Color(0.20f, 0.24f, 0.31f, 1f);
            }
        }

        private static void GetGaugeParts(NpcRelationGaugeView gauge, out RectTransform fill, out RectTransform tick)
        {
            Type t = gauge.GetType();
            fill = t.GetField("activeFillRect", AnyInstance)?.GetValue(gauge) as RectTransform;
            tick = t.GetField("indicatorTickRect", AnyInstance)?.GetValue(gauge) as RectTransform;
        }

        private static void StyleGaugeTick(RectTransform tick)
        {
            tick.sizeDelta = new Vector2(GaugeTickSize, GaugeTickSize);
            Image img = tick.GetComponent<Image>();
            if (img == null) return;
            img.sprite = GetCircleSprite();
            img.color = GaugeTick;
        }

        private static void ConfigureGaugeLayout(NpcRelationGaugeView gauge, float anchoredY)
        {
            if (gauge == null) return;

            RectTransform root = gauge.GetComponent<RectTransform>();
            if (root != null)
            {
                SetRect(root, TopCenter, new Vector2(0.5f, 0.5f), new Vector2(580f, 54f), new Vector2(0f, anchoredY));
            }

            GetGaugeParts(gauge, out RectTransform fill, out RectTransform tick);

            Transform trackTransform = fill != null ? fill.parent : tick != null ? tick.parent : null;
            if (trackTransform is RectTransform trackRect)
            {
                trackRect.sizeDelta = new Vector2(trackRect.sizeDelta.x, 10f);
                Image trackImg = trackRect.GetComponent<Image>();
                if (trackImg != null)
                {
                    trackImg.sprite = GetPillSprite();
                    trackImg.type = Image.Type.Sliced;
                    trackImg.color = GaugeTrack;
                }
            }

            if (fill != null)
            {
                Image fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.sprite = GetPillSprite();
                    fillImg.type = Image.Type.Sliced;
                    fillImg.color = GaugeFill;
                }
            }

            if (tick != null) StyleGaugeTick(tick);

            foreach (TextMeshProUGUI label in gauge.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.fontSize = 16f;
                label.color = GaugeLabel;
            }
        }

        private static void UpdateGauge(NpcRelationGaugeView gauge, float normalizedValue)
        {
            if (gauge == null) return;
            Type t = gauge.GetType();

            MethodInfo setVal = t.GetMethod("SetValue", AnyInstance) ?? t.GetMethod("SetNormalizedValue", AnyInstance);
            if (setVal != null && setVal.GetParameters().Length == 1)
            {
                setVal.Invoke(gauge, new object[] { normalizedValue });
            }

            GetGaugeParts(gauge, out RectTransform fill, out RectTransform tick);
            float anchorX = 0.5f + Mathf.Clamp(normalizedValue, -1f, 1f) * 0.5f;

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
                StyleGaugeTick(tick);
            }
        }

        #endregion

        #region Layout: Standing Illustration

        private void ApplyStandingFraming()
        {
            if (standingIllustrationImage == null) return;

            AspectRatioFitter arf = standingIllustrationImage.GetComponent<AspectRatioFitter>();
            if (arf != null && arf.enabled) arf.enabled = false;

            ContentSizeFitter csf = standingIllustrationImage.GetComponent<ContentSizeFitter>();
            if (csf != null && csf.enabled) csf.enabled = false;

            standingIllustrationImage.preserveAspect = true;

            NpcEntryData current = GetSelectedEntry();

            RectTransform standRect = standingIllustrationImage.rectTransform;
            standRect.anchorMin = new Vector2(0.36f, 0f);
            standRect.anchorMax = new Vector2(0.36f, 0f);
            standRect.pivot = new Vector2(0.5f, 0f);
            standRect.localScale = Vector3.one;

            const float baseHeight = 1920f;
            const float topCeilingY = 960f;
            const float minWidth = 1120f;
            const float defaultAspect = 0.62f;

            float height = current != null && current.standingHeight > 0f ? current.standingHeight : baseHeight;

            float aspect = defaultAspect;
            Sprite sprite = standingIllustrationImage.sprite;
            if (sprite != null && sprite.rect.height > 0f)
            {
                aspect = sprite.rect.width / sprite.rect.height;
            }

            float width = Mathf.Max(minWidth, height * aspect);
            float posX = current != null ? current.standingOffsetX : 0f;
            float posY = (topCeilingY - height) + (current != null ? current.standingOffsetY : 0f);

            standRect.sizeDelta = new Vector2(width, height);
            standRect.anchoredPosition = new Vector2(posX, posY);
        }

        private NpcEntryData GetSelectedEntry()
            => _selectedIndex >= 0 && _selectedIndex < _entries.Count ? _entries[_selectedIndex] : null;

        #endregion

        #region Procedural Sprites

        private static Sprite GetCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 96;
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1f;

            _circleSprite = BuildSprite(size, size, (x, y) =>
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                return Mathf.Clamp01(radius - dist + 0.75f);
            });
            return _circleSprite;
        }

        private static Sprite GetPillSprite()
        {
            if (_pillSprite != null) return _pillSprite;

            const int width = 128;
            const int height = 64;
            float r = (height - 1) * 0.5f;

            _pillSprite = BuildSprite(width, height, (x, y) =>
            {
                float cx = Mathf.Clamp(x, r, width - 1 - r);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, r));
                return Mathf.Clamp01(r - dist + 0.75f);
            }, new Vector4(31f, 0f, 31f, 0f));
            return _pillSprite;
        }

        /// <summary>선택 항목용 마스크: 아래쪽만 둥글고 위쪽은 열려 있어 머리가 튀어나올 수 있다.</summary>
        private static Sprite GetPopupSprite()
        {
            if (_popupSprite != null) return _popupSprite;

            const int width = 128;
            const int height = 64;
            float r = (height - 1) * 0.5f;

            _popupSprite = BuildSprite(width, height, (x, y) =>
            {
                if (y >= r) return 1f;
                float cx = Mathf.Clamp(x, r, width - 1 - r);
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, r));
                return Mathf.Clamp01(r - dist + 0.75f);
            }, new Vector4(31f, 31f, 31f, 31f));
            return _popupSprite;
        }

        private static Sprite GetChevronSprite()
        {
            if (_chevronSprite != null) return _chevronSprite;

            const int width = 96;
            const int height = 48;
            const float halfThickness = 2.8f;
            var a = new Vector2(8f, 40f);
            var b = new Vector2(48f, 8f);
            var c = new Vector2(88f, 40f);

            _chevronSprite = BuildSprite(width, height, (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                return Mathf.Clamp01(halfThickness - d + 0.75f);
            });
            return _chevronSprite;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>흰색 + 알파 함수로 절차적 스프라이트를 만든다.</summary>
        private static Sprite BuildSprite(int width, int height, Func<int, int, float> alphaAt, Vector4 border = default)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAt(x, y)));
                }
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        #endregion

        #region References

        private void EnsureReferences()
        {
            if (registry == null) registry = FindFirstObjectByType<NpcStateRegistry>();

            if (standingIllustrationImage == null)
            {
                Transform found = transform.Find("CenterStandingIllustration") ?? transform.Find("StandingIllustration");
                if (found != null) standingIllustrationImage = found.GetComponent<Image>();
            }

            if (npcListContent == null)
            {
                Transform found = transform.Find("LeftNpcListArea/NpcScrollView/Viewport/Content");
                if (found != null) npcListContent = found;
            }

            Transform card = transform.Find("RightProfileCard");
            if (card != null)
            {
                if (nameText == null) nameText = FindText(card, "NameText");
                if (subtitleText == null) subtitleText = FindText(card, "SubtitleText", "RoleText");
                if (introHeaderText == null) introHeaderText = FindText(card, "IntroHeaderText");
                if (descriptionText == null) descriptionText = FindText(card, "DescriptionText", "BioText", "IntroText");
            }

#if UNITY_EDITOR
            if (npcListItemPrefab == null)
            {
                npcListItemPrefab = AssetDatabase.LoadAssetAtPath<NpcListItemView>("Assets/NPC/Prefabs/NpcListItemPrefab.prefab");
            }
            if (standingSprites == null || standingSprites.Count == 0 || standingSprites.Any(s => s != null && IsDimSprite(s.name)))
            {
                LoadStandingSpritesInEditor();
            }
#endif
        }

        /// <summary>주어진 이름들을 순서대로 찾아 첫 번째로 발견된 TMP 를 반환.</summary>
        private static TextMeshProUGUI FindText(Transform parent, params string[] childNames)
        {
            foreach (string childName in childNames)
            {
                TextMeshProUGUI tmp = parent.Find(childName)?.GetComponent<TextMeshProUGUI>();
                if (tmp != null) return tmp;
            }
            return null;
        }

        #endregion

        #region Entry Building

        private void BuildEntries()
        {
            _entries.Clear();
            if (registry == null) return;

            ExtractEntriesFromRegistry(registry, _entries);
            _entries.Sort((a, b) => GetDisplayOrder(a).CompareTo(GetDisplayOrder(b)));

            var usedSprites = new HashSet<Sprite>();
            foreach (NpcEntryData entry in _entries)
            {
                if (TryApplyManualOverride(entry, out Sprite overrideSprite))
                {
                    usedSprites.Add(overrideSprite);
                    continue;
                }

                Sprite match = FindMatchingStandingSprite(entry, usedSprites);
                if (match != null)
                {
                    entry.sprite = match;
                    usedSprites.Add(match);
                }
            }
        }

        private bool TryApplyManualOverride(NpcEntryData entry, out Sprite matchedSprite)
        {
            matchedSprite = null;
            if (manualSpriteOverrides == null) return false;

            string normId = NormalizeToken(entry.id);
            string normName = NormalizeToken(entry.displayName);

            foreach (NpcSpriteOverride ov in manualSpriteOverrides)
            {
                if (string.IsNullOrWhiteSpace(ov.npcNameOrId)) continue;

                string key = NormalizeToken(ov.npcNameOrId);
                bool isMatch = IsLooseMatch(normId, key) || IsLooseMatch(normName, key);
                if (!isMatch) continue;

                if (ov.sprite != null)
                {
                    entry.sprite = ov.sprite;
                    matchedSprite = ov.sprite;
                }
                entry.portraitOffsetY = ov.portraitOffsetY;
                entry.standingHeight = ov.standingHeight;
                entry.standingOffsetY = ov.standingOffsetY;
                entry.standingOffsetX = ov.standingOffsetX;
                return ov.sprite != null;
            }
            return false;
        }

        private static bool IsLooseMatch(string value, string key)
            => !string.IsNullOrEmpty(value) && (value.Contains(key) || key.Contains(value));

        private void ExtractEntriesFromRegistry(NpcStateRegistry source, List<NpcEntryData> output)
        {
            if (source.Profiles == null) return;

            Dictionary<string, object> stateMap = BuildStateMap(source);

            foreach (var entry in source.Profiles)
            {
                if (entry == null || entry.profile == null) continue;

                string id = entry.npcId;
                object prof = entry.profile;

                string displayName = ReadFirstString(prof, "displayName", "koreanName", "npcName", "titleName", "name", "DisplayName", "Name");
                string affiliation = ReadFirstString(prof, "affiliation", "organization", "company", "faction", "group", "department", "Affiliation", "Organization", "Company");
                string role = ReadFirstString(prof, "subtitle", "role", "roleTitle", "position", "job", "title", "Role", "Subtitle", "Position");
                string description = ReadFirstString(prof,
                    "description", "summary", "bio", "biography", "profileText", "introText",
                    "introduction", "intro", "overview", "background", "detail", "details",
                    "personality", "note", "notes", "Description", "Summary", "Bio", "Introduction", "Overview");

                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = ReadAnyKoreanString(prof, 20) ?? id;
                }
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = ReadLongestKoreanDescription(prof, displayName, role, affiliation);
                }

                var data = new NpcEntryData
                {
                    id = string.IsNullOrEmpty(id) ? displayName : id,
                    displayName = string.IsNullOrEmpty(displayName) ? id : displayName,
                    subtitle = BuildSubtitle(affiliation, role),
                    description = description ?? string.Empty,
                    sprite = ReadObjectMember(prof, "standingSprite", "portraitSprite", "sprite", "portrait", "StandingSprite", "Sprite", "Portrait") as Sprite
                };

                if (!string.IsNullOrEmpty(id) && stateMap.TryGetValue(id, out object state) && state != null)
                {
                    ReadRelationAxes(state, data);
                }

                output.Add(data);
            }
        }

        private static string BuildSubtitle(string affiliation, string role)
        {
            bool hasAffiliation = !string.IsNullOrWhiteSpace(affiliation);
            bool hasRole = !string.IsNullOrWhiteSpace(role);

            if (hasAffiliation && hasRole && !role.Contains(affiliation, StringComparison.OrdinalIgnoreCase))
            {
                return $"{affiliation} · {role}";
            }
            return hasRole ? role : (affiliation ?? string.Empty);
        }

        private static void ReadRelationAxes(object state, NpcEntryData data)
        {
            object rel = ReadObjectMember(state, "relation", "relationState", "relationship", "Relation", "RelationState") ?? state;
            data.hostilityTrust = NormalizeAxis(ReadFloatMember(rel, "hostilityTrust", "trust", "affinity", "HostilityTrust", "Trust"));
            data.fearRespect = NormalizeAxis(ReadFloatMember(rel, "fearRespect", "respect", "fear", "FearRespect", "Respect"));
            data.dependencyInterest = NormalizeAxis(ReadFloatMember(rel, "dependencyInterest", "interest", "dependency", "DependencyInterest", "Interest"));
        }

        private static float NormalizeAxis(float raw)
            => Mathf.Abs(raw) > 1.001f ? Mathf.Clamp(raw / 100f, -1f, 1f) : Mathf.Clamp(raw, -1f, 1f);

        private static Dictionary<string, object> BuildStateMap(NpcStateRegistry source)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Type t = source.GetType();

            IEnumerable<IEnumerable> collections = t.GetProperties(AnyInstance)
                .Where(p => p.Name != "Profiles" && IsEnumerableNonString(p.PropertyType))
                .Select(p => p.GetValue(source) as IEnumerable)
                .Concat(t.GetFields(AnyInstance)
                    .Where(f => IsEnumerableNonString(f.FieldType))
                    .Select(f => f.GetValue(source) as IEnumerable))
                .Where(e => e != null);

            foreach (IEnumerable collection in collections)
            {
                foreach (object item in collection)
                {
                    if (item == null) continue;

                    string key;
                    object value = item;
                    Type itemType = item.GetType();

                    if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        key = itemType.GetProperty("Key")?.GetValue(item) as string;
                        value = itemType.GetProperty("Value")?.GetValue(item);
                    }
                    else
                    {
                        key = ReadFirstString(item, "npcId", "id", "Id", "NpcId");
                    }

                    if (!string.IsNullOrEmpty(key) && value != null && !map.ContainsKey(key))
                    {
                        map[key] = value;
                    }
                }
            }
            return map;
        }

        private static bool IsEnumerableNonString(Type type)
            => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

        #endregion

        #region Character Identity / Sprite Matching

        private static int FindIdentityIndex(string combinedNorm)
        {
            for (int i = 0; i < Identities.Length; i++)
            {
                if (ContainsAnyToken(combinedNorm, Identities[i].IdTokens)) return i;
            }
            return -1;
        }

        private static bool ContainsAnyToken(string normalizedText, string[] tokens)
            => tokens.Any(t => normalizedText.Contains(NormalizeToken(t)));

        private static int GetDisplayOrder(NpcEntryData entry)
        {
            int index = FindIdentityIndex(CombineNormalized(entry));
            return index >= 0 ? index : Identities.Length;
        }

        private static string CombineNormalized(NpcEntryData entry)
            => $"{NormalizeToken(entry.id)} {NormalizeToken(entry.displayName)}";

        private static bool? DetectSpriteIsFemale(string spriteName)
        {
            string norm = NormalizeToken(spriteName);
            foreach (CharacterIdentity identity in Identities)
            {
                if (ContainsAnyToken(norm, identity.SpriteTokens)) return identity.IsFemale;
            }
            return null;
        }

        private static bool IsDimSprite(string spriteName) => NameContains(spriteName, "dim");
        private static bool IsStandingSprite(string spriteName) => NameContains(spriteName, "standing");

        private static bool NameContains(string name, string part)
            => !string.IsNullOrEmpty(name) && name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static readonly char[] WordSeparators = { '-', '_', ' ' };

        private static string[] SplitWords(string raw)
            => raw.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        private Sprite FindMatchingStandingSprite(NpcEntryData entry, HashSet<Sprite> usedSprites)
        {
            if (standingSprites == null || standingSprites.Count == 0) return null;

            string combined = CombineNormalized(entry);
            int identityIndex = FindIdentityIndex(combined);
            bool? entryIsFemale = identityIndex >= 0 ? Identities[identityIndex].IsFemale : (bool?)null;

            HashSet<string> candidateTokens = BuildCandidateTokens(entry, combined);

            bool IsUsable(Sprite s)
            {
                if (s == null || usedSprites.Contains(s) || IsDimSprite(s.name)) return false;
                if (!entryIsFemale.HasValue) return true;
                bool? spriteGender = DetectSpriteIsFemale(s.name);
                return !spriteGender.HasValue || spriteGender.Value == entryIsFemale.Value;
            }

            List<Sprite> usableAny = standingSprites.Where(IsUsable).ToList();
            List<Sprite> usableStanding = usableAny.Where(s => IsStandingSprite(s.name)).ToList();

            bool ContainsCandidate(Sprite s)
            {
                string normSprite = NormalizeToken(s.name);
                return candidateTokens.Any(normSprite.Contains);
            }

            // 1) standing 스프라이트 중 단어가 정확히 일치
            Sprite exact = usableStanding.FirstOrDefault(s =>
                SplitWords(s.name).Select(NormalizeToken).Any(candidateTokens.Contains));
            if (exact != null) return exact;

            // 2) standing 스프라이트 중 부분 일치
            Sprite partialStanding = usableStanding.FirstOrDefault(ContainsCandidate);
            if (partialStanding != null) return partialStanding;

            // 3) 전체 스프라이트 중 부분 일치
            Sprite partialAny = usableAny.FirstOrDefault(ContainsCandidate);
            if (partialAny != null) return partialAny;

            // 4) 남은 것 중 아무거나
            return usableStanding.FirstOrDefault() ?? usableAny.FirstOrDefault();
        }

        private static HashSet<string> BuildCandidateTokens(NpcEntryData entry, string combinedNorm)
        {
            var tokens = new HashSet<string>();

            string normId = NormalizeToken(entry.id);
            string normName = NormalizeToken(entry.displayName);
            if (!string.IsNullOrEmpty(normId)) tokens.Add(normId);
            if (!string.IsNullOrEmpty(normName)) tokens.Add(normName);

            foreach (CharacterIdentity identity in Identities)
            {
                if (!ContainsAnyToken(combinedNorm, identity.IdTokens)) continue;
                foreach (string spriteToken in identity.SpriteTokens)
                {
                    tokens.Add(NormalizeToken(spriteToken));
                }
            }

            foreach (string word in SplitWords($"{entry.id}_{entry.displayName}"))
            {
                string token = NormalizeToken(word);
                if (token.Length >= 2 && !IgnoredNameTokens.Contains(token))
                {
                    tokens.Add(token);
                }
            }
            return tokens;
        }

        private static string NormalizeToken(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return raw.Replace("-", string.Empty)
                      .Replace("_", string.Empty)
                      .Replace(" ", string.Empty)
                      .ToLowerInvariant();
        }

        #endregion

        #region Reflection Readers

        private static object ReadObjectMember(object target, params string[] names)
        {
            if (target == null) return null;
            Type t = target.GetType();
            foreach (string n in names)
            {
                PropertyInfo p = t.GetProperty(n, AnyInstance);
                if (p != null) return p.GetValue(target);
                FieldInfo f = t.GetField(n, AnyInstance);
                if (f != null) return f.GetValue(target);
            }
            return null;
        }

        private static string ReadFirstString(object target, params string[] names)
        {
            if (target == null) return null;
            Type t = target.GetType();
            foreach (string n in names)
            {
                PropertyInfo p = t.GetProperty(n, AnyInstance);
                if (p != null && p.PropertyType == typeof(string) && p.GetValue(target) is string ps && !string.IsNullOrWhiteSpace(ps))
                    return ps;

                FieldInfo f = t.GetField(n, AnyInstance);
                if (f != null && f.FieldType == typeof(string) && f.GetValue(target) is string fs && !string.IsNullOrWhiteSpace(fs))
                    return fs;
            }
            return null;
        }

        private static float ReadFloatMember(object target, params string[] names)
        {
            switch (ReadObjectMember(target, names))
            {
                case float f: return f;
                case int i: return i;
                case double d: return (float)d;
                default: return 0f;
            }
        }

        /// <summary>대상의 모든 필드 값 → 읽기 가능한 프로퍼티 값 순으로 열거한다.</summary>
        private static IEnumerable<object> EnumerateMemberValues(object target)
        {
            Type t = target.GetType();

            foreach (FieldInfo f in t.GetFields(AnyInstance))
            {
                yield return f.GetValue(target);
            }

            foreach (PropertyInfo p in t.GetProperties(AnyInstance))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                yield return p.GetValue(target);
            }
        }

        private static bool ContainsHangul(string s) => s.Any(c => c >= '\uAC00' && c <= '\uD7A3');

        private static string ReadAnyKoreanString(object target, int maxLength)
        {
            if (target == null) return null;

            return EnumerateMemberValues(target)
                .OfType<string>()
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v.Length <= maxLength && ContainsHangul(v));
        }

        private static string ReadLongestKoreanDescription(object target, params string[] excludeValues)
        {
            if (target == null) return string.Empty;

            var candidates = new List<string>();
            foreach (object value in EnumerateMemberValues(target))
            {
                if (value is string s)
                {
                    if (!string.IsNullOrWhiteSpace(s)) candidates.Add(s.Trim());
                }
                else if (value is IEnumerable<string> lines)
                {
                    string joined = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                    if (!string.IsNullOrWhiteSpace(joined)) candidates.Add(joined);
                }
            }

            bool IsExcluded(string c) => excludeValues.Any(ex =>
                !string.IsNullOrEmpty(ex) && string.Equals(c, ex.Trim(), StringComparison.OrdinalIgnoreCase));

            return candidates
                       .Where(c => c.Length >= 15 && ContainsHangul(c) && !IsExcluded(c))
                       .OrderByDescending(c => c.Length)
                       .FirstOrDefault() ?? string.Empty;
        }

        #endregion

        #region List UI

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
                int index = i;
                NpcListItemView item = Instantiate(npcListItemPrefab, npcListContent);
                item.gameObject.SetActive(true);

                BindListItem(item, _entries[i], () => SelectIndex(index));
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
                ApplyItemVisualState(_spawnedItems[i], _entries[i], i == index);
            }
            if (npcListContent is RectTransform listRect) LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

            if (standingIllustrationImage != null)
            {
                standingIllustrationImage.sprite = data.sprite;
                standingIllustrationImage.enabled = data.sprite != null;
                ApplyStandingFraming();
            }

            if (nameText != null) nameText.text = data.displayName;
            if (subtitleText != null) subtitleText.text = data.subtitle;
            if (introHeaderText != null) introHeaderText.text = "인물 소개";
            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(true);
                descriptionText.enabled = true;
                descriptionText.text = data.description;
            }

            UpdateGauge(hostilityTrustGauge, data.hostilityTrust);
            UpdateGauge(fearRespectGauge, data.fearRespect);
            UpdateGauge(dependencyInterestGauge, data.dependencyInterest);
        }

        private static T GetItemField<T>(NpcListItemView item, string fieldName) where T : class
            => item.GetType().GetField(fieldName, AnyInstance)?.GetValue(item) as T;

        private static TextMeshProUGUI GetItemNameText(NpcListItemView item)
            => GetItemField<TextMeshProUGUI>(item, "nameText") ?? item.GetComponentInChildren<TextMeshProUGUI>(true);

        private static void BindListItem(NpcListItemView item, NpcEntryData data, Action onClick)
        {
            LayoutElement le = GetOrAdd<LayoutElement>(item.gameObject);
            le.preferredWidth = RowWidth;

            BindPillBackground(item);
            BindPortrait(item, data);
            BindNameLabel(item, data);

            Button btn = GetItemField<Button>(item, "button") ?? item.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick?.Invoke());
            }

            ApplyItemVisualState(item, data, false);
        }

        private static void BindPillBackground(NpcListItemView item)
        {
            Image bg = GetItemField<Image>(item, "backgroundImage");
            if (bg == null || bg.gameObject == item.gameObject) return;

            if (bg.transform.parent != item.transform) bg.transform.SetParent(item.transform, false);
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.transform.SetAsFirstSibling();
        }

        private static void BindPortrait(NpcListItemView item, NpcEntryData data)
        {
            Image portrait = GetItemField<Image>(item, "portraitImage");
            if (portrait == null) return;

            portrait.sprite = data.sprite;
            portrait.enabled = data.sprite != null;
            portrait.preserveAspect = true;

            RectTransform clip = GetOrCreateClipper(item);
            Transform oldParent = portrait.transform.parent;
            if (oldParent != clip) portrait.transform.SetParent(clip, false);

            // 프리팹에 있던 예전 원형 마스크 컨테이너는 숨김
            Image bg = GetItemField<Image>(item, "backgroundImage");
            bool isOldContainer = oldParent != null && oldParent != item.transform && oldParent != clip
                                  && (bg == null || oldParent != bg.transform);
            if (isOldContainer) oldParent.gameObject.SetActive(false);

            clip.SetSiblingIndex(1);
        }

        private static RectTransform GetOrCreateClipper(NpcListItemView item)
        {
            if (item.transform.Find(ClipperName) is RectTransform existing) return existing;

            var go = new GameObject(ClipperName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(item.transform, false);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;

            if (UseStencilMask)
            {
                go.AddComponent<Mask>().showMaskGraphic = false;
            }
            else
            {
                img.enabled = false;
                go.AddComponent<RectMask2D>();   // softness 0: 블러 없음
            }
            return go.GetComponent<RectTransform>();
        }

        private static void BindNameLabel(NpcListItemView item, NpcEntryData data)
        {
            TextMeshProUGUI tmp = GetItemNameText(item);
            if (tmp == null) return;

            if (tmp.transform.parent != item.transform) tmp.transform.SetParent(item.transform, false);

            tmp.text = data.displayName;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.transform.SetAsLastSibling();
        }

        #region Portrait Auto Framing

        /// <summary>스프라이트 안에서 머리 꼭대기(위에서 몇 %)와 머리 가로 중심(왼쪽에서 몇 %)</summary>
        private readonly struct HeadFrame
        {
            public readonly float Top;
            public readonly float CenterX;
            public bool IsValid => Top >= 0f;

            public HeadFrame(float top, float centerX)
            {
                Top = top;
                CenterX = centerX;
            }

            public static HeadFrame Invalid => new HeadFrame(-1f, 0.5f);
        }

        /// <summary>
        /// 초상화 RectTransform 의 anchoredPosition(상단 중앙 pivot 기준)을 계산한다.
        /// 스프라이트마다 머리 위치가 달라도 머리 꼭대기가 항상 클리퍼 위쪽에서 margin 만큼 아래에 오도록 맞춘다.
        /// </summary>
        private static Vector2 CalcPortraitPosition(Sprite sprite, float scale, float headX, float margin)
        {
            float boxW = PortraitBaseWidth * scale;
            float boxH = PortraitBaseHeight * scale;

            HeadFrame frame = GetHeadFrame(sprite);
            if (!frame.IsValid) return new Vector2(headX, HeadTopPadding * scale - margin);

            // preserveAspect 로 박스 안에 들어간 실제 그림 크기
            float aspect = sprite.rect.width / sprite.rect.height;
            float fitW = Mathf.Min(boxW, boxH * aspect);
            float fitH = fitW / aspect;

            float headTopInBox = (boxH - fitH) * 0.5f + frame.Top * fitH;
            float headCenterOffsetX = (frame.CenterX - 0.5f) * fitW;

            return new Vector2(headX - headCenterOffsetX, headTopInBox - margin);
        }

        private static HeadFrame GetHeadFrame(Sprite sprite)
        {
            if (sprite == null) return HeadFrame.Invalid;
            if (HeadFrameCache.TryGetValue(sprite, out HeadFrame cached)) return cached;

            HeadFrame measured = MeasureHeadFrame(sprite);
            HeadFrameCache[sprite] = measured;
            return measured;
        }

        /// <summary>
        /// 텍스처의 Read/Write 설정과 무관하게 동작하도록 RenderTexture 로 축소 복사한 뒤 알파를 읽는다.
        /// 가장 위의 불투명 행 = 머리 꼭대기, 그 근처(그림 높이의 10%)의 가로 평균 = 머리 중심.
        /// </summary>
        private static HeadFrame MeasureHeadFrame(Sprite sprite)
        {
            const int sampleW = 96;
            const byte alphaThreshold = 77;
            const int minPixelsPerRow = 2;
            const float headRegionRatio = 0.10f;

            Texture2D src = sprite.texture;
            Rect r = sprite.textureRect;
            if (src == null || r.width <= 0f || r.height <= 0f) return HeadFrame.Invalid;

            int sampleH = Mathf.Clamp(Mathf.RoundToInt(sampleW * r.height / r.width), 8, 512);

            RenderTexture rt = RenderTexture.GetTemporary(sampleW, sampleH, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D readback = null;

            try
            {
                Graphics.Blit(src, rt,
                    new Vector2(r.width / src.width, r.height / src.height),
                    new Vector2(r.x / src.width, r.y / src.height));

                RenderTexture.active = rt;
                readback = new Texture2D(sampleW, sampleH, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, sampleW, sampleH), 0, 0);
                Color32[] px = readback.GetPixels32();   // y=0 이 아래쪽 행

                int RowOpaqueCount(int y)
                {
                    int count = 0;
                    for (int x = 0; x < sampleW; x++)
                    {
                        if (px[y * sampleW + x].a >= alphaThreshold) count++;
                    }
                    return count;
                }

                int topRow = -1;
                for (int y = sampleH - 1; y >= 0; y--)
                {
                    if (RowOpaqueCount(y) >= minPixelsPerRow) { topRow = y; break; }
                }
                if (topRow < 0) return HeadFrame.Invalid;

                int bottomRow = topRow;
                for (int y = 0; y < topRow; y++)
                {
                    if (RowOpaqueCount(y) >= minPixelsPerRow) { bottomRow = y; break; }
                }

                int headRows = Mathf.Max(1, Mathf.RoundToInt((topRow - bottomRow) * headRegionRatio));
                float sumX = 0f;
                int count = 0;
                for (int y = topRow; y > topRow - headRows && y >= 0; y--)
                {
                    for (int x = 0; x < sampleW; x++)
                    {
                        if (px[y * sampleW + x].a < alphaThreshold) continue;
                        sumX += x + 0.5f;
                        count++;
                    }
                }

                float top = (sampleH - 1 - topRow) / (float)sampleH;
                float centerX = count > 0 ? sumX / count / sampleW : 0.5f;
                return new HeadFrame(top, centerX);
            }
            catch (Exception)
            {
                return HeadFrame.Invalid;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (readback != null) Destroy(readback);
            }
        }

        #endregion

        /// <summary>선택/비선택에 따라 달라지는 시각 요소(행 높이, pill, 클리퍼, 초상화, 이름)를 한 곳에서 처리.</summary>
        private static void ApplyItemVisualState(NpcListItemView item, NpcEntryData data, bool selected)
        {
            if (item == null) return;

            float pillW = selected ? PillWidthSelected : PillWidthNormal;
            float pillH = selected ? PillHeightSelected : PillHeightNormal;

            // 행 높이 (선택 항목은 아래 항목과의 간격을 위해 더 크다)
            float rowH = selected ? SelectedRowHeight : RowHeight;
            if (item.TryGetComponent(out RectTransform itemRect)) itemRect.sizeDelta = new Vector2(RowWidth, rowH);
            LayoutElement le = GetOrAdd<LayoutElement>(item.gameObject);
            le.preferredHeight = rowH;
            le.minHeight = rowH;

            // pill
            Image bg = GetItemField<Image>(item, "backgroundImage");
            if (bg != null && bg.gameObject != item.gameObject)
            {
                bg.color = selected ? PillSelected : PillNormal;
                SetRect(bg.rectTransform, MiddleLeft, MiddleLeft, new Vector2(pillW, pillH), Vector2.zero);
            }

            // 클리퍼: 선택 시 위로 PopupExtra 만큼 열어 머리가 튀어나오게 한다
            if (item.transform.Find(ClipperName) is RectTransform clip)
            {
                float clipH = selected ? pillH + PopupExtra : pillH;
                float clipY = selected ? PopupExtra * 0.5f : 0f;
                SetRect(clip, MiddleLeft, MiddleLeft, new Vector2(pillW, clipH), new Vector2(0f, clipY));

                if (UseStencilMask && clip.TryGetComponent(out Image maskImage))
                {
                    maskImage.sprite = selected ? GetPopupSprite() : GetPillSprite();
                    maskImage.type = Image.Type.Sliced;
                }

                Image portrait = GetItemField<Image>(item, "portraitImage");
                if (portrait != null)
                {
                    float scale = selected ? PortraitScaleSelected : PortraitScaleNormal;
                    float headX = selected ? HeadCenterXSelected : HeadCenterXNormal;
                    float margin = selected ? HeadMarginSelected : HeadMarginNormal;

                    Vector2 pos = CalcPortraitPosition(data.sprite, scale, headX, margin);
                    SetRect(portrait.rectTransform, new Vector2(0f, 1f), TopCenter,
                        new Vector2(PortraitBaseWidth * scale, PortraitBaseHeight * scale),
                        new Vector2(pos.x, pos.y + data.portraitOffsetY));
                    portrait.rectTransform.localScale = Vector3.one;
                }
            }

            // 이름
            TextMeshProUGUI tmp = GetItemNameText(item);
            if (tmp != null)
            {
                tmp.color = selected ? NameSelected : NameNormal;
                SetRect(tmp.rectTransform, MiddleLeft, new Vector2(0.5f, 0.5f),
                    new Vector2(NameWidth, NameHeight), new Vector2(selected ? NameXSelected : NameXNormal, 0f));
            }
        }

        #endregion

#if UNITY_EDITOR
        #region Editor

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
                .Where(s => s != null && !IsDimSprite(s.name))
                .OrderByDescending(s => IsStandingSprite(s.name))
                .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion
#endif
    }
}