using System;
using System.IO;
using UnityEngine;

public static class UserManager
{
    private const string FileName = "users.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static UserData CurrentUser { get; private set; }

    // ---------- Загрузка / сохранение ----------

    public static UserDatabase LoadDatabase()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var db = JsonUtility.FromJson<UserDatabase>(json);
                return db ?? new UserDatabase();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка загрузки users.json: {e.Message}");
        }
        return new UserDatabase();
    }

    public static void SaveDatabase(UserDatabase db)
    {
        try
        {
            string json = JsonUtility.ToJson(db, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"users.json сохранён: {FilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка сохранения users.json: {e.Message}");
        }
    }

    // ---------- Регистрация / авторизация ----------

    public static UserData Register(string login, string password, string fullName)
    {
        var db = LoadDatabase();

        if (db.Users.Exists(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase)))
            return null;

        var user = new UserData(login, password, fullName);
        db.Users.Add(user);
        SaveDatabase(db);

        CurrentUser = user;
        return user;
    }

    public static UserData Login(string login, string password)
    {
        var db = LoadDatabase();
        var user = db.Users.Find(u =>
            u.Login.Equals(login, StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        if (user == null) return null;

        CurrentUser = user;
        return user;
    }

    public static void SaveCurrentUser()
    {
        if (CurrentUser == null) return;

        var db = LoadDatabase();
        int idx = db.Users.FindIndex(u => u.Login == CurrentUser.Login);

        if (idx >= 0) db.Users[idx] = CurrentUser;
        else db.Users.Add(CurrentUser);

        SaveDatabase(db);
    }

    // ---------- Цели ----------

    /// <summary>
    /// Устанавливает активную цель. Если цель уже есть — переключается на неё,
    /// если нет — добавляет новую.
    /// </summary>
    public static void SetActiveGoal(string industry, string goalText, System.Collections.Generic.List<string> methods)
    {
        if (CurrentUser == null) return;

        int idx = CurrentUser.AddOrGetGoal(industry, goalText, methods);
        CurrentUser.ActiveGoalIndex = idx;

        // Обновляем дату последней игры
        CurrentUser.Goals[idx].LastPlayedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        SaveCurrentUser();
        Debug.Log($"Активная цель: {industry} — {goalText} (индекс {idx})");
    }

    /// <summary>
    /// Полный сброс всего прогресса по всем целям.
    /// </summary>
    public static void ResetAllProgress()
    {
        if (CurrentUser == null) return;

        CurrentUser.ResetAllProgress();
        SaveCurrentUser();
        Debug.Log("Весь прогресс по всем целям сброшен.");
    }

    public static void Logout()
    {
        CurrentUser = null;
    }
}