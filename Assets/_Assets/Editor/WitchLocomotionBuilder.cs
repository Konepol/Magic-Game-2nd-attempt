using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Magic > Build Witch Locomotion
// Gives the witch her own idle, walk, run, jump, fall and landing: the Starter Assets clips with a cute twist (a bouncier body, a head that
// tilts with every step, bigger arm swings, a stretchier jump), plus a broom-riding pose that takes over while she flies (the "Flying" parameter
// the broom already sets). The clips are copies, so the originals stay as they were. Running it again rebuilds them.
public static class WitchLocomotionBuilder
{
    const string Dir = "Assets/_Assets/Animator/Witch";
    const string SourceDir = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/";
    const string ControllerPath = SourceDir + "StarterAssetsThirdPerson.controller";

    static readonly string[] SourceFiles = { "Stand--Idle.anim.fbx", "Locomotion--Walk_N.anim.fbx", "Locomotion--Run_N.anim.fbx", "Locomotion--Walk_N_Land.anim.fbx",
                                             "Locomotion--Run_N_Land.anim.fbx", "Jump--InAir.anim.fbx", "Jump--Jump.anim.fbx" };

    delegate float Modify(string property, float t, float phase, float value, float mean);

    [MenuItem("Tools/Magic/Build Witch Locomotion")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);

