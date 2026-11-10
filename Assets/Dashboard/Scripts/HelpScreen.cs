using System;
using System.Linq;
using TakeOver.Events;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Help
{
    /// <summary>새 기능 추가 시 HelpTopics와 대응 화면 자료를 함께 갱신한다.</summary>
    public sealed class HelpScreen : MonoBehaviour
    {
        private GameObject canvasObject, panel, introduction;
        private Text heading, body;
        private Image photo;
        private ScrollRect scroll;
        private EventNegotiationView template;
        private GameObject photoOverlay;
        public bool IsOpen => panel != null && panel.activeSelf;
        private void EnsureBuilt()
        {
            if (canvasObject != null) return;
            // The tutorial's dialogue clone is destroyed on completion. Keep the scene's event UI as the source.
            template = FindObjectsByType<EventNegotiationView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(view => view.GetComponentInParent<TakeOver.Story.StoryDirector>() == null);
            canvasObject = new GameObject("Help canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25000;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            panel = template.CreateInformationPanel(canvasObject.transform, out heading, out body);
            var background=panel.GetComponent<Image>();
            if(background!=null){var color=background.color;color.a=1;background.color=color;background.raycastTarget=true;}
            Place((RectTransform)panel.transform, 24, 24, 1232, 620);
            var rule = heading.transform.parent.Find("Underline") as RectTransform;
            // Do not inherit the source dialogue block's live animation offset.
            heading.transform.SetParent(panel.transform,false);
            rule.SetParent(panel.transform,false);
            Place(heading.rectTransform, 24, 558, 1000, 30); heading.text = "도움말 · 게임 기능과 용어";
            Place(rule, 24, 548, 1184, 2);
            var viewport = new GameObject("Reading viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(panel.transform, false);
            Place((RectTransform)viewport.transform, 250, 24, 570, 508);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            body.transform.SetParent(viewport.transform, false);
            var rect = body.rectTransform; rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
            body.verticalOverflow = VerticalWrapMode.Overflow; body.horizontalOverflow = HorizontalWrapMode.Wrap;
            var fit = body.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform; scroll.content = rect;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            body.raycastTarget = true;
            var picture = new GameObject("Explanation image", typeof(RectTransform), typeof(Image)); picture.transform.SetParent(panel.transform,false);
            photo = picture.GetComponent<Image>(); photo.preserveAspect = true; photo.raycastTarget = false;
            Place(photo.rectTransform, 846, 170, 360, 340);
            photo.gameObject.AddComponent<Button>().onClick.AddListener(ShowPhoto);
            photo.raycastTarget=true;
            var caption=Instantiate(heading,panel.transform,false);caption.text="사진 선택: 크게 보기";caption.fontSize=14;
            Place(caption.rectTransform,846,130,360,30);
            caption.gameObject.name="Photo caption";
            for (int i=0;i<HelpTopics.Items.Length;i++)
            {
                int index=i;
                var button=template.CreateActionButton(panel.transform,"Help topic "+i,HelpTopics.Items[i].title,()=>Select(index));
                Place((RectTransform)button.transform,24,508-i*38,204,34);
            }
            var close=template.CreateActionButton(panel.transform,"Close help","닫기",Close);
            Place((RectTransform)close.transform,1020,556,180,36);
            panel.SetActive(false);
        }
        public void Open() { EnsureBuilt(); panel.SetActive(true); Select(0); }
        public void Close() { ClosePhoto(); if(panel!=null)panel.SetActive(false); }
        private void ClosePhoto(){if(photoOverlay!=null)Destroy(photoOverlay);photoOverlay=null;}
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if(photoOverlay!=null && UnityEngine.InputSystem.Keyboard.current!=null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)ClosePhoto();
#else
            if(photoOverlay!=null && Input.GetKeyDown(KeyCode.Escape))ClosePhoto();
#endif
        }
        private void OnDestroy(){if(canvasObject!=null)Destroy(canvasObject);}
        private void Select(int index)
        {
            var topic=HelpTopics.Items[index];
            body.text=topic.title+"\n\n"+topic.text+(string.IsNullOrEmpty(topic.image)?"":"\n\n사진은 설명을 위한 예시 화면입니다. 자금·날짜·회사 수치는 현재 플레이 상황과 다를 수 있습니다.");
            photo.sprite=string.IsNullOrEmpty(topic.image)?null:Resources.Load<Sprite>("HelpImages/"+topic.image);
            photo.gameObject.SetActive(photo.sprite!=null);
            panel.transform.Find("Photo caption").gameObject.SetActive(photo.sprite!=null);
            Canvas.ForceUpdateCanvases(); scroll.StopMovement(); scroll.verticalNormalizedPosition=1;
        }
        public static void ShowIntroduction()
        {
            var screen=FindFirstObjectByType<HelpScreen>();
            if(screen!=null)screen.Introduce();
        }
        private void ShowPhoto()
        {
            if(photo.sprite==null)return;
            ClosePhoto();
            var overlay=new GameObject("Help enlarged image",typeof(RectTransform),typeof(Image));overlay.transform.SetParent(canvasObject.transform,false);
            photoOverlay=overlay;
            var rect=(RectTransform)overlay.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            overlay.GetComponent<Image>().color=new Color(0,0,0,.96f);
            overlay.AddComponent<Button>().onClick.AddListener(ClosePhoto);
            var image=new GameObject("Screenshot",typeof(RectTransform),typeof(Image));image.transform.SetParent(overlay.transform,false);
            image.GetComponent<Image>().sprite=photo.sprite;image.GetComponent<Image>().preserveAspect=true;image.GetComponent<Image>().raycastTarget=false;
            Place((RectTransform)image.transform,40,82,1200,600);
            var close=template.CreateActionButton(overlay.transform,"Close enlarged image","닫기",ClosePhoto);
            Place((RectTransform)close.transform,540,24,200,42);
        }
        private void Introduce()
        {
            EnsureBuilt();
            if(introduction!=null)Destroy(introduction);
            introduction=new GameObject("Help introduction",typeof(RectTransform),typeof(Image));
            introduction.transform.SetParent(canvasObject.transform,false);
            var blocker=(RectTransform)introduction.transform; blocker.anchorMin=Vector2.zero;blocker.anchorMax=Vector2.one;blocker.offsetMin=blocker.offsetMax=Vector2.zero;
            introduction.GetComponent<Image>().color=new Color(0,0,0,.65f);
            var notice=template.CreateInformationPanel(introduction.transform,out var title,out var text);
            var noticeBackground=notice.GetComponent<Image>();
            if(noticeBackground!=null){var color=noticeBackground.color;color.a=1;noticeBackground.color=color;}
            Place((RectTransform)notice.transform,280,200,720,320);
            title.text="게임 기능이 기억나지 않을 때";
            text.text="화면 위쪽 메뉴의 ‘도움말’을 선택하면 게임 기능과 용어에 대한 설명을 볼 수 있습니다.\n\n주식 거래, 하루 진행, 이벤트와 협상, 인물 관계와 기억 등을 항목별로 정리해 두었습니다. 오랜만에 돌아오셨을 때도 언제든 다시 확인해 주십시오.\n\n설명이 길면 본문 위에서 마우스 휠로 내려 읽을 수 있습니다.";
            var open=template.CreateActionButton(notice.transform,"Open help","도움말 열기",()=>{Destroy(introduction);FindFirstObjectByType<TopBarNavigationController>().OpenHelp();});
            Place((RectTransform)open.transform,18,16,330,38);
            var confirm=template.CreateActionButton(notice.transform,"Confirm help notice","확인",()=>Destroy(introduction));
            Place((RectTransform)confirm.transform,370,16,330,38);
        }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        { rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height); }
    }
}
