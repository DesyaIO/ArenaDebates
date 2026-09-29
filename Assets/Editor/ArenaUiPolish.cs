using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A one-shot request is applied in the actual editor. Loaded scenes are never
// replaced with disk/test copies, so their current UI edits remain intact.
[InitializeOnLoad]
public static class ArenaUiPolish
{
    const string Request = "Library/ArenaUiPolish.request";
    const string Report = "Library/ArenaUiPolish.result.txt";
    static ArenaUiPolish() { EditorApplication.delayCall += TryApply; }

    static void TryApply()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryApply;
            return;
        }
        File.Delete(Request);
        Apply();
    }

    [MenuItem("Tools/Arena/Apply legacy arrows and folder art")]
    public static void Apply()
    {
        var report = new List<string>();
        var active = SceneManager.GetActiveScene();
        try
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Arena/folder_art.png");
            if (sprite == null) throw new InvalidOperationException("folder_art.png must be imported as a Sprite.");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art/Arena" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changes = UpdateRoot(root, sprite);
                    if (changes > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        report.Add(path + ": " + changes + " changes");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    int changes = 0;
                    foreach (var root in scene.GetRootGameObjects()) changes += UpdateRoot(root, sprite);
                    if (changes > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                        report.Add(path + ": " + changes + " changes; edited " + (wasLoaded ? "live scene" : "project file"));
                    }
                }
                finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            report.Add("SUCCESS: existing scene layouts and button references preserved.");
        }
        catch (Exception error) { report.Add("FAILED: " + error); Debug.LogException(error); }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            File.WriteAllLines(Report, report);
        }
    }

    static int UpdateRoot(GameObject root, Sprite folder)
    {
        int changes = 0;
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            if (LegacyArrowPrefix.Convert(label)) { EditorUtility.SetDirty(label); changes++; }
        foreach (var card in root.GetComponentsInChildren<ArenaArchiveCard>(true))
        {
            var image = card.GetComponent<Image>();
            if (image == null) continue;
            if (image.sprite != folder || image.color != Color.white)
            {
                image.sprite = folder; image.color = Color.white; image.type = Image.Type.Sliced;
                EditorUtility.SetDirty(image); changes++;
            }
            foreach (string name in new[] { "Tab", "FrostedFill" })
            {
                var part = card.transform.Find(name);
                if (part != null && part.gameObject.activeSelf) { part.gameObject.SetActive(false); changes++; }
            }
        }
        return changes;
    }
}
