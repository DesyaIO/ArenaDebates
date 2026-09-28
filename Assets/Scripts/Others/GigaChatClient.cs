using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class GigaChatClient : MonoBehaviour
{
    [Header("GigaChat API")]
    [Tooltip("Ключ авторизации (Authorization Key) из личного кабинета Sber Developers")]
    public string AuthorizationKey = "ВАШ_КЛЮЧ_АВТОРИЗАЦИИ";
    
    // Scope для физических лиц
    private const string Scope = "GIGACHAT_API_PERS";
    
    // URL для получения токена
    private const string AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    
    // URL для чата (с 17 июля 2026 используется api.giga.chat)
    private const string ChatUrl = "https://api.giga.chat/v1/chat/completions";

    // Кэшированный токен доступа
    private string _accessToken = "";
    private DateTime _tokenExpiry = DateTime.MinValue;

    // Структуры для JSON
    [Serializable]
    private class GigaChatRequest
    {
        public string model = "GigaChat";
        public List<Message> messages;
        public float temperature = 0.1f; // Низкая температура для стабильного формата
        public int max_tokens = 500;
    }

    [Serializable]
    private class Message
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class GigaChatResponse
    {
        public List<Choice> choices;
    }

    [Serializable]
    private class Choice
    {
        public Message message;
    }

    [Serializable]
    private class TokenResponse
    {
        public string access_token;
        public long expires_at;
    }

    /// <summary>
    /// Отправляет промпт в GigaChat. Сначала получает токен, если его нет.
    /// </summary>
    public void SendRequest(string prompt, Action<string> onComplete, Action<string> onError)
    {
        StartCoroutine(SendRequestCoroutine(prompt, onComplete, onError));
    }

    private IEnumerator SendRequestCoroutine(string prompt, Action<string> onComplete, Action<string> onError)
    {
        // 1. Проверяем токен. Если истёк или отсутствует — получаем новый.
        if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
        {
            yield return StartCoroutine(GetTokenCoroutine());
            
            if (string.IsNullOrEmpty(_accessToken))
            {
                onError?.Invoke("Не удалось получить Access Token.");
                yield break;
            }
        }

        // 2. Формируем тело запроса
        var requestBody = new GigaChatRequest
        {
            messages = new List<Message>
            {
                new Message { role = "user", content = prompt }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        Debug.Log($"JSON запроса: {jsonBody}");

        // 3. Отправляем запрос в чат
        using (UnityWebRequest request = new UnityWebRequest(ChatUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _accessToken);
            
            // Важно: для api.giga.chat требуется User-Agent, иначе может быть ошибка авторизации
            request.SetRequestHeader("User-Agent", "Unity-GigaChat-Client/1.0");

            Debug.Log($"Отправляю запрос в GigaChat...");
            yield return request.SendWebRequest();
            Debug.Log($"Ответ получен: result={request.result}, code={request.responseCode}, text={request.downloadHandler.text}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GigaChat Error: {request.error}\n{request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
            else
            {
                try
                {
                    GigaChatResponse response = JsonUtility.FromJson<GigaChatResponse>(request.downloadHandler.text);
                    
                    if (response?.choices != null && response.choices.Count > 0)
                    {
                        string responseText = response.choices[0].message.content;
                        onComplete?.Invoke(responseText);
                    }
                    else
                    {
                        onError?.Invoke("Пустой ответ от GigaChat.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Ошибка парсинга ответа: {e.Message}");
                    onError?.Invoke("Ошибка парсинга ответа.");
                }
            }
        }
    }

    /// <summary>
    /// Получает Access Token через OAuth 2.0.
    /// </summary>
    private IEnumerator GetTokenCoroutine()
    {
        Debug.Log("Получаю Access Token...");

        using (UnityWebRequest request = new UnityWebRequest(AuthUrl, "POST"))
        {
            // Тело: scope=GIGACHAT_API_PERS
            byte[] bodyRaw = Encoding.UTF8.GetBytes($"scope={Scope}");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            request.SetRequestHeader("Accept", "application/json");
            
            // RqUID — уникальный идентификатор запроса в формате uuid4
            request.SetRequestHeader("RqUID", Guid.NewGuid().ToString());
            
            // Ключ авторизации (уже должен быть в base64 формате)
            request.SetRequestHeader("Authorization", "Basic " + AuthorizationKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Ошибка получения токена: {request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            try
            {
                TokenResponse tokenResponse = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                _accessToken = tokenResponse.access_token;
                
                // Токен живёт 30 минут, берём с запасом — 25 минут
                _tokenExpiry = DateTime.UtcNow.AddMinutes(25);
                
                Debug.Log($"Access Token получен. Действует до: {_tokenExpiry}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка парсинга токена: {e.Message}\n{request.downloadHandler.text}");
            }
        }
    }
}