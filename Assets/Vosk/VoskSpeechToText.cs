using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;
using Vosk;

public class VoskSpeechToText : MonoBehaviour
{
    [Tooltip("Location of the model, relative to the Streaming Assets folder.")]
    public string ModelPath = "vosk-model-small-ru-0.22.zip";

    [Tooltip("The source of the microphone input.")]
    public VoiceProcessor VoiceProcessor;

    [Tooltip("The Max number of alternatives that will be processed.")]
    public int MaxAlternatives = 3;

    [Tooltip("How long should we record before restarting?")]
    public float MaxRecordLength = 10;

    [Tooltip("Should the recognizer start when the application is launched?")]
    public bool AutoStart = false;

    [Tooltip("The phrases that will be detected. If left empty, all words will be detected.")]
    public List<string> KeyPhrases = new List<string>();

    // Cached Vosk model and recognizer
    private Model _model;
    private VoskRecognizer _recognizer;
    private bool _recognizerReady;

    // Accumulated text for current recording session
    private readonly StringBuilder _accumulatedText = new StringBuilder();

    // Status/result events
    public Action<string> OnStatusUpdated;
    public Action<string> OnTranscriptionResult;

    // Path to the decompressed model folder
    private string _decompressedModelPath;

    // Grammar JSON for key phrases
    private string _grammar = "";

    // Flags
    private bool _isInitializing;
    private bool _didInit;
    private string _initializationError;

    // Threading
    private volatile bool _running;
    private Task _worker;
    private bool _finishing;
    private readonly ConcurrentQueue<short[]> _threadedBufferQueue = new ConcurrentQueue<short[]>();
    private readonly ConcurrentQueue<string> _threadedResultQueue = new ConcurrentQueue<string>();

    static readonly ProfilerMarker voskRecognizerCreateMarker = new ProfilerMarker("VoskRecognizer.Create");
    static readonly ProfilerMarker voskRecognizerReadMarker = new ProfilerMarker("VoskRecognizer.AcceptWaveform");

    void Start()
    {
        if (AutoStart)
        {
            StartVoskStt();
        }
    }

    /// <summary>
    /// Инициализация Vosk. Микрофон НЕ включается.
    /// </summary>
    public void StartVoskStt(List<string> keyPhrases = null, string modelPath = default,
        bool startMicrophone = false, int maxAlternatives = 3)
    {
        if (_isInitializing)
        {
            Debug.LogError("Initializing in progress!");
            return;
        }
        if (_didInit)
        {
            Debug.LogError("Vosk has already been initialized!");
            return;
        }

        if (!string.IsNullOrEmpty(modelPath))
            ModelPath = modelPath;

        if (keyPhrases != null)
            KeyPhrases = keyPhrases;

        MaxAlternatives = maxAlternatives;
        StartCoroutine(DoStartVoskStt(startMicrophone));
    }

    private IEnumerator DoStartVoskStt(bool startMicrophone)
    {
        _isInitializing = true;
        _initializationError = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            float permissionDeadline = Time.realtimeSinceStartup + 20f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)
                   && Time.realtimeSinceStartup < permissionDeadline)
                yield return null;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                FailInitialization("Разрешите доступ к микрофону в настройках приложения.");
                yield break;
            }
        }
