using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GoalSetupPanel : MonoBehaviour
{
    [Header("Выбор цели")]
    public Button IndustryButton, GoalButton, SaveButton, BackButton;
    public TMP_Text IndustryText, GoalText, MethodsText, MessageText;
    [Header("Прокручиваемый список")]
    public GameObject SelectionPanel;
    public TMP_Text SelectionTitle;
    public ScrollRect SelectionScroll;
    public RectTransform SelectionContent;
    public Button SelectionRowTemplate, SelectionCloseButton;
    [Header("Аккаунт и сброс")]
    public Button ResetButton, LogoutButton;
    public GameObject ResetConfirmPanel;
    public Button ResetYesButton, ResetNoButton;
    public TMP_Text ResetMessageText;

    private GoalDatabase _goalDb;
    private string _selectedIndustry = "";
    private GoalEntry _selectedGoal;
    private readonly List<GameObject> _rows = new List<GameObject>();
    private bool _saving;

    void Start()
    {
        IndustryButton.onClick.AddListener(OpenIndustries);
        GoalButton.onClick.AddListener(OpenGoals);
        SaveButton.onClick.AddListener(OnSave);
        BackButton.onClick.AddListener(GoToMenu);
        SelectionCloseButton.onClick.AddListener(CloseSelection);
        ResetButton.onClick.AddListener(ShowResetConfirm);
        LogoutButton.onClick.AddListener(OnLogout);
        ResetYesButton.onClick.AddListener(OnResetConfirmed);
        ResetNoButton.onClick.AddListener(() => ResetConfirmPanel.SetActive(false));
        SelectionPanel.SetActive(false);
        SelectionRowTemplate.gameObject.SetActive(false);
        ResetConfirmPanel.SetActive(false);
        Refresh();
        MessageText.text = "Загружаем цели…";
        StartCoroutine(GoalLoader.LoadGoals(db =>
        {
            _goalDb = db;
            var active = UserManager.CurrentUser?.GetActiveGoal();
            if (active != null && db.GetIndustry(active.Industry) != null)
            {
                _selectedIndustry = active.Industry;
                _selectedGoal = db.GetIndustry(active.Industry).Goals.Find(g => g.GoalText == active.GoalText);
            }
            MessageText.text = db.Industries.Count == 0
                ? "Не удалось загрузить цели. Вернитесь в меню и попробуйте ещё раз."
                : "Выберите отрасль, затем цель. Подходящие методы появятся ниже.";
            Refresh();
        }));
    }

    void Refresh()
    {
        IndustryText.text = string.IsNullOrEmpty(_selectedIndustry) ? "Выбрать отрасль  ›" : _selectedIndustry + "  ›";
        GoalText.text = _selectedGoal == null ? "Выбрать цель  ›" : _selectedGoal.GoalText + "  ›";
        MethodsText.text = _selectedGoal == null ? "Выберите цель, чтобы увидеть программу обучения."
            : string.Join("\n\n", _selectedGoal.Methods.ConvertAll(m => "•  " + m));
        IndustryButton.interactable = !_saving && _goalDb != null && _goalDb.Industries.Count > 0;
        GoalButton.interactable = !_saving && !string.IsNullOrEmpty(_selectedIndustry);
        SaveButton.interactable = !_saving && _selectedGoal != null && _selectedGoal.Methods.Count > 0;
        BackButton.gameObject.SetActive(UserManager.CurrentUser?.HasActiveGoal() == true);
    }

    void BeginSelection(string title)
    {
        foreach (var row in _rows) { row.SetActive(false); Destroy(row); }
        _rows.Clear();
        SelectionTitle.text = title;
        SelectionPanel.SetActive(true);
        SelectionPanel.transform.SetAsLastSibling();
        SelectionScroll.StopMovement();
    }

    void AddRow(string text, bool selected, float height, System.Action onClick)
    {
        var button = Instantiate(SelectionRowTemplate, SelectionContent);
        var rect = (RectTransform)button.transform;
        float top = 0;
        foreach (var row in _rows) top += ((RectTransform)row.transform).sizeDelta.y + 18;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(-12, height);
        rect.anchoredPosition = new Vector2(0, -top);
        button.GetComponentInChildren<TMP_Text>(true).text = text;
        button.GetComponent<Image>().color = selected ? new Color(.08f, .35f, .39f) : new Color(.07f, .12f, .20f);
        button.onClick.AddListener(() => onClick());
        button.gameObject.SetActive(true);
        _rows.Add(button.gameObject);
        SelectionContent.sizeDelta = new Vector2(0, top + height + 16);
    }

    void EndSelection()
    {
        if (_rows.Count == 0) SelectionContent.sizeDelta = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        SelectionScroll.verticalNormalizedPosition = 1;
    }

    void OpenIndustries()
    {
        if (_goalDb == null) return;
        BeginSelection("Выберите отрасль");
        foreach (var industry in _goalDb.Industries)
        {
            var item = industry;
            AddRow(item.Industry + "\n<size=25><color=#B7C9DF>Целей: " + item.Goals.Count + "</color></size>",
                item.Industry == _selectedIndustry, 145, () =>
                {
                    if (_selectedIndustry != item.Industry) _selectedGoal = null;
                    _selectedIndustry = item.Industry;
                    Refresh();
                    OpenGoals();
                });
        }
        EndSelection();
    }

    void OpenGoals()
    {
        var industry = _goalDb?.GetIndustry(_selectedIndustry);
        if (industry == null) return;
        BeginSelection(_selectedIndustry + " · цели и методы");
        foreach (var goal in industry.Goals)
        {
            var item = goal;
            AddRow(item.GoalText + "\n\n<size=25><color=#B7C9DF>" + string.Join(" • ", item.Methods) + "</color></size>",
                item == _selectedGoal, 230, () =>
                {
                    _selectedGoal = item;
                    CloseSelection();
                    MessageText.text = "Программа выбрана. Нажмите «Сохранить цель», чтобы продолжить.";
                    Refresh();
                });
        }
        EndSelection();
    }

    void CloseSelection() => SelectionPanel.SetActive(false);
    void OnSave()
    {
        if (_saving || _selectedGoal == null || _selectedGoal.Methods.Count == 0) return;
        if (UserManager.CurrentUser == null)
        {
            MessageText.text = "Сначала войдите в аккаунт через кнопку «Выйти».";
            return;
        }
        UserManager.SetActiveGoal(_selectedIndustry, _selectedGoal.GoalText, _selectedGoal.Methods);
        SceneParams.SelectedLearningMethod = _selectedGoal.Methods[0];
        _saving = true;
        Refresh();
        GoToMenu();
    }
    void ShowResetConfirm()
    {
        ResetMessageText.text = "Сбросить прогресс по всем целям?\nЭто действие нельзя отменить.";
        ResetConfirmPanel.transform.SetAsLastSibling();
        ResetConfirmPanel.SetActive(true);
    }
    void OnResetConfirmed()
    {
        UserManager.ResetAllProgress();
        ResetConfirmPanel.SetActive(false);
        _selectedIndustry = "";
        _selectedGoal = null;
        Refresh();
        MessageText.text = "Прогресс сброшен. Выберите новую цель.";
    }
    void OnLogout()
    {
        UserManager.Logout();
        SceneManager.LoadScene("LoginScene");
    }
    void GoToMenu() => SceneManager.LoadScene("MenuScene");
}
