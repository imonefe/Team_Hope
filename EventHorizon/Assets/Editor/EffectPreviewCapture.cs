using System.IO;
using UnityEditor;
using UnityEngine;

// 이펙트 미리보기 캡처 (에디터 전용). 프로젝트 루트에 fx_capture.trigger 가 있으면 1회 자동 실행.
[InitializeOnLoad]
public static class EffectPreviewCapture
{
    const string TriggerFile = "fx_capture.trigger";
    const int Size = 256;

    struct Job { public string path; public Color tint; public float[] times; public float ortho; public string name; }

    static EffectPreviewCapture()
    {
        EditorApplication.delayCall += () =>
        {
            string trig = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
            if (!File.Exists(trig)) return;
            File.Delete(trig);
            Capture();
        };
    }

    [MenuItem("EventHorizon/이펙트 미리보기 캡처")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) return;
        string outDir = @"C:\Users\USER\OneDrive\Desktop\백업\_fx_preview";
        Directory.CreateDirectory(outDir);

        var jobs = new[]
        {
            new Job { name = "SoftSmoke_green", path = "Assets/1.Prefab/Effect/VFX/FX_SoftSmoke_Test.prefab",
                      tint = new Color(0.45f, 1f, 0.35f), times = new[] { 0.3f, 0.8f, 1.5f, 2.5f, 4f }, ortho = 3f },
            new Job { name = "RicoImpact_orange", path = "Assets/1.Prefab/Effect/Enemy/FX_RicoImpact.prefab",
                      tint = new Color(1f, 0.55f, 0.15f), times = new[] { 0.03f, 0.08f, 0.15f, 0.3f, 0.55f }, ortho = 2f },
        };

        var camGo = new GameObject("__FxCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.11f, 1f);
        cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
        Vector3 origin = new Vector3(9000f, 9000f, 0f);
        camGo.transform.position = origin + new Vector3(0, 0, -10f);
        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        try
        {
            foreach (var job in jobs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(job.path);
                if (prefab == null) { Debug.LogWarning("[FxCapture] 없음: " + job.path); continue; }
                cam.orthographicSize = job.ortho;
                var sheet = new Texture2D(Size * job.times.Length, Size * 2, TextureFormat.RGB24, false);

                for (int row = 0; row < 2; row++)
                {
                    var inst = (GameObject)Object.Instantiate(prefab);
                    inst.hideFlags = HideFlags.HideAndDontSave;
                    inst.transform.position = origin;
                    var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in systems)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.useAutoRandomSeed = false; ps.randomSeed = 12345;
                        var main = ps.main;
                        main.simulationSpace = ParticleSystemSimulationSpace.Local;
                        if (row == 1)
                        {
                            float a = main.startColor.color.a;
                            main.startColor = new ParticleSystem.MinMaxGradient(new Color(job.tint.r, job.tint.g, job.tint.b, a));
                        }
                    }
                    var root = systems.Length > 0 ? systems[0] : null;
                    for (int i = 0; i < job.times.Length; i++)
                    {
                        foreach (var ps in systems) ps.Simulate(job.times[i], false, true, true);
                        cam.Render();
                        RenderTexture.active = rt;
                        // 아래쪽 행 = 틴트, 위쪽 행 = 원본 (Texture2D 는 y 가 아래부터)
                        sheet.ReadPixels(new Rect(0, 0, Size, Size), i * Size, (1 - row) * Size);
                        RenderTexture.active = null;
                    }
                    Object.DestroyImmediate(inst);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(outDir, job.name + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                Debug.Log("[FxCapture] 저장: " + job.name);
            }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }
}
