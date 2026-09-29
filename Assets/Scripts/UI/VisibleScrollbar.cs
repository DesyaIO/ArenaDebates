using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Добавляет видимый индикатор прокрутки к уже существующему ScrollRect.</summary>
public static class VisibleScrollbar
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureAllInScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAllInScene();
    }

    private static void EnsureAllInScene()
    {
        foreach (var scroll in Object.FindObjectsOfType<ScrollRect>(true))
            Ensure(scroll);
    }

    public static void Ensure(ScrollRect scroll)
    {
        if (scroll == null || !scroll.vertical || scroll.content == null) return;
        if (scroll.verticalScrollbar != null)
        {
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return;
        }

        var track = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Scrollbar), typeof(LayoutElement));
        var trackRect = track.GetComponent<RectTransform>();
        trackRect.SetParent(scroll.transform, false);
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-3f, 0f);
        trackRect.sizeDelta = new Vector2(9f, -12f);
        trackRect.SetAsLastSibling();
        track.GetComponent<LayoutElement>().ignoreLayout = true;

        var trackImage = track.GetComponent<Image>();
        trackImage.color = new Color32(32, 55, 67, 65);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.SetParent(trackRect, false);
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        var handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color32(184, 60, 70, 240);

        var bar = track.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handleRect;
        bar.targetGraphic = handleImage;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }
}
