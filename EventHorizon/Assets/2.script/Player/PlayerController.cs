using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NowAdvantage { None, Speed, Defense, Power }
public enum Weapontype { None, Sword, Hammer, Glove, Gun }

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    // ── 상태 ──────────────────────────────────────────────
    public NowAdvantage nowadvantage;
    public Weapontype weapontype;

    public WeaponData EquippedWeapon { get; private set; }
    public event Action<Weapontype> OnWeaponChanged;

    // ── 무기 목록 ──────────────────────────────────────────
    [Header("무기 목록")]
    [SerializeField] WeaponData[] weapons;

    // ── 공격 ──────────────────────────────────────────────
    [Header("공격")]
    [SerializeField] GameObject spHitboxPrefab;
    [SerializeField] Transform spHitboxSpawnPoint;
    [SerializeField] float attackDashForce = 5f;

    int comboIndex = 0;
    int comboQueued = 0;
    float comboInputBufferTime = 0.25f; // 입력 선예약 유효 시간
    float comboInputTimer = -1f;        // 마지막 공격입력 후 경과 (음수=없음)
    bool attackDashPending = false;
    float attackDashDir = 0f;
    bool comboEffectPending = false;   // 콤보 이펙트도 이동과 같은 타이밍에 늦춰서, 이동이 실제로 반영된 뒤에 재생되게 함
    int pendingComboEffectIndex = 0;
    bool isAttacking = false;
    bool animPlaying = false;
    float animPlayingTimeout = 0f;
    public float animPlayingMaxTime = 1.2f; // 공격 클립 중 가장 긴 총(gun_combo) 클립(~1.0s)보다 여유있게. OnAttackAnimEnd 이벤트 유실 시의 백업용 타임아웃.

    float comboWindow = 0.3f;
    float comboTimer = 0f;
    float hitboxTimer = 0f;

    // [2026-10-18 추가] 타격 후 캔슬 — 이번 공격 동작의 타격 판정이 모두 나간 뒤
    // WeaponData.hitCancelDelay 초가 지나면 이동(새로 누르거나 반대 방향)·점프로 남은 동작을 끊는다.
    int hitEventsThisSwing = 0;     // 이번 동작에서 나간 타격 이벤트 수 (SpawnHitbox / FireProjectile)
    int hitEventsInClip = -1;       // 이번 동작 클립에 들어 있는 타격 이벤트 수 (-1 = 아직 안 셈)
    float hitCancelTimer = -1f;     // 모든 타격이 나간 뒤 지난 시간 (-1 = 캔슬 불가)
    float hitCancelDir = 0f;        // 마지막 타격 때 누르고 있던 방향 (그대로 누르고 있는 건 캔슬로 안 침)
    float hitboxTime = 0.2f;
    bool hitboxSpawned = true; // [2026-10-10] 첫 공격 전에는 '이미 생성됨'으로 둔다 — 차지 대기 클립 등에 남은 SpawnHitbox 이벤트가 공격 없이 판정을 내지 않도록
    bool gaugeChargedThisSwing = false;

    public bool TryChargeGauge()
    {
        if (gaugeChargedThisSwing) return false;
        gaugeChargedThisSwing = true;
        return true;
    }

    float baseAnimMaxTime = 1.2f; // animPlayingMaxTime 기본값 보존용

    // ── 건틀릿 스택 (공격속도) ────────────────────────────
    int gauntletStack = 0;
    float gauntletStackTimer = 0f;

    // ── 공격 강화 버프 (Skill.cs의 isAttackBuff 스킬이 호출) ─
    float attackBuffDamageMultiplier = 1f;
    float attackBuffAnimSpeedMultiplier = 1f;
    GameObject attackBuffExtraEffectPrefab;
    GameObject activeAttackBuffAura;
    Coroutine attackBuffRoutine;

    // ── 망치 차지 강타 ────────────────────────────────────
    bool isCharging = false;      // 차지 버튼을 누르고 있는 중
    bool chargeArmed = false;     // chargeTime을 넘겨 차지 강타 발동이 가능한 상태
    float chargeHeldTime = 0f;    // 버튼을 누르고 있는 총 시간
    bool pendingChargedAttack = false; // 다음 ExecuteHitbox가 차지 강타여야 함
    float pendingChargeMultiplier = 1f;
    GameObject activeChargeEffect;
    bool chargeHoldTracking = false; // 1타/2타 콤보 도중 길게 누르는지 관찰 중
    bool chargeQueued = false;       // 관찰 결과 확정: 다음 콤보 대신 차지로 전환
    bool chargeFromCombo = false;    // [2026-10-10] 콤보 클립이 끝날 때 아직 누르고 있어서 차지로 이어간 경우 (무장 전에 떼면 평타를 새로 치지 않고 그냥 취소)

    // ── 이동 / 점프 ───────────────────────────────────────
    [Header("이동 / 점프")]
    public float JumpPower = 7f;
    public int JumpCount = 0;

    // ── 대쉬 ──────────────────────────────────────────────
    [Header("대쉬")]
    public int MaxDashCount = 2;
    public float DashPower = 10f;
    public float DashDuration = 0.2f;
    public float DashCooldown = 1f;

    [HideInInspector] public int DeshCount;
    bool isDashing;
    public bool IsDashing => isDashing;

    // ── 대화/컷씬/메뉴 조작 잠금 ──────────────────────────
    // [2026-10-10 수정] 잠그는 쪽(NPC 대화, ESC 메뉴, 인벤토리, 노드맵)이 여러 곳인데 bool 하나라서
    // 먼저 푼 쪽이 다른 쪽 잠금까지 풀어버렸다(대화 중 ESC 메뉴를 닫으면 대화창이 떠 있는데 움직임 등).
    // 이제 잠근 주체를 각각 기억하고, 전부 풀려야 조작이 돌아온다.
    // 호출할 때 자기 자신(this)을 넘길 것: LockControl(this) / ResumeControl(this)
    readonly HashSet<object> lockOwners = new HashSet<object>();
    static readonly object DefaultLockOwner = new object();
    bool controlLocked => lockOwners.Count > 0;

    /// <summary>대화·메뉴 등으로 조작이 잠겨 있는지 (스킬 입력 등 다른 스크립트에서 확인용)</summary>
    public bool IsControlLocked => controlLocked;

    // [2026-10-18 추가] 무기 정보 칸(WeaponStripUI)이 읽기만 하는 값
    /// <summary>지금 공격력 = 공격력(Atk) × 무기 배율 × 공격 버프 (적 방어·치명타·상성 적용 전, 공격할 때와 같은 식)</summary>
    public int CurrentAttackPower =>
        stats != null ? Mathf.RoundToInt(stats.Atk * (EquippedWeapon != null ? EquippedWeapon.damageMultiplier : 1f) * attackBuffDamageMultiplier) : 0;

    public void LockControl(object owner = null)
    {
        bool wasLocked = controlLocked;
        lockOwners.Add(owner ?? DefaultLockOwner);
        if (wasLocked) return; // 이미 잠겨 있으면 상태 정리는 처음 한 번만

        // [2026-10-09 수정] 대시 도중 잠기면 Update가 멈춰 대시 시간이 줄지 않는데 FixedUpdate는
        // 계속 대시 속도를 넣어서(중력 0) 대화 내내 미끄러져 날아가던 문제 → 잠그는 순간 대시를 끝낸다.
        if (isDashing) EndDash(false);
        attackDashPending = false;

        rigid.velocity = new Vector2(0f, rigid.velocity.y);
        animator.SetFloat(ParamSpeed, 0f);

        // 건틀릿 스택 속도가 대화/컷씬 중에 남지 않도록 초기화
        animator.speed = 1f;
        gauntletStack = 0;
        gauntletStackTimer = 0f;

        // [2026-10-10 추가] 진행 중이던 공격·발판 내려가기도 정리 — 안 그러면 잠겨 있는 동안
        // 공격 상태가 풀리지 않아 대화 내내 공격 모션이 반복되거나, 레이어가 dropping으로 남아 발판을 계속 통과함
        if (isAttacking) EndAttack();
        if (isDropping)
        {
            gameObject.layer = originalLayer;
            isDropping = false;
            dropTimer = 0f;
        }
    }

    public void ResumeControl(object owner = null)
    {
        lockOwners.Remove(owner ?? DefaultLockOwner);
    }

    // ── 피격 경직 ─────────────────────────────────────────
    [Header("피격 경직")]
    [Tooltip("맞은 뒤 이 시간(초) 동안은 이동 입력이 넉백 속도를 덮어쓰지 않는다")]
    public float hitStunTime = 0.15f;
    float hitStunTimer = 0f;
    float dashTimer;
    float dashCooldownTimer;
    float dashDir;
    float originalGravity;

    // ── 플랫폼 드롭 ───────────────────────────────────────
    bool isDropping = false;
    float dropDuration = 0.3f;
    float dropTimer = 0f;
    int platformLayer;
    int originalLayer;

    // ── 이펙트 ────────────────────────────────────────────
    [Header("이펙트")]
    [SerializeField] GameObject jumpEffectPrefab;
    [SerializeField] GameObject dashEffectPrefabRight;
    [SerializeField] GameObject dashEffectPrefabLeft;
    [SerializeField] Transform jumpEffectPoint;
    [SerializeField] Transform dashEffectPoint;

    // ── 효과음 (점프 / 대쉬) ─────────────────────────────
    [Header("효과음 — 점프 / 대쉬")]
    [Tooltip("점프할 때 재생. 여러 개 넣으면 그중 하나가 랜덤 재생 (비워두면 소리 없음)")]
    [SerializeField] RandomSfx jumpSfx = new RandomSfx();
    [Tooltip("2단 점프 때 재생. 비워두면 위의 점프 소리를 그대로 사용")]
    [SerializeField] RandomSfx doubleJumpSfx = new RandomSfx();
    [Tooltip("대쉬 시작할 때 재생. 여러 개 넣으면 랜덤 (비워두면 소리 없음)")]
    [SerializeField] RandomSfx dashSfx = new RandomSfx();

    // ── 그라운드 체크 ─────────────────────────────────────
    [Header("그라운드 체크")]
    public float GroundCheckDistance = 1.2f;
    public float GroundCheckWidth = 0.4f;

    // ── 컴포넌트 ──────────────────────────────────────────
    Rigidbody2D rigid;
    Stats stats;
    Animator animator;
    AudioSource audioSource;
    AudioSource swingAudioSource;   // 휘두르는 소리 전용 (배속을 바꿔도 다른 소리에 영향 없도록 분리)
    bool wasGrounded = false;

    static readonly int ParamSpeed = Animator.StringToHash("speed");
    static readonly int ParamAttackTrig = Animator.StringToHash("attackTrigger");
    static readonly int ParamComboIndex = Animator.StringToHash("comboIndex");
    static readonly int ParamIsDashing = Animator.StringToHash("IsDashing");
    static readonly int ParamIsGrounded = Animator.StringToHash("isGrounded");
    static readonly int ParamVerticalVel = Animator.StringToHash("verticalVel");
    static readonly int ParamLand = Animator.StringToHash("land");
    static readonly int ParamIsCharging = Animator.StringToHash("isCharging");
    static readonly int ParamChargeAttackTrig = Animator.StringToHash("chargeAttack");
    static readonly int ParamIdle02Trig = Animator.StringToHash("idle02Trigger");
    static readonly int HashIdelState = Animator.StringToHash("idel");

    // ── 특수 대기모션(idle02) ─────────────────────────────
    [Header("특수 대기모션")]
    [Range(0f, 1f)]
    public float idle02Chance = 0.15f;
    float lastIdleNormalizedTime;
    bool wasInIdle;

    // ── 초기화 ────────────────────────────────────────────
    void Awake()
    {
        Instance = this;

        rigid = GetComponent<Rigidbody2D>();
        stats = GetComponent<Stats>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        CreateSwingAudioSource();
        originalGravity = rigid.gravityScale;
        platformLayer = LayerMask.NameToLayer("platform");
        baseAnimMaxTime = animPlayingMaxTime;
    }

    // [2026-10-10 추가] 캐릭터 교체로 다시 켜진 쪽이 Instance가 되도록 (Awake는 처음 한 번만 불림)
    void OnEnable()
    {
        Instance = this;
    }

    // [2026-10-10 추가] 파괴된 뒤에도 Instance가 남아 메뉴·인벤토리가 파괴된 플레이어에 접근하던 문제 방지
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 첫 번째 무기 자동 장착
        if (weapons != null && weapons.Length > 0)
            SwitchWeapon(weapons[0]);
    }

    // ── 무기 전환 ─────────────────────────────────────────
    public void SwitchWeapon(WeaponData newWeapon)
    {
        // [2026-09-29 추가] 공격/콤보 도중 무기가 바뀌면 옛 무기 기준 콤보 인덱스·애니메이션
        // 이벤트가 새 애니메이터 컨트롤러에서 안 맞아떨어져 최대 animPlayingMaxTime(기본 1.2초)
        // 동안 조작이 막힐 수 있었다. Dead()에서 쓰는 것과 동일한 방식으로 공격 상태를 먼저 정리.
        if (isAttacking) ForceEndAttack();

        EquippedWeapon = newWeapon;
        weapontype = newWeapon.weapontype;
        nowadvantage = newWeapon.advantage;
        if (stats != null) stats.advantage = newWeapon.advantage;

        // 애니메이션 교체
        if (newWeapon.animatorOverride != null)
            animator.runtimeAnimatorController = newWeapon.animatorOverride;

        // 히트박스 교체
        if (newWeapon.hitboxPrefab != null)
            spHitboxPrefab = newWeapon.hitboxPrefab;

        animPlayingMaxTime = baseAnimMaxTime;
        animator.speed = 1f;

        // 건틀릿 스택 초기화
        gauntletStack = 0;
        gauntletStackTimer = 0f;

        // 망치 차지 상태 강제 초기화 (무기 자체가 바뀌므로 무장 여부와 무관하게 리셋)
        isCharging = false;
        chargeArmed = false;
        chargeHeldTime = 0f;
        RemoveChargeEffect();
        animator.SetBool(ParamIsCharging, false);
        pendingChargedAttack = false;

        OnWeaponChanged?.Invoke(weapontype);
    }

    // ── 메인 루프 ─────────────────────────────────────────
    void Update()
    {
        if (stats.currentHp <= 0) { Dead(); return; }

        // 대화/컷씬 중 조작 잠금
        if (controlLocked)
        {
            rigid.velocity = new Vector2(0f, rigid.velocity.y);
            animator.SetFloat(ParamSpeed, 0f);
            return;
        }

        if (hitStunTimer > 0f) hitStunTimer -= Time.deltaTime;

        TryHitCancel(); // [2026-10-18 추가] 타격 후 캔슬 — 이동·점프보다 먼저 확인해야 같은 프레임에 바로 움직임

        Desh();
        Jump();
        Move();
        Dash();

        HandleAttackInput();
        HandleComboWindow();
        HandleAnimPlayingTimeout();
        HandleDropTimer();
        HandleIdleVariation();

        // 건틀릿 스택 타이머 + 공격 애니메이션 속도 (공격 중에만 적용)
        UpdateGauntletStack();
        float baseAttackAnimSpeed = (weapontype == Weapontype.Glove && isAttacking) ? GetGauntletSpeed() : 1f;
        animator.speed = baseAttackAnimSpeed * attackBuffAnimSpeedMultiplier;
    }

    // ── 특수 대기모션(idle02) 랜덤 트리거 ──────────────────
    // idel 상태가 한 바퀴(루프) 돌 때마다 낮은 확률로 idle02Trigger를 발동시켜
    // 특수 대기모션이 가끔 섞여 나오게 한다. (female.controller: idel -> idle02 -> idel)
    void HandleIdleVariation()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool inIdle = state.shortNameHash == HashIdelState;

        if (!inIdle)
        {
            wasInIdle = false;
            return;
        }

        float normalizedTime = state.normalizedTime % 1f;

        // 루프가 한 바퀴 끝나고 다시 처음으로 돌아오는 순간(0 부근으로 wrap)에만 확률 체크.
        if (wasInIdle && normalizedTime < lastIdleNormalizedTime)
        {
            if (UnityEngine.Random.value < idle02Chance)
                animator.SetTrigger(ParamIdle02Trig);
        }

        lastIdleNormalizedTime = normalizedTime;
        wasInIdle = true;
    }

    void FixedUpdate()
    {
        if (isDashing)
            rigid.velocity = new Vector2(dashDir * DashPower, 0);

        if (attackDashPending)
        {
            rigid.velocity = new Vector2(attackDashDir * attackDashForce, rigid.velocity.y);
            attackDashPending = false;
        }

        GroundedCheck();
        animator.SetFloat(ParamVerticalVel, rigid.velocity.y);
    }

    // FixedUpdate에서 이동(attackDashPending)이 실제로 반영된 뒤 호출되므로,
    // 콤보 이펙트를 여기서 소환하면 "이동 후 이펙트"가 보장된다.
    void LateUpdate()
    {
        if (comboEffectPending)
        {
            SpawnComboEffect(pendingComboEffectIndex);
            comboEffectPending = false;
        }
    }

    // ── 사운드 ────────────────────────────────────────────
    void PlaySfx(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, GameSettings.SfxVolume);   // [2026-10-18] 설정 창의 효과음 크기 적용
    }

    // 1단 점프 = jumpSfx / 2단 점프 = doubleJumpSfx (비어 있으면 jumpSfx)
    void PlayJumpSfx()
    {
        AudioClip clip = JumpCount >= 2 ? doubleJumpSfx.Pick() : null;
        if (clip == null) clip = jumpSfx.Pick();
        PlaySfx(clip);
    }

    // 기존 AudioSource 설정(볼륨, 믹서, 2D/3D 등)을 그대로 복사한 휘두르기 전용 AudioSource를 만든다
    void CreateSwingAudioSource()
    {
        if (audioSource == null) return;
        swingAudioSource = gameObject.AddComponent<AudioSource>();
        swingAudioSource.playOnAwake = false;
        swingAudioSource.loop = false;
        swingAudioSource.volume = audioSource.volume;
        swingAudioSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        swingAudioSource.spatialBlend = audioSource.spatialBlend;
        swingAudioSource.priority = audioSource.priority;
        swingAudioSource.enabled = audioSource.enabled;
    }

    /// <summary>
    /// 휘두르는 소리 재생. 건틀릿은 공격 속도(스택 + 공격 버프)에 맞춰 소리도 빨라진다.
    /// 소리 배속 = 1 + (애니메이션 배속 - 1) × swingSfxSpeedFollow, 최대 swingSfxMaxPitch.
    /// </summary>
    void PlaySwingSfx(AudioClip clip)
    {
        if (clip == null) return;
        if (swingAudioSource == null) { PlaySfx(clip); return; }

        float pitch = 1f;
        if (weapontype == Weapontype.Glove && EquippedWeapon != null)
        {
            float animSpeed = GetGauntletSpeed() * attackBuffAnimSpeedMultiplier;
            pitch = 1f + (animSpeed - 1f) * EquippedWeapon.swingSfxSpeedFollow;
            pitch = Mathf.Clamp(pitch, 1f, Mathf.Max(1f, EquippedWeapon.swingSfxMaxPitch));
        }

        swingAudioSource.enabled = audioSource == null || audioSource.enabled;
        swingAudioSource.pitch = pitch;
        swingAudioSource.PlayOneShot(clip, GameSettings.SfxVolume);   // [2026-10-18] 설정 창의 효과음 크기 적용
    }

    // ── 이동 ──────────────────────────────────────────────
    void Move()
    {
        if (isDashing) return;

        // [2026-10-10 추가] 피격 경직 중에는 넉백 속도를 입력으로 덮어쓰지 않는다
        // (예전엔 맞은 다음 프레임에 가로 속도가 입력값으로 바뀌어 옆으로 거의 밀리지 않았음)
        if (hitStunTimer > 0f)
        {
            animator.SetFloat(ParamSpeed, 0f);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");

        if (animPlaying)
        {
            // [2026-10-10 수정] 매 프레임 0.7배 → 프레임 수와 무관하게 60프레임 기준 0.7배/프레임과 같은 감속
            rigid.velocity = new Vector2(rigid.velocity.x * AttackSlideDecay(), rigid.velocity.y);
            animator.SetFloat(ParamSpeed, 0f);
            return;
        }

        if (isAttacking || isCharging)
        {
            // 공격/차지 중엔 이동 애니메이션 억제 (공격 우선), 방향 전환만 허용
            if (h != 0f) Flip(h);
            // [2026-10-10 추가] 망치 차지 중에는 달리던 속도로 미끄러지지 않게 공격과 같은 방식으로 감속
            if (isCharging)
                rigid.velocity = new Vector2(rigid.velocity.x * AttackSlideDecay(), rigid.velocity.y);
            animator.SetFloat(ParamSpeed, 0f);
            return;
        }

        Flip(h);
        rigid.velocity = new Vector2(h * stats.GetEffectiveSpd(), rigid.velocity.y);
        animator.SetFloat(ParamSpeed, Mathf.Abs(h));
    }

    // 60프레임 기준 "매 프레임 0.7배" 감속을 실제 경과 시간으로 환산한 배율
    float AttackSlideDecay() => Mathf.Pow(0.7f, Time.deltaTime * 60f);

    void Flip(float h)
    {
        if (h > 0) transform.localScale = Vector3.one;
        else if (h < 0) transform.localScale = new Vector3(-1, 1, 1);
    }

    // ── 플랫폼 드롭 ───────────────────────────────────────
    bool CheckOnPlatform()
    {
        return Physics2D.Raycast(rigid.position, Vector2.down,
            GroundCheckDistance, LayerMask.GetMask("platform")).collider != null;
    }

    void HandleDropTimer()
    {
        if (!isDropping) return;
        dropTimer += Time.deltaTime;
        if (dropTimer >= dropDuration)
        {
            gameObject.layer = originalLayer;
            isDropping = false;
            dropTimer = 0f;
        }
    }

    // ── 점프 ──────────────────────────────────────────────
    void Jump()
    {
        if (!Input.GetKeyDown(KeyCode.C)) return;

        bool downKey = Input.GetAxisRaw("Vertical") < -0.5f;

        if (downKey && !isDropping && CheckOnPlatform())
        {
            isDropping = true;
            dropTimer = 0f;
            originalLayer = gameObject.layer;
            gameObject.layer = LayerMask.NameToLayer("dropping");
            rigid.velocity = new Vector2(rigid.velocity.x, -10f);
            return;
        }

        if (JumpCount < 2 && !isAttacking && !isDashing && !isCharging)
        {
            rigid.velocity = new Vector2(rigid.velocity.x, 0);
            rigid.AddForce(Vector2.up * JumpPower, ForceMode2D.Impulse);
            JumpCount++;
            lastJumpTime = Time.time;
            PlayJumpSfx();
            animator.ResetTrigger(ParamLand);
            if (jumpEffectPrefab != null && jumpEffectPoint != null)
                Instantiate(jumpEffectPrefab, jumpEffectPoint.position, jumpEffectPoint.rotation);
        }
    }

    float lastJumpTime = -1f;   // 마지막 점프 시각 (점프 직후 점프 횟수가 되돌려지지 않게)
    Collider2D bodyCollider;
    // Scene 시각화용
    Vector2 lastCastOrigin;
    Vector2 lastCastSize;
    float lastCastDist;
    bool lastGrounded;

    void GroundedCheck()
    {
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();

        LayerMask mask = isDropping
            ? LayerMask.GetMask("ground")
            : LayerMask.GetMask("ground", "platform");

        // 1. 레이 시작점 = 콜라이더 바닥에서 약간 위
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y + 0.1f);

        // 2. 낙하 속도 보정 (터널링 방지) — 여유 거리를 조금 더 넉넉하게 잡아서
        // 바닥 엣지에 아슬아슬하게 걸치거나 비벼질 때 감지가 씹히는 걸 줄인다.
        float fallDist = Mathf.Abs(rigid.velocity.y) * Time.fixedDeltaTime;
        float castDist = 0.28f + fallDist;

        // 3. 박스캐스트 (콜라이더 너비 기준, 높이도 약간 키워서 엣지 판정을 더 관대하게)
        Vector2 boxSize = new Vector2(b.size.x * 0.9f, 0.15f);
        RaycastHit2D hit = Physics2D.BoxCast(
            origin, boxSize, 0f, Vector2.down, castDist, mask);

        bool grounded = hit.collider != null;

        // 시각화용 저장
        lastCastOrigin = origin;
        lastCastSize = boxSize;
        lastCastDist = castDist;
        lastGrounded = grounded;

        // 4. 착지 순간 — 착지 애니메이션 트리거는 "막 착지한 순간"에만 1회
        if (!wasGrounded && grounded && rigid.velocity.y <= 0)
        {
            animator.SetTrigger(ParamLand);
        }

        // 점프 횟수 리셋은 착지 "순간"이 아니라 바닥에 닿아있는 "동안" 매 프레임 계속 걸어준다.
        // 엣지에 걸쳐서 박스캐스트가 한두 프레임 반짝이며 놓쳐도(grounded가 true/false를 오가도)
        // 다음 프레임에 다시 true가 되면 바로 잡아내서, JumpCount가 2에서 영영 안 풀리는
        // (그 다음부터 점프가 안 되는) 문제를 막는다.
        //
        // [2026-10-09 수정] 단, 막 뛰어오르는 중에는 되돌리지 않는다. 판정 거리가 넉넉해서
        // 점프 직후 2~3 물리 프레임 동안 아직 바닥에 닿은 걸로 나오는데, 그때 0이 되면
        // 공중에서 2번 더 뛸 수 있는 3단 점프가 됐었다.
        // (점프하고 0.15초가 지났으면 위로 움직이는 중이어도 되돌림 → 오르막에서도 점프 가능)
        bool justJumped = rigid.velocity.y > 0.1f && Time.time - lastJumpTime < 0.15f;
        if (grounded && !justJumped)
            JumpCount = 0;

        wasGrounded = grounded;
        animator.SetBool(ParamIsGrounded, grounded);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 착지 감지 박스 시각화 (초록=감지, 빨강=미감지)
        Gizmos.color = lastGrounded ? Color.green : Color.red;

        Vector2 boxCenter = lastCastOrigin + Vector2.down * lastCastDist;
        Gizmos.DrawWireCube(lastCastOrigin, lastCastSize);          // 시작 박스
        Gizmos.DrawWireCube(boxCenter, lastCastSize);               // 도착 박스
        Gizmos.DrawLine(lastCastOrigin, boxCenter);                 // 경로
    }

    // ── 대쉬 ──────────────────────────────────────────────
    float lastTapTimeLeft = -1f;
    float lastTapTimeRight = -1f;
    [SerializeField] float doubleTapTime = 0.25f; // 더블탭 인정 간격

    void Desh()
    {
        // Z 키: 바라보는 방향으로 대시
        if (Input.GetKeyDown(KeyCode.Z))
        {
            TriggerDash(transform.localScale.x);
            return;
        }

        // 방향키 더블탭: 해당 방향으로 대시
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (Time.time - lastTapTimeRight <= doubleTapTime)
            {
                TriggerDash(1f);
                lastTapTimeRight = -1f;
            }
            else lastTapTimeRight = Time.time;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (Time.time - lastTapTimeLeft <= doubleTapTime)
            {
                TriggerDash(-1f);
                lastTapTimeLeft = -1f;
            }
            else lastTapTimeLeft = Time.time;
        }
    }

    void TriggerDash(float dir)
    {
        if (isDashing || DeshCount >= MaxDashCount) return;
        if (chargeArmed) return; // 무장된 차징은 대시로도 취소 불가 — 반드시 강타로 이어짐
        if (isAttacking) ForceEndAttack();
        CancelHammerCharge();

        isDashing = true;
        dashTimer = DashDuration;
        dashDir = dir;
        Flip(dir); // 대시 방향으로 캐릭터도 전환
        rigid.gravityScale = 0;
        rigid.velocity = Vector2.zero;
        DeshCount++;
        dashCooldownTimer = 0;
        animator.SetBool(ParamIsDashing, true);
        PlaySfx(dashSfx.Pick());

        GameObject dashPrefab = dashDir > 0 ? dashEffectPrefabRight : dashEffectPrefabLeft;
        if (dashPrefab != null && dashEffectPoint != null)
            Instantiate(dashPrefab, dashEffectPoint.position, Quaternion.identity);
    }

    void Dash()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
                EndDash(true);
        }

        if (DeshCount > 0)
        {
            dashCooldownTimer += Time.deltaTime;
            if (dashCooldownTimer >= DashCooldown)
            {
                DeshCount = 0;
                dashCooldownTimer = 0;
            }
        }
    }

    // 대시 종료 (keepMomentum = 대시 속도의 절반을 남겨 자연스럽게 감속 / false = 그 자리에서 멈춤)
    void EndDash(bool keepMomentum)
    {
        isDashing = false;
        dashTimer = 0f;
        rigid.gravityScale = originalGravity;
        rigid.velocity = new Vector2(keepMomentum ? dashDir * DashPower * 0.5f : 0f, rigid.velocity.y);
        animator.SetBool(ParamIsDashing, false);
    }

    // ── 공격 입력 ─────────────────────────────────────────
    void HandleAttackInput()
    {
        if (isDashing) return;

        // 공격 입력 버퍼 시작 (짧은 클립에서도 다음 콤보 예약 유지)
        if (Input.GetKeyDown(KeyCode.X))
            comboInputTimer = 0f;

        switch (weapontype)
        {
            case Weapontype.Sword: HandleSwordInput(); break;
            case Weapontype.Hammer: HandleHammerInput(); break;
            case Weapontype.Glove: HandleGauntletInput(); break;
            case Weapontype.Gun: HandleGunInput(); break;
        }
    }

    // ── 검: 기존 3단 콤보 ─────────────────────────────────
    void HandleSwordInput()
    {
        if (!Input.GetKeyDown(KeyCode.X)) return;
        if (!isAttacking) StartAttack(1);
        else if (comboQueued == 0) comboQueued = comboIndex + 1;
    }

    // ── 망치: 짧게 탭=기존 콤보, 누르고 있으면 차지 후 강타 ─
    void HandleHammerInput()
    {
        // 무기가 없으면 공격 입력 자체를 무시한다.
        // (StartAttack()에서도 막아주지만, 아래 차지 로직이 EquippedWeapon.chargeTime 등을
        //  그대로 참조하므로 null 상태로 넘어가지 않게 여기서 먼저 리턴한다)
        if (EquippedWeapon == null) return;

        if (isCharging)
        {
            // [2026-10-10 수정] 뗀 "순간"(GetKeyUp)만 보면, 메뉴·대화로 잠겨 있던 사이에 뗀 경우를 놓쳐
            // 차지 상태에 갇혔다. 지금 누르고 있지 않으면 뗀 것으로 처리한다.
            if (!Input.GetKey(KeyCode.X))
            {
                if (chargeArmed) ReleaseHammerCharge();
                else if (chargeFromCombo) CancelComboCharge();
                else EndHammerChargeAsTap();
                return;
            }

            if (Input.GetKey(KeyCode.X))
            {
                chargeHeldTime += Time.deltaTime;
                if (!chargeArmed && chargeHeldTime >= EquippedWeapon.chargeTime)
                {
                    chargeArmed = true;
                    animator.SetBool(ParamIsCharging, true); // 이 순간부터만 차징 애니메이션 재생
                    PlaySfx(EquippedWeapon.PickCharge());
                    SpawnChargeEffect();
                }

                // 최대 차지 시간(chargeTime + maxChargeTime)을 넘기면 손을 떼지 않아도 무조건 강타 발동
                float maxHoldTime = EquippedWeapon.chargeTime + EquippedWeapon.maxChargeTime;
                if (chargeArmed && chargeHeldTime >= maxHoldTime)
                {
                    ReleaseHammerCharge();
                    return;
                }
            }
            return;
        }

        // ── 1타/2타 콤보 진행 중, 계속 누르고 있는지 관찰 (놓으면 콤보, 다 채우면 차지로 전환) ─
        if (chargeHoldTracking)
        {
            if (Input.GetKey(KeyCode.X))
            {
                chargeHeldTime += Time.deltaTime;
                if (chargeHeldTime >= EquippedWeapon.chargeTime)
                {
                    chargeHoldTracking = false;
                    chargeQueued = true;
                    comboQueued = 0; // 다음 콤보 예약은 취소하고 차지로 전환 확정
                }
            }
            else
            {
                chargeHoldTracking = false;
                chargeHeldTime = 0f;
            }
        }

        if (!Input.GetKeyDown(KeyCode.X)) return;

        if (!isAttacking)
        {
            StartHammerCharge();
        }
        else
        {
            if (comboQueued == 0 && !chargeQueued) comboQueued = comboIndex + 1;
            chargeHoldTracking = true;
            chargeHeldTime = 0f;
        }
    }

    // ── 망치 차지: 콤보 도중 확정된 차지로 즉시 진입 (이미 chargeTime만큼 눌렀으므로 바로 무장 취급) ─
    void EnterChargeDirectly()
    {
        isCharging = true;
        chargeArmed = true;
        chargeHeldTime = EquippedWeapon.chargeTime;
        animator.SetBool(ParamIsCharging, true);
        PlaySfx(EquippedWeapon.PickCharge());
        SpawnChargeEffect();
    }

    // ── 망치 차지: 콤보 클립이 끝났을 때 아직 누르고 있던 경우 → 그때까지 누른 시간을 이어서 차지 ─
    void ContinueChargeFromCombo(float heldTime)
    {
        StartHammerCharge();
        chargeHeldTime = heldTime;
        chargeFromCombo = true;
    }

    // 콤보에서 이어진 차지를 무장 전에 뗀 경우: 평타를 새로 치지 않고 조용히 취소
    void CancelComboCharge()
    {
        isCharging = false;
        chargeFromCombo = false;
        chargeHeldTime = 0f;
        animator.SetBool(ParamIsCharging, false);
    }

    // ── 망치 차지: 시작 ───────────────────────────────────
    void StartHammerCharge()
    {
        isCharging = true;
        chargeArmed = false;
        chargeFromCombo = false;
        chargeHeldTime = 0f;
        // 애니메이터 isCharging은 여기서 켜지 않는다 — 실제로 '무장'되는 순간에만 켠다.
        // (그래야 짧게 탭했을 때 차징 애니메이션이 한 프레임도 보이지 않음)
    }

    // ── 망치 차지: chargeTime을 못 채우고 뗌 → 평타(기존 콤보) 처리 ─
    void EndHammerChargeAsTap()
    {
        isCharging = false;
        chargeHeldTime = 0f;
        animator.SetBool(ParamIsCharging, false);
        if (!isAttacking) StartAttack(1);
        else if (comboQueued == 0) comboQueued = comboIndex + 1;
    }

    // ── 망치 차지: 무장 후 릴리즈 → 강타(콤보 3 / hammer_3 애니메이션) ─
    void ReleaseHammerCharge()
    {
        float extra = Mathf.Clamp(chargeHeldTime - EquippedWeapon.chargeTime,
                                   0f, Mathf.Max(0.01f, EquippedWeapon.maxChargeTime));
        float t = EquippedWeapon.maxChargeTime > 0f ? extra / EquippedWeapon.maxChargeTime : 1f;
        pendingChargeMultiplier = Mathf.Lerp(EquippedWeapon.minChargeMultiplier,
                                              EquippedWeapon.maxChargeMultiplier, t);
        pendingChargedAttack = true;

        isCharging = false;
        chargeArmed = false;
        chargeFromCombo = false;
        chargeHeldTime = 0f;
        animator.SetBool(ParamIsCharging, false);
        RemoveChargeEffect();

        StartAttack(3);
        animator.SetTrigger(ParamChargeAttackTrig); // Atk_Charge -> Atk03 전용 트리거
    }

    // ── 망치 차지: 대시/피격 등으로 중간에 끊김 ───────────
    public void CancelHammerCharge()
    {
        // 짧게 치는 것(무장 전)만 취소 가능. 한 번 무장(chargeArmed)되면
        // 대시/피격 등 무엇으로도 취소되지 않고 반드시 강타로 이어져야 함.
        if (!isCharging || chargeArmed) return;
        isCharging = false;
        chargeFromCombo = false;
        chargeHeldTime = 0f;
        animator.SetBool(ParamIsCharging, false);
        RemoveChargeEffect();
    }

    void SpawnChargeEffect()
    {
        if (EquippedWeapon == null || EquippedWeapon.chargeEffectPrefab == null) return;
        Vector3 pos = spHitboxSpawnPoint != null ? spHitboxSpawnPoint.position : transform.position;
        activeChargeEffect = Instantiate(EquippedWeapon.chargeEffectPrefab, pos, Quaternion.identity, transform);
    }

    void RemoveChargeEffect()
    {
        if (activeChargeEffect != null)
        {
            Destroy(activeChargeEffect);
            activeChargeEffect = null;
        }
    }

    // ── 건틀릿: 콤보 + 피격 시 속도 스택 ──────────────────
    void HandleGauntletInput()
    {
        if (!Input.GetKeyDown(KeyCode.X)) return;
        if (!isAttacking) StartAttack(1);
        else if (comboQueued == 0) comboQueued = comboIndex + 1;
    }

    /// <summary>건틀릿 공격이 적을 맞혔을 때 Hitbox가 호출. 스택 +1.</summary>
    public void OnGauntletHit()
    {
        if (weapontype != Weapontype.Glove) return;
        gauntletStack++;
        gauntletStackTimer = 0f;
    }

    /// <summary>현재 건틀릿 스택에 따른 공격 속도 배율 (1 ~ 최대).</summary>
    float GetGauntletSpeed()
    {
        if (weapontype != Weapontype.Glove || EquippedWeapon == null) return 1f;
        float speed = 1f + gauntletStack * EquippedWeapon.stackSpeedPerHit;
        return Mathf.Min(speed, EquippedWeapon.maxStackSpeed);
    }

    /// <summary>건틀릿 스택 타이머. n초 미공격 시 초기화.</summary>
    void UpdateGauntletStack()
    {
        if (weapontype != Weapontype.Glove || gauntletStack <= 0) return;
        gauntletStackTimer += Time.deltaTime;
        if (EquippedWeapon != null && gauntletStackTimer >= EquippedWeapon.stackResetTime)
        {
            gauntletStack = 0;
            gauntletStackTimer = 0f;
        }
    }

    // ── 총: 투사체 발사 ───────────────────────────────────
    void HandleGunInput()
    {
        if (!Input.GetKeyDown(KeyCode.X)) return;

        if (!isAttacking) StartAttack(1);
        else if (comboQueued == 0) comboQueued = comboIndex + 1;
    }

    // 총 빔 프리펩·지속시간은 WeaponData(무기별)에서 가져옴

    public void FireProjectile()
    {
        int dir = (int)Mathf.Sign(transform.localScale.x);
        Vector2 origin = spHitboxSpawnPoint != null
                       ? (Vector2)spHitboxSpawnPoint.position
                       : (Vector2)transform.position;

        // 콤보별 총구 오프셋 (좌우 자동 반전)
        var muzzleOffsets = EquippedWeapon?.gunBeamMuzzleOffsets;
        if (muzzleOffsets != null && muzzleOffsets.Length > 0)
        {
            int mi = Mathf.Clamp((comboIndex > 0 ? comboIndex : 1) - 1, 0, muzzleOffsets.Length - 1);
            Vector2 off = muzzleOffsets[mi];
            origin += new Vector2(off.x * dir, off.y);
        }

        float range = EquippedWeapon?.gunRange ?? 20f;

        // [2026-10-10 추가] 벽·바닥(ground)에서 레이저가 멈춘다 (적은 계속 관통, 한쪽 통과 발판은 막지 않음)
        RaycastHit2D wall = Physics2D.Raycast(origin, new Vector2(dir, 0), range, LayerMask.GetMask("ground"));
        if (wall.collider != null) range = wall.distance;

        Vector2 end = origin + new Vector2(dir * range, 0);

        // 레이저 시각화
        ShowLaser(origin, end);

        // 직선상 모든 적 감지 (모든 레이어)
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(dir, 0), range);

        // [2026-10-10 수정] 근접 공격과 같은 피해 경로(치명타·퍼즈 중 치명타 보너스·상성·피격음·화면 흔들림·숫자)를 거친다.
        // 예전엔 Health.TakeDamage를 직접 불러서 이것들이 전부 빠지고 퍼즈 게이지도 오르지 않았다.
        int rawDamage = Mathf.RoundToInt(stats.Atk * (EquippedWeapon?.damageMultiplier ?? 1f) * attackBuffDamageMultiplier);
        HashSet<Health> damaged = new HashSet<Health>();
        bool anyHit = false;
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            if (Hitbox.IsHitboxCollider(hit.collider)) continue; // 공격 판정 상자는 맞는 대상이 아님
            Health health = Health.Resolve(hit.collider);
            if (health == null || health.gameObject == gameObject || damaged.Contains(health)) continue;
            if (health.CompareTag("Player")) continue;
            damaged.Add(health);

            if (Hitbox.DealDirectHit(health, rawDamage, stats, dir, 5f, 3f, hit.point,
                                     EquippedWeapon != null ? EquippedWeapon.PickHit() : null,
                                     EquippedWeapon != null ? EquippedWeapon.PickCritical() : null,
                                     "Gun", true))
                anyHit = true;
        }

        // 퍼즈 게이지: 한 발에 적을 하나라도 맞혔으면 한 번만 충전 (관통으로 여러 마리 맞아도 한 번)
        if (anyHit)
        {
            PauseManager.Instance?.AddGauge(EquippedWeapon != null ? EquippedWeapon.pauseGaugePerHit : 2.5f);
            OnGauntletHit();
        }

        PlaySwingSfx(EquippedWeapon != null ? EquippedWeapon.PickSwing() : null);
        SpawnComboEffect(comboIndex > 0 ? comboIndex : 1);
        RegisterAttackHitEvent(); // [2026-10-18 추가] 타격 후 캔슬용 횟수 세기
    }

    void ShowLaser(Vector2 start, Vector2 end)
    {
        GameObject beamPrefab = EquippedWeapon != null ? EquippedWeapon.gunBeamPrefab : null;
        if (beamPrefab == null) return;

        float duration = EquippedWeapon != null ? EquippedWeapon.gunBeamDuration : 0.1f;

        Vector2 diff = end - start;
        float distance = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        // 각도 랜덤 흔들림 (시각용, 판정에는 영향 없음)
        float jitter = EquippedWeapon != null ? EquippedWeapon.gunBeamAngleJitter : 0f;
        if (jitter > 0f)
            angle += UnityEngine.Random.Range(-jitter, jitter);

        // 총구(start)에 소환, 발사 방향으로 회전
        GameObject beam = Instantiate(beamPrefab, start, Quaternion.Euler(0f, 0f, angle));

        SpriteRenderer sr = beam.GetComponent<SpriteRenderer>()
                         ?? beam.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // 스프라이트 원본 가로 길이(유닛) 기준으로 거리에 맞춰 X 스케일
            float baseWidth = sr.sprite.bounds.size.x;
            if (baseWidth > 0f)
            {
                Vector3 s = beam.transform.localScale;
                s.x = distance / baseWidth;
                beam.transform.localScale = s;
            }
        }

        Destroy(beam, duration);
    }

    // ── 공격 공통 로직 ────────────────────────────────────
    void StartAttack(int index)
    {
        if (EquippedWeapon == null) return; // 무기가 없으면 공격 자체를 시작하지 않음
        if (isAttacking && animPlaying && comboIndex == index) return;

        animator.speed = 1f;

        // 콤보 전환 시 방향키 누르고 있으면 그 방향으로 이동
        if (index > 1)
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (h != 0f)
            {
                Flip(h);
                rigid.velocity = new Vector2(h * attackDashForce, rigid.velocity.y);
            }
        }

        isAttacking = true;
        animPlaying = true;
        animPlayingTimeout = 0f;
        comboIndex = index;
        comboQueued = 0;
        comboInputTimer = -1f; // 시작과 함께 버퍼 소비
        hitboxTimer = 0f;
        hitboxSpawned = false;
        ResetHitCancel(); // [2026-10-18 추가]
        gaugeChargedThisSwing = false; // [2026-09-29 수정] 이번 스윙 새로 시작 → 게이지 충전 게이트 리셋.
                                        // (기존엔 한 번 켜진 뒤 다시 꺼지는 곳이 없어서 첫 충전 이후
                                        //  영구히 막히는 버그였음. 단, 현재 TryChargeGauge()를 실제로
                                        //  호출하는 곳이 코드 전체에 없어서 지금 당장 체감되는 버그는
                                        //  아니고, 나중에 이 게이트를 실제로 연결할 때를 대비한 수정.)

        animator.SetInteger(ParamComboIndex, index);
        animator.SetTrigger(ParamAttackTrig);
        animator.SetFloat(ParamSpeed, 0f);

        PlaySwingSfx(EquippedWeapon.PickSwing());

    }

    void HandleComboWindow()
    {
        // 입력 버퍼 타이머 감소
        if (comboInputTimer >= 0f)
            comboInputTimer += Time.deltaTime;

        if (animPlaying || !isAttacking) return;

        // 망치: 콤보 도중 차지로 전환이 확정된 경우 — 다음 콤보 대신 바로 차징 상태로 진입
        if (weapontype == Weapontype.Hammer && chargeQueued)
        {
            chargeQueued = false;
            EndAttack();
            EnterChargeDirectly();
            return;
        }

        // 건틀릿은 애니메이션이 빨라진 만큼 입력 버퍼도 늘려 콤보 유지
        float effectiveBuffer = comboInputBufferTime;
        if (weapontype == Weapontype.Glove)
            effectiveBuffer *= GetGauntletSpeed();

        // 큐 또는 버퍼에 유효 입력이 있으면 다음 콤보
        bool buffered = comboInputTimer >= 0f && comboInputTimer <= effectiveBuffer;

        if (comboQueued > 0 || buffered)
        {
            int next = comboQueued > 0 ? comboQueued : comboIndex + 1;
            comboQueued = 0;
            comboInputTimer = -1f; // 버퍼 소비

            int maxCombo = (weapontype == Weapontype.Hammer) ? 2 : 3;
            if (next <= maxCombo) StartAttack(next);
            else EndAttack();
            return;
        }

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f) EndAttack();
    }

    void HandleAnimPlayingTimeout()
    {
        if (!animPlaying) return;
        animPlayingTimeout += Time.deltaTime;

        // 타임아웃은 이벤트 누락 대비 백업. 콤보 진행은 OnAttackAnimEnd가 주도.
        if (animPlayingTimeout >= baseAnimMaxTime)
        {
            animPlaying = false;
            animPlayingTimeout = 0f;
            comboTimer = comboWindow;
        }
    }

    void ExecuteHitbox()
    {
        // [2026-10-10 수정] 타격 순간 방향키 처리(방향 전환 + 짧은 돌진)를 판정 생성보다 먼저 한다.
        // 예전엔 판정을 원래 방향으로 만든 뒤에 몸을 돌려서, 판정·넉백은 앞쪽인데 캐릭터·이펙트·돌진은 반대쪽이었다.
        float h = Input.GetAxisRaw("Horizontal");
        if (h != 0f)
        {
            Flip(h);
            attackDashPending = true;
            attackDashDir = h;
        }

        // 콤보별 이펙트 + 효과음
        // (이동보다 이펙트가 먼저 보이지 않도록, 즉시 재생하지 않고 LateUpdate에서 소환 — 이동이 실제로 반영된 뒤가 됨)
        comboEffectPending = true;
        pendingComboEffectIndex = comboIndex;

        bool isChargedHit = pendingChargedAttack;
        pendingChargedAttack = false; // 소비 (강제 중단되더라도 다음 타격에 영향 없도록 여기서 바로 리셋)

        GameObject prefabToUse = spHitboxPrefab;
        if (isChargedHit && EquippedWeapon != null && EquippedWeapon.chargeHitboxPrefab != null)
            prefabToUse = EquippedWeapon.chargeHitboxPrefab;

        if (prefabToUse == null || spHitboxSpawnPoint == null) return;

        Vector3 spawnPos = spHitboxSpawnPoint.localPosition;
        spawnPos.x *= transform.localScale.x;

        GameObject obj = Instantiate(prefabToUse,
                                     transform.position + spawnPos, Quaternion.identity);

        // 플레이어 방향에 맞게 히트박스 좌우반전
        Vector3 hbScale = obj.transform.localScale;
        hbScale.x = Mathf.Abs(hbScale.x) * Mathf.Sign(transform.localScale.x);
        obj.transform.localScale = hbScale;

        Hitbox hb = obj.GetComponent<Hitbox>();
        if (hb != null)
        {
            hb.direction = (int)Mathf.Sign(transform.localScale.x);
            float dmgMultiplier = (EquippedWeapon != null ? EquippedWeapon.damageMultiplier : 1f)
                                 * (isChargedHit ? pendingChargeMultiplier : 1f)
                                 * attackBuffDamageMultiplier;
            hb.damage = Mathf.RoundToInt(stats.Atk * dmgMultiplier);
            // DEF 계산은 Hitbox.OnTriggerEnter2D에서 처리
            hb.attackerStats = stats;
            if (EquippedWeapon != null)
            {
                hb.hitSfx = EquippedWeapon.PickHit();
                hb.criticalSfx = EquippedWeapon.PickCritical();
            }
            if (isChargedHit && EquippedWeapon != null)
            {
                hb.knockbackX = EquippedWeapon.chargeKnockbackX;
                hb.knockbackY = EquippedWeapon.chargeKnockbackY;
                hb.heavyImpact = true;
            }
        }
    }

    // ── 공격 강화 버프: 적용 / 해제 (Skill.cs에서 호출) ───
    /// <summary>
    /// duration초 동안 평타 데미지·애니메이션 속도를 강화하고, 선택적으로 오라/추가 타격 이펙트를 재생한다.
    /// 이미 버프가 걸려있으면 새 값으로 갱신(기존 코루틴은 취소하고 새로 시작).
    /// </summary>
    public void ApplyAttackBuff(float damageMultiplier, float animSpeedMultiplier, float duration,
                                 GameObject auraPrefab = null, GameObject extraHitEffectPrefab = null)
    {
        if (attackBuffRoutine != null) StopCoroutine(attackBuffRoutine);
        attackBuffRoutine = StartCoroutine(AttackBuffRoutine(damageMultiplier, animSpeedMultiplier, duration, auraPrefab, extraHitEffectPrefab));
    }

    IEnumerator AttackBuffRoutine(float dmgMult, float animMult, float duration, GameObject auraPrefab, GameObject extraHitEffectPrefab)
    {
        attackBuffDamageMultiplier = dmgMult;
        attackBuffAnimSpeedMultiplier = animMult;
        attackBuffExtraEffectPrefab = extraHitEffectPrefab;

        if (activeAttackBuffAura != null) Destroy(activeAttackBuffAura);
        if (auraPrefab != null)
            activeAttackBuffAura = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);

        yield return new WaitForSeconds(duration);

        attackBuffDamageMultiplier = 1f;
        attackBuffAnimSpeedMultiplier = 1f;
        attackBuffExtraEffectPrefab = null;
        if (activeAttackBuffAura != null)
        {
            Destroy(activeAttackBuffAura);
            activeAttackBuffAura = null;
        }
        attackBuffRoutine = null;
    }

    // ── 콤보별 이펙트 + 효과음 ────────────────────────────
    void SpawnComboEffect(int index)
    {
        // 공격 강화 버프 중이면 평타마다 추가 이펙트 재생 (콤보 이펙트 유무와 무관)
        if (attackBuffExtraEffectPrefab != null)
        {
            Vector3 buffFxPos = spHitboxSpawnPoint != null ? spHitboxSpawnPoint.position : transform.position;
            GameObject buffFx = Instantiate(attackBuffExtraEffectPrefab, buffFxPos, Quaternion.identity);
            Destroy(buffFx, 1f);
        }

        if (EquippedWeapon == null || EquippedWeapon.comboEffects == null) return;

        int i = index - 1;
        if (i < 0 || i >= EquippedWeapon.comboEffects.Length) return;

        ComboEffect ce = EquippedWeapon.comboEffects[i];
        if (ce == null) return;

        // 효과음 (콤보당 1개)
        PlaySfx(ce.PickSfx());   // PlaySfx가 null이면 알아서 무시

        // 이펙트 조각들 각각 소환
        if (ce.pieces == null) return;
        foreach (var piece in ce.pieces)
            SpawnEffectPiece(piece);
    }

    void SpawnEffectPiece(EffectPiece piece)
    {
        if (piece == null || piece.prefab == null) return;

        float dir = transform.localScale.x >= 0 ? 1f : -1f;

        Vector3 origin = spHitboxSpawnPoint != null ? spHitboxSpawnPoint.position : transform.position;
        Vector3 offsetPos = new Vector3(piece.posOffset.x * dir, piece.posOffset.y, piece.posOffset.z);
        Vector3 spawnPos = origin + offsetPos;

        GameObject anchor = new GameObject("EffectAnchor");
        anchor.transform.position = spawnPos;

        GameObject fx = Instantiate(piece.prefab, anchor.transform);
        fx.transform.localPosition = Vector3.zero;

        // 프리펩 자체를 회전 (Z축)
        fx.transform.localRotation = Quaternion.Euler(0f, 0f, piece.rotationZ);

        var allSystems = fx.GetComponentsInChildren<ParticleSystem>(true);

        // 최종 반전 여부 = 왼쪽 방향 XOR 수동 flipX
        bool doFlip = (dir < 0f) ^ piece.flipX;

        if (doFlip)
        {
            var renderers = fx.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
                r.flip = new Vector3(1f, r.flip.y, r.flip.z);

            foreach (var ps in allSystems)
            {
                var rol = ps.rotationOverLifetime;
                if (rol.enabled) rol.zMultiplier *= -1f;

                var rbs = ps.rotationBySpeed;
                if (rbs.enabled) rbs.zMultiplier *= -1f;
            }
        }

        fx.SetActive(true);

        // 삭제 시간
        float life = piece.lifetime;
        if (life <= 0f)
        {
            life = 1f; // 기본값
            var ps = fx.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                life = main.duration + main.startLifetime.constantMax;
            }
        }
        Destroy(anchor, life);
    }

    public void SpawnHitbox()
    {
        if (!hitboxSpawned)
        {
            ExecuteHitbox();
            hitboxSpawned = true;
        }
        RegisterAttackHitEvent(); // [2026-10-18 추가] 타격 후 캔슬용 횟수 세기
    }

    // ── 타격 후 캔슬 (2026-10-18 추가) ──────────────────────
    void ResetHitCancel()
    {
        hitEventsThisSwing = 0;
        hitEventsInClip = -1;
        hitCancelTimer = -1f;
        hitCancelDir = 0f;
    }

    // 타격 이벤트가 나올 때마다 호출. 이번 클립의 타격 이벤트가 다 나오면 캔슬 대기 시작.
    void RegisterAttackHitEvent()
    {
        if (!isAttacking) return;
        hitEventsThisSwing++;
        if (hitEventsInClip < 0) hitEventsInClip = CountHitEventsInCurrentClip();
        if (hitCancelTimer < 0f && hitEventsThisSwing >= hitEventsInClip)
        {
            hitCancelTimer = 0f;
            float h = Input.GetAxisRaw("Horizontal");
            hitCancelDir = h > 0f ? 1f : (h < 0f ? -1f : 0f);
        }
    }

    // 지금 재생 중인 공격 클립(전환 중이면 넘어가는 쪽)에 SpawnHitbox / FireProjectile 이벤트가 몇 개인지
    int CountHitEventsInCurrentClip()
    {
        AnimatorClipInfo[] infos = animator.IsInTransition(0)
                                 ? animator.GetNextAnimatorClipInfo(0)
                                 : animator.GetCurrentAnimatorClipInfo(0);
        if (infos == null || infos.Length == 0) infos = animator.GetCurrentAnimatorClipInfo(0);

        AnimationClip clip = null;
        float bestWeight = -1f;
        foreach (var info in infos)
        {
            if (info.clip != null && info.weight > bestWeight) { bestWeight = info.weight; clip = info.clip; }
        }
        if (clip == null) return 1;

        int count = 0;
        foreach (var e in clip.events)
            if (e.functionName == nameof(SpawnHitbox) || e.functionName == nameof(FireProjectile)) count++;
        return Mathf.Max(1, count);
    }

    void TryHitCancel()
    {
        if (!isAttacking || isCharging || isDashing || hitCancelTimer < 0f) return;
        if (hitStunTimer > 0f || EquippedWeapon == null || EquippedWeapon.hitCancelDelay < 0f) return;

        float h = Input.GetAxisRaw("Horizontal");
        float dir = h > 0f ? 1f : (h < 0f ? -1f : 0f);
        if (dir == 0f) hitCancelDir = 0f; // 손을 떼면 다음에 누르는 건 새 입력

        hitCancelTimer += Time.deltaTime;
        if (hitCancelTimer < EquippedWeapon.hitCancelDelay) return;

        // 망치: 콤보 도중 X를 길게 눌러 차지로 넘어가는 중이면 끊지 않음
        if (chargeHoldTracking && Input.GetKey(KeyCode.X)) return;

        // 점프 캔슬 — 공격을 끝내 두면 바로 아래 Jump()가 그대로 점프시킨다 (아래+점프 = 발판 내리기는 제외)
        bool downKey = Input.GetAxisRaw("Vertical") < -0.5f;
        if (Input.GetKeyDown(KeyCode.C) && !downKey && JumpCount < 2)
        {
            EndAttack();
            animator.CrossFade("Jump_Rise", 0.05f, 0); // 3타처럼 끝까지 재생해야 빠져나오는 동작도 바로 점프 동작으로
            return;
        }

        // 이동 캔슬 — 새로 누르거나 반대로 바꿨을 때만. 다음 콤보가 예약돼 있으면 콤보 우선.
        if (dir != 0f && dir != hitCancelDir)
        {
            bool comboWanted = comboQueued > 0
                            || (comboInputTimer >= 0f && comboInputTimer <= comboInputBufferTime);
            if (comboWanted) return;

            EndAttack();
            animator.CrossFade("f_Run", 0.05f, 0); // 3타처럼 끝까지 재생해야 빠져나오는 동작도 바로 달리기로
        }
    }

    // ── 공격 종료 ─────────────────────────────────────────
    public void OnAttackAnimEnd()
    {
        if (!isAttacking) return;
        // 이미 콤보 대기 중이면 무시.
        // (클립이 Loop면 이벤트가 반복 호출돼 대기 타이머가 계속 리셋되는 것 방지)
        if (!animPlaying) return;

        animPlaying = false;
        animPlayingTimeout = 0f;

        // 콤보 입력 대기 시작.
        // (건틀릿도 여기선 늘리지 않음 — 늘리면 그만큼 이동이 잠김.
        //  빠른 애니메이션 대응은 입력 버퍼 확장으로 처리)
        comboTimer = comboWindow;
    }

    public void EndAttack_Anim()
    {
        // [2026-10-10 수정] 망치 콤보 도중 길게 누르는 중이었는데 클립 이벤트로 공격이 먼저 끝나면
        // 차지 전환이 사라졌다(2타는 콤보 대기 단계를 거치지 않고 바로 끝나기 때문).
        if (weapontype == Weapontype.Hammer && isAttacking && EquippedWeapon != null)
        {
            if (chargeQueued)
            {
                // 이미 충분히 눌러 차지가 확정된 경우 → 바로 무장된 차지로
                EndAttack();
                EnterChargeDirectly();
                return;
            }
            if (chargeHoldTracking && Input.GetKey(KeyCode.X))
            {
                // 아직 다 채우진 못했지만 누르고 있는 중 → 누른 시간을 이어서 차지 진행
                float held = chargeHeldTime;
                EndAttack();
                ContinueChargeFromCombo(held);
                return;
            }
        }
        EndAttack();
    }

    public void EndAttack()
    {
        isAttacking = animPlaying = false;
        animPlayingTimeout = comboTimer = 0f;
        comboIndex = comboQueued = 0;
        comboInputTimer = -1f;
        hitboxSpawned = true;
        pendingChargedAttack = false;
        chargeHoldTracking = false;
        chargeQueued = false;
        animator.SetInteger(ParamComboIndex, 0);
        animator.ResetTrigger(ParamAttackTrig);
        animator.speed = 1f; // 공격 끝나면 속도 복원
        hitCancelTimer = -1f; // [2026-10-18 추가]
    }

    void ForceEndAttack()
    {
        isAttacking = animPlaying = false;
        animPlayingTimeout = comboTimer = 0f;
        comboIndex = comboQueued = 0;
        hitboxSpawned = true;
        pendingChargedAttack = false;
        chargeHoldTracking = false;
        chargeQueued = false;
        animator.ResetTrigger(ParamAttackTrig);
        hitCancelTimer = -1f; // [2026-10-18 추가]
    }

    // ── 넉백 ──────────────────────────────────────────────
    public void OnHit(int dir, float knockbackX = 5f, float knockbackY = 3f)
    {
        CancelHammerCharge();
        rigid.velocity = Vector2.zero;
        rigid.AddForce(new Vector2(dir * knockbackX, knockbackY), ForceMode2D.Impulse);
        hitStunTimer = hitStunTime; // 넉백이 다음 프레임 이동 입력에 지워지지 않게
    }

    // ── 무기 획득 ─────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D obj)
    {
        Weapon weapon = obj.GetComponentInParent<Weapon>();
        if (weapon != null)
        {
            weapon.ApplyTo(stats, this); // SwitchWeapon 안에서 OnWeaponChanged를 이미 호출함 (여기서 또 부르면 두 번)
            Destroy(weapon.gameObject);
            return;
        }

        // ── 스킬 획득 (박스 등에서 드랍된 SkillPickup에 닿으면 즉시 습득) ──
        // [2026-10-17] F키 줍기가 켜진 스킬 구슬은 SkillPickup 쪽에서 팝업 + F키로 처리하므로 여기서는 건너뜀
        SkillPickup skillPickup = obj.GetComponentInParent<SkillPickup>();
        if (skillPickup != null && skillPickup.skillPrefab != null && !skillPickup.pickupWithKey)
        {
            CharacterSkills skills = GetComponent<CharacterSkills>();
            if (skills != null && skills.AcquireSkill(skillPickup.skillPrefab))
                Destroy(skillPickup.gameObject);
        }
    }

    // [2026-10-10 수정] 체력이 Health를 거치지 않고 0이 된 경우(테스트 키 등)에도 정식 사망 처리(게임오버 등)를 탄다
    void Dead()
    {
        Health health = GetComponent<Health>();
        if (health != null) health.ForceDie();
        else Destroy(gameObject);
    }
}