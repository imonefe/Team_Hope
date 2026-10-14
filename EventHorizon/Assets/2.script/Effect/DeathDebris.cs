using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 이펙트 프리팹 안에 "잔해 조각"(머리, 무기 등)으로 넣는 오브젝트에 붙이는 스크립트.
/// 스폰되는 순간 랜덤한 힘과 회전력(토크)을 받아 튕겨나갔다가, Rigidbody2D/Collider2D의
/// 물리 마찰로 자연스럽게 굴러다니다 멈춘다. 일정 시간 뒤 서서히 사라지며 파괴된다.
///
/// [사용법]
/// 1. 잔해로 쓸 스프라이트(머리, 무기 등)에 SpriteRenderer + Rigidbody2D(Body Type: Dynamic)
///    + Collider2D(예: CircleCollider2D, PolygonCollider2D 등)를 붙이고 이 스크립트를 추가한다.
/// 2. Collider2D의 Material 슬롯에 마찰이 있는 PhysicsMaterial2D를 연결한다.
///    (Friction 0이면 계속 미끄러지기만 하고, 너무 높으면 거의 안 구른다. 0.3~0.6 권장)
/// 3. 오브젝트의 레이어를 지면(ground/platform)과 충돌하는 레이어로 설정한다.
/// 4. 이 오브젝트를 죽음 이펙트 프리팹(예: diSpon.prefab)의 자식으로 넣어두면,
///    Health.cs / BossController.cs 쪽 코드 수정 없이 적이 죽을 때 자동으로 함께 스폰된다
///    (두 스크립트 모두 deathEffectPrefab을 그대로 Instantiate만 하기 때문).
/// 5. 몬스터 종류별로 다른 잔해(머리 vs 무기 등)를 쓰고 싶다면, 이 오브젝트를 포함한
///    death 이펙트 프리팹을 몬스터별로 variant로 나눠서 각 적 프리팹의
///    deathEffectPrefab 필드에 각각 연결하면 된다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DeathDebris : MonoBehaviour
{
    [Header("발사 힘")]
    [Tooltip("좌우로 튕겨나가는 힘의 범위 (좌/우 방향은 랜덤으로 결정됨)")]
    public Vector2 launchForceX = new Vector2(1.5f, 4f);
    [Tooltip("위로 튀어오르는 힘의 범위")]
    public Vector2 launchForceY = new Vector2(4f, 7f);
    [Tooltip("회전력(토크) 범위 — 튕겨나가며 도는 느낌")]
    public Vector2 torqueRange = new Vector2(-180f, 180f);

    [Header("수명")]
    [Tooltip("이 시간(초) 동안 물리적으로 굴러다닌 뒤 페이드 시작")]
    public float lifetime = 3f;
    [Tooltip("사라지는 데 걸리는 시간(초)")]
    public float fadeDuration = 0.5f;

    Rigidbody2D rigid;
    SpriteRenderer sr;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // 좌우 랜덤 방향으로 튕겨나감
        float dir = Random.value < 0.5f ? -1f : 1f;
        float forceX = dir * Random.Range(launchForceX.x, launchForceX.y);
        float forceY = Random.Range(launchForceY.x, launchForceY.y);
        rigid.AddForce(new Vector2(forceX, forceY), ForceMode2D.Impulse);

        float torque = Random.Range(torqueRange.x, torqueRange.y);
        rigid.AddTorque(torque, ForceMode2D.Impulse);

        StartCoroutine(FadeAndDestroy());
    }

    IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(lifetime);

        if (sr != null)
        {
            float t = 0f;
            Color start = sr.color;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(start.a, 0f, t / fadeDuration);
                sr.color = new Color(start.r, start.g, start.b, a);
                yield return null;
            }
        }

        Destroy(gameObject);
    }
}
