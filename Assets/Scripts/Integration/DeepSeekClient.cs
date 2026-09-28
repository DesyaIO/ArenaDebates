using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class DeepSeekClient : MonoBehaviour
{
    [Header("KodikRouter API")]
    public string ApiKey = "ВАШ_КЛЮЧ_KODIKROUTER";
    public string ModelName = "deepseek-v4-flash";
    public string ApiUrl = "https://api.kodikrouter.ru/v1/chat/completions";

    [Range(0f, 1f)]
    public float Temperature = 0.1f;

    public int MaxTokens = 500;

    [Header("Timeout")]
    [Tooltip("Сколько секунд ждать ответ, прежде чем сообщить о долгом ожидании")]
    public float WarnAfterSeconds = 10f;

    // ---------- JSON-структуры ----------

    [Serializable]
    private class DeepSeekRequest
    {
        public string model;
        public List<Message> messages;
        public float temperature;
        public int max_tokens;
        public ReasoningOptions reasoning;
        public ThinkingOptions thinking;
        public string reasoning_effort;
    }

    [Serializable]
    private class ReasoningOptions
    {
        public bool enabled;
    }

    [Serializable]
    private class ThinkingOptions
    {
        public string type;
    }

    [Serializable]
    private class Message
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class DeepSeekResponse
    {
        public string id;
        public List<Choice> choices;
        public Usage usage;
    }

    [Serializable]
    private class Choice
    {
        public ResponseMessage message;
        public string finish_reason;
    }

    [Serializable]
    private class ResponseMessage
    {
        public string content;
        public string reasoning_content;
    }

    [Serializable]
    private class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public TokenDetails completion_tokens_details;
    }

    [Serializable]
    private class TokenDetails
    {
        public int reasoning_tokens;
    }

    // ---------- Публичный API ----------

    public Coroutine SendRequest(string prompt, Action<string> onComplete, Action<string> onError)
    {
        return StartCoroutine(SendRequestCoroutine(prompt, onComplete, onError));
    }

    // ---------- Основной запрос ----------

    private IEnumerator SendRequestCoroutine(string prompt, Action<string> onComplete, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.StartsWith("ВАШ_КЛЮЧ"))
        {
            onError?.Invoke("Укажите API-ключ KodikRouter в компоненте DeepSeekClient.");
            yield break;
        }

        // 1. Формируем тело запроса
        var requestBody = new DeepSeekRequest
        {
            model = ModelName,
            messages = new List<Message>
            {
                new Message { role = "user", content = prompt }
            },
            temperature = Temperature,
            max_tokens = MaxTokens,
            reasoning = new ReasoningOptions { enabled = false },
            // DeepSeek's native Chat Completions switch; a gateway may ignore
            // the OpenRouter-specific reasoning.enabled field.
            thinking = new ThinkingOptions { type = "disabled" },
            reasoning_effort = "none"
        };

        string jsonBody = JsonUtility.ToJson(requestBody);

        // 2. Отправляем запрос
        using (UnityWebRequest request = new UnityWebRequest(ApiUrl, "POST"))
        {
            request.timeout = 120;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + ApiKey.Trim());

            Debug.Log($"Отправляю запрос в KodikRouter ({ModelName})...");

            // Засекаем время
            float startTime = Time.realtimeSinceStartup;
            bool warnedAboutLongWait = false;

            // Запускаем запрос и ждём, обновляя лог о прогрессе
            UnityWebRequestAsyncOperation asyncOp = request.SendWebRequest();

            while (!asyncOp.isDone)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;

                // Логируем прогресс каждую секунду
                if (Mathf.FloorToInt(elapsed) != Mathf.FloorToInt(elapsed - Time.deltaTime))
                {
                    Debug.Log($"... ожидание ответа от DeepSeek: {Mathf.FloorToInt(elapsed)} сек.");
                }

                // Предупреждение о долгом ожидании
                if (!warnedAboutLongWait && elapsed >= WarnAfterSeconds)
                {
                    warnedAboutLongWait = true;
                    Debug.LogWarning($"DeepSeek не отвечает уже {WarnAfterSeconds} секунд. Ожидаем ответ KodikRouter.");
                }

                yield return null;
            }

            float totalTime = Time.realtimeSinceStartup - startTime;
            Debug.Log($"Ответ получен за {totalTime:F2} сек. result={request.result}, code={request.responseCode}");

            // 3. Обрабатываем результат
            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"Ошибка KodikRouter (HTTP {request.responseCode}): {request.error}";
                Debug.LogError(error);
                onError?.Invoke(error);
                yield break;
            }

            DeepSeekResponse response;
            try
            {
                response = JsonUtility.FromJson<DeepSeekResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка парсинга ответа KodikRouter: {e.Message}");
                onError?.Invoke("Ошибка парсинга ответа KodikRouter.");
                yield break;
            }

            if (response?.choices == null || response.choices.Count == 0 ||
                string.IsNullOrWhiteSpace(response.choices[0]?.message?.content))
            {
                Choice choice = response?.choices != null && response.choices.Count > 0
                    ? response.choices[0] : null;
                string reason = choice?.finish_reason ?? "unknown";
                string requestId = request.GetResponseHeader("X-KodikRouter-Request-Id")
                    ?? response?.id ?? "unknown";
                string usage = response?.usage == null ? "usage отсутствует"
                    : $"prompt_tokens={response.usage.prompt_tokens}, completion_tokens={response.usage.completion_tokens}, reasoning_tokens={response.usage.completion_tokens_details?.reasoning_tokens.ToString() ?? "unknown"}";
                string hint = reason == "length"
                    ? "Достигнут лимит генерации. Проверьте Max Tokens и режим рассуждений."
                    : "Проверьте запись запроса в кабинете KodikRouter.";
                string diagnostic = $"Нет итогового текста DeepSeek: finish_reason={reason}, {usage}, " +
                    $"reasoning_present={!string.IsNullOrWhiteSpace(choice?.message?.reasoning_content)}, " +
                    $"request_id={requestId}. {hint} Автоматического повтора нет.";
                onError?.Invoke(diagnostic);
                yield break;
            }

            // Ошибки пользовательского обработчика не являются ошибками парсинга.
            onComplete?.Invoke(response.choices[0].message.content);
        }
    }
}
