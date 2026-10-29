# NPC 작업 가이드

## 실제 이벤트 선택 연결 — 2026-10-27

미리보기의 제목·설명·대사·선택지·결과 대사는 `TodayEventInfo`의 공통 치환 규칙을 사용한다. 회사별 사건은 현재 NPC 회사의 등록된 CEO/소속을 조회해 `{company}`, `{speaker}`, `{speakerRole}`을 채운다. 외부 인물의 회사 사건은 현재 발생 회사 또는 테스트용 첫 실제 회사를 사용한다. 원본 이벤트 SO 문구는 바꾸지 않는다.

NPC ID는 `Docs/ID_CONVENTIONS_KO.md`의 표를 따른다. 등록 NPC 14명 모두 NPC팀 형식인 `npc-{companyID}-{직책}-{성}-{이름}`(외부 인물은 companyID 생략)을 사용한다. 성은 통용 표기, 이름은 국어 로마자 표기법을 따른다. 예: `npc-haneul-food-ceo-kim-harin`. 정수아는 `npc-investigative-reporter-jung-sua`, 김태성은 `npc-institutional-investor-kim-taeseong`이다. 기존 에셋 파일명·SO 내부 이름·`.meta` GUID·한글 표시명은 유지한다.

현재 관계 범위는 신뢰·존중 -100~100, 두려움·적대감·의존도·관심 0~100이다. 적대감·관심은 사건 적용 및 이전 저장 불러오기에서 0~100으로 제한하며 정보 화면 게이지도 같은 범위를 사용한다. 음수 변화량은 기존 수치를 감소시키는 입력으로 유지한다.

### NPC별 이벤트 미리보기

테스트 화면에서 NPC를 선택하고 **이벤트 미리보기**를 연다. 현재 NPC가 어느 선택에서든 반응 대상으로 등록된 실제 이벤트가 이름별 버튼으로 표시된다. 이벤트 이름을 누르면 원본 선택지를 확인할 수 있다.

- **선택 결과 테스트:** 등장 조건을 우회해 실제 이벤트 SO의 선택 결과를 NPC 수신기·채널·처리기로 전달한다. 회사별 사건은 현재 NPC 회사의 대상만 남긴다. 매번 고유 발생 ID를 사용하므로 반복 비교가 가능하며 관계·기억은 누적된다. 결과 대사, 대상별 관계 6축의 실제 변화와 생성 Memory를 표시한다. AP·현금·지분·실제 이벤트 완료 상태는 변경하지 않는다.
- **실제 이벤트 화면 열기:** 현재 회사에서 등장 중인 사건을 실제 상세 화면으로 연다. NPC 화면은 잠시 숨겨지고 **NPC 테스트로 돌아가기**로 복귀한다. 실제 화면에서 선택하면 게임의 이벤트 진행이 바뀐다. 등장하지 않았거나 이미 완료된 사건은 안내를 표시한다.

선택 결과 비교 전 **선택 NPC 초기화** 또는 **전체 NPC 초기화**로 관계·기억을 비울 수 있다. 테스트 결과 저장은 별도다. NPC 프리팹의 `previewEvents`는 현재 이벤트 7개 원본을 읽는 테스트 카탈로그이며 자동 이벤트 수신에는 영향을 주지 않는다.

`NpcEventResultReceiver`가 활성 이벤트 컨트롤러를 코드로 찾아 `choiceConfirmed`를 구독한다. 이벤트 알림의 `npcResult`를 복사하여 기존 NPC 채널 → 처리기 → 관계/Memory 경로로 전달한다. 이벤트 SO의 효과와 발생 ID를 유지하며, NPC 효과가 없는 선택은 건너뛴다. 비활성화 시 구독을 해제한다. 이벤트팀 파일이나 Inspector 참조 추가는 필요 없다.

이 자동 경로는 아래 수동 반응 SO 목록을 사용하지 않는다. 같은 결과를 수동으로도 발행하면 별도 발생으로 계산될 수 있으므로 중복 연결하지 않는다. 행동 ID가 비어 있는 이벤트에 선호 행동을 임의로 붙이지 않으며 기존 결과 Memory 선호만 적용한다.

`TakeOver > NPC > Verify Event Connection`은 현재 Game 씬에서 인터뷰 2개/내부 정보 제보 3개 선택을 실제 컨트롤러로 실행하고 관계·기억·중복·재구독·저장 복원을 검사한다. 씬을 저장하지 않고 Play 종료로 테스트 상태를 버린다. 결과는 workspace `psm/event-connection-verification.txt`, 전용 저장은 `psm/event-connection-test-save.json`이다. 검증 중 NPC 상태와 이벤트 진행을 초기화하므로 기존 Play 세션을 끝낸 후 실행한다.

