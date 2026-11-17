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
        [SerializeField] private List<FaceArea> faceOverrides = new List<FaceArea>
        {
            
            new FaceArea { npcId = "npc-broker-yoon-hajin", headTop = 0.097f, headHeight = 0.073f, headCenterX = 0.53f }
        };

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
            return defaultFace;
        }
    }
}
