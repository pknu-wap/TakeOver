# SaveSystem 사용법

## 난수 사용

```csharp
using TakeOver.SaveSystem;

float result = FixedDaySeed.Range(FixedDaySeed.Seed, min, max);
```

- `FixedDaySeed.Seed`: 현재 시드
- `min`, `max`: 난수의 최소·최대 범위
- 같은 시드로 반복 호출하면 난수 순서를 이어서 사용
- 최초 시드는 `GameSaveManager.Awake`에서 자동 생성
- 직접 새 시드를 만들 때는 `GenerateSeed()`, 특정 시드를 적용할 때는 `ChangeSeed(seed)` 호출
- 난수를 뽑을 때마다 시드 생성·변경을 반복하지 않기

## 저장 데이터 전달

1. 자기 파트의 컴포넌트에서 `ISaveParticipant` 구현
2. 자기 저장 데이터를 `CaptureJson()`에서 JSON 문자열로 반환
3. 해당 컴포넌트를 `GameSaveManager`의 Inspector `Participants` 목록에 등록

관리자가 저장할 때 각 컴포넌트의 `CaptureJson()`을 호출해 데이터를 수집하므로, 변수마다 관리자에 직접 전달할 필요 없음

`CaptureJson()`은 JSON 문자열을 반환하고, 실제 파일에는 내부 데이터도 들여쓰기된 JSON 객체로 기록

```csharp
// 자기 저장 데이터를 채운 뒤 JSON으로 반환
public string CaptureJson()
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
| `SaveId` | 다른 파트와 겹치지 않는 고정 ID |
| `SaveVersion` | 자기 저장 데이터의 형식 버전 |
| `CaptureJson()` | 저장할 데이터의 JSON 반환 |
| `PrepareLoad(json, savedVersion, dayStart)` | JSON 해석·변환·검증 |
| `ApplyLoad()` | 준비된 데이터 적용 |
| `RebuildAfterLoad()` | 전체 적용 후 참조·계산값 갱신 |

모든 파트의 하루 마감 처리 완료 후, 다음 날짜의 행동을 받기 전에 저장 호출:

```csharp
GameSaveManager saveManager = FindFirstObjectByType<GameSaveManager>();
DayStartData nextDay = new DayStartData(nextDayNumber, FixedDaySeed.Seed);
bool saved = saveManager.TrySaveDayEnd(nextDay);
```

- `nextDayNumber`: 이어서 시작할 날짜
- `saved`: 저장 성공 여부
- 실패 이유는 `saveManager.LastError`에서 확인
