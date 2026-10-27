# 회사·NPC ID 통일 기준

2026-10-27 사용자 요청으로 이번 ID 통일에 한해 이벤트·NPC·지분매입팀의 데이터와 참조를 함께 수정했다. 평소의 타 팀 파일 수정 제한은 유지한다.

| 회사 | companyId / companyID |
|---|---|
| 미래건설 | mirae-construction |
| 하늘식품 | haneul-food |
| 동방물산 | dongbang-trading |
| 한성공업 | hansung-industry |

외부 NPC의 소속은 `external-network`를 유지한다. 회사 SO와 이벤트 회사 목록, NPC SO에서 동일한 문자열을 사용하며 별칭 변환은 하지 않는다.

- 회사 소속 NPC: `npc-{companyID}-{직책}-{성이름}`
- 외부 인물: `npc-{직책}-{성이름}`
- 실제 ID는 `Assets/NPC/Data/Profiles`의 각 SO를 기준으로 대조한다. 직책 검색용 CEO는 회사별 1명이다.

이벤트 SO 7개의 선택 결과·NPC별 반응·조건, 이벤트 프리팹의 CEO/구조조정 상대 및 회사 목록을 통일했다. 테스트 씬과 이벤트 생성 도구의 식별자도 같은 규약을 사용한다.

거래팀 기존 회사 데이터 2개는 GUID와 재무값을 보존해 미래건설·하늘식품으로 정리했다. 동방물산·한성공업 SO와 프리팹 회사 항목을 추가했다. 추가 회사의 재무값은 기존 양식을 복사한 임시값이며 확정 기획값이 아니다. 지도 위치의 회사 이름 연결도 실제 회사 표시명에 맞췄다.

거래 이벤트 리스너는 알림의 `companyId`를 그대로 전달한다. 결과 효과·현금·지분 처리 전체를 이번 ID 수정으로 완성한 것은 아니다. 기존 결과값 분기 및 이벤트팀 임시 월드 데이터 구조는 별도 연결 업무다.

Unity 6000.3.6f1에서 93개 항목을 검증했다. 이벤트의 모든 NPC 대상과 회사 조건, CEO/구조조정 상대의 소속, 회사 SO 및 프리팹 4개 참조, 실제 회사 ID 조회와 기존 NPC 기능을 포함한다. `Game.unity` 파일은 변경하지 않았다. 검증 중 Play 모드에서는 NPC와 렌더링 카메라 외 오브젝트를 비활성화한다.

결과: workspace `psm/unity-verification/results.txt`, 로그: `psm/unity-verification-project-ids.log`.

과거 진행 기록·백업·원자료는 당시 상태를 설명하는 기록으로 보존한다. 현재 실행 데이터의 정본으로 사용하지 않는다.

## NPC 여권식 표기 — 2026-10-27 수정

사용자 요청으로 통용 여권식 인명 철자를 아래 표로 고정했다. 성과 이름 모두 붙인 소문자를 ID의 인명 부분으로 사용한다. 인명 안에는 하이픈을 넣지 않는다. 파일명은 `성_이름.asset`, SO 내부 이름은 `성 이름` 대문자다. 한글 표시명과 기존 .meta GUID는 보존한다. 이 표는 가상 인물의 프로젝트 표기 선택이며 여권의 유일한 철자라는 의미가 아니다.

외교부 기본 안내: https://www.passport.go.kr/home/kor/contents.do?menuPos=37

| 한글 | SO 파일 | 내부 이름 | npcId |
|---|---|---|---|
| 윤하진 | Yoon_Hajin.asset | YOON HAJIN | `npc-broker-yoonhajin` |
| 정수아 | Jung_Sooah.asset | JUNG SOOAH | `npc-investigative-reporter-jungsooah` |
| 김태성 | Kim_Taesung.asset | KIM TAESUNG | `npc-institutional-investor-kimtaesung` |
| 박상현 | Park_Sanghyun.asset | PARK SANGHYUN | `npc-political-lobbyist-parksanghyun` |
| 강준혁 | Kang_Junhyuk.asset | KANG JUNHYUK | `npc-mirae-construction-ceo-kangjunhyuk` |
| 서지아 | Seo_Jiah.asset | SEO JIAH | `npc-mirae-construction-cfo-seojiah` |
| 박도식 | Park_Dosik.asset | PARK DOSIK | `npc-mirae-construction-field-director-parkdosik` |
| 이민규 | Lee_Mingyu.asset | LEE MINGYU | `npc-economic-reporter-leemingyu` |
| 김하린 | Kim_Harin.asset | KIM HARIN | `npc-haneul-food-ceo-kimharin` |
| 오재성 | Oh_Jaesung.asset | OH JAESUNG | `npc-haneul-food-cfo-ohjaesung` |
| 강재현 | Kang_Jaehyun.asset | KANG JAEHYUN | `npc-dongbang-trading-ceo-kangjaehyun` |
| 박민우 | Park_Minwoo.asset | PARK MINWOO | `npc-dongbang-trading-hr-director-parkminwoo` |
| 조민석 | Cho_Minseok.asset | CHO MINSEOK | `npc-hansung-industry-ceo-chominseok` |
| 최미정 | Choi_Mijung.asset | CHOI MIJUNG | `npc-hansung-industry-labor-director-choimijung` |

이번 변경에서는 이벤트 SO/프리팹, NPC 기본 명단, 테스트 씬과 검증 코드의 참조를 함께 변경했다. 구 인명 ID의 별칭/자동 변환은 추가하지 않았다. 새 ID와 다른 저장은 기존 ID 검증 규칙에 따라 거부한다.
