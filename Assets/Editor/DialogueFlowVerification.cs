using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit batch entry point; never runs automatically in the user's editor.
public static class DialogueFlowVerification
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void UpdateScenesAndVerify()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DialogScene.unity");
        var ui = Object.FindObjectOfType<GameUIController>(true);
        Check(ui != null && ui.SpeechController != null && ui.RecordButton != null && ui.ResultText != null, "Dialog wiring");
        Check(ui.HistoryUI != null && ui.HistoryUI.HistoryEntryPrefab != null, "History wiring");
        ui.PlayerHealthText.text = "Ваши ходы: 0/5";
        ui.OpponentHealthText.text = "Оппонент: 0/5";
        var card = Object.FindObjectsOfType<RectTransform>(true).First(x => x.name == "ConversationCard");
        if (card.GetComponent<ScrollRect>() == null)
        {
            var viewport = new GameObject("ConversationViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(card, false);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(30, 30); viewport.offsetMax = new Vector2(-30, -30);
            viewport.GetComponent<Image>().color = Color.clear;
            var content = ui.ResultText.rectTransform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            ui.ResultText.enableAutoSizing = false; ui.ResultText.fontSize = 32;
            ui.ResultText.enableWordWrapping = true; ui.ResultText.overflowMode = TextOverflowModes.Overflow;
            ui.ResultText.alignment = TextAlignmentOptions.TopLeft;
            ui.ResultText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
        }
        foreach (var text in Object.FindObjectsOfType<TMP_Text>(true))
            if (text.name == "IntroDescription")
                text.text = "Отстаивайте свою позицию и отвечайте голосом.\nВыберите сложность, первый ход и длительность диалога.\nПосле каждого ответа появится разбор.";
        ui.ShowHistoryButton.GetComponentInChildren<TMP_Text>().text = "Разбор диалога";
        EditorUtility.SetDirty(ui);
        Check(Vector2.Distance(((RectTransform)ui.ShowHistoryButton.transform).anchoredPosition,
            ((RectTransform)ui.RestartButton.transform).anchoredPosition) > 120, "Result buttons must not overlap");
        DialogueOptions.Difficulty = "Средний";
        DialogueOptions.FirstSpeaker = DialogueFirstSpeaker.Random;
        DialogueOptions.Turns = 5;
        DialogueOptions.Show(ui.TopicText.font, () => {});
        var overlay = GameObject.Find("DialogueOptionsCanvas");
        var buttons = overlay.GetComponentsInChildren<Button>();
        Check(buttons.Length == 5, "All settings controls exist");
        buttons[0].onClick.Invoke();
        Check(DialogueOptions.Difficulty == "Сложный", "Difficulty selector");
        buttons[1].onClick.Invoke();
        Check(DialogueOptions.FirstSpeaker == DialogueFirstSpeaker.Player, "First speaker selector");
        buttons[2].onClick.Invoke();
        Check(DialogueOptions.Turns == 10, "Turn limit selector");
        DialogueOptions.Pending = true;
        var manager = Object.FindObjectOfType<GameManager>();
        var previousFirst = manager.FirstSpeaker;
        var previousTurns = manager.TurnsPerParticipant;
        var previousDifficulty = ui.SpeechController.OpponentDifficulty;
        typeof(GameManager).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, null);
        Check(manager.FirstSpeaker == DialogueFirstSpeaker.Player && manager.TurnsPerParticipant == 10 &&
            ui.SpeechController.OpponentDifficulty == "Сложный", "Settings transferred into public fields");
        DialogueOptions.Pending = false;
        Object.DestroyImmediate(overlay);
        manager.FirstSpeaker = previousFirst;
        manager.TurnsPerParticipant = previousTurns;
        ui.SpeechController.OpponentDifficulty = previousDifficulty;
        VerifyScripts();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorSceneManager.OpenScene("Assets/Scenes/LearningDialog.unity");
        var view = Object.FindObjectOfType<LearningDialogView>(true);
        Check(view != null, "Training view exists");
        view.ShowHealth(null);
        VerifyScripts();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        foreach (int limit in new[] {5, 10}) foreach (bool playerFirst in new[] {true, false})
        {
            var session = new GameSession { PlayerGoesFirst = playerFirst, TurnsPerParticipant = limit };
            for (int i = 0; i < limit * 2; i++)
            {
                bool player = session.IsPlayerTurn;
                Check(session.AddEntry(player, "reply", "type", "analysis", -15) != null, "Accepted turn");
                Check(session.AddEntry(player, "duplicate", "type", "", -15) == null, "Duplicate rejection");
                Check(session.IsGameOver == (i == limit * 2 - 1), "Exact ending");
            }
            Check(session.PlayerTurns == limit && session.OpponentTurns == limit, "Balanced turn counts");
            Check(session.PlayerHealth == 100 && session.OpponentHealth == 100, "No health changes");
        }
        Debug.Log("DIALOGUE_FLOW_VERIFIED: scenes wired, HP hidden, limits 5/10 for both first speakers");
    }

    static void VerifyScripts()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script on " + transform.name);
    }
}
