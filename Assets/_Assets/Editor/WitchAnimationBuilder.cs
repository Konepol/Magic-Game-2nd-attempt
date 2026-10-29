using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Magic > Build Witch Animations
// Makes the wand and upper-body animations of the witch as humanoid clips (muscle curves) and adds two override layers to the player animator:
//   Wand: right arm only: ready pose, tap flick, chanting pose, release thrust.
//   Body: spine, head and left arm: chanting lean, flinch when hit.
// The walk, run and jump animations underneath are not touched. Running it again rebuilds the clips and the layers.
public static class WitchAnimationBuilder
{
    const string Dir = "Assets/_Assets/Animator/Witch";
    const string ControllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
    static readonly string[] Scenes = { "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity" };
    static readonly int[] RightArm = { 46, 47, 48, 49, 50, 51, 52, 53, 54 };

    [MenuItem("Tools/Magic/Build Witch Animations")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        var names = HumanTrait.MuscleName;

        // ---------------- clips ----------------
        var hold = ArmClip("WandHold", 2.4f, true, names,
            (0f, WitchPoses.Hold), (1.2f, With(WitchPoses.Hold, 48, -0.32f, 51, 0.56f, 53, 0.18f)), (2.4f, WitchPoses.Hold));
        // a tap does not flick the wand: the arm is held out and WandAim turns the hand toward where the spell goes
        var tap = ArmClip("WandTap", 0.6f, false, names,
            (0f, WitchPoses.Hold), (0.1f, WitchPoses.Chant), (0.35f, WitchPoses.Chant), (0.6f, WitchPoses.Hold));
        var chant = ArmClip("WandChant", 1.4f, true, names,
            (0f, WitchPoses.Chant), (0.7f, With(WitchPoses.Chant, 48, 0.33f, 53, 0.15f)), (1.4f, WitchPoses.Chant));
        var release = ArmClip("WandRelease", 0.9f, false, names,
            (0f, WitchPoses.Chant), (0.12f, WitchPoses.PullBack), (0.28f, WitchPoses.Thrust),
            (0.6f, With(WitchPoses.Chant, 48, 0.15f, 49, 0.6f, 51, 0.15f)), (0.9f, WitchPoses.Hold));

        var hit = BodyClip("BodyHit", 0.5f, false, names,
            (0f, Body(0f, 0f, 0f, false)), (0.07f, Body(-0.5f, -0.35f, 0.5f, false)), (0.5f, Body(0f, 0f, 0f, false)));
        var lean = BodyClip("BodyChant", 1.4f, true, names,
            (0f, Body(-0.1f, -0.12f, 0.2f, true)), (0.7f, Body(-0.15f, -0.16f, 0.28f, true)), (1.4f, Body(-0.1f, -0.12f, 0.2f, true)));

        // ---------------- masks ----------------
        var wandMask = Mask("Mask_WandArm", AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.RightFingers);
        var bodyMask = Mask("Mask_UpperBody", AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.LeftFingers);

        // ---------------- animator ----------------
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("Witch animations: controller not found at " + ControllerPath); return; }

        Param(ctrl, "CastHold", AnimatorControllerParameterType.Bool);
        Param(ctrl, "CastTap", AnimatorControllerParameterType.Trigger);
        Param(ctrl, "CastRelease", AnimatorControllerParameterType.Trigger);
        Param(ctrl, "Hit", AnimatorControllerParameterType.Trigger);

        RemoveLayer(ctrl, "Wand");
        RemoveLayer(ctrl, "Body");

        // wand layer
        ctrl.AddLayer("Wand");
        var wl = LayerSM(ctrl, "Wand", wandMask);
        var sHold = wl.AddState("Hold"); sHold.motion = hold;
        var sTap = wl.AddState("Tap"); sTap.motion = tap;
        var sChant = wl.AddState("Chant"); sChant.motion = chant;
        var sRelease = wl.AddState("Release"); sRelease.motion = release;
        wl.defaultState = sHold;

        Trans(sHold, sTap, 0.05f, false, 0f, ("CastTap", AnimatorConditionMode.If));
        Trans(sTap, sChant, 0.1f, false, 0f, ("CastHold", AnimatorConditionMode.If));
        Trans(sTap, sHold, 0.12f, true, 0.9f);
        Trans(sHold, sChant, 0.15f, false, 0f, ("CastHold", AnimatorConditionMode.If));
        Trans(sChant, sRelease, 0.04f, false, 0f, ("CastRelease", AnimatorConditionMode.If));
        Trans(sChant, sHold, 0.2f, false, 0f, ("CastHold", AnimatorConditionMode.IfNot));
        Trans(sRelease, sChant, 0.1f, false, 0f, ("CastHold", AnimatorConditionMode.If));
        Trans(sRelease, sHold, 0.12f, true, 0.92f);

