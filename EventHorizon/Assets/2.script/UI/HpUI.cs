using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// [2026-09-29 통합] BossHpUI.cs를 이 스크립트로 흡수 통합.
// 두 스크립트가 체력바 트윈/잔상(칩 데미지)/피격 번쩍임·흔들림/저체력 색상 변화 로직이
// 사실상 100% 동일했고(추적 대상을 찾는 방식만 다름), 한쪽만 고치고 한쪽을 빠뜨리는 사고가
// 반복될 위험이 있어서 targetMode로 전환해서 쓰는 방식으로 합쳤다.
public enum HpUITargetMode
{
    Player, // CharacterManager를 통해 "현재 조작 중인 캐릭터"를 추적 (캐릭터 교체에도 자동 대응)
    Boss,   // 씬에서 BossController를 찾아서 추적. 이 오브젝트보다 늦게(방 진입 트리거 이후)
            // 스폰되는 경우를 대비해 찾을 때까지 주기적으로 재시도하고, 보스가 진짜로 사망하면
            // HP 캔버스 자체를 화면에서 치운다.
}

public class HpUI : MonoBehaviour
{
    [Header("추적 대상")]
    public HpUITargetMode targetMode = HpUITargetMode.Player;
    [Tooltip("Boss 모드에서 BossController를 찾는 재시도 간격(초). Player 모드에서는 사용하지 않음.")]
    public float findRetryInterval = 0.5f;

    public Slider hpSlider;

    [Header("데미지 / 회복 애니메이션 - 격투게임 스타일")]
    [Tooltip("데미지(체력 감소)일 때 초록 바가 새 값까지 걸리는 시간(초). 뚝 끊기지 않고 실제로 " +
             "감속되면서 줄어드는 곡선을 유지한다(잔상 바만 격투게임처럼 딱딱한 일정 속도로 뒤따라감).")]
    public float damageDuration = 0.25f;
    public Ease damageEase = Ease.OutQuad;
    [Tooltip("회복(체력 증가)일 때 걸리는 시간(초)")]
    public float healDuration = 0.4f;
    public Ease healEase = Ease.OutSine;

    [Header("데미지 잔상(칩 데미지) 바")]
    [Tooltip("체력바의 Fill Area 안에 있는 보조 Image. 데미지를 입으면 초록 바는 바로 뚝 떨어지고, " +
             "이 잔상 바가 원래 값에 잠깐 그대로 남아있다가 일정한 속도로 천천히 새 값까지 줄어들어서 " +
             "'방금 이만큼 맞았다'를 보여준다(격투게임 체력바의 그 노란/하얀 잔상). 비워두면 이 기능은 꺼짐.")]
    public Image trailFillImage;
    [Tooltip("잔상 바 색상")]
    public Color trailColor = Color.white;
    [Tooltip("데미지를 입은 뒤 잔상 바가 줄어들기 시작하기까지 대기하는 시간(초)")]
    public float trailDelay = 0.3f;
    [Tooltip("잔상 바가 줄어드는 속도(초당 줄어드는 체력 비율). 예를 들어 0.5면 바 전체(0~1)를 " +
             "다 줄이는 데 2초가 걸림. 데미지 '시간'이 아니라 '속도'를 고정해서, 크게 맞을수록 " +
             "잔상이 더 오래 남아있게 되는 격투게임 특유의 긴장감을 만든다.")]
    public float trailDrainSpeed = 0.5f;

    [Header("피격 시 살짝 번쩍임")]
    [Tooltip("맞았을 때 체력바가 살짝 하얗게 번쩍이는 연출을 켤지 여부.")]
    public bool enableHitReaction = true;
    [Tooltip("맞았을 때 번쩍이는 색")]
    public Color hitFlashColor = Color.white;
    [Tooltip("번쩍이는 데 걸리는 시간(초). 갔다가 돌아오는 왕복이라 실제 체감 시간은 이 값의 2배")]
    public float hitFlashDuration = 0.05f;

