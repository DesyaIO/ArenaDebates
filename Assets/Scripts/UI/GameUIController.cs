using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("Стартовая панель")]
    public GameObject StartPanel;
    public Button StartButton;

    [Header("История")]
    public HistoryUIController HistoryUI;
    public Button ShowHistoryButton;

    [Header("Экран окончания игры")]
    public GameObject EndGamePanel;
    public TMP_Text WinnerText;
    public TMP_Text EndTopicText;
    public TMP_Text EndWinnerPositionText;
    public Button RestartButton;

    [Header("Кнопки")]
    public Button RecordButton;
    public Button BackButton;

    [Header("Ссылки")]
    public VoskSpeechController SpeechController;

    void Awake()
    {
        if (BackButton != null) BackButton.onClick.AddListener(OnBackClicked);
        if (ShowHistoryButton != null)
            ShowHistoryButton.onClick.AddListener(() => HistoryUI.Show(OnRollbackRequested));

        if (StartButton != null)
            StartButton.onClick.AddListener(OnStartClicked);

        if (RestartButton != null)
            RestartButton.onClick.AddListener(OnRestartClicked);

        // Скрываем основные панели до старта
        if (MainPanel != null) MainPanel.SetActive(false);
        if (EndGamePanel != null) EndGamePanel.SetActive(false);
        if (StartPanel != null) StartPanel.SetActive(true);
    }

    // ---------- Старт игры ----------

    private void OnBackClicked()
    {
        SpeechController?.CancelPendingTurn();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnStartClicked()
    {
        if (StartPanel != null && !StartPanel.activeSelf) return;
        SpeechController?.CancelPendingTurn();
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
        TopicText.text = s.TopicDescription;
        PlayerPositionText.text = $"Ваша позиция: {s.PlayerPosition}";
        OpponentPositionText.text = $"Позиция оппонента: {s.OpponentPosition}";
        UpdateHealth(s.PlayerHealth, s.OpponentHealth);

        StatusText.text = s.IsPlayerTurn ? "Ваш ход" : "Ход оппонента";
        SpeechController?.ResumeSession();
    }

    public void OnEntryAdded(DialogueEntry entry)
    {
        if (entry == null) return;
        StatusText.text = entry.IsPlayerTurn ? "Ход оппонента" : "Ваш ход";
    }

    public void UpdateHealth(int playerHp, int opponentHp)
    {
        PlayerHealthText.text = $"HP: {playerHp}";
        OpponentHealthText.text = $"HP: {opponentHp}";
    }

    // ---------- Конец игры ----------

    public void OnGameOver()
    {
        var s = SessionManager.Instance.CurrentSession;
        bool playerWon = s.PlayerHealth > 0 && s.OpponentHealth <= 0;

        StatusText.text = playerWon ? "Победа!" : "Поражение!";

        // Показываем экран окончания
        if (EndGamePanel != null) EndGamePanel.SetActive(true);

        WinnerText.text = playerWon ? "Вы победили!" : "Вы проиграли!";
        EndTopicText.text = $"Тема: {s.TopicDescription}";

        string winnerPosition = playerWon ? s.PlayerPosition : s.OpponentPosition;
        string winnerSide = playerWon ? "игрока" : "оппонента";
        EndWinnerPositionText.text = $"Стратегия {winnerSide}: {winnerPosition}";

        // История открывается отдельной кнопкой на экране результата.
        HistoryUI.Hide();
    }

    // ---------- Откат ----------

    public void OnRollback(int index)
    {
        HistoryUI.Hide();
        if (EndGamePanel != null) EndGamePanel.SetActive(false);

        var s = SessionManager.Instance.CurrentSession;
        UpdateHealth(s.PlayerHealth, s.OpponentHealth);
        StatusText.text = $"Откат к ходу #{index}. Продолжайте.";
        Debug.Log($"Игрок откатился к ходу #{index}");
        SpeechController?.ResumeSession();
    }

    private void OnRollbackRequested(int index)
    {
        GameManager.Instance.RollbackTo(index);
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
}
