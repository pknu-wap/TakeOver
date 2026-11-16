#if UNITY_EDITOR
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TakeOver.NPC;

namespace TakeOver.Employees.Editor
{
    public static class EmployeeUISceneBuilder
    {
        private const string BaseFolder = "Assets/Employees";
        private const string DataFolder = BaseFolder + "/Data";
        private const string PrefabsFolder = BaseFolder + "/Prefabs";
        private const string ScenesFolder = BaseFolder + "/Scenes";

        private const string ScenePath = ScenesFolder + "/EmployeeUITestScene.unity";
        private const string EmployeeRowPrefabPath = PrefabsFolder + "/EmployeeRowPrefab.prefab";
        private const string ApplicantRowPrefabPath = PrefabsFolder + "/ApplicantRowPrefab.prefab";
        private const string TabPanelPrefabPath = PrefabsFolder + "/EmployeeTabPanel.prefab";

        // Figma Palette
        private static readonly Color ColLightBg = HexColor(0xF6F6F6);
        private static readonly Color ColBorderGray = HexColor(0xBABABA);
        private static readonly Color ColDarkText = HexColor(0x111111);
        private static readonly Color ColMidGray = HexColor(0x555555);
        private static readonly Color ColNavyPanel = HexColor(0x0A244C);
        private static readonly Color ColDarkNavy = HexColor(0x061731);
        private static readonly Color ColEmptyBar = HexColor(0xD9D9D9);
        private static readonly Color ColGoldAccent = HexColor(0xF3D176);

        private static readonly float[] EmpColWeights = { 1.4f, 1.0f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 1.3f, 0.8f, 0.8f, 0.8f, 0.9f };
        private static readonly float[] AppColWeights = { 1.4f, 1.0f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 1.3f, 0.8f, 0.8f, 0.8f, 0.9f, 1.2f };

        [MenuItem("TakeOver/Employees/Build Employee UI Test Scene")]
        public static void BuildTestScene()
        {
            EnsureFolders();

            EmployeeRowView empRowPrefab = BuildEmployeeRowPrefab();
            ApplicantRowView appRowPrefab = BuildApplicantRowPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // EventSystem with New Input System UI Module + Default Actions
            GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
            Type inputModuleType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                inputModuleType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (inputModuleType != null)
                {
                    break;
                }
            }

