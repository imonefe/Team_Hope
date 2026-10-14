using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 글리치 효과를 "실제 게임 화면"에 입히는 렌더러 기능 (URP Renderer Feature).
///
/// Renderer3D_LightingTest 렌더러에 등록되어 있다. 두 층을 그린다.
///  1) 퍼즈 층: 퍼즈 발동 중 → PauseGlitchScreen 셰이더 (푸른 화면 + 약한 노이즈 + 번개). 세기는 PauseGlitchEffect 가 정함
///  2) 피격 층: 맞았을 때 → HitGlitchScreen 셰이더 (붉은 줄 밀림 + 색 분리). 세기는 HitGlitchEffect 가 정함
/// 둘 다 켜져 있으면 퍼즈 층 위에 피격 층을 덮는다. 둘 다 꺼져 있으면 아무것도 하지 않으므로 비용이 없다.
///
/// 카메라가 여러 대일 때(예: SampleScene 의 Main Camera_3d + Camera_2D) 두 번 겹쳐 그려지지 않도록
/// "가장 나중에 그려지는 카메라(깊이 값이 가장 큰 기본 카메라)" 한 대에만 적용한다.
/// </summary>
public class HitGlitchScreenFeature : ScriptableRendererFeature
{
    [Tooltip("피격용: HitGlitchScreen 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/HitGlitchScreen.mat). 모양은 이 머티리얼에서 조절")]
    public Material material;

    [Tooltip("퍼즈용: PauseGlitchScreen 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/PauseGlitchScreen.mat). 모양은 이 머티리얼에서 조절")]
    public Material pauseMaterial;

    [Tooltip("언제 끼워 넣을지. 기본값(후처리 다음)이면 블룸 등 후처리까지 끝난 화면을 일그러뜨린다")]
    public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    /// <summary>이 기능이 마지막으로 대상 카메라를 본 프레임. HitGlitchEffect 가 "화면 왜곡이 실제로 동작 중인지" 확인하는 데 쓴다.</summary>
    public static int LastSeenFrame = -100;

    GlitchPass pass;

    public override void Create()
    {
        pass = new GlitchPass();
        pass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null && pauseMaterial == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        if (renderingData.cameraData.camera != GetTargetCamera()) return;

        if (material != null) LastSeenFrame = Time.frameCount;

        Material hit = material != null && HitGlitchEffect.ScreenActive ? material : null;
        Material pause = pauseMaterial != null && PauseGlitchEffect.ScreenActive ? pauseMaterial : null;
        if (hit == null && pause == null) return;   // 효과가 없을 땐 패스를 넣지 않음

        pass.renderPassEvent = injectionPoint;
        pass.Setup(hit, pause);
        pass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    // ── 대상 카메라: 화면에 직접 그리는 기본 카메라 중 깊이(Depth) 값이 가장 큰 것 ──
    static Camera cachedTarget;
    static int cachedFrame = -1;
    static Camera[] cameraBuffer = new Camera[8];

    static Camera GetTargetCamera()
    {
        if (cachedFrame == Time.frameCount) return cachedTarget;
        cachedFrame = Time.frameCount;
        cachedTarget = null;

        int count = Camera.allCamerasCount;
        if (cameraBuffer.Length < count) cameraBuffer = new Camera[count * 2];
        Camera.GetAllCameras(cameraBuffer);

        float best = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            Camera c = cameraBuffer[i];
            if (c == null || c.targetTexture != null || c.cameraType != CameraType.Game) continue;
            if (c.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay) continue;
            if (c.depth > best)
            {
                best = c.depth;
                cachedTarget = c;
            }
        }
        System.Array.Clear(cameraBuffer, 0, count);
        return cachedTarget;
    }

    // ── 실제로 그리는 패스 ──
    class GlitchPass : ScriptableRenderPass
    {
        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int BoltSeedId = Shader.PropertyToID("_BoltSeed");
        static readonly int BoltIntensityId = Shader.PropertyToID("_BoltIntensity");
        static readonly int BoltTimeId = Shader.PropertyToID("_BoltTime");
        static readonly int ColorIntensityId = Shader.PropertyToID("_ColorIntensity");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int ShockId = Shader.PropertyToID("_Shock");
        static readonly int ShockAmountId = Shader.PropertyToID("_ShockAmount");

        readonly ProfilingSampler sampler = new ProfilingSampler("HitGlitchScreen");
        readonly MaterialPropertyBlock hitProps = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock pauseProps = new MaterialPropertyBlock();
        Material hitMat;
        Material pauseMat;
        RTHandle copy;

        public void Setup(Material hit, Material pause)
        {
            hitMat = hit;
            pauseMat = pause;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref copy, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_HitGlitchScreenCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ((hitMat == null && pauseMat == null) || copy == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                RTHandle screen = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // 1) 퍼즈 층 (아래)
                if (pauseMat != null)
                {
                    pauseProps.Clear();
                    pauseProps.SetFloat(IntensityId, PauseGlitchEffect.ScreenIntensity);
                    pauseProps.SetFloat(SeedId, PauseGlitchEffect.ScreenSeed);
                    pauseProps.SetFloat(BoltSeedId, PauseGlitchEffect.BoltSeed);
                    pauseProps.SetFloat(BoltIntensityId, PauseGlitchEffect.BoltIntensity);
                    pauseProps.SetFloat(BoltTimeId, PauseGlitchEffect.BoltTime);
                    pauseProps.SetFloat(ColorIntensityId, PauseGlitchEffect.ColorIntensity);
                    pauseProps.SetFloat(FlashId, PauseGlitchEffect.FlashIntensity);
                    pauseProps.SetFloat(ShockId, PauseGlitchEffect.ShockProgress);
                    pauseProps.SetFloat(ShockAmountId, PauseGlitchEffect.ShockIntensity);
                    DrawLayer(cmd, screen, pauseMat, pauseProps);
                }

                // 2) 피격 층 (위)
                if (hitMat != null)
                {
                    hitProps.Clear();
                    hitProps.SetFloat(IntensityId, HitGlitchEffect.ScreenIntensity);
                    hitProps.SetFloat(SeedId, HitGlitchEffect.ScreenSeed);
                    DrawLayer(cmd, screen, hitMat, hitProps);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // 지금 화면을 복사한 뒤, 복사본을 읽어서 일그러뜨린 그림을 화면에 다시 그림
        void DrawLayer(CommandBuffer cmd, RTHandle screen, Material m, MaterialPropertyBlock props)
        {
            CoreUtils.SetRenderTarget(cmd, copy);
            Blitter.BlitTexture(cmd, screen, new Vector4(1, 1, 0, 0), 0f, false);

            CoreUtils.SetRenderTarget(cmd, screen);
            props.SetTexture(BlitTextureId, copy);
            props.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
            cmd.DrawProcedural(Matrix4x4.identity, m, 0, MeshTopology.Triangles, 3, 1, props);
        }

        public void Dispose()
        {
            copy?.Release();
            copy = null;
        }
    }
}
