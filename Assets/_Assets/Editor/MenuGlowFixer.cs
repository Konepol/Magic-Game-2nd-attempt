using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Makes sure the menu's Glow image has a MenuGlow, so the MainMenu scene looks the same in the Game view as when playing. A menu baked before
// MenuGlow existed gets it added when the scripts load or when the scene is opened. Save the scene afterwards to keep it.
[InitializeOnLoad]
static class MenuGlowFixer
{
    static MenuGlowFixer()
    {
        EditorSceneManager.sceneOpened += (scene, mode) => Fix();
        EditorApplication.delayCall += Fix;
    }

    static void Fix()
    {
        if (Application.isPlaying) return;

        var menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null) return;

        var glow = menu.transform.Find("MainMenuCanvas/Glow");
        if (glow == null || glow.GetComponent<RawImage>() == null || glow.GetComponent<MenuGlow>() != null) return;

        glow.gameObject.AddComponent<MenuGlow>();
        EditorSceneManager.MarkSceneDirty(glow.gameObject.scene);
        Debug.Log("Added Menu Glow to the main menu's Glow image. Save the scene to keep it.");
    }
}
