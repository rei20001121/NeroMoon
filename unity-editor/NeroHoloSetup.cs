// ネロの目（ホログラム）の「浮遊」と「グリッチ」のアニメーションを作り、
// Nero_FX にレイヤー「ホログラム」を追加するエディター拡張です。
// 使い方：このファイルを Assets/Nero/Editor フォルダに入れる → 上部メニュー「Nero」→「ホログラムの目アニメを作成」
// 何度実行しても大丈夫です（同じ名前のアニメとレイヤーを作り直します）。

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NeroHoloSetup
{
    // ===== 調整できる値 =====
    const string BoneName = "EyeHolo";
    const string EyeName = "MoonGlow";
    const string ControllerName = "Nero_FX";
    const string LayerName = "ホログラム";
    const string ClipPath = "Assets/Nero/Animations/EyeHolo_FloatGlitch.anim";

    const float Length = 12f;        // 1周の長さ（秒）。グリッチはこの長さの中で起きる
    const float BobPeriod = 6f;      // 上下に1往復する時間（秒）
    const float BobRatio = 0.03f;    // 上下のふわふわの幅（目の横幅に対する割合）
    const float GlitchRatio = 0.06f; // グリッチで横にずれる幅（目の横幅に対する割合）
    const float GlitchDim = 0.45f;   // グリッチ中の明るさ（1 = 通常）
    const int Fps = 60;

    // グリッチのタイミング（開始秒, 長さ秒, ずれ方向と大きさ）
    static readonly float[,] Glitches =
    {
        { 4.00f, 0.05f,  1.0f },
        { 4.08f, 0.03f, -0.6f },
        { 9.40f, 0.04f,  0.8f },
    };

    [MenuItem("Nero/ホログラムの目アニメを作成")]
    static void Create()
    {
        // --- アバターと目のボーンを探す ---
        Transform root = null, bone = null, eye = null;
        foreach (var anim in Object.FindObjectsOfType<Animator>(true))
        {
            var b = FindDeep(anim.transform, BoneName);
            var e = FindDeep(anim.transform, EyeName);
            if (b != null && e != null) { root = anim.transform; bone = b; eye = e; break; }
        }
        if (root == null)
        {
            EditorUtility.DisplayDialog("ホログラム", $"シーンの中に「{BoneName}」ボーンと「{EyeName}」があるアバターが見つかりません。\nFBXに EyeHolo ボーンが入っているか確認してください。", "OK");
            return;
        }
        var rend = eye.GetComponent<SkinnedMeshRenderer>();
        if (rend == null)
        {
            EditorUtility.DisplayDialog("ホログラム", $"「{EyeName}」に Skinned Mesh Renderer がありません。", "OK");
            return;
        }

        string bonePath = AnimationUtility.CalculateTransformPath(bone, root);
        string eyePath = AnimationUtility.CalculateTransformPath(eye, root);

        // --- 動きの向きと大きさを、見た目の大きさから決める ---
        Transform parent = bone.parent;
        float eyeWidth = Mathf.Max(rend.bounds.size.x, 0.001f);
        float parentScale = parent != null ? Mathf.Max(parent.lossyScale.x, 1e-6f) : 1f;
        Vector3 upLocal = parent != null ? parent.InverseTransformDirection(Vector3.up).normalized : Vector3.up;
        Vector3 rightLocal = parent != null ? parent.InverseTransformDirection(root.right).normalized : Vector3.right;
        float bob = eyeWidth * BobRatio / parentScale;
        float glitch = eyeWidth * GlitchRatio / parentScale;
        Vector3 basePos = bone.localPosition;

        // --- カーブを作る（1/60秒ごとに値を入れる） ---
        int n = Mathf.RoundToInt(Length * Fps);
        var px = new Keyframe[n + 1];
        var py = new Keyframe[n + 1];
        var pz = new Keyframe[n + 1];
        var em = new Keyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / Fps;
            float b = Mathf.Sin(t / BobPeriod * Mathf.PI * 2f) * bob;          // ゆっくり上下
            float g = 0f, dim = 1f;
            for (int k = 0; k < Glitches.GetLength(0); k++)
            {
                if (t >= Glitches[k, 0] && t < Glitches[k, 0] + Glitches[k, 1])
                {
                    g = Glitches[k, 2] * glitch;
                    dim = GlitchDim;
                }
            }
            Vector3 p = basePos + upLocal * b + rightLocal * g;
            px[i] = new Keyframe(t, p.x);
            py[i] = new Keyframe(t, p.y);
            pz[i] = new Keyframe(t, p.z);
            em[i] = new Keyframe(t, dim);
        }

        var clip = new AnimationClip { name = "EyeHolo_FloatGlitch", frameRate = Fps };
        SetLinear(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.x"), px);
        SetLinear(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.y"), py);
        SetLinear(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "m_LocalPosition.z"), pz);
        SetLinear(clip, EditorCurveBinding.FloatCurve(eyePath, typeof(SkinnedMeshRenderer), "material._EmissionBlend"), em);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ClipPath));
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null) AssetDatabase.DeleteAsset(ClipPath);
        AssetDatabase.CreateAsset(clip, ClipPath);

        // --- Nero_FX にレイヤーを追加 ---
        AnimatorController ctrl = null;
        foreach (var guid in AssetDatabase.FindAssets(ControllerName + " t:AnimatorController"))
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.name == ControllerName) { ctrl = c; break; }
        }
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("ホログラム", $"アニメは作りましたが、「{ControllerName}」が見つかりませんでした。", "OK");
            return;
        }

        var layers = new List<AnimatorControllerLayer>(ctrl.layers);
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (layers[i].name == LayerName) ctrl.RemoveLayer(i);
        }

        var sm = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        var state = sm.AddState("浮遊とグリッチ");
        state.motion = clip;
        state.writeDefaultValues = false;
        sm.defaultState = state;

        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = LayerName,
            defaultWeight = 1f,
            stateMachine = sm,
        });

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("ホログラム",
            "完了しました。\n\n" +
            $"・アニメ：{ClipPath}\n" +
            $"・{ControllerName} にレイヤー「{LayerName}」を追加（Weight 1、Write Defaults オフ）\n" +
            $"・ボーン：{bonePath}\n" +
            $"・上下の幅 {bob:0.0000} / グリッチの幅 {glitch:0.0000}", "OK");
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

    static void SetLinear(AnimationClip clip, EditorCurveBinding binding, Keyframe[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            float inS = i > 0 ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time) : 0f;
            float outS = i < keys.Length - 1 ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time) : 0f;
            keys[i].inTangent = inS;
            keys[i].outTangent = outS;
        }
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }
}
#endif
