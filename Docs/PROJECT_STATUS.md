# 2026-11-11 스토리 분기 제외 이벤트·저장 연동 보완 — 최신

- 유료 선택은 확정 전 잔액 검사, 실패 시 안내/재선택과 무효과. EventSystemController partial ISaveParticipant로 선택/회사별 선택/발생·기한/재발생 제한 보존. 미응답 면담 저장 거부, 완료 후 로드에서 효과 재지급 없음.
- NpcSaveService의 기존 단독 저장 유지 + 전체 저장 계약 추가. AP도 참여. GameSaveManager는 명시 등록 뒤 씬 ISaveParticipant 자동 보충, 중복 ID 거부, 이벤트 재구성 순서100. 거래/NPC 복원 후 카드 재구성, 대시보드 난수 상태 보존. 전체 검증 후 적용. 부분 없는 구 전체 저장은 거부, stats.Count 유지.
- 메뉴 DashboardTab 식별자 분리, StoryDirector 탐색 조건은 같은 의미로 enum 경로 사용. ITurnAdvanceGuard로 모든 AdvanceTurn 경로의 사전 차단/콜백 준비. 실제 운영 탭/문서 마감 UI는 미구현 상태 유지. 도움말 유료 선택 안내와 서지아/윤하진 상충 문서 정리.
- QA_EventPersistence/RESULT_KO.md: 신규25/NPC184/연결603/정상1002/스킵56/도움말37 = 1907 PASS, FAIL0. 실제 씬 + 별도 progress.json 전체 저장/로드·불량 파일 거부·원자성·기한/난수/완료/현금/매수금지/AP 검증. Windows 빌드 오류0/경고0, 최신 QA_NarrativeRevision/Build/TakeOver.exe (Assembly-CSharp.dll 2026-11-11 00:10). 실행 파일 수동 시연은 이번에 하지 않음.
- 회사 지표 PR #35/후속 규칙 PR 미머지. 중복 구현하지 않음. Docs/COMPANY_RULE_INTEGRATION_KO.md가 후속 검증 정본: 상태+greenmailBuyBlocked 병합, 구 수치 저장 변환, 추가 회사 초기값, 배당10%/주기 대조, 회사 종류/인수 전 폐지 예외, 정리매매 거래 제한, 에셋 GUID 이동, 7턴 경계 전체 저장 검증. 실제 타이틀 이어하기/마감 자동 저장 UI도 담당팀 연결 후 검증 필요.
- 이번 작업은 스토리 분기/씬/원본 프리팹/NPC ID 불변. 자동 폰트·설정 복원, 이전 미커밋 변경 보존. 커밋/push 없음.

# 2026-11-10 도움말 제목·닫기 배치 수정 — 최신

- HelpScreen 제목/구분선을 네이티브 QuestionBlock의 자식에서 도움말 panel 직접 자식으로 옮겨, 원본 대화 애니메이션 위치 오프셋이 전파되지 않게 함. 제목558/구분선548/닫기556으로12px 내리고 본문/목록 상단도12px 내려 간격 유지. 원본 이벤트 UI 불변.
- HelpVerification에서 원본 질문 블록+40px 이동을 재현해 제목이 상단바 아래인지, 닫기 버튼과 구분선 사이 간격이 있는지 실제 화면좌표 검사. 도움말/확대/스킵안내 포함37 PASS·FAIL0. 캡처 QA_Help/header-finance.png 직접확인. 빌드오류0/경고0, 최신 실행 QA_NarrativeRevision/Build/TakeOver.exe. 미커밋보존·커밋/push없음. 자동폰트/설정복원.

# 2026-11-10 튜토리얼 CFO 이벤트 원본 재사용 — 최신

