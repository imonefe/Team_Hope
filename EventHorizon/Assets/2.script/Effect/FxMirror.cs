using UnityEngine;

/// <summary>
/// [2026-11-20 추가] 파티클 이펙트 좌우 반전 보조.
///
/// 이펙트 루트의 Scale X 를 -1 로 두면 (또는 부모가 뒤집혀 있으면) 이펙트 전체가 좌우로 뒤집히도록 맞춰 준다.
///  - 파티클 Scaling Mode 를 Hierarchy 로: 부모의 -1 이 방출 모양·위치·방향에 적용됨
///    (Local 이면 부모 스케일을 무시해서 파티클만 안 뒤집혔음)
///  - 빌보드 파티클은 스케일이 음수여도 그림이 안 뒤집히므로 Renderer 의 Flip X 를 켬
///  - 시작 회전·회전 속도(Rotation over Lifetime)는 부호를 바꿈 (오른쪽으로 기운 그림 → 왼쪽으로 기울게)
/// 뒤집힌 상태가 바뀌면 (예: 생성 후 Scale X 를 바꿈) 다음 프레임에 다시 맞춘다.
/// 메시 렌더 모드 파티클·MeshRenderer 는 음수 스케일을 그대로 따라 뒤집히므로 건드리지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class FxMirror : MonoBehaviour
{
    [Tooltip("켜면 빌보드 파티클 그림을 Renderer Flip X 로 뒤집음")]
    public bool flipTexture = true;
    [Tooltip("켜면 시작 회전·회전 속도의 부호를 바꿈")]
    public bool mirrorRotation = true;

    struct Orig
    {
        public ParticleSystem ps;
        public ParticleSystemRenderer r;
        public Vector3 flip;
        public float rotMul, rotMulX, rotMulY, rotLifeMul;
    }

    Orig[] origs;
    int applied;   // 0 = 아직, 1 = 정방향 적용, -1 = 반전 적용

    void Capture()
    {
        var all = GetComponentsInChildren<ParticleSystem>(true);
        origs = new Orig[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            var ps = all[i];
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var rot = ps.rotationOverLifetime;
            origs[i] = new Orig
            {
                ps = ps,
                r = ps.GetComponent<ParticleSystemRenderer>(),
                rotMul = main.startRotationMultiplier,
                rotMulX = main.startRotationXMultiplier,
                rotMulY = main.startRotationYMultiplier,
                rotLifeMul = rot.zMultiplier,
            };
            if (origs[i].r != null) origs[i].flip = origs[i].r.flip;
        }
    }

    void OnEnable()
    {
        if (origs == null) Capture();
        applied = 0;
        Apply();
    }

    void LateUpdate() => Apply();

    /// <summary>[2026-11-20] 원래(안 뒤집힌) 값으로 되돌림 — 다른 스크립트가 방향을 직접 맞출 때 먼저 부름</summary>
    public void ResetToOriginal()
    {
        if (origs == null) Capture();
        applied = 0;
        Apply(1);
    }

    void Apply() => Apply(transform.lossyScale.x < 0f ? -1 : 1);

    void Apply(int want)
    {
        if (want == applied) return;
        applied = want;
        bool m = want < 0;
        foreach (var o in origs)
        {
            if (o.ps == null) continue;
            if (o.r != null && o.r.renderMode != ParticleSystemRenderMode.Mesh)
            {
                Vector3 f = o.flip;
                if (m && flipTexture) f.x = 1f - f.x;
                o.r.flip = f;
            }
            var main = o.ps.main;
            float s = (m && mirrorRotation) ? -1f : 1f;
            if (main.startRotation3D)
            {
                main.startRotationXMultiplier = o.rotMulX;
                main.startRotationYMultiplier = o.rotMulY * s;
                main.startRotationZMultiplier = o.rotMul * s;
            }
            else main.startRotationMultiplier = o.rotMul * s;
            var rot = o.ps.rotationOverLifetime;
            if (rot.enabled) rot.zMultiplier = o.rotLifeMul * s;
        }
    }
}
