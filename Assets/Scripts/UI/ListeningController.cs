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

        if (AllTasks == null || AllTasks.Length == 0)
        {
            Debug.LogError("AllTasks пустой!");
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

        // UI
        QuestionPanel.SetActive(false);
        AnswerPanel.SetActive(false);
        ResultPanel.SetActive(false);

        AnswerButton.interactable = false;
        AnswerButton.onClick.AddListener(OpenAnswerPanel);

        BackButton.onClick.AddListener(() => SceneManager.LoadScene("MenuScene"));

        PlayPauseButton.onClick.AddListener(TogglePlay);
        RewindButton.onClick.AddListener(() => Seek(-10f));
        ForwardButton.onClick.AddListener(() => Seek(10f));

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
        BuildMethodologyToggles();

        PlayAudio();
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

        PlayPauseText.text = "Пауза";
    }

    void TogglePlay()
    {
        if (_audioFinished) return;

        if (_isPaused)
        {
            AudioSource.UnPause();
            _isPaused = false;
            _isPlaying = true;
            PlayPauseText.text = "Пауза";
        }
        else
        {
            AudioSource.Pause();
            _isPaused = true;
            _isPlaying = false;
            PlayPauseText.text = "Играть";
        }
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

            PlayPauseText.text = "Играть";
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

        PlayPauseText.text = "Играть";

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
        foreach (Transform child in MethodologyGroup.transform)
            Destroy(child.gameObject);

        if (_currentTask.AvailableMethodologies == null) return;

        if (MethodologyTogglePrefab == null)
        {
            Debug.LogError("MethodologyTogglePrefab не назначен!");
            return;
        }

        List<string> methods = new List<string>(_currentTask.AvailableMethodologies);
        ShuffleList(methods);

        Toggle firstToggle = null;

        foreach (string method in methods)
        {
            var go = Instantiate(MethodologyTogglePrefab, MethodologyGroup.transform);
            go.name = method;

            var toggle = go.GetComponent<Toggle>();
            var label = go.GetComponentInChildren<TMP_Text>();

            if (label != null) label.text = method;
            if (toggle != null)
            {
                toggle.group = MethodologyGroup;
                toggle.isOn = false;

                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) OnToggleGroupChanged();
                });

                if (firstToggle == null)
                    firstToggle = toggle;
            }
        }

        if (firstToggle != null)
        {
            firstToggle.SetIsOnWithoutNotify(true);
            OnToggleGroupChanged();
        }
    }

    void OpenAnswerPanel()
    {
        AnswerPanel.SetActive(true);
    }

    void OnToggleGroupChanged()
    {
        bool anyOn = false;
        foreach (var t in MethodologyGroup.GetComponentsInChildren<Toggle>())
        {
            if (t.isOn) { anyOn = true; break; }
        }
        SubmitButton.interactable = anyOn;
    }

    void OnReplayFromAnswerPanel()
    {
        AnswerPanel.SetActive(false);
        PlayAudio();
    }

    void OnSubmit()
    {
        string selected = "";
        foreach (var t in MethodologyGroup.GetComponentsInChildren<Toggle>())
        {
            if (t.isOn) { selected = t.name; break; }
        }

        if (string.IsNullOrEmpty(selected)) return;

        AnswerPanel.SetActive(false);
        ResultPanel.SetActive(true);

        bool correct = selected == _currentTask.CorrectMethodology;

        _wasNewTask = !_activeGoal.IsTaskSolved(_currentTask.TaskId);

        ResultText.text = correct
            ? $"✅ Верно! {_currentTask.CorrectMethodology}"
            : $"❌ Неверно. Правильный ответ: {_currentTask.CorrectMethodology}";

        ExplanationText.text = _currentTask.CorrectExplanation;

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