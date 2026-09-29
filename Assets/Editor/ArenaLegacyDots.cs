using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ArenaLegacyDots
{
    public static void Convert(TMP_Text label)
    {
        if (!label.text.Contains("●")) return;
        label.text = label.text.Replace("●", "").TrimStart();
        var dot = new GameObject("LegacyDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)dot.transform;
        rect.SetParent(label.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(12, label.rectTransform.rect.height);
        var text = dot.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "●";
        text.fontSize = Mathf.RoundToInt(label.fontSize);
        text.color = new Color32(0xB8, 0x3C, 0x46, 0xFF);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        bool centered = label.alignment == TextAlignmentOptions.Center;
        text.alignment = centered ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
        if (centered)
            rect.anchoredPosition = new Vector2(Mathf.Max(0, (label.rectTransform.rect.width - label.preferredWidth) / 2 - 14), 0);
        else
        {
            var margin = label.margin;
            margin.x += 14;
            label.margin = margin;
        }
    }

    public static void Run()
    {
        int count = 0;
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity"))
        {
            var scene = EditorSceneManager.OpenScene(path);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    if (label.text.Contains("●")) { Convert(label); changed = true; count++; }
            if (changed) EditorSceneManager.SaveScene(scene);
        }
        File.WriteAllText("legacy-dots-result.txt", "Converted " + count + " TMP bullets to Legacy Text.");
    }
}
