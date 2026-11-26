using System.IO;
using TMPro;
using TakeOver.Operations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Operations.Editor
{
    public static class StartupTabDesignBuilder
    {
        private const string Art = "Assets/Operations/Art/";
        private const string OpsPrefab = "Assets/Operations/Prefabs/OperationsScreen.prefab";
        private const string RowPrefabPath = "Assets/Operations/Prefabs/StartupListItem.prefab";
        private const string FontPath = "Assets/ShareTrading/Fonts/Pretendard-Bold SDF.asset";
        private const string EmptyIconPath = "Assets/Employees/Art/icon_empty.png";
        private const float FrameLeft = 84f;
        private const float FrameTop = 226f;
        private const float FrameWidth = 1821f;
        private const float FrameHeight = 795f;
        private static float sx;
        private static float sy;
        private static TMP_FontAsset font;
        private static Sprite roundedSprite;
        private static Sprite headerSprite;
        private static Sprite titleBarSprite;
        private static Sprite rowGlowSprite;
        private static Sprite selectionSprite;
        private static Sprite circleSprite;
        private static Sprite thumbSprite;
        private static Sprite emptySprite;

        [MenuItem("TakeOver/Build Startup Tab Design")]
        public static void Build()
        {
            EnsureArt();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            emptySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyIconPath);
            if (font == null || emptySprite == null)
                throw new System.InvalidOperationException("Pretendard-Bold SDF 또는 icon_empty.png를 찾을 수 없어.");

            roundedSprite = LoadSprite("StartupRounded.png");
            headerSprite = LoadSprite("StartupHeader.png");
            titleBarSprite = LoadSprite("StartupTitleBar.png");
            rowGlowSprite = LoadSprite("StartupRowGlow.png");
            selectionSprite = LoadSprite("StartupSelection.png");
            circleSprite = LoadSprite("StartupCircle.png");
            thumbSprite = LoadSprite("StartupThumb.png");

            BuildRowPrefab();
            BuildOperationsPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("Startup tab prefab hierarchy was rebuilt from the design reference.");
        }

        private static void EnsureArt()
        {
            Directory.CreateDirectory(Art);
            WriteTexture("StartupRounded.png", makeRounded(96, 96, 16, false), 20);
            WriteTexture("StartupHeader.png", makeRounded(96, 96, 16, true), 20);
            WriteTexture("StartupTitleBar.png", makeGradientRounded(96, 96, 16), 20);
            WriteTexture("StartupRowGlow.png", makeGlow(315, 95), 0);
            WriteTexture("StartupSelection.png", makeSelection(291, 72, 10, 2), 0);
            WriteTexture("StartupCircle.png", makeCircle(96), 0);
            WriteTexture("StartupThumb.png", makeRounded(48, 96, 24, false), 24);
        }

        private static Texture2D makeRounded(int width, int height, int radius, bool topOnly)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool inside;
                if (topOnly && y < height - radius) inside = true;
                else
                {
                    float cx = x < radius ? radius : x >= width - radius ? width - radius - 1 : x;
                    float cy = y < radius ? radius : y >= height - radius ? height - radius - 1 : y;
                    bool corner = (x < radius || x >= width - radius) && (y < radius || y >= height - radius);
                    inside = !corner || ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius);
                    if (topOnly && y < height - radius) inside = true;
                    if (topOnly && y >= height - radius && (x < radius || x >= width - radius))
                    {
                        float dx = x < radius ? radius - x : x - (width - radius - 1);
                        float dy = y - (height - radius - 1);
                        inside = dx * dx + dy * dy <= radius * radius;
                    }
                }
                pixels[y * width + x] = inside ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D makeGradientRounded(int width, int height, int radius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = roundedDistance(x + .5f, y + .5f, 0f, 0f, width, height, radius);
                float t = (float)y / (height - 1);
                Color32 top = new Color32(239,249,254,255);
                Color32 bottom = new Color32(180,228,255,255);
                byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.r, top.r, t));
                byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.g, top.g, t));
                byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(bottom.b, top.b, t));
                pixels[y * width + x] = d <= 0f ? new Color32(r,g,b,255) : new Color32(0,0,0,0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D makeGlow(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[width * height];
            const float margin = 12f;
            float left = margin, right = width - margin - 1f, bottom = margin, top = height - margin - 1f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = roundedDistance(x + .5f, y + .5f, left, bottom, right, top, 10f);
                byte alpha = d <= 0f ? (byte)0 : (byte)Mathf.RoundToInt(80f * Mathf.Pow(Mathf.Clamp01((11f - d) / 10f), 2f));
                pixels[y * width + x] = new Color32(92,195,255,alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D makeSelection(int width, int height, int radius, int stroke)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = roundedDistance(x + 0.5f, y + 0.5f, 0f, 0f, width, height, radius);
                pixels[y * width + x] = d <= 0f && d >= -stroke ? new Color32(246,246,246,255) : new Color32(0,0,0,0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D makeCircle(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + .5f - radius, dy = y + .5f - radius;
                pixels[y * size + x] = dx * dx + dy * dy <= radius * radius
                    ? new Color32(246,246,246,255) : new Color32(0,0,0,0);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static float roundedDistance(float px, float py, float left, float bottom, float right, float top, float radius)
        {
            float cx = (left + right) * .5f;
            float cy = (bottom + top) * .5f;
            float hx = (right - left) * .5f - radius;
            float hy = (top - bottom) * .5f - radius;
            float qx = Mathf.Abs(px - cx) - hx;
            float qy = Mathf.Abs(py - cy) - hy;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static void WriteTexture(string filename, Texture2D texture, int border)
        {
            File.WriteAllBytes(Art + filename, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Art + filename, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(Art + filename);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = filename == "StartupSelection.png" ? FilterMode.Point : FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string filename) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + filename);

        private static void BuildRowPrefab()
        {
            GameObject root = new GameObject("StartupListItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)root.transform;
            sx = 1f / 1.5f;
            sy = 1f / 1.5f;
            rect.anchorMin = new Vector2(0f,1f);
            rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.sizeDelta = new Vector2(291f * sx, 71.73f * sy);

            Image background = root.GetComponent<Image>();
            background.sprite = roundedSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color32(22,41,70,255);

            Image glow = image("Glow", root.transform, rowGlowSprite, Color.white, Image.Type.Simple);
            localRect(glow.rectTransform, -12f * sx, 12f * sy, 291f * sx + 24f * sx, 71.73f * sy + 24f * sy);
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            Image selected = image("Selected", root.transform, selectionSprite, Color.white, Image.Type.Simple);
            localRect(selected.rectTransform, 0f, 0f, 291f * sx, 71.73f * sy);
            selected.gameObject.SetActive(false);
            selected.raycastTarget = false;

            GameObject circleObject = new GameObject("LogoCircle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            circleObject.transform.SetParent(root.transform, false);
            Image circle = circleObject.GetComponent<Image>();
            circle.sprite = circleSprite;
            circle.type = Image.Type.Sliced;
            circle.raycastTarget = false;
            localRect((RectTransform)circle.transform, 27f * sx, -13f * sy, 46f * sx, 46f * sy);
            circleObject.GetComponent<Mask>().showMaskGraphic = true;

            Image logo = image("Logo", circleObject.transform, null, Color.white, Image.Type.Simple);
            localRect(logo.rectTransform, 6f * sx, -7f * sy, 34f * sx, 32f * sy);
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            TMP_Text nameText = text("Company name", root.transform, "스타트업", 20f, "#F6F6F6", TextAlignmentOptions.Center);
            localRect(nameText.rectTransform, 93f * sx, -16f * sy, 181f * sx, 25f * sy);
            TMP_Text industryText = text("Industry", root.transform, "업종", 16f, "#BABABA", TextAlignmentOptions.Center);
            localRect(industryText.rectTransform, 93f * sx, -42f * sy, 181f * sx, 22f * sy);

            StartupListItemView component = root.AddComponent<StartupListItemView>();
            SerializedObject so = new SerializedObject(component);
            so.FindProperty("companyName").objectReferenceValue = nameText;
            so.FindProperty("industry").objectReferenceValue = industryText;
            so.FindProperty("logo").objectReferenceValue = logo;
            so.FindProperty("selectionFrame").objectReferenceValue = selected.gameObject;
            so.FindProperty("button").objectReferenceValue = root.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<Button>().transition = Selectable.Transition.None;
            root.GetComponent<Button>().targetGraphic = background;

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildOperationsPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(OpsPrefab);
            StartupTabView view = root.GetComponentInChildren<StartupTabView>(true);
            if (view == null) throw new System.InvalidOperationException("OperationsScreen.prefab에서 StartupTabView를 못 찾았어.");
            RectTransform panel = (RectTransform)view.transform;
            sx = 1f / 1.5f;
            sy = 1f / 1.5f;
            for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);

            RectTransform content = childRect(panel, "StartupContent");
            stretch(content);
            content.gameObject.AddComponent<CanvasGroup>();

            Image divider = image("StartupDivider", panel, null, new Color32(223,236,255,48), Image.Type.Simple);
            designRect(divider.rectTransform, 460f, 273f, 2f, 595f);
            divider.raycastTarget = false;

            GameObject listScrollObject = new GameObject("StartupListViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            RectTransform viewport = (RectTransform)listScrollObject.transform;
            viewport.SetParent(content, false);
            designRect(viewport, 104f, 273f, 357f, 305f);
            Image viewportImage = listScrollObject.GetComponent<Image>();
            viewportImage.color = new Color(1f,1f,1f,0f);
            viewportImage.raycastTarget = true;
            ScrollRect scroll = listScrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            scroll.viewport = viewport;

            RectTransform listContent = childRect(viewport, "StartupListContent");
            listContent.anchorMin = new Vector2(0f,1f);
            listContent.anchorMax = new Vector2(1f,1f);
            listContent.pivot = new Vector2(.5f,1f);
            listContent.anchoredPosition = Vector2.zero;
            listContent.sizeDelta = new Vector2(0f, viewport.rect.height);
            scroll.content = listContent;

            Image trackImage = image("StartupScrollTrack", content, null, new Color32(255,255,255,0), Image.Type.Simple);
            designRect(trackImage.rectTransform, 451f, 273f, 8f, 223f);
            trackImage.raycastTarget = true;
            RectTransform handleArea = childRect(trackImage.transform, "StartupScrollHandleArea");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = Vector2.zero;
            handleArea.offsetMax = Vector2.zero;
            Image thumb = image("StartupScrollThumb", handleArea, thumbSprite, new Color32(217,217,217,255), Image.Type.Sliced);
            thumb.rectTransform.anchorMin = Vector2.zero;
            thumb.rectTransform.anchorMax = Vector2.one;
            thumb.rectTransform.offsetMin = Vector2.zero;
            thumb.rectTransform.offsetMax = Vector2.zero;
            thumb.raycastTarget = true;
            Scrollbar scrollbar = trackImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = thumb.rectTransform;
            scrollbar.targetGraphic = thumb;
            scrollbar.size = 108f / 223f;
            scrollbar.numberOfSteps = 0;
            scrollbar.transition = Selectable.Transition.None;

            listContent.SetAsFirstSibling();

            GameObject mainDetail = new GameObject("StartupDetail", typeof(RectTransform));
            RectTransform detail = (RectTransform)mainDetail.transform;
            detail.SetParent(content, false);
            stretch(detail);

            TMP_Text title = text("StartupTitle", detail, "회사 이름 (업종)", 24f, "#F6F6F6", TextAlignmentOptions.Left);
            designRect(title.rectTransform, 550f, 310f, 800f, 44f);
            TMP_Text support = text("SupportEmployees", detail, "파견 직원 : 없음", 18f, "#BABABA", TextAlignmentOptions.Right);
            designRect(support.rectTransform, 1400f, 326f, 365f, 28f);
            Image titleBar = image("StartupTitleBar", detail, titleBarSprite, Color.white, Image.Type.Sliced);
            designRect(titleBar.rectTransform, 519f, 307f, 9f, 49f);
            titleBar.raycastTarget = false;

            var investmentValues = new TMP_Text[4];
            var financeValues = new TMP_Text[4];
            buildMetricCard(detail, "InvestmentCard", "투자 성과", 519f, 384f, 605f, 209f,
                new[] { "지분율", "지분 가치", "투자금", "수익률" }, investmentValues);
            buildMetricCard(detail, "FinanceCard", "재무 상태", 1160f, 384f, 605f, 209f,
                new[] { "현금", "매출", "순이익", "매출 성장률" }, financeValues);

            TMP_Text coreValue = null;
            TMP_Text description = null;
            buildIntroCard(detail, out coreValue, out description);

            StartupLineGraph graph = buildRevenueCard(detail, out TMP_Text[] ticks, out RectTransform labelRoot,
                out TMP_Text labelTemplate, out GameObject graphEmpty);
            Image graphAxis = image("RevenueYAxis", detail, null, new Color32(186,186,186,255), Image.Type.Simple);
            designRect(graphAxis.rectTransform, 1274f, 695f, 2f, 136f);
            CreateEmptyState(panel, out GameObject emptyRoot, out TMP_Text emptyTitle, out TMP_Text emptySubtitle);

            SerializedObject so = new SerializedObject(view);
            setRef(so, "contentRoot", content.gameObject);
            setRef(so, "emptyRoot", emptyRoot);
            setRef(so, "emptyTitle", emptyTitle);
            setRef(so, "emptyDescription", emptySubtitle);
            setRef(so, "listScroll", scroll);
            setRef(so, "listScrollbar", scrollbar);
            setRef(so, "listContent", listContent);
            setRef(so, "itemPrefab", AssetDatabase.LoadAssetAtPath<StartupListItemView>(RowPrefabPath));
            setRef(so, "scrollThumb", thumb.gameObject);
            setRef(so, "detailTitle", title);
            setRef(so, "supportEmployees", support);
            setArray(so, "investmentValues", investmentValues);
            setArray(so, "financeValues", financeValues);
            setRef(so, "coreValue", coreValue);
            setRef(so, "description", description);
            setRef(so, "graph", graph);
            setArray(so, "graphTickLabels", ticks);
            setRef(so, "graphLabelRoot", labelRoot);
            setRef(so, "graphLabelTemplate", labelTemplate);
            setRef(so, "graphEmptyRoot", graphEmpty);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, OpsPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void buildMetricCard(Transform parent, string name, string header, float x, float y,
            float width, float height, string[] labels, TMP_Text[] values)
        {
            Image body = image(name, parent, roundedSprite, Color.white, Image.Type.Sliced);
            designRect(body.rectTransform, x, y, width, height);
            body.raycastTarget = false;
            Image cap = image(name + "Header", parent, headerSprite, new Color32(186,186,186,255), Image.Type.Sliced);
            designRect(cap.rectTransform, x, y, width, 40f);
            cap.raycastTarget = false;
            TMP_Text heading = text(name + "Heading", parent, header, 19f, "#606060", TextAlignmentOptions.Center);
            designRect(heading.rectTransform, x + 10f, y + 8f, width - 20f, 25f);
            for (int i = 0; i < 4; i++)
            {
                float rowY = y + 49f + i * 36f;
                TMP_Text label = text(name + "Label" + i, parent, labels[i], 18f, "#24487E", TextAlignmentOptions.Left);
                designRect(label.rectTransform, x + 69f, rowY, 210f, 25f);
                values[i] = text(name + "Value" + i, parent, "0", 18f, "#606060", TextAlignmentOptions.Center);
                designRect(values[i].rectTransform, x + 396f, rowY, 200f, 25f);
                if (i < 3)
                {
                    Image line = image(name + "Line" + i, parent, null, new Color32(186,186,186,255), Image.Type.Simple);
                    designRect(line.rectTransform, x + 69f, rowY + 35f, 470f, 1f);
                    line.raycastTarget = false;
                }
            }
        }

        private static void buildIntroCard(Transform parent, out TMP_Text coreValue, out TMP_Text description)
        {
            Image body = image("StartupIntroCard", parent, roundedSprite, Color.white, Image.Type.Sliced);
            designRect(body.rectTransform, 519f, 634f, 605f, 257f);
            body.raycastTarget = false;
            Image cap = image("StartupIntroHeader", parent, headerSprite, new Color32(186,186,186,255), Image.Type.Sliced);
            designRect(cap.rectTransform, 519f, 634f, 605f, 40f);
            cap.raycastTarget = false;
            TMP_Text heading = text("StartupIntroHeading", parent, "회사 소개", 19f, "#606060", TextAlignmentOptions.Center);
            designRect(heading.rectTransform, 529f, 642f, 585f, 25f);
            coreValue = text("StartupCoreValue", parent, "핵심 가치 : ", 21f, "#606060", TextAlignmentOptions.Left);
            designRect(coreValue.rectTransform, 591f, 684f, 470f, 25f);
            Image line = image("StartupIntroSeparator", parent, null, new Color32(186,186,186,255), Image.Type.Simple);
            designRect(line.rectTransform, 588f, 714f, 471f, 1f);
            line.raycastTarget = false;
            description = text("StartupDescription", parent, "", 20f, "#606060", TextAlignmentOptions.TopLeft);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Overflow;
            designRect(description.rectTransform, 591f, 722f, 461f, 150f);
        }

        private static StartupLineGraph buildRevenueCard(Transform parent, out TMP_Text[] ticks,
            out RectTransform labelRoot, out TMP_Text labelTemplate, out GameObject graphEmpty)
        {
            Image body = image("StartupRevenueCard", parent, roundedSprite, Color.white, Image.Type.Sliced);
            designRect(body.rectTransform, 1160f, 634f, 605f, 257f);
            body.raycastTarget = false;
            Image cap = image("StartupRevenueHeader", parent, headerSprite, new Color32(186,186,186,255), Image.Type.Sliced);
            designRect(cap.rectTransform, 1160f, 634f, 605f, 40f);
            cap.raycastTarget = false;
            TMP_Text heading = text("StartupRevenueHeading", parent, "매출 추이", 19f, "#606060", TextAlignmentOptions.Center);
            designRect(heading.rectTransform, 1170f, 642f, 585f, 25f);

            RectTransform graphRect = childRect(parent, "RevenueGraph");
            designRect(graphRect, 1293f, 695f, 419f, 136f);
            StartupLineGraph graph = graphRect.gameObject.AddComponent<StartupLineGraph>();
            graph.color = Color.white;
            graph.raycastTarget = false;

            ticks = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                float y = 686f + i * 34f;
                ticks[i] = text("RevenueTick" + i, parent, "", 14f, "#606060", TextAlignmentOptions.Right);
                designRect(ticks[i].rectTransform, 1198f, y, 69f, 19f);
            }

            labelRoot = childRect(parent, "RevenueXAxisLabels");
            designRect(labelRoot, 1289f, 837f, 427f, 24f);
            labelTemplate = text("RevenueLabelTemplate", labelRoot, "", 15f, "#606060", TextAlignmentOptions.Center);
            RectTransform templateRect = labelTemplate.rectTransform;
            templateRect.anchorMin = new Vector2(0f,1f);
            templateRect.anchorMax = new Vector2(0f,1f);
            templateRect.pivot = new Vector2(.5f,1f);
            templateRect.sizeDelta = new Vector2(48f*sx, 20f*sy);
            templateRect.anchoredPosition = Vector2.zero;
            labelTemplate.gameObject.SetActive(false);

            graphEmpty = new GameObject("RevenueGraphEmpty", typeof(RectTransform));
            graphEmpty.transform.SetParent(parent, false);
            Image icon = image("EmptyIcon", graphEmpty.transform, emptySprite, new Color32(96,96,96,255), Image.Type.Simple);
            localDesignRect(icon.rectTransform, 283f, 91f, 31f, 27f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TMP_Text emptyText = text("EmptyMessage", graphEmpty.transform, "다음 실적 갱신 뒤부터 매출 그래프가 그려집니다", 20f, "#606060", TextAlignmentOptions.Center);
            localDesignRect(emptyText.rectTransform, 105f, 143f, 390f, 30f);
            stretch(graphEmpty.GetComponent<RectTransform>(), new Vector2(1160f,634f), new Vector2(1765f,891f));
            graphEmpty.transform.SetAsLastSibling();
            return graph;
        }

        private static void CreateEmptyState(RectTransform panel, out GameObject emptyRoot,
            out TMP_Text title, out TMP_Text subtitle)
        {
            emptyRoot = new GameObject("StartupEmptyState", typeof(RectTransform));
            RectTransform root = (RectTransform)emptyRoot.transform;
            root.SetParent(panel, false);
            stretch(root);
            Image icon = image("EmptyStateIcon", root, emptySprite, Color.white, Image.Type.Simple);
            designRect(icon.rectTransform, 1070f, 500f, 31f, 27f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            title = text("EmptyStateTitle", root, "계약한 스타트업이 없습니다", 24f, "#F6F6F6", TextAlignmentOptions.Center);
            designRect(title.rectTransform, 820f, 552f, 520f, 32f);
            subtitle = text("EmptyStateDescription", root, "직원 탭에서 직원에게 스타트업 발굴 직무를 지정하세요", 20f, "#BABABA", TextAlignmentOptions.Center);
            designRect(subtitle.rectTransform, 770f, 584f, 620f, 30f);
            emptyRoot.SetActive(true);
        }

        private static TMP_Text text(string name, Transform parent, string value, float fontPx, string hex, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.font = font;
            label.text = value;
            label.fontSize = fontPx / 1.5f;
            label.fontStyle = FontStyles.Bold;
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            label.color = color;
            label.alignment = alignment;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true;
            label.extraPadding = true;
            label.lineSpacing = 0f;
            return label;
        }

        private static Image image(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static RectTransform childRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void designRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f,1f);
            rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.sizeDelta = new Vector2(width*sx,height*sy);
            rect.anchoredPosition = new Vector2((x-FrameLeft)*sx,-(y-FrameTop)*sy);
        }

        private static void localDesignRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f,1f);
            rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.sizeDelta = new Vector2(width*sx,height*sy);
            rect.anchoredPosition = new Vector2(x*sx,-y*sy);
        }

        private static void localRect(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f,1f);
            rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.sizeDelta = new Vector2(width,height);
            rect.anchoredPosition = new Vector2(x,top);
        }

        private static void stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void stretch(RectTransform rect, Vector2 topLeft, Vector2 bottomRight)
        {
            rect.anchorMin = new Vector2(0f,1f);
            rect.anchorMax = new Vector2(0f,1f);
            rect.pivot = new Vector2(0f,1f);
            rect.sizeDelta = new Vector2((bottomRight.x-topLeft.x)*sx,(bottomRight.y-topLeft.y)*sy);
            rect.anchoredPosition = new Vector2((topLeft.x-FrameLeft)*sx,-(topLeft.y-FrameTop)*sy);
        }

        private static void setRef(SerializedObject so, string name, Object value)
        {
            so.FindProperty(name).objectReferenceValue = value;
        }

        private static void setArray(SerializedObject so, string name, Object[] values)
        {
            SerializedProperty array = so.FindProperty(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}