using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DialogueFirstSpeaker { Player, Opponent, Random }

public static class DialogueOptions
{
    public static bool Pending;
    public static string Difficulty = "Средний";
    public static DialogueFirstSpeaker FirstSpeaker = DialogueFirstSpeaker.Random;
    public static int Turns = 5;
    public static bool AnalyzeResponses = true;
    public static bool UseMySituation;
    public static string MySituationTopic = "";
    public static string MyPlayerPosition = "";
    public static string MyOpponentPosition = "";

    private static readonly Color Ink = new Color32(0x1D, 0x36, 0x44, 0xFF);
    private static readonly Color MutedInk = new Color32(0x5F, 0x73, 0x7D, 0xFF);
    private static readonly Color Paper = new Color32(0xF1, 0xF5, 0xF7, 0xFF);
    private static readonly Color Card = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    private static readonly Color SoftBlue = new Color32(0xE1, 0xEA, 0xEF, 0xFF);
    private static readonly Color Accent = new Color32(0xB8, 0x3C, 0x46, 0xFF);
    private static Sprite _roundedSprite;

    private static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite != null) return _roundedSprite;

            const int size = 48;
            const int radius = 18;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DialogueOptionsRoundedBackground",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius), 0f);
                    float alpha = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _roundedSprite.name = "DialogueOptionsRoundedBackground";
            _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return _roundedSprite;
        }
    }

    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
        r.pivot = new Vector2(.5f, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    public static GameObject Overlay(TMP_FontAsset font, string title, out Transform root)
    {
        var go = new GameObject("DialogueOptionsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 1;
        canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1;

        var backdropRect = Rect(go.transform, "Backdrop", 0, 0, 1080, 1920);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.anchoredPosition = Vector2.zero;
        backdropRect.sizeDelta = Vector2.zero;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        var backdrop = backdropRect.gameObject.AddComponent<Image>();
        bool isSettings = title.StartsWith("Настройки", StringComparison.Ordinal);
        backdrop.color = isSettings ? Paper : new Color(.08f, .12f, .16f, .42f);
        backdrop.raycastTarget = true;
        var card = Rect(backdropRect, "DialogueOptionsCard", 0, isSettings ? 0 : 740,
            isSettings ? 1080 : 1000, isSettings ? 1920 : 440);
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = isSettings ? Paper : Card;
        cardImage.sprite = RoundedSprite;
        cardImage.type = Image.Type.Sliced;
        root = card;
        if (isSettings)
        {
            Label(root, font, "ШАГ 02 / 02", 145, 50, 26, MutedInk, TextAlignmentOptions.Right);
            var heading = Label(root, font, "Настройки диалога", 250, 100, 65, Ink, TextAlignmentOptions.Left);
            heading.rectTransform.sizeDelta = new Vector2(940, 100);
            var subtitle = Label(root, font, "Выберите темп и характер разговора.", 350, 70, 32, MutedInk, TextAlignmentOptions.Left);
            subtitle.rectTransform.sizeDelta = new Vector2(940, 70);
        }
        else Label(root, font, title, 145, 80, 44, Ink, TextAlignmentOptions.Center);
        return go;
    }

    static TMP_Text Label(Transform parent, TMP_FontAsset font, string value, float y,
        float h = 65, float size = 29, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var text = Rect(parent, "Label_" + value, 0, y, 920, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color ?? MutedInk;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    static Button Button(Transform parent, TMP_FontAsset font, string value, float y, Action action,
        Color? fill = null, float height = 94)
    {
        var rect = Rect(parent, "Option_" + value, 0, y, 900, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = fill ?? SoftBlue;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label(rect, font, value, 0, height - 4, 28, fill == Accent ? Color.white : Ink);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.rectTransform.sizeDelta = Vector2.zero;
        label.rectTransform.offsetMin = new Vector2(20, 4);
        label.rectTransform.offsetMax = new Vector2(-20, -4);
        button.onClick.AddListener(() => action());
        return button;
    }

    static TMP_InputField InputField(Transform parent, TMP_FontAsset font, string name, string hint,
        string value, float y, float height)
    {
        var rect = Rect(parent, name, 0, y, 900, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Card;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 700;
        input.caretColor = Accent;
        input.selectionColor = new Color32(0xD9, 0x9A, 0x9E, 0x88);

        var viewport = new GameObject(name + "_Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(rect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(24, 13);
        viewport.offsetMax = new Vector2(-24, -13);
        input.textViewport = viewport;

        var text = new GameObject(name + "_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(viewport, false);
        Stretch(text.rectTransform);
        text.font = font;
        text.fontSize = 26;
        text.color = Ink;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        input.textComponent = text;

        var placeholder = new GameObject(name + "_Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        placeholder.transform.SetParent(viewport, false);
        Stretch(placeholder.rectTransform);
        placeholder.font = font;
        placeholder.fontSize = 24;
        placeholder.color = new Color(MutedInk.r, MutedInk.g, MutedInk.b, .72f);
        placeholder.text = hint;
        placeholder.alignment = TextAlignmentOptions.TopLeft;
        placeholder.enableWordWrapping = true;
        placeholder.raycastTarget = false;
        input.placeholder = placeholder;
        input.SetTextWithoutNotify(value ?? "");
        return input;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
    }

    private static RectTransform SettingsCard(Transform parent, string name, float y, float height)
    {
        var rect = Rect(parent, name, 0, y, 940, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = Card;
        image.raycastTarget = false;
        return rect;
    }

    private static Action ChoiceRow(Transform parent, TMP_FontAsset font, string name,
        string[] captions, float y, float height, Func<int> selected, Action<int> select)
    {
        var card = SettingsCard(parent, name, y, height);
        var heading = Label(card, font, name, 28, 47, 31, Ink, TextAlignmentOptions.Left);
        heading.rectTransform.sizeDelta = new Vector2(850, 47);
        var bar = Rect(card, name + "Choices", 0, 90, 850, 85);
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.sprite = RoundedSprite;
        barImage.type = Image.Type.Sliced;
        barImage.color = SoftBlue;
        barImage.raycastTarget = false;
        var choices = new Image[captions.Length];
        float itemWidth = 840f / captions.Length;
        for (int i = 0; i < captions.Length; i++)
        {
            int index = i;
            var item = Rect(bar, "Choice_" + captions[i],
                -420f + itemWidth * (i + .5f), 5, itemWidth - 6, 75);
            choices[i] = item.gameObject.AddComponent<Image>();
            choices[i].sprite = RoundedSprite;
            choices[i].type = Image.Type.Sliced;
            var button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = choices[i];
            var label = Label(item, font, captions[i], 13, 55, captions.Length == 3 ? 24 : 28, Ink);
            label.rectTransform.sizeDelta = new Vector2(itemWidth - 20, 55);
            button.onClick.AddListener(() => { select(index); Refresh(); });
        }
        void Refresh()
        {
            for (int i = 0; i < choices.Length; i++)
                choices[i].color = i == selected() ? Card : SoftBlue;
        }
        Refresh();
        return Refresh;
    }

    public static void Show(TMP_FontAsset font, Action confirm)
    {
        var panel = Overlay(font, "Настройки диалога", out var root);
        var topBack = Button(root, font, "Назад", 145,
            () => UnityEngine.Object.Destroy(panel), Card, 76);
        var topBackRect = (RectTransform)topBack.transform;
        topBackRect.anchoredPosition = new Vector2(-400, -145);
        topBackRect.sizeDelta = new Vector2(150, 76);
        topBack.GetComponentInChildren<TMP_Text>().fontSize = 24;

        var viewport = Rect(root, "SettingsViewport", 0, 450, 960, 1130);
        var hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = new Color(1, 1, 1, 0);
        hitArea.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36;
        scroll.viewport = viewport;
        var content = Rect(viewport, "SettingsContent", 0, 0, 960, 1150);
        scroll.content = content;
        VisibleScrollbar.Ensure(scroll);

        var opponentCard = SettingsCard(content, "Оппонент", 0, 190);
        var opponentHeading = Label(opponentCard, font, "Оппонент", 18, 45, 30, Ink, TextAlignmentOptions.Left);
        opponentHeading.rectTransform.sizeDelta = new Vector2(850, 45);
        var portraitRect = Rect(opponentCard, "OpponentPortrait", -360, 64.82f, 100, 100);
        var portraitMaskImage = portraitRect.gameObject.AddComponent<Image>();
        portraitMaskImage.sprite = RoundedSprite;
        portraitMaskImage.type = Image.Type.Simple;
        portraitRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var portrait = Rect(portraitRect, "Portrait", 0, 0, 125, 125);
        Stretch(portrait);
        var portraitImage = portrait.gameObject.AddComponent<Image>();
        portraitImage.sprite = Resources.Load<Sprite>("DialogPortrait");
        portraitImage.raycastTarget = false;
        var name = Label(opponentCard, font, "Куратор K-07", 70, 50, 34, Ink, TextAlignmentOptions.Left);
        name.rectTransform.anchoredPosition = new Vector2(72, -70);
        name.rectTransform.sizeDelta = new Vector2(590, 50);
        var role = Label(opponentCard, font, "УМ · ПЕРЕГОВОРЩИК СИСТЕМЫ", 122, 38, 23, MutedInk, TextAlignmentOptions.Left);
        role.rectTransform.anchoredPosition = new Vector2(72, -122);
        role.rectTransform.sizeDelta = new Vector2(590, 38);

        var difficulties = new[] { "Лёгкий", "Средний", "Сложный" };
        int DifficultyIndex() => Difficulty == "Лёгкий" ? 0 : Difficulty == "Сложный" ? 2 : 1;
        ChoiceRow(content, font, "Сложность оппонента", difficulties, 210, 195,
            DifficultyIndex, index => Difficulty = index == 0 ? "Лёгкий" : index == 1 ? "Средний" : "Сложный");

        var starters = new[] { "Я", "Оппонент", "Случайно" };
        ChoiceRow(content, font, "Кто начинает диалог", starters, 425, 195,
            () => FirstSpeaker == DialogueFirstSpeaker.Player ? 0 :
                FirstSpeaker == DialogueFirstSpeaker.Opponent ? 1 : 2,
            index => FirstSpeaker = index == 0 ? DialogueFirstSpeaker.Player :
                index == 1 ? DialogueFirstSpeaker.Opponent : DialogueFirstSpeaker.Random);

        var durations = new[] { "5 ходов", "10 ходов" };
        ChoiceRow(content, font, "Ходов каждому", durations, 640, 195,
            () => Turns == 10 ? 1 : 0, index => Turns = index == 0 ? 5 : 10);

        var analysis = Button(content, font, "", 855, () => { }, Card, 110);
        Action refreshAnalysis = () => analysis.GetComponentInChildren<TMP_Text>().text =
            "Разбор ответов    " + (AnalyzeResponses ? "ВКЛЮЧЁН" : "ВЫКЛЮЧЕН");
        analysis.onClick.AddListener(() => { AnalyzeResponses = !AnalyzeResponses; refreshAnalysis(); });
        refreshAnalysis();

        var situation = Button(content, font, "", 985, () => { }, Card, 110);
        Action refreshSituation = () => situation.GetComponentInChildren<TMP_Text>().text =
            "Моя ситуация    " + (UseMySituation ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА");
        var formCaption = Label(content, font, "", 1105, 80, 24, MutedInk);
        var topicLabel = Label(content, font, "СИТУАЦИЯ", 1200, 37, 24, MutedInk);
        var topic = InputField(content, font, "MySituationTopic",
            "Опишите ситуацию и тему разговора", MySituationTopic, 1240, 110);
        var playerLabel = Label(content, font, "ВАША ПОЗИЦИЯ", 1370, 37, 24, MutedInk);
        var player = InputField(content, font, "MyPlayerPosition",
            "Чего вы хотите добиться?", MyPlayerPosition, 1410, 110);
        var opponentLabel = Label(content, font, "ПОЗИЦИЯ ОППОНЕНТА", 1540, 37, 24, MutedInk);
        var opponent = InputField(content, font, "MyOpponentPosition",
            "Чего хочет собеседник?", MyOpponentPosition, 1580, 110);
        topic.onValueChanged.AddListener(value => MySituationTopic = value);
        player.onValueChanged.AddListener(value => MyPlayerPosition = value);
        opponent.onValueChanged.AddListener(value => MyOpponentPosition = value);

        var validation = Label(root, font, "", 1580, 45, 23, Accent);
        var start = Button(root, font, "Начать диалог", 1650, () =>
        {
            MySituationTopic = topic.text.Trim();
            MyPlayerPosition = player.text.Trim();
            MyOpponentPosition = opponent.text.Trim();
            if (UseMySituation && (string.IsNullOrWhiteSpace(MySituationTopic) ||
                string.IsNullOrWhiteSpace(MyPlayerPosition) || string.IsNullOrWhiteSpace(MyOpponentPosition)))
            {
                validation.text = "Заполните ситуацию и обе позиции.";
                return;
            }
            Pending = true;
            UnityEngine.Object.Destroy(panel);
            confirm?.Invoke();
        }, Accent, 120);
        var back = Button(root, font, "Назад к теме", 1785,
            () => UnityEngine.Object.Destroy(panel), Card, 95);
        start.GetComponentInChildren<TMP_Text>().fontSize = 38;
        back.GetComponentInChildren<TMP_Text>().fontSize = 30;

        void RefreshSituationForm()
        {
            bool active = UseMySituation;
            foreach (var item in new GameObject[] { topicLabel.gameObject, topic.gameObject,
                playerLabel.gameObject, player.gameObject, opponentLabel.gameObject, opponent.gameObject })
                item.SetActive(active);
            content.sizeDelta = new Vector2(960, active ? 1710 : 1185);
            formCaption.text = active
                ? "Эти поля заменят тему и позиции из каталога."
                : "По умолчанию тема и позиции выбираются из каталога.";
            validation.text = "";
            refreshSituation();
        }
        situation.onClick.AddListener(() => { UseMySituation = !UseMySituation; RefreshSituationForm(); });
        RefreshSituationForm();
    }
}
