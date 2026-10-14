using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면을 어둡게 하고, 빛줄기(LightBeam)가 닿는 곳만 원래 색으로 되살리는 렌더러 기능 (URP Renderer Feature).
/// Renderer3D_LightingTest 렌더러에 등록되어 있다. RoomDarkness 가 있는 방에서만 동작하고, 그 외에는 비용이 없다.
///
/// 순서
///  1) 빛 지도 그리기 (카메라마다): 이 카메라에 보이는 빛줄기들의 "LightMask" 단계만 따로 흑백(색) 그림에 더해 그림.
///     - 3D 배경 카메라는 3D 배경 속 빛줄기를, 2D 카메라는 2D 빛줄기를 각자 자기 시점으로 그린다.
///     - 이번 프레임에 가장 먼저 그리는 카메라가 빛 지도를 검게 비운다.
///     - 마지막 카메라에서는 플레이어 주변 빛도 빛 지도에 더한다.
///  2) 합치기 (마지막 카메라 한 대만): 화면색 × (어두운 바탕 + 빛 지도). 피격·퍼즈 등 글리치 효과보다 먼저 한다.
///
/// 카메라가 여러 대일 때(SampleScene 의 Main Camera_3d + Camera_2D) 합치기는 HitGlitchScreenFeature 와 같은 기준으로
/// "가장 나중에 그려지는 카메라" 한 대에만 적용해서 두 번 어두워지지 않게 한다.
/// </summary>
public class DarknessScreenFeature : ScriptableRendererFeature
{
    [Tooltip("DarknessComposite 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/DarknessComposite.mat). 비어 있으면 Resources 에서 찾음")]
    public Material compositeMaterial;

    [Tooltip("빛 지도 해상도 (화면 대비). 빛 지도는 어차피 흐린 그림이라 0.5 면 충분하고 가볍다")]
    [Range(0.25f, 1f)] public float maskResolution = 0.5f;

    [Tooltip("편집기의 씬 화면에서도 어둠을 보여줌 (RoomDarkness 의 미리보기가 켜져 있을 때)")]
    public bool showInSceneView = true;

    [Tooltip("빛 지도 그리는 시점")]
    public RenderPassEvent maskEvent = RenderPassEvent.AfterRenderingTransparents;

    [Tooltip("합치는 시점. 글리치 효과(후처리 다음)보다 앞이어야 글리치가 어둠에 묻히지 않는다")]
    public RenderPassEvent compositeEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    MaskPass maskPass;
    CompositePass compositePass;

    // 빛 지도는 게임 화면용 / 씬 화면용을 따로 둔다 (서로 덮어쓰지 않게)
    static RTHandle gameMask;
    static RTHandle sceneMask;
    static int lastGameClearFrame = -1;