        var sources = new Dictionary<string, AnimationClip>();
        foreach (var file in SourceFiles)
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SourceDir + file))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) sources[c.name] = c;

        var made = new Dictionary<string, AnimationClip>();
        foreach (var kv in sources)
        {
            Modify mod = ModifierFor(kv.Key);
            if (mod == null) continue;
            made[kv.Key] = Derive(kv.Value, "Witch_" + kv.Key, mod);
        }

        // the broom pose comes from the idle clip
        if (sources.TryGetValue("Idle", out var idle))
            made["Broom"] = Derive(idle, "Witch_BroomRide", BroomPose, 2.4f);

        // ---------------- controller ----------------
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("Witch locomotion: controller not found."); return; }
        bool hasFlying = false;
        foreach (var p in ctrl.parameters) if (p.name == "Flying") hasFlying = true;
        if (!hasFlying) ctrl.AddParameter("Flying", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st.motion is BlendTree tree)
            {
                var children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    string key = children[i].motion != null ? SourceName(children[i].motion.name) : null;
                    if (key != null && made.TryGetValue(key, out var clip)) children[i].motion = clip;
                }
                tree.children = children;
            }
            else if (st.motion != null)
            {
                string key = SourceName(st.motion.name);
                if (key != null && made.TryGetValue(key, out var clip)) st.motion = clip;
            }
        }

        // broom state
        AnimatorState ride = null, blend = null;
        foreach (var cs in sm.states)
        {
            if (cs.state.name == "BroomRide") ride = cs.state;
            if (cs.state.name == "Idle Walk Run Blend") blend = cs.state;
        }
        if (ride == null) ride = sm.AddState("BroomRide");
        ride.motion = made.ContainsKey("Broom") ? made["Broom"] : null;
        foreach (var t in sm.anyStateTransitions) if (t.destinationState == ride) { sm.RemoveAnyStateTransition(t); break; }
        foreach (var t in ride.transitions) { ride.RemoveTransition(t); break; }

        var toRide = sm.AddAnyStateTransition(ride);
        toRide.AddCondition(AnimatorConditionMode.If, 0f, "Flying");
        toRide.hasExitTime = false;
        toRide.duration = 0.25f;
        toRide.canTransitionToSelf = false;

        if (blend != null)
        {
            var back = ride.AddTransition(blend);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Flying");
            back.hasExitTime = false;
            back.duration = 0.2f;
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("Witch locomotion built: " + string.Join(", ", made.Keys));
    }

    // "Witch_Walk_N" or "Walk_N" -> "Walk_N"
    static string SourceName(string clipName)
    {
        if (clipName.StartsWith("Witch_")) clipName = clipName.Substring(6);
        switch (clipName)
        {
            case "Idle": case "Walk_N": case "Run_N": case "InAir": case "JumpStart": case "JumpLand": case "Walk_N_Land": case "Run_N_Land": return clipName;
        }
        return null;
    }

    // ---------------- how each clip is changed ----------------

    static float Around(float value, float mean, float scale) => mean + (value - mean) * scale;

    static Modify ModifierFor(string name)
    {
        switch (name)
        {
            case "Idle":
                return (prop, t, p, v, mean) =>
                {
                    float w = p * Mathf.PI * 2f;
                    if (prop == "RootT.y") return v + 0.012f * Mathf.Sin(w * 4f);                  // breathing bounce
                    if (prop == "Head Tilt Left-Right") return v + 0.14f * Mathf.Sin(w * 2f);     // a lazy head sway
                    if (prop == "Spine Left-Right") return v + 0.03f * Mathf.Sin(w * 2f + 1f);
                    if (prop == "Head Nod Down-Up") return v + 0.05f * Mathf.Sin(w * 4f + 0.6f);
                    return v;
                };
            case "Walk_N":
                return (prop, t, p, v, mean) =>
                {
                    float w = p * Mathf.PI * 2f;
                    if (prop == "RootT.y") return Around(v, mean, 2.3f);                           // bouncy steps
                    if (prop == "Head Tilt Left-Right") return v + 0.2f * Mathf.Sin(w);
                    if (prop == "Spine Left-Right") return v + 0.06f * Mathf.Sin(w);
                    if (prop.StartsWith("Left Arm") || prop.StartsWith("Right Arm")) return Around(v, mean, 1.3f);
                    return v;
                };
            case "Run_N":
                return (prop, t, p, v, mean) =>
                {
                    float w = p * Mathf.PI * 2f;
                    if (prop == "RootT.y") return Around(v, mean, 1.8f);
                    if (prop == "Head Tilt Left-Right") return v + 0.16f * Mathf.Sin(w);
                    if (prop.StartsWith("Left Arm") || prop.StartsWith("Right Arm")) return Around(v, mean, 1.25f);
                    return v;
                };
            case "InAir":
                return (prop, t, p, v, mean) =>
                {
                    float w = p * Mathf.PI * 2f;
                    if (prop == "RootT.y") return v + 0.025f * Mathf.Sin(w * 3f);
                    if (prop == "Left Arm Down-Up" || prop == "Right Arm Down-Up") return v + 0.3f;       // arms up, like floating down
                    if (prop == "Head Tilt Left-Right") return v + 0.15f * Mathf.Sin(w * 2f);
                    return v;
                };
            case "JumpStart":
            case "JumpLand":
            case "Walk_N_Land":
            case "Run_N_Land":
                return (prop, t, p, v, mean) => prop == "RootT.y" ? Around(v, mean, 1.35f) : v;           // more squash and stretch
        }
        return null;
    }

    // sitting on the broom: hips low, knees bent, one hand forward on the handle, leaning forward a little
    static float BroomPose(string prop, float t, float p, float v, float mean)
    {
        float w = p * Mathf.PI * 2f;
        switch (prop)
        {
            case "RootT.y": return 0.40f + 0.015f * Mathf.Sin(w * 2f);
            case "Left Upper Leg Front-Back": return v - 0.5f;
            case "Right Upper Leg Front-Back": return v - 0.5f;
            case "Left Lower Leg Stretch": return v - 0.4f;
            case "Right Lower Leg Stretch": return v - 0.4f;
            case "Left Arm Down-Up": return -0.35f;
            case "Left Arm Front-Back": return 0.5f;
            case "Left Forearm Stretch": return 0.4f;
            case "Spine Front-Back": return 0.12f;
            case "Spine Left-Right": return 0.03f * Mathf.Sin(w);
            case "Head Nod Down-Up": return -0.05f;
            case "Head Tilt Left-Right": return 0.1f * Mathf.Sin(w);
        }
        return v;
    }

    // ---------------- making a clip from another ----------------

    static AnimationClip Derive(AnimationClip src, string name, Modify mod, float length = 0f)
    {
        string path = Dir + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();

        float len = length > 0f ? length : src.length;
        int n = Mathf.Max(2, Mathf.CeilToInt(len * 30f));

        foreach (var binding in AnimationUtility.GetCurveBindings(src))
        {
            var cv = AnimationUtility.GetEditorCurve(src, binding);
            float mean = 0f;
            for (int i = 0; i < n; i++) mean += cv.Evaluate(Mathf.Min(i / 30f, src.length));
            mean /= n;

            var outCurve = new AnimationCurve();
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Min(i / 30f, len);
                float v = cv.Evaluate(Mathf.Min(t, src.length));
                outCurve.AddKey(new Keyframe(t, mod(binding.propertyName, t, t / len, v, mean)));
            }
            for (int i = 0; i < outCurve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(outCurve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(outCurve, i, AnimationUtility.TangentMode.Auto);
            }
            AnimationUtility.SetEditorCurve(clip, binding, outCurve);
        }

        var settings = AnimationUtility.GetAnimationClipSettings(src);
        settings.stopTime = len;
        settings.startTime = 0f;
        if (length > 0f) settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.frameRate = 30f;
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
