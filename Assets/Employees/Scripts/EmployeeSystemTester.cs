using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TakeOver.NPC;

namespace TakeOver.Employees
{
    public class EmployeeSystemTester : MonoBehaviour
    {
        [SerializeField] private EmployeeService employeeService;
        [SerializeField] private GameTurnClock gameTurnClock;
        [SerializeField] private ActionPointService actionPointService;

        private void Start()
        {
            if (employeeService == null || gameTurnClock == null || actionPointService == null)
            {
                Debug.LogError("[Test] 인스펙터에 EmployeeService, GameTurnClock, ActionPointService를 모두 연결해 주세요.");
                return;
            }

            RunAllTests();
        }

        private void RunAllTests()
        {
            Test1_EmployeeDataAndGrowth();
            Test2_RecruitAndApplicants();
            Test3_SalaryCalculation();
            Test4_HiddenStatReveal();
            Test5_ContractRenewAndDismiss();

            Debug.Log("========== [Employee System] 모든 완료 조건 테스트 종료 ==========");
        }

        private void Test1_EmployeeDataAndGrowth()
        {
            Employee emp = new Employee(
                "테스트사원",
                1, 10,
                1, 10,
                0, 10,
                0, 10,
                0, 10,
                10, 10, 10,
                300, 0, 120,
                employeeService.Config);

            int gradeChangedCount = 0;
            employeeService.OnGradeChanged += (e) => { gradeChangedCount++; };

            employeeService.RaiseStat(emp, AbilityStatType.Analysis, 0.35f);
            employeeService.RaiseStat(emp, AbilityStatType.Analysis, 0.35f);
            employeeService.RaiseStat(emp, AbilityStatType.Analysis, 0.35f);

            bool progressOk = emp.Analysis.Current == 2 && Mathf.Approximately(emp.Analysis.Progress, 0.05f);
            bool potentialOk = emp.Potential == 50;

            for (int i = 0; i < 8; i++)
            {
                employeeService.RaiseStat(emp, AbilityStatType.Analysis, 1f);
            }

            bool midGradeOk = emp.Grade == CareerGrade.Mid && gradeChangedCount == 1;

            for (int i = 0; i < 30; i++)
            {
                employeeService.RaiseStat(emp, AbilityStatType.Network, 1f);
                employeeService.RaiseStat(emp, AbilityStatType.Eye, 1f);
                employeeService.RaiseStat(emp, AbilityStatType.Legwork, 1f);
            }

            bool vetGradeOk = emp.Grade == CareerGrade.Veteran && gradeChangedCount == 2;

            Debug.Log($"[1. 직원 데이터] 잠재력 일치: {potentialOk}, 진행치(0.35*3 -> 2 / 0.05): {progressOk}, 중견 승급: {midGradeOk}, 베테랑 승급: {vetGradeOk}");
        }

        private void Test2_RecruitAndApplicants()
        {
            employeeService.CompanyScaleStage = 3;
            employeeService.Capacity = 1;

            bool arrived = false;
            employeeService.OnApplicantsArrived += () => { arrived = true; };

            bool started1 = employeeService.StartRecruit();
            bool started2 = employeeService.StartRecruit();

            AdvanceTurns(2);

            int applicantCount = employeeService.Applicants.Count;
            bool hired1 = applicantCount > 0 && employeeService.Hire(employeeService.Applicants[0]);
            bool hired2 = employeeService.Applicants.Count > 0 && employeeService.Hire(employeeService.Applicants[0]);

            employeeService.CloseApplicants();
            bool closedOk = employeeService.Applicants.Count == 0;

            Debug.Log($"[2. 모집/지원자] 첫 모집 성공: {started1}, 중복 모집 차단: {!started2}, 2턴 뒤 도착: {arrived} ({applicantCount}명), 상한 내 고용: {hired1}, 상한 초과 차단: {!hired2}, 닫기 후 비움: {closedOk}");
        }