    public override void Create()
    {
        if (compositeMaterial == null) compositeMaterial = Resources.Load<Material>("DarknessComposite");
        maskPass = new MaskPass();
        compositePass = new CompositePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (compositeMaterial == null) return;
        if (!DarknessSystem.IsActive) return;                     // 어두운 방이 아니면 아무것도 안 함

        ref CameraData cameraData = ref renderingData.cameraData;
        Camera cam = cameraData.camera;
        bool isScene = cameraData.cameraType == CameraType.SceneView;
        if (isScene)
        {
            if (!showInSceneView || Application.isPlaying) return;   // 재생 중 씬 화면은 그대로 둠
        }
        else
        {
            if (cameraData.cameraType != CameraType.Game) return;
            if (cam.targetTexture != null) return;
            if (cam.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay) return;
        }

        bool isFirst, isLast;
        if (isScene)
        {
            isFirst = isLast = true;
        }
        else
        {
            GetFirstAndLastCamera(out Camera first, out Camera last);
            isLast = cam == last;
            // 가장 먼저 그리는 카메라거나, 이번 프레임에 아직 아무도 안 비웠으면 비움
            isFirst = cam == first || lastGameClearFrame != Time.frameCount;
            if (isFirst) lastGameClearFrame = Time.frameCount;
        }

        DarknessSystem.Settings s = DarknessSystem.Current;
        float fade = DarknessSystem.Fade;

        maskPass.renderPassEvent = maskEvent;
        maskPass.Setup(isScene, isFirst, maskResolution);
        maskPass.ConfigureInput(ScriptableRenderPassInput.Color);   // 카메라마다 같은 방식(중간 텍스처)으로 그리게 맞춤
        renderer.EnqueuePass(maskPass);

        if (isLast)
        {
            compositePass.renderPassEvent = compositeEvent;
            compositePass.Setup(isScene, compositeMaterial, s, fade);
            compositePass.ConfigureInput(ScriptableRenderPassInput.Color);
            renderer.EnqueuePass(compositePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        compositePass?.Dispose();
        gameMask?.Release(); gameMask = null;
        sceneMask?.Release(); sceneMask = null;
    }

    // ── 카메라 순서: 화면에 직접 그리는 기본 카메라 중 깊이(Depth)가 가장 작은 것 / 가장 큰 것 ──
    static Camera cachedFirst, cachedLast;
    static int cachedFrame = -1;
    static Camera[] cameraBuffer = new Camera[8];

    static void GetFirstAndLastCamera(out Camera first, out Camera last)
    {
        if (cachedFrame != Time.frameCount)
        {
            cachedFrame = Time.frameCount;
            cachedFirst = cachedLast = null;

            int count = Camera.allCamerasCount;
            if (cameraBuffer.Length < count) cameraBuffer = new Camera[count * 2];
            Camera.GetAllCameras(cameraBuffer);

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Camera c = cameraBuffer[i];
                if (c == null || c.targetTexture != null || c.cameraType != CameraType.Game) continue;
                if (c.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay) continue;
                if (c.depth < min) { min = c.depth; cachedFirst = c; }
                if (c.depth > max) { max = c.depth; cachedLast = c; }
            }
            System.Array.Clear(cameraBuffer, 0, count);
        }
        first = cachedFirst;
        last = cachedLast;
    }

    static RTHandle GetMask(bool isScene) => isScene ? sceneMask : gameMask;

    // ── 1) 빛 지도 그리기 ──
    class MaskPass : ScriptableRenderPass
    {
        static readonly ShaderTagId LightMaskTag = new ShaderTagId("LightMask");

        readonly ProfilingSampler sampler = new ProfilingSampler("DarknessMask");
        FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all, -1);
        bool isScene, clear;
        float resolution;

        public void Setup(bool isScene, bool clear, float resolution)
        {
            this.isScene = isScene;
            this.clear = clear;
            this.resolution = resolution;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.width = Mathf.Max(1, Mathf.RoundToInt(desc.width * resolution));
            desc.height = Mathf.Max(1, Mathf.RoundToInt(desc.height * resolution));
            desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Render)
                ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;

            if (isScene) RenderingUtils.ReAllocateIfNeeded(ref sceneMask, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DarknessLightMaskScene");
            else RenderingUtils.ReAllocateIfNeeded(ref gameMask, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DarknessLightMask");

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RTHandle mask = GetMask(isScene);
            if (mask == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                // 빛 지도로 대상을 바꿈 (이번 프레임 첫 카메라면 검게 비움)
                CoreUtils.SetRenderTarget(cmd, mask, clear ? ClearFlag.Color : ClearFlag.None, Color.black);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 이 카메라에 보이는 빛줄기들의 LightMask 단계만 그림
                DrawingSettings drawing = CreateDrawingSettings(LightMaskTag, ref renderingData, SortingCriteria.CommonTransparent);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);

                // 다음 단계들이 원래대로 카메라 화면에 그리도록 되돌림
                ScriptableRenderer r = renderingData.cameraData.renderer;
                CoreUtils.SetRenderTarget(cmd, r.cameraColorTargetHandle, r.cameraDepthTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    }

    // ── 2) 합치기 ──
    class CompositePass : ScriptableRenderPass
    {
        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int LightMaskId = Shader.PropertyToID("_LightMaskTex");
        static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int MaxBrightId = Shader.PropertyToID("_MaxBright");

        readonly ProfilingSampler sampler = new ProfilingSampler("DarknessComposite");
        readonly MaterialPropertyBlock props = new MaterialPropertyBlock();
        bool isScene;
        Material mat;
        DarknessSystem.Settings settings;
        float fade;
        RTHandle copy;

        public void Setup(bool isScene, Material mat, DarknessSystem.Settings settings, float fade)
        {
            this.isScene = isScene;
            this.mat = mat;
            this.settings = settings;
            this.fade = fade;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref copy, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DarknessScreenCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RTHandle mask = GetMask(isScene);
            if (mat == null || copy == null || mask == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                RTHandle screen = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // 지금 화면을 복사
                CoreUtils.SetRenderTarget(cmd, copy);
                Blitter.BlitTexture(cmd, screen, new Vector4(1, 1, 0, 0), 0f, false);

                // 복사본 × (어두운 바탕 + 빛 지도) 를 화면에 다시 그림
                Color amb = settings.ambientTint * (1f - settings.darkness);
                CoreUtils.SetRenderTarget(cmd, screen);
                props.Clear();
                props.SetTexture(BlitTextureId, copy);
                props.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                props.SetTexture(LightMaskId, mask);
                props.SetVector(AmbientId, new Vector4(amb.r, amb.g, amb.b, 1f));
                props.SetFloat(FadeId, fade);
                props.SetFloat(MaxBrightId, settings.maxBrightness);
                cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1, props);

                // 다음 단계(글리치 효과 등)를 위해 카메라 화면·깊이로 되돌림
                CoreUtils.SetRenderTarget(cmd, screen, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            copy?.Release();
            copy = null;
        }
    }
}
