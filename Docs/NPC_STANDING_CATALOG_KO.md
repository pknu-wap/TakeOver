# 공용 모브 스탠딩 목록

| 용도 | 기본 | 암전 |
| --- | --- | --- |
| 남성 모브 | Mob_Male_Standing.png | Mob_Male_Dim.png |
| 여성 모브 | Mob_Female_Standing.png | Mob_Female_Dim.png |

경로: `Assets/Resources/NPC/MobStanding/`. 원본은 워크스페이스 `캐릭터 스탠딩일러/모브캐릭터(남/여)_기본.png`, 암전은 `기본2.png`이며 파일 내용은 그대로 복사했다.

`NpcStandingCatalog`가 런타임 NPC 프로필에 공용 원화를 연결한다. 전용 기본 또는 암전 일러스트가 있으면 이를 우선 보존한다. 기본 원화가 없는 회사 인물은 NPC SO의 **소개 · 소속 · 이미지 → 공용 모브 외형**에서 Male/Female을 지정한다(미지정 기본 Male). 정상·암전은 같은 외형의 한 쌍을 사용한다. 모브는 특정 이름의 인물 원화가 아니며 여러 서브 기업 인물에 재사용한다. 직접 연출할 때는 `NpcStandingCatalog.GetMob(appearance, dimmed)`로 조회할 수 있다.

다음 원화 대기 회사는 자동 대체하지 않는다.

- polaris-finance / 폴라리스파이낸스
- daeseong-industry / 대성산업
- sinseong-development / 신성개발
- blueharbor-retail / 블루하버리테일
- aurora-foodtech / 오로라푸드테크
- APEX 내부인원: apex 또는 apex-* 회사 ID, 소속 표시명에 APEX가 포함된 인물. APEX가 정식 등록되면 실제 회사 ID도 이 규칙에 맞춰 확인한다.

외부 인물(external-network)과 회사 미지정 인물에는 회사 모브를 자동 적용하지 않는다. 등록된 NPC 30명 중 23명은 전용 기본·암전 스탠딩을 사용하며, 원화 미등록 7명은 기존 실루엣을 유지한다. 전용 PNG는 `Assets/NPC/Art/Standing/`에 있다.
