using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// [2026-11-01 추가] 이펙트용 "부분 화면 왜곡" 렌더러 기능 (URP Renderer Feature). Renderer3D_LightingTest 에 등록.
///
/// HitGlitchScreenFeature(화면 전체 필터)와 같은 "화면 복사 → 다시 그리기" 방식이지만,
/// 화면 전체가 아니라 ScreenDistortSource 가 붙은 이펙트 모양 안쪽만 일그러뜨린다.
///
/// 순서 (반투명 물체를 다 그린 직후):
///  1) 지금까지 그려진 화면을 복사해서 _CameraSortingLayerTexture 라는 이름으로 모든 셰이더에 알려 준다
///     (2D 렌더러가 쓰는 이름과 같게 해서 SG_ScreenDistort 셰이더를 고치지 않고 그대로 쓴다)
///  2) 왜곡 오브젝트(렌더링 레이어 표시가 있는 것)만 골라 그린다 → 복사본을 비틀어 그 자리에 덮어씀
///
/// 왜곡 오브젝트가 하나도 없으면 패스를 넣지 않으므로 평소 비용은 0.
/// </summary>
public class ScreenDistortFeature : ScriptableRendererFeature
{
    [Tooltip("언제 끼워 넣을지. 기본값(반투명 다음)이면 배경·캐릭터·이펙트까지 다 그려진 화면을 왜곡한다")]
    public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

    [Tooltip("화면 복사본 해상도 나누기. 1 = 원본, 2 = 절반 (가볍지만 왜곡 부분이 살짝 흐려짐)")]
    [Range(1, 4)] public int downsample = 1;

    [Tooltip("Scene 뷰에서도 적용할지 (Scene 뷰가 3D 렌더러를 쓸 때만 의미 있음)")]
    public bool showInSceneView = true;

    DistortPass pass;

    public override void Create()
    {
        pass = new DistortPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ScreenDistortSource.ActiveCount <= 0) return;

        CameraType type = renderingData.cameraData.cameraType;
        if (type == CameraType.SceneView)
        {
            if (!showInSceneView) return;
        }
        else if (type == CameraType.Game)
        {
            // 왜곡 오브젝트가 올라간 레이어를 찍지 않는 카메라(예: Main Camera_3d)는 건너뜀
            if ((renderingData.cameraData.camera.cullingMask & ScreenDistortSource.LayerMask) == 0) return;
        }
        else return;

        pass.renderPassEvent = injectionPoint;
        pass.downsample = Mathf.Max(1, downsample);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    class DistortPass : ScriptableRenderPass
    {
        static readonly int ScreenTextureId = Shader.PropertyToID("_CameraSortingLayerTexture");

        // Sprite Unlit 셰이더 그래프의 2D 패스 + 일반 Unlit 패스. (UniversalForward 는 ScreenDistortSource 가 꺼 둠)
        static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("Universal2D"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        public int downsample = 1;

        readonly ProfilingSampler sampler = new ProfilingSampler("ScreenDistort");
        FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all, -1, ScreenDistortSource.RenderingLayerBit);
        RTHandle copy;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.width = Mathf.Max(1, desc.width / downsample);
            desc.height = Mathf.Max(1, desc.height / downsample);
            RenderingUtils.ReAllocateIfNeeded(ref copy, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ScreenDistortCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (copy == null) return;

            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            RTHandle screen = renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                // 1) 지금 화면 → 복사본, 셰이더들에게 알려 줌
                Blitter.BlitCameraTexture(cmd, screen, copy);
                cmd.SetGlobalTexture(ScreenTextureId, copy);

                // 2) 다시 원래 화면(+깊이)에 그리도록 되돌림
                CoreUtils.SetRenderTarget(cmd, screen, renderer.cameraDepthTargetHandle);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 3) 왜곡 오브젝트만 그림 (반투명 정렬 순서 유지)
                DrawingSettings drawing = CreateDrawingSettings(ShaderTags, ref renderingData, SortingCriteria.CommonTransparent);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
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
