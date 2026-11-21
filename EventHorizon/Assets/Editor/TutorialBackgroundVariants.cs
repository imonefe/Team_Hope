using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 튜토리얼 회로 배경 시안 4종 (셰이더는 같고 재질 값만 다름).
/// A = 기존 M_TutorialCircuit (건드리지 않음), B·C·D = 새 재질.
/// "시안 만들기·캡처": B·C·D 재질이 없으면 만들고(있으면 값 유지), 4종을 같은 구도로 찍어 bak_1116_tutorialbg 에 저장.
/// "시안 적용": TutorialRoom 의 CircuitBackground 재질만 교체. 나머지 재질은 지우지 않는다.
/// </summary>
public static class TutorialBackgroundVariants
{
    const string RoomPath = "Assets/1.Prefab/Room/Tutorial/TutorialRoom.prefab";
    const string Dir = "Assets/4. Art/Materials/Shader/";
    // [2026-11-16 2차] 색·크기만 다른 시안 → 회로 모양 자체가 다른 시안 4종으로 교체 (예전 색 시안 재질은 지우지 않고 남겨 둠)
    static readonly string[] Paths =
    {
        Dir + "M_TutorialCircuit.mat",               // A 직교 격자
        Dir + "M_TutorialCircuit_B_Diagonal.mat",    // B 45° 사선
        Dir + "M_TutorialCircuit_C_Bus.mat",         // C 평행 버스
        Dir + "M_TutorialCircuit_D_Hex.mat",         // D 육각 벌집
        Dir + "M_TutorialCircuit_E_GridHex.mat",     // E 격자 보조선 + 벌집 주 회로선 [2026-11-16 4차]
        Dir + "M_TutorialCircuit_F_ThinGrid.mat",    // F E에서 벌집을 뺀 가는 격자만 [2026-11-16 6차]
        Dir + "M_TutorialCircuit_G_DataBlue.mat",    // G 데이터 패널 파랑 (레퍼런스) [2026-11-16 7차]
        Dir + "M_TutorialCircuit_H_DataPink.mat",    // H 데이터 패널 분홍 (레퍼런스)
    };
    static readonly string[] Names = { "A_직교격자", "B_45도사선", "C_평행버스", "D_육각벌집", "E_격자벌집", "F_가는격자", "G_데이터파랑", "H_데이터분홍" };
    static readonly string[] StyleOrder = { "_STYLE_GRID", "_STYLE_DIAGONAL", "_STYLE_BUS", "_STYLE_HEX", "_STYLE_HYBRID", "_STYLE_DATA" };
    static readonly string[] Keywords = { "_STYLE_GRID", "_STYLE_DIAGONAL", "_STYLE_BUS", "_STYLE_HEX", "_STYLE_HYBRID", "_STYLE_HYBRID", "_STYLE_DATA", "_STYLE_DATA" };

