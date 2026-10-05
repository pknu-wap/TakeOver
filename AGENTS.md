# TakeOver 프로젝트 작업 지침

workspace 수준 `../AGENTS.md`와 `../psm/`의 요약·개인 지침을 먼저 따른다. 이 파일은 Unity 저장소 전용 안내다.

- Unity `6000.3.6f1 LTS`와 현재 프로젝트 패키지 구성을 기준으로 작업한다. 요청 없이 엔진·런타임·서버를 추가하지 않는다.
- 기능은 Dashboard, EventSystem, NPC, ShareTrading 등 영역별로 둔다. 씬/에셋 이동 시 `.meta` GUID 및 직렬화 참조를 보존한다.
- 게임 방향은 기업 인수·투자 전략 시뮬레이션이다. NPC 관계 축은 Trust, Respect, Fear, Hostility, Dependency, Interest다. 세부 공식과 값은 확정 기획이 확인되기 전까지 프로토타입으로 표시한다.
- 코드를 수정하면 설명용 주석은 한국어로 작성하고 실제 구현과 일치시킨다.
- 기획 원자료는 `Reference/`에 있다. 원자료는 수정하지 않고, 근거가 상충하거나 불명확하면 추정하지 말고 문서에 미정으로 남긴다.
- 진행 상태와 검증 여부는 workspace `psm/STATUS_KO.md` 및 `psm/QUICK_CONTEXT.md`에서 관리한다. 실행하지 않은 빌드/테스트를 성공으로 기록하지 않는다.
- 씬 통합 후 공통 플레이 씬은 `Assets/Scenes/Game.unity`다. 이 씬은 플레이만 하고 저장·수정하지 않으며, 새 작업 씬을 만들지 않는다.
- NPC 팀 작업은 NPC 팀 소유 프리팹에서만 한다. 팀 폴더 구조가 바뀔 수 있으므로 최신 연결 가이드와 실제 경로를 먼저 확인한다. 다른 팀 폴더·프리팹·씬은 열거나 수정하지 않는다.
- 타 팀 기능과 연결할 때는 Inspector 직렬화 참조를 만들지 않는다. 연결 가이드에 따라 NPC 소유 코드에서 `FindFirstObjectByType`를 사용한다. 담당 경계가 불분명하면 수정 전에 확인한다.