- 사용자 정정: 튜토리얼 이벤트만 기존 서지아 이벤트 기반 변형. tutorial_materials_01 별도ID/tutorialOnly 유지, 서지아/원본 카드·상세·협상배경 그대로 재사용. 공개 재무자료 현금/매출이익/부채 질문3개, 계약·보수·비공개정보거래 없음. 1AP+관찰기억/관계만. 윤하진은 소개/복귀 대화, TutorialContact=서지아, 기억확인대상도서지아. 본편 inside_info_01은 이번에 건드리지 않음.
- 136항목/필수14목표/1주매수·추가매수·첫매도/튜토리얼지분조건없음 유지. 도움말 이벤트/기억사진 교체. UI원본·NPC구조 그대로. 직전 윤하진 이벤트 버전 설명은 대체됨.
- QA_TutorialEventVariant/RESULT_KO.md: 그래픽참조8, 전답변/관련회귀33, 정상1002, 스킵56, 원본/참조870 PASS·FAIL0. 정상=전체스킵 상태일치, 빌드오류0/경고0. cfo-event.png 직접검수. 최신 실행 QA_NarrativeRevision/Build/TakeOver.exe. 미커밋보존·커밋/push없음. 실행파일전체손플레이 미검증. 자동폰트/설정 복원.

# 2026-11-10 초반 서사·운영 기획 정합성 수정 — 최신

- 새 실제 튜토리얼 이벤트 tutorial_materials_01: 윤하진과 공개 자료 질문3개, AP1회·관찰기억1회, 계약/보수/추가매매 없음. tutorialOnly로 자유진행 노출 방지. TutorialContact=Yoon, 기존 FinancialDirector 상수/정본 NPC ID 유지. 136항목/필수14목표/1주매수·1주추가매수·1주매도 유지, 튜토리얼 지분10% 요구 없음.
- 기존 inside_info_01은 미래건설 지분10% 본편 사건. 초반/일반 이벤트 의향·검토를 계약/약속이행으로 기록하던 문구·Custom기억 수정. 실제 선택 actorId=player. 구조조정51%+적자/이익감소(원본매출/비용으로 순서독립판정), 고정회사조건/만남전자격 재검사/회사대표 관계 연결.
- 그린메일 수락 전량매도+회사별영구매수제한, 거절 시 거래효과 미적용. CompanyState/저장옵션필드greenmailBuyBlocked 추가, 실제저장복원/구저장호환 검증. 관계임계값/회사유형정본 등 미정은 새 숫자로 채우지 않음(기존25%발생은 보존). 미구현운영/약속/주간경제 시스템 미추가. Docs/NARRATIVE_ALIGNMENT_KO.md 참조.
- 대화 스킵/설명창 전환의 반전버튼 클릭영역 보정, 정상기본선택=전체스킵 상태일치. 도움말 본편/효과/미구현 설명 및 새 이벤트/기억 사진 갱신. UI원본·NPC등록구조 유지.
- QA_NarrativeRevision/RESULT_KO.md: 신규조건/거래회귀33, 정상1002, 스킵56, 일반이벤트466, NPC184, 연결603, 도움말34, 원본/참조870 PASS·FAIL0. 빌드오류0/경고0. 최신 실행 QA_NarrativeRevision/Build/TakeOver.exe, 대본 REVISED_DIALOGUE_KO.md. 자동폰트/설정복원. 기존도움말미커밋보존, HEAD eaf2d89·커밋/push 없음. 실행파일전체손플레이 미검증.

# 2026-11-10 도움말 사진 확대 예외 수정 — 최신

- Player.log의 ArgumentNullException(data): HelpScreen.ShowPhoto → EventNegotiationView.CreateActionButton 확인. 도움말 초기화 시 StoryDirector의 임시 대화창 복제본을 원본으로 잡아, 튜토리얼 종료 뒤 파괴된 버튼을 복제하려던 문제. 원본 선택에서 StoryDirector 하위 임시 대화창 제외, 씬의 실제 이벤트 UI만 사용.
- HelpVerification에 전역 런타임 오류 수집 및 실제 전체튜토리얼 스킵/임시 UI 파괴 이후 사진 확대·닫기5회 반복 검증 추가. FAIL0/COMPLETE, 닫기 버튼 생성·창 종료 모두 통과. Windows 빌드 오류0/경고0. 최신 실행 QA_ContextUI/Build/TakeOver.exe. 미커밋.

