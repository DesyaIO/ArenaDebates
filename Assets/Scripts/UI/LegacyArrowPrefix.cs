using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps button captions in TMP, with the arrow drawn by the built-in UI Text.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class LegacyArrowPrefix : MonoBehaviour
{
    [SerializeField] private Text arrow;
    private TMP_Text label;

    public static bool Convert(TMP_Text caption)
    {
        if (caption == null || !caption.text.Contains("→")) return false;
        var prefix = caption.GetComponent<LegacyArrowPrefix>();
        if (prefix == null) prefix = caption.gameObject.AddComponent<LegacyArrowPrefix>();
        prefix.EnsureArrow();
        caption.text = caption.text.Replace("→", "").Trim();
        prefix.Refresh();
        return true;
    }

    void EnsureArrow()
    {
        label = GetComponent<TMP_Text>();
        if (arrow != null) return;
        var child = new GameObject("LegacyArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        child.GetComponent<LayoutElement>().ignoreLayout = true;
        arrow = child.GetComponent<Text>();
        arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrow.text = "→";
        arrow.raycastTarget = false;
        arrow.alignment = TextAnchor.MiddleCenter;
        arrow.horizontalOverflow = HorizontalWrapMode.Overflow;
        arrow.verticalOverflow = VerticalWrapMode.Overflow;
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = Vector2.zero;
        arrow.rectTransform.pivot = new Vector2(1, .5f);
    }

    void OnEnable() { label = GetComponent<TMP_Text>(); }
    void LateUpdate() { Refresh(); }

    public void Refresh()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null || arrow == null) return;
        // Also handles captions replaced at runtime (for example prologue pages).
        if (label.text.Contains("→")) label.text = label.text.Replace("→", "").Trim();
        arrow.color = label.color;
        arrow.fontSize = Mathf.Max(1, Mathf.RoundToInt(label.fontSize));
        arrow.rectTransform.sizeDelta = new Vector2(label.fontSize * 1.2f, label.fontSize * 1.8f);
        if (!label.gameObject.activeInHierarchy) return;
        label.ForceMeshUpdate();
        bool visible = false;
        for (int i = 0; i < label.textInfo.characterCount; i++)
        {
            var character = label.textInfo.characterInfo[i];
            if (!character.isVisible) continue;
            var bounds = label.rectTransform.rect;
            arrow.rectTransform.anchoredPosition = new Vector2(
                character.bottomLeft.x - bounds.xMin - label.fontSize * .25f,
                (character.ascender + character.descender) * .5f - bounds.yMin);
            visible = true;
            break;
        }
        arrow.enabled = visible;
    }
}
