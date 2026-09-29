using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public class Object { public static void Destroy(object x) {} }
 public class MonoBehaviour : Object { public GameObject gameObject=new(); public bool isActiveAndEnabled=true; public List<Coroutine> Active=new(); public Coroutine StartCoroutine(IEnumerator e) { var c=new Coroutine(); Active.Add(c);e.MoveNext();return c;} public void StopCoroutine(Coroutine c) {Active.Remove(c);} }
 public class GameObject {public bool activeSelf=true;public void SetActive(bool v){activeSelf=v;}}
 public class MinAttribute:Attribute {public MinAttribute(int n){}}
 public class Coroutine {}
 public class ScriptableObject {}
 public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
 public class TooltipAttribute:Attribute { public TooltipAttribute(string s){} }
 public class TextAreaAttribute:Attribute { public TextAreaAttribute(int a,int b){} }
 public class CreateAssetMenuAttribute:Attribute {public string fileName,menuName;}
 public struct Color {public static Color white;}
 public static class Debug {public static void Log(object x){} public static void LogError(object x){} }
 public static class Time {public static float unscaledDeltaTime;public static float realtimeSinceStartup;}
 public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int CeilToInt(float f)=>(int)Math.Ceiling(f);}
 public static class Random {public static int Range(int a,int b)=>a;public static float value=>0;}
 public class WaitForSecondsRealtime {public WaitForSecondsRealtime(float x){} }
}
namespace UnityEngine.Serialization { public class FormerlySerializedAsAttribute:Attribute {public FormerlySerializedAsAttribute(string s){} } }
namespace TMPro {public class TMP_Text {public string text;public GameObject gameObject=new();} }
namespace UnityEngine.UI {public class Button {public UnityEngine.GameObject gameObject=new();public bool interactable;public Event onClick=new(); public TMPro.TMP_Text label=new();public T GetComponentInChildren<T>() where T:class =>label as T;public class Event { public void AddListener(Action a){} public void RemoveListener(Action a){} }} }
public class VoskSpeechToText {public Action<string> OnStatusUpdated,OnTranscriptionResult;public float MaxRecordLength=20;public void StartVoskStt(object a,object b,bool c,int d){} public bool StartRecognition()=>true;public void StopRecognition(){} }
public class VoiceProcessor {}
public class DeepSeekClient:MonoBehaviour {public List<(Action<string> ok,Action<string> error)> Requests=new();public Coroutine SendRequest(string p,Action<string> ok,Action<string> error){Requests.Add((ok,error));return new Coroutine();}}
public class SessionManager {public static SessionManager Instance=new();public GameSession CurrentSession;public void Save(){}public void StartNewSession(DebateTopicSO t,PositionSO a,PositionSO b,bool first){CurrentSession=new(t,a,b,first);}public void RollbackTo(int i){CurrentSession.RollbackTo(i);} }
public class VoskSpeechController {public string OpponentDifficulty="Средний";}
public enum DialogueFirstSpeaker {Player,Opponent,Random}
public static class DialogueOptions {public static bool Pending;public static DialogueFirstSpeaker FirstSpeaker;public static int Turns=5;public static string Difficulty="Средний";}
public class GameUIController {public VoskSpeechController SpeechController=new();public void OnGameStarted(){} public void OnEntryAdded(DialogueEntry e){}public void UpdateHealth(int a,int b){}public void OnGameOver(){}public void OnRollback(int i){} }

namespace UnityEngine {public static class Microphone {public static string[] devices=new[]{"test"};} public static class JsonUtility {public static T FromJson<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});}}
namespace UnityEngine.SceneManagement {public static class SceneManager{public static void LoadScene(string s){}}}
public static class SceneParams {public static string SelectedLearningMethod="SPIN";}
