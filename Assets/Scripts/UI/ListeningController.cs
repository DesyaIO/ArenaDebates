using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ListeningController : MonoBehaviour
{
    [Header("Плеер")]
    public AudioSource AudioSource;
    public Slider ProgressSlider;
    public TMP_Text TimeText;
    public Button PlayPauseButton;
    public Image PlayIcon;
    public Image PauseIcon;
    public TMP_Text PlayPauseText;
    public Button RewindButton;
    public Button ForwardButton;

    [Header("Кнопка ответа")]
    public Button AnswerButton;
    public Button BackButton;

    [Header("Панели")]
    public GameObject QuestionPanel;
    public Button QuestionReplayButton;
    public Button QuestionReadyButton;

    public GameObject AnswerPanel;
    public ToggleGroup MethodologyGroup;
    public TMP_InputField MethodInput;
    public TMP_InputField ArgumentInput;
    public Button SubmitButton;
    public Button AnswerReplayButton;

    [Header("Панель результата")]
    public GameObject ResultPanel;
    public TMP_Text ResultText;
    public TMP_Text ExplanationText;

    public Button NextButton;
    public Button RetryButton;
    public Button MenuButton;

    [Header("Префаб радиобаттона")]
    public GameObject MethodologyTogglePrefab;

    [Header("Контент")]
    public ListeningTaskSO[] AllTasks;

    private ListeningTaskSO _currentTask;
    private GoalProgress _activeGoal;

    private bool _isPlaying;
    private bool _audioFinished;
    private bool _askedAboutReplay;
    private bool _isPaused;

    private bool _wasNewTask;

    void Start()
    {
        ResolveSceneReferences();

        var user = UserManager.CurrentUser;
        if (user == null)
        {
            SceneManager.LoadScene("MenuScene");
            return;
        }

        _activeGoal = user.GetActiveGoal();
        if (_activeGoal == null || _activeGoal.LearningMethods == null || _activeGoal.LearningMethods.Count == 0)
        {
            SceneManager.LoadScene("MenuScene");
            return;
        }

        foreach (var method in _activeGoal.LearningMethods)
        {
            if (!PlayerProgress.IsLearningCompleted(method))
            {
                SceneManager.LoadScene("MenuScene");
                return;
            }
        }

        LoadTasksIfMissing();
        if (AllTasks == null || AllTasks.Length == 0)
        {
            Debug.LogError("ListeningScene: AllTasks пустой, а Resources/ListetingTasks не содержит ListeningTaskSO.", this);
            return;
        }

        _currentTask = PickTask();

        if (_currentTask == null)
        {
            Debug.LogWarning("Не удалось подобрать задачу — возврат в меню.");
            SceneManager.LoadScene("MenuScene");
            return;
        }

        if (_currentTask.AudioClip == null)
        {
            Debug.LogError($"У задачи '{_currentTask.name}' нет AudioClip!");
            return;
        }

        if (!ValidateRequiredReferences()) return;

        // UI
        QuestionPanel.SetActive(false);
        AnswerPanel.SetActive(false);
        ResultPanel.SetActive(false);

        AnswerButton.interactable = false;
        AnswerButton.onClick.AddListener(OpenAnswerPanel);

        BackButton.onClick.AddListener(() => SceneManager.LoadScene("MenuScene"));

        PlayPauseButton.onClick.AddListener(TogglePlay);
        if (RewindButton != null) RewindButton.onClick.AddListener(() => Seek(-10f));
        if (ForwardButton != null) ForwardButton.onClick.AddListener(() => Seek(10f));

        QuestionReplayButton.onClick.AddListener(OnReplayClicked);
        QuestionReadyButton.onClick.AddListener(OnReadyClicked);
        AnswerReplayButton.onClick.AddListener(OnReplayFromAnswerPanel);

        SubmitButton.onClick.AddListener(OnSubmit);

        NextButton.onClick.AddListener(OnNext);
        RetryButton.onClick.AddListener(OnRetry);
        MenuButton.onClick.AddListener(() => SceneManager.LoadScene("MenuScene"));

        NextButton.gameObject.SetActive(false);
        RetryButton.gameObject.SetActive(false);

        SubmitButton.interactable = false;
        MethodInput.onValueChanged.AddListener(_ => OnMethodInputChanged());
        ArgumentInput.onValueChanged.AddListener(_ => UpdateSubmitState());
        BuildMethodologyToggles();

        PlayAudio();
    }

    private void ResolveSceneReferences()
    {
        GameObject canvasObject = GameObject.Find("ArenaCanvas");
        Transform design = canvasObject != null
            ? canvasObject.transform.Find("Design378x740")
            : null;

        if (AudioSource == null)
        {
            GameObject sourceObject = GameObject.Find("AudioSource");
            if (sourceObject != null) AudioSource = sourceObject.GetComponent<AudioSource>();
        }

        if (design == null) return;

        if (ProgressSlider == null) ProgressSlider = GetComponentAt<Slider>(design, "Progress");
        if (TimeText == null) TimeText = GetComponentAt<TMP_Text>(design, "Time");
        if (PlayPauseButton == null) PlayPauseButton = GetComponentAt<Button>(design, "PlayPause");
        if (PlayIcon == null) PlayIcon = GetComponentAt<Image>(design, "PlayPause/PlayIcon");
        if (PauseIcon == null) PauseIcon = GetComponentAt<Image>(design, "PlayPause/PauseIcon");
        if (RewindButton == null) RewindButton = GetComponentAt<Button>(design, "Rewind");
        if (ForwardButton == null) ForwardButton = GetComponentAt<Button>(design, "Forward");
        if (AnswerButton == null) AnswerButton = GetComponentAt<Button>(design, "Ready");
        if (BackButton == null) BackButton = GetComponentAt<Button>(design, "Back");

        if (QuestionPanel == null) QuestionPanel = GetObjectAt(design, "ReplayQuestion");
        if (QuestionReplayButton == null) QuestionReplayButton = GetComponentAt<Button>(design, "ReplayQuestion/Replay");
        if (QuestionReadyButton == null) QuestionReadyButton = GetComponentAt<Button>(design, "ReplayQuestion/Ready");

        if (AnswerPanel == null) AnswerPanel = GetObjectAt(design, "Answer");
        if (MethodologyGroup == null)
        {
            Transform methods = design.Find("Answer/Methods");
            MethodologyGroup = methods != null
                ? methods.GetComponentInChildren<ToggleGroup>(true)
                : null;
        }
        if (MethodInput == null) MethodInput = GetComponentAt<TMP_InputField>(design, "Answer/MethodInput");
        if (ArgumentInput == null) ArgumentInput = GetComponentAt<TMP_InputField>(design, "Answer/Argument");
        if (SubmitButton == null) SubmitButton = GetComponentAt<Button>(design, "Answer/Submit");
        if (AnswerReplayButton == null) AnswerReplayButton = GetComponentAt<Button>(design, "Answer/ListenAgain");

        if (ResultPanel == null) ResultPanel = GetObjectAt(design, "Result");
        if (ResultText == null) ResultText = GetComponentAt<TMP_Text>(design, "Result/ResultTitle");
        if (ExplanationText == null)
            ExplanationText = GetComponentAt<TMP_Text>(design, "Result/Explanation/Content/ExplanationText");
        if (NextButton == null) NextButton = GetComponentAt<Button>(design, "Result/Next");
        if (RetryButton == null) RetryButton = GetComponentAt<Button>(design, "Result/Retry");
        if (MenuButton == null) MenuButton = GetComponentAt<Button>(design, "Result/Menu");
    }

    private void LoadTasksIfMissing()
    {
        AllTasks = (AllTasks ?? new ListeningTaskSO[0]).Where(task => task != null).ToArray();
        if (AllTasks.Length > 0) return;

        AllTasks = Resources.LoadAll<ListeningTaskSO>("ListetingTasks");
        if (AllTasks.Length == 0)
            AllTasks = Resources.LoadAll<ListeningTaskSO>("ListeningTasks");

        AllTasks = (AllTasks ?? new ListeningTaskSO[0]).Where(task => task != null).ToArray();
        Debug.Log($"ListeningScene: загружено задач из Resources: {AllTasks.Length}.", this);
    }

    private bool ValidateRequiredReferences()
    {
        var missing = new List<string>();
        if (AudioSource == null) missing.Add(nameof(AudioSource));
        if (AnswerButton == null) missing.Add(nameof(AnswerButton));
        if (QuestionPanel == null) missing.Add(nameof(QuestionPanel));
        if (QuestionReplayButton == null) missing.Add(nameof(QuestionReplayButton));
        if (QuestionReadyButton == null) missing.Add(nameof(QuestionReadyButton));
        if (AnswerPanel == null) missing.Add(nameof(AnswerPanel));
        if (MethodologyGroup == null) missing.Add(nameof(MethodologyGroup));
        if (MethodInput == null) missing.Add(nameof(MethodInput));
        if (ArgumentInput == null) missing.Add(nameof(ArgumentInput));
        if (SubmitButton == null) missing.Add(nameof(SubmitButton));
        if (AnswerReplayButton == null) missing.Add(nameof(AnswerReplayButton));
        if (ResultPanel == null) missing.Add(nameof(ResultPanel));
        if (NextButton == null) missing.Add(nameof(NextButton));
        if (RetryButton == null) missing.Add(nameof(RetryButton));
        if (MenuButton == null) missing.Add(nameof(MenuButton));
        if (MethodologyTogglePrefab == null) missing.Add(nameof(MethodologyTogglePrefab));

        if (missing.Count == 0) return true;

        Debug.LogError("ListeningScene: не назначены обязательные ссылки: " + string.Join(", ", missing), this);
        return false;
    }

    private static GameObject GetObjectAt(Transform root, string path)
    {
        Transform item = root != null ? root.Find(path) : null;
        return item != null ? item.gameObject : null;
    }

    private static T GetComponentAt<T>(Transform root, string path) where T : Component
    {
        Transform item = root != null ? root.Find(path) : null;
        return item != null ? item.GetComponent<T>() : null;
    }

    // ---------- Логика выбора задачи ----------

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    ListeningTaskSO PickTask()
    {
        var relevantTasks = AllTasks
            .Where(t => t != null && _activeGoal.LearningMethods.Contains(t.CorrectMethodology))
            .ToList();

        if (relevantTasks.Count == 0)
        {
            Debug.LogWarning("Нет задач, подходящих под методы цели.");
            return null;
        }

        var unsolvedTasks = relevantTasks
            .Where(t => !_activeGoal.IsTaskSolved(t.TaskId))
            .ToList();

        if (unsolvedTasks.Count > 0)
        {
            foreach (var method in _activeGoal.LearningMethods)
            {
                var tasksForMethod = unsolvedTasks
                    .Where(t => t.CorrectMethodology == method)
                    .ToList();

                if (tasksForMethod.Count > 0)
                {
                    return tasksForMethod[Random.Range(0, tasksForMethod.Count)];
                }
            }

            return unsolvedTasks[Random.Range(0, unsolvedTasks.Count)];
        }

        Debug.Log("Все задачи по методам цели пройдены. Выбирается случайная из пройденных.");
        return relevantTasks[Random.Range(0, relevantTasks.Count)];
    }

    // ---------- Воспроизведение ----------

    void PlayAudio()
    {
        AudioSource.clip = _currentTask.AudioClip;
        AudioSource.time = 0;
        AudioSource.Play();

        _isPlaying = true;
        _isPaused = false;
        _audioFinished = false;

        SetPlaybackIcon(true);
    }

    void TogglePlay()
    {
        if (_audioFinished) return;

        if (_isPaused)
        {
            AudioSource.UnPause();
            _isPaused = false;
            _isPlaying = true;
            SetPlaybackIcon(true);
        }
        else
        {
            AudioSource.Pause();
            _isPaused = true;
            _isPlaying = false;
            SetPlaybackIcon(false);
        }
    }

    void SetPlaybackIcon(bool playing)
    {
        if (PlayIcon != null) PlayIcon.gameObject.SetActive(!playing);
        if (PauseIcon != null) PauseIcon.gameObject.SetActive(playing);
        if (PlayPauseText != null) PlayPauseText.text = playing ? "Пауза" : "Играть";
    }

    void Seek(float seconds)
    {
        if (AudioSource.clip == null) return;
        if (_audioFinished) return;

        float newTime = AudioSource.time + seconds;

        // Если перематываем вперёд и до конца осталось меньше шага —
        // считаем, что аудио закончилось: пауза, начало, QuestionPanel
        if (seconds > 0f && newTime >= AudioSource.clip.length)
        {
            AudioSource.Pause();
            AudioSource.time = 0;

            _isPlaying = false;
            _isPaused = false;

            SetPlaybackIcon(false);
            UpdateProgressUI();

            OnAudioFinished();
            return;
        }

        // Обычная перемотка
        newTime = Mathf.Clamp(newTime, 0f, AudioSource.clip.length);
        AudioSource.time = newTime;

        UpdateProgressUI();
    }

    void Update()
    {
        UpdateProgressUI();

        if (_isPlaying && !AudioSource.isPlaying)
        {
            OnAudioFinished();
        }
    }

    void UpdateProgressUI()
    {
        if (AudioSource.clip == null) return;

        float progress = AudioSource.time / AudioSource.clip.length;
        if (ProgressSlider != null) ProgressSlider.value = progress;

        if (TimeText != null)
        {
            TimeText.text = $"{FormatTime(AudioSource.time)} / {FormatTime(AudioSource.clip.length)}";
        }
    }

    string FormatTime(float t)
    {
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    // ---------- Конец аудио ----------

    void OnAudioFinished()
    {
        if (_audioFinished) return;
        _audioFinished = true;
        _isPlaying = false;
        _isPaused = false;

        SetPlaybackIcon(false);

        if (!_askedAboutReplay)
        {
            _askedAboutReplay = true;
            QuestionPanel.SetActive(true);
        }
        else
        {
            UnlockAnswer();
        }
    }

    void UnlockAnswer()
    {
        AnswerButton.interactable = true;
    }

    // ---------- Панель вопроса ----------

    void OnReplayClicked()
    {
        QuestionPanel.SetActive(false);
        PlayAudio();
    }

    void OnReadyClicked()
    {
        QuestionPanel.SetActive(false);
        UnlockAnswer();
        OpenAnswerPanel();
    }

    // ---------- Панель ответа ----------

    void BuildMethodologyToggles()
    {
        Transform toggleContainer = MethodologyGroup.transform;
        ScrollRect methodsScroll = MethodologyGroup.GetComponentInParent<ScrollRect>();
        VisibleScrollbar.Ensure(methodsScroll);
        if (methodsScroll != null && methodsScroll.content != null)
        {
            toggleContainer = methodsScroll.content;
        }
        else
        {
            Transform content = MethodologyGroup.transform.Find("Content");
            if (content != null) toggleContainer = content;
        }

        // Keep the ScrollRect, layout and decorative children intact; only clear
        // previously generated method choices.
        for (int i = toggleContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = toggleContainer.GetChild(i);
            if (child.GetComponent<Toggle>() != null)
                Destroy(child.gameObject);
        }

        if (_currentTask.AvailableMethodologies == null || _currentTask.AvailableMethodologies.Length == 0)
        {
            Debug.LogError($"У задачи '{_currentTask.TaskId}' не заданы варианты методов.", this);
            SubmitButton.interactable = false;
            return;
        }

        if (MethodologyTogglePrefab == null)
        {
            Debug.LogError("MethodologyTogglePrefab не назначен!");
            return;
        }

        List<string> methods = new List<string>(_currentTask.AvailableMethodologies);
        MethodologyGroup.allowSwitchOff = true;
        MethodInput.SetTextWithoutNotify(string.Empty);

        foreach (string method in methods)
        {
            var go = Instantiate(MethodologyTogglePrefab, toggleContainer);
            go.name = method;

            var toggle = go.GetComponent<Toggle>();
            var label = go.GetComponentInChildren<TMP_Text>();

            if (toggle == null)
            {
                Debug.LogError("В MethodologyTogglePrefab отсутствует компонент Toggle.", go);
                Destroy(go);
                continue;
            }

            if (label != null)
            {
                label.text = method;
                label.fontSize = 14;
                label.alignment = TextAlignmentOptions.Center;
            }

            float width = label != null
                ? Mathf.Clamp(Mathf.Ceil(label.GetPreferredValues(method).x) + 28f, 70f, 245f)
                : 110f;
            var rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(width, 50f);
            var layout = go.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = width;
                layout.preferredHeight = 50f;
            }
            Transform fill = go.transform.Find("FrostedFill");
            if (fill is RectTransform fillRect)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.one;
                fillRect.offsetMax = -Vector2.one;
            }
            if (label != null && label.rectTransform != null)
            {
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 0f);
                labelRect.offsetMax = new Vector2(-8f, 0f);
            }
            toggle.group = MethodologyGroup;
            toggle.isOn = false;

            toggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                MethodInput.SetTextWithoutNotify(method);
                UpdateSubmitState();
            });
        }

        UpdateSubmitState();
    }

    void OpenAnswerPanel()
    {
        AnswerPanel.SetActive(true);
    }

    void OnMethodInputChanged()
    {
        MethodologyGroup.SetAllTogglesOff(false);
        UpdateSubmitState();
    }

    void UpdateSubmitState()
    {
        SubmitButton.interactable = !string.IsNullOrWhiteSpace(MethodInput.text)
            && !string.IsNullOrWhiteSpace(ArgumentInput.text);
    }

    void OnReplayFromAnswerPanel()
    {
        AnswerPanel.SetActive(false);
        PlayAudio();
    }

    void OnSubmit()
    {
        string selected = MethodInput.text.Trim();

        if (string.IsNullOrEmpty(selected) || string.IsNullOrWhiteSpace(ArgumentInput.text)) return;

        AnswerPanel.SetActive(false);
        ResultPanel.SetActive(true);

        bool correct = string.Equals(selected, _currentTask.CorrectMethodology, System.StringComparison.OrdinalIgnoreCase);

        _wasNewTask = !_activeGoal.IsTaskSolved(_currentTask.TaskId);

        if (ResultText != null)
        {
            ResultText.text = correct
                ? $"Верно! {_currentTask.CorrectMethodology}"
                : $"Неверно. Правильный ответ: {_currentTask.CorrectMethodology}";
        }

        if (ExplanationText != null) ExplanationText.text = _currentTask.CorrectExplanation;

        if (correct)
        {
            NextButton.gameObject.SetActive(true);
            RetryButton.gameObject.SetActive(false);

            if (_wasNewTask)
            {
                _activeGoal.MarkTaskSolved(_currentTask.TaskId);
                PlayerProgress.ListeningSolved++;
                UserManager.SaveCurrentUser();
                Debug.Log($"Новая задача '{_currentTask.TaskId}' засчитана. Прогресс: {PlayerProgress.ListeningSolved} / {PlayerProgress.TotalListeningTasks}");
            }
            else
            {
                Debug.Log($"Задача '{_currentTask.TaskId}' уже была пройдена — прогресс не увеличивается.");
            }
        }
        else
        {
            NextButton.gameObject.SetActive(false);
            RetryButton.gameObject.SetActive(true);
            Debug.Log("Неверный ответ. Прогресс не засчитан.");
        }

        MenuButton.interactable = true;
    }

    // ---------- Кнопки ResultPanel ----------

    void OnNext()
    {
        if (PlayerProgress.ListeningSolved >= PlayerProgress.TotalListeningTasks)
        {
            SceneManager.LoadScene("MenuScene");
        }
        else
        {
            SceneManager.LoadScene("ListeningScene");
        }
    }

    void OnRetry()
    {
        SceneManager.LoadScene("ListeningScene");
    }
}