    /// <summary>시안 값 넣기 (색은 4종 모두 같게 — 모양만 비교)</summary>
    static void Setup(Material m, int i)
    {
        foreach (var k in Keywords) m.DisableKeyword(k);
        m.EnableKeyword(Keywords[i]);
        // _Style 은 셰이더 모양 목록(Grid, Diagonal, Bus, Hex, Hybrid, Data)의 번호여야 함 — 시안 번호가 아님
        // (예전에 시안 번호를 넣어 F·G·H 의 모양이 틀어졌던 문제 수정)
        m.SetFloat("_Style", System.Array.IndexOf(StyleOrder, Keywords[i]));
        m.SetColor("_BgColor", new Color(0.008f, 0.012f, 0.02f));
        m.SetColor("_IdleColor", new Color(0.03f, 0.07f, 0.09f));
        m.SetColor("_GlowColor", new Color(0.25f, 1.4f, 1.8f));
        m.SetColor("_HeadColor", new Color(1.2f, 2.2f, 2.6f));
        m.SetFloat("_Halo", 0.35f); m.SetFloat("_Intensity", 0.8f);
        m.SetFloat("_Period", 6f); m.SetFloat("_Fade", 8f); m.SetFloat("_Stagger", 0.03f); m.SetFloat("_HeadWidth", 0.012f);
        // [2026-11-16 3차] 고품질: 깊이·굵기 위계·흰 심·데이터 흐름·점화 깜빡임·칩·화면 마감
        m.SetFloat("_MainRatio", 0.3f); m.SetFloat("_BackScale", 1.9f); m.SetFloat("_BackStrength", 0.3f); m.SetFloat("_BackDelay", 0.06f);
        m.SetFloat("_PacketSpeed", 3f); m.SetFloat("_PacketAmount", 1.2f); m.SetFloat("_BootFlicker", 0.7f);
        m.SetFloat("_HotCore", 0.6f); m.SetFloat("_Vignette", 0.35f);
        m.SetFloat("_ChipChance", i >= 3 ? 0f : (i == 2 ? 0.25f : 0.35f));
        switch (i)
        {
            case 0: m.SetFloat("_CellSize", 1.5f); m.SetFloat("_LineWidth", 0.04f);  m.SetFloat("_Density", 0.45f); m.SetFloat("_Seed", 7f); break;
            case 1: m.SetFloat("_CellSize", 1.6f); m.SetFloat("_LineWidth", 0.04f);  m.SetFloat("_Density", 0.4f);  m.SetFloat("_Seed", 5f); break;
            case 2: m.SetFloat("_CellSize", 0.9f); m.SetFloat("_LineWidth", 0.06f);  m.SetFloat("_Density", 0.5f);  m.SetFloat("_Seed", 9f); m.SetFloat("_Stagger", 0.1f); break;
            case 3: m.SetFloat("_CellSize", 1.8f); m.SetFloat("_LineWidth", 0.035f); m.SetFloat("_Density", 0.45f); m.SetFloat("_Seed", 3f); break;
            case 4: // 격자 보조선(가늘게) + 벌집 주 회로선(3겹 관·마디 끊김)
                m.SetFloat("_CellSize", 1.2f); m.SetFloat("_LineWidth", 0.035f); m.SetFloat("_Density", 0.4f); m.SetFloat("_Seed", 7f);
                m.SetFloat("_MainRatio", 0f);
                m.SetFloat("_HexScale", 3f); m.SetFloat("_HexDensity", 0.55f); m.SetFloat("_HexGap", 0f); m.SetFloat("_HexPatch", 0.12f); m.SetFloat("_HexFade", 0.18f);
                m.SetFloat("_MainWidth", 2.0f); m.SetFloat("_RailOffset", 3f); m.SetFloat("_ThinStrength", 0.5f);
                break;
            case 5: // E 와 같고 벌집만 끔 (벌집 면적 0). 벌집이 빠진 만큼 가는 격자를 조금 밝게
                m.SetFloat("_CellSize", 1.2f); m.SetFloat("_LineWidth", 0.035f); m.SetFloat("_Density", 0.4f); m.SetFloat("_Seed", 7f);
                m.SetFloat("_MainRatio", 0f);
                m.SetFloat("_HexScale", 3f); m.SetFloat("_HexDensity", 0f); m.SetFloat("_HexGap", 0f); m.SetFloat("_HexPatch", 0.12f); m.SetFloat("_HexFade", 0.18f);
                m.SetFloat("_MainWidth", 2.0f); m.SetFloat("_RailOffset", 3f); m.SetFloat("_ThinStrength", 0.8f);
                break;
            case 6: // 데이터 패널 파랑
            case 7: // 데이터 패널 분홍
                // [9차] 레퍼런스 실측 반영: 그림자는 짙은 남색, 빛은 푸른 흰색 / 패널 대비 낮게 / 켜짐 약 3%
                bool pink = i == 7;
                m.SetColor("_BgColor", pink ? new Color(0.03f, 0.012f, 0.025f) : new Color(0.012f, 0.014f, 0.04f));
                m.SetColor("_IdleColor", pink ? new Color(0.13f, 0.05f, 0.08f) : new Color(0.05f, 0.06f, 0.13f));
                m.SetColor("_GlowColor", pink ? new Color(1.5f, 0.55f, 0.6f) : new Color(0.5f, 0.85f, 1.5f));
                m.SetColor("_HeadColor", pink ? new Color(1.8f, 1.3f, 1.35f) : new Color(1.3f, 1.55f, 1.7f));
                m.SetFloat("_Period", 6f); m.SetFloat("_Fade", 6f); m.SetFloat("_HeadWidth", 0.01f);
                m.SetFloat("_BlockDensity", 0.6f); m.SetFloat("_DotSpacing", 0.1f); m.SetFloat("_DotDensity", 0.3f);
                m.SetFloat("_LitChance", 0.028f); m.SetFloat("_FlickerRate", 0.8f); m.SetFloat("_PanelGlow", 0.9f);
                m.SetFloat("_ScanGlitch", 0.3f); m.SetFloat("_Parallax", 0.6f);
                m.SetFloat("_Vignette", 0.45f); m.SetFloat("_Intensity", 0.6f); m.SetFloat("_Seed", 4f);
                break;
        }
    }

