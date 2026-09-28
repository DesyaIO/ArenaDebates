using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SceneParams
{
    public static string SelectedLearningMethod = "";
    public static bool OpenLearningPanelOnMenu;
}

public class MenuController : MonoBehaviour
{
    [Header("Кнопки")]
    public Button LearningButton;
    public Button ListeningButton;
    public Button FreeDialogueButton;
    public Button GoalButton;
    public Button LogoutButton;

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

        AddListener(ListeningStartButton, StartListening);
        AddListener(ListeningCloseButton, CloseListeningPanel);

        // Панель диалогов
        AddListener(LearningDialogButton, StartLearningDialogue);
        AddListener(FreeDialogButton, () => SceneManager.LoadScene("DialogScene"));
        AddListener(DialogueCloseButton, CloseDialoguePanel);

        // Прячем панели
        if (LearningPanel != null) LearningPanel.SetActive(false);
        if (ListeningPanel != null) ListeningPanel.SetActive(false);
        if (DialoguePanel != null) DialoguePanel.SetActive(false);

        UpdateUI();

        if (SceneParams.OpenLearningPanelOnMenu)
        {
            SceneParams.OpenLearningPanelOnMenu = false;
            OpenLearningPanel();
        }
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

        bool allListeningDone = PlayerProgress.ListeningSolved >= PlayerProgress.TotalListeningTasks;
        bool freeDialogueUnlocked = listeningUnlocked && allListeningDone;
        // Оба режима диалога открываются только после полного обучения
        // и прохождения всех заданий аудирования.
        if (FreeDialogueButton != null) FreeDialogueButton.interactable = freeDialogueUnlocked;

        if (FreeDialogueStatusText != null)
        {
            FreeDialogueStatusText.text = freeDialogueUnlocked
                ? "Свободный и учебный режим"
                : "Откройте и выберите режим";
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
        ShowPanel(DialoguePanel);
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
