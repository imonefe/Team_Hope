using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;   // [2026-11-20] 타임라인 재생 속도 (PlayableExtensions.SetSpeed)

/// <summary>
/// [2026-11-19 신규] 관통(궁수) 중간보스.
///
/// 대기: 횡공격 대기 스파인(archer_idle). 근처에 플레이어가 붙으면 활대로 밀쳐내기(팅겨내기 그림).
/// 패턴 1 — 스코프 사격: 위로 화살을 쏘면(2s_archer02) 화살이 흩어져 포탈로 들어가고,
///          포탈에서 나온 스코프 3개가 플레이어를 쫓는다. 플레이어에 겹치면 잠깐 멈췄다가 그 자리를 화살이 내리꽂는다.
///          [2026-11-19 변경] 스코프는 조준선 이펙트(FX_BossSnipeMark)로, 플레이어 주변 고리 모양 범위(키 2~3배 거리) 안 랜덤 위치에 scopeStagger 간격으로 하나씩 나타나
///          scopeMaxChase 초 동안 플레이어를 쫓은 뒤 그 자리에 고정 → scopeLockDelay 뒤 사라지며 포탈의 화살이 꽂혀 범위 피해.
/// 패턴 2 — 횡·종 일격: 횡이면 플레이어 높이의 맵 오른쪽 끝, 종이면 플레이어 아래 바닥으로 순간이동해
///          경고 박스(WarningZone)를 띄운 뒤 그 줄을 화살 빛줄기가 지나간다. (횡: 2s_archer / 종: 2s_archer02)
/// 패턴 3 — 도탄 화살 [2026-11-19 변경: 포탈 릴레이]: 양쪽 벽 중 랜덤한 쪽 끝으로 순간이동해 플레이어 높이를 따라가며 붉은 광선으로 겨눔 →
///          높이 고정 → 반대쪽 벽 앞 입구 포탈 + [2026-11-20 변경] 좌우 가장자리에 포탈 ricoPairCount 쌍(쌍마다 기울어진 직선 하나)을 차례로 설치 →
///          화살이 지나갈 직선을 WARNING 글자 띠로 순서대로 표시 → 횡 사격, 화살이 입구로 들어가 쌍마다 시작→끝 포탈을 지나고(쌍 사이는 순간이동) 입구에서 나와 보스에게 돌아옴 →
///          궁수가 활대로 쳐내(팅겨내기) 플레이어 쪽으로 날림. 모든 구간에서 닿으면 피해.
/// 대시는 소스가 오면 추가한다.
///
/// 체력·피격·사망·재화 드랍·퍼즈 정지는 부모(MidBossController → BossController)가 처리한다.
/// 피해 계산은 Hitbox.DealDirectHit (근접 판정과 같은 규칙: 방어·치명타·속성 배율).
/// </summary>
public class ArcherMidBoss : MidBossController
{
    [Header("[ 궁수 — 그림 (하나만 켜짐) ]")]
    public GameObject idleVisual;          // 횡공격 대기 스파인
    public GameObject skyIdleVisual;       // 종공격 대기 스파인
    public SpriteFrameSequence shotH;      // 가로 사격 (2s_archer)
    public SpriteFrameSequence shotV;      // 위로 사격 (2s_archer02)
    public GameObject parryVisual;         // 팅겨내기
    [Tooltip("[2026-11-19] 팅겨내기 그림 좌우 반전. 켜면 원본 그림(활대가 등 뒤)을 뒤집어 활대가 바라보는 쪽 앞으로 오게 함")]
    public bool parryFlipX = true;
    [Tooltip("가로 사격에서 활시위를 놓는 그림 번호 (0부터)")]
    public int shotHReleaseFrame = 10;
    [Tooltip("위로 사격에서 활시위를 놓는 그림 번호 (0부터)")]
    public int shotVReleaseFrame = 6;
    [Tooltip("가로 사격 화살이 나오는 위치 (발 기준, 오른쪽을 볼 때)")]
    public Vector2 bowOffsetH = new Vector2(1.6f, 2.4f);
    [Tooltip("위로 사격 화살이 나오는 위치 (발 기준, 오른쪽을 볼 때)")]
    public Vector2 bowOffsetV = new Vector2(0.6f, 3.6f);

    [Header("[ 궁수 — 공통 ]")]
    [Tooltip("벽·바닥 판정 (맵 끝을 찾을 때)")]
    public LayerMask wallMask;
    public GameObject teleportFx;          // 순간이동할 때 양쪽에 (선택)
    public float teleportHide = 0.22f;
    public GameObject impactFx;            // 화살이 꽂히는 곳 (Boss2_ArrowImpactFX 등)
    public GameObject sparkFx;             // 도탄 불꽃

