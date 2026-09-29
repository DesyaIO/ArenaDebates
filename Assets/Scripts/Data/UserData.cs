using System;
using System.Collections.Generic;

[Serializable]
public class GoalProgress
{
    public string Industry;
    public string GoalText;
    public List<string> LearningMethods = new List<string>();
    public List<string> CompletedMethods = new List<string>();
    public List<string> SolvedTaskIds = new List<string>();   // ← новое
    public int ListeningSolved;
    public int Points;
    public string CreatedAt;
    public string LastPlayedAt;

    public GoalProgress() { }

    public GoalProgress(string industry, string goalText, List<string> methods)
    {
        Industry = industry;
        GoalText = goalText;
        LearningMethods = new List<string>(methods);
        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        LastPlayedAt = CreatedAt;
    }

    public string Key => $"{Industry}::{GoalText}";

    public bool IsTaskSolved(string taskId)
        => SolvedTaskIds.Contains(taskId);

    public void MarkTaskSolved(string taskId)
    {
        if (!SolvedTaskIds.Contains(taskId))
            SolvedTaskIds.Add(taskId);
    }
}

[Serializable]
public class UserData
{
    public string Login;
    public string Password;
    public string FullName;
    public string CreatedAt;

    // Список целей пользователя
    public List<GoalProgress> Goals = new List<GoalProgress>();

    // Индекс активной цели
    public int ActiveGoalIndex = -1;

    // Награды/баллы (общие)
    public int TotalPoints;
    public List<string> Rewards = new List<string>();

    // Истории, которые пользователь уже просмотрел. Это отдельный список,
    // чтобы сюжетный прогресс не смешивался с наградами.
    public List<string> SeenNarrativeKeys = new List<string>();

    public UserData() { }

    public UserData(string login, string password, string fullName)
    {
        Login = login;
        Password = password;
        FullName = fullName;
        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // ---------- Удобные методы ----------

    public GoalProgress GetActiveGoal()
    {
        if (ActiveGoalIndex < 0 || ActiveGoalIndex >= Goals.Count)
            return null;
        return Goals[ActiveGoalIndex];
    }

    public bool HasActiveGoal()
    {
        return GetActiveGoal() != null;
    }

    /// <summary>
    /// Ищет цель по отрасли + тексту. Если не найдена — возвращает -1.
    /// </summary>
    public int FindGoalIndex(string industry, string goalText)
    {
        for (int i = 0; i < Goals.Count; i++)
        {
            if (Goals[i].Industry == industry && Goals[i].GoalText == goalText)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Добавляет новую цель или возвращает индекс существующей.
    /// </summary>
    public int AddOrGetGoal(string industry, string goalText, List<string> methods)
    {
        int idx = FindGoalIndex(industry, goalText);
        if (idx >= 0)
        {
            // Обновляем методы, если цель уже есть (на случай, если JSON изменился)
            Goals[idx].LearningMethods = new List<string>(methods);
            return idx;
        }

        var goal = new GoalProgress(industry, goalText, methods);
        Goals.Add(goal);
        return Goals.Count - 1;
    }

    /// <summary>
    /// Сбрасывает весь прогресс по всем целям.
    /// </summary>
    public void ResetAllProgress()
    {
        Goals.Clear();
        ActiveGoalIndex = -1;
        TotalPoints = 0;
        Rewards.Clear();
        if (SeenNarrativeKeys == null) SeenNarrativeKeys = new List<string>();
        else SeenNarrativeKeys.Clear();
    }
}