        private void Test3_SalaryCalculation()
        {
            Employee lowAmbition = new Employee("저야망", 10, 20, 10, 20, 10, 20, 10, 20, 10, 20, 10, 10, 0, 0, 0, 120, employeeService.Config);
            Employee highAmbition = new Employee("고야망", 10, 20, 10, 20, 10, 20, 10, 20, 10, 20, 10, 10, 20, 0, 0, 120, employeeService.Config);

            long lowSum = 0;
            long highSum = 0;
            int minSeen = int.MaxValue;
            int maxSeen = int.MinValue;

            for (int i = 0; i < 1000; i++)
            {
                int sLow = employeeService.CalculateMonthlySalary(lowAmbition);
                int sHigh = employeeService.CalculateMonthlySalary(highAmbition);
                lowSum += sLow;
                highSum += sHigh;

                if (sLow < minSeen) minSeen = sLow;
                if (sHigh > maxSeen) maxSeen = sHigh;
            }

            float avgLow = lowSum / 1000f;
            float avgHigh = highSum / 1000f;

            Debug.Log($"[3. 월급 계산] 고야망 평균({avgHigh:F1}) > 저야망 평균({avgLow:F1}): {avgHigh > avgLow}, 최소/최대 범위(350 기준 315~455 내): {minSeen} ~ {maxSeen}");
        }

        private void Test4_HiddenStatReveal()
        {
            EmployeeConfig cfg = employeeService.Config;
            int originalInterval = cfg.hiddenStatRevealIntervalTurns;
            cfg.hiddenStatRevealIntervalTurns = 3;

            int revealCount = 0;
            employeeService.OnStatRevealed += (emp, stat) => { revealCount++; };

            AdvanceTurns(12);

            Employee target = employeeService.Employees[0];
            bool allRevealed = employeeService.IsRevealed(target, HiddenStatType.Diligence)
                && employeeService.IsRevealed(target, HiddenStatType.Loyalty)
                && employeeService.IsRevealed(target, HiddenStatType.Ambition)
                && employeeService.IsRevealed(target, HiddenStatType.Potential);

            cfg.hiddenStatRevealIntervalTurns = originalInterval;

            Debug.Log($"[4. 숨은 스탯] 12턴 내 4개 모두 공개: {allRevealed} (이벤트 발생 횟수: {revealCount})");
        }

        private void Test5_ContractRenewAndDismiss()
        {
            Employee target = employeeService.Employees[0];
            int currentTurn = GetCurrentTurn();
            target.ContractEndTurn = currentTurn + 60;
            target.MonthlySalary = 400;

            int severance = employeeService.GetSeveranceAmount(target);
            bool severanceOk = severance == 800;

            int expiredEvents = 0;
            int leftEvents = 0;
            employeeService.OnContractExpired += (emp) => { expiredEvents++; };
            employeeService.OnEmployeeLeft += (emp) => { leftEvents++; };

            AdvanceTurns(60);
            bool expiredOk = expiredEvents == 1;

            employeeService.Decline(target);
            AdvanceTurns(30);
            bool leftOk = leftEvents == 1 && employeeService.Employees.Count == 0;

            Debug.Log($"[5. 계약/해고] 60턴 보상금(800): {severanceOk} ({severance}), 만료 이벤트 1회: {expiredOk}, 거절 30턴 후 퇴사: {leftOk}");
        }

        private void AdvanceTurns(int count)
        {
            MethodInfo advanceMethod = typeof(GameTurnClock).GetMethod("AdvanceTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetMethod("NextTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (advanceMethod != null)
            {
                for (int i = 0; i < count; i++)
                {
                    advanceMethod.Invoke(gameTurnClock, null);
                }
                return;
            }

            FieldInfo turnField = typeof(GameTurnClock).GetField("currentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetField("turn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo eventField = typeof(GameTurnClock).GetField("TurnAdvanced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            int cur = turnField != null ? (int)turnField.GetValue(gameTurnClock) : 0;
            for (int i = 0; i < count; i++)
            {
                cur++;
                if (turnField != null)
                {
                    turnField.SetValue(gameTurnClock, cur);
                }

                if (eventField != null && eventField.GetValue(gameTurnClock) is System.Action<int> del)
                {
                    del.Invoke(cur);
                }
            }
        }

        private int GetCurrentTurn()
        {
            FieldInfo turnField = typeof(GameTurnClock).GetField("currentTurn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? typeof(GameTurnClock).GetField("turn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return turnField != null ? (int)turnField.GetValue(gameTurnClock) : 0;
        }
    }
}