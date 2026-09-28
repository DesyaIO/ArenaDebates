using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public static class GoalLoader
{
    private const string FileName = "goals.json";

    /// <summary>
    /// Загружает JSON с целями из StreamingAssets.
    /// </summary>
    public static IEnumerator LoadGoals(Action<GoalDatabase> onDone)
    {
        string path = Path.Combine(Application.streamingAssetsPath, FileName);

        // На Android StreamingAssets читается только через UnityWebRequest
        if (path.Contains("://") || path.Contains(":///"))
        {
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Ошибка загрузки goals.json: {www.error}");
                    onDone?.Invoke(new GoalDatabase());
                    yield break;
                }

                string json = www.downloadHandler.text;
                onDone?.Invoke(Parse(json));
            }
        }
        else
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"goals.json не найден: {path}");
                onDone?.Invoke(new GoalDatabase());
                yield break;
            }

            string json = File.ReadAllText(path);
            onDone?.Invoke(Parse(json));
            yield break;
        }
    }

    private static GoalDatabase Parse(string json)
    {
        try
        {
            var db = JsonUtility.FromJson<GoalDatabase>(json);
            Debug.Log($"Загружено отраслей: {db.Industries.Count}");
            return db ?? new GoalDatabase();
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка парсинга goals.json: {e.Message}");
            return new GoalDatabase();
        }
    }
}