# 2026-11-10 도움말 사진 닫기·항목 정리 — 최신

- 다시 시작하기 항목 삭제, 총11항목. 사진 확대 닫기를 아래 중앙으로 이동. 사진/배경 클릭 및 Esc 키로도 닫기 지원. 중복 확대창 방지, 도움말 종료 시 확대창도 정리.
- QA_Help/results.txt: 확대 닫기 버튼 실제 화면 좌표 Raycast 및 pointerClick 후 창 제거 통과. 11항목/사진7개/전체스킵안내 검증 통과. photo.png에서 아래 닫기 버튼 직접 확인. 최신 Windows 빌드 오류0/경고0. 실행 QA_ContextUI/Build/TakeOver.exe. 미커밋.

# 2026-11-10 도움말 탭 추가 — 최신

- 상단 기존 버튼을 복제한 도움말 탭 추가. 이벤트 공용 패널/버튼 기반 11항목, 마우스 휠 본문 읽기, 실제 플레이 사진7개 및 확대 보기. 미확보 사진 영역은 비움. 금액/재무/AP/날짜/거래/지분/이벤트/협상/관계/기억/경영제안/스킵 설명.
- StoryDirector.Finish 공통 종료에서 위치 안내창 표시: 정상 완료와 전체 스킵 모두 필수 표시. 도움말 열기/확인 제공. 게임 직접 진입은 자동 안내하지 않음.
- 완전히 새로운 기능 추가 때 HelpTopics.cs/사진을 함께 갱신하는 규칙을 양쪽 AGENTS에 추가. 기존 기능 개선은 사용자 요청 시 갱신. Docs/HELP_AUTHORING_KO.md 참조.
- QA_Help: HelpVerification 탭/11항목/7사진/확대/전체스킵안내 통과. 정상 전체튜토리얼+종료안내 통과(normal-results.txt). Windows 빌드 오류0/경고0. 캡처 help.png/finance.png/photo.png/introduction.png. 최신 실행 QA_ContextUI/Build/TakeOver.exe. 자동 폰트/URP/프로젝트설정 복원.
- HEAD eaf2d89 기준 도움말 수정은 미커밋. NPC ID/데이터/회사/이벤트 효과 수정 없음. 실행파일 전체 수동 플레이 미검증.

# 2026-11-10 도움말 사진 닫기·항목 정리 — 최신

- 다시 시작하기 항목 삭제, 총11항목. 사진 확대 닫기를 아래 중앙으로 이동. 사진/배경 클릭 및 Esc 키로도 닫기 지원. 중복 확대창 방지, 도움말 종료 시 확대창도 정리.
- QA_Help/results.txt: 확대 닫기 버튼 실제 화면 좌표 Raycast 및 pointerClick 후 창 제거 통과. 11항목/사진7개/전체스킵안내 검증 통과. photo.png에서 아래 닫기 버튼 직접 확인. 최신 Windows 빌드 오류0/경고0. 실행 QA_ContextUI/Build/TakeOver.exe. 미커밋.

# 2026-11-10 도움말 탭 추가 — 최신

