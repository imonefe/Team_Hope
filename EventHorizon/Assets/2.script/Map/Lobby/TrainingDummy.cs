using UnityEngine;

/// <summary>
/// [2026-10-17 추가] 로비 허수아비. 맞아도 죽지 않는 과녁 — 무기 손맛을 미리 확인하는 용도.
///
/// - 같은 오브젝트의 Stats·Health 로 일반 적처럼 맞는다 (공격 판정·총 레이저·데미지 숫자·피격음 모두 그대로).
/// - 체력이 줄어드는 순간 바로 다시 가득 채워서 절대 죽지 않는다.
/// - Enemy 컴포넌트가 없어서 방 클리어 판정(남은 적 확인)에는 잡히지 않는다 → 로비 포탈이 바로 열림.
/// - 맞으면 그림이 살짝 흔들리고 붉게 번쩍인다.
/// - [2026-10-18 변경] 흔들림 축을 그림의 발밑(아래 가운데)으로 — 바닥에 박힌 채 위쪽이 까딱거린다.
/// </summary>
[RequireComponent(typeof(Stats))]
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    [Header("흔들림")]
    [Tooltip("흔들릴 그림 (비워두면 자식 SpriteRenderer)")]
    public Transform visual;
    [Tooltip("맞았을 때 기울어지는 최대 각도")]
    public float wobbleAngle = 10f;
    [Tooltip("흔들림이 잦아드는 시간(초)")]
    public float wobbleTime = 0.35f;

    [Header("피격 번쩍임")]
    public Color hitColor = new Color(1f, 0.55f, 0.55f, 1f);
    public float flashTime = 0.08f;

    Stats stats;
    SpriteRenderer sr;
    Color baseColor = Color.white;
    float wobbleTimer;
    float flashTimer;
    float baseAngle;
    Vector3 basePos;          // 흔들리기 전 그림 위치 (부모 기준)
    Quaternion baseRot;       // 흔들리기 전 그림 회전
    Vector3 pivotLocal;       // 흔들림 축 = 그림 발밑 (부모 기준)

    void Awake()
    {
        stats = GetComponent<Stats>();
        stats.currentHp = stats.maxHp;
        stats.OnHpChanged += OnHpChanged;

        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        if (visual == null && sr != null) visual = sr.transform;
        if (sr != null) baseColor = sr.color;
        if (visual != null)
        {
            baseAngle = visual.localEulerAngles.z;
            basePos = visual.localPosition;
            baseRot = visual.localRotation;
            pivotLocal = basePos;
            if (sr != null && sr.sprite != null && visual.parent != null)
            {
                Bounds b = sr.bounds;
                pivotLocal = visual.parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, visual.position.z));
            }
            else if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.bounds;
                pivotLocal = new Vector3(b.center.x, b.min.y, visual.position.z);
            }
        }
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnHpChanged -= OnHpChanged;
    }

    // 체력이 줄면 바로 다시 채움 (Health 가 사망 판정을 하기 전에 채워지므로 죽지 않음)
    void OnHpChanged(int current, int max)
    {
        if (current >= max) return;
        stats.currentHp = max;
        wobbleTimer = wobbleTime;
        flashTimer = flashTime;
    }

    void Update()
    {
        if (visual != null)
        {
            float angle = 0f;
            if (wobbleTimer > 0f)
            {
                wobbleTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(wobbleTimer / wobbleTime);
                angle = Mathf.Sin(t * Mathf.PI * 4f) * wobbleAngle * (1f - t);
            }
            // 발밑을 축으로 회전: 회전한 만큼 위치도 축 둘레로 돌려 준다
            Quaternion q = Quaternion.Euler(0f, 0f, baseAngle + angle);
            visual.localRotation = q;
            visual.localPosition = pivotLocal + q * Quaternion.Inverse(baseRot) * (basePos - pivotLocal);
        }

        if (sr != null)
        {
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            sr.color = flashTimer > 0f ? hitColor * baseColor : baseColor;
        }
    }
}
