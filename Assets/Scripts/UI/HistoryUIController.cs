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
        // 1. Проверяем обязательные ссылки
        if (HistoryPanel == null)
        {
            Debug.LogError("HistoryUIController.Show: HistoryPanel не назначен в инспекторе.");
            return;
        }
        if (EntriesContainer == null)
        {
            Debug.LogError("HistoryUIController.Show: EntriesContainer не назначен в инспекторе.");
            return;
        }
        if (HistoryEntryPrefab == null)
        {
            Debug.LogError("HistoryUIController.Show: HistoryEntryPrefab не назначен в инспекторе.");
            return;
        }

        // 2. Проверяем сессию
        var session = SessionManager.Instance != null ? SessionManager.Instance.CurrentSession : null;
        if (session == null || session.Entries == null)
        {
            Debug.LogWarning("HistoryUIController.Show: нет активной сессии — история пуста.");
            // На всякий случай очистим контейнер и всё равно покажем панель
            ClearEntries();
            HistoryPanel.SetActive(true);
            return;
        }

        _onRollbackClicked = onRollbackClicked;

        // 3. Очищаем старые записи
        ClearEntries();

        // 4. Строим заново
        foreach (var entry in session.Entries)
        {
            if (entry == null) continue;
            var go = Instantiate(HistoryEntryPrefab, EntriesContainer);
            var ui = go.GetComponent<HistoryEntryUI>();
            if (ui != null)
                ui.Initialize(entry, _onRollbackClicked, AllowRollback);
            else
                Debug.LogWarning("HistoryEntryPrefab не содержит компонент HistoryEntryUI.");
        }

        HistoryPanel.SetActive(true);
    }

    public void Hide()
    {
        if (HistoryPanel != null)
            HistoryPanel.SetActive(false);
    }

    private void ClearEntries()
    {
        if (EntriesContainer == null) return;
        // Destroy отложен до конца кадра — используем обратную итерацию,
        // чтобы не мешать перебору.
        for (int i = EntriesContainer.childCount - 1; i >= 0; i--)
            Destroy(EntriesContainer.GetChild(i).gameObject);
    }
}