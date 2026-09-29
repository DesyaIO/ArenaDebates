using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSession
{
    public string SessionId;
    public string Category;          // Маркетинг / HR / IT
    public string TopicDescription;  // Формулировка темы
    public string PlayerPosition;    // Какую позицию выбрал игрок
    public string OpponentPosition;  // Противоположная позиция
    public bool IsCustomSituation;
    public bool AnalyzeResponses = true;

    public int PlayerHealth = 100;
    public int OpponentHealth = 100;

    public bool PlayerGoesFirst;
    public bool IsGameOver;
    public int TurnsPerParticipant = 5;
    public string OpponentDifficulty = "Средний";
    public int PlayerTurns => Entries.FindAll(e => e.IsPlayerTurn).Count;
    public int OpponentTurns => Entries.Count - PlayerTurns;

    public List<DialogueEntry> Entries = new List<DialogueEntry>();
    public string CreatedAt;

    public GameSession() { }

    public GameSession(DebateTopicSO topic, PositionSO playerPos, PositionSO opponentPos, bool playerFirst)
    {
        SessionId = Guid.NewGuid().ToString();
        Category = topic.Category;
        TopicDescription = topic.Description;
        PlayerPosition = playerPos.Description;
        OpponentPosition = opponentPos.Description;
        PlayerGoesFirst = playerFirst;
        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public GameSession(string topic, string playerPosition, string opponentPosition, bool playerFirst)
    {
        SessionId = Guid.NewGuid().ToString();
        Category = "Моя ситуация";
        TopicDescription = topic?.Trim() ?? "";
        PlayerPosition = playerPosition?.Trim() ?? "";
        OpponentPosition = opponentPosition?.Trim() ?? "";
        PlayerGoesFirst = playerFirst;
        IsCustomSituation = true;
        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public bool IsPlayerTurn => !IsGameOver &&
        (Entries.Count == 0 ? PlayerGoesFirst : !Entries[Entries.Count - 1].IsPlayerTurn);

    public DialogueEntry AddEntry(bool isPlayer, string text, string category,
                                  string explanation, int damage)
    {
        // Only accepted replies count. Retries and duplicate callbacks do not.
        if (IsGameOver || isPlayer != IsPlayerTurn) return null;
        int idx = Entries.Count + 1;

        var entry = new DialogueEntry(idx, isPlayer, text, category, explanation, 0,
                                      PlayerHealth, OpponentHealth);
        Entries.Add(entry);

        IsGameOver = TurnsPerParticipant > 0 && PlayerTurns >= TurnsPerParticipant && OpponentTurns >= TurnsPerParticipant;

        return entry;
    }

    /// <summary>
    /// Откат сессии до записи с указанным индексом (включительно).
    /// Удаляет все записи после index, восстанавливает HP из последней оставшейся.
    /// </summary>
    public bool RollbackTo(int index)
    {
        if (index < 0 || index > Entries.Count) return false;

        Entries.RemoveAll(e => e.Index > index);
        IsGameOver = false;

        if (Entries.Count > 0)
        {
            var last = Entries[Entries.Count - 1];
            PlayerHealth = last.PlayerHealthAfter;
            OpponentHealth = last.OpponentHealthAfter;
        }
        else
        {
            PlayerHealth = 100;
            OpponentHealth = 100;
        }

        IsGameOver = TurnsPerParticipant > 0 && PlayerTurns >= TurnsPerParticipant && OpponentTurns >= TurnsPerParticipant;
        return true;
    }

    public DialogueEntry GetEntry(int index) => Entries.Find(e => e.Index == index);
}
