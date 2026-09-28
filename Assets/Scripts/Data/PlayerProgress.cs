using System.Collections.Generic;
using UnityEngine;

public static class PlayerProgress
{
    public const int TotalListeningTasks = 3;

    private static GoalProgress ActiveGoal => UserManager.CurrentUser?.GetActiveGoal();

    // ---------- Обучение ----------

    public static bool IsLearningCompleted(string methodologyName)
        => ActiveGoal != null && ActiveGoal.CompletedMethods.Contains(methodologyName);

    public static void MarkLearningCompleted(string methodologyName)
    {
        if (ActiveGoal == null) return;

        if (!ActiveGoal.CompletedMethods.Contains(methodologyName))
        {
            ActiveGoal.CompletedMethods.Add(methodologyName);
            UserManager.SaveCurrentUser();
        }
    }

    // ---------- Аудирование ----------

    public static int ListeningSolved
    {
        get => ActiveGoal != null ? ActiveGoal.ListeningSolved : 0;
        set
        {
            if (ActiveGoal == null) return;
            ActiveGoal.ListeningSolved = value;
            UserManager.SaveCurrentUser();
        }
    }

    public static bool IsAllListeningDone()
        => ListeningSolved >= TotalListeningTasks;

    public static string ListeningProgressText()
        => $"Решено: {ListeningSolved} / {TotalListeningTasks}";

    // ---------- Общее ----------

    public static bool HasActiveGoal()
        => ActiveGoal != null;
}