    [Header("피격 시 아주 살짝 흔들림 (쉐이킹)")]
    [Tooltip("맞았을 때 체력바 UI 자체가 아주 미세하게 흔들리는 연출을 켤지 여부. 스케일(크기)은 " +
             "안 건드리고 위치만 살짝 떨리게 한다.")]
    public bool enableShake = true;
    [Tooltip("흔들리는 세기(픽셀 단위). '진짜 아주 약간'만 흔들리길 원해서 기본값을 아주 작게 잡음.")]
    public float shakeStrength = 1.5f;
    [Tooltip("흔들리는 데 걸리는 시간(초)")]
    public float shakeDuration = 0.15f;
    [Tooltip("흔들리는 동안 떨리는 횟수(진동수). 값이 클수록 자잘하게 파르르 떤다.")]
    public int shakeVibrato = 12;

    [Header("체력 낮으면 색 변화 (선택)")]
    [Tooltip("체력 비율에 따라 채워진 바 색을 초록 -> 노랑 -> 빨강으로 자연스럽게 바꿀지 여부")]
    public bool enableLowHpColor = true;
    public Color fullHpColor = new Color(0.25f, 0.85f, 0.3f);
    public Color midHpColor = new Color(0.95f, 0.8f, 0.15f);
    public Color lowHpColor = new Color(0.9f, 0.15f, 0.15f);
    [Range(0f, 1f)] public float midHpThreshold = 0.5f;
    [Range(0f, 1f)] public float lowHpThreshold = 0.25f;

    Stats targetStats;
    BossController bossRef; // Boss 모드에서만 사용 — 사망 시 캔버스를 숨기기 위해 OnDied 구독
    Image fillImage;
    Vector3 basePosition; // 흔들림 효과가 끝나면 돌아올 "원래" 로컬 위치(Awake에서 실제 값으로 채움)

    // 데미지로 잔상 바가 "천천히 따라 내려가는 중"인 동안에는 true — 이 동안엔 Update()가
    // 잔상 바를 건드리지 않는다(트윈 진행을 방해하지 않기 위해).
    bool trailCatchingUp;

    // 피격 번쩍임이 재생 중인 동안에는 true — 이 동안엔 체력 낮음 색 갱신이 fillImage.color를
    // 건드리지 않는다(번쩍임 트윈이랑 서로 색을 덮어써서 깜빡이는 걸 막기 위해).
    bool flashingHit;

    void Awake()
    {
        // 인스펙터에서 따로 연결 안 해놔도 같은 오브젝트의 Slider를 자동으로 사용 —
        // 씬/프리팹 저장 과정에서 필드 연결이 날아가는 경우가 있어서, 컴포넌트만 붙어있으면
        // 항상 동작하도록 코드에서 직접 보정한다.
        if (hpSlider == null)
            hpSlider = GetComponent<Slider>();

        if (trailFillImage != null)
            trailFillImage.color = trailColor;

        if (hpSlider != null && hpSlider.fillRect != null)
            fillImage = hpSlider.fillRect.GetComponent<Image>();

        if (hpSlider != null)
            basePosition = hpSlider.transform.localPosition;
    }

    // 전투 UI는 인벤토리/NPC 대화 중에 CombatUIVisibility가 SetActive(false)로 껐다가 다시 켜는
    // 경우가 흔해서(OnDestroy 없이 OnDisable/OnEnable만 반복), Player 모드 구독도 OnDisable에서
    // 정리한 만큼 OnEnable에서 다시 걸어줘야 한다. 다만 최초 1회는 CharacterManager.Instance가
    // 아직 Awake 전이라 null일 수 있어서(다른 오브젝트들의 초기화 순서 보장이 안 됨) Start()에서
    // 처리하고, started 플래그로 그 이후의 재활성화만 OnEnable이 담당하게 나눴다.
    bool started;

    void OnEnable()
    {
        if (targetMode == HpUITargetMode.Boss)
        {
            targetStats = null;
            InvokeRepeating(nameof(TryFindBoss), 0f, findRetryInterval);
        }
        else if (targetMode == HpUITargetMode.Player && started && CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterChanged += SetPlayer;
            SetPlayer(CharacterManager.Instance.CurrentCharacter);
        }
    }

