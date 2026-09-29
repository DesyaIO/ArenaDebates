using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SceneParams
{
    public const string FreeDialogueVictoryKey = "free_dialogue_first_victory";
    public const string FreeDialogueVictoryTitle = "ЭПИЛОГ / ПОБЕДА";
    public const string FreeDialogueVictorySubtitle = "СВОБОДНЫЙ ДИАЛОГ · ВОЗВРАЩЕНИЕ В МЕНЮ";
    public const string FreeDialogueVictoryText = "Это был тяжелый путь, но я справился! Я превзошел по силе сверхчеловека и стал для этих машин лидером. Теперь я могу вернуть всё на свои места. Власть снова будет принадлежать людям. Впереди меня ждет много работы, но я уже владею всеми нужными мне навыками. И точно знаю, что я могу всё!";
    public const string FreeDialogueLossTitle = "ЗАПИСЬ / СВОБОДНЫЙ ДИАЛОГ";
    public const string FreeDialogueLossSubtitle = "ПОРАЖЕНИЕ · НУЖНО ПРОДОЛЖАТЬ ТРЕНИРОВАТЬСЯ";
    public const string FreeDialogueLossText = "Мне нужно еще много тренироваться, чтобы превзойти «ума». Но я точно знаю, что победа будет за мной!";

    public static string SelectedLearningMethod = "";
    public static bool OpenLearningPanelOnMenu;
    public static bool ShowPrologueOnMenu;
    public static string PendingMenuStoryKey;
    public static string PendingMenuStoryTitle;
    public static string PendingMenuStorySubtitle;
    public static string PendingMenuStoryText;
}

public class MenuController : MonoBehaviour
{
    [Header("Кнопки")]
    public Button LearningButton;
    public Button ListeningButton;
    public Button FreeDialogueButton;
    public Button GoalButton;
    public Button LogoutButton;
    public Button ArchiveNavigationButton, ArchiveCloseButton, ArchiveListeningButton;
    public TMP_Text ArchiveProgressText, MapProgressText;
    public GameObject ProloguePanel;

    [Header("UI статуса")]
    public TMP_Text ListeningStatusText;
    public TMP_Text LearningStatusText;
    public TMP_Text FreeDialogueStatusText;
    public TMP_Text GoalStatusText;

    [Header("Панель обучения")]
    public GameObject LearningPanel;
    public Transform LearningListContainer;
    public GameObject LearningButtonPrefab;

    [Header("Панель аудирования")]
    public GameObject ListeningPanel;
    public TMP_Text ListeningPanelProgressText;
    public Button ListeningStartButton;
    public Button ListeningCloseButton;

    [Header("Панель «Диалоги»")]
    public GameObject DialoguePanel;
    public Button LearningDialogButton;    // "Учебный диалог"
    public Button FreeDialogButton;        // "Свободный диалог"
    public Button DialogueCloseButton;     // "Назад"

    [Header("Контент")]
    public LearningContentSO[] AllLearningContent;

    private MenuNarrativeOverlay _narrativeOverlay;

    void Start()
    {
        if (UserManager.CurrentUser == null)
        {
            SceneManager.LoadScene("LoginScene");
            return;
        }

        if (!UserManager.CurrentUser.HasActiveGoal())
        {
            SceneManager.LoadScene("GoalSetupScene");
            return;
        }

        AddListener(LearningButton, OpenLearningPanel);
        AddListener(ListeningButton, OpenListeningPanel);
        AddListener(FreeDialogueButton, OpenDialoguePanel);
        AddListener(GoalButton, OpenGoalSetup);
        AddListener(LogoutButton, OnLogout);
        AddListener(ArchiveNavigationButton, OpenLearningPanel);
        AddListener(ArchiveCloseButton, CloseLearningPanel);
        AddListener(ArchiveListeningButton, OpenListeningPanel);

        AddListener(ListeningStartButton, StartListening);
        AddListener(ListeningCloseButton, CloseListeningPanel);

        // Панель диалогов
        AddListener(LearningDialogButton, StartLearningDialogue);
        AddListener(FreeDialogButton, () => DialogueOptions.Show(
            FreeDialogButton.GetComponentInChildren<TMP_Text>().font,
            () => SceneManager.LoadScene("DialogScene")));
        AddListener(DialogueCloseButton, CloseDialoguePanel);

        // Прячем панели
        if (LearningPanel != null) LearningPanel.SetActive(false);
        if (ListeningPanel != null) ListeningPanel.SetActive(false);
        if (DialoguePanel != null) DialoguePanel.SetActive(false);

        UpdateUI();
        if (ProloguePanel != null)
        {
            ProloguePanel.SetActive(SceneParams.ShowPrologueOnMenu);
            SceneParams.ShowPrologueOnMenu = false;
        }

        if (!string.IsNullOrWhiteSpace(SceneParams.PendingMenuStoryText))
        {
            string key = SceneParams.PendingMenuStoryKey;
            string title = SceneParams.PendingMenuStoryTitle;
            string subtitle = SceneParams.PendingMenuStorySubtitle;
            string text = SceneParams.PendingMenuStoryText;
            SceneParams.PendingMenuStoryKey = null;
            SceneParams.PendingMenuStoryTitle = null;
            SceneParams.PendingMenuStorySubtitle = null;
            SceneParams.PendingMenuStoryText = null;

            if (string.IsNullOrWhiteSpace(key) || !PlayerProgress.HasSeenNarrative(key))
                ShowNarrative(title, subtitle, text, key, OpenLearningPanelIfRequested);
            else
                OpenLearningPanelIfRequested();
        }
        else OpenLearningPanelIfRequested();
    }