    [Header("[ 패턴 1 — 스코프 사격 ]")]
    public GameObject scopePrefab;
    public GameObject portalPrefab;
    public GameObject arrowPrefab;         // 날아가는 화살 (오른쪽을 향한 그림)
    [Tooltip("[2026-11-20 추가] 화살비 패턴에서만 쓰는 화살 (하늘로 쏘는 화살·떨어지는 화살). 비우면 arrowPrefab")]
    public GameObject rainArrowPrefab;
    public int scopeCount = 3;
    [Tooltip("스코프가 쫓는 속도 (플레이어 이동보다 빠르게)")]
    public float scopeSpeed = 11f;
    [Tooltip("스코프가 하나씩 출발하는 간격")]
    public float scopeStagger = 0.35f;
    [Tooltip("[2026-11-19] 이제 안 씀 (겹쳐도 멈추지 않고 scopeMaxChase 동안 계속 쫓음)")]
    public float scopeLockDistance = 0.35f;
    [Tooltip("[2026-11-19 변경] 스코프가 플레이어를 쫓는 시간(초). 지나면 그 자리에 고정")]
    public float scopeMaxChase = 3.5f;
    [Tooltip("[2026-11-19 추가] 스코프가 나타나는 최소 거리 = 플레이어 키 × 이 값")]
    public float scopeSpawnMinHeights = 2f;
    [Tooltip("[2026-11-19 추가] 스코프가 나타나는 최대 거리 = 플레이어 키 × 이 값")]
    public float scopeSpawnMaxHeights = 3f;
    [Tooltip("[2026-11-19 추가] 나타나는 방향 각도 범위 (도, 0 = 오른쪽, 90 = 위). 바닥 밑에 생기지 않게 기본은 위쪽 반원 + 양옆 약간 아래")]
    public Vector2 scopeSpawnAngleRange = new Vector2(-20f, 200f);
    [Tooltip("멈춘 뒤 화살이 꽂히기까지")]
    public float scopeLockDelay = 0.45f;
    public float scopeStrikeRadius = 1.3f;
    public float scopeArrowSpeed = 40f;
    public float scopeDamage = 1.0f;
    public Color scopeColor = new Color(0.4f, 0.9f, 1f, 1f);
    public Color scopeLockColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("[2026-11-19 추가] 스코프 대신 쓸 조준선 이펙트 (FX_BossSnipeMark). 비우면 예전 scopePrefab 그림을 씀")]
    public GameObject scopeMarkPrefab;
    [Tooltip("[2026-11-19 추가] 조준선이 나타나는 데 걸리는 시간(초). 그 뒤로는 멈출 때까지 그 모습을 유지")]
    public float scopeMarkAppearTime = 0.4f;
    [Tooltip("[2026-11-19 추가] 조준선 재생을 어디까지 진행한 뒤 유지할지 (0~1, 이펙트 디졸브 시작 0.75 보다 작게). 멈추면 나머지를 scopeLockDelay 동안 재생해 사라짐")]
    [Range(0f, 1f)] public float scopeMarkHoldRatio = 0.7f;
    [Tooltip("[2026-11-19 추가] 조준선 프리팹 크기 그대로일 때 바깥 고리 반지름. 고리가 피해 범위(scopeStrikeRadius)와 같아지도록 크기를 맞추는 데 씀")]
    public float scopeMarkRingRadius = 0.765f;
    [Tooltip("[2026-11-19 추가] 조준선 파티클의 원래 재생 길이(초)")]
    public float scopeMarkBaseLength = 0.35f;
    [Tooltip("[2026-11-20 추가] 조준선 회전 속도(도/초). 시작 각도는 랜덤, 방향(시계/반시계)도 매번 랜덤. 0 이면 회전 안 함")]
    public float scopeMarkSpinSpeed = 90f;

    [Header("[ 패턴 2 — 횡·종 일격 ]")]
    public GameObject warningPrefab;       // FX_WarningZone
    public GameObject streakPrefab;        // 빛줄기 화살 (오른쪽이 머리)
    [Tooltip("[2026-11-19] 공격 준비: 이 시간 동안 플레이어 좌표를 따라가며 겨눔 (횡=높이, 종=가로 위치). 경고 박스도 같이 따라감")]
    public float lineTrackTime = 2.0f;
    [Tooltip("겨누는 위치가 따라가는 속도 (초당 칸)")]
    public float lineTrackSpeed = 9f;
    [Tooltip("겨눔을 멈춘 뒤 쏘기까지 (피할 틈)")]
    public float lineLockTime = 0.3f;
    public float lineThickness = 2.2f;
    public float streakSpeed = 60f;
    public float lineDamage = 1.3f;
    [Tooltip("횡 공격 때 오른쪽 벽에서 떨어지는 거리")]
    public float edgeMargin = 1.4f;
    [Tooltip("[2026-11-19] 종 공격 때 바닥 아래로 내려가는 깊이 (월드 단위). 이만큼 타일맵 밑에 숨어서 위로 쏨")]
    public float underDepth = 1.6f;
    [Tooltip("[2026-11-19] 바닥 밑에 있을 때 타일 뒤로 숨길지 (끄면 타일보다 위에 그려져 땅속 모습이 보임)")]
    public bool hideUnderTiles = false;
    [Tooltip("숨길 때 쓰는 그리는 순서 — 바닥 타일(1004)보다 작아야 타일 뒤로 숨음")]
    public int underSortingOrder = 1000;

    [Header("[ 패턴 2-종 — 화살비 (2026-11-19) ]")]
    [Tooltip("맵 가운데로 순간이동해 하늘로 쏘면 화살비가 떨어진다. 이 폭만큼만 안전 구간")]
    public float rainSafeWidth = 3.5f;
    [Tooltip("하늘로 쏜 뒤 화살비가 떨어지기까지 (경고 박스가 떠 있는 시간)")]
    public float rainWarnTime = 1.4f;
    [Tooltip("화살비가 쏟아지는 시간")]
    public float rainDuration = 0.8f;
    public int rainArrowCount = 26;
    public float rainArrowSpeed = 32f;
    public float rainDamage = 1.3f;
    [Tooltip("순간이동할 높이 — 바닥에서 천장까지 중 몇 % 지점 (0.35 = 아래쪽 1/3 남짓)")]
    [Range(0f, 1f)] public float rainHoverRatio = 0.35f;
    [Tooltip("[2026-11-19] 체력이 이 비율 아래로 처음 내려가면 다음 공격은 무조건 화살비 (한 번)")]
    [Range(0f, 1f)] public float rainForceHpRatio = 0.5f;

    [Header("[ 패턴 3 — 도탄 화살 ]")]
    [Tooltip("[2026-11-19] 이제 안 씀 (포탈 릴레이 방식으로 바뀜 — ricoRelaySpeed 사용)")]
    public float ricochetSpeed = 17f;
    [Tooltip("[2026-11-19] 이제 안 씀")]
    public float returnSpeed = 19f;
    public float batSpeed = 22f;
    [Tooltip("[2026-11-19] 이제 안 씀")]
    public float returnAngle = 35f;
    [Tooltip("[2026-11-19 추가] 벽 끝에서 플레이어 높이를 따라가는 시간(초)")]
    public float ricoTrackTime = 1.5f;
    [Tooltip("[2026-11-19 추가] 높이 고정 후 포탈 설치까지 잠깐 멈추는 시간(초)")]
    public float ricoLockTime = 0.2f;
    [Tooltip("[2026-11-20] 이제 안 씀 (포탈 쌍 방식 — ricoPairCount)")]
    public int ricoPortalCount = 7;
    [Tooltip("[2026-11-20 추가] 좌우 가장자리에 짝지어 놓는 포탈 쌍 개수 (쌍마다 직선 하나, 쌍 사이는 순간이동)")]
    public int ricoPairCount = 4;
    [Tooltip("[2026-11-20] 이제 안 씀 (ricoPairRise 로 대체)")]
    [Range(0f, 1f)] public float ricoPairTilt = 0.8f;
    [Tooltip("[2026-11-20 추가] 한 쌍의 두 포탈 높이 차이 범위 (월드, 최소~최대 랜덤). 직선의 가운데는 쌍마다 맡은 높이 띠 안에 둠 — 크게 하면 쌍끼리 끝부분이 교차할 수 있음")]
    public Vector2 ricoPairRise = new Vector2(4f, 6f);
    [Tooltip("[2026-11-19 추가] 포탈이 하나씩 생기는 간격(초)")]
    public float ricoPortalInterval = 0.12f;
    [Tooltip("[2026-11-20] 이제 안 씀 (높이 칸 나누기로 대체 — ricoMinHeightStep)")]
    public float ricoPortalMinGap = 3f;
    [Tooltip("[2026-11-20 변경] 포탈 높이 범위 — 화면(보스방은 방 전체를 비춤) 위아래 가장자리에서 이 비율만큼 안쪽. 아래는 바닥+1 보다 낮아지지 않음")]
    [Range(0f, 0.45f)] public float ricoScreenMargin = 0.1f;
    [Tooltip("[2026-11-20] 켜면 도탄 패턴마다 포탈 높이 범위를 콘솔에 출력 (확인용)")] public bool ricoDebugLog = true;
    [Tooltip("[2026-11-20] 이제 안 씀 (포탈 쌍 방식은 쌍마다 높이 띠를 나눠 맡음)")]
    public float ricoMinHeightStep = 2.5f;
    [Tooltip("[2026-11-20 추가] 포탈 가로 위치 범위 = 맵 가장자리에서 안쪽으로 플레이어 가로 길이 × 이 값")]
    public float ricoEdgeWidths = 2f;
    [Tooltip("[2026-11-20 추가] 포탈이 벽에 박히지 않게 가장자리에서 띄우는 최소 거리")]
    public float ricoEdgeInset = 0.4f;
    [Tooltip("[2026-11-19 추가] 반대쪽 벽 포탈이 벽에서 떨어진 거리")]
    public float ricoWallPortalGap = 1.2f;
    [Tooltip("[2026-11-19 추가] 포탈 사이를 지나가는 화살 속도 (빠르게)")]
    public float ricoRelaySpeed = 38f;
    [Tooltip("[2026-11-19 추가] 화살 길 예고 광선 전체를 순서대로 다 그리는 데 걸리는 시간(초)")]
    public float ricoWarnDrawTime = 0.5f;
    [Tooltip("[2026-11-19 추가] 예고 광선을 다 그린 뒤 쏘기까지 기다리는 시간(초)")]
    public float ricoWarnHold = 0.2f;
    public float ricoWarnWidth = 0.08f;
    [Tooltip("[2026-11-20 추가] 도탄 릴레이 화살 판정 굵기 (전체 두께, 월드 단위). 0 이하면 예고 광선 굵기(ricoWarnWidth)와 같게. 다른 패턴의 화살 판정(ricochetHitRadius)은 그대로")]
    public float ricoArrowHitWidth = 0f;
    public Color ricoWarnColor = new Color(1f, 0.15f, 0.12f, 0.85f);
    [Tooltip("[2026-11-19 추가] 예고 광선 머티리얼 (비우면 Sprites/Default)")]
    public Material ricoWarnMaterial;
    [Tooltip("[2026-11-20 추가] 예고선을 경고 박스의 WARNING 글자 띠로 그림 (M_RicoWarnText). 비우면 예전 붉은 선(ricoWarnMaterial)")]
    public Material ricoWarnTextMaterial;
    [Tooltip("[2026-11-20 추가] 글자 띠가 흐르는 속도 (초당 텍스처 한 장 = 'WARNING' 두 번 분량)")]
    public float ricoWarnTextScroll = 1.5f;
    [Tooltip("[2026-11-20 추가] 글자 텍스처 가로/세로 비율 (T_WarningText 1024×128 = 8). 글자가 눌리거나 늘어나 보이지 않게 Tiling 계산에 씀")]
    public float ricoWarnTextAspect = 8f;
    [Tooltip("[2026-11-20 추가] 글자가 위아래로 뒤집혀 보이면 체크")]
    public bool ricoWarnTextFlipV = false;
    [Tooltip("[2026-11-20 추가] 도탄 화살 포탈 그림 (FX_PortalCrack — 균열 개방 스킬과 같은 포탈). 비우면 portalPrefab")]
    public GameObject ricoPortalPrefab;
    [Tooltip("[2026-11-20 추가] 도탄 패턴 활시위 이펙트 (midbos_archer_shot1). 시위를 당길 때 재생. 날아가는 화살은 arrowPrefab")]
    public GameObject ricoShotFxPrefab;
    [Tooltip("[2026-11-20 추가] 이펙트 안에서 화살이 나가는 순간(초) — spot·spot2 가 터지는 시각. 보스가 시위를 놓는 순간에 이 시각이 오도록 재생 속도를 맞춤")]
    public float ricoShotFxFireTime = 2.55f;
    [Tooltip("[2026-11-20 추가] 이펙트를 Scale X = +1 로 두었을 때 오른쪽으로 쏘는 그림이면 체크 (반대로 나가면 체크를 바꿀 것)")]
    public bool ricoShotFxFacesRight = true;
    [Tooltip("[2026-11-20 추가] 이펙트 위치 = 활 위치 + 이 값 (x 는 보스가 보는 방향 기준)")]
    public Vector2 ricoShotFxOffset = Vector2.zero;
    [Tooltip("[2026-11-20 추가] 시위 이펙트 전체가 끝나는 시간(초, 원래 속도 기준) — 이 뒤에 지움")]
    public float ricoShotFxLength = 6f;
    [Tooltip("[2026-11-20 추가] 이펙트 안에서 화살이 나가는 지점 오브젝트 이름 — 이 오브젝트가 활 위치에 오게 이펙트를 옮김")]
    public string ricoShotFxAnchor = "spot";
    [Tooltip("[2026-11-20 추가] 도탄 패턴에서 날아가는 화살 (FX_RicoBolt — 빛줄기 + 잔상 Trail + 불티). 비우면 arrowPrefab")]
    public GameObject ricoBoltPrefab;
    [Tooltip("[2026-11-20 추가] 도탄 화살이 보스에게 쳐내질 때·마지막에 멈출 때 터지는 이펙트 (FX_RicoImpact). 비우면 sparkFx")]
    public GameObject ricoImpactFx;
    [Tooltip("[2026-11-20 추가] 포탈 크기 배율")]
    public float ricoPortalScale = 0.4f;
    [Tooltip("[2026-11-20 추가] 포탈 애니메이션·파티클 재생 배속 (원래 1.6초에 열림 → 3배속이면 약 0.53초)")]
    public float ricoPortalAnimSpeed = 3f;
    [Tooltip("[2026-11-20 추가] 포탈 애니메이션을 이 시점(원래 속도 기준 초)에서 멈춰 열린 채로 둠. 화살이 지나가면 이어서 재생해 닫힘")]
    public float ricoPortalHoldAt = 2.4f;
    [Tooltip("[2026-11-20 추가] 포탈 애니메이션 전체 길이 (원래 속도 기준 초, AN_PortalCrack_Open = 3.5)")]
    public float ricoPortalAnimLength = 3.5f;
    [Tooltip("화살 판정 두께 (반지름). 판정은 화살 그림 길이 전체(arrowHalfLength × 2)를 따라감")]
    public float ricochetHitRadius = 0.35f;
    [Tooltip("[2026-11-19] 화살 판정 길이의 절반 — 화살 그림(FX_Archer_Arrow) 길이에 맞춤")]
    public float arrowHalfLength = 1.2f;
    public float ricochetDamage = 0.9f;
    [Tooltip("돌아온 화살을 쳐내는 거리")]
    public float batDistance = 1.6f;
    public float batPoseTime = 0.35f;

    [Header("[ 대기 중 떠다니기 (2026-11-19) ]")]
    [Tooltip("공격하지 않는 동안(대기) 대기 모션 그대로 방 안을 둥실둥실 떠다님")]
    public bool wanderWhileIdle = true;
    [Tooltip("떠다니는 속도 (초당 칸)")]
    public float wanderSpeed = 2.5f;
    [Tooltip("바닥 위로 떠다닐 높이 범위 (최소, 최대)")]
    public Vector2 wanderHeight = new Vector2(0.3f, 3.5f);
    [Tooltip("양쪽 벽에서 떨어질 거리")]
    public float wanderMargin = 2f;
    [Tooltip("다음 목적지를 고르는 간격 (초) — 도착하면 바로 새로 고름")]
    public float wanderRetarget = 2.5f;
    [Tooltip("떠다니면서 위아래로 출렁이는 폭과 빠르기")]
    public float wanderBobAmount = 0.25f;
    public float wanderBobSpeed = 0.5f;

    [Header("[ 밀쳐내기 (가까이 붙으면) ]")]
    public float pushRange = 2.6f;
    [Tooltip("[2026-11-19] 밀쳐내기(튕겨내기) 재사용 대기 시간 (초). 이 시간 안에는 가까이 붙어 있어도 일반 패턴을 씀")]
    public float pushCooldown = 6f;
    public float pushDamage = 0.6f;
    public float pushKnockX = 14f;
    public float pushKnockY = 5f;
    public Vector2 pushBox = new Vector2(3.2f, 3.5f);

    // ── 내부 ──
    GameObject[] visuals;
    SkeletonAnimation skyIdleSpine;
    readonly List<GameObject> live = new List<GameObject>();   // 패턴이 만든 것 (죽으면 정리)
    Collider2D bodyCol;
    Health playerHealth;
    float pushTimer;
    bool rainForceUsed, forceRain;
    Vector2 wanderTarget;
    float wanderTimer, wanderClock;
    bool hasWander;
    Renderer[] allRenderers;
    int[] baseOrders;
    bool isUnder;
    SpriteRenderer parrySr;

    const int P_SCOPE = 0, P_LINE = 1, P_RICOCHET = 2, P_PUSH = 3;

    protected override void Awake()
    {
        if (idleVisual != null && skeletonAnimation == null) skeletonAnimation = idleVisual.GetComponent<SkeletonAnimation>();
        animMode = AnimMode.Spine;
        useHitReaction = false;
        base.Awake();
        if (wallMask.value == 0) wallMask = LayerMask.GetMask("ground");
        visuals = new[] { idleVisual, skyIdleVisual, shotH != null ? shotH.gameObject : null, shotV != null ? shotV.gameObject : null, parryVisual };
        if (skyIdleVisual != null) skyIdleSpine = skyIdleVisual.GetComponent<SkeletonAnimation>();
        bodyCol = GetComponent<Collider2D>();
        if (parryVisual != null) parrySr = parryVisual.GetComponent<SpriteRenderer>();
        allRenderers = GetComponentsInChildren<Renderer>(true);
        baseOrders = new int[allRenderers.Length];
        for (int i = 0; i < allRenderers.Length; i++) baseOrders[i] = allRenderers[i].sortingOrder;
        ShowVisual(idleVisual);
    }

    protected override void Update()
    {
        base.Update();
        // 퍼즈 중엔 프레임 그림·종 대기 스파인도 멈춤 (가로 대기 스파인은 부모가 멈춤)
        if (pushTimer > 0f && !IsWorldPaused) pushTimer -= Time.deltaTime;   // 밀쳐내기 재사용 대기 (퍼즈 중엔 멈춤)
        float s = IsWorldPaused ? 0f : 1f;
        if (shotH != null) shotH.speed = s;
        if (shotV != null) shotV.speed = s;
        if (skyIdleSpine != null) skyIdleSpine.timeScale = s;
    }

    // 팅겨내기: 바라볼 쪽으로 돌린 뒤 그림 좌우 반전 설정을 적용
    void ShowParry(int faceDir)
    {
        if (faceDir != 0) FlipTo(faceDir);
        if (parrySr != null) parrySr.flipX = parryFlipX;
        ShowVisual(parryVisual);
    }

    // 바닥 밑에 숨을 때는 타일 뒤로, 나오면 원래 순서로
    void SetUnderground(bool under)
    {
        isUnder = under;
        if (allRenderers == null) return;
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i] != null) allRenderers[i].sortingOrder = under && hideUnderTiles ? underSortingOrder : baseOrders[i];
        if (bodyCol != null) bodyCol.isTrigger = under;   // 땅속에서 바닥 충돌에 밀려 튀지 않게
    }

    // ───────────────────────── 대기 중 떠다니기 ─────────────────────────
    // 부모(MidBossController)의 부유 처리 뒤에 속도를 덮어써서, 대기(Cooldown) 동안 방 안의 무작위 지점으로 천천히 이동
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!wanderWhileIdle || IsWorldPaused || rigid == null || !rigid.simulated) return;
        if (state != BossState.Cooldown) { hasWander = false; return; }

        wanderTimer -= Time.fixedDeltaTime;
        wanderClock += Time.fixedDeltaTime;
        Vector2 pos = rigid.position;
        if (!hasWander || wanderTimer <= 0f || (wanderTarget - pos).magnitude < 0.3f) PickWanderTarget();

        Vector2 to = wanderTarget - pos;
        float spd = Mathf.Min(wanderSpeed, to.magnitude * 2f);          // 도착 무렵 천천히
        Vector2 v = to.sqrMagnitude > 0.0001f ? to.normalized * spd : Vector2.zero;
        float w = wanderBobSpeed * Mathf.PI * 2f;
        v.y += Mathf.Cos(wanderClock * w) * wanderBobAmount * w;        // 위아래 출렁임 (사인 곡선의 속도)
        rigid.velocity = v;
    }

    void PickWanderTarget()
    {
        Vector3 from = player != null ? player.position : transform.position;
        GetRoomBounds(from, out float left, out float right, out float floor, out float ceil);
        float x0 = left + wanderMargin, x1 = right - wanderMargin;
        float x = x0 < x1 ? Random.Range(x0, x1) : (left + right) * 0.5f;
        float maxH = Mathf.Max(wanderHeight.x, Mathf.Min(wanderHeight.y, ceil - floor - 4f));   // 천장에 머리가 닿지 않게
        float y = floor + Random.Range(wanderHeight.x, maxH);
        wanderTarget = new Vector2(x, y);
        wanderTimer = wanderRetarget;
        hasWander = true;
    }

    void ShowVisual(GameObject v)
    {
        if (visuals == null) return;
        foreach (var g in visuals) if (g != null) g.SetActive(g == v);
    }

    protected override void OnCooldownStart()
    {
        base.OnCooldownStart();
        ShowVisual(idleVisual);
        SetUnderground(false);
    }

    protected override void OnTrueDeath()
    {
        foreach (var g in live) if (g != null) Destroy(g);
        live.Clear();
        if (isUnder) { transform.position += Vector3.up * underDepth; rigid.position = transform.position; }   // 땅속에서 죽으면 바닥 위로
        SetUnderground(false);
        ShowVisual(idleVisual);
        base.OnTrueDeath();
    }

    // ───────────────────────── 패턴 선택 ─────────────────────────
    protected override int GetPatternCount() => 3;

    protected override int SelectPattern()
    {
        // [2026-11-19] 체력이 절반(rainForceHpRatio) 아래로 처음 내려가면 무조건 화살비
        if (!rainForceUsed && stats != null && stats.maxHp > 0 && stats.currentHp <= stats.maxHp * rainForceHpRatio)
        {
            rainForceUsed = true;
            forceRain = true;
            return P_LINE;
        }
        // 가까이 붙으면 밀쳐내기 — 재사용 대기 시간이 지났을 때만 (직전 패턴 기록은 그대로)
        if (pushTimer <= 0f && player != null && Vector2.Distance(player.position, transform.position + Vector3.up * 1.5f) <= pushRange)
            return P_PUSH;
        return base.SelectPattern();
    }

    protected override IEnumerator ExecutePattern(int index)
    {
        if (player == null) yield break;
        state = BossState.Attacking;
        rigid.velocity = Vector2.zero;
        switch (index)
        {
            case P_SCOPE: yield return ScopePattern(); break;
            case P_LINE: yield return LinePattern(); break;
            case P_RICOCHET: yield return RicochetPattern(); break;
            default: yield return PushPattern(); break;
        }
        ShowVisual(idleVisual);
    }

    void FacePlayer()
    {
        if (player == null) return;
        FlipTo(player.position.x >= transform.position.x ? 1 : -1);
    }

    Vector3 Bow(Vector2 off) => transform.position + new Vector3(off.x * lockedDir, off.y, 0f);

    // 프레임 그림을 재생하고 활시위를 놓는 그림까지 기다린다
    IEnumerator PlayShotUntilRelease(SpriteFrameSequence seq, int releaseFrame)
    {
        if (seq == null) yield break;
        ShowVisual(seq.gameObject);
        seq.Play();
        float guard = 0f;
        while (seq.CurrentFrame < releaseFrame && !seq.Finished && guard < 5f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitSeqEnd(SpriteFrameSequence seq)
    {
        if (seq == null) yield break;
        float guard = 0f;
        while (!seq.Finished && guard < 5f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
    }

    // ───────────────────────── 패턴 1 — 스코프 사격 ─────────────────────────
    IEnumerator ScopePattern()
    {
        FacePlayer();
        yield return PlayShotUntilRelease(shotV, shotVReleaseFrame);

        // 포탈 자리: 천장 아래, 방 안에서 흩어지게
        GetRoomBounds(player.position, out float left, out float right, out float floor, out float ceil);
        int n = Mathf.Max(1, scopeCount);
        var portals = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float x = Mathf.Lerp(left + 1.5f, right - 1.5f, t) + Random.Range(-1f, 1f);
            portals[i] = new Vector3(x, ceil - Random.Range(1.2f, 2.4f), 0f);
        }

        // 화살이 흩어지며 포탈로 들어감
        var portalObjs = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            portalObjs[i] = Make(portalPrefab, portals[i], Quaternion.identity);
            StartCoroutine(FlyVisual(ArrowFx, Bow(bowOffsetV), portals[i], 30f, true));
        }
        yield return PausableWait(0.35f);
        ShowVisual(idleVisual);

        // 스코프 출발 (하나씩)
        int done = 0;
        for (int i = 0; i < n; i++)
        {
            StartCoroutine(OneScope(portals[i], () => done++));
            yield return PausableWait(scopeStagger);
        }
        float guard = 0f;
        while (done < n && guard < scopeMaxChase + scopeLockDelay + 3f)
        {
            if (!IsWorldPaused) guard += Time.deltaTime;
            yield return null;
        }
        foreach (var p in portalObjs) if (p != null) { live.Remove(p); Destroy(p, 0.2f); }
        yield return PausableWait(0.3f);
    }

    IEnumerator OneScope(Vector3 portal, System.Action onDone)
    {
        // [2026-11-19] 그림: 조준선 이펙트가 있으면 그것을, 없으면 예전 스코프 그림
        bool useMark = scopeMarkPrefab != null;
        GameObject scope = Make(useMark ? scopeMarkPrefab : scopePrefab, RandomAroundPlayer(portal), Quaternion.identity);   // [2026-11-19] 플레이어 주변 일정 거리 랜덤 위치에서 등장
        SpriteRenderer sr = (!useMark && scope != null) ? scope.GetComponentInChildren<SpriteRenderer>() : null;
        if (sr != null) sr.color = scopeColor;
        ParticleSystem[] ps = (useMark && scope != null) ? scope.GetComponentsInChildren<ParticleSystem>() : new ParticleSystem[0];
        if (useMark && scope != null && scopeMarkRingRadius > 0f)
            scope.transform.localScale *= scopeStrikeRadius / scopeMarkRingRadius;   // 고리 = 피해 범위
        // [2026-11-20] 조준선: 랜덤 시작 각도 + 랜덤 회전 방향
        float spin = 0f;
        if (useMark && scope != null)
        {
            scope.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spin = scopeMarkSpinSpeed * (Random.value < 0.5f ? -1f : 1f);
            // 빌보드(View) 정렬은 오브젝트 회전을 무시하므로, 이 복제본만 Local 정렬로 바꿔 회전이 보이게 함 (프리팹은 그대로)
            foreach (var r in scope.GetComponentsInChildren<ParticleSystemRenderer>())
                if (r.renderMode == ParticleSystemRenderMode.Billboard) r.alignment = ParticleSystemRenderSpace.Local;
        }
        float holdAt = scopeMarkBaseLength * scopeMarkHoldRatio;
        float appearSpeed = holdAt / Mathf.Max(0.05f, scopeMarkAppearTime);
        float fxTime = 0f;

        // 플레이어를 쫓음
        float t = 0f;
        while (scope != null && player != null && t < scopeMaxChase)
        {
            if (!IsWorldPaused)
            {
                Vector3 target = PlayerCenter();
                scope.transform.position = Vector3.MoveTowards(scope.transform.position, target, scopeSpeed * Time.deltaTime);
                if (!useMark) scope.transform.Rotate(0f, 0f, 90f * Time.deltaTime);
                else scope.transform.Rotate(0f, 0f, spin * Time.deltaTime);
                t += Time.deltaTime;   // [2026-11-19] 겹쳐도 멈추지 않고 정해진 시간 동안 계속 쫓음
            }
            // 조준선: holdAt 까지 재생한 뒤 그 모습으로 정지 (퍼즈 중에도 정지)
            float sp = (!IsWorldPaused && fxTime < holdAt) ? appearSpeed : 0f;
            fxTime += Time.deltaTime * sp;
            SetParticleSpeed(ps, sp);
            yield return null;
        }
        if (scope == null) { onDone(); yield break; }

        // 고정 → 깜빡임 (조준선은 남은 재생을 scopeLockDelay 동안 마저 해서 디졸브로 사라짐)
        Vector3 spot = scope.transform.position;
        float fadeSpeed = Mathf.Max(0f, scopeMarkBaseLength - fxTime) / Mathf.Max(0.05f, scopeLockDelay);
        float lt = 0f;
        while (lt < scopeLockDelay)
        {
            if (!IsWorldPaused)
            {
                lt += Time.deltaTime;
                if (useMark) scope.transform.Rotate(0f, 0f, spin * Time.deltaTime);   // 사라지는 동안에도 계속 회전
                if (sr != null) sr.color = (Mathf.FloorToInt(lt * 16f) % 2 == 0) ? scopeLockColor : scopeColor;
            }
            SetParticleSpeed(ps, IsWorldPaused ? 0f : fadeSpeed);
            yield return null;
        }
        if (useMark) { live.Remove(scope); Destroy(scope); scope = null; }

        // 포탈에서 화살이 내리꽂힘
        yield return FlyVisual(ArrowFx, portal, spot, scopeArrowSpeed, false);
        Make(impactFx, spot, Quaternion.identity, 2f);
        HitPlayerCircle(spot, scopeStrikeRadius, scopeDamage, 0, 4f, 4f);
        if (scope != null) { live.Remove(scope); Destroy(scope); }
        onDone();
    }

    // [2026-11-19 추가] 플레이어 중심에서 (키 × scopeSpawnMinHeights) ~ (키 × scopeSpawnMaxHeights) 거리의 랜덤 위치
    Vector3 RandomAroundPlayer(Vector3 fallback)
    {
        if (player == null) return fallback;
        Collider2D c = player.GetComponent<Collider2D>();
        float h = c != null ? c.bounds.size.y : 2f;
        float r = Random.Range(h * scopeSpawnMinHeights, h * scopeSpawnMaxHeights);
        float a = Random.Range(scopeSpawnAngleRange.x, scopeSpawnAngleRange.y) * Mathf.Deg2Rad;
        return PlayerCenter() + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
    }

    static void SetParticleSpeed(ParticleSystem[] list, float speed)
    {
        foreach (ParticleSystem p in list)
        {
            if (p == null) continue;
            ParticleSystem.MainModule m = p.main;
            m.simulationSpeed = speed;
        }
    }

    // ───────────────────────── 패턴 2 — 횡·종 일격 ─────────────────────────
    IEnumerator LinePattern()
    {
        bool horizontal = !forceRain && Random.value < 0.5f;
        forceRain = false;
        if (!horizontal) { yield return RainPattern(); yield break; }   // [2026-11-19] 종 공격 = 화살비
        Vector3 pp = player.position;
        GetRoomBounds(pp, out float left, out float right, out float floor, out float ceil);

        // 횡: 활 높이가 플레이어 몸 가운데에 오도록 (바닥 아래로는 내려가지 않음) / 종: 플레이어 아래 바닥
        float bandY = PlayerCenter().y;
        Vector3 dest;
        if (horizontal) dest = new Vector3(right - edgeMargin, Mathf.Max(floor + 0.05f, bandY - bowOffsetH.y), 0f);
        else dest = new Vector3(pp.x, floor - underDepth, 0f);   // [2026-11-19] 종: 타일맵 밑으로 내려가 위로 쏨
        if (!horizontal) SetUnderground(true);
        yield return TeleportTo(dest);

        if (horizontal) FlipTo(-1); else FacePlayer();
        ShowVisual(horizontal ? idleVisual : skyIdleVisual);
        if (!horizontal && skyIdleSpine != null && skyIdleSpine.AnimationState != null)
            skyIdleSpine.AnimationState.SetAnimation(0, "idle", true);

        // 경고 박스
        Vector3 center; Vector2 size;
        if (horizontal) { center = new Vector3((left + right) * 0.5f, bandY, 0f); size = new Vector2(right - left, lineThickness); }
        else { center = new Vector3(transform.position.x, (floor + ceil) * 0.5f, 0f); size = new Vector2(lineThickness, ceil - floor); }
        WarningZone wz = null;
        if (warningPrefab != null)
        {
            wz = WarningZone.Spawn(warningPrefab, center, size, lineTrackTime + lineLockTime);
            if (wz != null) live.Add(wz.gameObject);
        }

        // [2026-11-19] 공격 준비 — lineTrackTime 동안 플레이어 좌표를 쫓으며 겨눔 (횡: 높이 / 종: 가로 위치)
        float tt = 0f;
        while (tt < lineTrackTime && player != null)
        {
            if (!IsWorldPaused)
            {
                tt += Time.deltaTime;
                Vector3 p = transform.position;
                if (horizontal)
                {
                    float wantY = Mathf.Clamp(PlayerCenter().y, floor + lineThickness * 0.5f, ceil - lineThickness * 0.5f);
                    center.y = Mathf.MoveTowards(center.y, wantY, lineTrackSpeed * Time.deltaTime);
                    p.y = Mathf.Max(floor + 0.05f, center.y - bowOffsetH.y);
                }
                else
                {
                    float wantX = Mathf.Clamp(player.position.x, left + lineThickness * 0.5f, right - lineThickness * 0.5f);
                    center.x = Mathf.MoveTowards(center.x, wantX, lineTrackSpeed * Time.deltaTime);
                    p.x = center.x;
                }
                rigid.position = p; transform.position = p;
                if (wz != null) wz.transform.position = center;
            }
            yield return null;
        }
        yield return PausableWait(lineLockTime);   // 겨눔 고정 → 피할 틈

        // 사격
        var seq = horizontal ? shotH : shotV;
        yield return PlayShotUntilRelease(seq, horizontal ? shotHReleaseFrame : shotVReleaseFrame);
        // 횡: 활에서 왼쪽 벽까지 / 종: 바닥에서 천장까지
        Vector3 from, to;
        if (horizontal) { from = new Vector3(Bow(bowOffsetH).x, center.y, 0f); to = new Vector3(left, center.y, 0f); }
        else { from = new Vector3(center.x, transform.position.y + bowOffsetV.y * 0.6f, 0f); to = new Vector3(center.x, ceil, 0f); }   // 땅속 활에서 시작해 바닥을 뚫고 올라감
        yield return LineStreak(from, to, center, size);
        yield return WaitSeqEnd(seq);
        if (!horizontal)
        {
            // 바닥 위로 올라와 다음 행동
            yield return TeleportTo(new Vector3(transform.position.x, floor + 0.05f, 0f));
            SetUnderground(false);
        }
    }

    // ───────────────────────── 패턴 2-종 — 화살비 ─────────────────────────
    // 맵 가운데로 순간이동 → 하늘로 사격 → 안전 구간 하나만 남기고 경고 → 화살비 (안전 구간 밖이면 피해)
    IEnumerator RainPattern()
    {
        GetRoomBounds(player.position, out float left, out float right, out float floor, out float ceil);
        Vector3 dest = new Vector3((left + right) * 0.5f, floor + (ceil - floor) * rainHoverRatio, 0f);
        yield return TeleportTo(dest);
        FacePlayer();

        // 안전 구간 (벽에서 조금 떨어진 무작위 위치)
        float half = rainSafeWidth * 0.5f;
        float sx = Random.Range(left + half + 0.5f, right - half - 0.5f);
        float safeL = sx - half, safeR = sx + half;

        // 하늘로 사격
        yield return PlayShotUntilRelease(shotV, shotVReleaseFrame);
        for (int i = 0; i < 5; i++)
        {
            Vector3 from = Bow(bowOffsetV);
            Vector3 to = new Vector3(from.x + Random.Range(-2.5f, 2.5f), ceil + 3f, 0f);
            StartCoroutine(FlyVisual(RainArrowPrefab, from, to, 40f, false));
        }

        // 위험 구간 경고 (안전 구간 양옆)
        float h = ceil - floor, cy = (floor + ceil) * 0.5f;
        Vector3 cL = new Vector3((left + safeL) * 0.5f, cy, 0f), cR = new Vector3((safeR + right) * 0.5f, cy, 0f);
        Vector2 sL = new Vector2(safeL - left, h), sR = new Vector2(right - safeR, h);
        if (warningPrefab != null)
        {
            if (sL.x > 0.2f) { var a = WarningZone.Spawn(warningPrefab, cL, sL, rainWarnTime + rainDuration); if (a != null) live.Add(a.gameObject); }
            if (sR.x > 0.2f) { var b = WarningZone.Spawn(warningPrefab, cR, sR, rainWarnTime + rainDuration); if (b != null) live.Add(b.gameObject); }
        }
        yield return WaitSeqEnd(shotV);
        ShowVisual(idleVisual);
        yield return PausableWait(Mathf.Max(0f, rainWarnTime - 0.3f));

        // 화살비 — 위험 구간 안 무작위 x 에서 천장 위부터 바닥까지 (그림), 판정은 위험 구간 전체
        int n = Mathf.Max(1, rainArrowCount);
        float lenL = Mathf.Max(0f, safeL - left), lenR = Mathf.Max(0f, right - safeR);
        for (int i = 0; i < n; i++)
        {
            float r = Random.Range(0f, lenL + lenR);
            float x = r < lenL ? left + r : safeR + (r - lenL);
            StartCoroutine(RainArrow(x, ceil + 1.5f, floor, Random.Range(0f, rainDuration)));
        }
        bool hit = false; float t = 0f;
        while (t < rainDuration + 0.2f)
        {
            if (!IsWorldPaused)
            {
                t += Time.deltaTime;
                if (!hit && t > 0.12f)
                    hit = (sL.x > 0.2f && HitPlayerBox(cL, sL, rainDamage, 0, 4f, 4f)) || (sR.x > 0.2f && HitPlayerBox(cR, sR, rainDamage, 0, 4f, 4f));
            }
            yield return null;
        }
        yield return PausableWait(0.3f);
    }

    // [2026-11-20] 화살 그림 고르기 — 칸이 비었거나 연결이 끊긴(Missing) 경우 다음 화살로 대신해 화살이 안 보이는 일이 없게
    GameObject ArrowFx => arrowPrefab != null ? arrowPrefab : (rainArrowPrefab != null ? rainArrowPrefab : ricoBoltPrefab);
    GameObject RainArrowPrefab => rainArrowPrefab != null ? rainArrowPrefab : ArrowFx;

    IEnumerator RainArrow(float x, float top, float floor, float delay)
    {
        yield return PausableWait(delay);
        yield return FlyVisual(RainArrowPrefab, new Vector3(x, top, 0f), new Vector3(x, floor, 0f), rainArrowSpeed, false);
        if (Random.value < 0.35f) Make(impactFx, new Vector3(x, floor, 0f), Quaternion.identity, 2f);
    }

    // 빛줄기가 줄을 따라 지나가며, 머리가 플레이어를 지나칠 때 그 줄 안에 있으면 피해
    IEnumerator LineStreak(Vector3 from, Vector3 to, Vector3 bandCenter, Vector2 bandSize)
    {
        Vector3 dir = (to - from).normalized;
        float len = Vector3.Distance(from, to);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject streak = Make(streakPrefab, from, Quaternion.Euler(0, 0, ang));
        bool hit = false;
        float d = 0f;
        while (d < len)
        {
            if (!IsWorldPaused)
            {
                d = Mathf.Min(len, d + streakSpeed * Time.deltaTime);
                Vector3 head = from + dir * d;
                if (streak != null) streak.transform.position = head;
                if (!hit && player != null)
                {
                    // 머리가 지나간 구간 = 시작점 ~ 머리
                    Vector3 mid = (from + head) * 0.5f;
                    Vector2 sweep = Mathf.Abs(dir.x) > 0.5f ? new Vector2(d, bandSize.y) : new Vector2(bandSize.x, d);
                    hit = HitPlayerBox(mid, sweep, lineDamage, dir.x < -0.5f ? -1 : 1, 6f, 3f);
                }
            }
            yield return null;
        }
        if (streak != null) { live.Remove(streak); Destroy(streak, 0.08f); }
        Make(impactFx, to, Quaternion.identity, 2f);
    }

    // ───────────────────────── 패턴 3 — 도탄 화살 ─────────────────────────
    // [2026-11-19 변경] 포탈 릴레이 도탄 화살
    IEnumerator RicochetPattern()
    {
        GetRoomBounds(player.position, out float left, out float right, out float floor, out float ceil);

        // ① 양쪽 벽 중 랜덤한 쪽 끝으로 순간이동, 반대쪽을 바라봄
        int side = Random.value < 0.5f ? 1 : -1;   // 1 = 오른쪽 벽
        float bandY = PlayerCenter().y;
        float bx = side > 0 ? right - edgeMargin : left + edgeMargin;
        yield return TeleportTo(new Vector3(bx, Mathf.Max(floor + 0.05f, bandY - bowOffsetH.y), 0f));
        FlipTo(-side);
        ShowVisual(idleVisual);
        float farX = side > 0 ? left + ricoWallPortalGap : right - ricoWallPortalGap;   // 반대쪽 벽 포탈 x

        // ② 플레이어 높이를 따라가며 붉은 광선으로 겨눔
        LineRenderer aim = MakeWarnLine();
        float tt = 0f;
        while (tt < ricoTrackTime && player != null)
        {
            if (!IsWorldPaused)
            {
                tt += Time.deltaTime;
                float wantY = Mathf.Clamp(PlayerCenter().y, floor + 0.6f, ceil - 0.6f);
                bandY = Mathf.MoveTowards(bandY, wantY, lineTrackSpeed * Time.deltaTime);
                Vector3 p = transform.position;
                p.y = Mathf.Max(floor + 0.05f, bandY - bowOffsetH.y);
                rigid.position = p; transform.position = p;
            }
            SetWarnLine(aim, Bow(bowOffsetH), new Vector3(farX, Bow(bowOffsetH).y, 0f), 1f);
            yield return null;
        }
        yield return PausableWait(ricoLockTime);   // 높이 고정

        // ③ [2026-11-20 변경: 포탈 쌍] 반대쪽 벽 입구 포탈 E → 좌우 가장자리에 포탈 한 쌍씩 ricoPairCount 쌍 (쌍마다 기울어진 직선 하나)
        //    화살: 보스 → E 로 들어감 → 1쌍 시작 포탈에서 나와 끝 포탈로 → (순간이동) 2쌍 시작 → … → 마지막 쌍 끝 → (순간이동) E 에서 나와 보스에게 돌아옴
        //    쌍과 쌍 사이는 순간이동이라 선이 없음 → 경로가 겹치지 않음. 쌍마다 높이 띠를 하나씩 맡아 서로 교차하지 않게 함
        Vector3 bow = Bow(bowOffsetH);
        Vector3 entry = new Vector3(farX, bow.y, 0f);
        GetRelayHeightRange(floor, out float hy0, out float hy1);
        int pairN = Mathf.Max(1, ricoPairCount);
        var starts = new List<Vector3>(); var ends = new List<Vector3>();
        int[] band = new int[pairN];
        for (int i = 0; i < pairN; i++) band[i] = i;
        for (int i = pairN - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (band[i], band[j]) = (band[j], band[i]); }
        float bh = (hy1 - hy0) / pairN;
        for (int i = 0; i < pairN; i++)
        {
            // [2026-11-20 수정] 기울기 강화: 직선 가운데는 맡은 띠 중앙, 두 끝 높이 차이는 ricoPairRise 범위 랜덤 (방 높이 안으로 밀어 넣음)
            float mid = hy0 + bh * (band[i] + 0.5f);
            float rise = Mathf.Min(Random.Range(ricoPairRise.x, Mathf.Max(ricoPairRise.x, ricoPairRise.y)), hy1 - hy0);
            float yLow = mid - rise * 0.5f, yHigh = mid + rise * 0.5f;
            if (yLow < hy0) { yHigh += hy0 - yLow; yLow = hy0; }
            if (yHigh > hy1) { yLow -= yHigh - hy1; yHigh = hy1; }
            bool upFirst = Random.value < 0.5f;
            int startEdge = (i % 2 == 0) ? -side : side;   // 첫 쌍은 입구(반대쪽 벽) 쪽에서 시작, 이후 번갈아
            starts.Add(RelayPoint(startEdge, upFirst ? yHigh : yLow, left, right));
            ends.Add(RelayPoint(-startEdge, upFirst ? yLow : yHigh, left, right));
        }

        // 포탈 설치 (차례로): E(보스를 바라봄) → 1쌍 시작·끝 → 2쌍 … (쌍의 두 포탈은 서로를 바라봄)
        bool crackPortal = ricoPortalPrefab != null;
        GameObject entryPortal = SpawnRicoPortal(entry, bow, crackPortal);
        yield return PausableWait(ricoPortalInterval);
        var pairPortals = new List<GameObject>();
        for (int i = 0; i < pairN; i++)
        {
            pairPortals.Add(SpawnRicoPortal(starts[i], ends[i], crackPortal));
            yield return PausableWait(ricoPortalInterval);
            pairPortals.Add(SpawnRicoPortal(ends[i], starts[i], crackPortal));
            yield return PausableWait(ricoPortalInterval);
        }

        // ④ 예고선: 보스 → E (돌아올 때도 같은 줄이라 끝까지 남김), 그다음 쌍마다 직선 하나씩 순서대로 (전체 ricoWarnDrawTime 초)
        var segs = new List<LineRenderer> { aim };
        SetWarnLine(aim, bow, entry, 1f);
        float per = ricoWarnDrawTime / pairN;
        for (int i = 0; i < pairN; i++)
        {
            LineRenderer lr = MakeWarnLine();
            segs.Add(lr);
            float g = 0f;
            while (g < per)
            {
                if (!IsWorldPaused) g += Time.deltaTime;
                SetWarnLine(lr, starts[i], ends[i], per > 0f ? Mathf.Clamp01(g / per) : 1f);
                yield return null;
            }
            SetWarnLine(lr, starts[i], ends[i], 1f);
        }
        yield return PausableWait(ricoWarnHold);

        // ⑤ 횡 사격 → 보스에서 E 로
        //    [2026-11-20] 시위를 당기기 시작할 때 활시위 이펙트 전체를 재생 — 이펙트의 발사 시각이 시위를 놓는 순간과 겹치게 재생 속도를 맞춤
        SpawnRicoShotFx(bow);
        yield return PlayShotUntilRelease(shotH, shotHReleaseFrame);
        GameObject arrow = Make(ricoBoltPrefab != null ? ricoBoltPrefab : ArrowFx, bow, Quaternion.identity);
        yield return MoveArrowTo(arrow, () => entry, ricoRelaySpeed, 0.15f, RicoArrowHitRadius());
        ShowVisual(idleVisual);
        if (arrow == null) { CleanupWarn(segs); ClosePortal(entryPortal, crackPortal); foreach (var g in pairPortals) ClosePortal(g, crackPortal); yield break; }
        Make(sparkFx, entry, Quaternion.identity, 0.6f);

        // 쌍마다: 시작 포탈로 순간이동 → 끝 포탈까지 날아감 → 그 쌍의 선·포탈 정리
        for (int i = 0; i < pairN; i++)
        {
            TeleportArrow(arrow, starts[i]);
            SetArrowAngle(arrow, ends[i] - starts[i]);
            Make(sparkFx, starts[i], Quaternion.identity, 0.6f);
            int k = i;
            yield return MoveArrowTo(arrow, () => ends[k], ricoRelaySpeed, 0.15f, RicoArrowHitRadius());
            if (arrow == null) break;
            Make(sparkFx, ends[i], Quaternion.identity, 0.6f);
            KillWarnLine(segs, i + 1);
            ClosePortal(pairPortals[i * 2], crackPortal);
            ClosePortal(pairPortals[i * 2 + 1], crackPortal);
        }
        if (arrow == null) { CleanupWarn(segs); ClosePortal(entryPortal, crackPortal); foreach (var g in pairPortals) ClosePortal(g, crackPortal); yield break; }

        // ⑥ 마지막 쌍 끝 → E 에서 나와 보스에게 돌아옴
        TeleportArrow(arrow, entry);
        Make(sparkFx, entry, Quaternion.identity, 0.6f);
        yield return MoveArrowTo(arrow, () => transform.position + Vector3.up * bowOffsetH.y, ricoRelaySpeed, batDistance, RicoArrowHitRadius());
        ClosePortal(entryPortal, crackPortal);
        CleanupWarn(segs);
        if (arrow == null) yield break;

        // ⑦ 활대로 쳐내 플레이어 쪽으로 — 화살이 날아온 쪽을 보고 휘두름
        ShowParry(arrow.transform.position.x >= transform.position.x ? 1 : -1);
        Make(ricoImpactFx != null ? ricoImpactFx : sparkFx, arrow.transform.position, Quaternion.identity, 1f);
        Vector2 toPlayer = player != null ? ((Vector2)(PlayerCenter() - arrow.transform.position)).normalized : new Vector2(lockedDir, 0f);
        Coroutine fly = StartCoroutine(MoveArrowUntilWall(arrow, toPlayer, batSpeed, 4f));
        yield return PausableWait(batPoseTime);
        ShowVisual(idleVisual);
        yield return fly;
        if (arrow != null) { Make(ricoImpactFx != null ? ricoImpactFx : sparkFx, arrow.transform.position, Quaternion.identity, 1f); live.Remove(arrow); Destroy(arrow); }
    }

    // [2026-11-20 추가] 화살 순간이동 — 잔상(Trail)이 이전 자리에서 새 자리까지 길게 이어지지 않게 지움
    //   [2026-11-20] 거리 비례 방출(Rate over Distance) 파티클은 순간이동한 거리만큼 한 줄로 뿌려지므로, 이동하는 프레임 동안 방출을 잠깐 끔
    void TeleportArrow(GameObject arrow, Vector3 pos)
    {
        if (arrow == null) return;
        var ps = arrow.GetComponentsInChildren<ParticleSystem>();
        var on = new bool[ps.Length];
        for (int i = 0; i < ps.Length; i++) { var em = ps[i].emission; on[i] = em.enabled; em.enabled = false; }
        arrow.transform.position = pos;
        foreach (TrailRenderer tr in arrow.GetComponentsInChildren<TrailRenderer>()) tr.Clear();
        StartCoroutine(ReenableEmission(ps, on));
    }

    static IEnumerator ReenableEmission(ParticleSystem[] ps, bool[] on)
    {
        yield return null;   // 순간이동한 프레임이 지나간 뒤 다시 켬 (그 사이 이동 거리는 방출에 안 셈)
        yield return null;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i] != null) { var em = ps[i].emission; em.enabled = on[i]; }
    }

    // [2026-11-20 추가] 이펙트를 보스가 보는 방향(dir: 1 = 오른쪽)으로 향하게 하는 Scale X
    float RicoFxScaleX(int dir)
    {
        float sx = Mathf.Abs(ricoShotFxPrefab.transform.localScale.x);
        return sx * dir * (ricoShotFxFacesRight ? 1f : -1f);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    // [2026-11-20 추가] 이펙트 프리팹 생성 — 루트 원점이 그림과 떨어져 있어서(약 7.8) 화살이 나가는 지점(ricoShotFxAnchor, spot)이 pos 에 오게 옮김
    GameObject MakeRicoFx(Vector3 pos, int dir)
    {
        GameObject g = Instantiate(ricoShotFxPrefab, pos, Quaternion.identity);
        Vector3 sc = g.transform.localScale; sc.x = RicoFxScaleX(dir); g.transform.localScale = sc;
        Transform anchor = !string.IsNullOrEmpty(ricoShotFxAnchor) ? FindDeep(g.transform, ricoShotFxAnchor) : null;
        if (anchor != null)
        {
            Vector3 d = anchor.position - g.transform.position; d.z = 0f;
            g.transform.position -= d;
        }
        return g;
    }

    // [2026-11-20 추가] 활시위 이펙트 전체 재생 — 재생 속도 k = (이펙트 발사 시각) / (시위를 놓을 때까지 걸리는 시간)
    void SpawnRicoShotFx(Vector3 bow)
    {
        if (ricoShotFxPrefab == null) return;
        int dir = lockedDir >= 0 ? 1 : -1;
        Vector3 pos = bow + new Vector3(ricoShotFxOffset.x * dir, ricoShotFxOffset.y, 0f);
        GameObject g = MakeRicoFx(pos, dir);
        float release = shotH != null ? shotH.TimeUntilFrame(shotHReleaseFrame) : 0f;
        float k = release > 0.05f ? ricoShotFxFireTime / release : 1f;
        live.Add(g);
        StartCoroutine(DriveFxSpeed(g, k, ricoShotFxLength / Mathf.Max(0.05f, k)));
    }

    // 이펙트 안 파티클·애니메이터·타임라인을 k 배속으로 (멈춤 중이면 0), life 초 뒤 지움
    IEnumerator DriveFxSpeed(GameObject g, float k, float life)
    {
        ParticleSystem[] ps = g.GetComponentsInChildren<ParticleSystem>(true);
        Animator[] an = g.GetComponentsInChildren<Animator>(true);
        UnityEngine.Playables.PlayableDirector[] pd = g.GetComponentsInChildren<UnityEngine.Playables.PlayableDirector>(true);
        float t = 0f;
        while (g != null && t < life)
        {
            float s = IsWorldPaused ? 0f : k;
            SetParticleSpeed(ps, s);
            foreach (var a in an) if (a != null) a.speed = s;
            foreach (var d in pd)
                if (d != null && d.playableGraph.IsValid() && d.playableGraph.GetRootPlayableCount() > 0)
                    d.playableGraph.GetRootPlayable(0).SetSpeed(s);
            if (!IsWorldPaused) t += Time.deltaTime;
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    // [2026-11-20 추가] 도탄 포탈 하나 생성 — look 쪽을 바라보게 회전, 균열 포탈이면 크기 조절 후 빠르게 열고 유지
    GameObject SpawnRicoPortal(Vector3 pos, Vector3 look, bool crack)
    {
        Vector2 d = look - pos;
        GameObject g = Make(crack ? ricoPortalPrefab : portalPrefab, pos, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
        if (crack && g != null)
        {
            g.transform.localScale *= ricoPortalScale;
            StartCoroutine(CrackPortalOpenHold(g));
        }
        return g;
    }

    // [2026-11-20] 포탈 높이 범위 (2차 수정)
    //   1순위: 보스가 들어 있는 보스방(Room)의 카메라 범위(roomCameraCenter ± roomCameraSize/2) — 방 전체 높이
    //   2순위: 스프라이트를 그리는 2D 직교 카메라(CameraFollow.camera2D)의 화면
    //   3순위: Camera.main
    //   (Camera.main 은 3D 원근 카메라라서 z=0 에서 보이는 범위가 실제 화면(2D 카메라)보다 좁게 계산될 수 있었음)
    void GetRelayHeightRange(float floor, out float y0, out float y1)
    {
        y0 = floor + 1f;
        y1 = floor + 10f;
        float m = Mathf.Clamp(ricoScreenMargin, 0f, 0.45f);
        string src = "fallback";
        Room room = GetComponentInParent<Room>();
        if (room != null && room.isBossRoom)
        {
            float cy = room.roomCameraCenter != null ? room.roomCameraCenter.position.y : room.transform.position.y;
            float h = room.roomCameraSize.y;
            y0 = cy - h * 0.5f + h * m;
            y1 = cy + h * 0.5f - h * m;
            src = "room";
        }
        else
        {
            Camera c2 = CameraFollow.Instance != null ? CameraFollow.Instance.camera2D : null;
            if (c2 != null && c2.orthographic)
            {
                float cy = c2.transform.position.y, h = c2.orthographicSize * 2f;
                y0 = Mathf.Max(y0, cy - h * 0.5f + h * m);
                y1 = cy + h * 0.5f - h * m;
                src = "camera2D";
            }
            else if (Camera.main != null)
            {
                Camera c = Camera.main;
                float z = Mathf.Abs(transform.position.z - c.transform.position.z);
                y0 = Mathf.Max(y0, c.ViewportToWorldPoint(new Vector3(0.5f, m, z)).y);
                y1 = c.ViewportToWorldPoint(new Vector3(0.5f, 1f - m, z)).y;
                src = "Camera.main";
            }
        }
        if (y1 < y0 + 1f) y1 = y0 + 1f;
        if (ricoDebugLog) Debug.Log($"[ArcherMidBoss] 도탄 포탈 높이 범위 {y0:F1} ~ {y1:F1} (기준: {src})");
    }

    // [2026-11-20] 높이 n 개 — 범위를 n 칸으로 나눠 칸마다 하나씩, 칸 순서를 섞어 연달아 나오는 높이가 minStep 이상 차이 나게
    //              (시작 높이 = 보스 활 높이 = 반대쪽 벽 포탈 높이). 조건을 다 만족하는 순서가 없으면 가장 덜 겹치는 순서
    float[] PlanRelayHeights(int n, float y0, float y1, float startY)
    {
        n = Mathf.Max(1, n);
        float slot = (y1 - y0) / n;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        float[] best = null; float bestScore = -1f;
        for (int tries = 0; tries < 40; tries++)
        {
            for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            var ys = new float[n];
            float prev = startY, minStep = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                ys[i] = y0 + slot * (order[i] + Random.Range(0.25f, 0.75f));   // 칸 안에서 가운데 쪽 랜덤
                minStep = Mathf.Min(minStep, Mathf.Abs(ys[i] - prev));
                prev = ys[i];
            }
            if (minStep >= ricoMinHeightStep) return ys;
            if (minStep > bestScore) { bestScore = minStep; best = ys; }
        }
        return best;
    }

    // [2026-11-20] 포탈 가로 위치 — edge 쪽(1 = 오른쪽) 맵 가장자리에서 안쪽으로 플레이어 가로 길이 × ricoEdgeWidths 범위 랜덤
    Vector3 RelayPoint(int edge, float y, float left, float right)
    {
        Collider2D pc = player != null ? player.GetComponent<Collider2D>() : null;
        float band = Mathf.Max(ricoEdgeInset + 0.2f, (pc != null ? pc.bounds.size.x : 1f) * ricoEdgeWidths);
        float x0 = edge > 0 ? right - band : left + ricoEdgeInset;
        float x1 = edge > 0 ? right - ricoEdgeInset : left + band;
        return new Vector3(Random.Range(x0, x1), y, 0f);
    }

    // [2026-11-20 추가] 균열 포탈: ricoPortalAnimSpeed 배속으로 열고 ricoPortalHoldAt 에서 멈춤 (ClosePortal 이 부르면 이어서 재생해 닫힘)
    readonly HashSet<GameObject> closingPortals = new HashSet<GameObject>();

    IEnumerator CrackPortalOpenHold(GameObject g)
    {
        Animator anim = g.GetComponent<Animator>();
        ParticleSystem[] ps = g.GetComponentsInChildren<ParticleSystem>();
        float animT = 0f;
        while (g != null && !closingPortals.Contains(g))
        {
            bool hold = animT >= ricoPortalHoldAt;
            float s = IsWorldPaused ? 0f : ricoPortalAnimSpeed;
            if (anim != null) anim.speed = hold ? 0f : s;
            SetParticleSpeed(ps, s);
            if (!hold) animT += Time.deltaTime * s;
            yield return null;
        }
        if (g == null) yield break;
        if (anim != null) anim.speed = ricoPortalAnimSpeed;
        SetParticleSpeed(ps, ricoPortalAnimSpeed);
    }

    void ClosePortal(GameObject g, bool crack)
    {
        if (g == null) return;
        live.Remove(g);
        if (!crack) { Destroy(g, 0.15f); return; }
        closingPortals.Add(g);
        float rest = Mathf.Max(0.1f, ricoPortalAnimLength - ricoPortalHoldAt) / Mathf.Max(0.1f, ricoPortalAnimSpeed);
        Destroy(g, rest + 0.1f);
    }

    // [2026-11-19 추가] 예고용 붉은 광선 (LineRenderer)
    LineRenderer MakeWarnLine()
    {
        var go = new GameObject("RicoWarnLine");
        live.Add(go);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = ricoWarnWidth;
        lr.numCapVertices = 2;
        if (ricoWarnTextMaterial != null)
        {
            // [2026-11-20] WARNING 글자 띠: 선 길이를 따라 텍스처를 반복 (Tile) — 반복 간격은 SetWarnLine 에서 _Tiling 으로 맞춤
            lr.sharedMaterial = ricoWarnTextMaterial;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 0;
            lr.startColor = lr.endColor = Color.white;
        }
        else
        {
            if (ricoWarnMaterial == null) ricoWarnMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.sharedMaterial = ricoWarnMaterial;
            lr.startColor = lr.endColor = ricoWarnColor;
        }
        Renderer br = allRenderers != null && allRenderers.Length > 0 ? allRenderers[0] : null;
        if (br != null) { lr.sortingLayerID = br.sortingLayerID; lr.sortingOrder = br.sortingOrder + 20; }
        return lr;
    }

    static readonly int IdTiling = Shader.PropertyToID("_Tiling");
    static readonly int IdOffset = Shader.PropertyToID("_Offset");
    static readonly int IdScroll = Shader.PropertyToID("_Scroll");
    MaterialPropertyBlock warnMpb;

    void SetWarnLine(LineRenderer lr, Vector3 a, Vector3 b, float grow)
    {
        if (lr == null) return;
        lr.SetPosition(0, a);
        lr.SetPosition(1, Vector3.Lerp(a, b, grow));
        if (ricoWarnTextMaterial == null || lr.sharedMaterial != ricoWarnTextMaterial) return;

        // [2026-11-20] 글자 띠 Tiling: Tile 모드에서 UV.x = 선을 따라 간 거리(월드). 텍스처 한 장 길이 = 굵기 × 가로세로비
        //   왼쪽으로 가는 선은 가로·세로를 같이 뒤집어(180° 회전) 글자가 거꾸로 서지 않게 하고, 흐르는 방향은 항상 a → b
        float tile = 1f / Mathf.Max(0.001f, ricoWarnWidth * ricoWarnTextAspect);
        bool left = b.x < a.x;
        float sx = left ? -tile : tile;
        float sy = (left ? -1f : 1f) * (ricoWarnTextFlipV ? -1f : 1f);
        if (warnMpb == null) warnMpb = new MaterialPropertyBlock();
        lr.GetPropertyBlock(warnMpb);
        warnMpb.SetVector(IdTiling, new Vector4(sx, sy, 0f, 0f));
        warnMpb.SetVector(IdOffset, new Vector4(0f, sy < 0f ? 1f : 0f, 0f, 0f));
        warnMpb.SetVector(IdScroll, new Vector4(-Mathf.Sign(sx) * ricoWarnTextScroll, 0f, 0f, 0f));
        lr.SetPropertyBlock(warnMpb);
    }

    void KillWarnLine(List<LineRenderer> segs, int i)
    {
        if (i < 0 || i >= segs.Count || segs[i] == null) return;
        live.Remove(segs[i].gameObject); Destroy(segs[i].gameObject); segs[i] = null;
    }

    void CleanupWarn(List<LineRenderer> segs)
    {
        for (int i = 0; i < segs.Count; i++) KillWarnLine(segs, i);
    }

    // 화살을 방향대로 움직이다 벽에 닿으면 멈춤 (maxTime 이 지나도 멈춤). 지나가며 닿으면 피해 (구간마다 한 번)
    IEnumerator MoveArrowUntilWall(GameObject arrow, Vector2 dir, float speed, float maxTime)
    {
        if (arrow == null) yield break;
        dir = dir.normalized;
        SetArrowAngle(arrow, dir);
        bool hit = false; float t = 0f;
        while (arrow != null && t < maxTime)
        {
            if (!IsWorldPaused)
            {
                float step = speed * Time.deltaTime;
                Vector2 p = arrow.transform.position;
                RaycastHit2D h = Physics2D.Raycast(p, dir, step + 0.1f, wallMask);
                if (h.collider != null) { arrow.transform.position = h.point - dir * 0.15f; break; }
                arrow.transform.position = p + dir * step;
                t += Time.deltaTime;
                if (!hit) hit = HitPlayerArrow(arrow.transform.position, dir, ricochetDamage);
            }
            yield return null;
        }
    }

    // [2026-11-20] hitRadius > 0 이면 얇은 판정 + 이번 프레임에 지나온 구간 전체를 검사 (아주 빠른 화살이 플레이어를 건너뛰지 않게)
    IEnumerator MoveArrowTo(GameObject arrow, System.Func<Vector3> target, float speed, float stopDist, float hitRadius = -1f)
    {
        bool hit = false; float t = 0f;
        while (arrow != null && t < 4f)
        {
            if (!IsWorldPaused)
            {
                Vector3 tg = target();
                Vector2 dir = ((Vector2)(tg - arrow.transform.position));
                float dist = dir.magnitude;
                if (dist <= stopDist) break;
                dir.Normalize();
                SetArrowAngle(arrow, dir);
                Vector3 prev = arrow.transform.position;
                arrow.transform.position += (Vector3)(dir * Mathf.Min(speed * Time.deltaTime, dist));   // [2026-11-20] 빠를 때 목표를 지나쳐 왔다갔다하지 않게
                t += Time.deltaTime;
                if (!hit)
                {
                    if (hitRadius > 0f)
                        hit = HitPlayerSweep((Vector2)prev - dir * arrowHalfLength, (Vector2)arrow.transform.position + dir * arrowHalfLength, hitRadius, dir, ricochetDamage);
                    else
                        hit = HitPlayerArrow(arrow.transform.position, dir, ricochetDamage);
                }
            }
            yield return null;
        }
    }

    static void SetArrowAngle(GameObject arrow, Vector2 dir)
    {
        if (arrow == null) return;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // ───────────────────────── 밀쳐내기 ─────────────────────────
    IEnumerator PushPattern()
    {
        pushTimer = pushCooldown;
        ShowParry(player != null && player.position.x < transform.position.x ? -1 : 1);
        yield return PausableWait(0.12f);
        Vector3 c = transform.position + new Vector3(pushBox.x * 0.5f * lockedDir, pushBox.y * 0.5f, 0f);
        HitPlayerBox(c, pushBox, pushDamage, lockedDir, pushKnockX, pushKnockY);
        Make(sparkFx, c, Quaternion.identity, 0.6f);
        yield return PausableWait(0.35f);
        ShowVisual(idleVisual);
        yield return PausableWait(0.25f);
    }

    // ───────────────────────── 공용 ─────────────────────────
    IEnumerator TeleportTo(Vector3 dest)
    {
        Make(teleportFx, transform.position + Vector3.up * 2f, Quaternion.identity, 1.5f);
        ShowVisual(null);
        yield return PausableWait(teleportHide);
        rigid.velocity = Vector2.zero;
        rigid.position = dest;
        transform.position = dest;
        Make(teleportFx, dest + Vector3.up * 2f, Quaternion.identity, 1.5f);
        ShowVisual(idleVisual);
    }

    // 날아가는 그림만 (피해 없음) — 화살이 포탈로 들어가는 연출 / 포탈에서 내리꽂는 연출
    IEnumerator FlyVisual(GameObject prefab, Vector3 from, Vector3 to, float speed, bool shrinkAtEnd)
    {
        GameObject g = Make(prefab, from, Quaternion.identity);
        if (g == null) yield break;
        Vector2 dir = (to - from).normalized;
        SetArrowAngle(g, dir);
        while (g != null && Vector3.Distance(g.transform.position, to) > 0.05f)
        {
            if (!IsWorldPaused) g.transform.position = Vector3.MoveTowards(g.transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    GameObject Make(GameObject prefab, Vector3 pos, Quaternion rot, float lifetime = 0f)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, pos, rot);
        if (lifetime > 0f) Destroy(g, lifetime);
        else live.Add(g);
        return g;
    }

    // 플레이어가 서 있는 줄을 기준으로 방의 왼쪽·오른쪽 벽, 바닥, 천장
    void GetRoomBounds(Vector3 from, out float left, out float right, out float floor, out float ceil)
    {
        Vector2 o = from + Vector3.up * 0.8f;
        RaycastHit2D h;
        h = Physics2D.Raycast(o, Vector2.left, 60f, wallMask); left = h.collider != null ? h.point.x : o.x - 12f;
        h = Physics2D.Raycast(o, Vector2.right, 60f, wallMask); right = h.collider != null ? h.point.x : o.x + 12f;
        h = Physics2D.Raycast(o, Vector2.down, 60f, wallMask); floor = h.collider != null ? h.point.y : from.y;
        h = Physics2D.Raycast(o, Vector2.up, 60f, wallMask); ceil = h.collider != null ? h.point.y : from.y + 12f;
    }

    Vector3 PlayerCenter()
    {
        if (player == null) return transform.position;
        Collider2D c = player.GetComponent<Collider2D>();
        return c != null ? c.bounds.center : player.position;
    }

    Health PlayerHealth()
    {
        if (player == null) return null;
        if (playerHealth == null || playerHealth.transform != player) playerHealth = player.GetComponent<Health>();
        return playerHealth;
    }

    bool HitPlayerCircle(Vector3 c, float r, float mul, int dir, float kbX, float kbY)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        Vector2 pc = col != null ? (Vector2)col.ClosestPoint(c) : (Vector2)player.position;
        if (Vector2.Distance(pc, c) > r) return false;
        if (dir == 0) dir = player.position.x >= c.x ? 1 : -1;
        return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir, kbX, kbY, pc, null, null, "ArcherMidBoss", false);
    }

    // 화살 그림 길이를 따라 판정 (가운데 점 하나만 보면 긴 화살의 머리·꼬리에 닿아도 안 맞던 문제)
    bool HitPlayerArrow(Vector3 center, Vector2 dir, float mul)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        if (col == null) return HitPlayerCircle(center, ricochetHitRadius, mul, dir.x < 0 ? -1 : 1, 5f, 2f);
        Vector2 n = dir.normalized;
        for (int i = 0; i <= 6; i++)
        {
            Vector2 p = (Vector2)center + n * Mathf.Lerp(-arrowHalfLength, arrowHalfLength, i / 6f);
            Vector2 cp = col.ClosestPoint(p);
            if (Vector2.Distance(cp, p) <= ricochetHitRadius)
                return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, n.x < 0 ? -1 : 1, 5f, 2f, cp, null, null, "ArcherMidBoss", false);
        }
        return false;
    }

    // [2026-11-20 추가] 도탄 릴레이 화살 판정 반지름 (= 굵기 / 2)
    float RicoArrowHitRadius()
    {
        float w = ricoArrowHitWidth > 0f ? ricoArrowHitWidth : ricoWarnWidth;
        return Mathf.Max(0.01f, w * 0.5f);
    }

    // [2026-11-20 추가] 선분 a→b 를 따라 0.2 간격으로 검사 — 반지름 r 이내에 플레이어 콜라이더가 있으면 피해
    //   (플레이어 콜라이더 안쪽 점은 ClosestPoint 거리가 0 이라, 간격이 플레이어 폭보다 좁으면 관통을 놓치지 않음)
    bool HitPlayerSweep(Vector2 a, Vector2 b, float r, Vector2 dir, float mul)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        if (col == null) return HitPlayerCircle(b, r, mul, dir.x < 0 ? -1 : 1, 5f, 2f);
        int n = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f), 1, 200);
        for (int i = 0; i <= n; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)n);
            Vector2 cp = col.ClosestPoint(p);
            if (Vector2.Distance(cp, p) <= r)
                return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir.x < 0 ? -1 : 1, 5f, 2f, cp, null, null, "ArcherMidBoss", false);
        }
        return false;
    }

    bool HitPlayerBox(Vector3 center, Vector2 size, float mul, int dir, float kbX, float kbY)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        Bounds pb = col != null ? col.bounds : new Bounds(player.position, Vector3.one);
        Bounds box = new Bounds(center, new Vector3(size.x, size.y, 100f));
        pb.center = new Vector3(pb.center.x, pb.center.y, 0f);
        if (!box.Intersects(pb)) return false;
        return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir, kbX, kbY, pb.center, null, null, "ArcherMidBoss", false);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, pushRange);
        Gizmos.color = Color.cyan;
        int d = transform.localScale.x >= 0 ? 1 : -1;
        Gizmos.DrawWireSphere(transform.position + new Vector3(bowOffsetH.x * d, bowOffsetH.y, 0), 0.2f);
        Gizmos.DrawWireSphere(transform.position + new Vector3(bowOffsetV.x * d, bowOffsetV.y, 0), 0.2f);
    }
}
