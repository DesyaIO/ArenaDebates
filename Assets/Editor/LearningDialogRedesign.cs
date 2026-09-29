using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Applies a one-shot rebuild to the real LearningDialog scene inside the open
// editor. Other open scenes are left in memory and on disk exactly as they are.
[InitializeOnLoad]
public static class LearningDialogRedesign
{
    const string Request = "Library/LearningDialogRedesign.request";
    const string Report = "Library/LearningDialogRedesign.result.txt";
    static TMP_FontAsset Font;
    static Sprite Round, Icon;
    static List<Button> BackButtons;
    static List<TMP_Text> StageLabels, StepLabels, StatusLabels;
    static readonly Color Paper = Hex("EEF4F7"), CardFill = Hex("F9FCFD"), Ink = Hex("263B46"), Muted = Hex("748891"),
        Red = Hex("B83C46"), Dark = Hex("264652"), PaleBlue = Hex("DCEAF0"), Line = Hex("D5E1E7");

    static LearningDialogRedesign() { EditorApplication.delayCall += ApplyRequested; }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

    static void ApplyRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += ApplyRequested; return; }
        File.Delete(Request);
        try { RebuildRealScene(); File.WriteAllText(Report, "SUCCESS: LearningDialog rebuilt and saved in the project editor."); }
        catch (Exception e) { File.WriteAllText(Report, "FAILED: " + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Arena/Redesign LearningDialog")]
    public static void RebuildRealScene()
    {
        string path = "Assets/Scenes/LearningDialog.unity";
        var originalActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/niks_0 SDF.asset");
            Round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Arena/Round.png");
            Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Arena/icon.jpg");
            if (Font == null) Font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            if (Font == null || Icon == null) throw new InvalidOperationException("Could not load niks_0 SDF or NewDesign/icon.jpg.");
            var view = Find<LearningDialogView>(scene);
            if (view == null) throw new InvalidOperationException("LearningDialogView is missing from LearningDialog scene.");
            var layout = FindRect(scene, "LearningLayout1080x1920");
            if (layout == null) throw new InvalidOperationException("1080x1920 layout root is missing.");
            foreach (Transform child in layout) UnityEngine.Object.DestroyImmediate(child.gameObject);
            layout.anchorMin = layout.anchorMax = new Vector2(.5f, .5f);
            layout.pivot = new Vector2(.5f, .5f); layout.anchoredPosition = Vector2.zero;
            layout.sizeDelta = new Vector2(1080, 1920);
            var scaler = layout.GetComponentInParent<CanvasScaler>();
            if (scaler == null) throw new InvalidOperationException("CanvasScaler missing.");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;

            var background = ImageBox(layout, "LearningBackground", 0, 0, 1080, 1920, Paper, null);
            background.transform.SetAsFirstSibling();
            var dialogue = Page(layout, "DialogueScreen");
            var transcript = Page(layout, "TranscriptScreen");
            var feedback = Page(layout, "FeedbackScreen");
            var summary = Page(layout, "SummaryScreen");
            BackButtons = new List<Button>();
            StageLabels = new List<TMP_Text>(); StepLabels = new List<TMP_Text>(); StatusLabels = new List<TMP_Text>();
            BuildDialogue(dialogue, view);
            BuildTranscript(transcript, view);
            BuildFeedback(feedback, view);
            BuildSummary(summary, view);
            view.TopBackButtons = BackButtons.ToArray();
            view.StageLabels = StageLabels.ToArray(); view.StepLabels = StepLabels.ToArray(); view.StatusLabels = StatusLabels.ToArray();
            view.MethodText = StageLabels[0]; view.StageText = StageLabels[0]; view.StepText = StepLabels[0];
            view.DialogueScreen = dialogue; view.TranscriptScreen = transcript;
            view.FeedbackScreen = feedback; view.SummaryScreen = summary;
            dialogue.SetActive(true); transcript.SetActive(false); feedback.SetActive(false); summary.SetActive(false);
            view.SetOpponentAvatar(Icon);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save LearningDialog scene.");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        }
    }

    static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects()) { var found = root.GetComponentInChildren<T>(true); if (found != null) return found; }
        return null;
    }
    static RectTransform FindRect(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<RectTransform>(true)) if (t.name == name) return t;
        return null;
    }
    static GameObject Page(Transform parent, string name)
    {
        var go = Rect(name, parent, 0, 0, 1080, 1920).gameObject;
        go.AddComponent<CanvasGroup>();
        return go;
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false); go.layer = parent.gameObject.layer;
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    static Image ImageBox(Transform p, string name, float x, float y, float w, float h, Color color, Sprite sprite, bool raycast = false)
    {
        var image = Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>(); image.color=color; image.sprite=sprite;
        image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced; image.raycastTarget=raycast; return image;
    }
    static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,float size=30,Color? color=null,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
    {
        var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); t.font=Font; t.text=value; t.fontSize=size;
        t.color=color??Ink; t.alignment=align; t.enableWordWrapping=true; t.overflowMode=TextOverflowModes.Ellipsis;
        t.margin=new Vector4(0,0,0,0); t.raycastTarget=false;
        ArenaLegacyDots.Convert(t);
        LegacyArrowPrefix.Convert(t);
        return t;
    }
    static Image Card(Transform p,string name,float x,float y,float w,float h,Color? color=null)
    {
        return ImageBox(p,name,x,y,w,h,color??CardFill,Round);
    }
    static Button Button(Transform p,string name,string label,float x,float y,float w,float h,bool primary=false)
    {
        var image=ImageBox(p,name,x,y,w,h,primary?Red:CardFill,Round,true);
        var b=image.gameObject.AddComponent<Button>(); b.targetGraphic=image;
        var colors=b.colors; colors.normalColor=primary?Red:CardFill; colors.highlightedColor=primary?Hex("A9323D"):Hex("E5EEF2");
        colors.pressedColor=primary?Hex("922B35"):Hex("D8E5EB"); colors.selectedColor=colors.normalColor; b.colors=colors;
        Text(image.transform,"Label",label,18,0,w-36,h,primary?28:25,primary?Color.white:Muted,TextAlignmentOptions.Center);
        return b;
    }
    static Button Header(Transform p,string stage,string step,LearningDialogView view)
    {
        Text(p,"StatusBarTime","9:41",48,22,150,48,28,Ink,TextAlignmentOptions.Left);
        var back=Button(p,"BackButton","‹",34,96,82,82,false); view.BackButton=back;
        BackButtons.Add(back);
        view.StageText=Text(p,"StageTitle",stage,150,118,680,44,19,Muted,TextAlignmentOptions.Center);
        StageLabels.Add(view.StageText);
        Card(p,"StepBadge",852,101,190,66);
        view.StepText=Text(p,"StepCounter",step,866,119,162,32,17,Muted,TextAlignmentOptions.Center);
        StepLabels.Add(view.StepText);
        return back;
    }
    static void BuildDialogue(GameObject page, LearningDialogView view)
    {
        var p=page.transform; Header(p,"МОДУЛЬ / ПЕРЕГОВОРЫ","ХОД 01 / 03",view);
        Card(p,"TopicCard",32,215,1016,207);
        Text(p,"TopicEyebrow","ТЕМА ФИНАЛЬНЫХ ПЕРЕГОВОРОВ",68,245,880,32,17,Red);
        view.TopicText=Text(p,"TopicText","Тема переговоров",68,289,900,58,31,Ink);
        Text(p,"TopicCaption","Найдите решение, учитывая интересы обеих сторон",68,359,900,40,22,Muted);
        Card(p,"PlayerPositionCard",32,445,494,250); Card(p,"OpponentPositionCard",540,445,508,250);
        view.PlayerPositionText=Text(p,"PlayerPositionText","ВАША ПОЗИЦИЯ\nПозиция игрока",58,477,438,194,21,Ink);
        view.OpponentPositionText=Text(p,"OpponentPositionText","ПОЗИЦИЯ ОППОНЕНТА\nПозиция оппонента",568,477,450,194,21,Ink);
        Card(p,"OpponentCard",32,720,1016,366,CardFill);
        var avatar=ImageBox(p,"OpponentAvatar",60,754,132,132,Color.white,Icon);
        avatar.preserveAspect=true; avatar.type=Image.Type.Simple; view.OpponentAvatar=avatar;
        Text(p,"OpponentName","Куратор K-07",220,767,510,44,31,Ink);
        Text(p,"OpponentRole","УМ · СИСТЕМНЫЙ ПЕРЕГОВОРЩИК",220,816,650,34,18,Muted);
        var badge=Card(p,"SpeakingBadge",850,778,160,58);
        Text(p,"SpeakingText","ГОВОРИТ",866,793,128,30,17,Muted,TextAlignmentOptions.Center);
        ImageBox(p,"QuoteRule",60,921,5,118,Red,null);
        view.OpponentText=Text(p,"OpponentText","Оппонент готовит реплику…",88,916,914,136,27,Ink);
        view.HintButton=Button(p,"HintButton","✦   Запросить подсказку     +",32,1111,500,88,false);
        view.DialogueHintText=Text(p,"HintText","Подсказка появится здесь после ответа оппонента.",60,1212,960,92,22,Muted);
        view.DialogueHintText.gameObject.SetActive(false);
        Card(p,"VoiceActionCard",32,1322,1016,174);
        var mic=ImageBox(p,"MicrophoneButton",60,1343,130,130,Red,Round,true);
        var record=mic.gameObject.AddComponent<Button>(); record.targetGraphic=mic; view.RecordButton=record;
        ImageBox(mic.transform,"MicrophoneCapsule",56,35,18,48,Color.white,Round);
        ImageBox(mic.transform,"MicrophoneCup",44,69,42,6,Color.white,Round);
        ImageBox(mic.transform,"MicrophoneStem",62,74,7,20,Color.white,Round);
        ImageBox(mic.transform,"MicrophoneBase",50,92,32,6,Color.white,Round);
        Text(p,"VoiceActionTitle","Ваш ход · ответить голосом",220,1364,770,44,28,Ink);
        Text(p,"VoiceActionSubtitle","Говорите свободно — ответ будет распознан",220,1420,770,42,22,Muted);
        Text(p,"VoiceActionArrow","→",960,1373,54,50,32,Red,TextAlignmentOptions.Center);
        view.StatusText=Text(p,"StatusText","Подготовьте ответ — запись включится после подсказки оппонента.",40,1516,1000,50,20,Muted,TextAlignmentOptions.Center);
        StatusLabels.Add(view.StatusText);
        view.StartButton=Button(p,"StartLessonButton","Новая тренировка",42,1590,484,82,false);
        view.MenuButton=Button(p,"BottomMenuButton","В меню",554,1590,484,82,false);
        view.ProgressText=Text(p,"ProgressText","Ходы: 0 / 3     ·     Методы применены: 0",70,1693,940,40,18,Muted,TextAlignmentOptions.Center);
        view.PlayerHealthText=null; view.OpponentHealthText=null;
    }
    static void BuildTranscript(GameObject page, LearningDialogView view)
    {
        var p=page.transform; Header(p,"МОДУЛЬ / ВАШ ОТВЕТ","ШАГ 01 / 03",view);
        Card(p,"TopicBadge",34,215,650,65); Text(p,"TopicBadgeText","ТЕМА · КУРАТОР K-07",55,232,610,30,17,Muted);
        Text(p,"PageTitle","ОТВЕТ РАСПОЗНАН",36,306,980,74,42,Ink);
        Text(p,"PageSubtitle","Проверьте, как система услышала вашу реплику.",36,399,980,48,24,Muted);
        Card(p,"WaveformCard",34,490,1012,300,Dark);
        Text(p,"WaveLabel","●   ГОЛОСОВОЙ ВВОД",70,524,540,36,18,Color.white);
        Text(p,"WaveTime","00:12",880,524,115,36,18,Hex("BFD4DC"),TextAlignmentOptions.Right);
        var heights=new[]{32,64,100,54,82,40,115,74,130,53,93,62,116,45,72,122,52,97,38,110,67,133,58,91,48,124,75,43,103,61,117,52,85,137,45,99,63,114,34,80,55,126,44,103,67,117};
        for(int i=0;i<heights.Length;i++) ImageBox(p,"WaveBar"+i,80+i*20,600+(137-heights[i])/2,7,heights[i],Hex("9CC7D4"),Round);
        Text(p,"WaveFooterLeft","ЗАПИСЬ ЗАВЕРШЕНА",70,725,420,35,17,Hex("BFD4DC"));
        Text(p,"WaveFooterRight","RU · 00:12",790,725,210,35,17,Hex("BFD4DC"),TextAlignmentOptions.Right);
        Card(p,"RecognizedCard",34,820,1012,335);
        Text(p,"RecognizedLabel","РАСПОЗНАННАЯ РЕЧЬ",64,850,900,34,18,Muted);
        var inputRect=Rect("RecognizedText",p,64,900,942,225);
        var inputText=inputRect.gameObject.AddComponent<TextMeshProUGUI>(); inputText.font=Font; inputText.fontSize=27;
        inputText.color=Ink; inputText.alignment=TextAlignmentOptions.TopLeft; inputText.enableWordWrapping=true;
        inputText.overflowMode=TextOverflowModes.Ellipsis; inputText.margin=new Vector4(0,0,0,0); inputText.raycastTarget=true;
        var input=inputRect.gameObject.AddComponent<TMP_InputField>(); input.textComponent=inputText; input.lineType=TMP_InputField.LineType.MultiLineNewline;
        input.pointSize=27; input.targetGraphic=inputText; inputText.raycastTarget=true;
        view.TranscriptInput=input; view.RecognizedText=inputText; view.SetRecognized("Текст распознанной речи появится здесь.");
        Text(p,"EditHint","Можно исправить текст перед отправкой",38,1180,730,40,18,Muted);
        view.SubmitButton=Button(p,"SubmitTranscriptButton","Разобрать ответ",34,1250,1012,110,true);
        view.RetakeButton=Button(p,"RetakeButton","Записать ещё раз",34,1380,1012,96,false);
        Text(p,"SpeechDisclaimer","Распознавание может быть неточным — проверьте формулировку.",36,1492,1008,40,17,Muted,TextAlignmentOptions.Center);
    }
    static void BuildFeedback(GameObject page, LearningDialogView view)
    {
        var p=page.transform; Header(p,"МОДУЛЬ / РАЗБОР","ОТВЕТ 01 / 03",view);
        Card(p,"TopicBadge",34,215,650,65); Text(p,"TopicBadgeText","ТЕМА · МЕТОД",55,232,610,30,17,Muted);
        Text(p,"PageTitle","РАЗБОР ВАШЕГО ОТВЕТА",36,306,980,75,39,Ink);
        Text(p,"PageSubtitle","Тренер оценивает ход и подсказывает, как усилить позицию.",36,399,980,52,22,Muted);
        Card(p,"QuoteCard",34,482,1012,150);
        ImageBox(p,"QuoteRule",34,482,5,150,Red,null);
        view.TranscriptText=Text(p,"InterpretationText","«Ваша реплика появится здесь»",69,510,934,108,23,Muted);
        Card(p,"TacticCard",34,664,1012,360);
        Text(p,"TacticLabel","ТАКТИКА ОТВЕТА",68,694,900,32,18,Red);
        view.TechniqueText=Text(p,"TacticTitle","ВАШ СИЛЬНЫЙ ХОД",68,735,920,48,27,Ink);
        view.FeedbackText=Text(p,"FeedbackText","Разбор ответа появится здесь.",68,798,916,175,23,Muted);
        Card(p,"CoachAdviceCard",34,1052,1012,222,PaleBlue);
        Text(p,"CoachAdviceTitle","СОВЕТ ТРЕНЕРА",68,1082,910,35,20,Ink);
        view.HintText=Text(p,"CoachAdviceText","Подсказка тренера появится здесь.",68,1132,916,112,22,Ink);
        Text(p,"TurnProgress","Ход 1 из 3",40,1295,1000,40,18,Muted);
        view.ContinueButton=Button(p,"ContinueButton","Продолжить диалог",34,1350,1012,108,true);
        view.ReturnToTranscriptButton=Button(p,"ReturnTranscriptButton","Вернуться к реплике",34,1480,1012,92,false);
        view.RetryButton=Button(p,"RetryCoachButton","Повторить разбор",34,1600,1012,88,false);
        view.StatusText=Text(p,"FeedbackStatus","Разбор готов",40,1710,1000,40,18,Muted,TextAlignmentOptions.Center);
        StatusLabels.Add(view.StatusText);
        if(view.RetryButton!=null)view.RetryButton.gameObject.SetActive(false);
    }
    static void BuildSummary(GameObject page, LearningDialogView view)
    {
        var p=page.transform; Header(p,"МОДУЛЬ / ИТОГ","РАУНД ЗАВЕРШЁН",view);
        Card(p,"SummaryHero",34,218,1012,660);
        Text(p,"HeroEyebrow","ПРОТОКОЛ УМА ПЕРЕСМОТРЕН",70,256,940,38,18,Muted,TextAlignmentOptions.Center);
        var avatar=ImageBox(p,"SummaryAvatar",377,315,326,326,Color.white,Icon); avatar.preserveAspect=true;
        Text(p,"HeroTitle","ВЫ НАШЛИ ПУТЬ К СОГЛАШЕНИЮ",70,674,940,68,36,Ink,TextAlignmentOptions.Center);
        Text(p,"HeroSubtitle","Выслушайте обе стороны и проверьте предложенный вариант на практике.",90,756,900,85,22,Muted,TextAlignmentOptions.Center);
        Card(p,"StrengthCard",34,912,494,190); Card(p,"GrowthCard",540,912,506,190);
        Text(p,"StrengthLabel","СИЛЬНЫЙ ХОД",58,937,440,28,16,Muted); Text(p,"StrengthText","Ваши удачные приёмы",58,976,440,94,21,Ink);
        Text(p,"GrowthLabel","ЗОНА РОСТА",564,937,440,28,16,Muted); Text(p,"GrowthText","Что попробовать в следующий раз",564,976,440,94,21,Ink);
        Text(p,"MethodsLabel","МЕТОДЫ В ДИАЛОГЕ",38,1120,670,34,18,Muted);
        Card(p,"MethodChip1",34,1168,320,62,PaleBlue); Text(p,"Method1","Гарвардский метод",50,1184,290,31,16,Muted,TextAlignmentOptions.Center);
        Card(p,"MethodChip2",380,1168,320,62,PaleBlue); Text(p,"Method2","BATNA",396,1184,290,31,16,Muted,TextAlignmentOptions.Center);
        Card(p,"MethodChip3",726,1168,320,62); Text(p,"Method3","ZOPA",742,1184,290,31,16,Muted,TextAlignmentOptions.Center);
        Card(p,"SummaryAdvice",34,1255,1012,175,PaleBlue);
        Text(p,"SummaryAdviceTitle","ОБРАТНАЯ СВЯЗЬ",68,1280,920,34,18,Ink);
        view.SummaryText=Text(p,"SummaryText","Ваш итог появится здесь.",68,1322,922,94,19,Ink);
        view.SummaryRestartButton=Button(p,"SummaryRepeatButton","Повторить тренировку",34,1470,1012,96,false);
        view.SummaryBackButton=Button(p,"SummaryBackButton","Вернуться в меню",34,1584,1012,100,true);
        Text(p,"SummaryDisclaimer","Итог основан на содержании реплик и применённых техниках.",40,1710,1000,40,17,Muted,TextAlignmentOptions.Center);
    }
}
