# 플레이어 기억과 일지 연결 계약

## 현재 기록

「미래건설의 협력 제안」 두 번째 선택(`inside_info_01`, 원본 선택 번호 1, `prepare_takeover`)을 확정하면 다음 기록이 한 번 생성된다.

- 키: `intent.hostile-takeover`
- 대상 회사: `mirae-construction`
- ID: `player:intent.hostile-takeover:mirae-construction`
- 제목: 미래건설 공격적 인수 검토
- 유형: Intent(의도), 영구, 플레이어 비공개
- 내용: 협력하는 태도를 보이며 자금 일정·의사결정 구조를 살피고 공격적 인수의 기회를 검토하기로 했다는 기억. 실제 인수·계약 완료를 뜻하지 않는다.
- 출처: 원본 사건 ID, 선택 번호, 발생 ID, 발생 일차를 함께 보존한다.

협력 검토/거절은 이 기억을 만들지 않는다. 서지아는 기존대로 겉으로 표현된 협력 의향만 기억하며, 플레이어의 속마음은 NPC 채널·관계·등록 명단에 추가하지 않는다. 주인공을 NPC로 등록하지 않고 기존 행위자 ID `player`를 소유자로 사용한다. 턴 경과·NPC 기억 만료·지분 매도는 이 영구 기록을 지우지 않는다.

## 읽기·알림

Player에 자동 연결되는 `PlayerMemoryService`가 데이터를 소유한다. 일지·엔딩 등 소비자는 `IPlayerMemoryReader`를 통해 읽는다. 서비스는 이벤트 UI·NPC 레지스트리·일지 화면에 종속되지 않는다.

```csharp
IPlayerMemoryReader reader = player.GetComponent<PlayerMemoryService>();
bool preparingMiraeTakeover = reader.HasMemory("intent.hostile-takeover", "mirae-construction");
var entries = reader.GetMemories();
reader.MemoryAdded += OnMemoryAdded;
reader.MemoriesRestored += OnMemoriesRestored;
```

- `HasMemory(key, companyId)`는 회사별 조건이다. 회사 인수를 생략하면 해당 키의 모든 회사 기록 중 존재 여부를 조회한다. 빈 문자열을 전달하면 회사 없는 전역 기록만 조회한다.
- `GetMemories()`는 불변 기록을 담은 읽기 전용 스냅샷이다. 제목·내용·유형·회사·영구 여부·발생 일차·출처 종류/ID/발생 ID/선택 번호·태그를 제공한다. 일지는 필요에 따라 일차와 ID로 정렬하거나 태그로 분류한다. 과거 회사의 기록을 유지할 수 있도록 현재 회사 목록 조회를 필수로 요구하지 않는다.
- `MemoryAdded`는 새 영구 기록에만 전달한다. 동일 키+회사 중복은 최초 기록을 유지하고 다시 알리지 않는다. 전체 선택의 NPC/현금 처리 후 알리며, 선택 확정 구독자에게는 이미 기억이 반영된 상태를 제공한다.
- `MemoriesRestored`는 전체 저장 적용 후 현재 목록을 전달한다. 일지는 기존 표시를 재구성한다. 로드를 신규 추가 알림으로 재생하지 않아 중복 일지·효과를 방지한다. 구독 실패는 다른 구독자나 게임 선택 확정을 중단하지 않는다.
- 소비자는 비활성/파괴 시 구독을 해제하고 새 게임/씬 변경 시 새 Player의 읽기 인터페이스에 연결한다. 저장소는 플레이 회차를 넘어 자동 공유하지 않는다.

현재 일지 UI는 구현하지 않는다. 일지가 자체 데이터로 기억을 중복 저장하기보다 `MemoryId`를 외래 키로 삼아 읽기 모델을 구성하는 방식을 권장한다. 영구 기억은 과거 기록이며, 향후 의도 변경·포기 같은 사실은 별도 결정 기록으로 표현할 수 있다.

## 새 사건과 분기

`EventOutcome.Player Memories` 목록에 `PlayerMemoryGrant`를 넣으면 성공/불만족 결과별로 플레이어 기억을 배정할 수 있다. 키·제목·설명·유형·영구 여부·회사·태그를 지정한다. 회사가 비어 있으면 사건의 대상 회사를 쓴다. 원본 템플릿과 저장된 기록은 독립 사본이다. 현재 v1은 영구 비공개 기록을 지원하며 임시 수명/공개 전파는 별도 확장 범위다.

EventCondition에 `PlayerHasMemory` / `PlayerLacksMemory`를 추가했다. `Player Memory Key`와 선택적 `Company Id`를 지정하며 Value/Comparison은 사용하지 않는다. Company Id를 비우면 모든 회사 중 존재 여부를 검사한다. 이벤트 등장 조건과 각 선택지의 Result Conditions에서 같은 방식으로 사용한다. 기존 만족/불만족 응답·효과 경로를 재사용하므로 향후 선택 결과를 기억에 따라 바꿀 수 있다. 아직 실제 후속 사건이나 엔딩 분기를 임의로 추가하지 않았다. 엔딩 담당자는 같은 읽기 인터페이스를 사용하면 된다.

직접 기록하는 다른 시스템은 `Record(grants, PlayerMemoryOrigin)`을 사용할 수 있다. 출처 종류는 EventChoice/Story/System을 구분하며, 의도·관찰·결정·결과 유형과 태그를 제공한다. 새 기록은 완전한 검증 후 반영한다. 표시 문자열을 조건 키로 사용하지 않는다.

## 저장 호환

- 전체 저장 형식 v3에 `player-memories` 구역 v1을 추가했다. 이벤트 구역 버전은 기존 v3를 유지한다. 저장 관리자는 모든 구역을 검증한 다음 적용하며 기존 기업 통계·NPC·이벤트·AP 검증을 완화하지 않는다.
- 이전 전체 저장 v2에는 새 구역만 명시적으로 빈 목록으로 초기화한다(`ILegacySaveSectionInitializer`). 구 이벤트/NPC/거래 구역 누락은 계속 거부한다. 새 형식 v3에서 플레이어 기억 구역이 없으면 손상된 저장으로 거부한다.
- 과거 저장의 선택 번호만으로 적대적 의도를 소급 생성하지 않는다. 같은 사건·선택 번호의 과거 대본 의미가 달랐고 당시 기억 생성 여부/출처를 확인할 수 없기 때문이다. 기존 선택·보상·관계 기록은 보존한다. 새 영구 기억은 변경 이후 실제 선택부터 생성된다.
- 영구 여부·소유자·유형·출처·일차·필수 항목·중복 ID를 검증한다. 불량 기억 때문에 전체 저장이 부분 적용되지 않는다. 향후 기록 스키마를 확장할 때 구역 버전과 변환을 명시한다.
