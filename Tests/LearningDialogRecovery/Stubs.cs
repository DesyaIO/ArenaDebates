using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { }
    public class GameObject { public bool activeSelf; public void SetActive(bool value) => activeSelf = value; }
    public class Coroutine { public bool Stopped; }
    public class MonoBehaviour : Object
    {
        public bool isActiveAndEnabled = true;
        public readonly List<Coroutine> Active = new();
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            var handle = new Coroutine();
            if (routine.MoveNext()) Active.Add(handle);
            return handle;
        }
        public void StopCoroutine(Coroutine handle) { handle.Stopped = true; Active.Remove(handle); }
    }
    public class ScriptableObject { }
    public class MinAttribute : Attribute { public MinAttribute(int value) { } }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } }
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public struct Color { public static Color white; }
    public class Sprite { }
    public static class Debug { public static void LogWarning(object value) { } }
    public static class Time { public static float realtimeSinceStartup, unscaledDeltaTime; }
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
    }
    public static class Random { public static int Range(int min, int max) => min; }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float value) { } }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text,
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneManager { public static void LoadScene(string name) { } }
}
namespace TMPro
{
    public class TMP_Text { public string text; }
    public class TMP_InputField { public string text; }
}
namespace UnityEngine.UI
{
    public class Button
    {
        public UnityEngine.GameObject gameObject = new();
        public ButtonEvent onClick = new();
        public class ButtonEvent { public void AddListener(Action action) { } public void RemoveListener(Action action) { } }
    }
    public class Image { public UnityEngine.Sprite sprite; }
}
public class VoskSpeechToText
{
    public Action<string> OnStatusUpdated, OnTranscriptionResult;
    public float MaxRecordLength = 20;
    public void StartVoskStt(object phrases, object path, bool start, int alternatives) { }
    public bool StartRecognition() => true;
    public void StopRecognition() { }
}
public class DeepSeekClient : UnityEngine.MonoBehaviour
{
    public sealed record Request(string Prompt, Action<string> Ok, Action<string> Error,
        UnityEngine.Coroutine Handle, bool Warnings);
    public List<Request> Requests = new();
    public bool FailSynchronously;
    public UnityEngine.Coroutine SendRequest(string prompt, Action<string> ok, Action<string> error,
        bool logErrorsAsWarnings = false)
    {
        var handle = new UnityEngine.Coroutine();
        Requests.Add(new(prompt, ok, error, handle, logErrorsAsWarnings));
        if (FailSynchronously) error("configuration error");
        return handle;
    }
}
public static class SceneParams { public static string SelectedLearningMethod = "SPIN"; }
// Rendering is outside these tests; the real controller, coach and session are exercised above.
public class LearningDialogView
{
    public TMPro.TMP_Text MethodText = new(), FeedbackText = new(), TranscriptText = new(),
        ProgressText = new(), DialogueHintText = new(), OpponentText = new();
    public TMPro.TMP_InputField TranscriptInput = new();
    public UnityEngine.UI.Button StartButton = new(), RecordButton = new(), RetryButton = new(), HintButton = new();
    public UnityEngine.UI.Image OpponentAvatar = new();
    public string Screen, Status;
    public bool Busy, CanRetry;
    public void SetStageButtonActions(Action submit, Action retake, Action next, Action restart, Action back) { }
    public void SetStatus(string value) => Status = value;
    public void ShowDialogue() => Screen = "dialogue";
    public void ShowTranscriptScreen() => Screen = "transcript";
    public void ShowFeedback(bool complete) => Screen = "feedback";
    public void SetStep(int current, int total, string stage) { }
    public void ShowContext(string method, GameSession session) { }
    public void SetRecognized(string value) => TranscriptInput.text = value;
    public void SetContinueLabel(string value) { }
    public void ShowReply(LearningDialogCoach.Reply reply, bool opening) { }
    public void ShowHealth(GameSession session) { }
    public void ShowSummary(string method, int turns, int successes, string feedback, string advice,
        string[] categories, UnityEngine.Sprite sprite) => Screen = "summary";
    public void Controls(bool busy, bool ready, bool recording, bool canRecord, bool canRetry, bool finished)
    { Busy = busy; CanRetry = canRetry; }
    public void ToggleHint() { }
}
