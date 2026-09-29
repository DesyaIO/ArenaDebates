using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LearningDialogController : MonoBehaviour
{
    public LearningDialogView View;
    public LearningDialogCoach Coach;
    public VoskSpeechToText Speech;
    public DebateTopicSO[] Topics;
    public LearningContentSO[] Lessons;
    public string DefaultMethod = "Гарвардский метод";
    [Min(1)] public int PracticeTurns = 3;

    private GameSession _session;
    private LearningContentSO _lesson;
    private string _method, _pendingTranscript, _submittedTranscript;
    private string _speechError;
    private bool _ready, _initializing, _busy, _recording, _awaitingTranscript, _retry, _finished, _needsClarification;
    private int _generation, _turns, _successes;
    private float _remaining, _recognitionDeadline;
    private Coroutine _request, _progress;

    void Awake()
    {
        View.StartButton.onClick.AddListener(StartLesson);
        View.RecordButton.onClick.AddListener(Record);
        View.RetryButton.onClick.AddListener(Retry);
        View.SetStageButtonActions(SubmitTranscript, Retake, ContinueAfterFeedback, StartLesson, Back);
        Speech.OnStatusUpdated += SpeechStatus;
        Speech.OnTranscriptionResult += Transcript;
    }

    void Start()
    {
        _method = string.IsNullOrWhiteSpace(SceneParams.SelectedLearningMethod) ? DefaultMethod : SceneParams.SelectedLearningMethod;
        _lesson = Array.Find(Lessons, l => l != null && l.MethodologyName == _method);
        View.MethodText.text = "МОДУЛЬ / " + _method.ToUpperInvariant();
        View.SetStatus("Нажмите «Новая тренировка», чтобы начать");
        View.ShowDialogue();
        View.SetStep(1, PracticeTurns, "ДИАЛОГ / ТРЕНИРОВКА");
        Refresh();
    }

    public void StartLesson()
    {
        if (_busy || _recording) return;
        Cancel();
        var valid = Array.FindAll(Topics, t => t != null && t.Positions.Count >= 2 && t.Positions[0] != null && t.Positions[1] != null);
        if (valid.Length == 0) { View.SetStatus("Добавьте тему с двумя позициями в Inspector"); return; }
        var topic = valid[UnityEngine.Random.Range(0, valid.Length)];
        int side = UnityEngine.Random.Range(0, 2);
        _session = new GameSession(topic, topic.Positions[side], topic.Positions[1 - side], false);
        _session.TurnsPerParticipant = 0; // The lesson uses its own PracticeTurns limit.
        _turns = _successes = 0;
        _finished = false;
        _needsClarification = false;
        _submittedTranscript = _pendingTranscript = "";
        View.ShowContext(_method, _session);
        View.SetStep(1, PracticeTurns, "МОДУЛЬ / ПЕРЕГОВОРЫ");
        View.FeedbackText.text = "Разбор появится после вашего ответа. Ошибки распознавания не считаются ошибками метода.";
        View.SetStatus("Говорите своими словами. Необязательно повторять пример дословно.");
        View.DialogueHintText.text = "Готовим подсказку по методу «" + _method + "»…";
        View.OpponentText.text = "Готовим учебную ситуацию…";
        if (!_ready && !_initializing)
        {
            _initializing = true;
            Speech.StartVoskStt(null, default, false, 3);
        }
        _pendingTranscript = "";
        View.ShowDialogue();
        Send();
    }

    void Record()
    {
        if (_busy || !_ready || _finished || _session == null || !_session.IsPlayerTurn) return;
        if (_recording)
        {
            _recording = false;
            _busy = _awaitingTranscript = true;
            _recognitionDeadline = Time.realtimeSinceStartup + 30f;
            Progress("Распознаём вашу речь");
            Speech.StopRecognition();
        }
        else
        {
            _speechError = null;
            if (!Speech.StartRecognition()) { View.SetStatus(_speechError ?? "Дождитесь завершения предыдущей записи"); return; }
            _retry = false;
            _recording = true;
            _remaining = Speech.MaxRecordLength;
        }
        Refresh();
    }

    void Update()
    {
        if (_recording)
        {
            _remaining -= Time.unscaledDeltaTime;
            View.SetStatus($"Говорите: осталось {Mathf.Max(0, Mathf.CeilToInt(_remaining))} сек");
            if (_remaining <= 0) Record();
        }
        if (_awaitingTranscript && Time.realtimeSinceStartup >= _recognitionDeadline)
        {
            _awaitingTranscript = false;
            EndBusy("Распознавание заняло слишком долго. Попробуйте записать ответ ещё раз.");
        }
    }

    void SpeechStatus(string status)
    {
        if (status.StartsWith("Error:"))
        {
            _speechError = status.Substring("Error:".Length).Trim();
            if (_initializing) _ready = false;
            _initializing = false;
            View.SetStatus(_speechError);
            Refresh();
            return;
        }
        if (!status.Contains("Initialized")) return;
        _ready = true;
        _initializing = false;
        Refresh();
    }

    void Transcript(string text)
    {
        if (!_awaitingTranscript) return;
        _awaitingTranscript = false;
        _busy = false;
        if (_progress != null) StopCoroutine(_progress);
        _progress = null;
        if (string.IsNullOrWhiteSpace(text)) { EndBusy("Речь не распознана. Повторите запись."); return; }
        _pendingTranscript = text;
        View.SetRecognized(text);
        View.SetStep(_turns + 1, PracticeTurns, "ВАШ ОТВЕТ / ПРОВЕРКА РЕЧИ");
        View.SetStatus("Проверьте распознанный текст перед отправкой.");
        View.ShowTranscriptScreen();
        Refresh();
    }

    void Retry()
    {
        if (!_busy && !_recording && _retry && !string.IsNullOrWhiteSpace(_submittedTranscript))
            Send(_submittedTranscript);
    }

    void SubmitTranscript()
    {
        if (_busy || _recording || string.IsNullOrWhiteSpace(_pendingTranscript)) return;
        if (View.TranscriptInput != null) _pendingTranscript = View.TranscriptInput.text.Trim();
        if (string.IsNullOrWhiteSpace(_pendingTranscript)) { View.SetStatus("Скажите фразу или проверьте распознанный текст."); return; }
        _submittedTranscript = _pendingTranscript;
        _pendingTranscript = "";
        Send(_submittedTranscript);
    }

    void Retake()
    {
        if (_busy || _recording) return;
        _pendingTranscript = "";
        _submittedTranscript = "";
        View.ShowDialogue();
        Record();
    }

    void ContinueAfterFeedback()
    {
        if (_busy || _finished) return;
        if (_needsClarification)
        {
            _needsClarification = false;
            View.SetContinueLabel("Продолжить диалог");
            View.ShowDialogue();
            View.SetStatus("Переформулируйте мысль и запишите новую реплику. Этот ответ не засчитан.");
            return;
        }
        View.SetStep(_turns + 1, PracticeTurns, "СЛЕДУЮЩИЙ ШАГ / ИНТЕРЕСЫ СТОРОН");
        View.SetContinueLabel("Продолжить диалог");
        View.ShowDialogue();
        View.SetStatus("Оппонент ответил. Используйте подсказку в следующей реплике.");
        View.StartButton.gameObject.SetActive(true);
        View.RecordButton.gameObject.SetActive(true);
    }

    void Send(string transcript = null)
    {
        _busy = true;
        _retry = false;
        int generation = ++_generation;
        bool opening = _session.Entries.Count == 0;
        string submittedText = transcript ?? _pendingTranscript ?? "";
        if (!opening) View.SetStep(_turns + 1, PracticeTurns, "ВАШ ОТВЕТ / РАЗБОР");
        Progress(opening ? "Готовим подсказку" : "Разбираем ответ и готовим следующий ход");
        Refresh();
        _request = Coach.Client.SendRequest(Coach.BuildPrompt(_method, _lesson, _session, submittedText),
            text =>
            {
                if (this == null || !isActiveAndEnabled || generation != _generation) return;
                ++_generation;
                if (!Coach.TryParse(text, opening, out var reply, out string error)) { Fail(error); return; }
                if (reply.needsClarification && !opening)
                {
                    _needsClarification = true;
                    _submittedTranscript = "";
                    View.ShowReply(reply, false);
                    View.SetContinueLabel("Ответить заново");
                    EndBusy("Повторите мысль — эта попытка не засчитана");
                    View.ShowFeedback(false);
                    return;
                }
                if (!opening)
                {
                    _session.AddEntry(true, submittedText, reply.playerCategory, reply.feedback, 0);
                    _turns++;
                    if (reply.methodApplied) _successes++;
                }
                _needsClarification = false;
                _submittedTranscript = "";
                if (!_session.IsGameOver)
                    _session.AddEntry(false, reply.opponentReply, reply.opponentCategory, "Учебная реплика", 0);
                View.ShowReply(reply, opening);
                View.ShowHealth(_session);
                _finished = _turns >= Mathf.Max(1, PracticeTurns) || _session.IsGameOver;
                EndBusy(_finished ? $"Тренировка завершена: метод применён в {_successes} из {_turns} ответов. Можно потренироваться ещё." : "Разбор готов. Посмотрите совет тренера.");
                if (opening || !reply.needsClarification)
                {
                    if (_finished)
                    {
                        View.SetStep(_turns, PracticeTurns, "МОДУЛЬ / ИТОГ");
                        var categories = new List<string>();
                        foreach (var entry in _session.Entries)
                        {
                            if (!entry.IsPlayerTurn || string.IsNullOrWhiteSpace(entry.Category)) continue;
                            if (!categories.Contains(entry.Category)) categories.Add(entry.Category);
                        }
                        string growthAdvice = string.IsNullOrWhiteSpace(reply.improvedExample)
                            ? $"Попробуйте применить «{_method}» раньше в разговоре и уточнить, что важно обеим сторонам."
                            : "Сверьте свою формулировку с примером из разбора и попробуйте назвать интересы сторон до предложения решения.";
                        View.ShowSummary(_method, _turns, _successes, reply.feedback +
                            (string.IsNullOrWhiteSpace(reply.improvedExample) ? "" : "\n\nПример усиленной реплики\n«" + reply.improvedExample + "»"),
                            growthAdvice, categories.ToArray(), View.OpponentAvatar != null ? View.OpponentAvatar.sprite : null);
                    }
                    else if (opening) View.ShowDialogue();
                    else View.ShowFeedback(false);
                }
            },
            error =>
            {
                if (this == null || !isActiveAndEnabled || generation != _generation) return;
                ++_generation;
                Fail(error);
            });
    }

    void Fail(string error)
    {
        _retry = true;
        Debug.LogError("LearningDialog: " + error);
        EndBusy("Не удалось получить разбор. Нажмите «Повторить запрос» или начните новую тренировку.");
        View.ShowFeedback(false);
    }

    void Refresh()
    {
        View.ProgressText.text = $"Реплик: {_turns}/{Mathf.Max(1, PracticeTurns)} · Метод применён: {_successes}";
        View.Controls(_busy, _ready, _recording, _session != null && _session.IsPlayerTurn && !_finished,
            _retry, _finished);
    }

    void Progress(string label)
    {
        if (_progress != null) StopCoroutine(_progress);
        _progress = StartCoroutine(Animate(label));
    }
    IEnumerator Animate(string label)
    {
        int n = 0;
        while (true) { View.SetStatus(label + new string('.', n++ % 4)); yield return new WaitForSecondsRealtime(.5f); }
    }
    void EndBusy(string status)
    {
        if (_progress != null) StopCoroutine(_progress);
        _progress = _request = null;
        _busy = false;
        View.SetStatus(status);
        Refresh();
    }
    void Cancel()
    {
        ++_generation;
        _awaitingTranscript = false;
        if (_recording) Speech.StopRecognition();
        _recording = false;
        if (_request != null) Coach.Client.StopCoroutine(_request);
        if (_progress != null) StopCoroutine(_progress);
        _request = _progress = null;
        _busy = _retry = false;
    }

    void Back() { Cancel(); SceneManager.LoadScene("MenuScene"); }
    void OnDisable() { Cancel(); }
    void OnDestroy()
    {
        View.StartButton.onClick.RemoveListener(StartLesson);
        View.RecordButton.onClick.RemoveListener(Record);
        View.RetryButton.onClick.RemoveListener(Retry);
        View.SetStageButtonActions(null, null, null, null, null);
        if (View.HintButton != null) View.HintButton.onClick.RemoveListener(View.ToggleHint);
        Speech.OnStatusUpdated -= SpeechStatus;
        Speech.OnTranscriptionResult -= Transcript;
    }
}