- 상단 기존 버튼을 복제한 도움말 탭 추가. 이벤트 공용 패널/버튼 기반 12항목, 마우스 휠 본문 읽기, 실제 플레이 사진7개 및 확대 보기. 미확보 사진 영역은 비움. 금액/재무/AP/날짜/거래/지분/이벤트/협상/관계/기억/경영제안/스킵 설명.
- StoryDirector.Finish 공통 종료에서 위치 안내창 표시: 정상 완료와 전체 스킵 모두 필수 표시. 도움말 열기/확인 제공. 게임 직접 진입은 자동 안내하지 않음.
- 완전히 새로운 기능 추가 때 HelpTopics.cs/사진을 함께 갱신하는 규칙을 양쪽 AGENTS에 추가. 기존 기능 개선은 사용자 요청 시 갱신. Docs/HELP_AUTHORING_KO.md 참조.
- QA_Help: HelpVerification 탭/12항목/7사진/확대/전체스킵안내 통과. 정상 전체튜토리얼+종료안내 통과(normal-results.txt). Windows 빌드 오류0/경고0. 캡처 help.png/finance.png/photo.png/introduction.png. 최신 실행 QA_ContextUI/Build/TakeOver.exe. 자동 폰트/URP/프로젝트설정 복원.
- HEAD eaf2d89 기준 도움말 수정은 미커밋. NPC ID/데이터/회사/이벤트 효과 수정 없음. 실행파일 전체 수동 플레이 미검증.

# 2026-11-10 공용 설명창·버튼·주인공 정렬 수정 완료 — 최신

- 주인공 본문 우측=인명 밑줄 우측. 독백 인명/밑줄 숨김·전체폭 중앙 유지. 설명창은 이벤트 DialogueBand/QuestionBlock 복제, 모든 확인/인물정보/스킵은 닫기 버튼 복제. 원본 프리팹/씬 불변. 확인 라벨=확인, 금액N0/백분율 소수점 허용.
- 기능 설명은 사용 시점 옆 설명창, 인물 대사는 짧은 구어체 요약. 정보/기업 화면 오른쪽·설명 왼쪽, 기업창 비겹침/화면 경계 확인. System 단독은 설명창. 재무 구간 CFO 대신 동행 윤하진. 첫날 CFO 소개/실제 이벤트 유지. 136항목/필수14목표·추가매수/첫매도·실제 이벤트 덧씌움 금지 유지.
- 양쪽 AGENTS.md에 별도 제작 요청 없으면 이벤트 공용 양식 사용/원본 설정 수정금지 정책 추가. 최신 QA_ContextUI/RESULT_KO.md, 실행 QA_ContextUI/Build/TakeOver.exe, 대본 REVISED_DIALOGUE_KO.md.
- 전체Play1001/스킵56/이벤트466/NPC184/연결603/제작432/참조554 PASS·FAIL0, Windows 오류0/경고0. 이전 정상 완료와 게임 상태 동일. 자동 폰트/설정 복원. NPC ID/기업/이벤트 효과 불변. HEAD44972c6 이전 미커밋 보존·커밋/push 없음. 실행파일 전체 손플레이/중간저장 미검증.

# 독백·가독성·대사/시스템 문체 수정 — 2026-11-10

독백은 인명/밑줄 없이 전체 본문 중앙에 표시한다. 공용 본문 폭을 넓혀 잦은 줄바꿈을 줄였고, 인물 대사는 구어체·시스템 안내는 ~하였습니다/~입니다 문체로 구분했다(StorySpeaker.System 추가). 145항목/필수14목표·추가매수/첫매도·실제 이벤트 덧씌움 금지 유지. 최신 결과/실행: workspace `QA_DialoguePolish/RESULT_KO.md`, `QA_DialoguePolish/Build/TakeOver.exe`. Windows 오류0/경고0.

# 대화 UI 통일·이벤트 시연 수정 — 2026-11-10

튜토리얼 대사도 기존 이벤트 대화 UI/공용 렌더러를 사용하며 별도 대사 패널·선택지·장 제목 띠는 제거했다. 이벤트/AP 설명은 실제 이벤트 전 대사로 이동했다. 실제 이벤트 상세·대화에는 튜토리얼 안내띠·테두리·입력 마스크·확인 버튼을 표시하지 않고 기존 이벤트 스킵 하나만 사용한다. 145항목/필수14목표·추가매수/첫매도 유지. 최신 검증/실행은 workspace `QA_DialogueUI/RESULT_KO.md`, `QA_DialogueUI/Build/TakeOver.exe`. Windows 오류0/경고0.

# 기능 튜토리얼 확장 — 2026-11-10

