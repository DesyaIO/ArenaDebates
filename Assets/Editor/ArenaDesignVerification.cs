using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class ArenaDesignVerification
{
    static int checks;
    static void Assert(bool condition,string message) { if(!condition)throw new Exception(message);checks++; }
    static void Invoke(object obj,string method) => obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(obj,null);
    static void Open(string scene)=>EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
    static void Render(string name,int width=756,int height=1480)
    {
        var canvases=Object.FindObjectsOfType<Canvas>();
        var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.933f,.953f,.965f);
        camera.orthographic=true;camera.orthographicSize=height/2f;camera.transform.position=new Vector3(0,0,-100);
        var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;
        foreach(var c in canvases)
        {
            c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=10;
            var scaler=c.GetComponent<CanvasScaler>();if(scaler!=null) {scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=Mathf.Min(width/378f,height/740f);}
        }
        foreach(var t in Object.FindObjectsOfType<TMP_Text>())t.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        foreach(var c in canvases)LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
        Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        Directory.CreateDirectory("DesignPreviews");File.WriteAllBytes("DesignPreviews/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
    public static void Verify()
    {
        foreach(var scene in new[]{"LoginScene","GoalSetupScene","MenuScene","LearningScene","ListeningScene"})
        {
            Open(scene);
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var tr in root.GetComponentsInChildren<Transform>(true))
                Assert(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)==0,"Missing script: "+scene+"/"+tr.name);
            var controllers=Object.FindObjectsOfType<MonoBehaviour>(true).Where(x=>new[]{typeof(LoginPanel),typeof(GoalSetupPanel),typeof(MenuController),typeof(LearningController),typeof(ListeningController),typeof(ArenaWelcome),typeof(ArenaPrologue)}.Contains(x.GetType()));
            foreach(var c in controllers)foreach(var f in c.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(typeof(Object).IsAssignableFrom(f.FieldType))Assert((Object)f.GetValue(c)!=null,scene+": unset "+c.GetType().Name+"."+f.Name);
        }
        Open("LoginScene");Render("01-Welcome");
        var welcome=Object.FindObjectOfType<ArenaWelcome>();
        Invoke(welcome,"Awake");
        welcome.StartButton.onClick.Invoke();
        Assert(!welcome.WelcomePanel.activeSelf&&welcome.ProfilePanel.activeSelf,"Login Start button opens profile");
        welcome.BackButton.onClick.Invoke();
        Assert(welcome.WelcomePanel.activeSelf&&!welcome.ProfilePanel.activeSelf,"Login Back button returns to welcome");
        welcome.ContinueButton.onClick.Invoke();
        Assert(!welcome.WelcomePanel.activeSelf&&welcome.ProfilePanel.activeSelf,"Login Continue button opens profile");
        welcome.Login.SetRegisterMode(true);Render("02-Profile");
        Open("GoalSetupScene");var goal=Object.FindObjectOfType<GoalSetupPanel>();Render("03-GoalSetup");
        var db=JsonUtility.FromJson<GoalDatabase>(File.ReadAllText("Assets/StreamingAssets/goals.json"));
        goal.GetType().GetField("_goalDb",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(goal,db);
        Invoke(goal,"OpenIndustries");Assert(goal.SelectionContent.childCount==db.Industries.Count,"Every industry must be selectable");Render("04-Industries");
        // New scene to clear rows without Destroy's delayed play-mode semantics.
        Open("GoalSetupScene");goal=Object.FindObjectOfType<GoalSetupPanel>();
        goal.GetType().GetField("_goalDb",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(goal,db);
        goal.GetType().GetField("_selectedIndustry",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(goal,"Производство");Invoke(goal,"OpenGoals");Render("05-Goals");
        var user=new UserData("preview","","Переговорщик");user.AddOrGetGoal("IT","Партнёрские соглашения",new List<string>{"Гарвардский метод","BATNA","SPIN"});user.ActiveGoalIndex=0;
        typeof(UserManager).GetProperty("CurrentUser").SetValue(null,user);
        Open("MenuScene");var menu=Object.FindObjectOfType<MenuController>();Invoke(menu,"UpdateUI");
        Assert(!menu.ListeningButton.interactable&&!menu.FreeDialogueButton.interactable,"Locked progression before learning");Render("06-Map");
        user.GetActiveGoal().CompletedMethods.AddRange(user.GetActiveGoal().LearningMethods);Invoke(menu,"UpdateUI");
        Assert(menu.ListeningButton.interactable&&!menu.FreeDialogueButton.interactable,"Only listening unlocked after study");
        user.GetActiveGoal().ListeningSolved=PlayerProgress.TotalListeningTasks;Invoke(menu,"UpdateUI");Assert(menu.FreeDialogueButton.interactable,"Dialogue unlocks after practice");
        menu.LearningPanel.SetActive(true);Invoke(menu,"RefreshLearningList");Assert(menu.LearningListContainer.childCount==3,"Three dossier cards for active goal");Render("07-Archive");
        menu.LearningPanel.SetActive(false);menu.ProloguePanel.SetActive(true);var prologue=menu.ProloguePanel.GetComponent<ArenaPrologue>();Invoke(prologue,"Awake");Render("08-Prologue");
        prologue.Next.onClick.Invoke();Assert(prologue.Counter.text=="02 / 09","Prologue advances");prologue.Previous.onClick.Invoke();Assert(prologue.Counter.text=="01 / 09","Prologue goes back");
        menu.ProloguePanel.SetActive(false);Render("09-MapLandscape",1480,756);
        Open("LearningScene");var learning=Object.FindObjectOfType<LearningController>();var lesson=learning.AllContent.First(x=>x!=null);learning.TitleText.text=lesson.MethodologyName;learning.DescriptionText.text=lesson.Description;learning.ExamplesText.text=lesson.Examples;Render("10-Method");
        Open("ListeningScene");var listening=Object.FindObjectOfType<ListeningController>();Render("11-Intercept");
        listening.AnswerPanel.SetActive(true);
        foreach(var method in new[]{"Гарвардский метод","BATNA","SPIN"})
        {
            var chip=Object.Instantiate(listening.MethodologyTogglePrefab,listening.MethodologyGroup.transform);chip.GetComponentInChildren<TMP_Text>().text=method;chip.GetComponent<Toggle>().group=listening.MethodologyGroup;
        }
        Render("12-Answer");listening.AnswerPanel.SetActive(false);listening.ResultPanel.SetActive(true);listening.ResultText.text="Верно! BATNA";listening.ExplanationText.text="Собеседники обсуждают альтернативу на случай, если договориться не получится.";listening.RetryButton.gameObject.SetActive(false);Render("13-Result");
        typeof(UserManager).GetProperty("CurrentUser").SetValue(null,null);
        File.WriteAllText("design-verification.txt",$"PASS: {checks} reference, hierarchy and progression checks. 13 Unity renders saved.");
    }
}
