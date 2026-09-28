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

    public DebateTopicSO CurrentTopic { get; private set; }
    public PositionSO PlayerPosition { get; private set; }
    public PositionSO OpponentPosition { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Запускает новую игру: случайная тема, случайная позиция, случайный первый ход.
    /// </summary>
    public void StartNewGame()
    {
        // 1. Случайная тема
        CurrentTopic = AllTopics[Random.Range(0, AllTopics.Count)];

        // 2. Игрок выбирает позицию (для примера — случайно, но можно дать UI-выбор)
        int posIdx = Random.Range(0, CurrentTopic.Positions.Count);
        PlayerPosition = CurrentTopic.Positions[posIdx];
        OpponentPosition = CurrentTopic.Positions[1 - posIdx];

        // 3. Кто ходит первым — монетка
        bool playerFirst = Random.value > 0.5f;

        // 4. Создаём сессию
        SessionManager.Instance.StartNewSession(CurrentTopic, PlayerPosition, OpponentPosition, playerFirst);

        Debug.Log($"Тема: {CurrentTopic.Category} — {CurrentTopic.Description}");
        Debug.Log($"Позиция игрока: {PlayerPosition.ShortName}");
        Debug.Log($"Первый ход: {(playerFirst ? "Игрок" : "Оппонент")}");

        UIController?.OnGameStarted();
    }

    /// <summary>
    /// Записывает ход игрока и обновляет HP.
    /// </summary>
    public void ApplyPlayerAnswer(string text, string category, string explanation)
    {
        if (SessionManager.Instance?.CurrentSession == null) return;
        int damage = AnswerTypes.GetDamage(category);
        var entry = SessionManager.Instance.CurrentSession.AddEntry(true, text, category, explanation, damage);

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
        int damage = AnswerTypes.GetDamage(category);

        // Знак одинаков для обеих сторон: минус наносит урон, плюс лечит адресата.
        var entry = SessionManager.Instance.CurrentSession.AddEntry(false, text, category, explanation, damage);

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