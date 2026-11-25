using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [2026-11-24 추가] 2스테이지(폐철장)에서만 하늘(스카이박스) 색을 바꾼다.
/// SampleScene 의 RoomBGM 오브젝트에 붙어 있다. 색은 아래 인스펙터 칸에서 바꾸면 플레이 중에도 바로 보인다.
///
///  - 2스테이지 판단: 지금 진행 중인 스테이지 구성 칸의 묶음 이름이 "2S" (테스트로 방을 바로 열면 방 이름에 2S)
///    → 두 스테이지가 같이 쓰는 로비 방도 2스테이지일 때는 2스테이지 하늘이 됨
///  - 화면을 그리는 순간에만 바꾼다: 그리기 직전에 지금 하늘 재질을 복사본에 옮겨 색만 섞고, 다 그리면 원래 재질로 되돌림.
///    그래서 RoomLighting(금서고·로비의 하늘 밝기 조절)과 부딪히지 않고, 원본 재질 파일도 바뀌지 않는다.
///  - 2스테이지에 들어가고 나갈 때 fadeTime 초에 걸쳐 서서히 바뀜
/// </summary>
public class StageSkyboxColor : MonoBehaviour
{
    [Tooltip("이 묶음 이름의 칸을 진행 중이면 2스테이지 하늘")]
    public string stage2Group = "2S";
    [Tooltip("들어가고 나갈 때 색이 바뀌는 시간 (실제 시간 초)")]
    public float fadeTime = 1f;

    [Header("2스테이지 하늘 색 (체크한 것만 바꿈)")]
    [Tooltip("하늘 전체 색조")] public bool changeTint = true;
    public Color tint = new Color(0.42f, 0.36f, 0.32f, 0.5f);
    [Tooltip("아래쪽 안개 색")] public bool changeFog = true;
    public Color fogColor = new Color(0.34f, 0.27f, 0.22f, 0.5f);
    [Tooltip("위쪽 안개 색")] public bool changeTopFog = true;
    public Color topFogColor = new Color(0.28f, 0.24f, 0.21f, 0.5f);
    [Tooltip("태양 주변 번짐 색")] public bool changeMie = true;
    public Color mieTint = new Color(0.55f, 0.42f, 0.30f, 0.5f);
    [Tooltip("하늘 밝기 배율 (1 = 그대로)")]
    [Range(0f, 2f)] public float exposureScale = 1f;

    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int FogId = Shader.PropertyToID("_FogCol");
    static readonly int TopFogId = Shader.PropertyToID("_TopFogCol");
    static readonly int MieId = Shader.PropertyToID("_MieTint");
    static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    float weight;        // 0 = 원래 하늘, 1 = 2스테이지 하늘
    float target;
    Material work;       // 그릴 때만 쓰는 복사본
    Material saved;      // 그리는 동안 잠깐 비켜 둔 원래 재질
    bool swapped;

    void OnEnable()
    {
        RoomManager.OnRoomLoaded += OnRoomLoaded;
        RenderPipelineManager.beginContextRendering += BeginRender;
        RenderPipelineManager.endContextRendering += EndRender;
    }

    void OnDisable()
    {
        RoomManager.OnRoomLoaded -= OnRoomLoaded;
        RenderPipelineManager.beginContextRendering -= BeginRender;
        RenderPipelineManager.endContextRendering -= EndRender;
        EndRender(default, null);
    }

    void OnDestroy()
    {
        if (work != null) Destroy(work);
    }

    void OnRoomLoaded(Room room, bool isStart)
    {
        if (room == null) return;
        target = IsStage2(room) ? 1f : 0f;
    }

    bool IsStage2(Room room)
    {
        if (StageFlow.Active)
        {
            var e = StageFlow.CurrentEntry;
            if (e != null) return e.group == stage2Group;
        }
        return room.name.Contains("2S");
    }

    void Update()
    {
        float speed = fadeTime > 0f ? 1f / fadeTime : 1000f;
        weight = Mathf.MoveTowards(weight, target, speed * Time.unscaledDeltaTime);
    }

    void BeginRender(ScriptableRenderContext ctx, List<Camera> cams)
    {
        if (swapped || weight <= 0.0001f) return;
        Material sky = RenderSettings.skybox;
        if (sky == null) return;
        if (work == null || work.shader != sky.shader) { if (work != null) Destroy(work); work = new Material(sky) { name = sky.name + " (2스테이지)" }; }
        else work.CopyPropertiesFromMaterial(sky);   // 밝기 조절(RoomLighting) 등 지금 상태를 그대로 받아 옴

        float w = Mathf.SmoothStep(0f, 1f, weight);
        Mix(sky, TintId, changeTint, tint, w);
        Mix(sky, FogId, changeFog, fogColor, w);
        Mix(sky, TopFogId, changeTopFog, topFogColor, w);
        Mix(sky, MieId, changeMie, mieTint, w);
        if (work.HasProperty(ExposureId))
            work.SetFloat(ExposureId, sky.GetFloat(ExposureId) * Mathf.Lerp(1f, exposureScale, w));

        saved = sky;
        RenderSettings.skybox = work;
        swapped = true;
    }

    void Mix(Material from, int id, bool on, Color to, float w)
    {
        if (!on || !work.HasProperty(id)) return;
        work.SetColor(id, Color.Lerp(from.GetColor(id), to, w));
    }

    void EndRender(ScriptableRenderContext ctx, List<Camera> cams)
    {
        if (!swapped) return;
        swapped = false;
        if (RenderSettings.skybox == work) RenderSettings.skybox = saved;
        saved = null;
    }
}
