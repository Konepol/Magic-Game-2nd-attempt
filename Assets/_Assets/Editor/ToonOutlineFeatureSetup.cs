using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The Stylized shader's outline pass is tagged LightMode "ToonOutline", which URP does not draw by itself. This adds a Render Objects
// renderer feature to every URP renderer asset that draws that pass after all opaque objects. Runs by itself after scripts load;
// also available as ToonShade > Setup Outline Renderer Feature.
public static class ToonOutlineFeatureSetup
{
    const string FeatureName = "ToonOutlines";

    [InitializeOnLoadMethod]
    static void Auto() { EditorApplication.delayCall += Run; }

    [MenuItem("Tools/Magic/Stylized Toon/Setup Outline Renderer Feature")]
    static void Run()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/Settings/")) continue;   // only the game's own renderers, not packages or the Starter Assets samples
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            bool has = false;
            foreach (var f in data.rendererFeatures) if (f != null && f.name == FeatureName) has = true;
            if (has) continue;

            var feature = ScriptableObject.CreateInstance<RenderObjects>();
            feature.name = FeatureName;
            feature.settings.passTag = FeatureName;
            feature.settings.Event = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingOpaques;
            feature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            feature.settings.filterSettings.LayerMask = ~0;
            feature.settings.filterSettings.PassNames = new[] { "ToonOutline" };
            AssetDatabase.AddObjectToAsset(feature, data);

            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(feature);
            Debug.Log("ToonShade: added the outline Render Objects feature to " + path);
        }
        AssetDatabase.SaveAssets();
    }
}
