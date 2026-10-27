using UnityEngine;

/// <summary>
/// [2026-10-27 추가] 재화 획득 연출 — "+N" 숫자 대신 노란 작은 파편이 튀어 올랐다가 바닥에 떨어져 잠깐 구르고 사라진다.
///
/// 사용법: CurrencyShardFX.Spawn(위치, 획득량);  (RunManager.DropRandomCurrency 에서 호출)
///  - 씬에 미리 둘 것 없음. 부를 때마다 파티클 오브젝트를 하나 만들고, 다 재생되면 스스로 지워진다.
///  - 파편 개수는 획득량에 비례 (최소 minCount ~ 최대 maxCount).
///  - 그림 소스는 쓰지 않는다 — 흰 사각형(코드로 만든 점)을 노랗게 물들여 작은 조각처럼 보이게 한다.
///  - 바닥/발판(ground, platform 층)에 부딪혀 튕긴다.
///  - 모양·색은 아래 상수에서 조절.
/// </summary>
public static class CurrencyShardFX
{
    const int MinCount = 5;
    const int MaxCount = 18;
    const float CountPerAmount = 0.6f;      // 획득량 1당 파편 개수
    const float Lifetime = 1.4f;            // 파편 하나가 사는 시간(초)
    const float MinSize = 0.07f, MaxSize = 0.13f;
    const float MinSpeed = 4f, MaxSpeed = 7.5f;
    const float Gravity = 2.6f;
    const int SortingOrder = 5100;          // 플레이어(4000~5000)보다 위
    const float TowardCamera = 1.5f;        // ItemPickupEffect 와 같은 이유로 카메라 쪽으로 당김
    static readonly Color ColorA = new Color(1f, 0.80f, 0.30f, 1f);   // 진한 금빛 노랑
    static readonly Color ColorB = new Color(1f, 0.93f, 0.58f, 1f);   // 밝은 노랑

    static Material shardMat;

    public static void Spawn(Vector3 position, int amount)
    {
        if (amount <= 0) return;
        EnsureMaterial();

        position.z -= TowardCamera;
        var go = new GameObject("CurrencyShardFX");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        int count = Mathf.Clamp(Mathf.RoundToInt(amount * CountPerAmount), MinCount, MaxCount);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime * 0.75f, Lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(MinSpeed, MaxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(MinSize, MaxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(ColorA, ColorB);
        main.gravityModifier = Gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = MaxCount;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // 위쪽 부채꼴로 튀어 오름
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 원뿔이 +Y(위)를 향하게

        // 공중에서 빙글빙글
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        // 바닥·발판에 부딪혀 튕김 (2D 충돌)
        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision2D;
        col.collidesWith = LayerMask.GetMask("ground", "platform");
        col.bounce = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        col.dampen = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        col.lifetimeLoss = 0f;
        col.radiusScale = 0.5f;

        // 끝에서만 빠르게 사라짐 (바닥에 떨어진 뒤 잠깐 남아 있다가)
        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = grad;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = shardMat;
        r.sortingOrder = SortingOrder;

        ps.Play();
    }

    static void EnsureMaterial()
    {
        if (shardMat != null) return;
        // 빛을 받지 않는 기본 스프라이트 셰이더 + 흰 사각형 (어두운 방에서도 또렷하게)
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        shardMat = new Material(sh) { name = "CurrencyShard", hideFlags = HideFlags.DontSave };
        shardMat.mainTexture = Texture2D.whiteTexture;
    }
}