        // body layer
        ctrl.AddLayer("Body");
        var bl = LayerSM(ctrl, "Body", bodyMask);
        var bEmpty = bl.AddState("Empty");
        var bLean = bl.AddState("Chant"); bLean.motion = lean;
        var bHit = bl.AddState("Hit"); bHit.motion = hit;
        bl.defaultState = bEmpty;

        Trans(bEmpty, bLean, 0.2f, false, 0f, ("CastHold", AnimatorConditionMode.If));
        Trans(bLean, bEmpty, 0.25f, false, 0f, ("CastHold", AnimatorConditionMode.IfNot));
        var any = bl.AddAnyStateTransition(bHit);
        any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        any.hasExitTime = false;
        any.duration = 0.04f;
        any.canTransitionToSelf = false;
        Trans(bHit, bEmpty, 0.12f, true, 0.85f);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // ---------------- the player in each scene ----------------
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var caster = Object.FindAnyObjectByType<PlayerSpellCaster>();
            if (caster == null) continue;
            var root = caster.transform.root.gameObject;
            if (root.GetComponent<PlayerCastAnimator>() == null) root.AddComponent<PlayerCastAnimator>();
            if (root.GetComponent<WandAim>() == null) root.AddComponent<WandAim>();
            if (root.GetComponent<BroomDashLean>() == null && root.GetComponent<BroomFlight>() != null) root.AddComponent<BroomDashLean>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("Witch animations built.");
    }

    // ---------- clips ----------

    static Dictionary<int, float> With(Dictionary<int, float> pose, params float[] overrides)
    {
        var d = new Dictionary<int, float>(pose);
        for (int i = 0; i + 1 < overrides.Length; i += 2) d[(int)overrides[i]] = overrides[i + 1];
        return d;
    }

    // spine, chest and head, and (while chanting) the left arm a little raised and forward
    static Dictionary<int, float> Body(float spine, float chest, float head, bool chanting)
    {
        return new Dictionary<int, float>
        {
            { 0, spine }, { 3, chest }, { 12, head },
            { 39, chanting ? -0.3f : -0.14f }, { 40, chanting ? 0.4f : 0.22f }, { 42, chanting ? 0.3f : 0.48f },
        };
    }

    static AnimationClip ArmClip(string name, float length, bool loop, string[] names, params (float t, Dictionary<int, float> pose)[] keys)
    {
        return MakeClip(name, length, loop, names, new HashSet<int>(RightArm), keys);
    }

    static AnimationClip BodyClip(string name, float length, bool loop, string[] names, params (float t, Dictionary<int, float> pose)[] keys)
    {
        return MakeClip(name, length, loop, names, new HashSet<int> { 0, 3, 12, 39, 40, 42 }, keys);
    }

    static AnimationClip MakeClip(string name, float length, bool loop, string[] names, HashSet<int> muscles, (float t, Dictionary<int, float> pose)[] keys)
    {
        string path = Dir + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();

        foreach (int m in muscles)
        {
            var curve = new AnimationCurve();
            foreach (var (t, pose) in keys)
                curve.AddKey(new Keyframe(t, pose.TryGetValue(m, out float v) ? v : 0f));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), names[m]), curve);
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlendOrientation = false;
        settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.frameRate = 30f;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AvatarMask Mask(string name, params AvatarMaskBodyPart[] on)
    {
        string path = Dir + "/" + name + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, path); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var p in on) mask.SetHumanoidBodyPartActive(p, true);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // ---------- controller ----------

    static void Param(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }

    static void RemoveLayer(AnimatorController c, string name)
    {
        for (int i = c.layers.Length - 1; i >= 0; i--)
            if (c.layers[i].name == name) c.RemoveLayer(i);
    }

    static AnimatorStateMachine LayerSM(AnimatorController c, string name, AvatarMask mask)
    {
        var layers = c.layers;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == name)
            {
                layers[i].avatarMask = mask;
                layers[i].defaultWeight = 1f;
                layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
                c.layers = layers;
                return c.layers[i].stateMachine;
            }
        return null;
    }

    static void Trans(AnimatorState from, AnimatorState to, float duration, bool exitTime, float exitAt, params (string param, AnimatorConditionMode mode)[] conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = exitTime;
        t.exitTime = exitAt;
        t.duration = duration;
        t.hasFixedDuration = true;
        foreach (var (param, mode) in conditions) t.AddCondition(mode, 0f, param);
    }
}
