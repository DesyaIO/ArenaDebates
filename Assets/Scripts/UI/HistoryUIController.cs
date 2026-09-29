using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HistoryUIController : MonoBehaviour
{
    [Header("UI")]
    public GameObject HistoryPanel;
    public Transform EntriesContainer;
    public GameObject HistoryEntryPrefab;
    public Button CloseButton;

    [Header("Откат")]
    [Tooltip("Если true — на каждой записи появится кнопка «Переиграть отсюда»")]
    public bool AllowRollback = true;

    private System.Action<int> _onRollbackClicked;

    void Awake()
    {
        if (EntriesContainer != null)
            VisibleScrollbar.Ensure(EntriesContainer.GetComponentInParent<ScrollRect>());
        if (CloseButton != null)
            CloseButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// Показать историю из текущей сессии.
    /// </summary>
    public void Show(System.Action<int> onRollbackClicked = null)
    {
        _onRollbackClicked = onRollbackClicked;

        // Очистить старые записи
        foreach (Transform child in EntriesContainer)
            Destroy(child.gameObject);

        var session = SessionManager.Instance.CurrentSession;
        if (session == null) return;

        foreach (var entry in session.Entries)
        {
            var go = Instantiate(HistoryEntryPrefab, EntriesContainer);
            var ui = go.GetComponent<HistoryEntryUI>();
            ui.Initialize(entry, _onRollbackClicked, AllowRollback);
        }

        HistoryPanel.SetActive(true);
    }

    public void Hide()
    {
        HistoryPanel.SetActive(false);
    }
}
