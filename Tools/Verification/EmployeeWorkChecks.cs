using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TakeOver.Employees;
using TakeOver.Startups;
using TakeOver.Documents;
using TakeOver.CompanyRules;
using TakeOver.NPC;

class FirstRandom : System.Random
{
    public override int Next(int minValue, int maxValue) => minValue;
}
class LastRandom : System.Random
{
    public override int Next(int minValue, int maxValue) => maxValue - 1;
}

int checks = 0;
void check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void near(float actual, float expected, string message) => check(Math.Abs(actual - expected) < .001f, message);
MarketAppraisal.Direction direction(int sum) => sum > 0 ? MarketAppraisal.Direction.Up : sum < 0 ? MarketAppraisal.Direction.Down : MarketAppraisal.Direction.Unknown;

check(MarketAppraisal.getCompanyLimit(0) == 1, "발품 0");
check(MarketAppraisal.getCompanyLimit(10) == 3, "발품 10");
check(MarketAppraisal.getCompanyLimit(20) == 5, "발품 20");
check(MarketAppraisal.getIndicatorCount(0) == 2, "분석력 0");
check(MarketAppraisal.getIndicatorCount(10) == 4, "분석력 10");
check(MarketAppraisal.getIndicatorCount(20) == 6, "분석력 20");
string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
for (int combination = 0; combination < 729; combination++)
{
    float[] signs = new float[6];
    int code = combination;
    for (int i = 0; i < 6; i++) { signs[i] = code % 3 - 1; code /= 3; }
    var result = MarketAppraisal.evaluate(signs, 6, new System.Random(combination));
    check(result.performance == direction((int)(signs[0] + signs[1] + signs[2])), "실적 다수결 " + combination);
    check(result.market == direction((int)(signs[3] + signs[4] + signs[5])), "시장 다수결 " + combination);
}
var tie = MarketAppraisal.evaluate(new float[] { 1, -1, 1, 1, 1, 1 }, 2, new FirstRandom());
check(tie.performance == MarketAppraisal.Direction.Unknown && tie.market == MarketAppraisal.Direction.Unknown, "동점·미조사");
var neutral = MarketAppraisal.evaluate(new float[] { 0, 1, -1, -1, -1, -1 }, 2, new FirstRandom());
check(neutral.performance == MarketAppraisal.Direction.Up && neutral.market == MarketAppraisal.Direction.Unknown, "중립 표 제외");
var sampleA = MarketAppraisal.evaluate(new float[] { 1, 1, 1, -1, -1, -1 }, 2, new FirstRandom());
var sampleB = MarketAppraisal.evaluate(new float[] { 1, 1, 1, -1, -1, -1 }, 2, new LastRandom());
check(sampleA.market != sampleB.market, "재조사 지표 변경");
check(JsonUtility.ToJson(UnityEngine.Random.state) == randomBefore, "기존 Unity 난수 미소비");
near(StartupContractDocument.calculateInvestment(1000f, .2f), 250f, "20% 투자금");
near(StartupContractDocument.calculateInvestment(1000f, .3f), 428.57143f, "30% 투자금");
near(StartupContractDocument.calculateInvestment(1000f, .4f), 666.6667f, "40% 투자금");