#endif
        yield return Decompress();
        if (!string.IsNullOrEmpty(_initializationError))
        {
            FailInitialization(_initializationError);
            yield break;
        }

        OnStatusUpdated?.Invoke("Loading Model from: " + _decompressedModelPath);
        try
        {
            _model = new Model(_decompressedModelPath);
        }
        catch (DllNotFoundException exception)
        {
            _initializationError = "Не найдена библиотека libvosk.dll в сборке: " + exception.Message;
        }
        catch (Exception exception)
        {
            _initializationError = "Не удалось загрузить модель Vosk: " + exception.Message;
        }
        if (!string.IsNullOrEmpty(_initializationError))
        {
            FailInitialization(_initializationError);
            yield break;
        }

        yield return null;

        if (VoiceProcessor == null)
        {
            FailInitialization("Не назначен VoiceProcessor.");
            yield break;
        }

        VoiceProcessor.OnFrameCaptured += VoiceProcessorOnFrameCaptured;
        VoiceProcessor.OnRecordingStop += VoiceProcessorOnRecordingStop;

        if (startMicrophone)
            VoiceProcessor.StartRecording();

        _isInitializing = false;
        _didInit = true;

        OnStatusUpdated?.Invoke("Initialized");
    }

    /// <summary>
    /// Запуск распознавания (кнопка «Начать запись»).
    /// </summary>
    public bool StartRecognition()
    {
        if (!_didInit)
        {
            Debug.LogError("Vosk не инициализирован!");
            return false;
        }
        if (_running || _finishing || (_worker != null && !_worker.IsCompleted)) return false;
        VoiceProcessor.UpdateDevices();
        if (Microphone.devices.Length == 0)
        {
            OnStatusUpdated?.Invoke("Error: Микрофон не найден или доступ к нему запрещён в Windows.");
            return false;
        }

        // Очищаем накопленный текст перед новой сессией
        _accumulatedText.Clear();
        while (_threadedResultQueue.TryDequeue(out _)) { }
        while (_threadedBufferQueue.TryDequeue(out _)) { }

        try
        {
            VoiceProcessor.StartRecording();
        }
        catch (Exception exception)
        {
            OnStatusUpdated?.Invoke("Error: Не удалось включить микрофон: " + exception.Message);
            return false;
        }
        if (!VoiceProcessor.IsRecording)
        {
            OnStatusUpdated?.Invoke("Error: Микрофон не начал запись. Проверьте разрешение на доступ к микрофону.");
            return false;
        }
        _running = true;
        _worker = Task.Run(ThreadedWork);
        Debug.Log("Recognition started");
        return true;
    }

    /// <summary>
    /// Остановка распознавания (кнопка «Стоп» или таймер).
    /// Возвращает ВЕСЬ накопленный текст одним куском.
    /// </summary>
    public void StopRecognition()
    {
        if (!_running) return;

        _running = false;
        VoiceProcessor.StopRecording();

        _finishing = true;
        StartCoroutine(FinishRecognition());
    }

    private IEnumerator FinishRecognition()
    {
        // Native recognizer and StringBuilder belong exclusively to the worker.
        while (_worker != null && !_worker.IsCompleted) yield return null;
        string text = "";
        if (_worker != null && _worker.IsFaulted)
            Debug.LogError($"Ошибка распознавания Vosk: {_worker.Exception?.GetBaseException().Message}");
        else
            text = _accumulatedText.ToString().Trim();
        _accumulatedText.Clear();
        _finishing = false;
        // Empty audio is also a completion, so the UI can leave processing state.
        _threadedResultQueue.Enqueue(text);
    }

    private void Update()
    {
        if (_threadedResultQueue.TryDequeue(out string voiceResult))
        {
            OnTranscriptionResult?.Invoke(voiceResult);
        }
    }

    private void VoiceProcessorOnFrameCaptured(short[] samples)
    {
        if (_running) _threadedBufferQueue.Enqueue(samples);
    }

    private void VoiceProcessorOnRecordingStop()
    {
        Debug.Log("Stopped");
    }

    /// <summary>
    /// Поток распознавания. Работает, пока _running == true
    /// или пока в очереди остались фреймы.
    /// </summary>
    private async Task ThreadedWork()
    {
        voskRecognizerCreateMarker.Begin();

        if (!_recognizerReady)
        {
            UpdateGrammar();

            if (string.IsNullOrEmpty(_grammar))
                _recognizer = new VoskRecognizer(_model, 16000.0f);
            else
                _recognizer = new VoskRecognizer(_model, 16000.0f, _grammar);

            _recognizer.SetMaxAlternatives(MaxAlternatives);
            _recognizerReady = true;

            Debug.Log("Recognizer ready");
        }

        voskRecognizerCreateMarker.End();
        voskRecognizerReadMarker.Begin();

        while (_running || !_threadedBufferQueue.IsEmpty)
        {
            if (_threadedBufferQueue.TryDequeue(out short[] voiceResult))
            {
                if (_recognizer.AcceptWaveform(voiceResult, voiceResult.Length))
                {
                    var result = _recognizer.Result();
                    string text = ParseTextFromJson(result);
                    if (!string.IsNullOrEmpty(text))
                    {
                        _accumulatedText.Append(text).Append(" ");
                    }
                }
            }
            else
            {
                await Task.Delay(10);
            }
        }

        string finalText = ParseTextFromJson(_recognizer.FinalResult());
        if (!string.IsNullOrWhiteSpace(finalText)) _accumulatedText.Append(finalText).Append(" ");
        voskRecognizerReadMarker.End();
    }

    // ---------- Вспомогательные ----------

    private void UpdateGrammar()
    {
        if (KeyPhrases.Count == 0)
        {
            _grammar = "";
            return;
        }

        JSONArray keywords = new JSONArray();
        foreach (string keyphrase in KeyPhrases)
            keywords.Add(new JSONString(keyphrase.ToLower()));
        keywords.Add(new JSONString("[unk]"));

        _grammar = keywords.ToString();
    }

    private IEnumerator Decompress()
    {
        string modelFolder = Path.Combine(Application.persistentDataPath,
            Path.GetFileNameWithoutExtension(ModelPath));
        if (Directory.Exists(modelFolder) && File.Exists(Path.Combine(modelFolder, "am", "final.mdl")))
        {
            OnStatusUpdated?.Invoke("Using existing decompressed model.");
            _decompressedModelPath = modelFolder;
            yield break;
        }

        OnStatusUpdated?.Invoke("Decompressing model...");
        string dataPath = Path.Combine(Application.streamingAssetsPath, ModelPath);

        Stream dataStream = null;
        if (dataPath.Contains("://"))
        {
            UnityWebRequest www = UnityWebRequest.Get(dataPath);
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                _initializationError = "Не удалось прочитать ZIP-модель из StreamingAssets: " + www.error;
                www.Dispose();
                yield break;
            }
            dataStream = new MemoryStream(www.downloadHandler.data);
            www.Dispose();
        }
        else
        {
            try { dataStream = File.OpenRead(dataPath); }
            catch (Exception exception)
            {
                _initializationError = "ZIP-модель Vosk не найдена в StreamingAssets: " + exception.Message;
            }
            if (!string.IsNullOrEmpty(_initializationError)) yield break;
        }

        try
        {
            using (dataStream)
            using (var zipFile = ZipFile.Read(dataStream))
            {
                OnStatusUpdated?.Invoke("Reading Zip file");
                zipFile.ExtractAll(Application.persistentDataPath, ExtractExistingFileAction.OverwriteSilently);
            }
        }
        catch (Exception exception)
        {
            _initializationError = "Не удалось распаковать модель Vosk: " + exception.Message;
        }
        if (!string.IsNullOrEmpty(_initializationError)) yield break;

        _decompressedModelPath = modelFolder;
        if (!File.Exists(Path.Combine(modelFolder, "am", "final.mdl")))
        {
            _initializationError = "ZIP-модель Vosk распакована не полностью: отсутствует am/final.mdl.";
            yield break;
        }

        OnStatusUpdated?.Invoke("Decompressing complete!");
        yield return null;
    }

    private void FailInitialization(string message)
    {
        _isInitializing = false;
        _didInit = false;
        Debug.LogError("Vosk: " + message, this);
        OnStatusUpdated?.Invoke("Error: " + message);
    }

    /// <summary>
    /// Парсит JSON от Vosk и вытаскивает поле "text".
    /// </summary>
    private string ParseTextFromJson(string json)
    {
        const string key = "\"text\" : \"";
        int startIndex = json.IndexOf(key, StringComparison.Ordinal);
        if (startIndex < 0) return "";
        startIndex += key.Length;

        int endIndex = json.IndexOf("\"", startIndex, StringComparison.Ordinal);
        if (endIndex < 0) return "";

        return json.Substring(startIndex, endIndex - startIndex).Trim();
    }
}
