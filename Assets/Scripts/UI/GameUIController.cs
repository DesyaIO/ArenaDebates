using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameUIController : MonoBehaviour
{
    [Header("Основной UI")]
    public GameObject MainPanel;
    public TMP_Text TopicText;
    public TMP_Text PlayerPositionText;
    public TMP_Text OpponentPositionText;
    public TMP_Text PlayerHealthText;
    public TMP_Text OpponentHealthText;
    public TMP_Text StatusText;
    public TMP_Text ResultText;
    public TMP_Text TurnText;

    [Header("Стартовая панель")]
    public GameObject StartPanel;
    public Button StartButton;
    public TMP_Text StartErrorText;

    [Header("История")]
    public HistoryUIController HistoryUI;
    public Button ShowHistoryButton;

    [Header("Экран окончания игры")]
    public GameObject EndGamePanel;
    public TMP_Text WinnerText;
    public TMP_Text EndTopicText;
    public TMP_Text EndWinnerPositionText;
    public TMP_Text EndPlayerTurnsText;
    public TMP_Text EndOpponentTurnsText;
    public Button RestartButton;

    [Header("Кнопки")]
    public Button RecordButton;
    public Button BackButton;
    public Button FinishDialogueButton;

    [Header("Ссылки")]
    public VoskSpeechController SpeechController;
    public bool IsPresentingStart { get; private set; }
    private Coroutine _intro;
    private GameObject _banner;
    private string _transcript = "", _feedback = "", _opponent = "";
    private bool _transcriptPending;
    private ScrollRect _conversationScroll;
    private RectTransform _conversationContent;
    private string _previousConversation;
    private readonly System.Collections.Generic.List<ScrollableText> _scrollableTexts = new System.Collections.Generic.List<ScrollableText>();

    private sealed class ScrollableText
    {
        public TMP_Text Text;
        public RectTransform Viewport, Content;
        public ScrollRect Scroll;
        public string PreviousText;
    }

    void Awake()
    {
        if (BackButton != null) BackButton.onClick.AddListener(OnBackClicked);
        if (FinishDialogueButton != null) FinishDialogueButton.onClick.AddListener(OnBackClicked);
        if (ShowHistoryButton != null)
            ShowHistoryButton.onClick.AddListener(ShowHistory);
        if (HistoryUI != null && HistoryUI.CloseButton != null)
            HistoryUI.CloseButton.onClick.AddListener(() => {
                if (EndGamePanel != null && SessionManager.Instance?.CurrentSession?.IsGameOver == true)
                    EndGamePanel.SetActive(true);
            });

        if (StartButton != null)
            StartButton.onClick.AddListener(ConfigureDialogue);

        if (RestartButton != null)
            RestartButton.onClick.AddListener(OnRestartClicked);

        // Скрываем основные панели до старта
        if (MainPanel != null) MainPanel.SetActive(false);
        if (EndGamePanel != null) EndGamePanel.SetActive(false);
        if (StartPanel != null) StartPanel.SetActive(true);
        UpdateHealth(0, 0);
        PrepareConversationScroll();
        PrepareScrollableText(TopicText);
        PrepareScrollableText(PlayerPositionText);
        PrepareScrollableText(OpponentPositionText);
        PrepareScrollableText(EndTopicText);
        PrepareScrollableText(EndWinnerPositionText);
    }

    void LateUpdate()
    {
        foreach (var area in _scrollableTexts)
        {
            if (area.Text == null || area.Viewport == null || area.Content == null) continue;
            area.Text.ForceMeshUpdate();
            float contextWidth = Mathf.Max(1f, area.Viewport.rect.width - 24f);
            float contextHeight = area.Text.GetPreferredValues(contextWidth, 100000f).y + 20f;
            if (float.IsNaN(contextHeight) || float.IsInfinity(contextHeight)) contextHeight = area.Viewport.rect.height;
            contextHeight = Mathf.Max(area.Viewport.rect.height, contextHeight);
            area.Content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contextHeight);
            area.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contextHeight);
            if (area.PreviousText != area.Text.text)
            {
                area.PreviousText = area.Text.text;
                if (area.Scroll != null) area.Scroll.verticalNormalizedPosition = 1f;
            }
        }
        if (ResultText == null || _conversationContent == null) return;
        ResultText.ForceMeshUpdate();
        float width = Mathf.Max(1f, _conversationContent.rect.width - 36f);
        float height = ResultText.GetPreferredValues(width, 100000f).y + 32f;
        if (float.IsNaN(height) || float.IsInfinity(height)) height = 100f;
        height = Mathf.Max(height, _conversationContent.parent is RectTransform viewport ? viewport.rect.height : 100f);
        _conversationContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        ResultText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        if (_previousConversation != ResultText.text)
        {
            _previousConversation = ResultText.text;
            if (_conversationScroll != null) _conversationScroll.verticalNormalizedPosition = 0f;
        }
    }

    void PrepareConversationScroll()
    {
        if (ResultText == null || ResultText.transform.parent == null) return;
        var viewport = ResultText.transform.parent as RectTransform;
        if (viewport == null) return;
        _conversationScroll = viewport.GetComponent<ScrollRect>();
        if (_conversationScroll == null) _conversationScroll = viewport.gameObject.AddComponent<ScrollRect>();
        if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();
        var original = ResultText.rectTransform;
        int sibling = original.GetSiblingIndex();
        var contentObject = new GameObject("ConversationContent", typeof(RectTransform));
        _conversationContent = contentObject.GetComponent<RectTransform>();
        _conversationContent.SetParent(viewport, false);
        _conversationContent.anchorMin = new Vector2(0f, 1f);
        _conversationContent.anchorMax = new Vector2(1f, 1f);
        _conversationContent.pivot = new Vector2(.5f, 1f);
        _conversationContent.anchoredPosition = Vector2.zero;
        _conversationContent.sizeDelta = Vector2.zero;
        _conversationContent.SetAsFirstSibling();
        original.SetParent(_conversationContent, false);
        original.anchorMin = new Vector2(0f, 1f);
        original.anchorMax = new Vector2(1f, 1f);
        original.pivot = new Vector2(.5f, 1f);
        original.anchoredPosition = Vector2.zero;
        original.sizeDelta = Vector2.zero;
        ResultText.enableWordWrapping = true;
        ResultText.overflowMode = TextOverflowModes.Overflow;
        _conversationScroll.content = _conversationContent;
        _conversationScroll.viewport = viewport;
        _conversationScroll.horizontal = false;
        _conversationScroll.vertical = true;
        _conversationScroll.inertia = false;
        _conversationScroll.movementType = ScrollRect.MovementType.Clamped;
        _conversationScroll.scrollSensitivity = 30f;
        VisibleScrollbar.Ensure(_conversationScroll);
    }

    void PrepareScrollableText(TMP_Text text)
    {
        if (text == null || text.rectTransform == null || text.transform.parent == null) return;
        var original = text.rectTransform;
        var parent = original.parent;
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
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        original.SetParent(content, false);
        original.anchorMin = new Vector2(0f, 1f);
        original.anchorMax = new Vector2(1f, 1f);
        original.pivot = new Vector2(.5f, 1f);
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
        scroll.scrollSensitivity = 24f;
        VisibleScrollbar.Ensure(scroll);
        _scrollableTexts.Add(new ScrollableText
        {
            Text = text, Viewport = viewport, Content = content, Scroll = scroll, PreviousText = text.text
        });
    }

    void Start()
    {
        if (!DialogueOptions.Pending) return;
        DialogueOptions.Pending = false;
        OnStartClicked();
    }

    void ConfigureDialogue()
    {
        if (StartErrorText != null)
        {
            StartErrorText.text = "";
            StartErrorText.color = new Color32(0x1D, 0x36, 0x44, 0xFF);
        }
        DialogueOptions.Difficulty = SpeechController.OpponentDifficulty;
        DialogueOptions.FirstSpeaker = GameManager.Instance.FirstSpeaker;
        DialogueOptions.Turns = GameManager.Instance.TurnsPerParticipant == 10 ? 10 : 5;
        if (SessionManager.Instance?.CurrentSession != null)
            DialogueOptions.AnalyzeResponses = SessionManager.Instance.CurrentSession.AnalyzeResponses;
        if (SessionManager.Instance?.CurrentSession != null && SessionManager.Instance.CurrentSession.IsCustomSituation)
        {
            var session = SessionManager.Instance.CurrentSession;
            DialogueOptions.UseMySituation = true;
            DialogueOptions.MySituationTopic = session.TopicDescription;
            DialogueOptions.MyPlayerPosition = session.PlayerPosition;
            DialogueOptions.MyOpponentPosition = session.OpponentPosition;
        }
        DialogueOptions.Show(TopicText.font, () => {
            DialogueOptions.Pending = false;
            SpeechController.OpponentDifficulty = DialogueOptions.Difficulty;
            GameManager.Instance.FirstSpeaker = DialogueOptions.FirstSpeaker;
            GameManager.Instance.TurnsPerParticipant = DialogueOptions.Turns;
            OnStartClicked();
        });
    }

    public void ShowStartError(string message)
    {
        SpeechController?.CancelPendingTurn();
        if (MainPanel != null) MainPanel.SetActive(false);
        if (StartPanel != null) StartPanel.SetActive(true);
        if (StartErrorText != null)
        {
            StartErrorText.text = message;
            StartErrorText.color = new Color32(0xB8, 0x3C, 0x46, 0xFF);
        }
        if (StatusText != null) StatusText.text = message;
    }

    // ---------- Старт игры ----------

    private void OnBackClicked()
    {
        SpeechController?.CancelPendingTurn();

        SceneParams.PendingMenuStoryKey = null;
        SceneParams.PendingMenuStoryTitle = null;
        SceneParams.PendingMenuStorySubtitle = null;
        SceneParams.PendingMenuStoryText = null;

        var session = SessionManager.Instance != null ? SessionManager.Instance.CurrentSession : null;
        if (session != null && session.IsGameOver)
        {
            GetDialogueScores(session, out int playerScore, out int opponentScore, out bool hasScores);
            int result = hasScores ? playerScore.CompareTo(opponentScore) : 0;
            if (result > 0 && !PlayerProgress.HasSeenNarrative(SceneParams.FreeDialogueVictoryKey))
            {
                SceneParams.PendingMenuStoryKey = SceneParams.FreeDialogueVictoryKey;
                SceneParams.PendingMenuStoryTitle = SceneParams.FreeDialogueVictoryTitle;
                SceneParams.PendingMenuStorySubtitle = SceneParams.FreeDialogueVictorySubtitle;
                SceneParams.PendingMenuStoryText = SceneParams.FreeDialogueVictoryText;
            }
            else if (result < 0)
            {
                SceneParams.PendingMenuStoryTitle = SceneParams.FreeDialogueLossTitle;
                SceneParams.PendingMenuStorySubtitle = SceneParams.FreeDialogueLossSubtitle;
                SceneParams.PendingMenuStoryText = SceneParams.FreeDialogueLossText;
            }
        }

        SceneManager.LoadScene("MenuScene");
    }

    private void OnStartClicked()
    {
        if (StartPanel != null && !StartPanel.activeSelf) return;
        if (DialogueOptions.UseMySituation && (string.IsNullOrWhiteSpace(DialogueOptions.MySituationTopic) ||
            string.IsNullOrWhiteSpace(DialogueOptions.MyPlayerPosition) || string.IsNullOrWhiteSpace(DialogueOptions.MyOpponentPosition)))
        {
            ShowStartError("Заполните ситуацию и обе позиции в настройках перед началом.");
            return;
        }
        if (StartErrorText != null)
        {
            StartErrorText.text = "";
            StartErrorText.color = new Color32(0x1D, 0x36, 0x44, 0xFF);
        }
        SpeechController?.CancelPendingTurn();
        _transcript = _feedback = _opponent = "";
        _transcriptPending = false;
        RenderConversation();
        if (StartPanel != null) StartPanel.SetActive(false);
        if (MainPanel != null) MainPanel.SetActive(true);

        // Инициализируем Vosk
        if (SpeechController != null)
            SpeechController.InitializeVosk();

        // Запускаем новую игру
        GameManager.Instance.StartNewGame();
    }

    // ---------- Игра началась ----------

    public void OnGameStarted()
    {
        var s = SessionManager.Instance.CurrentSession;
        TopicText.text = $"<size=72%><color=#B83C46>{(s.IsCustomSituation ? "МОЯ СИТУАЦИЯ" : "ТЕМА ДИАЛОГА")}</color></size>\n{EscapeMarkup(s.TopicDescription)}";
        PlayerPositionText.text = $"<size=72%><color=#60717C>ВАША ПОЗИЦИЯ</color></size>\n{EscapeMarkup(s.PlayerPosition)}";
        OpponentPositionText.text = $"<size=72%><color=#60717C>ПОЗИЦИЯ ОППОНЕНТА</color></size>\n{EscapeMarkup(s.OpponentPosition)}";
        UpdateHealth(s.PlayerHealth, s.OpponentHealth);

        StatusText.text = (s.AnalyzeResponses ? "Разбор включён · " : "Разбор выключен · ") +
            (s.IsPlayerTurn ? "Ваш ход" : "Ход оппонента");
        RenderConversation();
        if (_intro != null) StopCoroutine(_intro);
        _intro = StartCoroutine(AnnounceFirstTurn(s.IsPlayerTurn));
    }

    IEnumerator AnnounceFirstTurn(bool playerFirst)
    {
        IsPresentingStart = true;
        RecordButton.interactable = false;
        _banner = DialogueOptions.Overlay(TopicText.font,
            playerFirst ? "Первым ходите вы" : "Первым ходит оппонент", out var root);
        yield return new WaitForSecondsRealtime(2.2f);
        Destroy(_banner); _banner = null; _intro = null;
        IsPresentingStart = false;
        SpeechController?.ResumeSession();
    }

    public void OnEntryAdded(DialogueEntry entry)
    {
        if (entry == null) return;
        if (entry.IsPlayerTurn) _transcriptPending = false;
        var session = SessionManager.Instance?.CurrentSession;
        StatusText.text = entry.IsPlayerTurn ? "Ответ сохранён · ход оппонента" : "Ответ сохранён · ваш ход";
        if (session != null && session.AnalyzeResponses)
            StatusText.text = "Разбор готов · " + StatusText.text;
        RenderConversation();
    }

    public void UpdateHealth(int playerHp, int opponentHp)
    {
        var session = SessionManager.Instance?.CurrentSession;
        if (PlayerHealthText != null) PlayerHealthText.text = session == null ? "" : $"Ваши ходы: {session.PlayerTurns}/{session.TurnsPerParticipant}";
        if (OpponentHealthText != null) OpponentHealthText.text = session == null ? "" : $"Оппонент: {session.OpponentTurns}/{session.TurnsPerParticipant}";
        if (TurnText != null) TurnText.text = session == null ? ""
            : $"ХОД {Mathf.Min(session.PlayerTurns + 1, session.TurnsPerParticipant):00} / {session.TurnsPerParticipant:00}";
    }

    public void ShowTranscript(string text) { _transcript = text; _feedback = "Ответ обрабатывается…"; _transcriptPending = true; RenderConversation(); }
    public void ShowClarification(string text) { _feedback = "Уточнение\n" + text; _transcriptPending = true; RenderConversation(); }
    void RenderConversation()
    {
        if (ResultText == null) return;
        var session = SessionManager.Instance?.CurrentSession;
        bool showAnalysis = session == null || session.AnalyzeResponses;
        var conversation = new System.Text.StringBuilder();
        if (session != null && session.Entries != null)
        {
            foreach (var entry in session.Entries)
            {
                conversation.Append(entry.IsPlayerTurn ? "<b><color=#1D3644>ВЫ</color></b>\n" : "<b><color=#B83C46>ОППОНЕНТ</color></b>\n");
                conversation.Append(EscapeMarkup(entry.UserText));
                if (showAnalysis)
                {
                    conversation.Append("\n\n<size=85%><color=#60717C>РАЗБОР · ")
                        .Append(EscapeMarkup(entry.Category)).Append("</color></size>");
                    if (!string.IsNullOrWhiteSpace(entry.Explanation))
                        conversation.Append("\n<size=85%>").Append(EscapeMarkup(entry.Explanation)).Append("</size>");
                }
                conversation.Append("\n\n");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(_transcript)) conversation.Append("<b>ВЫ</b>\n").Append(EscapeMarkup(_transcript)).Append("\n\n");
            if (!string.IsNullOrWhiteSpace(_opponent)) conversation.Append("<b>ОППОНЕНТ</b>\n").Append(EscapeMarkup(_opponent)).Append("\n\n");
        }
        if (_transcriptPending && !string.IsNullOrWhiteSpace(_transcript))
        {
            conversation.Append("<b>ВАШ ОТВЕТ · ПРОВЕРКА</b>\n").Append(EscapeMarkup(_transcript));
            if (!string.IsNullOrWhiteSpace(_feedback)) conversation.Append("\n<size=85%><color=#60717C>").Append(EscapeMarkup(_feedback)).Append("</color></size>");
        }
        if (conversation.Length == 0) conversation.Append(showAnalysis
            ? "После первого хода здесь появятся реплики и разборы обеих сторон."
            : "После первого хода здесь появятся реплики участников.");
        ResultText.text = conversation.ToString().TrimEnd();
    }

    static string EscapeMarkup(string value) => string.IsNullOrEmpty(value)
        ? "" : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---------- Конец игры ----------

    public void OnGameOver()
    {
        var s = SessionManager.Instance.CurrentSession;
        if (s == null) return;
        GetDialogueScores(s, out int playerScore, out int opponentScore, out bool hasScores);
        int result = hasScores ? playerScore.CompareTo(opponentScore) : 0;
        StatusText.text = "Диалог завершён";

        // Показываем экран окончания
        if (EndGamePanel != null) EndGamePanel.SetActive(true);

        WinnerText.text = result > 0 ? "ПОБЕДА" : result < 0 ? "ПОРАЖЕНИЕ" : "НИЧЬЯ";
        if (EndPlayerTurnsText != null)
            EndPlayerTurnsText.text = $"{s.PlayerTurns} / {s.TurnsPerParticipant}\nВАШИХ ХОДОВ";
        if (EndOpponentTurnsText != null)
            EndOpponentTurnsText.text = $"{s.OpponentTurns} / {s.TurnsPerParticipant}\nХОДОВ ОППОНЕНТА";
        EndTopicText.text = $"<size=75%><color=#B83C46>{(s.IsCustomSituation ? "ВАША СИТУАЦИЯ" : "ТЕМА ДИАЛОГА")}</color></size>\n{EscapeMarkup(s.TopicDescription)}";

        string scoreLine = hasScores
            ? $"Баллы за типы ответов: {playerScore} : {opponentScore}"
            : s.AnalyzeResponses
                ? "Не удалось определить итог по типам ответов."
                : "Разбор ответов выключен; итог не определён.";
        EndWinnerPositionText.text = $"{scoreLine}\nВы: {s.PlayerTurns} ходов · Оппонент: {s.OpponentTurns} ходов\nСложность: {EscapeMarkup(s.OpponentDifficulty)}\nРазбор ответов: {(s.AnalyzeResponses ? "включён" : "выключен")}\nОткройте историю, чтобы посмотреть ход диалога.";

        // История открывается отдельной кнопкой на экране результата.
        HistoryUI.Hide();
    }

    private static void GetDialogueScores(GameSession session, out int playerScore, out int opponentScore, out bool hasScores)
    {
        playerScore = 0;
        opponentScore = 0;
        hasScores = false;
        var database = GameManager.Instance != null ? GameManager.Instance.AnswerTypes : null;
        if (session == null || session.Entries == null || database == null) return;

        foreach (var entry in session.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Category) || entry.Category == "Разбор отключён")
                continue;

            var answerType = database.GetByName(entry.Category);
            if (answerType == null) continue;

            // У AnswerTypeSO отрицательное значение означает сильный ответ;
            // переворачиваем знак, чтобы больший итог соответствовал убедительнее.
            int points = -answerType.DamageToOpponent;
            if (entry.IsPlayerTurn) playerScore += points;
            else opponentScore += points;
            hasScores = true;
        }
    }

    // ---------- Откат ----------

    public void OnRollback(int index)
    {
        HistoryUI.Hide();
        if (EndGamePanel != null) EndGamePanel.SetActive(false);

        var s = SessionManager.Instance.CurrentSession;
        _transcript = _feedback = _opponent = "";
        _transcriptPending = false;
        foreach (var entry in s.Entries) OnEntryAdded(entry);
        RenderConversation();
        UpdateHealth(s.PlayerHealth, s.OpponentHealth);
        StatusText.text = $"Откат к ходу #{index}. Продолжайте.";
        Debug.Log($"Игрок откатился к ходу #{index}");
        SpeechController?.ResumeSession();
    }

    private void OnRollbackRequested(int index)
    {
        GameManager.Instance.RollbackTo(index);
    }

    private void ShowHistory()
    {
        if (EndGamePanel != null) EndGamePanel.SetActive(false);
        HistoryUI.Show(OnRollbackRequested);
    }

    // ---------- Рестарт ----------

    private void OnRestartClicked()
    {
        SpeechController?.CancelPendingTurn();
        SessionManager.Instance.DeleteSession();

        if (EndGamePanel != null) EndGamePanel.SetActive(false);
        if (MainPanel != null) MainPanel.SetActive(false);
        if (StartPanel != null) StartPanel.SetActive(true);

        HistoryUI.Hide();
    }

    void OnDisable()
    {
        if (_intro != null) StopCoroutine(_intro);
        if (_banner != null) Destroy(_banner);
        _intro = null; IsPresentingStart = false;
    }
}