var fixture = new GameObject("Employee work logic fixture");
fixture.SetActive(false);
var definitions = new System.Collections.Generic.List<CompanyDefinition>();
try
{
    var player = fixture.AddComponent<Player>();
    typeof(Player).GetField("<state>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, new PlayerState(10000f, 10f, 5f, 0f));
    var companies = fixture.AddComponent<Companies>();
    var clock = fixture.AddComponent<GameTurnClock>();
    int id = 0;
    foreach (float ratio in new[] { .2f, .3f, .4f })
    {
        var definition = ScriptableObject.CreateInstance<CompanyDefinition>();
        definitions.Add(definition);
        definition.companyID = "verification-" + id++;
        definition.companyName = "검증 회사";
        definition.totalShares = 1000;
        definition.initialCash = 200f;
        definition.initialRevenue = 100f;
        definition.initialCost = 80f;
        var child = new GameObject(definition.companyID);
        child.SetActive(false);
        child.transform.SetParent(fixture.transform);
        var company = child.AddComponent<Company>();
        company.initialize(definition);
        company.state.setDerivedStat(CompanyDerivedStat.FUNDAMENTAL_VALUE, 1000f);
        company.state.setDerivedStat(CompanyDerivedStat.REVENUE_GROWTH, 1f);
        company.state.setDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH, 1f);
        company.state.setDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND, -1f);
        company.state.setStat(CompanyStat.REPUTATION_B2C, 51f);
        company.state.setStat(CompanyStat.REPUTATION_B2B, 49f);
        company.state.setStat(CompanyStat.MARKET_POSITION, 50f);
        var appraisal = MarketAppraisal.evaluate(company, 20, new FirstRandom());
        check(appraisal.performance == MarketAppraisal.Direction.Up && appraisal.market == MarketAppraisal.Direction.Unknown, "회사 지표 연결");
        check(companies.RegisterStartup(company), "스타트업 등록");
        player.state.addCash(10000f - player.state.cash);
        var document = new StartupContractDocument(company, player, companies, 1);
        document.SetField("stake", ratio);
        check(document.CanApprove(out _), "계약 가능");
        float cash = player.state.cash;
        float cost = document.investment;
        var applier = new ChangeApplier(player);
        applier.Apply(document.OnReject(), company);
        near(player.state.cash, cash, "거절 시 현금 유지");
        near(company.state.getStat(CompanyStat.CASH), 200f, "거절 시 회사 현금 유지");
        near(company.state.getStat(CompanyStat.PLAYER_SHARES), 0f, "거절 시 지분 유지");
        applier.Apply(document.OnApprove(), company);
        near(player.state.cash, cash - cost, "플레이어 투자금 차감");
        near(company.state.getStat(CompanyStat.CASH), 200f + cost, "스타트업 동일 투자금 수령");
        company.calculator.calculateAboutShares(company);
        near(company.state.getDerivedStat(CompanyDerivedStat.STAKE), ratio * 100f, "취득 지분");
        check(!document.CanApprove(out _), "최초 투자 제한");
        company.state.setStat(CompanyStat.PLAYER_SHARES, 0f);
        player.state.addCash(-player.state.cash);
        check(!document.CanApprove(out _), "자금 부족 제한");
    }
    var inbox = new DocumentInbox(new ChangeApplier(player), clock, 20);
    var rows = new[] { new DocumentInfo("회사 A", "실적 추세: 오를 쪽"), new DocumentInfo("회사 B", "시장 평가: 알 수 없음") };
    var report1 = new SnapshotDocument("market-appraisal", "시세 예측 보고서", "직원", rows);
    var report2 = new SnapshotDocument("market-appraisal", "시세 예측 보고서", "직원", rows);
    check(inbox.Add(report1) && inbox.Add(report2), "보고서 중복 등록");
    check(!inbox.HasOverdue(int.MaxValue), "읽기 전용 보고서 기한 차단 없음");
    inbox.MarkRead(report1);
    check(inbox.Open.Count == 1 && inbox.History.Count == 1, "읽음 처리");
    var settings = AssetDatabase.LoadAssetAtPath<DocumentSettings>("Assets/Operations/Data/DocumentSettings.asset");
    var restored = new DocumentInbox(new ChangeApplier(player), clock, 20);
    restored.RestoreFrom(inbox.ToSaveRecords(), new DocumentRegistry(), new DocumentContext(companies, settings));
    check(restored.Open.Count == 1 && restored.History.Count == 1 && restored.Open[0].InfoRows.Count == 2, "보고서 저장 복원");
    var approval = new DividendPolicyDocument(companies.StartupList[0], 2, settings);
    restored.Add(approval);
    check(!restored.HasOverdue(1) && restored.HasOverdue(2), "기존 승인 문서 기한 차단 유지");
}
finally
{
    UnityEngine.Object.DestroyImmediate(fixture);
    foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
}
return new { checks, fullIndicatorCombinations = 729, result = "passed" };