둘째 날 실제 이벤트·AP·관계/기억 결과·기업 재무 비교·지분 확인·추가 매수/첫 매도를 필수 체험으로 연결했다. 총145항목·필수14목표다. 경영 참여·구조조정 실습은 추가하지 않았다. 대화 스킵은 필수 목표에서 정지하며, 타이틀 전체 스킵만 정상 기본 답변 완료 상태를 만든다. 최신 검증/실행은 workspace `QA_TutorialExpansion/RESULT_KO.md`, `QA_TutorialExpansion/Build/TakeOver.exe`. Windows 오류0/경고0. 상세 안내 `Assets/Story/TUTORIAL_FUNCTIONS_KO.md`.

# 일반 이벤트 대화 스킵 — 2026-11-10

일반 이벤트에도 선택지까지 넘기는 페이드 스킵을 적용했다. 기존 7개 이벤트의 17개 선택지를 정상/스킵으로 비교해 전체 게임 상태가 일치했고, 기존 이벤트/NPC 회귀 검증도 통과했다. 최신 검증 및 실행 파일은 workspace `QA_EventDialogueSkip/RESULT_KO.md`, `QA_EventDialogueSkip/Build/TakeOver.exe`다. 빌드 오류0/경고0. 아래 종합 점검에서 기록했던 일반 이벤트 스킵 누락은 해결됐다.

# 인물 서사 후속 확장 — 2026-11-10

대립기업 신규 16명의 공개 프로필과 제작용 서사 조건을 개편했다. 끝까지 대립·조건부 거래·직업적 경계·내부 갈등을 구분했으며 상세 설정은 `Assets/Story/RIVAL_CHARACTER_DESIGN_KO.md`다. 기존 관계·Memory 수치와 기업 재무는 유지한다. 최신 검증·실행 파일은 workspace `QA_RivalDepth/RESULT_KO.md`, `QA_RivalDepth/Build/TakeOver.exe`. 실제 신규 메인 스토리 이벤트와 자동 전향 엔진은 별도 구현 범위다.

# 도입부 후속 수정 — 2026-11-10

빌드 경고 2건을 해결해 최종 오류·경고 0건이다. 가족 피해 장면의 성인 스탠딩을 숨기고, 타이틀의 전체 스킵 체크란과 대화창의 선택지 단위 페이드 스킵을 추가했다. 최신 검증·실행 파일은 workspace `QA_StorySkip/RESULT_KO.md`, `QA_StorySkip/Build/TakeOver.exe`다. 전체 스킵은 기본 첫 선택 3개를 적용한 정상 완료 상태와 일치한다. 상세 정책은 `Assets/Story/STORY_AUTHORING_KO.md`를 따른다.

# 최신 상태 — 2026-11-10

프롤로그·튜토리얼 93개 진행 항목과 대립기업 5곳/NPC 16명을 구현했다. 현재 총 회사 9곳, NPC 30명이다. 실행·제작 안내는 `Assets/Story/STORY_AUTHORING_KO.md`, 정본 ID는 `Docs/ID_CONVENTIONS_KO.md`, 최신 검증은 workspace `QA_Story/RESULT_KO.md`를 따른다. 아래 본문은 초기 인계 당시의 과거 기록이다.

# TakeOver 진행 상황 이전 기록

이 문서는 현재 저장소에서 확인한 기획과 구현 상태를 이어받기 위한 요약이다. 저장소의 기준 커밋은 `2f811ce` (`Unity 프로젝트 초기 세팅`)이며, 아래 구현 상태는 커밋 이후 작업 트리에 존재하는 파일을 기준으로 기록했다. 별도 브랜치나 외부 저장소의 이력은 이 환경에서 확인하지 못했다.

## 프로젝트 개요