## 2026-10-27 · ID·직책·선호·정보 화면

현재 프리팹은 14명이다. 작업표의 이벤트 등장 12명과 기존 정수아·김태성 2명을 포함한다. 회사 ID는 `mirae-construction`, `haneul-food`, `dongbang-trading`, `hansung-industry`이며 외부 인물은 `external-network`다.

`seed.position`은 검색용 `NpcPosition { None, CEO }`다. 강준혁·김하린·강재현·조민석만 CEO다. 표시용 `profile.role`은 유지하며 `registry.TryFindByPosition(companyId, NpcPosition.CEO, out var seed)`로 유일한 CEO를 조회한다.

신규 인물 설정은 `Reference/TakeOver_NPC_기업구조_기획서_v3.pdf`의 하늘식품 13쪽, 강재현·박민우 18~19쪽, 한성공업 20쪽, 이민규 40쪽을 참고했다. 강재현·박민우의 회사 배정은 사용자 제공 `TakeOver_ID_통일_작업표.txt`의 동방물산을 우선했다. 원자료는 변경하지 않았다.

### 선호 반응 — 임시 규칙

- `seed.preferenceResponse.enabled`를 켠 NPC만 배율이 적용된다. 현재 서지아만 활성이고 배율은 선호/불호 각각 1.5다. 기존 인물의 선호 목록·고정 보정은 유지하며 신규 7명의 수치 선호는 비워 두었다.
- 대상별 결과 → 기존 프로필 고정 보정 → 선호 배율 → Memory 기록/중복 방지 순서다. `actionId` 선호가 우선이며 없으면 `seed.memoryPreferences`로 결과 Memory를 판단한다. 둘을 중복 곱하지 않는다.
- 선호는 신뢰·존중·관심 증가와 적대감 감소, 불호는 반대 방향을 확대한다. 나머지 방향·0·공포·의존도는 유지한다. 기존 상한/하한과 기억 강도·수명·영향력 규칙도 유지한다.
- 이벤트 도중 행동도 `NpcEventChannel.Publish`에 행동 ID, `Custom` Memory, 기본 효과를 주면 반응한다. 행동과 최종 결과는 각각 고유 발생 ID를 사용한다. 같은 발생 재전송에만 동일 ID를 사용한다. 실제 선택 확정 알림의 자동 구독은 위 연결 항목을 따른다.
- **선호·불호** 탭의 데모 버튼으로 자금 계획/거짓 재무/중립 행동 및 약속 이행/계약 위반 결과를 비교한다. 서지아의 기본 ±4가 ±6으로 변하며 실제 변화량이 표시된다. 기존 testEvent는 이 데모에서 변경하지 않는다. 저장은 별도다.

### 마지막 사건의 선호 보너스 로그

테스트 화면 하단의 마지막 사건 로그는 사건 당시 분류/배율, 기본 변화량(프로필 고정 보정 포함), 선호·불호 추가분, 최종 요청량과 상한/하한 적용 후 실제 반영량을 구분한다. 예를 들어 신뢰 기본 +4, 선호 추가 +2, 최종 +6을 확인할 수 있다. 불호의 추가 하락도 음수로 표시한다. 중립·비활성·변화가 없는 경우도 구분한다.

상세 내역은 Memory와 함께 저장된다. 현재 SO를 다시 읽어 과거 수치를 추정하지 않으며, 상세 내역이 없는 기존 기록은 '상세 기록 없음'으로 표시한다. 긴 기록 ID와 로그는 해당 영역에서 스크롤하고 저장 상태 표시는 별도 영역을 유지한다.

### NPC 정보 화면 사용

테스트 화면의 **NPC 정보 확인** 버튼을 누르면 왼쪽에 스탠딩, 오른쪽에 현재 관계 6축과 인물 설정이 표시된다. 상단 인물 선택, 설명 스크롤, 테스트 화면 복귀를 지원한다. 설명의 성향 단서와 실제 수치 선호 확정은 구분한다.

SO의 `seed.profile.standingIllustration`에 Sprite를 지정한다. 원화 미등록 시 임시 실루엣을 표시하며 확정 외형이 아니다. 아틀라스는 Full Rect 패킹을 권장한다.

저장·조회·이벤트 입력은 현재 SO의 정식 ID만 사용한다. 구 ID 별칭이나 자동 변환은 두지 않는다. 저장의 NPC/회사 ID가 현재 프로필과 다르면 로드를 거부하고 현재 상태를 유지한다. 구 형식 저장은 별도 변환 없이는 불러올 수 없다.

