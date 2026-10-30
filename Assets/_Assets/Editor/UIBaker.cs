using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Magic > Bake UI Prefabs
// The menus are built from code. This runs that code in the editor and saves the result so you can edit it (move things, change colours,
// fonts, sizes, add pictures). When the game runs, the screens use what you saved: your layout and style win, and the code only puts the
// words and the click handlers back.
//   Main menu    -> real objects in the MainMenu scene (Assets/Scenes/MainMenu.unity), under the MainMenu object
//   Pause menu, stage results, death screen -> prefabs in Resources/UI, because every stage scene uses them
// "Bake" only makes what is missing, so it never touches your edits. "Reset" rebuilds everything from the code.
public static class UIBaker
{
    const string Dir = "Assets/_Assets/Resources/UI";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Magic/Bake UI Prefabs (missing only)")]
    public static void BakeMissing() => BakeAll(false);

    [MenuItem("Tools/Magic/Reset UI Prefabs (overwrites your edits)")]
    public static void ResetAll()
    {
        if (!EditorUtility.DisplayDialog("Reset UI", "Rebuild the main menu (in its scene), pause menu, stage results and death screen from the code? Edits you made to them are lost.", "Reset", "Cancel")) return;
        BakeAll(true);
    }

    static void BakeAll(bool overwrite)
    {
        Directory.CreateDirectory(Dir);
        UIKit.EndAdopt();      // bake from the code, never from an existing prefab

        BakeMainMenuScene(overwrite);

        // the name of the prefab is the name of the canvas the screen asks UIKit for
        Bake("PauseMenu", overwrite, host => host.AddComponent<PauseMenu>().BuildForBake());
        Bake("StageResults", overwrite, host => host.AddComponent<StageRunner>().BuildForBake());
        Bake("DeathScreen", overwrite, host => host.AddComponent<PlayerDeath>().BuildForBake());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------- the main menu, into its scene ----------

    static void BakeMainMenuScene(bool overwrite)
    {
        if (!File.Exists(MenuScenePath)) MenuBuilder.BuildMenuScene();      // makes the scene (and returns to the scene that was open)
        if (!File.Exists(MenuScenePath)) { Debug.LogError("Main menu scene not found at " + MenuScenePath); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        try
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            if (menu == null) { Debug.LogError("No MainMenu object in " + MenuScenePath + ". Run Tools > Magic > Build Main Menu Scene first."); return; }

            var existing = menu.transform.Find("MainMenuCanvas");
            if (existing != null && !overwrite)
            {
                Debug.Log("The main menu canvas is already in the scene, left as it is. Use Reset UI Prefabs to rebuild it.");
                return;
            }
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var canvas = menu.BuildForBake();         // builds under the MainMenu object
            foreach (var raw in canvas.GetComponentsInChildren<RawImage>(true)) raw.texture = null;     // the glow is made by the code at run time

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Baked the main menu into " + MenuScenePath + " (MainMenu > MainMenuCanvas).");
        }
        catch (Exception e)
        {
            Debug.LogError("Baking the main menu failed: " + e);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous) && previous != MenuScenePath) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }

    // ---------- the shared screens, into prefabs ----------

    static void Bake(string name, bool overwrite, Func<GameObject, GameObject> build)
    {
        string path = Dir + "/" + name + ".prefab";
        if (!overwrite && File.Exists(path))
        {
            Debug.Log("UI prefab " + name + " already exists, left as it is. Use Reset UI Prefabs to rebuild it.");
            return;
        }

        var host = new GameObject("UIBakeHost");
        try
        {
            var canvas = build(host);
            canvas.transform.SetParent(null, false);

            // pictures made at run time are not assets; the code makes them again
            foreach (var raw in canvas.GetComponentsInChildren<RawImage>(true)) raw.texture = null;

            PrefabUtility.SaveAsPrefabAsset(canvas, path);
            UnityEngine.Object.DestroyImmediate(canvas);
            Debug.Log("Baked UI prefab " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Baking " + name + " failed: " + e);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
