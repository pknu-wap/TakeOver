# SaveSystem 사용법

## 현재 저장 흐름 — 2026-11-23

전체 저장 형식은 v5다. 자동 저장은 새 날짜의 사건 배정, 사건 결과 종료, 직원 변경, 주식 매매 및 문서 열람·결재를 반영한다. 사건의 답변 전 AP 차감 직전에도 진행을 기록한다. 같은 날짜를 불러올 때 배정 사건과 시드를 복원하며 다시 뽑지 않는다.

상단 **저장**에서 수동 저장 3칸을 보관·불러올 수 있고, 타이틀 **불러오기**에서도 고를 수 있다. 수동 저장은 `manual/1/progress.json`~`manual/3/progress.json`이며 자동 저장 `progress.json`과 독립이다. 덮어쓰기 및 불러오기는 확인창을 거친다. 튜토리얼·이벤트·계약 진행 중이거나 저장 실패 재처리가 남아 있으면 수동 저장을 받지 않는다. 원자적 파일 교체 경로를 공통으로 사용한다.

인원 지원은 직원 직무 구역에 대상 회사 ID 한 개와 `Support`, 남은 일수 0으로 보관한다. 상장·폐업·직원 이탈 등으로 대상이 없어지면 배치를 해제한다. 회사 기록의 `pendingContractDay`는 투자 검토 이후 예정된 계약을 복원하며, 없는 구형 필드는 0으로 읽힌다. NPC ID는 변경하지 않았다.

아래의 v3 설명은 플레이어 기억 저장이 도입된 당시 기록이다.

## 플레이어 기억 구역과 전체 저장 v3 — 2026-11-15

Player의 `PlayerMemoryService`가 `player-memories` 구역 v1을 소유한다. 일지는 이 저장소의 읽기 인터페이스와 추가/복원 알림을 사용한다. 도입 당시 전체 저장 형식은 v3이며, 이전 v2는 새 플레이어 기억 구역만 빈 목록으로 초기화하는 명시 변환을 지원한다(`ILegacySaveSectionInitializer`). 다른 필수 구역 누락이나 통계/enum 검증은 완화하지 않는다. v3에서 플레이어 기억 구역 누락은 손상으로 거부한다. 당시 이벤트 구역 버전은 v3였다. 상세 계약은 `Docs/PLAYER_MEMORY_KO.md`를 따른다.

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