- Unity `6000.3.6f1` 기반 2D 기업 인수 경영 전략 게임.
- 플레이어는 초기 자본 없이 투자 회사를 세우고 여러 기업의 지분을 확보한다. 포트폴리오 기업과 주요 NPC의 반응, 협상, 경영 개입을 거쳐 APEX Corporation을 최종 목표로 삼는다.
- 설계 참고 자료는 `Reference/`에 보관되어 있다. 참고 자료에 적힌 미정 항목은 확정 시스템으로 간주하지 않는다.

## 현재 저장소에서 확인한 구현

### NPC 관계 및 기억 프로토타입

- `Assets/Scripts/NPC/NpcRelationTypes.cs`: Trust, Respect, Fear, Hostility, Dependency, Interest 관계값과 이벤트 기억 데이터 구조를 정의한다. 기억에는 유형, 행위자/대상, 강도, 턴, 공개 범위, 영구 여부, 신뢰도, 해석, 태그, 관계 변화량 필드가 있다.
- `Assets/Scripts/NPC/NpcMemoryService.cs`: 기억 기록, 활성 기억의 중복 ID 억제, 관계 변화량 반영, 턴 경과에 따른 비영구 기억 영향력 감소 및 잔여 턴 0 도달 시 제거, 테스트 상태 초기화를 제공한다. 제거된 ID는 이후 다시 기록할 수 있다.
- `Assets/Scripts/NPC/NpcEventChannel.cs`와 `NpcEventProcessor.cs`: 이벤트/포트폴리오 등 출처와 무관하게 결과를 발행하고 대상 NPC별 Memory와 관계 변화로 반영한다.
- `Assets/Scripts/NPC/NpcFeatureTestHarness.cs`: 테스트 씬 데이터와 버튼을 공용 사건 처리 경로에 연결하는 테스트 전용 어댑터다.
- `Assets/Scripts/NPC/NpcScreenNavigation.cs`: 이벤트 및 포트폴리오 화면 진입을 Inspector UnityEvent로 연결하는 공용 진입점이다.
- `Assets/Scripts/NPC/NpcFeatureTestScene.cs` 및 `Assets/Scenes/NPC/NpcRelationMemoryTest.unity`: 관계값 변경, 기억 기록, 턴 진행을 살펴보는 테스트 화면/씬이 있다.
- 초기 프로토타입 커밋에는 런타임 동작만 있었다. 저장/불러오기, 실제 포트폴리오 모델 및 공통 턴 연결은 아래 2026-09-29 작업 트리 확장 항목에서 별도로 기록한다.

### 2026-09-29 — NPC 연결 경계 확장 작업 트리

- `NpcStateRegistry`를 추가해 NPC ID별 관계·기억 상태를 공유하고, 상태 변경 알림을 다른 시스템에 전달하도록 했다.
- `NpcEventPayload`/`NpcEventChannel`/`NpcEventProcessor`로 출처 중립적인 사건 전달과 NPC별 Memory 기록을 분리했다. 사건별 고유 ID와 대상 NPC를 지정해 발행하며, 테스트 화면은 같은 경로를 사용한다.
- 공용 이벤트 계약은 고유 사건 ID·대상 NPC·선택적 actionId를 요구하고, NPC별 `NpcEventReaction`이 있으면 공통 기본 효과 대신 그 NPC 전용 기억 해석/관계 효과를 적용한다. 중복 ID는 새 기록 알림과 관계 변화를 다시 발생시키지 않는다.
- NPC별 `NpcActionPreference` 데이터는 actionId 기준 Likes/Dislikes로 초기 설정/조회한다. 정적 프로필은 세이브 런타임 상태와 분리하며 사건 Memory에는 기록 당시 분류를 보존한다. 선호 수치 효과, 관계 축 범위/계산, Memory 영향력 소비 규칙은 기획 회의 전까지 적용하지 않는다.
- `GameTurnClock`이 공통 턴을 소유하고 등록된 NPC 전체의 Memory 경과 처리를 호출한다.
- `NpcSaveService`가 턴과 NPC 상태를 JSON으로 저장/복원한다. 테스트 화면에 저장·불러오기 동작을 추가했다.
- `Reference/미팅자료 1.pdf` 3쪽에 따라 이벤트팀/지분매입팀 화면을 여는 NPC 파트 연결 버튼과 UnityEvent 후크를 테스트 UI에 추가했다. 실제 대응 화면이 생기면 화면 담당 파트가 Inspector 리스너를 연결해야 한다.
- 대응 화면이 아직 없는 경우에도 두 버튼을 누르면 각각의 연결 안내 화면으로 전환되고 NPC 테스트 화면으로 돌아올 수 있다. UnityEvent 리스너가 설정되어 있으면 해당 화면 열기 동작도 호출한다.
- Memory 영향력 조회 API는 있지만 협상/행동 소비 시스템은 아직 없다. 실제 포트폴리오, 선택형 이벤트, NPC 프로필 공급원도 저장소에 없어 현재는 호출 계약과 테스트 발행자까지 연결된 상태다.
- 사용자가 기존 연결 버전의 씬 실행을 확인했다고 보고했다. 이번에 추가한 안내 화면 전환은 아직 재실행 확인하지 않았다. Unity 배치 컴파일은 같은 프로젝트를 연 Unity 인스턴스 때문에 실행되지 않았고 자동 테스트는 수행하지 않았다.

