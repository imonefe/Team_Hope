using System.Collections;
using UnityEngine;

public class Portal : ProximityInteractable
{
    [Header("스프라이트")]
    public SpriteRenderer spriteRenderer;
    public Sprite inactiveSprite;
    public Sprite activeSprite;

    [Header("콜라이더")]
    public Collider2D portalCollider;

    [Header("애니메이션")]
    public Animator animator;
    public string openTrigger = "Open";

    [Header("이동할 방 (선택, 2026-10-17 추가)")]
    [Tooltip("비워두면 노드맵을 연다. 방 프리팹을 넣으면 노드맵 대신 그 방으로 바로 이동한다 (예: 로비 → 첫 방)")]
    public GameObject targetRoom;

    [Header("닫힌 문 안내 (2026-10-26 추가)")]
    [Tooltip("적이 남아 문이 닫혀 있을 때 F를 누르면 화면 하단에 뜨는 문구")]
    public string lockedMessage = "남아있는 적이 있다";

    [Header("문 들어가기 파티클 (2026-10-26 추가)")]
    [Tooltip("문 테두리에서 생겨 문 가운데로 빨려 들어가는 빛 알갱이 색")]
    public Color enterParticleColor = new Color(1f, 0.78f, 0.42f, 1f);
    [Tooltip("빛 알갱이 개수")]
    public int enterParticleCount = 45;
    [Tooltip("알갱이가 생기는 원의 반지름 (월드 단위)")]
    public float enterParticleRadius = 1.6f;
    [Tooltip("알갱이 하나가 사는 시간(초) — 이 시간 동안 가운데로 빨려 들어감")]
    public float enterParticleLifetime = 0.55f;
    [Tooltip("알갱이 크기 (최소~최대, 월드 단위)")]
    public Vector2 enterParticleSize = new Vector2(0.12f, 0.28f);
    [Tooltip("파티클 위치 추가 조정 (문 감지 영역 가운데 기준)")]
    public Vector3 enterParticleOffset = Vector3.zero;
    public int enterParticleSortingOrder = 2100;

    bool isActive = false;
    /// <summary>[2026-10-27] 문이 열려 있는지 (감지 영역은 항상 켜져 있으므로 열림 여부는 이걸로 확인)</summary>
    public bool IsOpen => isActive;
    bool transitioning = false; // [2026-10-10] 페이드 진행 중 F를 다시 눌러 전환이 겹치지 않게

    static Material dustMat; // 빛줄기 먼지와 같은 머티리얼 (Etc/Resources/LightDust) — 그림 없이 둥근 빛 점을 그림

    void Awake()
    {
        SetActive(false);
    }

