using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HistoryEntryUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text IndexText;
    public TMP_Text TurnText;       // "Игрок" / "Оппонент"
    public TMP_Text UserText;
    public TMP_Text CategoryText;
    public TMP_Text ExplanationText;
    public TMP_Text DamageText;
    public TMP_Text HealthText;
    public Image Background;
    public Button RollbackButton;

    private int _entryIndex;
    private System.Action<int> _onRollbackClicked;

    public void Initialize(DialogueEntry entry, System.Action<int> onRollbackClicked, bool allowRollback)
    {
        _entryIndex = entry.Index;
        _onRollbackClicked = onRollbackClicked;

        IndexText.text = $"#{entry.Index}";
        TurnText.text = entry.IsPlayerTurn ? "Игрок" : "Оппонент";
        UserText.text = entry.UserText;
        bool hasAnalysis = !string.IsNullOrWhiteSpace(entry.Explanation) && entry.Category != "Разбор отключён";
        CategoryText.text = hasAnalysis ? entry.Category : "Реплика без разбора";
        ExplanationText.text = hasAnalysis ? entry.Explanation : "";

        var ink = new Color32(0x1D, 0x36, 0x44, 0xFF);
        var mutedInk = new Color32(0x5F, 0x73, 0x7D, 0xFF);
        if (IndexText != null) IndexText.color = mutedInk;
        if (TurnText != null) TurnText.color = entry.IsPlayerTurn
            ? ink : new Color32(0xB8, 0x3C, 0x46, 0xFF);
        if (UserText != null) UserText.color = ink;
        if (CategoryText != null) CategoryText.color = ink;
        if (ExplanationText != null) ExplanationText.color = mutedInk;

        if (DamageText != null) DamageText.gameObject.SetActive(false);

        if (HealthText != null) HealthText.gameObject.SetActive(false);

        // Цвет фона: игрок — синеватый, оппонент — красноватый
        if (Background != null)
            Background.color = entry.IsPlayerTurn
                ? new Color32(0xE4, 0xED, 0xF1, 0xFF)
                : new Color32(0xF5, 0xE5, 0xE6, 0xFF);

        // Кнопка отката — только если игра закончена и разрешено
        if (RollbackButton != null)
        {
            RollbackButton.gameObject.SetActive(allowRollback);
            if (RollbackButton.targetGraphic != null)
                RollbackButton.targetGraphic.color = new Color32(0xB8, 0x3C, 0x46, 0xFF);
            RollbackButton.onClick.RemoveAllListeners();
            RollbackButton.onClick.AddListener(() => _onRollbackClicked?.Invoke(_entryIndex));
        }
    }
}
