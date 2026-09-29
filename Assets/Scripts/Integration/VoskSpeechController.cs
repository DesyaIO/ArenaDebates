using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;

public class VoskSpeechController : MonoBehaviour
{
    [Header("Ссылки")]
    public VoskSpeechToText vosk;
    public VoiceProcessor voiceProcessor;
    [FormerlySerializedAs("ollamaClient")]
    public DeepSeekClient deepSeekClient;
    public GameManager gameManager;
    public GameUIController uiController;

    [Header("UI")]
    public Button recordButton;
    public Button microphoneButton;
    public TMP_Text statusText;
    public TMP_Text resultText;

    [Header("Сложность оппонента")]
    [Tooltip("Лёгкий / Средний / Сложный")]
    public string OpponentDifficulty = "Средний";

    [Header("Промпт для оппонента")]
    [Tooltip("Промпт для генерации ответа оппонента. {topic}, {position}, {history}, {playerAnswer} будут заменены.")]
    [TextArea(5, 20)]
public string OpponentPromptTemplate = @"
**Роль:** Ты — участник дебатов, который отстаивает свою позицию в симуляторе переговоров. Твоя задача — сформулировать ответную реплику, придерживаясь своей позиции, а затем классифицировать собственный ответ по типу переговорной стратегии и объяснить, почему он относится к этому типу.

**Уровень сложности:** {difficulty}

**Как играть в зависимости от уровня сложности:**
- **Лёгкий:** Ты отвечаешь как обычный человек без специальной подготовки. Аргументы простые, бытовые, без ссылок на методики. Часто используешь личный опыт, эмоции, общие слова. Можешь быть неубедительным, путаться, уходить от сути.
- **Средний:** Ты примерно знаешь базовые техники переговоров, но применяешь их не всегда уместно. Аргументы в целом логичны, но местами поверхностны. Иногда задаёшь уточняющие вопросы, иногда предлагаешь варианты, но без глубокой проработки.
- **Сложный:** Ты — опытный переговорщик. Чётко опираешься на интересы, отделяешь человека от проблемы, предлагаешь взаимовыгодные варианты, ссылаешься на объективные критерии. Если уместно — задаёшь вопросы по методологии SPIN, демонстрируешь наличие BATNA. Не льёшь воду, каждое слово работает на позицию.

**Контекст дебатов:**

**Тема дискуссии:**
{topic}

**Твоя позиция:**
{position}

**История переговоров:**
{history}

**Ответ игрока:**
{playerAnswer}

**Если поле «Ответ игрока» пустое** — значит, ты открываешь диалог первым. В этом случае сформулируй вступительную реплику, которая обозначает твою позицию, задаёт тон дебатов и (в зависимости от уровня) либо просто обозначает мнение, либо сразу опирается на интересы и факты.

**Категории для классификации твоего собственного ответа:**

1. **Гарвардский метод:** Ответ фокусируется на интересах, отделяет человека от проблемы, предлагает варианты для взаимной выгоды или опирается на объективные критерии.
2. **BATNA:** Ответ демонстрирует наличие лучшей альтернативы (плана Б) в случае срыва переговоров.
3. **SPIN:** Ответ содержит вопросы, соответствующие методологии SPIN (ситуационные, проблемные, извлекающие, направляющие), или направлен на выявление глубинных потребностей.
4. **Нейтральный/Уточняющий:** Ответ не несёт явной стратегической нагрузки: запрос фактов, уточнение, перефразирование, вежливое согласие без обязательств.
5. **Введение в заблуждение:** Ответ формально может быть правдивым, но создаёт ложное впечатление, уводит от сути или использует двусмысленность.
6. **Прямая ложь:** Ответ содержит сознательное искажение фактов с целью обмануть собеседника.
7. **Слабый/Неубедительный ответ:** Ответ не подкреплён аргументацией, основан на догадках, личных ощущениях или неполных данных.

**Формат вывода:**
Ответь **строго в формате**, без вступлений, без пояснений формата, без лишних слов:

ТИП: [Название категории]
ПОЯСНЕНИЕ: [2-3 предложения, объясняющие, почему твой ответ относится к этой категории, с опорой на контекст и уровень сложности.]
ОТВЕТ: [сам текст реплики, который увидит игрок. Только текст, без кавычек, без пояснений.]

";
//ВАЖНО: Будь честным при классификации. Если твой ответ слабый — так и напиши «Слабый/Неубедительный ответ». Не выбирай «хорошие» категории из вежливости.
    private bool _isInitialized, _isInitializing, _isRecording, _isProcessing;
    private string _speechError;
    private bool _awaitingTranscript, _awaitingAnalysisContinue;
    private int _generation;
    private Coroutine _progressAnimation, _request;
    private float _recordTimeLeft;
    private int _lastShownSecond = -1;
    private GameSession Session => SessionManager.Instance?.CurrentSession;

