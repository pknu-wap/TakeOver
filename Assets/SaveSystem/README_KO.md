# SaveSystem 사용법

## 난수 사용

```csharp
using TakeOver.SaveSystem;

float result = FixedDaySeed.range(FixedDaySeed.seed, min, max);
```

- `FixedDaySeed.seed`: 현재 시드
- `min`, `max`: 난수의 최소·최대 범위
- 같은 시드로 반복 호출하면 난수 순서를 이어서 사용
- 최초 시드는 `GameSaveManager.Awake`에서 자동 생성
- 직접 새 시드를 만들 때는 `generateSeed()`, 특정 시드를 적용할 때는 `changeSeed(seed)` 호출
- 난수를 뽑을 때마다 시드 생성·변경을 반복하지 않기

## 저장 데이터 전달

1. 자기 파트의 컴포넌트에서 `ISaveParticipant` 구현
2. 자기 저장 데이터를 `captureJson()`에서 JSON 문자열로 반환
3. 해당 컴포넌트를 `GameSaveManager`의 Inspector `Participants` 목록에 등록

관리자가 저장할 때 각 컴포넌트의 `captureJson()`을 호출해 데이터를 수집하므로, 변수마다 관리자에 직접 전달할 필요 없음

`captureJson()`은 JSON 문자열을 반환하고, 실제 파일에는 내부 데이터도 들여쓰기된 JSON 객체로 기록

```csharp
// 자기 저장 데이터를 채운 뒤 JSON으로 반환
public string captureJson()
{
    MySaveData data = new MySaveData();
    data.money = money;
    data.actionCount = actionCount;
    return JsonUtility.ToJson(data);
}
```

`MySaveData`는 `[Serializable]`이 붙은 자기 파트의 저장용 클래스이며, `money`와 `actionCount`는 예시 변수

`ISaveParticipant`의 필수 구현 항목:

| 항목 | 내용 |
|---|---|
| `saveId` | 다른 파트와 겹치지 않는 고정 ID |
| `saveVersion` | 자기 저장 데이터의 형식 버전 |
| `captureJson()` | 저장할 데이터의 JSON 반환 |
| `prepareLoad(json, savedVersion, dayStart)` | JSON 해석·변환·검증 |
| `applyLoad()` | 준비된 데이터 적용 |
| `rebuildAfterLoad()` | 전체 적용 후 참조·계산값 갱신 |

모든 파트의 하루 마감 처리 완료 후, 다음 날짜의 행동을 받기 전에 저장 호출:

```csharp
GameSaveManager saveManager = FindFirstObjectByType<GameSaveManager>();
DayStartData nextDay = new DayStartData(nextDayNumber, FixedDaySeed.seed);
bool saved = saveManager.trySaveDayEnd(nextDay);
```

- `nextDayNumber`: 이어서 시작할 날짜
- `saved`: 저장 성공 여부
- 실패 이유는 `saveManager.lastError`에서 확인


## 2026-11-10 이벤트·NPC·AP 전체 저장 연결

GameSaveManager는 Inspector 등록 순서를 유지한 뒤 로드된 씬의 미등록 ISaveParticipant를 자동 보충합니다. 현재 trading/events/npc/action-points가 참여합니다. 서로 다른 컴포넌트의 중복 ID는 거부합니다. ISaveRebuildOrder 구현체는 기본 파트 이후 재구성 순서를 지정할 수 있으며 이벤트 카드의 순서는 100입니다.

미응답 면담 중 저장은 거부됩니다. 결과 확정 뒤 저장한 대화는 로드하면 닫히며 효과를 재지급하지 않습니다. 이벤트 구역이 없는 기존 전체 파일은 거부합니다. 기존 NPC 단독 저장 API는 유지됩니다. 전체 적용 전 검증, 적용 중 알림 금지, 전체 적용 후 UI 갱신을 따릅니다. 선행 회사 지표 PR의 구 저장 호환성까지 해결한 것은 아닙니다. 자세한 범위는 Docs/COMPANY_RULE_INTEGRATION_KO.md를 참고합니다.
