using System.Collections.Generic;
using UnityEngine;

public static class PlayerProgress
{
    private static ListeningTaskSO[] _listeningTaskCatalog;

    // A goal may include only one or two of the methods represented by the
    // listening recordings, so the required count must match that goal.
    public static int TotalListeningTasks
    {
        get
        {
            GoalProgress goal = ActiveGoal;
            if (goal == null || goal.LearningMethods == null || goal.LearningMethods.Count == 0)
                return 0;

            var methods = new HashSet<string>(goal.LearningMethods);
            var taskIds = new HashSet<string>();
            foreach (ListeningTaskSO task in GetListeningTaskCatalog())
            {
                if (task == null || string.IsNullOrWhiteSpace(task.TaskId) ||
                    !methods.Contains(task.CorrectMethodology))
                    continue;

                taskIds.Add(task.TaskId);
            }

            return taskIds.Count;
        }
    }

    private static GoalProgress ActiveGoal => UserManager.CurrentUser?.GetActiveGoal();

    private static ListeningTaskSO[] GetListeningTaskCatalog()
    {
        if (_listeningTaskCatalog == null || _listeningTaskCatalog.Length == 0)
        {
            _listeningTaskCatalog = Resources.LoadAll<ListeningTaskSO>("ListetingTasks");
            if (_listeningTaskCatalog.Length == 0)
                _listeningTaskCatalog = Resources.LoadAll<ListeningTaskSO>("ListeningTasks");
        }

        return _listeningTaskCatalog;
    }

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

    // ---------- Сюжет меню ----------

    public static bool HasSeenNarrative(string key)
    {
        var user = UserManager.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(key)) return false;
        if (user.SeenNarrativeKeys == null) user.SeenNarrativeKeys = new List<string>();
        return user.SeenNarrativeKeys.Contains(key);
    }

    public static void MarkNarrativeSeen(string key)
    {
        var user = UserManager.CurrentUser;
        if (user == null || string.IsNullOrWhiteSpace(key)) return;
        if (user.SeenNarrativeKeys == null) user.SeenNarrativeKeys = new List<string>();
        if (user.SeenNarrativeKeys.Contains(key)) return;
        user.SeenNarrativeKeys.Add(key);
        UserManager.SaveCurrentUser();
    }

    // ---------- Общее ----------

    public static bool HasActiveGoal()
        => ActiveGoal != null;
}