    void UpdateUI()
    {
        var user = UserManager.CurrentUser;
        var activeGoal = user?.GetActiveGoal();

        List<LearningContentSO> filteredContent = GetFilteredContent(activeGoal);
        int totalMethods = filteredContent.Count;

        if (LearningButton != null) LearningButton.interactable = true;

        bool allLearningDone = totalMethods > 0 && AllMethodsCompleted(filteredContent);
        bool listeningUnlocked = allLearningDone;
        if (ListeningButton != null) ListeningButton.interactable = listeningUnlocked;

        if (ListeningStatusText != null)
            ListeningStatusText.text = listeningUnlocked ? PlayerProgress.ListeningProgressText() : "Пройдите обучение";

        int completed = 0;
        foreach (var content in filteredContent)
            if (PlayerProgress.IsLearningCompleted(content.MethodologyName))
                completed++;

        LearningStatusText.text = $"Изучено: {completed} / {totalMethods}";
        if (ArchiveProgressText != null) ArchiveProgressText.text = $"Прочитано {completed} из {totalMethods} материалов";
        if (ArchiveListeningButton != null) ArchiveListeningButton.interactable = listeningUnlocked;
        if (MapProgressText != null) MapProgressText.text = $"Пройдено {(allLearningDone ? 1 : 0) + (PlayerProgress.IsAllListeningDone() ? 1 : 0)} из 3 модулей";

        bool allListeningDone = PlayerProgress.ListeningSolved >= PlayerProgress.TotalListeningTasks;
        bool freeDialogueUnlocked = listeningUnlocked && allListeningDone;
        // Оба режима диалога открываются только после полного обучения
        // и прохождения всех заданий аудирования.
        if (FreeDialogueButton != null) FreeDialogueButton.interactable = freeDialogueUnlocked;

        if (FreeDialogueStatusText != null)
        {
            FreeDialogueStatusText.text = freeDialogueUnlocked
                ? "Свободный и учебный режим"
                : "Открывается после обучения и аудирования";
        }

        if (GoalStatusText != null && activeGoal != null)
        {
            GoalStatusText.text = $"Цель: {activeGoal.Industry} — {activeGoal.GoalText}";
        }
    }

    List<LearningContentSO> GetFilteredContent(GoalProgress activeGoal)
    {
        var result = new List<LearningContentSO>();

        if (activeGoal == null || activeGoal.LearningMethods == null || activeGoal.LearningMethods.Count == 0)
            return result;

        foreach (var content in AllLearningContent)
        {
            if (activeGoal.LearningMethods.Contains(content.MethodologyName))
                result.Add(content);
        }

        return result;
    }

    bool AllMethodsCompleted(List<LearningContentSO> methods)
    {
        foreach (var m in methods)
            if (!PlayerProgress.IsLearningCompleted(m.MethodologyName))
                return false;
        return true;
    }

    // ---------- Выбор цели ----------

    void OpenGoalSetup()
    {
        SceneManager.LoadScene("GoalSetupScene");
    }

    // ---------- Выход ----------

    void OnLogout()
    {
        UserManager.Logout();
        SceneManager.LoadScene("LoginScene");
    }

    // ---------- Обучение ----------

    void OpenLearningPanel()
    {
        if (ShowNarrativeIfFirstTime(
            "module_1_first_open",
            "ЗАПИСЬ / 001",
            "МОДУЛЬ 01 · ТАЙНЫЙ АРХИВ",
            "Говорят, что «умы» освоили секретные техники и методики ведения переговоров. Они стали всесильны. Чтобы их подчинить своей власти, я должен стать сильнее и умнее их. Когда я добрался до тайных архивов, мне удалось найти засекреченные файлы, которые когда-то послужили основой для обучения сверхчеловека. Нужно их срочно изучить. Ведь именно я могу всё изменить!"))
            return;

        OpenLearningPanelContent();
    }

    private void OpenLearningPanelContent()
    {
        ShowPanel(LearningPanel);
        RefreshLearningList();
    }

    public void CloseLearningPanel() => LearningPanel.SetActive(false);

