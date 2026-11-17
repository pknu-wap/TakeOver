"""Compile the reviewed paired campaign, company premise and fourteen endings."""
import json
import re
from build_authoring import ROOT, COMPANIES, NPC, read, lines, definition, simple_answers

M, H, D, S = COMPANIES


def heading_block(text, prefix):
    start = next(m for m in re.finditer(r"(?m)^#{2,3} .+$", text) if m[0].startswith(prefix))
    depth = len(start[0].split()[0])
    end = re.search(r"(?m)^#{2," + str(depth) + r"} ", text[start.end():])
    return start[0].lstrip('# ').strip(), text[start.end():start.end() + end.start() if end else len(text)]


def narrated(text):
    return {"narration": True, "text": text}


def spoken(who, text):
    return {"npcId": NPC.get(who, ""), "player": who == "주인공", "text": text}


def compile_paired_campaign(legacy):
    old = {x['eventId']: x for x in legacy}
    result = []
    paired = read('EARLY_STORY_PAIRED_EXPANSION_KO.md')

    def add(event_id, day, company, speaker, opening, titles=None, previous=(), start=0, title=None):
        answers = simple_answers(speaker, [(t, r) for t, r in (titles or [('확인', '오늘 확인한 범위에서 정리하겠습니다.')])])
        for i, answer in enumerate(answers):
            answer['npcResult'] = {'targetNpcIds': [NPC[speaker]], 'memoryType': 8, 'durationTurns': 3, 'strength': 1, 'publicity': 0, 'reliability': 1,
                'interpretation': '투자 검토 대화에서 ' + answer['choiceText'], 'relationDelta': {'respect': 1 if i == 0 else 0, 'interest': 1 if i == 1 else 0}}
        if not opening or opening[-1].get('player') or opening[-1].get('narration'):
            opening.append(spoken(speaker, '어떤 방향으로 검토하실지 말씀해주시겠어요?' if len(answers) > 1 else '확인하셨으면 말씀해주세요.'))
        item = definition(event_id, title or event_id, speaker, opening, answers, company, category=1, campaignDay=day,
            missionStartDay=start, prerequisiteEventId='', prerequisiteEventIds=list(previous), apCost=0, validDays=10000)
        result.append(item)
        return item

    def from_draft(event_id, prefix, day, company, speaker, titles, previous, start=0):
        title, body = heading_block(paired, prefix)
        return add(event_id, day, company, speaker, lines(body), titles, previous, start, title.split(' — ', 1)[-1])

    # Existing first meetings retain their full company introductions and approved choices.
    for event_id in ['main_mirae_02', 'main_haneul_contact', 'main_haneul_01', 'main_haneul_02']:
        item = json.loads(json.dumps(old[event_id], ensure_ascii=False))
        result.append(item)
    result[1]['openingLines'].insert(0, narrated('회사에 제출할 다음 보고는 건설사 한 곳을 넘어 다른 업종의 투자 가치를 검토하는 내용이었다. 공동출자자들은 이번 사례를 보고 회사의 성장 계획을 다시 검토하기로 했다.'))
    result[2]['openingLines'].insert(0, narrated('하늘식품의 첫 면담에 참여할 지분을 마련했다. 우리 회사가 다른 업종도 판단할 수 있는지 보여줄 기회다. 매수 대금이 하늘식품의 운영자금으로 들어가는 것은 아니다.'))

    add('main_dongbang_contact', 15, D, '윤하진', [
        narrated('미래건설에서 받은 공개 인계 일정의 운영 거점 참여사를 찾았다. 동방물산. 지금은 고객을 나누고 자체 운영을 늘리려는 회사였다.'),
        spoken('서지아', '저희 잔여 정리안도 20일차에 검토합니다. 동방 쪽 공개 운영 설명하고 같은 시기로 보시면 질문을 정리하기 좋을 거예요.'),
        spoken('박민우', '동방물산 박민우입니다. 현재 운영안과 공개할 수 있는 당시 설명을 함께 문의하셨죠? 20일차에 주주 면담을 합니다.'),
        spoken('주인공', '확정 물량과 앞으로 늘릴 물량을 구분해서 보고 싶습니다. 지금 회사의 가치부터 확인하려고요.'),
        spoken('박민우', '그 두 표를 따로 보자고 하시면 시작은 좋네요. 아직 없는 물건까지 세면 창고는 벌써 꽉 차거든요.'),
        spoken('윤하진', '동방물산은 첫 면담 기준이 10%예요. 미래건설은 이번에 주요 주주 검토 자리를 잡으면서 30%를 기준으로 정했고요. 두 회사 모두 20일차 시작에 확인합니다.'),
        spoken('주인공', '새 회사만 늘리는 계획은 아니었습니다. 앞선 투자와 다음 운영을 연결해 회사에 보고해야겠군요.'),
        narrated('20일차 시작까지 동방물산 10%와 미래건설 30%를 모두 확보해야 한다. 19일차 마감 전까지 준비한다. 두 조건이 충족되면 필수 사건 두 개가 함께 열리며 모두 마쳐야 일반 사건으로 돌아간다. 지분은 경영권이나 회사의 현금 지원이 아니다.')
    ], previous=['main_haneul_02'], title='운영 거점의 다음 일정')
    first_d = json.loads(json.dumps(old['main_dongbang_01'], ensure_ascii=False))
    _, body = heading_block(paired, '### 20일차 동방물산')
    first_d['openingLines'] += lines(body)
    first_d['prerequisiteEventIds'] = ['main_dongbang_contact']; first_d['prerequisiteEventId'] = ''; first_d['missionStartDay'] = 15
    result.append(first_d)
    from_draft('main_mirae_03', '### 20일차 미래건설', 20, M, '서지아', [
        ('잔여 정리 비용을 먼저 분리한다', '네. 지금 남은 부담과 새 수주에서 기대하는 수익을 따로 정리할게요.'),
        ('새 수주도 검토하되 부담을 합산하지 않는다', '두 항목을 구분해 보겠습니다. 다음 공사가 과거 비용을 모두 해결할 거라고 가정하지 않을게요.'),
        ('공유 가능한 날짜부터 확인한다', '알겠습니다. 회사 내부 결정과 공개할 수 있는 일정 대조를 나눠 설명드릴게요.')], ['main_dongbang_contact'])
    _, body = heading_block(paired, '### 21일차 공동')
    add('main_dongbang_02', 21, D, '박민우', lines(body) + [narrated('미래건설은 잔여 비용·새 수주를, 동방물산은 확정 물량·인원 배치를 나눈 검토안을 준비한다. 32일차 재검토 기준은 미래건설 30%와 동방물산 20%다. 새 목표는 29일차에 정식 안내한다.')],
        previous=['main_dongbang_01', 'main_mirae_03'], title='같은 날, 다른 통보')
    add('main_hansung_contact', 23, S, '최미정', [
        narrated('하늘식품의 포장·품목 전환 질문을 현재 투자 검토 항목으로 정리했다. 공개된 범용 설비·부품 대응 이력에서 한성공업을 찾았다.'),
        spoken('오재성', '설비 견적이 싸다는 답보다 실제로 품질을 반복할 수 있는지 보고 싶습니다. 주문이나 발주를 확정한 건 아닙니다.'),
        spoken('최미정', '한성공업 최미정입니다. 현재 생산과 전수 계획을 함께 문의하셨죠. 28일차 설명 면담에서 공유할 수 있는 범위를 준비하겠습니다.'),
        spoken('김태성', '제품 수요와 생산 조건을 함께 보려는 계획이군요. 출자자에게도 어느 한쪽이 빠진 투자안을 내지 않도록 하세요.'),
        spoken('윤하진', '한성공업 첫 면담은 10%, 하늘식품의 다음 주요 주주 검토는 30%가 기준입니다. 28일차 시작까지 둘 다 준비해야 해요.'),
        narrated('27일차를 마치기 전에 하늘식품 30%와 한성공업 10%를 모두 확보한다. 이번 검토 결과는 주인공 회사의 확장안 심사에도 포함된다. 주식 매수만으로 공급 계약이 체결되거나 회사 현금이 늘지는 않는다.')
    ], previous=['main_dongbang_02'], title='제품을 계속 만들 조건')
    first_s = json.loads(json.dumps(old['main_hansung_01'], ensure_ascii=False))
    _, body = heading_block(paired, '### 28일차 한성공업')
    first_s['openingLines'] = lines(body) + first_s['openingLines']
    first_s['prerequisiteEventIds'] = ['main_hansung_contact']; first_s['prerequisiteEventId'] = ''
    result.append(first_s)
    from_draft('main_haneul_03', '### 28일차 하늘식품', 28, H, '김하린', [
        ('핵심 제품을 정하고 전환 비용을 계산한다', '남길 이유부터 정리할게요. 무엇을 덜 만들지 설명하는 책임도 저희에게 있고요.'),
        ('회수 조건과 제품 구성을 함께 비교한다', '오 이사님과 숫자를 준비하겠습니다. 손님이 다시 고를 이유도 함께 봐주세요.'),
        ('판매 유지안과 전환안을 병렬 검토한다', '비교할 수 있게 준비하겠습니다. 오늘 답변으로 발주가 정해진 건 아니에요.')], ['main_hansung_contact'])
    _, body = heading_block(paired, '### 29일차 공동')
    add('main_hansung_02', 29, S, '최미정', lines(body) + [
        spoken('정수아', 'APEX가 사업에 관여했다는 사실과 실제 누가 계약을 위반했는지는 구분해야 합니다. 그쪽 실행 기록이 더 필요해요.'),
        spoken('김태성', '매매 수익과 대표에게 다음 운용을 맡길지는 다른 문제입니다. 준비한 검토를 끝까지 정리한 투자 판단으로 보여주세요.'),
        narrated('다음 투자 목표: 32일차 시작까지 미래건설 30%와 동방물산 20% 이상. 31일차 마감 전까지 준비한다. 두 필수 사건을 모두 마쳐야 한다. 40일차에는 하늘식품 30%·한성공업 20%로 지속안을 검토할 예정이다.')
    ], previous=['main_hansung_01', 'main_haneul_03'], title='견적서의 빈 시간')
    from_draft('main_mirae_04', '### 32일차 미래건설', 32, M, '서지아', [
        ('범위를 정해 비용 설명안을 검토한다', '남은 비용과 공유 범위를 구분해서 설명하겠습니다.'),
        ('신규 수주의 심사 기준을 보강한다', '과거 부담과 새 수익을 따로 볼 수 있는 기준을 제시하겠습니다.'),
        ('경영진의 수정안을 먼저 요청한다', '네. 회사가 결정할 부분을 먼저 정리하고 주주 의견을 듣겠습니다.')], ['main_hansung_02'], 29)
    from_draft('main_dongbang_03', '### 32일차 동방물산', 32, D, '강재현', [
        ('단계적 운영안을 지지한다', '확정 물량과 다음 집행 기준을 나눠 검토하겠습니다.'),
        ('고객 다변화의 중간 점검을 더한다', '어느 물량이 빠지면 무엇을 멈출지 점검 기준을 적겠소.'),
        ('수익성과 인력 전환을 재검토한다', '속도만으로 설명하지 않겠습니다. 실제 비용과 전환 범위를 같이 보시죠.')], ['main_hansung_02'])
    _, body = heading_block(paired, '### 33일차 공동')
    add('main_pair_a_complete', 33, M, '서지아', lines(body) + [narrated('두 회사가 각자의 현재 계획과 설명 책임을 정리했다. 공동 계약이나 과거 배상, 투자 수익이 확정된 것은 아니다. 주인공 회사도 이 검토 결과를 다음 사업 심사에 제출한다. 미래건설과 동방물산의 초반 메인이 끝났다.')],
        previous=['main_mirae_04', 'main_dongbang_03'], title='서로의 남은 일을 확인하다')
    add('main_pair_b_review', 34, H, '윤하진', [
        narrated('다음 회사 보고를 준비했다. 제품·회수·전수·납기를 함께 놓은 지속 사업 검토다. 공동출자자들은 이 결과를 보고 회사의 다음 운용 방향을 심사하기로 했다.'),
        spoken('윤하진', '앞에서 보신 건 만들 수 있는지와 남길 제품이었죠. 이번에는 다음에도 같은 방식으로 이어갈 수 있는지 정리할 차례예요.'),
        spoken('주인공', '하늘식품과 한성공업에서 각각 자기 계획을 결정하는 자리가 되겠군요.'),
        narrated('새 목표: 40일차 시작까지 하늘식품 30%와 한성공업 20% 이상. 39일차 마감 전까지 준비한다. 기한 미달 시 주인공 회사의 후속 사업과 운용 권한 심사도 실패 서사로 이어진다.')
    ], previous=['main_pair_a_complete'], title='지속할 사업의 조건')
    from_draft('main_haneul_04', '### 40일차 하늘식품', 40, H, '김하린', [
        ('핵심 제품과 재구매 기준을 본다', '남길 이유와 그 비용을 함께 설명하겠습니다.'),
        ('현금 회수 기준을 먼저 점검한다', '납품한 돈과 다음 생산을 위한 돈을 구분해서 보겠습니다.'),
        ('단계적 품목 전환을 검토한다', '손님에게 설명할 범위와 생산 전환을 함께 준비하겠습니다.')], ['main_pair_b_review'], 34)
    from_draft('main_hansung_03', '### 40일차 한성공업', 40, S, '최미정', [
        ('전수 검증 후 단계 전환을 검토한다', '누가 다음에도 만들 수 있는지 확인하는 단계를 계획에 남기겠습니다.'),
        ('기존 납기와 전수 병행을 점검한다', '일이 끝나고 남는 시간이 아니라 계획에 잡힌 시간으로 확인할게요.'),
        ('비용과 일정을 다시 설명해달라 한다', '회사에서 책임질 일정과 주주께 설명할 비용을 구분해 준비하겠습니다.')], ['main_pair_b_review'])
    _, body = heading_block(paired, '### 41일차 공동')
    add('main_pair_b_complete', 41, H, '김하린', lines(body) + [narrated('제품·회수·전수·납기의 현재 계획을 각 회사가 책임지기로 했다. 실제 공급 계약은 별도 절차다. 주인공 회사는 이 검토를 바탕으로 다음 사업을 준비한다. 하늘식품과 한성공업의 초반 메인이 끝났다.')],
        previous=['main_haneul_04', 'main_hansung_03'], title='팔 수 있는 것, 계속 만들 수 있는 것')
    _, body = heading_block(paired, '## 9.')
    add('main_early_conclusion', 42, S, '윤하진', lines(body) + [narrated('회사 보고서에는 확인한 사업 관계와 남은 검증 질문을 나눠 적었다. 공동출자 검토는 다음 단계로 이어진다. 자금이 자동 지급되거나 수익이 보장된 것은 아니다. 다음에는 실제 거래를 집행한 기업들의 현재 사업과 기록을 살펴본다.')], [
        ('미래건설 관련 거래를 우선 검토한다', '현장·개발·정산 관계부터 다음 질문을 준비하죠.'),
        ('하늘식품 관련 거래를 우선 검토한다', '유통과 제품 전환의 실행 조건부터 보세요.'),
        ('동방물산 관련 거래를 우선 검토한다', '물량과 거점 운영의 실제 집행 경로를 보시죠.'),
        ('한성공업 관련 거래를 우선 검토한다', '설비·생산·자금 관계의 실행 기록부터 보시죠.')],
        ['main_pair_a_complete', 'main_pair_b_complete'], title='네 회사의 다른 이름, 이어지는 사업망')
    assert len(result) == 20
    return result


