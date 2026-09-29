using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit editor command. No automatic scene changes on import or at runtime.
public static class ArenaDesignBuilder
{
    const string Art = "Assets/Art/Arena/";
    static TMP_FontAsset font;
    static Sprite round, pill;
    static Color Ink = Hex("22333E"), Sub = Hex("60717C"), Red = Hex("B83C46"), Paper = Hex("EEF3F6");
    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }
    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return r;
    }
    static void Stretch(RectTransform r, float inset = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f,.5f);
        r.offsetMin = Vector2.one * inset; r.offsetMax = Vector2.one * -inset;
    }
    static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool rounded = true)
    {
        var image = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        if (rounded) { image.sprite = round; image.type = Image.Type.Sliced; }
        return image;
    }
    static Image Picture(Transform parent, string name, string asset, float x,float y,float w,float h)
    {
        var i = Box(parent,name,x,y,w,h,Color.white,false);
        i.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset); return i;
    }
    static RawImage CroppedPicture(Transform parent, string name, string asset, float x,float y,float w,float h, Rect uv)
    {
        var r=Rect(name,parent,x,y,w,h).gameObject.AddComponent<RawImage>();
        r.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+asset); r.uvRect=uv; r.raycastTarget=false; return r;
    }
    static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size=14, Color? color=null, bool centered=false)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font; t.text=value; t.fontSize=size; t.color=color??Ink; t.raycastTarget=false;
        t.enableWordWrapping=true; t.alignment=centered?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
        t.overflowMode=TextOverflowModes.Overflow; t.margin=Vector4.zero;
        ArenaLegacyDots.Convert(t);
        LegacyArrowPrefix.Convert(t);
        return t;
    }
    static Image Glass(Transform parent,string name,float x,float y,float w,float h)
    {
        var shadow=Box(parent,name+"Shadow",x,y+5,w,h,new Color(.25f,.38f,.45f,.07f));
        var b=Box(parent,name,x,y,w,h,Color.white);
        var fill=Box(b.transform,"FrostedFill",1,1,w-2,h-2,new Color(.947f,.969f,.978f,.90f));
        fill.sprite=pill;
        return b;
    }
    static Button Button(Transform parent,string name,string label,float x,float y,float w=330,float h=52,bool primary=true,UnityAction action=null)
    {
        Image i = primary ? Box(parent,name,x,y,w,h,Red) : Glass(parent,name,x,y,w,h);
        i.raycastTarget=true; var b=i.gameObject.AddComponent<Button>(); b.targetGraphic=i;
        var colors=b.colors; colors.highlightedColor=new Color(.94f,.97f,1); colors.pressedColor=new Color(.8f,.84f,.88f);
        colors.disabledColor=new Color(.74f,.78f,.8f,.6f); b.colors=colors;
        var t=Text(i.transform,"Label",label,10,0,w-20,h,14,primary?Color.white:Ink,true);
        if(action!=null) UnityEventTools.AddPersistentListener(b.onClick,action); return b;
    }
    static Button Header(Transform p,string caption,UnityAction back=null)
    {
        var b=Button(p,"Back","←",22,12,40,40,false,back);
        Text(p,"Section",caption,76,21,226,23,10,Sub,true).characterSpacing=2;
        Box(p,"YearBadge",311,18,44,27,Hex("F8FAFB")); Text(p,"Year","3100",311,18,44,27,10,Sub,true);
        return b;
    }
    static void Step(Transform p,int step)
    {
        Box(p,"Step1",24,252,163,3,Red,false); Box(p,"Step2",192,252,162,3,step==2?Red:Hex("DCE5EC"),false);
    }
    static TMP_InputField Input(Transform p,string name,string hint,float x,float y,float w,float h,bool multiline=false)
    {
        var bg=Glass(p,name,x,y,w,h); bg.sprite=pill; bg.raycastTarget=true;
        var f=bg.gameObject.AddComponent<TMP_InputField>();f.targetGraphic=bg;
        var vp=Rect("TextViewport",bg.transform,14,10,w-28,h-20);vp.gameObject.AddComponent<RectMask2D>();
        var text=Text(vp,"Text","",0,0,w-28,h-20,14);Stretch(text.rectTransform);
        var placeholder=Text(vp,"Placeholder",hint,0,0,w-28,h-20,14,Hex("8999A4"));Stretch(placeholder.rectTransform);
        f.textViewport=vp;f.textComponent=(TextMeshProUGUI)text;f.placeholder=placeholder;f.fontAsset=font;
        f.pointSize=14;f.lineType=multiline?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
        return f;
    }
    static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,bool layout=true)
    {
        var vp=Box(parent,name,x,y,w,h,new Color(1,1,1,0),false);vp.raycastTarget=true;vp.gameObject.AddComponent<RectMask2D>();
        var sc=vp.gameObject.AddComponent<ScrollRect>();sc.horizontal=false;sc.vertical=true;sc.scrollSensitivity=25;sc.movementType=ScrollRect.MovementType.Clamped;
        var content=Rect("Content",vp.transform,0,0,w,h);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.sizeDelta=new Vector2(0,h);
        sc.viewport=(RectTransform)vp.transform;sc.content=content;
        if(layout)
        {
            var l=content.gameObject.AddComponent<VerticalLayoutGroup>();l.spacing=14;l.padding=new RectOffset(0,0,12,8);
            l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        }
        return sc;
    }
    static void FlexibleText(TMP_Text t)
    {
        t.gameObject.AddComponent<LayoutElement>().minHeight=30;
        t.enableAutoSizing=false;
    }
    static RectTransform Shell()
    {
        var c=new GameObject("ArenaCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=c.GetComponent<Canvas>();
        var uiCamera=Object.FindObjectOfType<Camera>(true);
        if(uiCamera==null)
        {
            uiCamera=new GameObject("UICamera",typeof(Camera)).GetComponent<Camera>();
            uiCamera.transform.position=new Vector3(0,0,-100);
        }
        uiCamera.orthographic=true;
        uiCamera.orthographicSize=10;
        uiCamera.nearClipPlane=0.1f;
        uiCamera.farClipPlane=1000f;
        uiCamera.transform.rotation=Quaternion.identity;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;
        canvas.worldCamera=uiCamera;
        canvas.planeDistance=10;
        var scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=1;
        var bg=Box(c.transform,"ScreenBackground",0,0,378,740,Paper,false);Stretch((RectTransform)bg.transform);
        var root=Rect("Design378x740",c.transform,0,0,378,740);root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
        root.localScale=new Vector3(1080f/378f,1920f/740f,1f);
        var eventSystem=Object.FindObjectOfType<EventSystem>(true);
        if(eventSystem==null) eventSystem=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).GetComponent<EventSystem>();
        eventSystem.gameObject.SetActive(true);
        var inputModule=eventSystem.GetComponent<StandaloneInputModule>();
        if(inputModule==null) inputModule=eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        inputModule.enabled=true;
        return root;
    }
    static T Controller<T>() where T:MonoBehaviour
    {
        var old=Object.FindObjectOfType<T>(true);
        var n=new GameObject(typeof(T).Name).AddComponent<T>();
        if(old!=null) { EditorUtility.CopySerialized(old,n);Object.DestroyImmediate(old); }
        foreach(var c in Object.FindObjectsOfType<Canvas>(true)) if(c.isRootCanvas) Object.DestroyImmediate(c.gameObject);
        return n;
    }
    static GameObject Panel(Transform p,string name)
    {
        var i=Box(p,name,0,0,378,740,Paper,false);i.raycastTarget=true;return i.gameObject;
    }
    static void Save(string name) { EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Scenes/"+name+".unity"); }
    static void Open(string name) { EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity"); }
    [MenuItem("Arena/Apply NewDesign to five scenes")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(Art); PrepareArt();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/niks_0 SDF.asset");
        round=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Round.png");pill=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Pill.png");
        BuildLogin();BuildGoal();BuildMenu();BuildLearning();BuildListening();
        AssetDatabase.SaveAssets();
        File.WriteAllText("design-build-complete.txt","All five scenes built with native UI.");
    }
    static void PrepareArt()
    {
        foreach(var pair in new[]{("Round",10),("Pill",28)})
        {
            int size=64;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(pair.Item2-x-.5f,x+.5f-(size-pair.Item2));
                float dy=Mathf.Max(pair.Item2-y-.5f,y+.5f-(size-pair.Item2));
                float distance=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));
                t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(pair.Item2-distance+.5f)));
            }
            t.Apply();File.WriteAllBytes(Art+pair.Item1+".png",t.EncodeToPNG());Object.DestroyImmediate(t);
        }
        AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles(Art).Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")))
        {
            var imp=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;
            imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit=100;
            if(path.EndsWith("Round.png"))imp.spriteBorder=new Vector4(12,12,12,12);
            if(path.EndsWith("Pill.png"))imp.spriteBorder=new Vector4(30,30,30,30);
            imp.SaveAndReimport();
        }
    }
    static void BuildLogin()
    {
        Open("LoginScene");var c=Controller<LoginPanel>();var root=Shell();
        var profile=Panel(root,"Profile");Header(profile.transform,"ИДЕНТИФИКАЦИЯ");
        Text(profile.transform,"Eyebrow","ЛИЧНОЕ ДОСЬЕ",24,80,330,22,10,Sub).characterSpacing=2;
        Text(profile.transform,"Title","НАСТРОЙТЕ СВОЮ\nМИССИЮ",24,105,330,90,32);
        Text(profile.transform,"Subtitle","Войдите в историю. Ваша цель и профессиональный мир определят маршрут.",24,205,330,58,14,Sub);
        c.RegisterFieldsPanel=Rect("RegistrationFields",profile.transform,24,280,330,83).gameObject;
        Text(c.RegisterFieldsPanel.transform,"Caption","Никнейм",0,0,330,20,12,Sub);
        c.FullNameInput=Input(c.RegisterFieldsPanel.transform,"Nickname","Как тебя называть?",0,25,330,50);
        Text(profile.transform,"LoginCaption","Логин",24,375,330,20,12,Sub);
        c.LoginInput=Input(profile.transform,"Login","Ваш логин",24,400,330,50);
        Text(profile.transform,"PasswordCaption","Пароль",24,465,330,20,12,Sub);
        c.PasswordInput=Input(profile.transform,"Password","Ваш пароль",24,490,330,50);c.PasswordInput.contentType=TMP_InputField.ContentType.Password;
        c.LoginButton=Button(profile.transform,"Submit","Войти",24,610);
        c.RegisterButton=Button(profile.transform,"SwitchMode","Регистрация",24,675,330,42,false);
        c.MessageText=Text(profile.transform,"Message","",24,553,330,45,12,Red);
        var welcome=Panel(root,"Welcome");Picture(welcome.transform,"City","WelcomeBackground.png",0,0,378,740);
        Text(welcome.transform,"Eyebrow","●  L100204Xi / 3100 ГОД / РЕЖИМ ИГРЫ",22,24,340,22,9,Sub).characterSpacing=1;
        var title=Text(welcome.transform,"Title","АРЕНА\nПЕРЕГОВОРОВ<color=#B83C46>.</color>",22,55,338,105,43);title.fontStyle=FontStyles.Bold;title.characterSpacing=-2;
        Text(welcome.transform,"Subtitle","Освой искусство переговоров. Верни\nлюдям право решать.",22,167,320,55,13,Ink);
        Glass(welcome.transform,"IdentityTag",263,230,103,25);Text(welcome.transform,"Identity","HUMAN / NO ID",269,236,92,15,8,Sub).characterSpacing=1;
        Glass(welcome.transform,"SystemTag",289,397,78,56);Text(welcome.transform,"System","СИСТЕМА\nНЕ УЗНАЁТ\nТЕБЯ",300,406,65,44,9,Ink);
        Box(welcome.transform,"ChapterTag",15,441,60,30,Red);Text(welcome.transform,"Chapter","01 / 03",15,441,60,30,14,Color.white,true);
        Box(welcome.transform,"CoordinatesLine",23,487,332,1,Hex("9DB8C5"),false);
        Text(welcome.transform,"Coordinates","L100204Xi                      3100 / НОВАЯ ЭРА",47,499,300,22,9,Sub);
        var flow=root.gameObject.AddComponent<ArenaWelcome>();flow.Login=c;flow.WelcomePanel=welcome;flow.ProfilePanel=profile;
        flow.StartButton=Button(welcome.transform,"Start","Начать игру",24,530);
        flow.ContinueButton=Button(welcome.transform,"Continue","Продолжить",24,592,330,50,false);
        flow.BackButton=profile.GetComponentsInChildren<Button>().First(b=>b.name=="Back");
        flow.PasswordVisibilityButton=Button(profile.transform,"PasswordVisibility","◉",312,501,30,28,false);
        Text(welcome.transform,"Footnote","●  БУДУЩЕЕ НАЧИНАЕТСЯ С ДИАЛОГА",40,665,298,28,9,Sub,true);
        profile.SetActive(false);Save("LoginScene");
    }
    static void BuildGoal()
    {
        Open("GoalSetupScene");var c=Controller<GoalSetupPanel>();var root=Shell();
        c.BackButton=Header(root,"ЛИЧНОЕ ДОСЬЕ");
        Text(root,"Eyebrow","ВАША ПЕРЕГОВОРНАЯ ЗАДАЧА",24,82,330,22,10,Sub);
        Text(root,"Title","НАСТРОЙТЕ СВОЮ\nМИССИЮ",24,111,330,80,32);
        Text(root,"Subtitle","Выберите сценарий — игра соберёт переговорную задачу под вашу цель.",24,209,330,60,14,Sub);
        Text(root,"IndustryCaption","Отрасль",24,284,330,22,12,Sub);
        c.IndustryButton=Button(root,"Industry","Выбрать отрасль  ›",24,311,330,52,false);c.IndustryText=c.IndustryButton.GetComponentInChildren<TMP_Text>();
        Text(root,"GoalCaption","Цель переговоров",24,380,330,22,12,Sub);
        c.GoalButton=Button(root,"Goal","Выбрать цель  ›",24,407,330,52,false);c.GoalText=c.GoalButton.GetComponentInChildren<TMP_Text>();
        Glass(root,"Programme",24,478,330,113);Text(root,"ProgrammeCaption","РЕКОМЕНДУЕМЫЕ МЕТОДЫ",36,489,302,20,9,Sub);
        c.MethodsText=Text(root,"Methods","",36,515,302,73,12,Ink);c.MethodsText.enableAutoSizing=true;c.MethodsText.fontSizeMin=10;c.MethodsText.fontSizeMax=12;
        c.MessageText=Text(root,"Message","",24,600,330,38,11,Sub);
        c.SaveButton=Button(root,"Save","Сохранить цель  →",24,647);
        c.ResetButton=Button(root,"Reset","Сбросить прогресс",24,704,160,30,false);
        c.LogoutButton=Button(root,"Logout","Выйти",198,704,156,30,false);
        c.SelectionPanel=Panel(root,"GoalSelection");c.SelectionTitle=Text(c.SelectionPanel.transform,"Title","Цели и методы",24,88,330,70,28);
        c.SelectionCloseButton=Header(c.SelectionPanel.transform,"ВЫБОР СЦЕНАРИЯ");
        Text(c.SelectionPanel.transform,"Hint","Выберите карточку. Список можно прокручивать.",24,164,330,44,12,Sub);
        c.SelectionScroll=Scroll(c.SelectionPanel.transform,"Options",24,215,330,490,false);c.SelectionContent=c.SelectionScroll.content;
        c.SelectionRowTemplate=Button(c.SelectionPanel.transform,"OptionTemplate","Название",24,0,318,110,false);
        var label=c.SelectionRowTemplate.GetComponentInChildren<TMP_Text>();label.alignment=TextAlignmentOptions.MidlineLeft;label.fontSize=14;Stretch(label.rectTransform,12);
        c.SelectionRowTemplate.gameObject.SetActive(false);c.SelectionPanel.SetActive(false);
        c.ResetConfirmPanel=Panel(root,"ConfirmReset");Header(c.ResetConfirmPanel.transform,"СБРОС ПРОГРЕССА");
        c.ResetMessageText=Text(c.ResetConfirmPanel.transform,"Message","Сбросить прогресс?",24,220,330,150,25);
        c.ResetYesButton=Button(c.ResetConfirmPanel.transform,"Yes","Сбросить",24,450);
        c.ResetNoButton=Button(c.ResetConfirmPanel.transform,"No","Отмена",24,514,330,52,false);
        // Both back and cancel close the same confirmation, without side effects.
        UnityEventTools.AddBoolPersistentListener(c.ResetConfirmPanel.GetComponentsInChildren<Button>().First(b=>b.name=="Back").onClick,c.ResetConfirmPanel.SetActive,false);
        c.ResetConfirmPanel.SetActive(false);Save("GoalSetupScene");
    }
    static void Nav(Transform root,MenuController c,bool archive)
    {
        Box(root,"Navigation",0,670,378,70,Hex("F6F8F9"),false);
        var map=Button(root,"MapNav","Карта",14,681,110,42,false);
        UnityEventTools.AddPersistentListener(map.onClick,c.CloseLearningPanel);
        var a=Button(root,"ArchiveNav","Архив",134,681,110,42,false);
        if(!archive)c.ArchiveNavigationButton=a;
        var profile=Button(root,"ProfileNav","Профиль",254,681,110,42,false);
        // The same profile button callback is shared with the map's profile control.
        var route=profile.gameObject.AddComponent<ArenaSceneLink>();route.SceneName="GoalSetupScene";UnityEventTools.AddPersistentListener(profile.onClick,route.Open);
        map.GetComponentInChildren<TMP_Text>().color=archive?Sub:Red;a.GetComponentInChildren<TMP_Text>().color=archive?Red:Sub;
    }
    static void BuildMenu()
    {
        Open("MenuScene");var c=Controller<MenuController>();var root=Shell();
        Picture(root,"CityMap","MapBackground.png",0,0,378,740);
        Glass(root,"Connection",22,18,77,28);Text(root,"ConnectionText","●  На связи",29,24,67,17,9,Red);
        Text(root,"CityName","L100204Xi",21,94,330,44,27);
        Text(root,"MapSubtitle","3 модуля · один путь к свободе",21,135,330,24,12,Sub);
        c.MapProgressText=Text(root,"Progress","Пройдено 0 из 3 модулей",21,159,330,22,12,Sub);
        c.FreeDialogueButton=Route(root,"Dialogue","МОДУЛЬ 03 · ФИНАЛ","Победоносное\nвыступление",46,188,173,105,out c.FreeDialogueStatusText);
        c.ListeningButton=Route(root,"Listening","МОДУЛЬ 02 · ПЕРЕХВАТ","Подслушанный разговор",179,321,176,93,out c.ListeningStatusText);
        c.LearningButton=Route(root,"Learning","МОДУЛЬ 01 · ОТКРЫТ","Засекреченные материалы",26,501,190,93,out c.LearningStatusText);
        Glass(root,"Sector",9,624,352,24);Text(root,"SectorLabel","СЕКТОР 07 · МАРШРУТ СОПРОТИВЛЕНИЯ",18,630,333,16,8,Sub);
        Nav(root,c,false);
        c.GoalButton=root.GetComponentsInChildren<Button>().First(b=>b.name=="ProfileNav");
        // MenuController owns the profile action; avoid a second persistent scene load.
        c.GoalButton.onClick=new Button.ButtonClickedEvent();
        c.LogoutButton=Button(root,"Logout","Выйти",290,17,66,28,false);
        c.GoalStatusText=Text(root,"GoalStatus","",20,595,338,28,9,Sub);
        c.LearningPanel=Panel(root,"Archive");c.ArchiveCloseButton=Header(c.LearningPanel.transform,"МОДУЛЬ 01");
        Glass(c.LearningPanel.transform,"ArchiveBadge",24,72,200,27);Text(c.LearningPanel.transform,"BadgeText","ДОСТУП К АРХИВУ ОТКРЫТ",34,79,186,17,9,Sub);
        Text(c.LearningPanel.transform,"ArchiveTitle","Засекреченные\nматериалы",24,121,330,83,31);
        Text(c.LearningPanel.transform,"ArchiveIntro","Знания, которые «умы» скрыли от людей. Открой каждый файл.",24,211,330,55,14,Sub);
        var list=Scroll(c.LearningPanel.transform,"Dossiers",24,280,330,275);c.LearningListContainer=list.content;
        var prototype=Button(root,"DossierTemplate","Название метода",0,0,330,126,false);
        prototype.gameObject.AddComponent<LayoutElement>().preferredHeight=126;
        var plate=prototype.GetComponent<Image>();plate.color=Color.white;
        plate.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"folder_art.png");plate.type=Image.Type.Sliced;
        var oldFill=prototype.transform.Find("FrostedFill");if(oldFill!=null)oldFill.gameObject.SetActive(false);
        Box(prototype.transform,"RedStroke",278,17,35,2,Red,false);
        var card=prototype.gameObject.AddComponent<ArenaArchiveCard>();card.Title=prototype.GetComponentInChildren<TMP_Text>();
        card.Title.rectTransform.anchoredPosition=new Vector2(19,-53);card.Title.rectTransform.sizeDelta=new Vector2(291,50);card.Title.alignment=TextAlignmentOptions.TopLeft;card.Title.fontSize=20;
        card.Number=Text(prototype.transform,"DossierNumber","ДОСЬЕ / 001",19,29,140,18,8,Sub);
        card.Status=Text(prototype.transform,"ReadStatus","НЕ ПРОЧИТАНО",181,29,137,18,8,Sub);
        Text(prototype.transform,"SecretStamp","СЕКРЕТНО",256,105,70,17,8,Red);
        c.LearningButtonPrefab=PrefabUtility.SaveAsPrefabAsset(prototype.gameObject,Art+"Dossier.prefab");Object.DestroyImmediate(prototype.gameObject);
        c.ArchiveProgressText=Text(c.LearningPanel.transform,"ReadCount","",24,570,330,25,11,Sub);
        c.ArchiveListeningButton=Button(c.LearningPanel.transform,"ToIntercept","К перехвату  →",24,603,330,50,false);Nav(c.LearningPanel.transform,c,true);
        c.LearningPanel.SetActive(false);
        c.ListeningPanel=Panel(root,"InterceptStart");c.ListeningCloseButton=Header(c.ListeningPanel.transform,"МОДУЛЬ 02");
        Text(c.ListeningPanel.transform,"Title","ПОДСЛУШАННЫЙ\nРАЗГОВОР",24,131,330,95,32);
        Text(c.ListeningPanel.transform,"Intro","Мы перехватили разговор двух «умов». Какая техника скрывается за их словами?",24,255,330,92,16,Sub);
        c.ListeningPanelProgressText=Text(c.ListeningPanel.transform,"Progress","",24,419,330,44,20);
        c.ListeningStartButton=Button(c.ListeningPanel.transform,"Start","Начать перехват  →",24,603);c.ListeningPanel.SetActive(false);
        c.DialoguePanel=Panel(root,"DialogueModes");c.DialogueCloseButton=Header(c.DialoguePanel.transform,"МОДУЛЬ 03");
        Text(c.DialoguePanel.transform,"Title","ПОБЕДОНОСНОЕ\nВЫСТУПЛЕНИЕ",24,141,330,100,32);
        Text(c.DialoguePanel.transform,"Intro","Выберите формат переговоров с «умом».",24,262,330,65,16,Sub);
        c.FreeDialogButton=Button(c.DialoguePanel.transform,"FreeDialogue","Свободный диалог",24,390);
        Text(c.DialoguePanel.transform,"FreeHint","Отстаивайте свою позицию без подсказок",24,450,330,40,12,Sub);
        c.LearningDialogButton=Button(c.DialoguePanel.transform,"LearningDialogue","Учебный диалог",24,510,330,52,false);
        Text(c.DialoguePanel.transform,"LearningHint","Подсказки и практика доступного метода",24,574,330,40,12,Sub);c.DialoguePanel.SetActive(false);
        BuildPrologue(root,c);Save("MenuScene");
    }
    static Button Route(Transform p,string name,string meta,string title,float x,float y,float w,float h,out TMP_Text status)
    {
        var b=Button(p,name,"",x,y,w,h,false);Object.DestroyImmediate(b.GetComponentInChildren<TMP_Text>().gameObject);
        var dot=Box(b.transform,"Marker",-8,12,16,16,Color.white);dot.sprite=pill;
        var inner=Box(dot.transform,"Dot",3,3,10,10,Red);inner.sprite=pill;
        Text(b.transform,"Module",meta,14,12,w-23,18,8,Red).characterSpacing=.7f;
        var t=Text(b.transform,"Title",title,14,32,w-23,36,12);t.fontStyle=FontStyles.Bold;
        status=Text(b.transform,"Status","",14,h-27,w-23,24,9,Sub);return b;
    }
    static void BuildPrologue(Transform root,MenuController menu)
    {
        menu.ProloguePanel=Panel(root,"Prologue");var p=menu.ProloguePanel.transform;var c=p.gameObject.AddComponent<ArenaPrologue>();
        c.Story=AssetDatabase.LoadAssetAtPath<TextAsset>(Art+"chapters.json");var back=Header(p,"ПРОЛОГ / L100204Xi");UnityEventTools.AddBoolPersistentListener(back.onClick,menu.ProloguePanel.SetActive,false);
        Picture(p,"Corridor","Corridor.png",10,62,358,235);
        c.Location=Text(p,"Location","",20,270,338,22,9,Sub);
        Glass(p,"StoryPaper",16,290,346,316);
        c.Title=Text(p,"ChapterTitle","Пробуждение",32,307,314,40,27);
        var sc=Scroll(p,"StoryScroll",32,353,314,237);c.StoryScroll=sc;c.Body=Text(sc.content,"Story","",0,0,314,220,15,Hex("3F5665"));c.Body.lineSpacing=8;FlexibleText(c.Body);
        Text(p,"StoryCaption","ИСТОРИЯ ТВОЕГО ПОЯВЛЕНИЯ",24,624,267,20,9,Sub);
        c.Counter=Text(p,"Counter","01 / 09",300,624,60,20,9,Sub);
        c.Steps=new Image[9];for(int i=0;i<9;i++)c.Steps[i]=Box(p,"Step"+i,24+i*37,651,33,2,Hex("D3DFE8"),false);
        c.Previous=Button(p,"Previous","←",24,674,51,50,false);c.Next=Button(p,"Next","Войти в город  →",85,674,269,50);
        menu.ProloguePanel.SetActive(false);
    }
    static void BuildLearning()
    {
        Open("LearningScene");var c=Controller<LearningController>();var root=Shell();
        var top=Header(root,"АРХИВ / ДОСЬЕ");
        var link=top.gameObject.AddComponent<ArenaSceneLink>();link.SceneName="MenuScene";link.OpenArchive=true;UnityEventTools.AddPersistentListener(top.onClick,link.Open);
        Text(root,"Eyebrow","СЕКРЕТНЫЕ ТЕХНИКИ ПЕРЕГОВОРОВ",24,79,330,25,10,Sub);
        c.TitleText=Text(root,"MethodTitle","ГАРВАРДСКИЙ МЕТОД",24,109,330,70,31);c.TitleText.enableAutoSizing=true;c.TitleText.fontSizeMin=23;c.TitleText.fontSizeMax=31;
        Glass(root,"Badge",24,180,170,27);Text(root,"BadgeLabel","АРХИВ СОПРОТИВЛЕНИЯ",34,187,152,18,9,Sub);
        Glass(root,"Paper",24,226,330,354);Text(root,"Declassified","РАССЕКРЕЧЕНО",46,246,285,22,10,Red).characterSpacing=2;
        var sc=Scroll(root,"MethodScroll",46,282,286,267);
        c.DescriptionText=Text(sc.content,"Description","Описание метода",0,0,286,100,14,Ink);FlexibleText(c.DescriptionText);
        var cap=Text(sc.content,"ExamplesCaption","ПРИМЕРЫ ПРИМЕНЕНИЯ",0,0,286,26,10,Red);FlexibleText(cap);
        c.ExamplesText=Text(sc.content,"Examples","Примеры",0,0,286,100,14,Sub);FlexibleText(c.ExamplesText);
        c.CompleteButton=Button(root,"Complete","Отметить как прочитанное",24,611);
        c.BackButton=Button(root,"BackToArchive","Вернуться в архив",24,674,330,50,false);Save("LearningScene");
    }
    static void BuildListening()
    {
        Open("ListeningScene");var old=Object.FindObjectOfType<ListeningController>(true);var tasks=old.AllTasks;
        var c=Controller<ListeningController>();c.AllTasks=tasks;
        // AudioSource may have belonged to the previous Canvas.
        if(c.AudioSource==null)c.AudioSource=c.gameObject.AddComponent<AudioSource>();c.AudioSource.playOnAwake=false;
        var root=Shell();c.BackButton=Header(root,"МОДУЛЬ 02");
        Text(root,"StepLabel","ШАГ 1 / 2 · СЛУШАЙ ВНИМАТЕЛЬНО",24,77,330,25,10,Sub);
        Text(root,"Title","ПОДСЛУШАННЫЙ\nРАЗГОВОР",24,108,330,86,32);
        Text(root,"Intro","Мы перехватили разговор двух «умов». Какая техника скрывается за их словами?",24,198,330,67,14,Sub);Step(root,1);
        Box(root,"AudioCard",18,248,342,263,Hex("263E4A"));
        Box(root,"InterceptDot",38,276,5,5,Red,false);
        Text(root,"AudioMeta","ПЕРЕХВАТ / 008",50,268,158,22,9,Hex("B8D3DD"));
        Text(root,"SectorMeta","СЕКТОР 07",268,268,75,22,9,Hex("B8D3DD"),true);
        Box(root,"MindIconFrame",294,286,28,28,new Color(.82f,.86f,.87f,.9f));
        CroppedPicture(root,"MindIcon","icon.jpg",297,289,22,22,new Rect(.18f,.35f,.65f,.55f));
        for(int i=0;i<48;i++) {float h=10+Mathf.Abs(Mathf.Sin(i*1.8f)*Mathf.Cos(i*.32f))*67;Box(root,"Wave"+i,39+i*6.3f,343-h/2,3,h,Hex("7EABC3"));}
        c.PlayPauseButton=Button(root,"PlayPause",">",138,383,56,56);c.PlayPauseText=c.PlayPauseButton.GetComponentInChildren<TMP_Text>();
        c.RewindButton=Button(root,"Rewind","−10",38,395,58,34,false);c.ForwardButton=Button(root,"Forward","+10",282,395,58,34,false);
        Text(root,"ReadyStatus","Готово",38,396,80,22,9,Hex("B8D3DD"));
        c.TimeText=Text(root,"Time","00:00 / 00:00",264,396,80,22,9,Hex("B8D3DD"),true);
        var slider=Rect("Progress",root,38,466,302,3).gameObject.AddComponent<Slider>();slider.interactable=false;slider.direction=Slider.Direction.LeftToRight;
        Box(slider.transform,"Track",0,0,302,3,Hex("4D707E"),false);
        var fillArea=Rect("FillArea",slider.transform,0,0,302,3);Stretch(fillArea);
        var fill=Box(fillArea,"Fill",0,0,302,3,Red,false);var fillRect=(RectTransform)fill.transform;fillRect.anchorMin=new Vector2(0,0);fillRect.anchorMax=new Vector2(0,1);fillRect.pivot=new Vector2(0,.5f);fillRect.sizeDelta=new Vector2(0,0);slider.fillRect=fillRect;slider.value=0;c.ProgressSlider=slider;
        Text(root,"Transcript",">  Текст перехвата",38,476,180,22,9,Hex("B8D3DD"));
        Box(root,"Notice",18,526,342,76,Hex("E5EEF4"));Box(root,"NoticeLine",18,526,2,76,Red,false);
        Text(root,"NoticeText","Слушай не только аргументы. Обрати внимание, что собеседники изучают, если договориться не получится.",40,539,300,55,11,Sub);
        Text(root,"DemoCaption","Демо: голосовое воспроизведение текста.",18,626,342,20,9,Sub);
        c.AnswerButton=Button(root,"Ready","Я готов ответить  →",18,653,342,52);
        c.QuestionPanel=Panel(root,"ReplayQuestion");var qb=Header(c.QuestionPanel.transform,"ПЕРЕХВАТ ЗАВЕРШЁН");
        Text(c.QuestionPanel.transform,"Title","ГОТОВЫ ОТВЕТИТЬ?",24,205,330,85,31);
        Text(c.QuestionPanel.transform,"Intro","Можно прослушать разговор ещё раз или перейти к определению метода.",24,323,330,100,16,Sub);
        c.QuestionReadyButton=Button(c.QuestionPanel.transform,"Ready","Я готов ответить  →",24,512);
        c.QuestionReplayButton=Button(c.QuestionPanel.transform,"Replay","Послушать ещё раз",24,581,330,52,false);
        UnityEventTools.AddBoolPersistentListener(qb.onClick,c.QuestionPanel.SetActive,false);
        c.AnswerPanel=Panel(root,"Answer");var ab=Header(c.AnswerPanel.transform,"МОДУЛЬ 02");UnityEventTools.AddBoolPersistentListener(ab.onClick,c.AnswerPanel.SetActive,false);
        Text(c.AnswerPanel.transform,"Step","ШАГ 2 / 2 · РАСПОЗНАЙ ТЕХНИКУ",24,77,330,25,10,Sub);
        Text(c.AnswerPanel.transform,"Title","ЧТО СТОИТ\nЗА ИХ СЛОВАМИ?",24,108,330,86,32);
        Text(c.AnswerPanel.transform,"Intro","Назови метод и объясни, какие детали разговора помогли его узнать.",24,198,330,67,14,Sub);Step(c.AnswerPanel.transform,2);
        Text(c.AnswerPanel.transform,"MethodCaption","Метод переговоров",24,279,330,24,12,Sub);
        var ms=Scroll(c.AnswerPanel.transform,"Methods",24,309,330,113);c.MethodologyGroup=ms.content.gameObject.AddComponent<ToggleGroup>();
        var proto=Glass(root,"MethodChip",0,0,330,36);proto.raycastTarget=true;var toggle=proto.gameObject.AddComponent<Toggle>();toggle.targetGraphic=proto;
        var selected=Box(proto.transform,"Selected",0,0,330,36,new Color(.72f,.24f,.28f,.14f));Stretch((RectTransform)selected.transform);toggle.graphic=selected;
        var tl=Text(proto.transform,"Label","Метод",12,0,306,36,12,Ink,true);proto.gameObject.AddComponent<LayoutElement>().preferredHeight=36;
        c.MethodologyTogglePrefab=PrefabUtility.SaveAsPrefabAsset(proto.gameObject,Art+"MethodChip.prefab");Object.DestroyImmediate(proto.gameObject);
        Text(c.AnswerPanel.transform,"ReasonCaption","По каким признакам ты определил метод?",24,437,330,36,12,Sub);
        c.ArgumentInput=Input(c.AnswerPanel.transform,"Argument","Приведи фразу из разговора и объясни, как она связана с методом…",24,477,330,120,true);
        c.SubmitButton=Button(c.AnswerPanel.transform,"Submit","Проверить ответ  →",24,615);
        c.AnswerReplayButton=Button(c.AnswerPanel.transform,"ListenAgain","Послушать ещё раз",24,680,330,44,false);
        c.ResultPanel=Panel(root,"Result");var rb=Header(c.ResultPanel.transform,"РАЗБОР ПЕРЕХВАТА");var rl=rb.gameObject.AddComponent<ArenaSceneLink>();rl.SceneName="MenuScene";UnityEventTools.AddPersistentListener(rb.onClick,rl.Open);
        c.ResultText=Text(c.ResultPanel.transform,"ResultTitle","Результат",24,133,330,130,28);
        Glass(c.ResultPanel.transform,"Feedback",24,282,330,247);var es=Scroll(c.ResultPanel.transform,"Explanation",42,300,294,208);
        c.ExplanationText=Text(es.content,"ExplanationText","Разбор ответа",0,0,294,180,14,Sub);FlexibleText(c.ExplanationText);
        c.NextButton=Button(c.ResultPanel.transform,"Next","Следующий перехват",24,584);
        c.RetryButton=Button(c.ResultPanel.transform,"Retry","Попробовать ещё раз",24,584);
        c.MenuButton=Button(c.ResultPanel.transform,"Menu","Вернуться на карту",24,653,330,52,false);
        c.QuestionPanel.SetActive(false);c.AnswerPanel.SetActive(false);c.ResultPanel.SetActive(false);Save("ListeningScene");
    }
}
