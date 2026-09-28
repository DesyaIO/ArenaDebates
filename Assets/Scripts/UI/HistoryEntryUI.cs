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
        CategoryText.text = entry.Category;
        ExplanationText.text = entry.Explanation;

        // Знак урона: отрицательный — урон, положительный — лечение
        string damageStr = entry.Damage < 0
            ? $"Урон: {entry.Damage}"
            : $"Лечение: +{entry.Damage}";
        DamageText.text = damageStr;

        HealthText.text = $"HP игрока: {entry.PlayerHealthAfter} | HP оппонента: {entry.OpponentHealthAfter}";

        // Цвет фона: игрок — синеватый, оппонент — красноватый
        if (Background != null)
            Background.color = entry.IsPlayerTurn
                ? new Color(0.2f, 0.3f, 0.5f, 0.6f)
                : new Color(0.5f, 0.2f, 0.2f, 0.6f);

        // Кнопка отката — только если игра закончена и разрешено
        if (RollbackButton != null)
        {
            RollbackButton.gameObject.SetActive(allowRollback);
            RollbackButton.onClick.RemoveAllListeners();
            RollbackButton.onClick.AddListener(() => _onRollbackClicked?.Invoke(_entryIndex));
        }
    }
}