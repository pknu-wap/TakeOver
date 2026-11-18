using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Journal
{
    
    public sealed class JournalPersonChip : MonoBehaviour
    {
        
        [Serializable]
        public sealed class FaceArea
        {
            [Tooltip("이 값을 쓸 NPC ID (기본값에서는 비워 둔다)")]
            public string npcId = "";
            [Tooltip("그림 맨 위 → 머리 꼭대기 (그림 높이 대비)")]
            [Range(0f, 1f)] public float headTop = 0.03f;
            [Tooltip("머리 꼭대기 → 턱 길이 (그림 높이 대비)")]
            [Range(0.01f, 1f)] public float headHeight = 0.09f;
            [Tooltip("얼굴 중심의 가로 위치 (그림 너비 대비, 0.5 = 가운데)")]
            [Range(0f, 1f)] public float headCenterX = 0.5f;
        }

        [Tooltip("마스크 안에 들어있는 인물 이미지")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;

        [Header("칩 안에서 얼굴이 놓일 자리 (마스크 왼쪽 위 기준)")]
        [Tooltip("머리 꼭대기 → 턱이 칩 안에서 차지할 높이")]
        [SerializeField] private float faceHeightInChip = 28f;
        [Tooltip("마스크 위 끝 → 머리 꼭대기 거리")]
        [SerializeField] private float faceTopInChip = 2f;
        [Tooltip("마스크 왼쪽 끝 → 얼굴 중심 거리")]
        [SerializeField] private float faceCenterXInChip = 35f;

        [Header("스탠딩 일러스트 속 얼굴 위치")]
        [Tooltip("대부분의 NPC에 쓰는 값")]
        [SerializeField] private FaceArea defaultFace = new FaceArea();
        [Tooltip("구도가 다른 NPC만 따로 지정한다")]
        [SerializeField] private List<FaceArea> faceOverrides = new List<FaceArea>();

        private static readonly Dictionary<string, FaceArea> measuredFaces = new Dictionary<string, FaceArea>
        {
            ["npc-hansung-industry-labor-director-choi-mijeong"] = measured(0.034f),
            ["npc-hansung-industry-ceo-jo-minseok"] = measured(0.011f),
            ["npc-investigative-reporter-jung-sua"] = measured(0.048f),
            ["npc-dongbang-trading-ceo-kang-jaehyeon"] = measured(0.009f),
            ["npc-mirae-construction-ceo-kang-junhyeok"] = measured(0.004f),
            ["npc-haneul-food-ceo-kim-harin"] = measured(0.021f),
            ["npc-institutional-investor-kim-taeseong"] = measured(0.013f),
            ["npc-economic-reporter-lee-mingyu"] = measured(0.005f),
            ["npc-haneul-food-cfo-oh-jaeseong"] = measured(0.004f),
            ["npc-mirae-construction-field-director-park-dosik"] = measured(0.005f),
            ["npc-dongbang-trading-hr-director-park-minu"] = measured(0.006f),
            ["npc-political-lobbyist-park-sanghyeon"] = measured(0.003f),
            ["npc-mirae-construction-cfo-seo-jia"] = measured(0.023f),
            ["npc-broker-yoon-hajin"] = new FaceArea { headTop = 0.098f, headHeight = 0.073f, headCenterX = 0.53f }
        };

        private static FaceArea measured(float headTop) => new FaceArea { headTop = headTop };

        public void setup(JournalPerson person)
        {
            nameText.text = person.displayName;

            
            bool hasPortrait = person.portrait != null;
            portraitImage.enabled = hasPortrait;
            if (!hasPortrait) return;

            portraitImage.sprite = person.portrait;
            fitFace(person.npcId, person.portrait);
        }

        
        private void fitFace(string npcId, Sprite sprite)
        {
            FaceArea face = findFace(npcId);

            float imageHeight = faceHeightInChip / face.headHeight;
            float imageWidth = imageHeight * (sprite.rect.width / sprite.rect.height);

            RectTransform rect = portraitImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);   
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(face.headCenterX, 1f - face.headTop);
            rect.sizeDelta = new Vector2(imageWidth, imageHeight);
            rect.anchoredPosition = new Vector2(faceCenterXInChip, -faceTopInChip);
            portraitImage.preserveAspect = true;
        }

        private FaceArea findFace(string npcId)
        {
            foreach (FaceArea face in faceOverrides)
            {
                if (face != null && face.npcId == npcId) return face;
            }
            if (npcId != null && measuredFaces.TryGetValue(npcId, out FaceArea measuredFace)) return measuredFace;
            return defaultFace;
        }
    }
}