def compile_plan():
    ending_text = read('EARLY_STORY_BAD_ENDINGS_KO.md')
    signals = re.findall(r'^\| (?:5|12|20|28|32|40) \|[^\n`]*`([^`\n]+)`', ending_text, re.M)
    blocks = re.findall(r'(?ms)^(?:## [45]\. |### (?:A1|B1|A2|B2)-)[^\n]*\n(.*?)(?=^## |^### |\Z)', ending_text)
    outcomes = ['회사 청산·대표 업무 종료', '자산 보유 법인 전환·대표 퇴임', '재편 사업 폐지·대표 운용 위임 종료', '발굴 사업 폐쇄·새 대표 선임', '회사 해산·잔여 자산 정리',
                '독립 투자 업무 종료·회사 정리', '외부 투자회사 흡수·대표 직위 소멸', '회사 매각·창업자 퇴임', '대표 교체·창업자 지분 정리', '투자 결정권 회수·대표 퇴임',
                '외부 조직 통합·독립 회사 소멸', '사업 매각·새 경영진 선임', '회사 지배권·운용 업무 이전', 'APEX 인수·주인공 대표 권한 종료']
    endings = []
    for signal, body, outcome in zip(signals, blocks, outcomes):
        title = re.search(r'\*\*주인공 회사로 이어지는 후반부 — (.+?)\*\*', body)[1]
        scripted = []
        in_company = False
        for row in body.splitlines():
            if row.startswith('**주인공 회사로 이어지는 후반부'):
                in_company = True; scripted.append(narrated('주인공 투자회사. 며칠 뒤, 사업 검토와 대안 협의가 이어졌다.')); continue
            if row.startswith('**종료의 의미') or row.startswith('이 장면은 '): break
            if row.startswith('**상실:') or row.startswith('**사전 복선:'): continue
            dialogue = lines(row)
            if dialogue: scripted.extend(dialogue); continue
            match = re.match(r'\[(?:후일담|회사 후일담|마지막 독백|최종 독백)\]\s*`?(.*?)`?$', row)
            if match: scripted.append(narrated(match[1].strip('`'))); continue
            if in_company and row.strip() and not row.startswith(('**', '[', '#')):
                scripted.append(narrated(row.replace('**', '')))
        assert scripted and any('주인공 투자회사' in line['text'] for line in scripted)
        endings.append({'signal': signal, 'title': title, 'companyOutcome': outcome, 'lines': scripted})
    assert len(endings) == len(signals) == len(blocks) == 14
    missions = []
    rows = [
        ('mirae-initial', 1, 5, [(M,10)], ['inside_info_01'], signals[0:1]),
        ('haneul-initial', 7, 12, [(H,10)], ['main_haneul_01'], signals[1:2]),
        ('early-a1', 15, 20, [(M,30),(D,10)], ['main_dongbang_01','main_mirae_03'], signals[2:5]),
        ('early-b1', 23, 28, [(H,30),(S,10)], ['main_hansung_01','main_haneul_03'], signals[5:8]),
        ('early-a2', 29, 32, [(M,30),(D,20)], ['main_mirae_04','main_dongbang_03'], signals[8:11]),
        ('early-b2', 34, 40, [(H,30),(S,20)], ['main_haneul_04','main_hansung_03'], signals[11:14])]
    for mission_id, start, due, goals, events, failed in rows:
        missions.append({'id':mission_id,'startDay':start,'dueDay':due,'goals':[{'companyId':c,'percent':p} for c,p in goals],'eventIds':events,'signals':failed})
    return {'missions': missions, 'endings': endings}


