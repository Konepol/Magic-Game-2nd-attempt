using UnityEditor;
using UnityEngine;

// A Rebuild button on the Forest Map: change a number, press it, and the forest in the Scene view follows.
[CustomEditor(typeof(ForestMap))]
public class ForestMapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("The forest is made from these numbers: while playing, and as a preview in the Scene view (never saved). "
            + "Its objects appear under \"Forest (generated)\" in the hierarchy, one per chunk, but edit the numbers here, not the objects: "
            + "they are rebuilt from them. Chunks the camera does not look at are not drawn while playing.", MessageType.Info);
        if (GUILayout.Button("Rebuild forest")) foreach (var t in targets) ((ForestMap)t).Rebuild();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
