# 스토리 이벤트 제작

스토리 이벤트는 `Assets/Resources/Story/StoryEventDatabase.asset`의 Events 목록에서 관리한다. 미래건설 본편 `inside_info_01`은 이 목록에 등록되어 있다. 지분 10% 조건과 ID는 유지하며, 현재 내용은 회사 운영 상황과 협력 방향을 논의하는 「미래건설의 협력 제안」이다. 상세 대본·세 가지 의도와 기억 효과는 `Docs/MIRAE_COOPERATION_EVENT_KO.md`를 따른다. 튜토리얼 자료 검토 이벤트는 별도 tutorialOnly 이벤트로 유지한다. 그린메일 등 일반 이벤트의 조건은 이번 작업에서 변경하지 않는다.

## 등록과 조건

1. Create > TakeOver > Events > Event Definition으로 에셋을 만들고 Category를 Story로 설정한다. 고유 Event Id, 대상 Company Ids, 화자, 대화, 선택지는 기존 이벤트와 같은 방식으로 작성한다.
2. 전용 DB Events에 등록한다. 일반 이벤트 목록에만 등록한 Story 이벤트는 실행하지 않는다. DB의 빈 참조·중복 ID·일반/튜토리얼 이벤트 혼입은 초기화 오류로 처리한다.
3. Conditions는 등장 조건이다. 모두 만족해야 카드가 나온다. 기존 지분·소비자 반응·실적·NPC 관계 외에 Day, EventCompleted, EventChoice, StorySucceeded, StoryFailed, PlayerHasMemory, PlayerLacksMemory를 사용할 수 있다. 플레이어 기억 조건과 결과별 기억 생성은 `Docs/PLAYER_MEMORY_KO.md`를 따른다.
4. 각 선택지의 Result Conditions는 선택 확정 시 다시 계산한다. 모두 만족하면 해당 선택지의 기본 결과, 하나라도 불만족하면 Failure Outcome의 대사·현금·NPC 관계/기억·기존 거래 효과를 적용한다. 조건이 없으면 기본 결과다. 불만족은 이벤트 미등장과 다르다. 실패 답변도 완료한 사건으로 기록된다.
5. Required Event Id는 등록된 선행 이벤트 ID다. EventChoice의 Required Choice Index는 0부터 센다. EventCompleted는 성공/실패와 무관한 답변 완료, StorySucceeded/StoryFailed는 스토리의 실제 조건별 결과다. Company Id가 비어 있으면 선행 사건의 마지막 선택, 지정하면 해당 회사의 선택을 조회한다(EventCompleted/EventChoice).
6. 지분·실적·관계 조건의 Company Id는 비워 두면 이벤트 대상 회사, 지정하면 그 회사다. 관계의 Npc Id가 비어 있으면 대상 회사 대표다. Day·수치 조건은 Comparison과 Value로 비교하며 선행 사건 조건은 완료/일치 여부를 직접 검사한다. 등장 조건이 성립한 사건은 날짜 시작 시 생성되며, 완료 직후의 후속 사건은 다음 날짜 시작에서 판정한다.

스토리는 회사별 반복 사건이 아니라 ID당 한 번 완료한다. 일반 이벤트의 회사별 완료 처리는 유지한다. OwnedCompanies 선택지 생성은 기존 주가조작 전용 흐름이므로 새 스토리에서는 Fixed를 사용한다. 새로운 경제 효과나 약속 이행 판정은 추가하지 않았다. 성공/실패 대사와 효과는 기획에 맞게 에셋에서 작성하며 임의의 후반 사건은 생성하지 않는다.

## 저장

현재 이벤트 저장 v3는 스토리 성공/실패·튜토리얼 보상 지급 여부와 필수 기한의 강제 진행/실패 상태를 함께 보존한다. v1/v2도 지원하며, v1의 새 결과는 미기록·보상은 미지급으로 명시 변환한다. 기존 완료 사건의 성공 여부를 추측하거나 구 저장을 읽는 순간 보상을 지급하지 않는다. 복원은 효과·보상을 재실행하지 않는다. 필수 기한과 엔딩 전환 신호는 `Docs/STORY_DEADLINE_KO.md`를 따른다. 튜토리얼 도중 이어하기는 이번 범위가 아니다.

## 튜토리얼 보상

정상 완료와 타이틀 전체 스킵은 같은 Finish 경로에서 미래건설 전체 주식의 5%를 기존 보유량에 무상 추가한다(현재 1,000주 중 50주). 필수 거래 후 남은 1주와 합쳐 51주/5.1%다. 현금·AP·날짜는 보상으로 변경하지 않는다. 주식 파생 수치를 즉시 갱신하고, 지급 완료 기록은 전체 저장에 포함한다.

공용 이벤트 패널·확인 버튼 복제로 보상 안내를 먼저 표시하고 확인 후 기존 도움말 위치 안내를 표시한다. 원본 이벤트 UI 프리팹·씬은 수정하지 않는다.
