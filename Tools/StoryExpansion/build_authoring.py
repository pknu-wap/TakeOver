"""Compile the reviewed Markdown scripts into Unity authoring JSON (no game assets modified)."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "Docs"
NPC = {
    "윤하진": "npc-broker-yoon-hajin", "정수아": "npc-investigative-reporter-jung-sua",
    "김태성": "npc-institutional-investor-kim-taeseong", "박상현": "npc-political-lobbyist-park-sanghyeon",
    "이민규": "npc-economic-reporter-lee-mingyu", "강준혁": "npc-mirae-construction-ceo-kang-junhyeok",
    "서지아": "npc-mirae-construction-cfo-seo-jia", "박도식": "npc-mirae-construction-field-director-park-dosik",
    "김하린": "npc-haneul-food-ceo-kim-harin", "오재성": "npc-haneul-food-cfo-oh-jaeseong",
    "강재현": "npc-dongbang-trading-ceo-kang-jaehyeon", "박민우": "npc-dongbang-trading-hr-director-park-minu",
    "조민석": "npc-hansung-industry-ceo-jo-minseok", "최미정": "npc-hansung-industry-labor-director-choi-mijeong",
}
COMPANIES = ["mirae-construction", "haneul-food", "dongbang-trading", "hansung-industry"]
OWNERS = {"M": ["강준혁", "박도식", "서지아"], "H": ["김하린", "오재성", "김하린"], "D": ["강재현", "박민우", "박민우"], "S": ["조민석", "최미정", "조민석"]}
EXTERNAL = {"Y": "윤하진", "J": "정수아", "K": "김태성", "P": "박상현", "L": "이민규"}
NEW_IDS = {
    "M3": "sub_mirae_payment_calendar_01", "H3": "sub_haneul_package_line_01", "D3": "sub_dongbang_address_check_01", "S3": "sub_hansung_inspection_note_01",
    "Y2": "sub_yoon_fee_scope_01", "Y3": "sub_yoon_introduction_01", "J1": "sub_jung_quote_check_01", "J2": "sub_jung_explanation_01", "J3": "sub_jung_after_article_01",
    "K1": "sub_kim_pass_reason_01", "K2": "sub_kim_cash_room_01", "K3": "sub_kim_seasonality_01", "P2": "sub_park_public_briefing_01", "P3": "sub_park_scope_note_01", "L2": "sub_lee_headline_check_01", "L3": "sub_lee_field_question_01",
}


def read(name):
    return (DOCS / name).read_text(encoding="utf-8-sig")


def sections(text):
    return {m[1]: (m[2].strip(), m[3]) for m in re.finditer(r"(?ms)^## ([A-Z]+(?:-[A-Z])?\d+|\d+)\. ([^\n]+)\n(.*?)(?=^## |\Z)", text)}


def lines(text):
    result = []
    for row in text.splitlines():
        match = re.match(r"\*\*([^:*]+):\*\*\s*(.*)", row)
        if match and match[1] in (*NPC, "주인공"):
            result.append({"npcId": NPC.get(match[1], ""), "player": match[1] == "주인공", "text": match[2]})
        elif row.startswith("[서술] "):
            result.append({"narration": True, "text": row[5:]})
    return result


def choices(body, speaker, values=None, repeat=True):
    body = normalize_choices(body)
    result = []
    for i, match in enumerate(re.finditer(r"(?ms)^### ([ABC])\. ([^\n]+)\n(.*?)(?=^### |^## |\Z)", body)):
        response = lines(match[3])
        assert response, match[2]
        player = next((x["text"] for x in response if x.get("player")), match[2])
        npc = "\n\n".join(x["text"] for x in response if x.get("npcId"))
        interpretation = re.search(r"(?:결과·기억|기록|관찰 기록 제안)[:：]\s*([^\n]+)", match[3])
        delta = {"respect": 1 if i == 0 else 0, "interest": 1 if i == 1 else 0}
        if not repeat:
            delta = {"respect": 1 if i == 0 else 0}
        result.append({"choiceText": match[2], "choiceDescription": "이번 답변을 관계와 기억에 반영합니다. 현금·지분 변화 없음.",
                       "playerLine": player, "resultDialogue": npc, "resultLines": response,
                       "resultValue": values[i] if values else f"answer_{i}",
                       "npcResult": {"targetNpcIds": [NPC[speaker]], "memoryType": 8, "durationTurns": 3, "strength": 1, "publicity": 0, "reliability": 1,
                                     "interpretation": (interpretation[1].split("효과 방향")[0] if interpretation else "이번 대화에서 " + match[2])[:350], "relationDelta": delta}})
    return result


def normalize_choices(body):
    return re.sub(r"(?m)^\*\*([ABC])\. ([^\n]+)\*\*$", r"### \1. \2", body)


def definition(event_id, title, speaker, opening, answers, company="", **extra):
    data = {"eventId": event_id, "category": 0, "title": title, "description": title + "에 관한 면담 요청입니다. 내용과 비용을 확인한 뒤 만나십시오.",
            "apCost": 1, "startButtonText": "만나기", "speakerNpcId": NPC[speaker], "speakerName": speaker, "speakerRole": "",
            "companyIds": [company] if company else [], "openingLines": opening, "dialogue": next((x["text"] for x in opening if not x.get("player") and not x.get("narration")), ""),
            "choices": answers, "validDays": 2, "speakerSource": 0, "choiceGeneration": 0, "conditions": []}
    data.update(extra)
    return data


def compile_general():
    all_sections = sections(read("REPEATABLE_SIDE_COMPANY_SCRIPT_KO.md")) | sections(read("REPEATABLE_SIDE_NEUTRAL_SCRIPT_KO.md"))
    definitions = {}
    for key, (title, body) in all_sections.items():
        index = int(key[1:]) - 1
        company = COMPANIES[list(OWNERS).index(key[0])] if key[0] in OWNERS else ""
        speaker = OWNERS[key[0]][index] if company else EXTERNAL[key[0]]
        shared = "management_defense_01" if company and index == 0 else "restructuring_01" if company and index == 1 else None
        event_id = shared or {"Y1": "stock_manipulation_01", "P1": "political_donation_01", "L1": "press_interview_01"}.get(key) or NEW_IDS[key]
        values = {"management_defense_01": ["investment_review", "takeover"], "restructuring_01": ["mass_layoff", "gradual", "retain"], "stock_manipulation_01": ["accept", "reject"], "political_donation_01": ["sponsor", "public_donation", "reject"], "press_interview_01": ["disclose", "evade"]}.get(event_id)
        opening = lines(body.split("### A.")[0])
        answers = choices(body, speaker, values)
        assert len(answers) == (2 if key in ["M1", "H1", "D1", "S1", "Y1", "L1"] else 3), key
        if shared:
            data = definitions.setdefault(event_id, {"eventId": event_id, "repeatable": True, "repeatCooldown": 7 if index == 1 else 3, "companyVariants": []})
            data["companyVariants"].append({"companyId": company, "title": title, "description": title + "에 관한 회사 측의 면담 요청입니다.", "openingLines": opening, "choices": answers})
            continue
        data = definition(event_id, title, speaker, opening, answers, company, repeatable=True, repeatCooldown=10 if key in ["Y1", "P1"] else 3, repeatGroup=NPC[speaker] if not company else "")
        if company:
            data["conditions"] = [{"kind": 2, "value": 10, "comparison": 0}]
        if key == "Y1":
            # Preserve the existing stock-selection questions, eligibility and art.
            data = {k: v for k, v in data.items() if k not in ["conditions", "choiceGeneration", "companyIds", "speakerSource", "validDays"]}
            data["choices"][0]["stockSurgeMinRatio"] = .3
            data["choices"][0]["stockSurgeMaxRatio"] = .4
            data["choices"][0]["resultLines"] = lines(body.split("[종목 선택 후]")[1].split("### B.")[0])
            data["choices"][0]["choiceDescription"] = "선택 종목에 기존 다음 턴 급등 효과를 적용합니다. 매도 수익은 보장하지 않습니다."
        if key == "P1":
            for answer, cash in zip(data["choices"], [-100, -50, 0]):
                answer["cashDelta"] = cash
                answer["choiceDescription"] = f"자금 {abs(cash):,} 지출." if cash else "자금 변화 없음."
            data["choices"][0]["npcResult"]["memoryType"] = 7
            data["validDays"] = 3
        if key == "L1":
            data["validDays"] = 3
            data["choices"][1]["npcResult"]["relationDelta"] = {"trust": -2, "interest": 1}
        definitions[event_id] = data
    first = sections(read("EARLY_STORY_NEUTRAL_SCRIPT_KO.md"))
    for key, event_id, speaker in [("N1", "neutral-lee-first-interview", "이민규"), ("N2", "neutral-jung-first-record", "정수아"), ("N3", "neutral-kim-first-review", "김태성"), ("N4", "neutral-park-first-briefing", "박상현")]:
        title, body = first[key]
        body = re.sub(r"(?m)^\*\*[ABC]\. ([^\n]+)\*\*$", r"### A. \1", body)
        opening = lines(body.split("### A.")[0])
        answers = choices(body, speaker, repeat=False)
        assert len(answers) == 3, key
        # First interview material is generic; no forced sale or extra company selector.
        if key == "N3":
            opening = [x for x in opening if "현재 MARKET" not in x["text"]]
        definitions[event_id] = definition(event_id, title, speaker, opening, answers, firstContact=True, validDays=10000)
    assert len(definitions) == 25, len(definitions)  # 21 repeat definitions plus 4 first contacts; greenmail remains unchanged.
    return list(definitions.values())


def simple_answers(speaker, texts):
    return [{"choiceText": text, "choiceDescription": "이번 대화의 선택과 관찰 사실을 기록합니다. 현금·지분 변화 없음.", "playerLine": text,
             "resultDialogue": reply, "resultValue": f"answer_{i}", "resultLines": [{"player": True, "text": text}, {"npcId": NPC[speaker], "text": reply}]} for i, (text, reply) in enumerate(texts)]


def compile_campaign():
    arc = sections(read("EARLY_STORY_MAIN_ARC_KO.md"))
    company = {key: (title, normalize_choices(body)) for key, (title, body) in sections(read("EARLY_STORY_COMPANY_SCRIPT_KO.md")).items()}
    schedule = [
        ("main_mirae_02", "4", 6, 0, COMPANIES[0], "서지아", "inside_info_01", "C-M2"),
        ("main_haneul_contact", "5", 7, 0, COMPANIES[1], "이민규", "main_mirae_02", None),
        ("main_haneul_01", "6", 12, 7, COMPANIES[1], "김하린", "main_haneul_contact", "C-H1"),
        ("main_haneul_02", "6", 13, 0, COMPANIES[1], "정수아", "main_haneul_01", None),
        ("main_dongbang_contact", "7", 15, 0, COMPANIES[2], "박민우", "main_haneul_02", None),
        ("main_dongbang_01", "8", 20, 15, COMPANIES[2], "강재현", "main_dongbang_contact", "C-D1"),
        ("main_dongbang_02", "8", 21, 0, COMPANIES[2], "이민규", "main_dongbang_01", None),
        ("main_hansung_contact", "9", 23, 0, COMPANIES[3], "최미정", "main_dongbang_02", None),
        ("main_hansung_01", "10", 28, 23, COMPANIES[3], "조민석", "main_hansung_contact", "C-S1"),
        ("main_hansung_02", "11", 29, 0, COMPANIES[3], "정수아", "main_hansung_01", None),
    ]
    result = []
    for event_id, section, day, start, cid, speaker, previous, intro_key in schedule:
        title, body = arc[section]
        if event_id.endswith("_02") and section in ["6", "8"]:
            body = body.split("### ")[1]
            title = body.splitlines()[0]
        elif section in ["6", "8"]:
            body = body.split("### ")[0]
        opening = lines(body)
        if intro_key:
            intro_title, intro_body = company[intro_key]
            opening = lines(intro_body.split("### A.")[0]) + opening
            answers = choices(intro_body, speaker, repeat=False)
            if event_id == "main_mirae_02":
                opening.insert(0, {"npcId": NPC["서지아"], "text": "오늘은 주주에게 공유할 수 있는 요약을 볼게요. 협력 여부와 별개로 현재 계획을 설명드리는 자리예요."})
            # The company introduction choices retain their individual responses.
        else:
            answers = simple_answers(speaker, [("확인한다", "네. 오늘 확인한 범위에서 기록하겠습니다.")])
            answers[0]["choiceText"] = "확인"
            answers[0]["playerLine"] = "확인했습니다. 계속 살펴보겠습니다."
            answers[0]["resultLines"][0]["text"] = answers[0]["playerLine"]
        if event_id == "main_hansung_02":
            opening += lines(company["X1"][1].split("### 관심 회사 선택")[0])
            answers = simple_answers("윤하진", [
                ("미래건설을 우선한다", "현장과 계약을 이어갈 조건부터 보시죠. 지금까지 들은 내용을 바탕으로 다음 계획을 정하면 되겠네요."),
                ("하늘식품을 우선한다", "브랜드와 회수 구조를 함께 보세요. 좋은 제품을 계속 만들 조건이 필요하니까요."),
                ("동방물산을 우선한다", "확장 속도와 전환 비용을 함께 보시죠. 다른 회사보다 빨리 결정해야 하는지도 따로 정하시고요."),
                ("한성공업을 우선한다", "생산 기반과 다음 사람에게 이어질 기술을 같이 보세요. 회사가 남을 조건을 묻는 거죠."),
            ])
        if event_id == "main_haneul_01":
            answers = simple_answers("김하린", [
                ("브랜드와 거래 조건을 함께 본다", "그렇게 봐주시면 좋겠어요. 손님이 다시 사는 이유와 저희가 계속 만들 조건을 같이 정리해드릴게요. 오늘은 협력을 검토하는 단계로 이해하겠습니다."),
                ("회수 구조부터 검토한다", "네. 오 이사님하고 회수 조건부터 준비하겠습니다. 다만 숫자를 볼 때 제품을 찾는 손님이 왜 남았는지도 함께 봐주세요."),
                ("경영 협력은 보류한다", "알겠습니다. 오늘 설명을 들으신 것만으로 협력을 약속하신 건 아니니까요. 공개할 수 있는 내용은 계속 안내해드릴게요."),
            ])
        if event_id == "main_dongbang_01":
            answers = simple_answers("강재현", [
                ("현재 사업과 과거 손실을 함께 본다", "좋습니다. 과거에 당했다는 설명으로 끝내지는 않겠소. 지금은 무엇이 달라졌는지와 다음 투자 비용을 함께 보시죠."),
                ("당시 결정 주체부터 확인한다", "그쪽은 확인할 수 있는 범위를 알려드리겠습니다. 다만 그 일이 끝날 때까지 회사의 현재 계획을 전부 멈출 수는 없다는 점은 이해해주십시오."),
                ("협력은 보류하고 설명만 듣는다", "알겠습니다. 투자자로서 확인하는 것과 함께 사업을 하는 건 다른 일이죠. 오늘 질문은 그 범위로 이해하겠습니다."),
            ])
        if event_id == "main_hansung_01":
            answers = simple_answers("조민석", [
                ("과거 결정 범위를 함께 확인한다", "확인할 수 있는 내용은 설명하겠습니다. 다만 그 이름 하나로 모든 결론을 내리지는 마십시오. 실제로 무엇을 한 상대인지 더 봐야 합니다."),
                ("투자와 조사를 구분한다", "그렇게 하십시오. 회사가 지금 만들 수 있는 것과 과거 거래를 구분해 보는 데 동의합니다. 어느 쪽도 대충 넘길 일은 아니오."),
                ("자료를 정리한 뒤 답한다", "알겠습니다. 오늘 자리에서 약속을 받아내려는 건 아닙니다. 다시 질문할 때는 어떤 부분을 더 확인하고 싶은지 알려주십시오."),
            ])
        if opening[-1].get("player") or opening[-1].get("narration"):
            opening.append({"npcId": NPC[speaker], "text": "오늘 확인한 내용은 어떻게 보고 계십니까?" if len(answers) > 1 else "내용을 확인하셨으면 말씀해주세요."})
        assert opening and answers, event_id
        data = definition(event_id, title, speaker, opening, answers, cid, category=1, campaignDay=day, missionStartDay=start,
                          prerequisiteEventId=previous, failureSignal=(cid + "-investment-deadline-failed") if start else "", apCost=0, validDays=10000)
        if start:
            data["conditions"] = [{"kind": 2, "value": 10, "comparison": 0}]
        if event_id.endswith("_contact"):
            data["description"] = f"{day}일차 투자 검토 연락입니다. {dict(zip(COMPANIES,['미래건설','하늘식품','동방물산','한성공업']))[cid]} 지분 10%를 {dict(zip(COMPANIES,[5,12,20,28]))[cid]}일차 시작 전까지 확보하십시오."
            data["choices"][0]["choiceDescription"] = data["description"]
        result.append(data)
    return result


def compile_intro():
    path = ROOT / "Assets/Story/Editor/AuthoringData.json"
    authoring = json.loads(path.read_text(encoding="utf-8-sig"))
    beats = [b for b in authoring["beats"] if not b["id"].startswith(("neutral-intro-", "company-premise-"))]
    tutorial = sections(read("EARLY_STORY_NEUTRAL_SCRIPT_KO.md"))
    insertions = {"intro-074": ("T1", "이민규"), "intro-088": ("T2", "정수아"), "lesson-033": ("T3", "김태성"), "intro-090": ("T4", "박상현")}
    expanded = []
    for beat in beats:
        if beat["id"] in insertions:
            key, speaker = insertions[beat["id"]]
            title, body = tutorial[key]
            initial = body.split("### 선택")[0]
            response_body = body.split("### 선택")[1].split("### 공통 마무리")[0]
            response_body = re.sub(r"(?m)^\*\*([ABC])\. ([^\n]+)\*\*$", r"### \1. \2", response_body)
            answers = choices(response_body, speaker, repeat=False)
            assert len(answers) == 3, key
            for n, line in enumerate(lines(initial)):
                expanded.append(beat_from_line(key, title, n, line))
            expanded.append({"id": f"neutral-intro-{key}-choice", "sceneTitle": title, "backgroundPath": "Assets/Story/Art/HotelLounge.png", "speaker": 2,
                             "npcId": NPC[speaker], "text": "어떤 생각이신지 듣고 싶습니다.", "showPlayer": True, "goal": 0, "skipDefaultChoice": 0,
                             "choices": [{"title": a["choiceText"], "playerLine": a["playerLine"], "response": a["resultDialogue"], "targetNpcId": NPC[speaker], "memoryType": 8,
                                          "interpretation": a["npcResult"]["interpretation"], "relationDelta": a["npcResult"]["relationDelta"]} for a in answers]})
            tail = body.split("### 공통 마무리")[1]
            for n, line in enumerate(lines(tail)):
                expanded.append(beat_from_line(key + "-tail", title, n, line))
            # Show functional guidance in the existing event-style tutorial information panel.
            info = re.search(r"\[설명창[^\]]*\]\s*\n\s*([^\n]+)", tail)
            if info:
                expanded.append({"id": f"neutral-intro-{key}-help", "sceneTitle": title, "backgroundPath": "Assets/Story/Art/HotelLounge.png", "speaker": 4, "text": info[1],
                                 "tutorialTitle": "외부 인물과의 만남", "tutorialText": info[1], "showPlayer": False, "goal": 0, "choices": []})
        if beat["id"] == "intro-092":
            beat["text"] = "아직 모르는 회사와 거래가 많다. 다음 판단도 누구의 설명을 듣고 무엇을 확인할지부터 정해야 한다."
        expanded.append(beat)
    authoring["beats"] = expanded
    path.write_text(json.dumps(authoring, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return len(expanded)


def beat_from_line(key, title, number, line):
    return {"id": f"neutral-intro-{key}-{number}", "sceneTitle": title, "backgroundPath": "Assets/Story/Art/HotelLounge.png",
            "speaker": 0 if line.get("narration") else 1 if line.get("player") else 2, "npcId": line.get("npcId", ""), "text": line["text"], "showPlayer": not line.get("narration", False), "goal": 0, "choices": []}


if __name__ == "__main__":
    from paired_authoring import compile_paired_campaign, compile_plan, expand_company_intro
    from dialogue_revision import revise, simplify_instructions, export_review
    from story_polish import apply_general_cards, expand_campaign_story
    general = apply_general_cards(revise(compile_general()))
    legacy = compile_campaign()
    campaign = compile_paired_campaign(legacy)
    campaign = expand_campaign_story(revise(campaign))
    legacy = expand_campaign_story(revise(legacy), legacy=True)
    output = ROOT / "Assets/Story/Editor/ExpansionData.json"
    output.write_text(json.dumps({"general": general, "legacyCampaign": legacy, "campaign": campaign, "plan": compile_plan()}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    compile_intro()
    count = expand_company_intro()
    simplify_instructions(ROOT / 'Assets/Story/Editor/AuthoringData.json')
    export_review(json.loads(output.read_text(encoding='utf-8')), DOCS / 'PRESENTATION_REVISION_SCRIPT_KO.md')
    print(f"General definitions: {len(general)}; campaign scenes: {len(campaign)}; intro beats: {count}")