**TakeOver > NPC > Verify in Unity Play Mode**에서 Game 씬을 저장하지 않고 런타임 검증을 재실행한다. 별도 검증 세이브를 사용하며 결과는 workspace `psm/unity-verification/`에 남긴다. 대시보드와 AP는 이번 범위에서 제외했다.

## 새 NPC 만들기

1. `Assets/NPC/Data/Profiles/NpcTemplate.asset`을 복제하고 파일 이름을 바꾼다.
2. 에셋을 선택해 Inspector에서 정보를 입력한다.
3. `Assets/NPC/Prefabs/NpcLogic.prefab`을 열고, **NpcStateRegistry > Npc Definitions** 목록에 에셋을 추가한다.

기존 NPC를 수정할 때는 같은 폴더의 해당 인물 에셋을 선택하면 된다.

| 항목 | 입력할 내용 |
|---|---|
| Npc Id | NPC 고유 ID. 다른 NPC와 중복되지 않게 지정 |
| Company Id | 소속 회사 또는 외부 인물 그룹의 ID |
| Profile | 이름, 역할, 소속, 성격, 게임에서 맡는 역할 |
| Action Preferences | 행동 ID와 해당 행동의 선호·불호, 이유 |
| Any Event Relation Delta | 모든 사건에 추가할 관계 변화량 |
| Event Relation Biases | 특정 Memory 유형에 추가할 관계 변화량 |

잘못된 ID나 중복 등록, 반응 설정 오류는 Inspector에 표시된다. 표시된 원인을 확인해 직접 수정한다.

관계 변화량은 아직 임시 기획값이다. 비어 있는 양식 에셋은 목록에 등록하지 않는다.

**NPC 에셋은 인물 설정용이다.** 플레이 중 바뀌는 관계와 기억은 별도로 관리하므로 에셋에 직접 입력하지 않는다.

## 이벤트 반응 만들기

1. Project 창에서 **Create > TakeOver > NPC > Choice Reaction**을 선택한다.
2. 어떤 선택 결과에 반응할지 입력한다.
   - **Source Event Id:** 이벤트 ID
   - **Choice Index:** 선택지 번호. 첫 번째는 0, 두 번째는 1
   - **Result Value:** 이벤트에서 전달하는 결과값
3. **Npc Event**에 반응할 NPC ID와 Memory·관계 효과를 입력한다. 여러 NPC의 반응이 다르면 **Target Reactions**에 각각 설정한다.
4. `NpcLogic.prefab`의 **NpcEventResultReceiver > Reactions** 목록에 반응 에셋을 추가한다.

이벤트 ID·선택지 번호·결과값이 모두 일치해야 반응한다. 같은 조건의 에셋은 중복 등록하지 않는다.

**아래 API는 수동 반응 SO용이다.** 실제 이벤트의 내장 NPC 결과는 위 자동 경로로 연결하며, 별도 생산자가 반응 SO를 사용할 때 다음 함수를 호출한다.

```csharp
receiver.TryPublishChoice(eventId, choiceIndex, resultValue);
```

반환값 true는 채널 발행 완료를 뜻하며 실제 기억 추가 여부는 아니다. 기존 ReceiveChoice도 같은 의미로 사용할 수 있다.

같은 이벤트가 여러 번 발생한다면 네 번째 인수로 발생마다 고유한 `occurrenceId`를 전달한다. 같은 발생을 재전송할 때는 같은 ID를 사용한다.

NPC 소유 UnityEvent를 Inspector에서 연결할 때는 수신기의 **ReceiveConfiguredChoice()**를 선택하고 Configured 항목 3개를 채운다. 이벤트팀의 C# 알림에 연결하는 작업은 별도 코드가 필요하다.

## 테스트 화면 보기

`NpcLogic.prefab`의 **NpcFeatureTestScene > Show Test UI**를 켠다. NPC 선택, 관계·기억 조작, 턴 진행, 저장·불러오기를 사용할 수 있다.

Npc Definitions 목록에 새 NPC SO를 등록한 뒤 Play를 시작하면 선택 버튼이 자동으로 추가된다. NPC가 많으면 버튼 아래의 가로 스크롤바를 드래그하거나 버튼 영역 위에서 마우스 휠로 좌우 이동한다.

- **선호·불호:** 선택한 NPC의 행동 ID, 선호 여부와 이유를 확인한다.
- **이벤트 미리보기:** Reactions에 등록한 반응 SO를 선택하고 **선택 반응 수동 발행**을 누른다. 대상 NPC, 실제 관계 변화와 생성된 Memory가 표시된다.

미리보기는 반응 SO에 지정된 NPC의 런타임 상태를 실제로 바꾼다. 클릭마다 별도 발생으로 처리하며, 파일에 남기려면 저장 버튼을 누른다. 실제 이벤트와 자동 연결되지는 않는다.
