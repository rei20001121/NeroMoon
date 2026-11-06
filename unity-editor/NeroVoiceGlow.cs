// しゃべると、ネロの目のまわりの光の輪（MoonHalo）が明るくなるようにするエディター拡張です。
// 光の輪のマテリアルの「発光テクスチャ2nd」の強さ（_Emission2ndBlend）を、声の大きさ（Voice）で動かします。
// 使い方：Assets/Nero/Editor に入れる → 上部メニュー「Nero」→「声で光の輪を光らせる」
// 何度実行しても大丈夫です（同じ名前のアニメとレイヤーを作り直します）。

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NeroVoiceGlow
{
    const string EyeName = "MoonGlow";
    const string ControllerName = "Nero_FX";
    const string LayerName = "声の光";
    const string Param = "Voice";
    const string Prop = "material._Emission2ndBlend";
    const string Dir = "Assets/Nero/Animations";

    [MenuItem("Nero/声で光の輪を光らせる")]
    static void Create()
    {
        // --- 目を探す ---
        Transform root = null, eye = null;
        foreach (var anim in Object.FindObjectsOfType<Animator>(true))
        {
            var e = FindDeep(anim.transform, EyeName);
            if (e != null && e.GetComponent<SkinnedMeshRenderer>() != null) { root = anim.transform; eye = e; break; }
        }
        if (root == null)
        {
            EditorUtility.DisplayDialog("声の光", $"シーンの中に「{EyeName}」が見つかりません。", "OK");
            return;
        }
        string eyePath = AnimationUtility.CalculateTransformPath(eye, root);

        // --- アニメ2つ（声なし＝0、声あり＝1） ---
        System.IO.Directory.CreateDirectory(Dir);
        var off = MakeClip("VoiceGlow_0", eyePath, 0f);
        var on = MakeClip("VoiceGlow_1", eyePath, 1f);

        // --- Nero_FX ---
        AnimatorController ctrl = null;
        foreach (var guid in AssetDatabase.FindAssets(ControllerName + " t:AnimatorController"))
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.name == ControllerName) { ctrl = c; break; }
        }
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("声の光", $"「{ControllerName}」が見つかりませんでした。", "OK");
            return;
        }

        // Voice パラメーターが無ければ追加（冠の声で使っていれば、そのまま使う）
        if (!ctrl.parameters.Any(p => p.name == Param))
            ctrl.AddParameter(Param, AnimatorControllerParameterType.Float);

        // 前に作ったレイヤーを消す
        var layers = new List<AnimatorControllerLayer>(ctrl.layers);
        for (int i = layers.Count - 1; i >= 0; i--)
            if (layers[i].name == LayerName) ctrl.RemoveLayer(i);

        var sm = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);

        var tree = new BlendTree
        {
            name = "声の大きさ",
            blendType = BlendTreeType.Simple1D,
            blendParameter = Param,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        tree.AddChild(off, 0f);
        tree.AddChild(on, 1f);

        var state = sm.AddState("声で光る");
        state.motion = tree;
        state.writeDefaultValues = false;
        sm.defaultState = state;

        ctrl.AddLayer(new AnimatorControllerLayer { name = LayerName, defaultWeight = 1f, stateMachine = sm });

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("声の光",
            "完了しました。\n\n" +
            $"・{ControllerName} にレイヤー「{LayerName}」を追加（Voice で 0〜1）\n" +
            $"・動かす値：{EyeName} の {Prop}\n\n" +
            "Moonhalo マテリアルの「発光テクスチャ2nd」を有効にするのを忘れないでください。", "OK");
    }

    static AnimationClip MakeClip(string name, string path, float v)
    {
        string p = $"{Dir}/{name}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(p) != null) AssetDatabase.DeleteAsset(p);
        var clip = new AnimationClip { name = name };
        var curve = new AnimationCurve(new Keyframe(0f, v), new Keyframe(1f / 60f, v));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "material._Emission2ndBlend"), curve);
        AssetDatabase.CreateAsset(clip, p);
        return clip;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}
#endif