def expand_company_intro():
    path = ROOT/'Assets/Story/Editor/AuthoringData.json'
    source = json.loads(path.read_text(encoding='utf-8-sig'))
    beats = [b for b in source['beats'] if not b['id'].startswith('company-premise-')]
    text = read('PROTAGONIST_COMPANY_STAKES_KO.md')
    _, block = heading_block(text, '## 3.')
    additions = lines(block)
    additions.append(narrated('첫 투자 계획은 회사의 초기 사업 검토 대상이다. 기한에 목표를 달성하지 못하면 후속 출자와 대표의 독립 운용이 재검토되어 회사의 종료 서사로 이어진다. 잔여 현금과 회사 경영권은 별개다.'))
    result=[]
    for beat in beats:
        result.append(beat)
        if '작은 투자회사의 등록 서류' in beat['text']:
            for i,line in enumerate(additions):
                result.append({'id':f'company-premise-{i}','sceneTitle':'내 회사의 다음 장','backgroundPath':beat['backgroundPath'],
                    'speaker':0 if line.get('narration') else 1 if line.get('player') else 2,'npcId':line.get('npcId',''),
                    'text':line['text'],'showPlayer':not line.get('narration',False),'goal':0,'choices':[]})
    assert len(result)>len(beats)
    source['beats']=result;path.write_text(json.dumps(source,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return len(result)