    void Start()
    {
        started = true;
        if (targetMode == HpUITargetMode.Player)
        {
            CharacterManager.Instance.OnCharacterChanged += SetPlayer;
            SetPlayer(CharacterManager.Instance.CurrentCharacter);
        }
        // Boss 모드는 OnEnable에서 시작한 InvokeRepeating(TryFindBoss)이 알아서 추적을 시작한다.
    }

    void Update()
    {
        // 회복(체력 증가) 중이거나 평소에는, 잔상 바가 항상 현재 화면에 보이는 체력 바 값을
        // 실시간으로 따라가게 해서 초록 바보다 앞서 나가는 일이 없게 한다.
        // (데미지로 "천천히 따라 내려가는" 애니메이션이 재생 중일 때는 건드리지 않는다.)
        if (trailFillImage != null && hpSlider != null && !trailCatchingUp)
            SetTrailRatio(hpSlider.value);
    }

    void SetPlayer(GameObject player)
    {
        if (targetStats != null)
            targetStats.OnHpChanged -= UpdateHp;
        // [2026-10-10 추가] 사망으로 파괴된 캐릭터가 넘어와도 예외 없이 무시 (전투 UI가 꺼졌다 켜질 때 등)
        if (player == null) { targetStats = null; return; }

        targetStats = player.GetComponent<Stats>();
        targetStats.OnHpChanged += UpdateHp;

        // 캐릭터 교체 시엔 트윈 없이 바로 반영
        ApplyCurrentHpImmediate();
    }

    void TryFindBoss()
    {
        if (targetStats != null)
        {
            CancelInvoke(nameof(TryFindBoss));
            return;
        }

        BossController boss = FindObjectOfType<BossController>();
        if (boss == null) return;

        Stats s = boss.GetComponent<Stats>();
        if (s == null) return;

        targetStats = s;
        targetStats.OnHpChanged += UpdateHp;

        // 처음 찾았을 때는 트윈 없이 바로 반영
        ApplyCurrentHpImmediate();

        bossRef = boss;
        bossRef.OnDied += HandleBossDied;

        CancelInvoke(nameof(TryFindBoss));
    }

    // 새 추적 대상이 막 잡혔을 때(캐릭터 교체 / 보스 발견) 트윈 없이 현재 체력을 즉시 반영
    void ApplyCurrentHpImmediate()
    {
        if (hpSlider == null || targetStats == null) return;

        hpSlider.DOKill();
        float ratio = targetStats.maxHp > 0 ? (float)targetStats.currentHp / targetStats.maxHp : 0f;
        hpSlider.value = ratio;
        if (enableLowHpColor) UpdateFillColor(ratio);   // [2026-10-11] 색 고정일 때는 건드리지 않음

        if (trailFillImage != null)
        {
            trailFillImage.DOKill();
            trailCatchingUp = false;
            SetTrailRatio(ratio);
        }
    }

    // 보스가 진짜로 사망하면(부활 전 임시 사망 말고) HP 캔버스 자체를 화면에서 치운다 (Boss 모드 전용)
    void HandleBossDied()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
            parentCanvas.gameObject.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    void UpdateHp(int current, int max)
    {
        if (hpSlider == null) return;

        float target = max > 0 ? (float)current / max : 0f;

        // 지금 화면에 보이는 값(이전 트윈이 아직 진행 중이었다면 그 중간값)을 기준으로 판단
        float beforeRatio = hpSlider.value;
        bool isDamage = target < beforeRatio;

        hpSlider.DOKill();
        float duration = isDamage ? damageDuration : healDuration;
        Ease ease = isDamage ? damageEase : healEase;
        hpSlider.DOValue(target, duration).SetEase(ease)
            .OnUpdate(() =>
            {
                if (enableLowHpColor && !flashingHit)
                    UpdateFillColor(hpSlider.value);
            });

        if (isDamage && enableHitReaction)
            PlayHitReaction();

        if (trailFillImage != null)
        {
            trailFillImage.DOKill();

            if (isDamage)
            {
                // 데미지: 잔상 바를 지금 값에 잠깐 고정해뒀다가, 살짝 대기 후 "일정한 속도"로
                // 천천히 새 값까지 따라 내려간다(속도 고정이라 많이 맞을수록 더 오래 걸림).
                trailCatchingUp = true;
                SetTrailRatio(beforeRatio);
                float amount = beforeRatio - target;
                float drainDuration = trailDrainSpeed > 0f ? amount / trailDrainSpeed : 0f;
                DOTween.To(GetTrailRatio, SetTrailRatio, target, drainDuration)
                    .SetDelay(trailDelay)
                    .SetEase(Ease.Linear)
                    .SetTarget(trailFillImage)
                    .OnComplete(() => trailCatchingUp = false);
            }
            else
            {
                // 회복(또는 변화 없음): 잔상 없이 Update()가 매 프레임 초록 바를 그대로 따라가게 둔다.
                trailCatchingUp = false;
            }
        }
    }

