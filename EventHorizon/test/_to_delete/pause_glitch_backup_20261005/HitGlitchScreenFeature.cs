using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 피격 글리치를 "실제 게임 화면"에 입히는 렌더러 기능 (URP Renderer Feature).
///
/// Renderer3D_LightingTest 렌더러에 등록되어 있다. 피격 효과가 켜져 있는 동안에만
/// 화면을 한 장 복사한 뒤 HitGlitchScreen 셰이더로 다시 그린다 → 가장자리만 줄 밀림 + 색 분리 + 붉은 물들임.
/// 평소(피격 없음)에는 아무것도 하지 않으므로 비용이 없다.
///
/// 카메라가 여러 대일 때(예: SampleScene 의 Main Camera_3d + Camera_2D) 두 번 겹쳐 그려지지 않도록
/// "가장 나중에 그려지는 카메라(깊이 값이 가장 큰 기본 카메라)" 한 대에만 적용한다.
/// 세기(_Intensity)와 무작위 씨앗(_Seed)은 HitGlitchEffect 가 정해 준다.
/// </summary>
public class HitGlitchScreenFeature : ScriptableRendererFeature
{
    [Tooltip("HitGlitchScreen 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/HitGlitchScreen.mat). 모양은 이 머티리얼에서 조절")]
    public Material material;

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
        if (material == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        if (renderingData.cameraData.camera != GetTargetCamera()) return;

        LastSeenFrame = Time.frameCount;
        if (!HitGlitchEffect.ScreenActive) return;   // 피격 효과가 없을 땐 패스를 넣지 않음

        pass.renderPassEvent = injectionPoint;
        pass.Setup(material);
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

        readonly ProfilingSampler sampler = new ProfilingSampler("HitGlitchScreen");
        readonly MaterialPropertyBlock props = new MaterialPropertyBlock();
        Material mat;
        RTHandle copy;

        public void Setup(Material m) { mat = m; }

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
            if (mat == null || copy == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                RTHandle screen = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // 1) 지금 화면을 복사
                CoreUtils.SetRenderTarget(cmd, copy);
                Blitter.BlitTexture(cmd, screen, new Vector4(1, 1, 0, 0), 0f, false);

                // 2) 복사본을 읽어서 일그러뜨린 그림을 화면에 다시 그림
                CoreUtils.SetRenderTarget(cmd, screen);
                props.Clear();
                props.SetTexture(BlitTextureId, copy);
                props.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                props.SetFloat(IntensityId, HitGlitchEffect.ScreenIntensity);
                props.SetFloat(SeedId, HitGlitchEffect.ScreenSeed);
                cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1, props);
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
