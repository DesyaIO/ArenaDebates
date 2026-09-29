using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class LearningDialogView : MonoBehaviour
{
    public TMP_Text MethodText, TopicText, PlayerPositionText, OpponentPositionText;
    public TMP_Text PlayerHealthText, OpponentHealthText, OpponentText, HintText, FeedbackText, TranscriptText, StatusText, ProgressText;
    public Button StartButton, RecordButton, RetryButton, BackButton;
    public Button SubmitButton, RetakeButton, ContinueButton, ReturnToTranscriptButton;
    public Button SummaryRestartButton, SummaryBackButton;
    public Button MenuButton;
    public Button HintButton;
    public Button[] TopBackButtons;
    public GameObject DialogueScreen, TranscriptScreen, FeedbackScreen, SummaryScreen;
    public TMP_Text StageText, StepText, RecognizedText, ResultTitleText, SummaryText, TechniqueText;
    [Header("Existing summary screen content")]
    public TMP_Text HeroEyebrow, HeroTitle, HeroSubtitle, SummaryDisclaimer;
    public TMP_Text MethodsLabel, Method1, Method2, Method3, SummaryStepCounter;
    public TMP_Text StrengthLabel, StrengthText, GrowthLabel, GrowthText, SummaryAdviceTitle;
    public Image SummaryAvatar;
    public TMP_Text[] StageLabels, StepLabels, StatusLabels;
    public TMP_Text DialogueHintText;
    public TMP_InputField TranscriptInput;
    public Image OpponentAvatar;
    private GameObject _currentScreen;
    private Coroutine _transition;
    private bool _busy;
    private bool _controlsBusy, _controlsReady, _controlsRecording, _controlsCanRecord, _controlsCanRetry, _controlsFinished;
    private readonly List<ScrollableArea> _scrollableAreas = new List<ScrollableArea>();

    private sealed class ScrollableArea
    {
        public TMP_Text Text;
        public RectTransform Viewport;
        public RectTransform Content;
        public ScrollRect Scroll;
        public TMP_InputField Input;
        public string PreviousText;
    }

    void Awake()
    {
        // Navigation is handled by one persistent menu button. Hide the old
        // per-screen back arrows and the obsolete "return to utterance" action.
        if (TopBackButtons != null)
            foreach (var button in TopBackButtons)
                if (button != null) button.gameObject.SetActive(false);
        if (ReturnToTranscriptButton != null) ReturnToTranscriptButton.gameObject.SetActive(false);
        if (SummaryBackButton != null) SummaryBackButton.gameObject.SetActive(false);
        if (MenuButton != null && DialogueScreen != null && DialogueScreen.transform.parent != null)
        {
            MenuButton.transform.SetParent(DialogueScreen.transform.parent, false);
            MenuButton.transform.SetAsLastSibling();
            MenuButton.gameObject.SetActive(true);
        }

        // AI responses and recognized speech can be much longer than the sample copy
        // used while laying out the screen. Keep them inside their cards and scroll.
        MakeScrollable(TopicText);
        MakeScrollable(MethodText);
        MakeScrollable(PlayerPositionText);
        MakeScrollable(OpponentPositionText);
        MakeScrollable(OpponentText);
        MakeScrollable(HintText);
        MakeScrollable(FeedbackText);
        MakeScrollable(TranscriptText);
        MakeScrollable(SummaryText);
        MakeScrollable(DialogueHintText);
        MakeScrollable(StatusText);
        MakeScrollable(TranscriptInput, RecognizedText);
        ShowHealth(null);
        if (HintButton != null) HintButton.onClick.AddListener(ToggleHint);
    }

    void LateUpdate()
    {
        foreach (var area in _scrollableAreas)
        {
            if (area.Text == null || area.Viewport == null || area.Content == null) continue;

            // Measure against the available width, then give the content all the
            // height it needs. RectMask2D clips it to the original card bounds.
            area.Text.ForceMeshUpdate();
            float width = Mathf.Max(1f, area.Viewport.rect.width - 20f);
            float height = area.Text.GetPreferredValues(width, 100000f).y + 20f;
            if (float.IsNaN(height) || float.IsInfinity(height)) height = area.Viewport.rect.height;
            height = Mathf.Max(area.Viewport.rect.height, height);
            area.Content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            area.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (area.PreviousText != area.Text.text)
            {
                area.PreviousText = area.Text.text;
                if (area.Scroll != null) area.Scroll.verticalNormalizedPosition = 1f;
            }
        }
    }

    void MakeScrollable(TMP_Text text)
    {
        CreateScrollArea(text, null);
    }

    void MakeScrollable(TMP_InputField input, TMP_Text text)
    {
        if (input == null || text == null || input.textComponent != text) return;
        var area = CreateScrollArea(text, input);
        if (area != null)
        {
            input.textViewport = area.Viewport;
            // TMP_InputField caches the viewport mask in OnEnable. Re-enable it
            // after installing the runtime viewport so caret scrolling is clipped.
            if (input.isActiveAndEnabled)
            {
                input.enabled = false;
                input.enabled = true;
            }
        }
    }

    ScrollableArea CreateScrollArea(TMP_Text text, TMP_InputField input)
    {
        if (text == null || text.rectTransform == null) return null;
        var original = text.rectTransform;
        var parent = original.parent;
        if (parent == null) return null;

        int sibling = original.GetSiblingIndex();
        var viewportObject = new GameObject(text.name + "_ScrollViewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.anchorMin = original.anchorMin;
        viewport.anchorMax = original.anchorMax;
        viewport.pivot = original.pivot;
        viewport.anchoredPosition3D = original.anchoredPosition3D;
        viewport.sizeDelta = original.sizeDelta;
        viewport.localRotation = original.localRotation;
        viewport.localScale = original.localScale;
        viewport.SetSiblingIndex(sibling);

        var hitArea = viewportObject.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        var contentObject = new GameObject(text.name + "_ScrollContent", typeof(RectTransform));
        var content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        original.SetParent(content, false);
        original.anchorMin = new Vector2(0f, 1f);
        original.anchorMax = new Vector2(1f, 1f);
        original.pivot = new Vector2(0.5f, 1f);
        original.anchoredPosition = Vector2.zero;
        original.sizeDelta = Vector2.zero;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        VisibleScrollbar.Ensure(scroll);

        var area = new ScrollableArea
        {
            Text = text,
            Viewport = viewport,
            Content = content,
            Scroll = scroll,
            Input = input,
            PreviousText = text.text
        };
        _scrollableAreas.Add(area);
        return area;
    }

    public void ShowContext(string method, GameSession session)
    {
        MethodText.text = "МОДУЛЬ / " + method.ToUpperInvariant();
        TopicText.text = session.TopicDescription;
        PlayerPositionText.text = "Ваша позиция\n" + session.PlayerPosition;
        OpponentPositionText.text = "Позиция оппонента\n" + session.OpponentPosition;
        ShowHealth(session);
    }

    public void SetStep(int current, int total, string stage)
    {
        if (StageText != null) StageText.text = stage;
        if (StepText != null) StepText.text = $"ХОД {Mathf.Clamp(current, 1, Mathf.Max(1,total)):00} / {Mathf.Max(1,total):00}";
        if (StageLabels != null) foreach (var item in StageLabels) if (item != null) item.text = stage;
        if (StepLabels != null) foreach (var item in StepLabels) if (item != null) item.text = $"ХОД {Mathf.Clamp(current, 1, Mathf.Max(1,total)):00} / {Mathf.Max(1,total):00}";
    }

    public void SetStatus(string value)
    {
        if (StatusText != null) StatusText.text = value;
        if (StatusLabels != null) foreach (var item in StatusLabels) if (item != null) item.text = value;
    }

    public void SetRecognized(string value)
    {
        if (TranscriptInput != null) TranscriptInput.text = value;
        if (RecognizedText != null) RecognizedText.text = "«" + value + "»";
    }

    public void SetOpponentAvatar(Sprite sprite)
    {
        if (OpponentAvatar != null) OpponentAvatar.sprite = sprite;
    }

    public void ToggleHint()
    {
        if (DialogueHintText == null) return;
        bool show = !DialogueHintText.gameObject.activeSelf;
        DialogueHintText.gameObject.SetActive(show);
        var label = HintButton != null ? HintButton.GetComponentInChildren<TMP_Text>() : null;
        if (label != null) label.text = show ? "✦   Скрыть подсказку     −" : "✦   Запросить подсказку     +";
    }

    public void ShowDialogue()
    {
        ShowScreen(DialogueScreen);
        if (RecordButton != null) RecordButton.gameObject.SetActive(true);
        if (SubmitButton != null) SubmitButton.gameObject.SetActive(false);
        if (RetakeButton != null) RetakeButton.gameObject.SetActive(false);
        if (ContinueButton != null) ContinueButton.gameObject.SetActive(false);
        if (StartButton != null) StartButton.gameObject.SetActive(true);
    }

    public void ShowTranscriptScreen()
    {
        ShowScreen(TranscriptScreen);
        if (SubmitButton != null) SubmitButton.gameObject.SetActive(true);
        if (RetakeButton != null) RetakeButton.gameObject.SetActive(true);
    }

    public void ShowFeedback(bool complete)
    {
        ShowScreen(FeedbackScreen);
        if (ContinueButton != null) ContinueButton.gameObject.SetActive(!complete);
        if (ReturnToTranscriptButton != null) ReturnToTranscriptButton.gameObject.SetActive(false);
        if (StartButton != null) StartButton.gameObject.SetActive(false);
        if (RecordButton != null) RecordButton.gameObject.SetActive(false);
    }

    public void ShowSummary(string text)
    {
        if (SummaryText != null) SummaryText.text = text;
        ShowScreen(SummaryScreen);
    }

    public void ShowSummary(string method, int turns, int applied, string feedback, string growthAdvice,
        string[] observedCategories, Sprite avatar)
    {
        turns = Mathf.Max(0, turns);
        applied = Mathf.Clamp(applied, 0, turns);
        string safeMethod = string.IsNullOrWhiteSpace(method) ? "Учебный метод" : method.Trim();
        string ratio = $"{applied} ИЗ {turns}";
        SetText(HeroEyebrow, "ИТОГ УЧЕБНОГО ДИАЛОГА");
        SetText(HeroTitle, applied == turns && turns > 0 ? "Метод в работе" : "Практика завершена");
        SetText(HeroSubtitle, $"«{safeMethod}» применён в {applied} из {turns} реплик.");
        SetText(MethodsLabel, "МЕТОД И ПРИЁМЫ");
        SetText(Method1, safeMethod.ToUpperInvariant());
        string firstCategory = FindCategory(observedCategories, 0);
        string secondCategory = FindCategory(observedCategories, 1);
        SetText(Method2, firstCategory ?? "ВАШИ АРГУМЕНТЫ");
        SetText(Method3, secondCategory ?? (turns > 0 ? $"ПРИМЕНЕНО {ratio}" : "ПЕРВАЯ ТРЕНИРОВКА"));
        SetText(SummaryStepCounter, $"{turns:00} РЕПЛИК" );
        SetText(StrengthLabel, "СИЛЬНЫЙ ХОД");
        SetText(StrengthText, turns == 0 ? "В этой тренировке пока нет засчитанных ответов." :
            $"Вы применили метод в {applied} из {turns} реплик. Продолжайте опираться на интересы сторон и ясные аргументы.");
        SetText(GrowthLabel, "ТОЧКА РОСТА");
        SetText(GrowthText, growthAdvice);
        SetText(SummaryAdviceTitle, "ОБРАТНАЯ СВЯЗЬ ТРЕНЕРА");
        SetText(SummaryText, string.IsNullOrWhiteSpace(feedback) ? "Тренировка завершена. Попробуйте применить совет в следующем диалоге." : feedback.Trim());
        SetText(SummaryDisclaimer, $"ИТОГ ПО {turns} УЧЕБНЫМ РЕПЛИКАМ · {safeMethod.ToUpperInvariant()}");
        if (SummaryAvatar != null && avatar != null) SummaryAvatar.sprite = avatar;
        ShowScreen(SummaryScreen);
    }

    static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    static string FindCategory(string[] categories, int index)
    {
        if (categories == null) return null;
        int found = 0;
        foreach (string category in categories)
        {
            if (string.IsNullOrWhiteSpace(category)) continue;
            if (found++ == index) return category.Trim().ToUpperInvariant();
        }
        return null;
    }

    public void SetContinueLabel(string value)
    {
        var label = ContinueButton != null ? ContinueButton.GetComponentInChildren<TMP_Text>() : null;
        if (label != null) label.text = value;
    }

    void ShowScreen(GameObject screen)
    {
        if (screen == null) return;
        if (_transition != null) StopCoroutine(_transition);
        _transition = StartCoroutine(Transition(screen));
    }

    public void SetStageButtonActions(System.Action submit, System.Action retake, System.Action next,
        System.Action restart, System.Action back)
    {
        Bind(SubmitButton, submit); Bind(RetakeButton, retake); Bind(ContinueButton, next);
        Bind(SummaryRestartButton, restart);
        Bind(MenuButton, back);
    }

    static void Bind(Button button, System.Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
    }

    IEnumerator Transition(GameObject next)
    {
        if (_currentScreen == next) { next.SetActive(true); _transition = null; yield break; }
        var old = _currentScreen;
        var oldGroup = old != null ? old.GetComponent<CanvasGroup>() : null;
        var nextGroup = next.GetComponent<CanvasGroup>();
        if (nextGroup == null) nextGroup = next.AddComponent<CanvasGroup>();
        next.SetActive(true);
        nextGroup.alpha = 0;
        next.transform.localScale = Vector3.one * .985f;
        _busy = true;
        float elapsed = 0;
        while (elapsed < .18f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .18f);
            nextGroup.alpha = t;
            next.transform.localScale = Vector3.Lerp(Vector3.one * .985f, Vector3.one, t);
            if (oldGroup != null) oldGroup.alpha = 1 - t;
            yield return null;
        }
        if (old != null && old != next) old.SetActive(false);
        if (oldGroup != null) oldGroup.alpha = 1;
        nextGroup.alpha = 1;
        next.transform.localScale = Vector3.one;
        _currentScreen = next;
        _busy = false;
        _transition = null;
        ApplyControls();
    }

    public void ShowHealth(GameSession session)
    {
        if (PlayerHealthText != null) PlayerHealthText.gameObject.SetActive(false);
        if (OpponentHealthText != null) OpponentHealthText.gameObject.SetActive(false);
    }

    public void Controls(bool busy, bool ready, bool recording, bool canRecord, bool canRetry, bool finished)
    {
        _controlsBusy = busy;
        _controlsReady = ready;
        _controlsRecording = recording;
        _controlsCanRecord = canRecord;
        _controlsCanRetry = canRetry;
        _controlsFinished = finished;
        ApplyControls();
    }

    void ApplyControls()
    {
        bool busy = _controlsBusy;
        bool ready = _controlsReady;
        bool recording = _controlsRecording;
        bool canRecord = _controlsCanRecord;
        bool canRetry = _controlsCanRetry;
        bool finished = _controlsFinished;
        if (RecordButton != null)
        {
            RecordButton.interactable = !busy && ready && canRecord && !_busy;
            var label = RecordButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = recording ? "Закончить ответ" : "Ответить голосом";
        }
        if (RetryButton != null)
        {
            RetryButton.gameObject.SetActive(canRetry);
            RetryButton.interactable = !busy;
        }
        if (StartButton != null)
        {
            StartButton.interactable = !busy && !recording;
            var label = StartButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = finished ? "Ещё тренировка" : "Новая тренировка";
        }
        if (SubmitButton != null) SubmitButton.interactable = !busy && !_busy;
        if (RetakeButton != null) RetakeButton.interactable = !busy && !_busy;
        if (ContinueButton != null) ContinueButton.interactable = !_busy;
    }

    public void ShowReply(LearningDialogCoach.Reply reply, bool opening)
    {
        if (!reply.needsClarification)
        {
            OpponentText.text = "Оппонент\n" + reply.opponentReply;
            HintText.text = "Подсказка для следующей реплики\n" + reply.hint;
            if (DialogueHintText != null) DialogueHintText.text = reply.hint;
        }
        if (!opening)
        {
            TranscriptText.text = "«" + reply.interpretation + "»";
            FeedbackText.text = reply.needsClarification ? "Уточни мысль\n" + reply.feedback :
                reply.interpretation + "\n\n" + reply.feedback + "\n\nПример усиленной реплики\n«" + reply.improvedExample + "»";
            if (TechniqueText != null) TechniqueText.text = string.IsNullOrWhiteSpace(reply.playerCategory) ? "РАЗБОР ОТВЕТА" : reply.playerCategory.ToUpperInvariant() + (reply.methodApplied ? " · ПРИМЕНЕНА" : " · МОЖНО УСИЛИТЬ");
            if (ResultTitleText != null) ResultTitleText.text = "РАЗБОР ВАШЕГО ОТВЕТА";
        }
    }

    void OnDestroy()
    {
        if (HintButton != null) HintButton.onClick.RemoveListener(ToggleHint);
        if (_transition != null) StopCoroutine(_transition);
    }
}