    void RefreshLearningList()
    {
        foreach (Transform child in LearningListContainer)
            Destroy(child.gameObject);

        var user = UserManager.CurrentUser;
        var activeGoal = user?.GetActiveGoal();
        List<LearningContentSO> filteredContent = GetFilteredContent(activeGoal);

        if (filteredContent.Count == 0)
        {
            Debug.LogWarning("Для текущей цели нет доступных методологий.");
            return;
        }

        int dossierIndex = 0;
        foreach (var content in filteredContent)
        {
            var go = Instantiate(LearningButtonPrefab, LearningListContainer);
            var btn = go.GetComponent<Button>();
            var text = go.GetComponentInChildren<TMP_Text>();

            if (LearningStatusText != null) text.font = LearningStatusText.font;
            bool completed = PlayerProgress.IsLearningCompleted(content.MethodologyName);

            text.text = completed
                ? $"{content.MethodologyName} ✅"
                : content.MethodologyName;
            var card = go.GetComponent<ArenaArchiveCard>();
            if (card != null) card.Bind(content.MethodologyName, ++dossierIndex, completed);

            btn.interactable = true;

            string methodName = content.MethodologyName;
            btn.onClick.AddListener(() => OpenLearningContent(methodName));
        }
    }

    void OpenLearningContent(string methodologyName)
    {
        SceneParams.SelectedLearningMethod = methodologyName;
        SceneManager.LoadScene("LearningScene");
    }

    // ---------- Аудирование ----------

    void OpenListeningPanel()
    {
        if (ListeningButton != null && !ListeningButton.interactable) return;
        if (ShowNarrativeIfFirstTime(
            "module_2_first_open",
            "ЗАПИСЬ / 002",
            "МОДУЛЬ 02 · ПОДСЛУШАННЫЙ РАЗГОВОР",
            "Теперь я владею важной информацией, которая поможет мне в любом диалоге. Ведь главная сила — это слово. Возвращаясь по длинным коридорам, я услышал, как двое «умов» разговаривают. Нужно затаиться и подслушать их. Ведь я должен понять, как именно они используют разговорные техники."))
            return;

        ShowPanel(ListeningPanel);
        RefreshListeningPanel();
    }

    public void CloseListeningPanel() => ListeningPanel.SetActive(false);

    void RefreshListeningPanel()
    {
        if (ListeningPanelProgressText != null)
            ListeningPanelProgressText.text = PlayerProgress.ListeningProgressText();

        bool allDone = PlayerProgress.ListeningSolved >= PlayerProgress.TotalListeningTasks;
        ListeningStartButton.interactable = !allDone;
    }

    void StartListening() => SceneManager.LoadScene("ListeningScene");

    // ---------- Панель диалогов ----------

    void OpenDialoguePanel()
    {
        if (FreeDialogueButton != null && !FreeDialogueButton.interactable) return;
        if (ShowNarrativeIfFirstTime(
            "module_3_first_open",
            "ЗАПИСЬ / 003",
            "МОДУЛЬ 03 · ДИАЛОГИ",
            "Отлично, теперь я тоже владею всеми навыками переговоров! Самое время использовать их на практике и сразиться со сверхчеловеком!"))
            return;

        ShowPanel(DialoguePanel);
    }

    private bool ShowNarrativeIfFirstTime(string key, string title, string subtitle, string text)
    {
        if (PlayerProgress.HasSeenNarrative(key)) return false;
        ShowNarrative(title, subtitle, text, key, null);
        return true;
    }

    private void ShowNarrative(string title, string subtitle, string text, string key, System.Action onNext)
    {
        if (_narrativeOverlay == null)
        {
            TMP_FontAsset font = LearningStatusText != null ? LearningStatusText.font : TMP_Settings.defaultFontAsset;
            _narrativeOverlay = MenuNarrativeOverlay.Create(font);
        }

        _narrativeOverlay.Show(title, subtitle, text, () =>
        {
            if (!string.IsNullOrWhiteSpace(key)) PlayerProgress.MarkNarrativeSeen(key);
            onNext?.Invoke();
        });
    }

    private void OpenLearningPanelIfRequested()
    {
        if (!SceneParams.OpenLearningPanelOnMenu) return;
        SceneParams.OpenLearningPanelOnMenu = false;
        OpenLearningPanel();
    }

    public void CloseDialoguePanel() => DialoguePanel.SetActive(false);

    private void StartLearningDialogue()
    {
        var activeGoal = UserManager.CurrentUser?.GetActiveGoal();
        var availableMethods = new List<string>();

        if (activeGoal?.LearningMethods != null)
        {
            foreach (string method in activeGoal.LearningMethods)
            {
                if (string.IsNullOrWhiteSpace(method)) continue;
                bool hasContent = AllLearningContent != null &&
                    System.Array.Exists(AllLearningContent, content =>
                        content != null && content.MethodologyName == method);
                if (hasContent && !availableMethods.Contains(method))
                    availableMethods.Add(method);
            }
        }

        if (availableMethods.Count == 0)
        {
            if (FreeDialogueStatusText != null)
                FreeDialogueStatusText.text = "Для этой цели нет учебных материалов";
            return;
        }

        SceneParams.SelectedLearningMethod = availableMethods[Random.Range(0, availableMethods.Count)];
        SceneManager.LoadScene("LearningDialog");
    }

    private void ShowPanel(GameObject panel)
    {
        if (LearningPanel != null) LearningPanel.SetActive(false);
        if (ListeningPanel != null) ListeningPanel.SetActive(false);
        if (DialoguePanel != null) DialoguePanel.SetActive(false);
        if (panel == null) return;
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

}
