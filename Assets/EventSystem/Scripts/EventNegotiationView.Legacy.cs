using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TakeOver.Events
{
    public sealed partial class EventNegotiationView
    {
        public void show(TodayEventInfo info, EventSystemController controller, Sprite speakerIllustration = null,
            Sprite speakerDimmedIllustration = null)
        {
            initialize();
            ClearDialogueHistory();
            dialogueMode = false;
            configureBodyLayout(false);
            questionBlock.transform.parent.parent.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
            cancelSkip();
            this.controller = controller;
            skipTarget = null;
            ensureSkipButton();
            stopResultRoutine();
            choiceSubmitted = false;
            if (defaultBackground == null) defaultBackground = roomBackground.sprite;
            Sprite background = info.definition.getNegotiationBackground();
            roomBackground.sprite = background != null ? background : defaultBackground;
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition;
            setBlock(questionBlock, true);
            setBlock(playerBlock, false);
            setBlock(responseBlock, false);
            speakerText.text = info.speakerLabel;
            FitNpcName(speakerText, dialogueText);
            responseSpeakerText.text = speakerText.text;
            dialogueText.text = info.dialogue;
            npcIllustration.sprite = speakerIllustration;
            npcStandingIllustration = speakerIllustration;
            npcDimmedIllustration = speakerDimmedIllustration;
            npcIllustration.gameObject.SetActive(speakerIllustration != null);
            playerIllustration.sprite = playerStandingIllustration;
            currentPlayerIllustration = playerStandingIllustration;
            setPlayerFrame(playerInitialFrame);
            playerIllustration.gameObject.SetActive(playerStandingIllustration != null);
            highlightPlayer(false);
            closeButton.onClick.RemoveAllListeners();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            currentDialogue = dialogueText;
            paginate(info.dialogue ?? "");
            page = 0;
            showLegacyQuestionPage(() => showChoices(info, controller));
        }

        private void showLegacyQuestionPage(Action showAnswers)
        {
            dialogueText.text = pages[page];
            RecordCurrentLine();
            if (!HasMorePages)
            {
                SetContinueVisible(false);
                showAnswers();
                return;
            }
            clearChoices();
            choicesRoot.gameObject.SetActive(false);
            showLegacyAction("계속", () => { page++; showLegacyQuestionPage(showAnswers); });
            skipTarget = () => { page = pages.Count - 1; showLegacyQuestionPage(showAnswers); };
            refreshSkipButton();
        }

        public void showCompanyChoices(TodayEventInfo info, EventSystemController controller) => showChoices(info, controller);

        private void showChoices(TodayEventInfo info, EventSystemController controller)
        {
            skipTarget = null;
            SetContinueVisible(false);
            choiceSubmitted = false;
            clearChoices();
            choicesRoot.gameObject.SetActive(true);
            choiceTemplate.gameObject.SetActive(false);
            int choiceCount = controller.getNegotiationChoiceCount();
            for (int i = 0; i < choiceCount; i++)
            {
                int choiceIndex = i;
                createChoice(i, choiceCount, info.resolve(controller.getNegotiationChoiceText(i)),
                    controller.getNegotiationChoiceDescription(i), () => submitChoice(controller, choiceIndex));
            }
            refreshSkipButton();
        }

        private void submitChoice(EventSystemController controller, int choiceIndex)
        {
            if (choiceSubmitted) return;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            controller.confirmChoice(choiceIndex);
        }

        public void showChoiceFailure(string message)
        {
            choiceSubmitted = false;
            foreach (Button button in choices) button.interactable = true;
            var notice = FindFirstObjectByType<FloatingMessage>();
            if (notice != null) notice.Show(message);
            else dialogueText.text = message;
        }

        public void showResult(string dialogue, string playerLine = "") =>
            beginExchange(dialogue, playerLine, showEventResultPage, false);

        private void showEventResultPage()
        {
            responseText.text = pages[page];
            RecordCurrentLine();
            showLegacyAction(HasMorePages ? "계속" : "확인", () =>
            {
                if (HasMorePages) { page++; showEventResultPage(); }
                else controller.closeResult();
            });
        }

        private void showLegacyAction(string label, Action clicked)
        {
            closeButton.GetComponentInChildren<Text>(true).text = label;
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => clicked());
            closeButton.interactable = true;
            SetContinueVisible(true);
        }

        public void showExchange(string dialogue, string playerLine, Action completed) =>
            beginExchange(dialogue, playerLine, completed, true);

        private void beginExchange(string dialogue, string playerLine, Action completed, bool companyQuestion)
        {
            stopResultRoutine();
            // Only presentation is skipped. The controller still owns every choice and effect.
            skipTarget = companyQuestion ? (Action)(() =>
            {
                prepareCompanyQuestion(dialogue);
                page = pages.Count - 1;
                showLegacyQuestionPage(completed);
            }) : controller.closeResult;
            choiceSubmitted = true;
            foreach (Button button in choices) button.interactable = false;
            choicesRoot.gameObject.SetActive(false);
            SetContinueVisible(false);
            resultRoutine = StartCoroutine(animateExchange(dialogue, playerLine, completed, companyQuestion));
            refreshSkipButton();
        }

        private IEnumerator animateExchange(string dialogue, string playerLine, Action completed, bool companyQuestion)
        {
            setBlock(questionBlock, false);
            setBlock(playerBlock, false);
            setBlock(responseBlock, false);
            ((RectTransform)playerBlock.transform).anchoredPosition = playerPosition;
            bool hasPlayerLine = !string.IsNullOrWhiteSpace(playerLine);
            ((RectTransform)responseBlock.transform).anchoredPosition = responsePosition + (hasPlayerLine ? Vector2.zero :
                Vector2.up * (speakerText.rectTransform.localPosition.y - responseSpeakerText.rectTransform.localPosition.y));
            playerIllustration.sprite = playerReplyIllustration;
            currentPlayerIllustration = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame);
            if (hasPlayerLine)
            {
                highlightPlayer(true);
                var label = PlayerSpeaker;
                label.supportRichText = false;
                label.text = ProtagonistName();
                FitPlayerName();
                currentDialogue = playerLineText;
                paginate(playerLine);
                page = 0;
                playerLineText.text = pages[page];
                RecordCurrentLine();
                bool advance = false;
                showLegacyAction("계속", () =>
                {
                    if (HasMorePages) { playerLineText.text = pages[++page]; RecordCurrentLine(); }
                    else
                    {
                        advance = true;
                        SetContinueVisible(false);
                    }
                });
                setBlock(playerBlock, true);
                while (!advance) yield return null;
            }
            if (companyQuestion)
            {
                if (hasPlayerLine)
                {
                    setBlock(playerBlock, false);
                }
                prepareCompanyQuestion(dialogue);
            }
            else
            {
                currentDialogue = responseText;
                paginate(dialogue ?? "");
                page = 0;
                responseText.text = pages[page];
                highlightPlayer(false);
                setBlock(responseBlock, true);
            }
            resultRoutine = null;
            if (companyQuestion) showLegacyQuestionPage(completed);
            else completed();
            refreshSkipButton();
        }

        private void prepareCompanyQuestion(string dialogue)
        {
            setBlock(playerBlock, false); setBlock(responseBlock, false);
            ((RectTransform)questionBlock.transform).anchoredPosition = questionPosition;
            currentDialogue = dialogueText;
            paginate(dialogue ?? "");
            page = 0;
            dialogueText.text = pages[page];
            currentPlayerIllustration = playerReplyIllustration;
            setPlayerFrame(playerReplyFrame); highlightPlayer(false);
            setBlock(questionBlock, true);
        }
    }
}
