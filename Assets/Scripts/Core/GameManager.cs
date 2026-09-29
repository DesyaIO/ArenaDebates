using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Контент")]
    public List<DebateTopicSO> AllTopics = new List<DebateTopicSO>();
    public AnswerTypeDatabaseSO AnswerTypes;

    [Header("Ссылки")]
    public GameUIController UIController;
    [Header("Настройки диалога")]
    public DialogueFirstSpeaker FirstSpeaker = DialogueFirstSpeaker.Random;
    public int TurnsPerParticipant = 5;

    public DebateTopicSO CurrentTopic { get; private set; }
    public PositionSO PlayerPosition { get; private set; }
    public PositionSO OpponentPosition { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (DialogueOptions.Pending)
        {
            FirstSpeaker = DialogueOptions.FirstSpeaker;
            TurnsPerParticipant = DialogueOptions.Turns;
            if (UIController != null && UIController.SpeechController != null)
                UIController.SpeechController.OpponentDifficulty = DialogueOptions.Difficulty;
        }
    }

    /// <summary>
    /// Запускает новую игру: случайная тема, случайная позиция, случайный первый ход.
    /// </summary>
    public void StartNewGame()
    {
        // Who opens is chosen once from the options screen and reused for either source of context.
        bool playerFirst = FirstSpeaker == DialogueFirstSpeaker.Player ||
            (FirstSpeaker == DialogueFirstSpeaker.Random && Random.value > 0.5f);

        if (DialogueOptions.UseMySituation)
        {
            if (string.IsNullOrWhiteSpace(DialogueOptions.MySituationTopic) ||
                string.IsNullOrWhiteSpace(DialogueOptions.MyPlayerPosition) ||
                string.IsNullOrWhiteSpace(DialogueOptions.MyOpponentPosition))
            {
                UIController?.ShowStartError("Заполните ситуацию, свою позицию и позицию оппонента в настройках диалога.");
                return;
            }
            CurrentTopic = null;
            PlayerPosition = OpponentPosition = null;
            SessionManager.Instance.StartNewSession(DialogueOptions.MySituationTopic,
                DialogueOptions.MyPlayerPosition, DialogueOptions.MyOpponentPosition, playerFirst);
        }
        else
        {
            var validTopics = AllTopics.FindAll(t => t != null && t.Positions != null &&
                t.Positions.Count >= 2 && t.Positions[0] != null && t.Positions[1] != null);
            if (validTopics.Count == 0)
            {
                UIController?.ShowStartError("В проекте нет доступных тем с двумя позициями.");
                return;
            }
            CurrentTopic = validTopics[Random.Range(0, validTopics.Count)];
            int posIdx = Random.Range(0, 2);
            PlayerPosition = CurrentTopic.Positions[posIdx];
            OpponentPosition = CurrentTopic.Positions[1 - posIdx];
            SessionManager.Instance.StartNewSession(CurrentTopic, PlayerPosition, OpponentPosition, playerFirst);
        }

        var session = SessionManager.Instance.CurrentSession;
        session.TurnsPerParticipant = TurnsPerParticipant == 10 ? 10 : 5;
        session.OpponentDifficulty = UIController != null && UIController.SpeechController != null
            ? UIController.SpeechController.OpponentDifficulty : DialogueOptions.Difficulty;
        session.AnalyzeResponses = DialogueOptions.AnalyzeResponses;
        SessionManager.Instance.Save();

        Debug.Log($"Тема: {session.Category} — {session.TopicDescription}");
        Debug.Log($"Позиция игрока: {session.PlayerPosition}");
        Debug.Log($"Первый ход: {(playerFirst ? "Игрок" : "Оппонент")}");

        UIController?.OnGameStarted();
    }

    /// <summary>
    /// Записывает принятый ход игрока и обновляет прогресс диалога.
    /// </summary>
    public void ApplyPlayerAnswer(string text, string category, string explanation)
    {
        if (SessionManager.Instance?.CurrentSession == null) return;
        var entry = SessionManager.Instance.CurrentSession.AddEntry(true, text, category, explanation, 0);

        if (entry == null) return;
        SessionManager.Instance.Save();
        UIController?.OnEntryAdded(entry);
        UIController?.UpdateHealth(SessionManager.Instance.CurrentSession.PlayerHealth,
                                SessionManager.Instance.CurrentSession.OpponentHealth);

        if (SessionManager.Instance.CurrentSession.IsGameOver)
            UIController?.OnGameOver();
    }

    public void ApplyOpponentAnswer(string text, string category, string explanation)
    {
        if (SessionManager.Instance?.CurrentSession == null) return;
        var entry = SessionManager.Instance.CurrentSession.AddEntry(false, text, category, explanation, 0);

        if (entry == null) return;
        SessionManager.Instance.Save();
        UIController?.OnEntryAdded(entry);
        UIController?.UpdateHealth(SessionManager.Instance.CurrentSession.PlayerHealth,
                                SessionManager.Instance.CurrentSession.OpponentHealth);

        if (SessionManager.Instance.CurrentSession.IsGameOver)
            UIController?.OnGameOver();
    }

    /// <summary>
    /// Откатывает игру до указанного хода.
    /// </summary>
    public void RollbackTo(int index)
    {
        SessionManager.Instance.RollbackTo(index);
        UIController?.OnRollback(index);
    }

}
