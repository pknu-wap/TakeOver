# NPC 상태창 프리팹 연결 안내

- 프리팹: `Assets/NPC/Prefabs/NpcStatusScreen.prefab`
- 컴포넌트: `TakeOver.NPC.NpcStatusScreen`
- 기존 테스트 화면: `NpcFeatureTestScene` 및 `NpcInfoPanel`은 유지했다.

## 연결

1. 프리팹을 실제 게임 씬에 배치한다. 기본 상태는 비활성이다.
2. 연결 담당자가 상단 인물 Button의 OnClick에 씬에 배치한 프리팹 인스턴스의 `NpcStatusScreen.Open()`을 연결한다.
3. 화면 내부의 닫기 또는 Escape로 닫는다. 외부 화면 전환에서도 `Close()`를 호출할 수 있다.

이번 작업에는 상단 버튼 연결이나 씬 배치가 포함되지 않는다. 프리팹 에셋 자체가 아니라 **씬의 인스턴스**를 버튼에 지정한다.

## 데이터와 표시

- 활성 `NpcStateRegistry`를 자동 조회한다. 다른 저장소를 사용하려면 Inspector의 Registry 참조 또는 `Configure(NpcStateRegistry)`로 지정한다.
- 등록된 `Profiles` 전체가 이름/직책 버튼으로 표시된다. 가로 스크롤로 이동하며 한 명씩 선택한다.
- `SelectNpc(string npcId)`로 특정 인물을 선택할 수 있다. `SelectedNpcId`, `NpcCount`는 조회용이다.
- 선택은 이 화면에만 적용된다. 테스트창의 선택 인물이나 NPC 관계/기억을 수정하지 않는다.
- 현재 상태를 매번 조회하므로 열려 있는 동안 변경된 관계와 기억 수도 표시된다.

## 외형 유지 방식

기존 상태창의 **IMGUI 표시 코드 `NpcInfoPanel.Draw`를 그대로 공유**한다. 같은 초상화, 임시 실루엣, 관계 바, 소개, 스크롤, 1280×720 기준 배치와 화면 비율 보정을 사용한다. 테스트용 헤더/복귀 버튼은 일반 화면용 제목/닫기로 표시한다.

따라서 상태창의 각 글자/바가 별도 Canvas 자식으로 변환된 프리팹은 아니다. Canvas와 InputBlocker는 뒤의 게임 UI 클릭을 막는 역할이고, 양식 자체는 공용 표시 코드에서 그린다. 외형 변경이 필요하면 공용 `NpcInfoPanel`을 수정하면 테스트창과 이 프리팹에 함께 반영된다.
