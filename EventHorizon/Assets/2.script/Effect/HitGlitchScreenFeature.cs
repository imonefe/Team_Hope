using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면 글리치 효과를 "실제 게임 화면"에 입히는 렌더러 기능 (URP Renderer Feature).
///
/// Renderer3D_LightingTest 렌더러에 등록되어 있다. 세 층을 아래부터 차례로 그린다.
///  1) 저체력 층: HP 20% 이하 → LowHpGlitchScreen 셰이더 (가장자리 회붉은 노이즈 + 심한 왜곡). 세기는 LowHpGlitchEffect 가 정함
///  2) 퍼즈 층: 퍼즈 발동 중 → PauseGlitchScreen 셰이더 (푸른 화면 + 테두리 노이즈 + 번개). 세기는 PauseGlitchEffect 가 정함
///  3) 피격 층: 맞았을 때 → HitGlitchScreen 셰이더 (붉은 줄 밀림 + 색 분리). 세기는 HitGlitchEffect 가 정함
///  4) 메뉴 블러 층: ESC 메뉴가 열려 있을 때 → MenuBlurScreen 셰이더를 여러 번 겹쳐 뒤 화면을 흐리게. 세기는 MenuBackdrop 이 정함
/// 켜진 층만 그리고, 모두 꺼져 있으면 아무것도 하지 않으므로 비용이 없다.
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

    [Tooltip("저체력용: LowHpGlitchScreen 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/LowHpGlitchScreen.mat). 모양은 이 머티리얼에서 조절")]
    public Material lowHpMaterial;

    [Tooltip("메뉴 블러용: MenuBlurScreen 셰이더를 쓰는 머티리얼 (Assets/Etc/Resources/MenuBlurScreen.mat)")]
    public Material menuBlurMaterial;

    [Tooltip("[2026-10-22] 화면 가르기용: M_ScreenSlash (ScreenSlashEffect 가 값을 움직임). _Intensity 가 0보다 클 때만 그림")]
    public Material slashMaterial;

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
        if (material == null && pauseMaterial == null && lowHpMaterial == null && menuBlurMaterial == null && slashMaterial == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        if (renderingData.cameraData.camera != GetTargetCamera()) return;

        if (material != null) LastSeenFrame = Time.frameCount;

        Material hit = material != null && HitGlitchEffect.ScreenActive ? material : null;
        Material pause = pauseMaterial != null && PauseGlitchEffect.ScreenActive ? pauseMaterial : null;
        Material lowHp = lowHpMaterial != null && LowHpGlitchEffect.ScreenActive ? lowHpMaterial : null;
        Material blur = menuBlurMaterial != null && MenuBackdrop.BlurActive && MenuBackdrop.BlurIterations > 0 ? menuBlurMaterial : null;
        Material slash = slashMaterial != null && slashMaterial.GetFloat(SlashIntensityId) > 0.001f ? slashMaterial : null;
        if (hit == null && pause == null && lowHp == null && blur == null && slash == null) return;   // 효과가 없을 땐 패스를 넣지 않음

        pass.renderPassEvent = injectionPoint;
        pass.Setup(hit, pause, lowHp, blur, slash);
        pass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    static readonly int SlashIntensityId = Shader.PropertyToID("_Intensity");

    /// <summary>[2026-10-22] 화면 효과가 적용되는 최종 카메라 (여러 대면 가장 나중에 그려지는 기본 카메라). 화면 좌표 계산용</summary>
    public static Camera FinalCamera => GetTargetCamera();

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
        static readonly int StartSliceId = Shader.PropertyToID("_StartSlice");
        static readonly int StartEchoId = Shader.PropertyToID("_StartEcho");
        static readonly int StartSeedId = Shader.PropertyToID("_StartSeed");
        static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        static readonly int OffsetId = Shader.PropertyToID("_Offset");
        static readonly int DimAmountId = Shader.PropertyToID("_DimAmount");
        static readonly int DimId = Shader.PropertyToID("_Dim");

        readonly ProfilingSampler sampler = new ProfilingSampler("HitGlitchScreen");
        readonly MaterialPropertyBlock hitProps = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock pauseProps = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock lowHpProps = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock[] blurProps = new MaterialPropertyBlock[8];
        Material hitMat;
        Material pauseMat;
        Material lowHpMat;
        Material blurMat;
        Material slashMat;
        readonly MaterialPropertyBlock slashProps = new MaterialPropertyBlock();
        RTHandle copy;

        public void Setup(Material hit, Material pause, Material lowHp, Material blur, Material slash)
        {
            hitMat = hit;
            pauseMat = pause;
            lowHpMat = lowHp;
            blurMat = blur;
            slashMat = slash;
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
            if ((hitMat == null && pauseMat == null && lowHpMat == null && blurMat == null && slashMat == null) || copy == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                RTHandle screen = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // 1) 저체력 층 (맨 아래)
                if (lowHpMat != null)
                {
                    lowHpProps.Clear();
                    lowHpProps.SetFloat(IntensityId, LowHpGlitchEffect.ScreenIntensity);
                    lowHpProps.SetFloat(SeedId, LowHpGlitchEffect.ScreenSeed);
                    lowHpProps.SetFloat(EffectTimeId, LowHpGlitchEffect.EffectTime);
                    DrawLayer(cmd, screen, lowHpMat, lowHpProps);
                }

                // 2) 퍼즈 층
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
                    pauseProps.SetFloat(StartSliceId, PauseGlitchEffect.StartSlice);
                    pauseProps.SetFloat(StartEchoId, PauseGlitchEffect.StartEcho);
                    pauseProps.SetFloat(StartSeedId, PauseGlitchEffect.StartSeed);
                    DrawLayer(cmd, screen, pauseMat, pauseProps);
                }

                // 3) 피격 층 (맨 위)
                if (hitMat != null)
                {
                    hitProps.Clear();
                    hitProps.SetFloat(IntensityId, HitGlitchEffect.ScreenIntensity);
                    hitProps.SetFloat(SeedId, HitGlitchEffect.ScreenSeed);
                    DrawLayer(cmd, screen, hitMat, hitProps);
                }

                // [2026-10-22] 화면 가르기 층 (피격 위, 메뉴 블러 아래). 값은 ScreenSlashEffect 가 머티리얼에 직접 넣음
                if (slashMat != null)
                {
                    slashProps.Clear();
                    DrawLayer(cmd, screen, slashMat, slashProps);
                }

                // 4) 메뉴 블러 (맨 위 — 위의 효과까지 전부 흐리게). 반복할수록 샘플 간격을 넓혀 부드럽게 퍼짐
                if (blurMat != null)
                {
                    int n = Mathf.Clamp(MenuBackdrop.BlurIterations, 1, blurProps.Length);
                    float amount = MenuBackdrop.BlurAmount;
                    for (int i = 0; i < n; i++)
                    {
                        if (blurProps[i] == null) blurProps[i] = new MaterialPropertyBlock();
                        MaterialPropertyBlock bp = blurProps[i];
                        bp.Clear();
                        bp.SetFloat(OffsetId, (i + 0.5f) * MenuBackdrop.BlurRadius * amount);
                        bp.SetFloat(DimAmountId, i == n - 1 ? amount : 0f);
                        bp.SetFloat(DimId, MenuBackdrop.BlurDim);
                        DrawLayer(cmd, screen, blurMat, bp);
                    }
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
