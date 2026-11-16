using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    public class EmployeeTabTestHarness : MonoBehaviour
    {
        [SerializeField] private GameTurnClock turnClock;
        [SerializeField] private ActionPointService actionPointService;
        [SerializeField] private EmployeeService employeeService;
        [SerializeField] private RectTransform tabContainer;
        [SerializeField] private GameObject tabViewObject;

        [SerializeField] private Button advanceOneTurnButton;
        [SerializeField] private Button advanceTwoTurnsButton;
        [SerializeField] private Button toggleTabVisibilityButton;
        [SerializeField] private Button toggleContainerSizeButton;

        private bool isCompactSize = false;

        private void Start()
        {
            if (employeeService != null)
            {
                employeeService.CompanyScaleStage = 3;
                employeeService.Capacity = 2;
            }

            if (advanceOneTurnButton != null)
            {
                advanceOneTurnButton.onClick.AddListener(() => AdvanceTurns(1));
            }

            if (advanceTwoTurnsButton != null)
            {
                advanceTwoTurnsButton.onClick.AddListener(() => AdvanceTurns(2));
            }

            if (toggleTabVisibilityButton != null)
            {
                toggleTabVisibilityButton.onClick.AddListener(ToggleTabVisibility);
            }

            if (toggleContainerSizeButton != null)
            {
                toggleContainerSizeButton.onClick.AddListener(ToggleContainerSize);
            }
        }

        private void ToggleTabVisibility()
        {
            if (tabViewObject != null)
            {
                tabViewObject.SetActive(!tabViewObject.activeSelf);
            }
        }

        private void ToggleContainerSize()
        {
            if (tabContainer == null)
            {
                return;
            }

            isCompactSize = !isCompactSize;
            if (isCompactSize)
            {
                tabContainer.anchorMin = new Vector2(0.18f, 0.15f);
                tabContainer.anchorMax = new Vector2(0.82f, 0.85f);
            }
            else
            {
                tabContainer.anchorMin = new Vector2(0.05f, 0.05f);
                tabContainer.anchorMax = new Vector2(0.95f, 0.88f);
            }
            tabContainer.offsetMin = Vector2.zero;
            tabContainer.offsetMax = Vector2.zero;
        }

        private void AdvanceTurns(int count)
        {
            if (turnClock == null)
            {
                return;
            }

            MethodInfo advanceMethod = typeof(GameTurnClock).GetMethod("AdvanceTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetMethod("NextTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (advanceMethod != null)
            {
                for (int i = 0; i < count; i++)
                {
                    advanceMethod.Invoke(turnClock, null);
                }
                return;
            }

            FieldInfo turnField = typeof(GameTurnClock).GetField("currentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetField("turn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo eventField = typeof(GameTurnClock).GetField("TurnAdvanced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            int cur = turnField != null ? (int)turnField.GetValue(turnClock) : 0;
            for (int i = 0; i < count; i++)
            {
                cur++;
                if (turnField != null)
                {
                    turnField.SetValue(turnClock, cur);
                }

                if (eventField != null && eventField.GetValue(turnClock) is System.Action<int> del)
                {
                    del.Invoke(cur);
                }
            }
        }
    }
}