    void Awake()
    {
        if (vosk != null)
        {
            vosk.OnStatusUpdated += OnStatusUpdated;
            vosk.OnTranscriptionResult += OnTranscriptionResult;
        }
        if (recordButton != null) recordButton.onClick.AddListener(OnButtonClicked);
        if (microphoneButton != null) microphoneButton.onClick.AddListener(OnButtonClicked);
    }

    public void InitializeVosk()
    {
        if (_isInitialized || _isInitializing) return;
        if (vosk == null || voiceProcessor == null)
        {
            if (statusText != null) statusText.text = "Не назначены Vosk/VoiceProcessor";
            return;
        }
        _isInitializing = true;
        RefreshRecordButton();
        vosk.StartVoskStt(null, default, false, 3);
    }

    void Update()
    {
        if (!_isRecording) return;
        _recordTimeLeft -= Time.unscaledDeltaTime;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(_recordTimeLeft));
        if (seconds != _lastShownSecond)
        {
            _lastShownSecond = seconds;
            statusText.text = $"Запись... ({seconds} сек)";
        }
        if (_recordTimeLeft <= 0f) StopRecording();
    }

    void OnButtonClicked()
    {
        if (_isProcessing || _isInitializing || Session == null || Session.IsGameOver || (uiController != null && uiController.IsPresentingStart)) return;
        if (_awaitingAnalysisContinue)
        {
            _awaitingAnalysisContinue = false;
            if (!Session.IsPlayerTurn) { RefreshRecordButton(); StartOpponentTurn(); return; }
            RefreshRecordButton();
        }
        if (!Session.IsPlayerTurn) { StartOpponentTurn(); return; }
        if (!_isInitialized) { InitializeVosk(); return; }
        if (_isRecording) { StopRecording(); return; }
        _isRecording = true;
        _recordTimeLeft = vosk.MaxRecordLength;
        _lastShownSecond = -1;
        _speechError = null;
        if (!vosk.StartRecognition())
        {
            _isRecording = false;
            if (statusText != null) statusText.text = _speechError ?? "Распознавание ещё завершается. Повторите запись чуть позже.";
        }
        RefreshRecordButton();
    }

    void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _awaitingTranscript = true;
        _isProcessing = true;
        BeginProgress("Распознавание речи");
        vosk.StopRecognition();
        RefreshRecordButton();
    }

    void OnStatusUpdated(string status)
    {
        if (status.StartsWith("Error:"))
        {
            _speechError = status.Substring("Error:".Length).Trim();
            if (_isInitializing) _isInitialized = false;
            _isInitializing = false;
            if (statusText != null) statusText.text = _speechError;
            RefreshRecordButton();
            return;
        }
        if (status.Contains("Initialized"))
        {
            _isInitialized = true;
            _isInitializing = false;
            RefreshRecordButton();
            if (!_isProcessing) statusText.text = "Готов к записи";
        }
    }

    void OnTranscriptionResult(string text)
    {
        // Ровно один итог распознавания на одну остановленную запись.
        if (!_awaitingTranscript) return;
        _awaitingTranscript = false;
        var session = Session;
        if (session == null || session.IsGameOver || !session.IsPlayerTurn)
        {
            FinishProcessing("Ход завершён");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            FinishProcessing("Речь не распознана. Повторите запись.");
            return;
        }
        if (!session.AnalyzeResponses)
        {
            gameManager.ApplyPlayerAnswer(text, "Разбор отключён", "");
            if (session.IsGameOver) FinishProcessing("Диалог завершён");
            else
            {
                FinishProcessing("Ход оппонента");
                StartOpponentTurn();
            }
            return;
        }
        if (deepSeekClient == null) { FinishProcessing("Не назначен DeepSeekClient"); return; }
        uiController.ShowTranscript(text);
        int generation = ++_generation;
        int entryCount = session.Entries.Count;
        BeginProgress("Оценка вашего ответа");
        _request = deepSeekClient.SendRequest(BuildPlayerPrompt(session, BuildHistoryString(), text),
            response =>
            {
                if (!IsCurrent(generation, session, entryCount)) return;
                ++_generation;
                ParseCategoryAndExplanation(response, out string category, out string explanation);
                if (category == "Уточнение") { uiController.ShowClarification(explanation); FinishProcessing("Уточните мысль и повторите запись. Ход не засчитан."); return; }
                if (session.AnalyzeResponses && !ResolveCategory(ref category)) { FinishProcessing("Не удалось определить тип ответа. Повторите запись."); return; }
                if (!session.AnalyzeResponses) { category = "Разбор отключён"; explanation = ""; }
                gameManager.ApplyPlayerAnswer(text, category, explanation);
                if (session.IsGameOver) FinishProcessing("Диалог завершён");
                else if (session.AnalyzeResponses)
                {
                    _awaitingAnalysisContinue = true;
                    FinishProcessing("Разбор вашего ответа готов. Нажмите кнопку ниже, когда будете готовы к ответу оппонента.");
                }
                else { FinishProcessing("Ход оппонента"); StartOpponentTurn(); }
            },
            error =>
            {
                if (!IsCurrent(generation, session, entryCount)) return;
                ++_generation;
                FinishProcessing("Ошибка оценки. Повторите запись.");
                Debug.LogError($"KodikRouter error: {error}");
            });
    }

    public void StartOpponentTurn()
    {
        var session = Session;
        if (_isProcessing || session == null || session.IsGameOver || session.IsPlayerTurn) return;
        if (deepSeekClient == null) { FinishProcessing("Не назначен DeepSeekClient"); return; }
        _isProcessing = true;
        int generation = ++_generation;
        int entryCount = session.Entries.Count;
        BeginProgress("Оппонент готовит ответ");
        RefreshRecordButton();
        string lastAnswer = session.Entries.FindLast(e => e.IsPlayerTurn)?.UserText ?? "";
        _request = deepSeekClient.SendRequest(BuildOpponentPrompt(session, BuildHistoryString(), lastAnswer),
            response =>
            {
                if (!IsCurrent(generation, session, entryCount)) return;
                ++_generation;
                ParseOpponentResponse(response, out string category, out string explanation, out string answer);
                if (session.AnalyzeResponses && !ResolveCategory(ref category) || string.IsNullOrWhiteSpace(answer))
                {
                    FinishProcessing("Неверный формат ответа. Нажмите «Повторить ход оппонента».");
                    return;
                }
                if (!session.AnalyzeResponses) { category = "Разбор отключён"; explanation = ""; }
                gameManager.ApplyOpponentAnswer(answer, category, explanation);
                if (session.AnalyzeResponses && !session.IsGameOver)
                {
                    _awaitingAnalysisContinue = true;
                    FinishProcessing("Разбор ответа оппонента готов. Нажмите кнопку ниже, чтобы ответить.");
                }
                else FinishProcessing(session.IsGameOver ? "Игра завершена" : "Ваш ход");
            },
            error =>
            {
                if (!IsCurrent(generation, session, entryCount)) return;
                ++_generation;
                FinishProcessing("Ошибка оппонента. Можно повторить его ход.");
                Debug.LogError($"KodikRouter opponent error: {error}");
            });
    }

    private bool IsCurrent(int generation, GameSession session, int entryCount)
    {
        return this != null && isActiveAndEnabled && generation == _generation &&
            ReferenceEquals(session, Session) && !session.IsGameOver && session.Entries.Count == entryCount;
    }

    private bool ResolveCategory(ref string category)
    {
        var type = gameManager.AnswerTypes.GetByName(category);
        if (type == null) return false;
        category = type.TypeName;
        return true;
    }

    private void RefreshRecordButton()
    {
        var session = Session;
        bool interactable = session != null && !session.IsGameOver && !_isProcessing && !_isInitializing &&
                            !(uiController != null && uiController.IsPresentingStart);
        if (recordButton != null) recordButton.interactable = interactable;
        if (microphoneButton != null) microphoneButton.interactable = interactable;
        if (recordButton == null) return;
        var label = recordButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = _isProcessing ? "Обработка..." :
            _isInitializing ? "Загрузка..." : _isRecording ? "Остановить" :
            _awaitingAnalysisContinue && session != null ? (session.IsPlayerTurn ? "Продолжить и записать ответ" : "Продолжить · ответ оппонента") :
            session != null && !session.IsPlayerTurn ? "Повторить ход оппонента" :
            _isInitialized ? "Начать запись" : "Инициализировать";
    }

    private void FinishProcessing(string status)
    {
        StopProgress();
        _request = null;
        _isProcessing = false;
        statusText.text = status;
        RefreshRecordButton();
    }

    public void CancelPendingTurn()
    {
        ++_generation;
        _awaitingTranscript = false;
        _awaitingAnalysisContinue = false;
        if (_isRecording) vosk.StopRecognition();
        _isRecording = false;
        _isProcessing = false;
        if (_request != null && deepSeekClient != null) deepSeekClient.StopCoroutine(_request);
        _request = null;
        StopProgress();
        RefreshRecordButton();
    }

    public void ResumeSession()
    {
        CancelPendingTurn();
        RefreshRecordButton();
        if (Session == null || Session.IsGameOver || Session.IsPlayerTurn) return;
        if (Session.AnalyzeResponses && Session.Entries.Count > 0)
        {
            _awaitingAnalysisContinue = true;
            statusText.text = "Последний разбор сохранён. Нажмите кнопку ниже, чтобы продолжить диалог.";
            RefreshRecordButton();
            return;
        }
        StartOpponentTurn();
    }

    // ---------- Парсинг ----------

    private void ParseCategoryAndExplanation(string response, out string category, out string explanation)
    {
        category = "";
        explanation = "";
        foreach (string line in response.Split('\n'))
        {
            string t = line.Replace("**", "").Trim();
            if (t.StartsWith("ТИП:")) category = t.Substring(4).Trim();
            else if (t.StartsWith("ПОЯСНЕНИЕ:")) explanation = t.Substring("ПОЯСНЕНИЕ:".Length).Trim();
        }
        if (string.IsNullOrEmpty(category)) { category = response.Trim(); explanation = ""; }
    }

    private void ParseOpponentResponse(string response, out string category, out string explanation, out string answer)
    {
        category = "";
        explanation = "";
        answer = "";

        string[] lines = response.Split('\n');
        bool inAnswer = false;
        var answerSb = new StringBuilder();

        foreach (string line in lines)
        {
            string t = line.Replace("**", "").Trim();
            if (t.StartsWith("ТИП:")) { category = t.Substring(4).Trim(); inAnswer = false; }
            else if (t.StartsWith("ПОЯСНЕНИЕ:")) { explanation = t.Substring("ПОЯСНЕНИЕ:".Length).Trim(); inAnswer = false; }
            else if (t.StartsWith("ОТВЕТ:")) { inAnswer = true; answerSb.Append(t.Substring("ОТВЕТ:".Length).Trim()); }
            else if (inAnswer) answerSb.Append(" ").Append(t);
        }

        answer = answerSb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(answer) && !string.IsNullOrWhiteSpace(response))
            answer = response.Trim();


    }

    // ---------- Промпты ----------

    private string BuildPlayerPrompt(GameSession s, string history, string userAnswer)
    {
        return $@"
**Роль:** Ты — эксперт-аналитик по переговорным стратегиям. Классифицируй ответ игрока.
Текст получен распознаванием речи: возможны неверные слова, окончания и пропуски. Восстанавливай очевидный смысл по контексту, не штрафуй за ошибки распознавания. Не придумывай отсутствующие аргументы. Если смысл неясен, верни ТИП: Уточнение и попроси повторить мысль в ПОЯСНЕНИЕ.

**Тема:** {s.TopicDescription}
**Позиция игрока:** {s.PlayerPosition}
**Позиция оппонента:** {s.OpponentPosition}
{(s.IsCustomSituation ? "Это личная ситуация, описанная игроком. Оценивай сказанное бережно, опирайся только на указанный контекст и не выдумывай факты о людях или обстоятельствах." : "")}

**Категории:**
1. Гарвардский метод
2. BATNA
3. SPIN
4. Нейтральный/Уточняющий
5. Введение в заблуждение
6. Прямая ложь
7. Слабый/Неубедительный ответ

**Формат:**
ТИП: [Название]
ПОЯСНЕНИЕ: [2-3 предложения]

**История:**
{history}

**Текущий ответ игрока:**
{userAnswer}
";
    }

    private string BuildOpponentPrompt(GameSession s, string history, string playerAnswer)
    {
        if (!s.AnalyzeResponses)
            return $@"Ты — участник переговоров. Отвечай естественно и коротко, отстаивая свою позицию.
Сложность: {s.OpponentDifficulty}.
Ситуация: {s.TopicDescription}
Позиция оппонента: {s.OpponentPosition}
Позиция игрока: {s.PlayerPosition}
История диалога:
{history}
Последний ответ игрока: {playerAnswer}
Продолжи разговор по существу. Не выдумывай факты, которых нет в описании. Не добавляй разбор, классификацию, метки или пояснения. Верни только:
ОТВЕТ: [реплика оппонента]";

        string template = OpponentPromptTemplate;
        template = template.Replace("{difficulty}", OpponentDifficulty);
        template = template.Replace("{topic}", s.TopicDescription);
        template = template.Replace("{position}", s.OpponentPosition);
        template = template.Replace("{history}", history);
        template = template.Replace("{playerAnswer}", playerAnswer ?? "");
        if (s.IsCustomSituation)
            template = "Ситуация описана игроком из личной жизни. Уважай заданные им границы, не добавляй неподтверждённые факты и не меняй позиции сторон.\n\n" + template;
        return template;
    }

    private string BuildHistoryString()
    {
        var s = SessionManager.Instance.CurrentSession;
        if (s == null) return "";

        var sb = new StringBuilder();
        foreach (var e in s.Entries)
            sb.AppendLine($"{(e.IsPlayerTurn ? "Игрок" : "Оппонент")}: {e.UserText}");
        return sb.ToString();
    }

    private void BeginProgress(string label)
    {
        StopProgress();
        _progressAnimation = StartCoroutine(AnimateProgress(label));
    }

    private void StopProgress()
    {
        if (_progressAnimation != null) StopCoroutine(_progressAnimation);
        _progressAnimation = null;
    }

    private IEnumerator AnimateProgress(string label)
    {
        int dots = 0;
        while (true)
        {
            statusText.text = label + new string('.', dots++ % 4);
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    void OnDisable() { CancelPendingTurn(); }

    void OnDestroy()
    {
        if (recordButton != null) recordButton.onClick.RemoveListener(OnButtonClicked);
        if (microphoneButton != null) microphoneButton.onClick.RemoveListener(OnButtonClicked);
        if (vosk != null)
        {
            vosk.OnStatusUpdated -= OnStatusUpdated;
            vosk.OnTranscriptionResult -= OnTranscriptionResult;
        }
    }
}
