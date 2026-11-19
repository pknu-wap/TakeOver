#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeOver.NPC.Editor
{
    public static class NpcProfileUISceneBuilder
    {
        private const string PreferredFontName = "NotoSansKR-Regular SDF";
        private const string PrefabFolder = "Assets/NPC/Prefabs";
        private const string StandingArtFolder = "Assets/NPC/Art/Standing";

        // Design Colors (리니어/감마 색공간 모두 시안처럼 밝게 나오도록 보정)
        private static readonly Color ColSkyBg = new Color(0.87f, 0.92f, 0.97f, 1f);
        private static readonly Color ColCardWhite = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color ColNavyActive = new Color(0.043f, 0.145f, 0.302f, 1f);
        private static readonly Color ColPillUnselected = new Color(0.88f, 0.87f, 0.85f, 0.96f);
        private static readonly Color ColDarkText = new Color(0.11f, 0.11f, 0.11f, 1f);
        private static readonly Color ColSubText = new Color(0.42f, 0.45f, 0.49f, 1f);
        private static readonly Color ColBodyText = new Color(0.24f, 0.25f, 0.27f, 1f);
        private static readonly Color ColTrackBg = new Color(0.80f, 0.86f, 0.92f, 1f);
        private static readonly Color ColFillBlue = new Color(0.27f, 0.44f, 0.62f, 1f);

        private static readonly (string koreanName, string[] keywords)[] NpcMatchRules =
        {
            ("윤하진", new[] { "hajin", "yoonhajin", "yunhajin", "윤하진", "하진", "broker" }),
            ("정수아", new[] { "sua", "sooah", "sooa", "jungsua", "jeongsua", "정수아", "수아", "analyst" }),
            ("강준혁", new[] { "junhyuk", "junhyeok", "joonhyuk", "kangjunhyuk", "gangjunhyuk", "강준혁", "준혁", "director" }),
            ("박상현", new[] { "sanghyun", "sanghyeon", "parksanghyun", "parksanghyeon", "박상현", "상현", "inspector" }),
            ("김태성", new[] { "taeseong", "taesung", "kimtaeseong", "kimtaesung", "김태성", "태성", "aide", "policy" }),
        };

        [MenuItem("TakeOver/NPC/Apply Korean Font to NPC UI")]
        public static void ApplyKoreanFontToAll()
        {
            TMP_FontAsset koreanFont = FindKoreanFontAsset();
            if (koreanFont == null)
            {
                Debug.LogWarning($"[NpcProfileUISceneBuilder] '{PreferredFontName}' 폰트 에셋을 찾지 못했습니다.");
                return;
            }

            ApplyFontToPrefabAsset(PrefabFolder + "/NpcListItemPrefab.prefab", koreanFont);
            ApplyFontToPrefabAsset(PrefabFolder + "/NpcProfilePanel.prefab", koreanFont);

            int sceneUpdatedCount = 0;
            NpcProfilePanelView[] scenePanels = UnityEngine.Object.FindObjectsByType<NpcProfilePanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NpcProfilePanelView panel in scenePanels)
            {
                foreach (TextMeshProUGUI tmp in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    Undo.RecordObject(tmp, "Apply Korean Font");
                    tmp.font = koreanFont;
                    EditorUtility.SetDirty(tmp);
                    sceneUpdatedCount++;
                }
            }

            if (sceneUpdatedCount > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TakeOver/NPC/Build NPC Profile UI Prefabs & Test Scene")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.isPlaying = false;
                return;
            }

            EnsurePrefabFolder();

            Sprite crispPillSprite = EnsureCrispRoundedSprite(PrefabFolder + "/CrispCapsuleSprite.png", 68, 33);
            Sprite crispCardSprite = EnsureCrispRoundedSprite(PrefabFolder + "/CrispCardSprite.png", 96, 20);
            Sprite crispCircleSprite = EnsureCrispRoundedSprite(PrefabFolder + "/CrispCircleSprite.png", 64, 31);

            Dictionary<string, Sprite> standingSprites = LoadAndMapStandingSprites();
            Sprite defaultSprite = standingSprites.TryGetValue("윤하진", out Sprite yoonSprite)
                ? yoonSprite
                : standingSprites.Values.FirstOrDefault();

            string scriptFolder = ResolveExistingNpcScriptFolder();
            string listItemPrefabPath = PrefabFolder + "/NpcListItemPrefab.prefab";
            string profilePanelPrefabPath = PrefabFolder + "/NpcProfilePanel.prefab";
            string testScenePath = scriptFolder + "/NpcProfileTestScene.unity";

            TMP_FontAsset koreanFont = FindKoreanFontAsset();

            BuildListItemPrefab(listItemPrefabPath, koreanFont, defaultSprite, crispPillSprite, crispCircleSprite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            NpcListItemView itemPrefab = AssetDatabase.LoadAssetAtPath<NpcListItemView>(listItemPrefabPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            Type inputModuleType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                inputModuleType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (inputModuleType != null) break;
            }
            if (inputModuleType != null)
            {
                Component module = eventSystemGo.AddComponent(inputModuleType);
                inputModuleType.GetMethod("AssignDefaultActions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(module, null);
            }
            else
            {
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasGo = new GameObject("NpcProfileCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            NpcProfilePanelView panelView = BuildProfilePanel(canvasGo.transform, itemPrefab, koreanFont, defaultSprite, crispCardSprite);

            PrefabUtility.SaveAsPrefabAsset(panelView.gameObject, profilePanelPrefabPath);

            NpcStateRegistry registry = UnityEngine.Object.FindFirstObjectByType<NpcStateRegistry>();
            if (registry == null)
            {
                GameObject regGo = new GameObject("NpcSystems");
                registry = regGo.AddComponent<NpcStateRegistry>();
            }

            SerializedObject panelSo = new SerializedObject(panelView);
            panelSo.FindProperty("registry").objectReferenceValue = registry;
            panelSo.FindProperty("npcListItemPrefab").objectReferenceValue = itemPrefab;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, testScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[NpcProfileUISceneBuilder] 얼굴 크롭 및 밝은 카드 레이아웃 빌드 완료!");
        }

        private static Sprite EnsureCrispRoundedSprite(string assetPath, int size, int radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float r = Mathf.Min(radius, size * 0.5f - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, size - r);
                    float cy = Mathf.Clamp(py, r, size - r);
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                int border = Mathf.Min(radius, size / 2 - 1);
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Dictionary<string, Sprite> LoadAndMapStandingSprites()
        {
            var result = new Dictionary<string, Sprite>();
            if (!AssetDatabase.IsValidFolder(StandingArtFolder)) return result;

            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { StandingArtFolder });
            List<Sprite> allSprites = spriteGuids
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var usedSprites = new HashSet<Sprite>();
            foreach (var rule in NpcMatchRules)
            {
                Sprite matched = allSprites.FirstOrDefault(s =>
                    !usedSprites.Contains(s) &&
                    rule.keywords.Any(k => s.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                if (matched != null)
                {
                    result[rule.koreanName] = matched;
                    usedSprites.Add(matched);
                }
            }
            return result;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/NPC")) AssetDatabase.CreateFolder("Assets", "NPC");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/NPC", "Prefabs");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                BuildAll();
            }
        }

        private static TMP_FontAsset FindKoreanFontAsset()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && (string.Equals(font.name, PreferredFontName, StringComparison.OrdinalIgnoreCase) ||
                                     font.name.IndexOf("NotoSansKR", StringComparison.OrdinalIgnoreCase) >= 0))
                    return font;
            }
            return null;
        }

        private static void ApplyFontToPrefabAsset(string prefabPath, TMP_FontAsset font)
        {
            if (!File.Exists(prefabPath)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;
            try
            {
                foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true)) tmp.font = font;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string ResolveExistingNpcScriptFolder()
        {
            string[] guids = AssetDatabase.FindAssets("NpcProfilePanelView t:MonoScript");
            if (guids.Length > 0)
            {
                string dir = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guids[0]))?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir)) return dir;
            }
            return "Assets";
        }

        // 좌측 캡슐 버튼 + 얼굴 정중앙 원형 초상화 프리팹
        private static void BuildListItemPrefab(string prefabPath, TMP_FontAsset font, Sprite defaultSprite, Sprite pillSprite, Sprite circleSprite)
        {
            GameObject root = new GameObject("NpcListItemPrefab", typeof(RectTransform), typeof(Button), typeof(LayoutElement));
            LayoutElement le = root.GetComponent<LayoutElement>();
            le.minHeight = 68f;
            le.preferredHeight = 68f;

            // 캡슐 배경 (선택 시 오른쪽으로 돌출)
            GameObject pillBgGo = new GameObject("PillBackground", typeof(RectTransform), typeof(Image));
            pillBgGo.transform.SetParent(root.transform, false);
            RectTransform pillRect = pillBgGo.GetComponent<RectTransform>();
            SetStretch(pillRect, 0f, 34f, 0f, 0f);

            Image bg = pillBgGo.GetComponent<Image>();
            bg.sprite = pillSprite != null ? pillSprite : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bg.type = Image.Type.Sliced;
            bg.color = ColPillUnselected;

            // 원형 얼굴 썸네일 마스크
            GameObject portraitMask = new GameObject("PortraitMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            portraitMask.transform.SetParent(pillBgGo.transform, false);
            Image maskImg = portraitMask.GetComponent<Image>();
            maskImg.sprite = circleSprite != null ? circleSprite : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            maskImg.color = Color.white;
            portraitMask.GetComponent<Mask>().showMaskGraphic = false;

            RectTransform maskRect = portraitMask.GetComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0f, 0.5f);
            maskRect.anchorMax = new Vector2(0f, 0.5f);
            maskRect.pivot = new Vector2(0f, 0.5f);
            maskRect.sizeDelta = new Vector2(62f, 62f);
            maskRect.anchoredPosition = new Vector2(3f, 0f);

            // 전신 이미지 상단(머리·얼굴)이 원 정중앙에 오도록 Top -12 / Bottom -350 배치
            GameObject portraitGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(portraitMask.transform, false);
            SetStretch(portraitGo.GetComponent<RectTransform>(), -82f, -82f, -12f, -350f);
            Image portraitImg = portraitGo.GetComponent<Image>();
            portraitImg.preserveAspect = true;
            if (defaultSprite != null) portraitImg.sprite = defaultSprite;

            TextMeshProUGUI nameTmp = CreateText(pillBgGo.transform, "NameText", "윤하진", 19, ColDarkText, TextAlignmentOptions.MidlineLeft, font);
            nameTmp.fontStyle = FontStyles.Bold;
            SetStretch(nameTmp.rectTransform, 78f, 16f, 2f, 2f);

            NpcListItemView view = root.AddComponent<NpcListItemView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = root.GetComponent<Button>();
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static NpcProfilePanelView BuildProfilePanel(Transform parent, NpcListItemView itemPrefab, TMP_FontAsset font, Sprite defaultSprite, Sprite cardSprite)
        {
            GameObject root = new GameObject("NpcProfilePanel", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            SetStretch(root.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            root.GetComponent<Image>().color = ColSkyBg;

            NpcProfilePanelView view = root.AddComponent<NpcProfilePanelView>();

            // 1. LEFT: Vertical NPC Capsule List
            GameObject leftArea = new GameObject("LeftNpcListArea", typeof(RectTransform));
            leftArea.transform.SetParent(root.transform, false);
            RectTransform leftRect = leftArea.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0f, 1f);
            leftRect.pivot = new Vector2(0f, 0.5f);
            leftRect.sizeDelta = new Vector2(250f, 0f);
            leftRect.anchoredPosition = new Vector2(54f, 0f);

            GameObject svGo = new GameObject("NpcScrollView", typeof(RectTransform), typeof(ScrollRect));
            svGo.transform.SetParent(leftArea.transform, false);
            SetStretch(svGo.GetComponent<RectTransform>(), 0f, 0f, 185f, 220f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(svGo.transform, false);
            SetStretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = svGo.GetComponent<ScrollRect>();
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;

            GameObject downBtnGo = new GameObject("ScrollDownButton", typeof(RectTransform), typeof(Image), typeof(Button));
            downBtnGo.transform.SetParent(leftArea.transform, false);
            downBtnGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            RectTransform downRect = downBtnGo.GetComponent<RectTransform>();
            downRect.anchorMin = new Vector2(0.42f, 0f);
            downRect.anchorMax = new Vector2(0.42f, 0f);
            downRect.pivot = new Vector2(0.5f, 0f);
            downRect.sizeDelta = new Vector2(60f, 40f);
            downRect.anchoredPosition = new Vector2(0f, 175f);
            CreateText(downBtnGo.transform, "Arrow", "v", 24, ColSubText, TextAlignmentOptions.Center, font);

            // 2. CENTER: Zoomed Half-Body Standing Illustration
            GameObject standingGo = new GameObject("StandingIllustration", typeof(RectTransform), typeof(Image));
            standingGo.transform.SetParent(root.transform, false);
            RectTransform standRect = standingGo.GetComponent<RectTransform>();
            standRect.anchorMin = new Vector2(0.36f, 0f);
            standRect.anchorMax = new Vector2(0.36f, 0f);
            standRect.pivot = new Vector2(0.5f, 0f);
            standRect.sizeDelta = new Vector2(900f, 1480f);
            standRect.anchoredPosition = new Vector2(10f, -470f);
            Image standImg = standingGo.GetComponent<Image>();
            standImg.preserveAspect = true;
            standImg.raycastTarget = false;
            if (defaultSprite != null) standImg.sprite = defaultSprite;

            // 3. RIGHT: Bright Frosted White Profile Card
            GameObject cardGo = new GameObject("RightProfileCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(root.transform, false);
            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.sprite = cardSprite != null ? cardSprite : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cardImg.type = Image.Type.Sliced;
            cardImg.color = ColCardWhite;

            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(1f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(1f, 0.5f);
            cardRect.sizeDelta = new Vector2(660f, 760f);
            cardRect.anchoredPosition = new Vector2(-96f, 0f);

            TextMeshProUGUI nameTmp = CreateText(cardGo.transform, "NpcNameText", "윤하진", 36, ColDarkText, TextAlignmentOptions.Center, font);
            nameTmp.fontStyle = FontStyles.Bold;
            SetTopCenterRect(nameTmp.rectTransform, 64f, 560f, 48f);

            TextMeshProUGUI subTmp = CreateText(cardGo.transform, "NpcSubtitleText", "외부 거래망 · M&A 브로커", 19, ColSubText, TextAlignmentOptions.Center, font);
            SetTopCenterRect(subTmp.rectTransform, 118f, 560f, 32f);

            NpcRelationGaugeView g1 = CreateBipolarGauge(cardGo.transform, "Gauge_HostilityTrust", "적대감", "신뢰", 200f, font);
            NpcRelationGaugeView g2 = CreateBipolarGauge(cardGo.transform, "Gauge_FearRespect", "두려움", "존중", 276f, font);
            NpcRelationGaugeView g3 = CreateBipolarGauge(cardGo.transform, "Gauge_DependencyInterest", "경계", "친밀", 352f, font);

            TextMeshProUGUI introHdr = CreateText(cardGo.transform, "IntroHeaderText", "인물 소개", 22, ColDarkText, TextAlignmentOptions.Center, font);
            introHdr.fontStyle = FontStyles.Bold;
            SetTopCenterRect(introHdr.rectTransform, 456f, 520f, 36f);

            TextMeshProUGUI descTmp = CreateText(cardGo.transform, "DescriptionText",
                "거래 수수료와 협상 신뢰를 중시한다. 거래를 늘리는 상대를 선호하며, 약속을 깨면 정보 정확도를 낮춘다.\n\n비공개 매물과 거래 상대를 연결한다. 주가조작 제안 사건의 발신자이기도 하다.",
                17, ColBodyText, TextAlignmentOptions.Top, font);
            descTmp.lineSpacing = 8f;
            SetTopCenterRect(descTmp.rectTransform, 510f, 520f, 200f);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("npcListContent").objectReferenceValue = content.transform;
            so.FindProperty("npcListItemPrefab").objectReferenceValue = itemPrefab;
            so.FindProperty("npcListScrollRect").objectReferenceValue = sr;
            so.FindProperty("scrollDownButton").objectReferenceValue = downBtnGo.GetComponent<Button>();
            so.FindProperty("standingIllustrationImage").objectReferenceValue = standImg;
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.FindProperty("subtitleText").objectReferenceValue = subTmp;
            so.FindProperty("hostilityTrustGauge").objectReferenceValue = g1;
            so.FindProperty("fearRespectGauge").objectReferenceValue = g2;
            so.FindProperty("dependencyInterestGauge").objectReferenceValue = g3;
            so.FindProperty("introHeaderText").objectReferenceValue = introHdr;
            so.FindProperty("descriptionText").objectReferenceValue = descTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static NpcRelationGaugeView CreateBipolarGauge(Transform parent, string name, string leftLabel, string rightLabel, float topY, TMP_FontAsset font)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetTopCenterRect(row.GetComponent<RectTransform>(), topY, 520f, 54f);

            TextMeshProUGUI leftTmp = CreateText(row.transform, "LeftLabel", leftLabel, 17, ColBodyText, TextAlignmentOptions.MidlineLeft, font);
            leftTmp.rectTransform.anchorMin = new Vector2(0f, 1f);
            leftTmp.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            leftTmp.rectTransform.pivot = new Vector2(0f, 1f);
            leftTmp.rectTransform.sizeDelta = new Vector2(0f, 26f);
            leftTmp.rectTransform.anchoredPosition = Vector2.zero;

            TextMeshProUGUI rightTmp = CreateText(row.transform, "RightLabel", rightLabel, 17, ColBodyText, TextAlignmentOptions.MidlineRight, font);
            rightTmp.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rightTmp.rectTransform.anchorMax = new Vector2(1f, 1f);
            rightTmp.rectTransform.pivot = new Vector2(1f, 1f);
            rightTmp.rectTransform.sizeDelta = new Vector2(0f, 26f);
            rightTmp.rectTransform.anchoredPosition = Vector2.zero;

            GameObject trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(row.transform, false);
            trackGo.GetComponent<Image>().color = ColTrackBg;
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.sizeDelta = new Vector2(0f, 9f);
            trackRect.anchoredPosition = new Vector2(0f, 10f);

            GameObject fillGo = new GameObject("ActiveFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            fillGo.GetComponent<Image>().color = ColFillBlue;
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.5f, 0f);
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            GameObject tickGo = new GameObject("IndicatorTick", typeof(RectTransform), typeof(Image));
            tickGo.transform.SetParent(trackGo.transform, false);
            tickGo.GetComponent<Image>().color = ColNavyActive;
            RectTransform tickRect = tickGo.GetComponent<RectTransform>();
            tickRect.anchorMin = new Vector2(0.5f, 0.5f);
            tickRect.anchorMax = new Vector2(0.5f, 0.5f);
            tickRect.pivot = new Vector2(0.5f, 0.5f);
            tickRect.sizeDelta = new Vector2(7f, 18f);
            tickRect.anchoredPosition = Vector2.zero;

            NpcRelationGaugeView gauge = row.AddComponent<NpcRelationGaugeView>();
            SerializedObject so = new SerializedObject(gauge);
            so.FindProperty("leftLabelText").objectReferenceValue = leftTmp;
            so.FindProperty("rightLabelText").objectReferenceValue = rightTmp;
            so.FindProperty("trackRect").objectReferenceValue = trackRect;
            so.FindProperty("activeFillRect").objectReferenceValue = fillRect;
            so.FindProperty("indicatorTickRect").objectReferenceValue = tickRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gauge;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopCenterRect(RectTransform rect, float topY, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -topY);
        }
    }
}
#endif