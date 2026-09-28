using System;
using System.Collections;
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
    private string _method, _pendingTranscript;
    private bool _ready, _initializing, _busy, _recording, _awaitingTranscript, _retry, _finished;
    private int _generation, _turns, _successes;
    private float _remaining, _recognitionDeadline;
    private Coroutine _request, _progress;

    void Awake()
    {
        View.StartButton.onClick.AddListener(StartLesson);
        View.RecordButton.onClick.AddListener(Record);
        View.RetryButton.onClick.AddListener(Retry);
        View.BackButton.onClick.AddListener(Back);
        Speech.OnStatusUpdated += SpeechStatus;
        Speech.OnTranscriptionResult += Transcript;
    }

    void Start()
    {
        _method = string.IsNullOrWhiteSpace(SceneParams.SelectedLearningMethod) ? DefaultMethod : SceneParams.SelectedLearningMethod;
        _lesson = Array.Find(Lessons, l => l != null && l.MethodologyName == _method);
        View.MethodText.text = "Учимся применять: " + _method;
        View.StatusText.text = "Нажмите «Новая тренировка», чтобы начать";
        Refresh();
    }

    public void StartLesson()
    {
        if (_busy || _recording) return;
        Cancel();
        var valid = Array.FindAll(Topics, t => t != null && t.Positions.Count >= 2 && t.Positions[0] != null && t.Positions[1] != null);
        if (valid.Length == 0) { View.StatusText.text = "Добавьте тему с двумя позициями в Inspector"; return; }
        var topic = valid[UnityEngine.Random.Range(0, valid.Length)];
        int side = UnityEngine.Random.Range(0, 2);
        _session = new GameSession(topic, topic.Positions[side], topic.Positions[1 - side], false);
        _turns = _successes = 0;
        _finished = false;
        View.ShowContext(_method, _session);
        View.FeedbackText.text = "Разбор появится после вашего ответа. Ошибки распознавания не считаются ошибками метода.";
        View.TranscriptText.text = "Говорите своими словами. Необязательно повторять пример дословно.";
        View.HintText.text = "Готовим подсказку по методу «" + _method + "»…";
        View.OpponentText.text = "Готовим учебную ситуацию…";
        if (!_ready && !_initializing)
        {
            if (Microphone.devices.Length == 0)
                View.TranscriptText.text = "Микрофон не найден. Подключите его и начните новую тренировку.";
            else
            {
                _initializing = true;
                Speech.StartVoskStt(null, default, false, 3);
            }
        }
        _pendingTranscript = "";
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
            if (!Speech.StartRecognition()) { View.StatusText.text = "Дождитесь завершения предыдущей записи"; return; }
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
            View.StatusText.text = $"Говорите: осталось {Mathf.Max(0, Mathf.CeilToInt(_remaining))} сек";
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
        if (!status.Contains("Initialized")) return;
        _ready = true;
        _initializing = false;
        Refresh();
    }

    void Transcript(string text)
    {
        if (!_awaitingTranscript) return;
        _awaitingTranscript = false;
        if (string.IsNullOrWhiteSpace(text)) { EndBusy("Речь не распознана. Повторите запись."); return; }
        View.TranscriptText.text = "Распознано\n" + text;
        _pendingTranscript = text;
        Send();
    }

    void Retry() { if (!_busy && !_recording && _retry) Send(); }

    void Send()
    {
        _busy = true;
        _retry = false;
        int generation = ++_generation;
        bool opening = _session.Entries.Count == 0;
        Progress(opening ? "Готовим подсказку" : "Разбираем ответ и готовим следующий ход");
        Refresh();
        _request = Coach.Client.SendRequest(Coach.BuildPrompt(_method, _lesson, _session, _pendingTranscript),
            text =>
            {
                if (this == null || !isActiveAndEnabled || generation != _generation) return;
                ++_generation;
                if (!Coach.TryParse(text, opening, out var reply, out string error)) { Fail(error); return; }
                if (reply.needsClarification && !opening)
                {
                    View.ShowReply(reply, false);
                    EndBusy("Повторите мысль — здоровье и число попыток не изменились");
                    return;
                }
                if (!opening)
                {
                    _session.AddEntry(true, _pendingTranscript, reply.playerCategory, reply.feedback, Coach.AnswerTypes.GetDamage(reply.playerCategory));
                    _turns++;
                    if (reply.methodApplied) _successes++;
                }
                if (!_session.IsGameOver)
                    _session.AddEntry(false, reply.opponentReply, reply.opponentCategory, "Учебная реплика", opening ? 0 : Coach.AnswerTypes.GetDamage(reply.opponentCategory));
                View.ShowReply(reply, opening);
                View.ShowHealth(_session);
                _finished = _turns >= Mathf.Max(1, PracticeTurns) || _session.IsGameOver;
                EndBusy(_finished ? $"Тренировка завершена: метод применён в {_successes} из {_turns} ответов. Можно потренироваться ещё." : "Ваш ход — используйте подсказку");
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
        while (true) { View.StatusText.text = label + new string('.', n++ % 4); yield return new WaitForSecondsRealtime(.5f); }
    }
    void EndBusy(string status)
    {
        if (_progress != null) StopCoroutine(_progress);
        _progress = _request = null;
        _busy = false;
        View.StatusText.text = status;
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
        View.BackButton.onClick.RemoveListener(Back);
        Speech.OnStatusUpdated -= SpeechStatus;
        Speech.OnTranscriptionResult -= Transcript;
    }
}