    static Material Load(int i, bool create)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(Paths[i]);
        if (m == null && create)
        {
            m = new Material(Shader.Find("EventHorizon/TutorialCircuitBG"));
            AssetDatabase.CreateAsset(m, Paths[i]);
        }
        if (m != null && create) { Setup(m, i); EditorUtility.SetDirty(m); }
        return m;
    }

    [MenuItem("EventHorizon/튜토리얼 배경 시안 만들기·캡처")]
    public static void MakeAndCapture()
    {
        var mats = Enumerable.Range(0, Paths.Length).Select(i => Load(i, true)).ToArray();
        AssetDatabase.SaveAssets();

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath), scene);
        foreach (var c in inst.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
        SpriteRenderer bg = inst.transform.Find("CircuitBackground").GetComponent<SpriteRenderer>();
        Transform spawner = inst.transform.Find("PlayerSpawner");
        Vector3 focus = spawner != null ? spawner.position : bg.transform.position;

        var camGo = new GameObject("CapCam");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
        Camera cam = camGo.AddComponent<Camera>();
        cam.scene = scene; cam.orthographic = true; cam.orthographicSize = 9f;
        cam.transform.position = new Vector3(focus.x + 14f, focus.y + 6f, -20);   // 1·2구간이 보이는 구도 (실제 게임 화면 크기와 비슷)
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;

        // 물결 앞머리를 화면 가운데보다 조금 위에 고정 (시안마다 주기가 달라도 같은 순간으로 비교)
        Bounds bb = bg.bounds;
        float headUV = Mathf.Clamp01((cam.transform.position.y + 2f - bb.min.y) / bb.size.y);
        var mpb = new MaterialPropertyBlock();
        string gifDir = System.IO.Path.Combine(Application.dataPath, "../bak_1116_tutorialbg/gif");
        System.IO.Directory.CreateDirectory(gifDir);
        const int Frames = 36, gw = 800, gh = 450;
        var grt = new RenderTexture(gw, gh, 24);

        for (int i = 0; i < Paths.Length; i++)
        {
            if (mats[i] == null) continue;
            bg.sharedMaterial = mats[i];

            // 정지 이미지
            mpb.Clear();
            mpb.SetFloat("_PhaseOverride", 1f - headUV + 0.0001f);
            mpb.SetFloat("_TimeOverride", 1.37f);
            bg.SetPropertyBlock(mpb);
            cam.targetTexture = rt;
            cam.Render();
            Save(rt, w, h, System.IO.Path.Combine(Application.dataPath, $"../bak_1116_tutorialbg/variant_{Names[i]}.png"));

            // E·F 는 확대 한 장 더 (선 품질 확인용)
            if (i >= 4)
            {
                float keep = cam.orthographicSize;
                Vector3 keepPos = cam.transform.position;
                cam.orthographicSize = 3.5f;
                cam.transform.position = keepPos + new Vector3(-4f, 4.5f, 0);
                cam.Render();
                Save(rt, w, h, System.IO.Path.Combine(Application.dataPath, $"../bak_1116_tutorialbg/variant_{Names[i]}_확대.png"));
                cam.orthographicSize = keep;
                cam.transform.position = keepPos;
            }

            // 움직이는 GIF 프레임: 물결 한 주기를 36장으로 (이미 뽑아 둔 A~F 는 건너뜀)
            if (i < 6) continue;
            // [9차] 데이터 패널은 레퍼런스와 같은 30fps 로 3초 (1/30초 단위 글리치를 담으려고)
            float period = mats[i].GetFloat("_Period");
            int frames = 90; float dt = 1f / 30f;
            cam.targetTexture = grt;
            for (int f = 0; f < frames; f++)
            {
                mpb.Clear();
                mpb.SetFloat("_PhaseOverride", -1f);
                mpb.SetFloat("_TimeOverride", 100f * period + period * (1f - headUV) - period * 0.25f + f * dt);
                bg.SetPropertyBlock(mpb);
                cam.Render();
                Save(grt, gw, gh, System.IO.Path.Combine(gifDir, $"{Names[i]}_{f:00}.png"));
            }
        }
        cam.targetTexture = null;
        Object.DestroyImmediate(grt);
        Object.DestroyImmediate(rt);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        Debug.Log("[TutorialBG] 시안 4종 캡처: bak_1116_tutorialbg/variant_*.png");
    }

    static void Save(RenderTexture rt, int w, int h, string path)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void Apply(int i)
    {
        Material m = Load(i, false);
        if (m == null) m = Load(i, true);
        GameObject root = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            Transform bg = root.transform.Find("CircuitBackground");
            if (bg == null) { Debug.LogError("[TutorialBG] CircuitBackground 없음 — 먼저 '튜토리얼 배경 배치' 실행"); return; }
            bg.GetComponent<SpriteRenderer>().sharedMaterial = m;
            PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
            Debug.Log($"[TutorialBG] 배경 시안 적용: {Names[i]}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/A 직교 격자")] static void A() => Apply(0);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/B 45도 사선")] static void B() => Apply(1);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/C 평행 버스")] static void C() => Apply(2);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/D 육각 벌집")] static void D() => Apply(3);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/E 격자 + 벌집")] static void E() => Apply(4);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/F 가는 격자만")] static void F() => Apply(5);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/G 데이터 패널 파랑")] static void G() => Apply(6);
    [MenuItem("EventHorizon/튜토리얼 배경 시안 적용/H 데이터 패널 분홍")] static void H() => Apply(7);
}