    public void SetActive(bool value)
    {
        isActive = value;

        if (spriteRenderer != null)
            spriteRenderer.sprite = isActive ? activeSprite : inactiveSprite;

        // [2026-10-26 변경] 닫힌 문 앞에서도 F를 감지해 "남아있는 적이 있다"를 띄우려고 감지 영역은 항상 켜 둔다.
        // (열림 여부는 CanInteract()가 isActive로 판단)
        if (portalCollider != null)
            portalCollider.enabled = true;

        if (isActive && animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger(openTrigger);
        }
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동.
    protected override bool CanInteract() => isActive && !transitioning;

    // [2026-10-26 추가] 닫힌 문에 F → 화면 하단 안내
    protected override void Update()
    {
        base.Update();

        if (isActive || !playerNearby) return;
        if (!Input.GetKeyDown(interactKey)) return;
        // 베이스와 같은 조건: 창이 떠 있거나, 대화 중이거나, 방금 같은 F로 대화를 끝낸 프레임이면 무시
        if (Time.timeScale == 0f) return;
        if (NPC.AnyTalking) return;
        if (Time.frameCount == NPC.LastEndFrame) return;
        if (CutsceneSequencer.IsAnyPlaying) return;
        if (PlayerController.Instance != null && PlayerController.Instance.IsControlLocked) return;

        ScreenNotice.Show(lockedMessage);
    }

    protected override void Interact()
    {
        PlayEnterParticles(); // [2026-10-26 추가] 표시용 파티클만 — 플레이어는 건드리지 않는다

        if (targetRoom != null)
            StartCoroutine(LoadRoomWithFade());
        else
            StartCoroutine(OpenMapWithFade());
    }

    // [2026-10-17 추가] 노드맵을 거치지 않고 정해진 방으로 바로 이동 (로비 → 첫 방)
    // 방이 바뀌면 이 포탈(지금 방의 자식)도 같이 지워지므로, 페이드는 씬에 계속 남는
    // TransitionCanvas 쪽에서 돌린다 (포탈에서 돌리면 화면이 까만 채로 멈춤)
    IEnumerator LoadRoomWithFade()
    {
        if (RoomManager.Instance == null)
        {
            Debug.LogWarning("[Portal] RoomManager 없음!");
            yield break;
        }

        transitioning = true;
        GameObject room = targetRoom;
        if (TransitionCanvas.Instance != null)
            TransitionCanvas.Instance.StartCoroutine(TransitionCanvas.Instance.Transition(() =>
            {
                RoomManager.Instance.LoadRoom(room);
            }));
        else
            RoomManager.Instance.LoadRoom(room);
    }

    IEnumerator OpenMapWithFade()
    {
        if (MapManager.Instance == null)
        {
            Debug.LogWarning("[Portal] MapManager 없음!");
            yield break;
        }

        transitioning = true;
        if (TransitionCanvas.Instance != null)
            yield return StartCoroutine(TransitionCanvas.Instance.Transition(() =>
            {
                MapManager.Instance.OpenMap();
            }));
        else
            MapManager.Instance.OpenMap();
        transitioning = false;
    }

    // ── [2026-10-26 추가] 문 들어가기 파티클 ─────────────────
    // 문 둘레 원 위에서 빛 알갱이가 생겨 문 가운데로 빨려 들어가며 작아지고 사라진다.
    // 씬 루트에 만들어 문 크기에 영향받지 않게 하고, 다 재생되면 스스로 지워진다
    // (방이 바뀌면 RoomManager의 잔여물 정리로도 지워짐).
    void PlayEnterParticles()
    {
        if (dustMat == null) dustMat = Resources.Load<Material>("LightDust");
        if (dustMat == null)
        {
            Debug.LogWarning("[Portal] Resources/LightDust 머티리얼을 찾지 못해 문 파티클을 생략합니다.");
            return;
        }

        Vector3 center = (portalCollider != null ? portalCollider.bounds.center : transform.position) + enterParticleOffset;
        center.z = transform.position.z;

        var go = new GameObject("DoorEnterFX");
        go.layer = gameObject.layer; // 문과 같은 카메라에 보이도록
        go.transform.position = center;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float life = Mathf.Max(0.1f, enterParticleLifetime);

        var main = ps.main;
        main.duration = 0.35f;
        main.loop = false;
        main.playOnAwake = false;
        main.useUnscaledTime = true;          // 노드맵이 열려 시간이 멈춰도 끝까지 재생되고 지워짐
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(enterParticleSize.x, Mathf.Max(enterParticleSize.x, enterParticleSize.y));
        main.startColor = enterParticleColor;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = Mathf.Max(1, enterParticleCount);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f,    (short)Mathf.CeilToInt(enterParticleCount * 0.5f)),
            new ParticleSystem.Burst(0.15f, (short)Mathf.FloorToInt(enterParticleCount * 0.5f)),
        });

        // 원 둘레에서 생성
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = enterParticleRadius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        // 가운데로 빨려 들어감 (반지름 ÷ 수명 = 수명 끝에 딱 가운데 도착)
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        float pull = enterParticleRadius / life;
        AnimationCurve accel = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)); // 갈수록 빨라짐
        vel.radial = new ParticleSystem.MinMaxCurve(-pull, accel);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(1.2f); // 살짝 소용돌이

        // 작아지며
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        // 금색 → 흰빛으로 짧게 반짝이며 사라짐
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white * 0.9f, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = dustMat;
        r.sortingOrder = enterParticleSortingOrder;

        ps.Play();
    }
}
