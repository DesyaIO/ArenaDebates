using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Полноэкранная сюжетная карточка, которая появляется поверх меню.</summary>
public sealed class MenuNarrativeOverlay : MonoBehaviour
{
    private Canvas _canvas;
    private TMP_Text _title;
    private TMP_Text _subtitle;
    private TMP_Text _body;
    private ScrollRect _scroll;
    private Button _nextButton;
    private Action _onNext;

    public static MenuNarrativeOverlay Create(TMP_FontAsset font)
    {
        var root = new GameObject("MenuNarrativeOverlay", typeof(RectTransform));
        var overlay = root.AddComponent<MenuNarrativeOverlay>();
        overlay.Build(font);
        root.SetActive(false);
        return overlay;
    }

    public void Show(string title, string subtitle, string body, Action onNext)
    {
        _title.text = title ?? "ЗАПИСЬ";
        _subtitle.text = subtitle ?? string.Empty;
        _body.text = body ?? string.Empty;
        _onNext = onNext;
        _scroll.verticalNormalizedPosition = 1f;
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        _body.ForceMeshUpdate();
        _scroll.verticalNormalizedPosition = 1f;
    }

    private void Build(TMP_FontAsset font)
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        Color32 ink = new Color32(0x1D, 0x36, 0x44, 0xFF);
        Color32 paper = new Color32(0xED, 0xF3, 0xF6, 0xFF);
        Color32 muted = new Color32(0x60, 0x71, 0x7C, 0xFF);
        Color32 red = new Color32(0xB8, 0x3C, 0x46, 0xFF);

        var dim = CreateImage("Dim", transform, new Color32(0x0B, 0x1C, 0x25, 0xD8));
        Stretch((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var card = CreateImage("StoryCard", transform, paper);
        Stretch((RectTransform)card.transform, new Vector2(.045f, .045f), new Vector2(.955f, .955f), Vector2.zero, Vector2.zero);
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(0x0B, 0x1C, 0x25, 0x35);
        shadow.effectDistance = new Vector2(0f, -14f);

        _title = CreateText("Title", card.transform, font, ink, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch((RectTransform)_title.transform, new Vector2(.08f, .90f), new Vector2(.92f, .97f), Vector2.zero, Vector2.zero);
        _title.text = "ЗАПИСЬ";
        _title.characterSpacing = 3f;

        _subtitle = CreateText("Subtitle", card.transform, font, muted, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch((RectTransform)_subtitle.transform, new Vector2(.08f, .84f), new Vector2(.92f, .895f), Vector2.zero, Vector2.zero);
        _subtitle.text = string.Empty;
        _subtitle.characterSpacing = 1.5f;

        var accent = CreateImage("AccentRule", card.transform, red);
        Stretch((RectTransform)accent.transform, new Vector2(.08f, .815f), new Vector2(.92f, .819f), Vector2.zero, Vector2.zero);

        var scrollObject = new GameObject("StoryScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollObject.transform.SetParent(card.transform, false);
        Stretch((RectTransform)scrollObject.transform, new Vector2(.08f, .17f), new Vector2(.92f, .79f), Vector2.zero, Vector2.zero);
        var viewportImage = scrollObject.GetComponent<Image>();
        viewportImage.color = new Color32(0xED, 0xF3, 0xF6, 0x01);
        var mask = scrollObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(scrollObject.transform, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0f;
        layout.padding = new RectOffset(4, 4, 8, 12);
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _body = CreateText("NarrativeText", content.transform, font, ink, 34f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.enableWordWrapping = true;
        _body.overflowMode = TextOverflowModes.Overflow;
        _body.margin = new Vector4(2f, 2f, 2f, 6f);
        _body.text = string.Empty;

        _scroll = scrollObject.GetComponent<ScrollRect>();
        _scroll.content = contentRect;
        _scroll.viewport = (RectTransform)scrollObject.transform;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 38f;
        VisibleScrollbar.Ensure(_scroll);

        var buttonImage = CreateImage("NextButton", card.transform, red);
        Stretch((RectTransform)buttonImage.transform, new Vector2(.16f, .055f), new Vector2(.84f, .135f), Vector2.zero, Vector2.zero);
        _nextButton = buttonImage.gameObject.AddComponent<Button>();
        _nextButton.targetGraphic = buttonImage;
        _nextButton.transition = Selectable.Transition.ColorTint;
        _nextButton.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color32(0xC9, 0x50, 0x59, 0xFF),
            pressedColor = new Color32(0x98, 0x2E, 0x38, 0xFF),
            selectedColor = Color.white,
            disabledColor = Color.gray,
            colorMultiplier = 1f,
            fadeDuration = .1f
        };
        _nextButton.onClick.AddListener(Dismiss);

        var buttonLabel = CreateText("NextLabel", buttonImage.transform, font, Color.white, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch((RectTransform)buttonLabel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        buttonLabel.text = "ДАЛЕЕ";
        buttonLabel.raycastTarget = false;
    }

    private void Dismiss()
    {
        gameObject.SetActive(false);
        var callback = _onNext;
        _onNext = null;
        callback?.Invoke();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, Color color,
        float size, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
