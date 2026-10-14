using UnityEngine;

/// <summary>
/// 직선 투사체
/// 적이 바라보는 방향으로 직선 이동 후 소멸
/// </summary>
public class ProjectileStraight : ProjectileBase
{
    [Header("설정")]
    public float moveSpeed = 10f;
    public float lifetime = 2f;

    [Header("감속 (선택)")]
    [Tooltip("true면 수명이 끝나기 직전 decelerateDuration 동안 점점 느려지다가 사라짐")]
    public bool decelerateAtEnd = false;
    [Tooltip("감속이 시작되어 속도 0이 될 때까지 걸리는 시간(초). lifetime의 마지막 구간에 적용됨")]
    public float decelerateDuration = 0.5f;

    Vector2 direction;
    float elapsed = 0f;

    protected override float FacingX => direction.x;

    public void Init(Vector2 dir)
    {
        direction = dir.normalized;

        // 순수 좌우 방향(위아래 성분 없음)이면 회전 대신 좌우 반전(스케일)으로 표현.
        // 180도 회전으로 처리하면 이펙트가 뒤집혀(위아래까지 반전) 보이거나,
        // 상하 대칭인 이펙트라 눈에 반전 자체가 안 보이는 문제가 생기기 때문.
        if (Mathf.Approximately(dir.y, 0f))
        {
            transform.rotation = Quaternion.identity;
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (dir.x < 0f ? -1f : 1f);
            transform.localScale = s;
        }
        else
        {
            // 대각선 등 순수 좌우가 아닌 방향은 기존처럼 회전으로 표현
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // [2026-10-10 수정] 수명은 Destroy 예약 대신 아래 FixedUpdate에서 센다 — 퍼즈로 멈춘 동안은 수명이 줄지 않음
    }

    void FixedUpdate()
    {
        if (PauseCheck()) return;

        elapsed += Time.fixedDeltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 수명 마지막 decelerateDuration 구간 동안 속도를 1 -> 0으로 선형 감속
        float speedScale = 1f;
        if (decelerateAtEnd && decelerateDuration > 0f)
        {
            float timeLeft = lifetime - elapsed;
            if (timeLeft < decelerateDuration)
                speedScale = Mathf.Clamp01(timeLeft / decelerateDuration);
        }

        rigid.velocity = direction * moveSpeed * speedScale;
    }
}
