using System;
using System.IO;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;

    [Header("Настройки")]
    public bool AutoSave = true;
    public string FileName = "game_session.json";

    public GameSession CurrentSession { get; private set; }

    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartNewSession(DebateTopicSO topic, PositionSO playerPos,
                                PositionSO opponentPos, bool playerFirst)
    {
        CurrentSession = new GameSession(topic, playerPos, opponentPos, playerFirst);
        Debug.Log($"Новая сессия: {CurrentSession.SessionId}");
        if (AutoSave) Save();
    }

    public void Save()
    {
        if (CurrentSession == null) return;
        try
        {
            string json = JsonUtility.ToJson(CurrentSession, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"Сессия сохранена: {FilePath}");
        }
        catch (Exception e) { Debug.LogError($"Save error: {e.Message}"); }
    }

    public void LoadOrNull()
    {
        if (!File.Exists(FilePath)) { CurrentSession = null; return; }
        try
        {
            string json = File.ReadAllText(FilePath);
            CurrentSession = JsonUtility.FromJson<GameSession>(json);
            Debug.Log($"Сессия загружена: {CurrentSession.SessionId}, записей: {CurrentSession.Entries.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e.Message}");
            CurrentSession = null;
        }
    }

    public void RollbackTo(int index)
    {
        if (CurrentSession == null) return;
        if (CurrentSession.RollbackTo(index))
        {
            if (AutoSave) Save();
            Debug.Log($"Откат до #{index}. HP игрока: {CurrentSession.PlayerHealth}, HP оппонента: {CurrentSession.OpponentHealth}");
        }
    }

    public void DeleteSession()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
        CurrentSession = null;
    }
}