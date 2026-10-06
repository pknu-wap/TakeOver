# NPC 작업 가이드

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

**현재는 반응 설정만 가능하며 실제 이벤트와 자동 연결되지는 않는다.** 추후 연결 코드에서 다음 함수를 호출한다.

```csharp
receiver.TryPublishChoice(eventId, choiceIndex, resultValue);
```

반환값 true는 채널 발행 완료를 뜻하며 실제 기억 추가 여부는 아니다. 기존 ReceiveChoice도 같은 의미로 사용할 수 있다.

같은 이벤트가 여러 번 발생한다면 네 번째 인수로 발생마다 고유한 `occurrenceId`를 전달한다. 같은 발생을 재전송할 때는 같은 ID를 사용한다.

NPC 소유 UnityEvent를 Inspector에서 연결할 때는 수신기의 **ReceiveConfiguredChoice()**를 선택하고 Configured 항목 3개를 채운다. 이벤트팀의 C# 알림에 연결하는 작업은 별도 코드가 필요하다.

## 테스트 화면 보기

`NpcLogic.prefab`의 **NpcFeatureTestScene > Show Test UI**를 켠다. NPC 선택, 관계·기억 조작, 턴 진행, 저장·불러오기를 사용할 수 있다.

Npc Definitions 목록에 새 NPC SO를 등록한 뒤 Play를 시작하면 선택 버튼이 자동으로 추가된다. NPC가 많으면 버튼 아래의 가로 스크롤바를 드래그하거나 버튼 영역 위에서 마우스 휠로 좌우 이동한다.
