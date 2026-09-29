using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The nine chapters are content from NewDesign, independent of progression rules.
public class ArenaPrologue : MonoBehaviour
{
    public TextAsset Story;
    public TMP_Text Title, Location, Body, Counter;
    public Button Previous, Next;
    public Image[] Steps;
    public ScrollRect StoryScroll;
    [Serializable] public class Chapter { public string title, location, text; }
    [Serializable] public class Chapters { public Chapter[] chapters; }
    private Chapter[] _chapters;
    private int _index;

    void Awake()
    {
        // Recover references by their scene object names if optional Inspector
        // fields were cleared while editing the Prologue hierarchy.
        if (Title == null) Title = FindChildText("StoryCaption");
        if (Body == null) Body = FindChildText("Story");
        if (Counter == null) Counter = FindChildText("Counter");
        if (Previous == null) Previous = FindChildComponent<Button>("Previous");
        if (Next == null) Next = FindChildComponent<Button>("Next");
        if (StoryScroll == null) StoryScroll = FindChildComponent<ScrollRect>("StoryScroll");
        VisibleScrollbar.Ensure(StoryScroll);

        if (Story == null || string.IsNullOrWhiteSpace(Story.text))
        {
            Debug.LogError("ArenaPrologue: не назначен файл chapters.json или он пустой.", this);
            enabled = false;
            return;
        }

        try
        {
            _chapters = JsonUtility.FromJson<Chapters>(Story.text)?.chapters;
        }
        catch (Exception e)
        {
            Debug.LogError($"ArenaPrologue: не удалось прочитать главы пролога: {e.Message}", this);
            enabled = false;
            return;
        }

        if (_chapters == null || _chapters.Length == 0)
        {
            Debug.LogError("ArenaPrologue: в chapters.json нет глав для показа.", this);
            enabled = false;
            return;
        }

        if (Previous != null)
            Previous.onClick.AddListener(ShowPreviousChapter);
        else
            Debug.LogWarning("ArenaPrologue: кнопка Previous не назначена; листать назад нельзя.", this);

        if (Next != null)
            Next.onClick.AddListener(ShowNextChapter);
        else
            Debug.LogError("ArenaPrologue: кнопка Next не найдена; продолжить пролог нельзя.", this);

        Draw();
    }

    private void ShowPreviousChapter()
    {
        if (_index <= 0) return;
        _index--;
        Draw();
    }

    private void ShowNextChapter()
    {
        if (_chapters == null) return;
        if (_index + 1 < _chapters.Length)
        {
            _index++;
            Draw();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void Draw()
    {
        if (_chapters == null || _index < 0 || _index >= _chapters.Length) return;
        var chapter = _chapters[_index];
        if (chapter == null)
        {
            Debug.LogError($"ArenaPrologue: глава с индексом {_index} пустая.", this);
            return;
        }

        if (Title != null) Title.text = _index == 0 ? chapter.title : string.Empty;
        // Location is optional: it was intentionally removed from the current UI.
        if (Location != null) Location.text = chapter.location ?? string.Empty;
        if (Body != null) Body.text = chapter.text ?? string.Empty;
        else Debug.LogError("ArenaPrologue: не найден текстовый объект Story для основного текста.", this);

        if (StoryScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            StoryScroll.verticalNormalizedPosition = 1f;
        }

        if (Counter != null) Counter.text = $"{_index + 1:00} / {_chapters.Length:00}";
        if (Previous != null) Previous.interactable = _index > 0;

        TMP_Text nextLabel = Next != null ? Next.GetComponentInChildren<TMP_Text>(true) : null;
        if (nextLabel != null)
            nextLabel.text = _index == _chapters.Length - 1 ? "В путь" : "Далее";

        if (Steps == null) return;
        for (int i = 0; i < Steps.Length; i++)
        {
            if (Steps[i] == null) continue;
            Steps[i].color = i <= _index
                ? new Color32(0xB8, 0x3C, 0x46, 0xFF)
                : new Color32(0xD4, 0xE0, 0xE6, 0xFF);
        }
    }

    private TMP_Text FindChildText(string objectName)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            if (string.Equals(text.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                return text;
        return null;
    }

    private T FindChildComponent<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
            if (string.Equals(component.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                return component;
        return null;
    }
}