            if (inputModuleType != null)
            {
                Component module = eventSystemGo.AddComponent(inputModuleType);
                MethodInfo assignMethod = inputModuleType.GetMethod("AssignDefaultActions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                assignMethod?.Invoke(module, null);
            }
            else
            {
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            GameObject systemsGo = new GameObject("EmployeeSystems");
            EmployeeService empService = systemsGo.AddComponent<EmployeeService>();
            GameTurnClock turnClock = systemsGo.AddComponent<GameTurnClock>();
            ActionPointService apService = systemsGo.AddComponent<ActionPointService>();

            EmployeeConfig configAsset = FindOrCreateAndEnsureDefaultsConfig();
            SerializedObject serviceSo = new SerializedObject(empService);
            serviceSo.FindProperty("config").objectReferenceValue = configAsset;
            serviceSo.FindProperty("gameTurnClock").objectReferenceValue = turnClock;
            serviceSo.FindProperty("actionPointService").objectReferenceValue = apService;
            serviceSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject canvasGo = new GameObject("EmployeeUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Optional Test Debug Bar at very top (44px)
            GameObject debugBar = CreatePanel(canvasGo.transform, "TestDebugBar", ColDarkNavy);
            RectTransform debugRect = debugBar.GetComponent<RectTransform>();
            debugRect.anchorMin = new Vector2(0f, 1f);
            debugRect.anchorMax = new Vector2(1f, 1f);
            debugRect.pivot = new Vector2(0.5f, 1f);
            debugRect.sizeDelta = new Vector2(0f, 44f);
            debugRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI debugStatusText = CreateText(debugBar.transform, "DebugStatusText", "[테스트 바] 현재 턴: 0  |  AP: 50 / 50", 16, ColGoldAccent, TextAlignmentOptions.MidlineLeft);
            SetStretch(debugStatusText.rectTransform, 20f, 180f, 4f, 4f);

            Button debugTurnBtn = CreateButton(debugBar.transform, "DebugTurnButton", "+1 턴 진행", ColGoldAccent, ColDarkText, 15);
            RectTransform turnBtnRect = debugTurnBtn.GetComponent<RectTransform>();
            turnBtnRect.anchorMin = new Vector2(1f, 0.5f);
            turnBtnRect.anchorMax = new Vector2(1f, 0.5f);
            turnBtnRect.pivot = new Vector2(1f, 0.5f);
            turnBtnRect.sizeDelta = new Vector2(130f, 32f);
            turnBtnRect.anchoredPosition = new Vector2(-16f, 0f);

            // Host area for EmployeeTabPanel (resizes freely with parent)
            GameObject hostArea = new GameObject("WorkspaceHostArea", typeof(RectTransform));
            hostArea.transform.SetParent(canvasGo.transform, false);
            RectTransform hostRect = hostArea.GetComponent<RectTransform>();
            SetStretch(hostRect, 0f, 0f, 44f, 0f);

            EmployeeTabView tabView = BuildEmployeeTabPanel(hostArea.transform, empRowPrefab, appRowPrefab);

            // Save reusable EmployeeTabPanel Prefab (without debug bar)
            PrefabUtility.SaveAsPrefabAsset(tabView.gameObject, TabPanelPrefabPath);

            // Wire scene service references on scene instance
            SerializedObject tabSo = new SerializedObject(tabView);
            tabSo.FindProperty("employeeService").objectReferenceValue = empService;
            tabSo.FindProperty("gameTurnClock").objectReferenceValue = turnClock;
            tabSo.FindProperty("actionPointService").objectReferenceValue = apService;
            tabSo.FindProperty("debugStatusText").objectReferenceValue = debugStatusText;
            tabSo.FindProperty("debugAdvanceTurnButton").objectReferenceValue = debugTurnBtn;
            tabSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EmployeeUISceneBuilder] A2 직원 탭 화면 및 프리팹 생성 완료:\n- {TabPanelPrefabPath}\n- {ScenePath}");
        }

        private static EmployeeTabView BuildEmployeeTabPanel(Transform parent, EmployeeRowView empRowPrefab, ApplicantRowView appRowPrefab)
        {
            GameObject root = CreatePanel(parent, "EmployeeTabPanel", ColDarkNavy);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetStretch(rootRect, 0f, 0f, 0f, 0f);

            EmployeeTabView tabView = root.AddComponent<EmployeeTabView>();

            // Vertical split container: Top = Employee Table (54%), Bottom = Recruitment Section (46%)
            GameObject empSection = CreatePanel(root.transform, "EmployeeTableSection", ColNavyPanel);
            RectTransform empSecRect = empSection.GetComponent<RectTransform>();
            empSecRect.anchorMin = new Vector2(0f, 0.46f);
            empSecRect.anchorMax = new Vector2(1f, 1f);
            empSecRect.offsetMin = new Vector2(16f, 8f);
            empSecRect.offsetMax = new Vector2(-16f, -16f);

            GameObject recSection = CreatePanel(root.transform, "RecruitmentSection", ColNavyPanel);
            RectTransform recSecRect = recSection.GetComponent<RectTransform>();
            recSecRect.anchorMin = new Vector2(0f, 0f);
            recSecRect.anchorMax = new Vector2(1f, 0.46f);
            recSecRect.offsetMin = new Vector2(16f, 16f);
            recSecRect.offsetMax = new Vector2(-16f, -8f);

            // --- TOP: EMPLOYEE TABLE ---
            TextMeshProUGUI empTitle = CreateText(empSection.transform, "Title", "재직 직원 목록", 20, ColLightBg, TextAlignmentOptions.MidlineLeft);
            empTitle.fontStyle = FontStyles.Bold;
            SetTopBarRect(empTitle.rectTransform, 16f, 220f, 8f, 36f);

            TextMeshProUGUI empCountText = CreateText(empSection.transform, "EmployeeCountText", "직원 0 / 상한 5", 17, ColGoldAccent, TextAlignmentOptions.MidlineRight);
            empCountText.fontStyle = FontStyles.Bold;
            RectTransform countRect = empCountText.rectTransform;
            countRect.anchorMin = new Vector2(1f, 1f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(1f, 1f);
            countRect.sizeDelta = new Vector2(220f, 36f);
            countRect.anchoredPosition = new Vector2(-16f, -8f);

            // Header row with 12 Sort Buttons
            GameObject empHeaderRow = CreatePanel(empSection.transform, "EmployeeTableHeader", ColDarkNavy);
            RectTransform empHdrRect = empHeaderRow.GetComponent<RectTransform>();
            empHdrRect.anchorMin = new Vector2(0f, 1f);
            empHdrRect.anchorMax = new Vector2(1f, 1f);
            empHdrRect.pivot = new Vector2(0.5f, 1f);
            empHdrRect.sizeDelta = new Vector2(-24f, 36f);
            empHdrRect.anchoredPosition = new Vector2(0f, -48f);

            HorizontalLayoutGroup empHdrLayout = empHeaderRow.AddComponent<HorizontalLayoutGroup>();
            empHdrLayout.spacing = 4f;
            empHdrLayout.padding = new RectOffset(8, 8, 2, 2);
            empHdrLayout.childControlWidth = true;
            empHdrLayout.childControlHeight = true;
            empHdrLayout.childForceExpandWidth = true;
            empHdrLayout.childForceExpandHeight = true;

            string[] empHeaders = { "이름", "등급", "분석", "인맥", "안목", "발품", "관리", "월급", "성실", "충성", "야망", "잠재력" };
            Button[] sortBtns = new Button[empHeaders.Length];
            for (int i = 0; i < empHeaders.Length; i++)
            {
                sortBtns[i] = CreateCellButton(empHeaderRow.transform, $"Sort_{empHeaders[i]}", empHeaders[i], EmpColWeights[i], ColDarkNavy, ColLightBg, 14);
            }

            // Employee ScrollView
            Transform empContent = CreateScrollView(empSection.transform, "EmployeeScrollView", 88f, 12f);
            TextMeshProUGUI emptyEmpText = CreateText(empContent.parent, "EmptyEmployeeText", "고용된 직원이 없습니다. 아래에서 직원을 모집해 보세요.", 16, ColBorderGray, TextAlignmentOptions.Center);
            SetStretch(emptyEmpText.rectTransform, 0f, 0f, 0f, 0f);

            // --- BOTTOM: RECRUITMENT SECTION ---
            Button recruitBtn = CreateButton(recSection.transform, "RecruitButton", "직원 모집 (AP 5)", ColGoldAccent, ColDarkText, 16);
            RectTransform recBtnRect = recruitBtn.GetComponent<RectTransform>();
            recBtnRect.anchorMin = new Vector2(0f, 1f);
            recBtnRect.anchorMax = new Vector2(0f, 1f);
            recBtnRect.pivot = new Vector2(0f, 1f);
            recBtnRect.sizeDelta = new Vector2(210f, 38f);
            recBtnRect.anchoredPosition = new Vector2(16f, -8f);
            TextMeshProUGUI recruitBtnText = recruitBtn.GetComponentInChildren<TextMeshProUGUI>();

            TextMeshProUGUI arrivalText = CreateText(recSection.transform, "ApplicantArrivalText", "3명이 지원했습니다", 17, ColGoldAccent, TextAlignmentOptions.MidlineLeft);
            arrivalText.fontStyle = FontStyles.Bold;
            SetTopBarRect(arrivalText.rectTransform, 240f, 260f, 8f, 38f);
            arrivalText.gameObject.SetActive(false);

            Button closeAppBtn = CreateButton(recSection.transform, "CloseApplicantsButton", "닫기", ColMidGray, ColLightBg, 15);
            RectTransform closeAppRect = closeAppBtn.GetComponent<RectTransform>();
            closeAppRect.anchorMin = new Vector2(1f, 1f);
            closeAppRect.anchorMax = new Vector2(1f, 1f);
            closeAppRect.pivot = new Vector2(1f, 1f);
            closeAppRect.sizeDelta = new Vector2(96f, 36f);
            closeAppRect.anchoredPosition = new Vector2(-16f, -9f);
            closeAppBtn.gameObject.SetActive(false);

            // Applicant Table Header
            GameObject appHeaderRow = CreatePanel(recSection.transform, "ApplicantTableHeader", ColDarkNavy);
            RectTransform appHdrRect = appHeaderRow.GetComponent<RectTransform>();
            appHdrRect.anchorMin = new Vector2(0f, 1f);
            appHdrRect.anchorMax = new Vector2(1f, 1f);
            appHdrRect.pivot = new Vector2(0.5f, 1f);
            appHdrRect.sizeDelta = new Vector2(-24f, 34f);
            appHdrRect.anchoredPosition = new Vector2(0f, -52f);

            HorizontalLayoutGroup appHdrLayout = appHeaderRow.AddComponent<HorizontalLayoutGroup>();
            appHdrLayout.spacing = 4f;
            appHdrLayout.padding = new RectOffset(8, 8, 2, 2);
            appHdrLayout.childControlWidth = true;
            appHdrLayout.childControlHeight = true;
            appHdrLayout.childForceExpandWidth = true;
            appHdrLayout.childForceExpandHeight = true;

            string[] appHeaders = { "이름", "등급", "분석", "인맥", "안목", "발품", "관리", "월급", "성실", "충성", "야망", "잠재력", "채용" };
            for (int i = 0; i < appHeaders.Length; i++)
            {
                CreateCellLabel(appHeaderRow.transform, $"Hdr_{appHeaders[i]}", appHeaders[i], AppColWeights[i], ColLightBg, 14, TextAlignmentOptions.Center);
            }

            Transform appContent = CreateScrollView(recSection.transform, "ApplicantScrollView", 90f, 12f);
            TextMeshProUGUI emptyAppText = CreateText(appContent.parent, "EmptyApplicantText", "현재 지원자가 없습니다.", 16, ColBorderGray, TextAlignmentOptions.Center);
            SetStretch(emptyAppText.rectTransform, 0f, 0f, 0f, 0f);

            // --- DETAIL POPUP OVERLAY ---
            EmployeeDetailView detailView = BuildDetailPopup(root.transform);

            // Serialize references into EmployeeTabView
            SerializedObject so = new SerializedObject(tabView);
            so.FindProperty("employeeCountText").objectReferenceValue = empCountText;
            so.FindProperty("employeeListContent").objectReferenceValue = empContent;
            so.FindProperty("employeeRowPrefab").objectReferenceValue = empRowPrefab;
            so.FindProperty("emptyEmployeeText").objectReferenceValue = emptyEmpText;

            so.FindProperty("sortNameButton").objectReferenceValue = sortBtns[0];
            so.FindProperty("sortGradeButton").objectReferenceValue = sortBtns[1];
            so.FindProperty("sortAnalysisButton").objectReferenceValue = sortBtns[2];
            so.FindProperty("sortNetworkButton").objectReferenceValue = sortBtns[3];
            so.FindProperty("sortEyeButton").objectReferenceValue = sortBtns[4];
            so.FindProperty("sortLegworkButton").objectReferenceValue = sortBtns[5];
            so.FindProperty("sortManagementButton").objectReferenceValue = sortBtns[6];
            so.FindProperty("sortSalaryButton").objectReferenceValue = sortBtns[7];
            so.FindProperty("sortDiligenceButton").objectReferenceValue = sortBtns[8];
            so.FindProperty("sortLoyaltyButton").objectReferenceValue = sortBtns[9];
            so.FindProperty("sortAmbitionButton").objectReferenceValue = sortBtns[10];
            so.FindProperty("sortPotentialButton").objectReferenceValue = sortBtns[11];

            so.FindProperty("recruitButton").objectReferenceValue = recruitBtn;
            so.FindProperty("recruitButtonText").objectReferenceValue = recruitBtnText;
            so.FindProperty("applicantArrivalText").objectReferenceValue = arrivalText;
            so.FindProperty("closeApplicantsButton").objectReferenceValue = closeAppBtn;
            so.FindProperty("applicantListContent").objectReferenceValue = appContent;
            so.FindProperty("applicantRowPrefab").objectReferenceValue = appRowPrefab;
            so.FindProperty("emptyApplicantText").objectReferenceValue = emptyAppText;
            so.FindProperty("detailView").objectReferenceValue = detailView;
            so.ApplyModifiedPropertiesWithoutUndo();

            return tabView;
        }

        private static EmployeeRowView BuildEmployeeRowPrefab()
        {
            GameObject rowGo = new GameObject("EmployeeRowPrefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            rowGo.GetComponent<Image>().color = HexColor(0x103163);
            LayoutElement le = rowGo.GetComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 40f;

            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            EmployeeRowView view = rowGo.AddComponent<EmployeeRowView>();

            TextMeshProUGUI[] cells = new TextMeshProUGUI[EmpColWeights.Length];
            for (int i = 0; i < EmpColWeights.Length; i++)
            {
                cells[i] = CreateCellLabel(rowGo.transform, $"Col_{i}", "-", EmpColWeights[i], ColLightBg, 15, TextAlignmentOptions.Center);
            }

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("rowButton").objectReferenceValue = rowGo.GetComponent<Button>();
            so.FindProperty("nameText").objectReferenceValue = cells[0];
            so.FindProperty("gradeText").objectReferenceValue = cells[1];
            so.FindProperty("analysisText").objectReferenceValue = cells[2];
            so.FindProperty("networkText").objectReferenceValue = cells[3];
            so.FindProperty("eyeText").objectReferenceValue = cells[4];
            so.FindProperty("legworkText").objectReferenceValue = cells[5];
            so.FindProperty("managementText").objectReferenceValue = cells[6];
            so.FindProperty("salaryText").objectReferenceValue = cells[7];
            so.FindProperty("diligenceText").objectReferenceValue = cells[8];
            so.FindProperty("loyaltyText").objectReferenceValue = cells[9];
            so.FindProperty("ambitionText").objectReferenceValue = cells[10];
            so.FindProperty("potentialText").objectReferenceValue = cells[11];
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(rowGo, EmployeeRowPrefabPath);
            UnityEngine.Object.DestroyImmediate(rowGo);
            return saved.GetComponent<EmployeeRowView>();
        }

        private static ApplicantRowView BuildApplicantRowPrefab()
        {
            GameObject rowGo = new GameObject("ApplicantRowPrefab", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            rowGo.GetComponent<Image>().color = HexColor(0x103163);
            LayoutElement le = rowGo.GetComponent<LayoutElement>();
            le.minHeight = 42f;
            le.preferredHeight = 42f;

            HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            ApplicantRowView view = rowGo.AddComponent<ApplicantRowView>();

            TextMeshProUGUI[] cells = new TextMeshProUGUI[12];
            for (int i = 0; i < 12; i++)
            {
                Color c = i >= 8 ? ColBorderGray : ColLightBg;
                cells[i] = CreateCellLabel(rowGo.transform, $"Col_{i}", i >= 8 ? "?" : "-", AppColWeights[i], c, 15, TextAlignmentOptions.Center);
            }

            Button hireBtn = CreateCellButton(rowGo.transform, "HireButton", "고용", AppColWeights[12], ColGoldAccent, ColDarkText, 14);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = cells[0];
            so.FindProperty("gradeText").objectReferenceValue = cells[1];
            so.FindProperty("analysisText").objectReferenceValue = cells[2];
            so.FindProperty("networkText").objectReferenceValue = cells[3];
            so.FindProperty("eyeText").objectReferenceValue = cells[4];
            so.FindProperty("legworkText").objectReferenceValue = cells[5];
            so.FindProperty("managementText").objectReferenceValue = cells[6];
            so.FindProperty("salaryText").objectReferenceValue = cells[7];
            so.FindProperty("diligenceText").objectReferenceValue = cells[8];
            so.FindProperty("loyaltyText").objectReferenceValue = cells[9];
            so.FindProperty("ambitionText").objectReferenceValue = cells[10];
            so.FindProperty("potentialText").objectReferenceValue = cells[11];
            so.FindProperty("hireButton").objectReferenceValue = hireBtn;
            so.FindProperty("hireButtonText").objectReferenceValue = hireBtn.GetComponentInChildren<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(rowGo, ApplicantRowPrefabPath);
            UnityEngine.Object.DestroyImmediate(rowGo);
            return saved.GetComponent<ApplicantRowView>();
        }

        private static EmployeeDetailView BuildDetailPopup(Transform parent)
        {
            GameObject dimmer = CreatePanel(parent, "EmployeeDetailPopup", new Color(0f, 0f, 0f, 0.65f));
            SetStretch(dimmer.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            EmployeeDetailView detailView = dimmer.AddComponent<EmployeeDetailView>();

            GameObject card = CreatePanel(dimmer.transform, "DetailCard", ColNavyPanel);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 640f);

            // Header
            TextMeshProUGUI nameText = CreateText(card.transform, "NameText", "직원이름", 24, ColLightBg, TextAlignmentOptions.MidlineLeft);
            nameText.fontStyle = FontStyles.Bold;
            SetTopBarRect(nameText.rectTransform, 24f, 200f, 18f, 36f);

            TextMeshProUGUI gradeText = CreateText(card.transform, "GradeText", "사원", 18, ColGoldAccent, TextAlignmentOptions.MidlineLeft);
            SetTopBarRect(gradeText.rectTransform, 230f, 120f, 20f, 32f);

            Button closeBtn = CreateButton(card.transform, "CloseButton", "닫기", ColMidGray, ColLightBg, 15);
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(84f, 34f);
            closeRect.anchoredPosition = new Vector2(-20f, -18f);

            // Salary & Contract
            TextMeshProUGUI salaryText = CreateText(card.transform, "SalaryText", "월급: 300만원", 16, ColLightBg, TextAlignmentOptions.MidlineLeft);
            SetTopBarRect(salaryText.rectTransform, 24f, 240f, 64f, 28f);

            TextMeshProUGUI remainText = CreateText(card.transform, "RemainingContractText", "남은 계약 일수: 120일", 16, ColBorderGray, TextAlignmentOptions.MidlineRight);
            RectTransform remRect = remainText.rectTransform;
            remRect.anchorMin = new Vector2(1f, 1f);
            remRect.anchorMax = new Vector2(1f, 1f);
            remRect.pivot = new Vector2(1f, 1f);
            remRect.sizeDelta = new Vector2(240f, 28f);
            remRect.anchoredPosition = new Vector2(-24f, -64f);

            // Ability Stats (0~20 bar, no max)
            float y = 110f;
            CreateStatBarRow(card.transform, "분석력", y, out Image aFill, out TextMeshProUGUI aVal); y += 40f;
            CreateStatBarRow(card.transform, "인맥", y, out Image nFill, out TextMeshProUGUI nVal); y += 40f;
            CreateStatBarRow(card.transform, "안목", y, out Image eFill, out TextMeshProUGUI eVal); y += 40f;
            CreateStatBarRow(card.transform, "발품", y, out Image lFill, out TextMeshProUGUI lVal); y += 40f;
            CreateStatBarRow(card.transform, "관리", y, out Image mFill, out TextMeshProUGUI mVal); y += 54f;

            // Hidden Stats
            CreateStatBarRow(card.transform, "성실도", y, out Image dFill, out TextMeshProUGUI dVal); y += 40f;
            CreateStatBarRow(card.transform, "충성", y, out Image loyFill, out TextMeshProUGUI loyVal); y += 40f;
            CreateStatBarRow(card.transform, "야망", y, out Image ambFill, out TextMeshProUGUI ambVal); y += 44f;

            TextMeshProUGUI potTitle = CreateText(card.transform, "PotentialTitle", "잠재력 (0~100)", 16, ColLightBg, TextAlignmentOptions.MidlineLeft);
            SetTopBarRect(potTitle.rectTransform, 24f, 160f, y, 30f);
            TextMeshProUGUI potVal = CreateText(card.transform, "PotentialValue", "?", 18, ColGoldAccent, TextAlignmentOptions.MidlineRight);
            SetTopBarRect(potVal.rectTransform, 200f, 336f, y, 30f);

            // Footer: Severance & Dismiss Button
            TextMeshProUGUI severanceText = CreateText(card.transform, "SeveranceText", "해고 보상금: 0만원", 16, ColGoldAccent, TextAlignmentOptions.MidlineLeft);
            RectTransform sevRect = severanceText.rectTransform;
            sevRect.anchorMin = new Vector2(0f, 0f);
            sevRect.anchorMax = new Vector2(0f, 0f);
            sevRect.pivot = new Vector2(0f, 0f);
            sevRect.sizeDelta = new Vector2(300f, 40f);
            sevRect.anchoredPosition = new Vector2(24f, 20f);

            Button dismissBtn = CreateButton(card.transform, "DismissButton", "해고", HexColor(0xB83232), ColLightBg, 16);
            RectTransform disRect = dismissBtn.GetComponent<RectTransform>();
            disRect.anchorMin = new Vector2(1f, 0f);
            disRect.anchorMax = new Vector2(1f, 0f);
            disRect.pivot = new Vector2(1f, 0f);
            disRect.sizeDelta = new Vector2(120f, 40f);
            disRect.anchoredPosition = new Vector2(-24f, 20f);

            SerializedObject so = new SerializedObject(detailView);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("gradeText").objectReferenceValue = gradeText;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("salaryText").objectReferenceValue = salaryText;
            so.FindProperty("remainingContractText").objectReferenceValue = remainText;
            so.FindProperty("analysisFill").objectReferenceValue = aFill;
            so.FindProperty("analysisValueText").objectReferenceValue = aVal;
            so.FindProperty("networkFill").objectReferenceValue = nFill;
            so.FindProperty("networkValueText").objectReferenceValue = nVal;
            so.FindProperty("eyeFill").objectReferenceValue = eFill;
            so.FindProperty("eyeValueText").objectReferenceValue = eVal;
            so.FindProperty("legworkFill").objectReferenceValue = lFill;
            so.FindProperty("legworkValueText").objectReferenceValue = lVal;
            so.FindProperty("managementFill").objectReferenceValue = mFill;
            so.FindProperty("managementValueText").objectReferenceValue = mVal;
            so.FindProperty("diligenceFill").objectReferenceValue = dFill;
            so.FindProperty("diligenceValueText").objectReferenceValue = dVal;
            so.FindProperty("loyaltyFill").objectReferenceValue = loyFill;
            so.FindProperty("loyaltyValueText").objectReferenceValue = loyVal;
            so.FindProperty("ambitionFill").objectReferenceValue = ambFill;
            so.FindProperty("ambitionValueText").objectReferenceValue = ambVal;
            so.FindProperty("potentialValueText").objectReferenceValue = potVal;
            so.FindProperty("severanceText").objectReferenceValue = severanceText;
            so.FindProperty("dismissButton").objectReferenceValue = dismissBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            dimmer.SetActive(false);
            return detailView;
        }

        private static void CreateStatBarRow(Transform parent, string label, float topY, out Image fillImg, out TextMeshProUGUI valueText)
        {
            TextMeshProUGUI title = CreateText(parent, label + "_Label", label, 16, ColLightBg, TextAlignmentOptions.MidlineLeft);
            SetTopBarRect(title.rectTransform, 24f, 90f, topY, 28f);

            GameObject barBg = CreatePanel(parent, label + "_BarBg", ColEmptyBar);
            RectTransform bgRect = barBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.sizeDelta = new Vector2(340f, 18f);
            bgRect.anchoredPosition = new Vector2(120f, -(topY + 5f));

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            SetStretch(fillGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImg.color = ColGoldAccent;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            valueText = CreateText(parent, label + "_Value", "0", 16, ColLightBg, TextAlignmentOptions.MidlineRight);
            SetTopBarRect(valueText.rectTransform, 470f, 66f, topY, 28f);
        }

        private static Transform CreateScrollView(Transform parent, string name, float topOffset, float bottomOffset)
        {
            GameObject svGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            svGo.transform.SetParent(parent, false);
            svGo.GetComponent<Image>().color = ColDarkNavy;
            SetStretch(svGo.GetComponent<RectTransform>(), 12f, 12f, topOffset, bottomOffset);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(svGo.transform, false);
            SetStretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = svGo.GetComponent<ScrollRect>();
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = contentRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 25f;

            return content.transform;
        }

        private static TextMeshProUGUI CreateCellLabel(Transform parent, string name, string text, float weight, Color color, int fontSize, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = weight;
            le.minWidth = 20f;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static Button CreateCellButton(Transform parent, string name, string text, float weight, Color bgColor, Color textColor, int fontSize)
        {
            Button btn = CreateButton(parent, name, text, bgColor, textColor, fontSize);
            LayoutElement le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;
            le.minWidth = 20f;
            return btn;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color bgColor, Color textColor, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;

            TextMeshProUGUI tmp = CreateText(go.transform, "Label", label, fontSize, textColor, TextAlignmentOptions.Center);
            tmp.fontStyle = FontStyles.Bold;
            SetStretch(tmp.rectTransform, 4f, 4f, 2f, 2f);
            return go.GetComponent<Button>();
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopBarRect(RectTransform rect, float left, float width, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(BaseFolder)) AssetDatabase.CreateFolder("Assets", "Employees");
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder(BaseFolder, "Data");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder)) AssetDatabase.CreateFolder(BaseFolder, "Prefabs");
            if (!AssetDatabase.IsValidFolder(ScenesFolder)) AssetDatabase.CreateFolder(BaseFolder, "Scenes");
        }

        private static EmployeeConfig FindOrCreateAndEnsureDefaultsConfig()
        {
            EmployeeConfig cfg = null;
            string[] guids = AssetDatabase.FindAssets("t:EmployeeConfig", new[] { DataFolder });
            if (guids.Length > 0)
            {
                cfg = AssetDatabase.LoadAssetAtPath<EmployeeConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            else
            {
                guids = AssetDatabase.FindAssets("t:EmployeeConfig");
                if (guids.Length > 0)
                {
                    cfg = AssetDatabase.LoadAssetAtPath<EmployeeConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<EmployeeConfig>();
                AssetDatabase.CreateAsset(cfg, DataFolder + "/EmployeeConfig.asset");
            }

            // Ensure non-zero applicant count & stat caps for stage 1+
            if (cfg.GetApplicantCountForStage(1) <= 0 || cfg.GetMaxStatCapForStage(1) <= 0 || cfg.recruitDurationTurns <= 0)
            {
                SerializedObject cfgSo = new SerializedObject(cfg);
                SerializedProperty durProp = cfgSo.FindProperty("recruitDurationTurns");
                if (durProp != null && durProp.intValue <= 0) durProp.intValue = 2;

                SerializedProperty it = cfgSo.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arraySize == 0)
                    {
                        string lower = it.name.ToLowerInvariant();
                        if (lower.Contains("applicant") || lower.Contains("count"))
                        {
                            it.arraySize = 3;
                            for (int i = 0; i < 3; i++)
                            {
                                SerializedProperty el = it.GetArrayElementAtIndex(i);
                                if (el.propertyType == SerializedPropertyType.Integer) el.intValue = 3 + i;
                            }
                        }
                        else if (lower.Contains("cap") || lower.Contains("stat"))
                        {
                            it.arraySize = 3;
                            for (int i = 0; i < 3; i++)
                            {
                                SerializedProperty el = it.GetArrayElementAtIndex(i);
                                if (el.propertyType == SerializedPropertyType.Integer) el.intValue = 10 + i * 5;
                            }
                        }
                    }
                }
                cfgSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cfg);
            }

            return cfg;
        }

        private static Color HexColor(uint hex)
        {
            float r = ((hex >> 16) & 0xFF) / 255f;
            float g = ((hex >> 8) & 0xFF) / 255f;
            float b = (hex & 0xFF) / 255f;
            return new Color(r, g, b, 1f);
        }
    }
}
#endif