// [2026-11-26 추가] 이펙트 스타일 분석용 캡처 (에디터 전용)
// - 프로젝트 루트에 fx_style.trigger 파일이 있으면, 다음 스크립트 컴파일 때 1회 자동 실행하고 파일을 지운다.
// - 메뉴: EventHorizon/이펙트 스타일 캡처
// - 대표 이펙트마다 6개 시점을 찍어 한 장으로 저장 (바탕화면 백업/_fx_style/)
//   · 파티클은 Simulate, 애니메이터가 있으면 AnimationMode 로 같은 시점을 샘플링 (메시 참격의 재질 움직임까지)
//   · 게임 카메라와 같은 렌더러(1번)와 후처리로 찍는다
// - 장면에는 저장되지 않는 임시 오브젝트로만 찍고 바로 지운다. 기존 프리팹 · 재질은 건드리지 않는다.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class EffectStyleCapture
{
    const string TriggerFile = "fx_style.trigger";
    const int Size = 320;
    static readonly float[] Fractions = { 0.03f, 0.1f, 0.2f, 0.35f, 0.55f, 0.8f };

    static readonly string[] Paths =
    {
        "Assets/1.Prefab/Effect/weapon/Sword/FX_Sword_Slash01.prefab",
        "Assets/1.Prefab/Effect/weapon/Sword/FX_Sword_Slash03.prefab",
        "Assets/1.Prefab/Effect/weapon/Glove/FX_Strike01.prefab",
        "Assets/1.Prefab/Effect/weapon/Hammer/FX_Hammer_Slash01.prefab",
        "Assets/1.Prefab/Effect/weapon/Sword/FX_RectSlash.prefab",
        "Assets/1.Prefab/Effect/Combat/sword_hit.prefab",
        "Assets/1.Prefab/Effect/Combat/Hit.prefab",
        "Assets/1.Prefab/Effect/Heal/healing.prefab",
        "Assets/1.Prefab/Effect/Status/buff.prefab",
        "Assets/1.Prefab/Effect/Spawn/diSpon.prefab",
        "Assets/1.Prefab/Effect/Player/jump_Effect.prefab",
        "Assets/4. Art/VFX/Skill/Desh_Effect.prefab",
        "Assets/1.Prefab/Effect/Player/FX_ShieldShatter.prefab",
        "Assets/1.Prefab/Effect/Player/FX_Shield_Future.prefab",
        "Assets/1.Prefab/Effect/Enemy/FX_RicoImpact.prefab",
        "Assets/1.Prefab/Effect/Enemy/FX_BossSnipeMark.prefab",
        "Assets/1.Prefab/Effect/Enemy/Boss1_ArrowImpactFX.prefab",
        "Assets/1.Prefab/Effect/VFX/FX_CoreBreak_Slash.prefab",
        "Assets/1.Prefab/Effect/VFX/FX_SoftSmoke_Test.prefab",
        "Assets/1.Prefab/Effect/VFX/FX_FireProjectile_White.prefab",
        "Assets/1.Prefab/Effect/FX_PortalCrack.prefab",
        "Assets/1.Prefab/Skill/sword_skill_1step1.prefab",
    };

    static EffectStyleCapture()
    {
        EditorApplication.delayCall += () =>
        {
            string trig = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
            if (!File.Exists(trig)) return;
            File.Delete(trig);
            Capture();
        };
    }

    [MenuItem("EventHorizon/이펙트 스타일 캡처")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string outDir = @"C:\Users\USER\OneDrive\Desktop\백업\_fx_style";
        Directory.CreateDirectory(outDir);
        var log = new System.Text.StringBuilder();

        var camGo = new GameObject("__FxStyleCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.11f, 1f);
        cam.nearClipPlane = 0.01f; cam.farClipPlane = 200f;
        cam.allowHDR = true;
        var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
        try { urp.SetRenderer(1); } catch { }
        urp.renderPostProcessing = true;
        Vector3 origin = new Vector3(9000f, 9000f, 0f);
        var desc = new RenderTextureDescriptor(Size, Size, RenderTextureFormat.ARGB32, 24) { sRGB = true };
        var rt = new RenderTexture(desc);
        cam.targetTexture = rt;

        bool animMode = false;
        try
        {
            AnimationMode.StartAnimationMode(); animMode = true;
            foreach (var path in Paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { log.AppendLine("없음 " + path); continue; }
                string name = Path.GetFileNameWithoutExtension(path);
                try { CaptureOne(prefab, name, cam, rt, origin, outDir, log); }
                catch (System.Exception e) { log.AppendLine("실패 " + name + " : " + e.Message); }
            }
        }
        finally
        {
            if (animMode) AnimationMode.StopAnimationMode();
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            File.WriteAllText(Path.Combine(outDir, "_log.txt"), log.ToString());
            Debug.Log("[FxStyleCapture] 완료 → " + outDir);
        }
    }

    static void CaptureOne(GameObject prefab, string name, Camera cam, RenderTexture rt, Vector3 origin, string outDir, System.Text.StringBuilder log)
    {
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        inst.transform.position = origin;
        // 자동 삭제 · 자동 재생 스크립트가 끼어들지 않게 끈다 (에디터라 Awake 는 안 돌지만 안전하게)
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is ScreenDistortSource)) mb.enabled = false;

        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false; ps.randomSeed = 12345;
        }
        // 애니메이션 클립 (애니메이터마다 첫 클립)
        var clips = new List<KeyValuePair<GameObject, AnimationClip>>();
        float total = 0f;
        foreach (var an in inst.GetComponentsInChildren<Animator>(true))
        {
            var ctl = an.runtimeAnimatorController;
            if (ctl == null || ctl.animationClips.Length == 0) continue;
            var c = ctl.animationClips[0];
            clips.Add(new KeyValuePair<GameObject, AnimationClip>(an.gameObject, c));
            total = Mathf.Max(total, c.length);
        }
        foreach (var ps in systems)
        {
            var m = ps.main;
            float d = m.loop ? Mathf.Max(1f, m.duration) : m.duration + m.startDelay.constantMax + m.startLifetime.constantMax;
            total = Mathf.Max(total, Mathf.Min(d, 4f));
        }
        if (total <= 0f) total = 1f;

        // 1차: 범위
        Bounds b = new Bounds(origin, Vector3.zero); bool has = false;
        float[] times = new float[Fractions.Length];
        for (int i = 0; i < Fractions.Length; i++)
        {
            times[i] = total * Fractions[i];
            Pose(inst, systems, clips, times[i]);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer pr && pr.GetComponent<ParticleSystem>().particleCount == 0) continue;
                var rb = r.bounds;
                if (rb.size.sqrMagnitude < 1e-6f || rb.size.magnitude > 200f) continue;
                if (!has) { b = rb; has = true; } else b.Encapsulate(rb);
            }
        }
        float half = Mathf.Max(b.extents.x, b.extents.y) * 1.12f;
        cam.orthographicSize = Mathf.Clamp(half, 0.4f, 40f);
        cam.transform.position = new Vector3(b.center.x, b.center.y, origin.z - 50f);

        var sheet = new Texture2D(Size * times.Length, Size, TextureFormat.RGB24, false);
        for (int i = 0; i < times.Length; i++)
        {
            Pose(inst, systems, clips, times[i]);
            cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, Size, Size), i * Size, 0);
            RenderTexture.active = null;
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        log.AppendLine($"{name}\t길이 {total:0.###}초\t시점 {string.Join(",", System.Array.ConvertAll(times, x => x.ToString("0.###")))}\t화면 반높이 {cam.orthographicSize:0.##}\t중심 {(b.center - origin).x:0.##},{(b.center - origin).y:0.##}");
        Object.DestroyImmediate(inst);
    }

    static void Pose(GameObject inst, ParticleSystem[] systems, List<KeyValuePair<GameObject, AnimationClip>> clips, float t)
    {
        AnimationMode.BeginSampling();
        foreach (var kv in clips) AnimationMode.SampleAnimationClip(kv.Key, kv.Value, Mathf.Min(t, kv.Value.length));
        AnimationMode.EndSampling();
        foreach (var ps in systems)
            if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
                ps.Simulate(t, true, true, true);
    }
}