    // 맞는 순간 체력바가 살짝 하얗게 번쩍이고, 아주 미세하게 흔들리는 연출.
    void PlayHitReaction()
    {
        if (hpSlider == null) return;

        if (enableShake)
        {
            hpSlider.transform.DOKill();
            hpSlider.transform.localPosition = basePosition;
            hpSlider.transform.DOShakePosition(shakeDuration, Vector3.one * shakeStrength, shakeVibrato, 90f, false, true)
                .SetUpdate(true);
        }

        if (fillImage == null) return;

        fillImage.DOKill();
        flashingHit = true;
        fillImage.DOColor(hitFlashColor, hitFlashDuration)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                flashingHit = false;
                if (enableLowHpColor) UpdateFillColor(hpSlider.value);
            });
    }

    // 체력 비율에 맞춰 채워진 바 색을 초록(가득) -> 노랑(중간) -> 빨강(위험)으로 부드럽게 보간.
    void UpdateFillColor(float ratio)
    {
        if (fillImage == null) return;

        Color c;
        if (ratio >= midHpThreshold)
        {
            float t = midHpThreshold >= 1f ? 1f : Mathf.InverseLerp(midHpThreshold, 1f, ratio);
            c = Color.Lerp(midHpColor, fullHpColor, t);
        }
        else if (ratio >= lowHpThreshold)
        {
            float t = Mathf.InverseLerp(lowHpThreshold, midHpThreshold, ratio);
            c = Color.Lerp(lowHpColor, midHpColor, t);
        }
        else
        {
            c = lowHpColor;
        }
        fillImage.color = c;
    }

    float GetTrailRatio()
    {
        if (trailFillImage == null) return 0f;
        return trailFillImage.rectTransform.anchorMax.x;
    }

    void SetTrailRatio(float ratio)
    {
        if (trailFillImage == null) return;
        RectTransform rt = trailFillImage.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
    }

    // [2026-09-29 변경] 기존엔 HpUI는 OnDestroy에서만, BossHpUI는 OnDisable에서만 정리했다.
    // OnDisable은 "비활성화될 때"와 "파괴되기 직전" 양쪽 모두에 항상 호출되므로(Unity 보장),
    // OnDestroy 하나만 있을 때보다 더 안전하다 — 오브젝트가 파괴되지 않고 그냥 꺼지기만 해도
    // 트윈/구독이 깔끔하게 정리된다. 두 스크립트의 정리 로직을 여기로 합쳤다.
    void OnDisable()
    {
        if (targetMode == HpUITargetMode.Boss)
            CancelInvoke(nameof(TryFindBoss));

        if (hpSlider != null)
        {
            hpSlider.DOKill();
            hpSlider.transform.DOKill();
        }
        if (trailFillImage != null)
            trailFillImage.DOKill();
        if (fillImage != null)
            fillImage.DOKill();

        if (targetStats != null)
            targetStats.OnHpChanged -= UpdateHp;
        targetStats = null;

        if (targetMode == HpUITargetMode.Player && CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= SetPlayer;

        if (bossRef != null)
            bossRef.OnDied -= HandleBossDied;
        bossRef = null;
    }
}
