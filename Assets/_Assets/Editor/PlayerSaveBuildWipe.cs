using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Before every build: the PlayerSave asset is emptied and saved, the saved file is removed, and any leftover PlayerPrefs of older
// versions are cleared, so the build starts like a fresh install and never carries the choices of whoever tested it.
// Also under Tools > Magic > Wipe Player Save.
public class PlayerSaveBuildWipe : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report) => Wipe();

    [MenuItem("Tools/Magic/Wipe Player Save")]
    public static void Wipe()
    {
        const string path = "Assets/_Assets/Resources/PlayerSave.asset";
        var save = AssetDatabase.LoadAssetAtPath<PlayerSave>(path);
        if (save == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            save = ScriptableObject.CreateInstance<PlayerSave>();
            AssetDatabase.CreateAsset(save, path);
        }
        save.Wipe();
        EditorUtility.SetDirty(save);
        AssetDatabase.SaveAssets();
        PlayerPrefs.DeleteAll();   // the old storage, before the save asset existed
        PlayerPrefs.Save();
        Debug.Log("Player save wiped.");
    }
}