## 아직 정해야 할 설계

참고 문서와 코드에서 구체화되지 않은 핵심 항목이다. 먼저 문서에서 결정을 기록한 다음 구현에 반영한다.

- 하루 행동 포인트(AP), 턴 종료, 게임 날짜 진행 규칙과 값.
- 지분 취득 단계 및 각 단계에서 열리는 정보/협상/경영 개입 권한.
- 공개·조건부 이벤트의 트리거 조건, 이벤트 선택/거절 처리, 복수 조건 동시 충족 우선순위.
- 관계 수치의 의미와 허용 범위: 구현은 일부 관계를 `-100~100`, Fear/Dependency를 `0~100`으로 제한하지만, 이 범위가 기획 확정인지 불분명하다.
- 기억의 영향력 감소를 협상/행동 판단에 적용하는 방식. 현재 서비스는 기억별 영향력 수치만 감소시키고 관계값을 되감거나 판단 모델에 연결하지 않는다.
- NPC별 목표/성향/선호/의사결정, 기업 현황 및 포트폴리오 데이터 모델.
- APEX 최종전의 시작 조건, 진행 방식, 엔딩 분기.

## 남은 연결 작업

1. 이벤트/포트폴리오 선택 시스템은 결과 확정 지점에서 `NpcEventChannel.Publish`를 호출하고, 출처 식별자·대상 NPC·고유 사건 ID·결과 효과를 공급한다.
2. 실제 게임 부트스트랩이 `NpcStateRegistry`와 `GameTurnClock`을 소유하도록 연결하고, 현재 테스트용 런타임 생성 경로를 게임 초기화 경로로 바꾼다.
3. 협상/대화 파트가 생긴 뒤 Memory 공개 범위와 영향력 소비 규칙을 기획에서 결정하고, `NpcMemoryService.GetMemoryInfluence` 및 상태 변경 알림을 소비하도록 연결한다.
4. 실제 게임 씬에서 저장 슬롯·버전 호환·턴 진행 순서를 검증한다. 이번 JSON 저장은 단일 프로토타입 상태 파일이며 저장 슬롯/버전 관리 기능은 아직 아니다.

## 참고 및 작업 상태

- 기획 자료: `Reference/` 아래 회의 자료와 시스템/서사 메모.
- 작업 규칙: 저장소 루트 `AGENTS.md`, `Assets/AGENTS.md`, `Reference/AGENTS.md`.
- 기준 커밋에는 초기 Unity 프로젝트와 README 초안이 있다. 현재 작업 트리에는 NPC 프로토타입, 테스트 씬, 참고 문서 및 Unity 설정 변경이 포함되어 있다.
- 이 이전 기록을 위해 Unity 빌드나 테스트를 실행하지 않았다.
