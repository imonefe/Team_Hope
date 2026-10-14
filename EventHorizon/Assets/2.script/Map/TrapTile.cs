using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함정 타일맵에 붙이는 스크립트
/// Composite Collider 2D → Is Trigger 체크 필요
/// Stay 이벤트에 의존하지 않고, 닿아있는 대상을 추적해 Update에서 주기 데미지
/// </summary>
public class TrapTile : MonoBehaviour
{
    [Header("데미지 설정")]
    public int damage = 10;
    public float damageCooldown = 0.5f;

    [Header("넉백")]
    public float knockbackX = 5f;
    public float knockbackY = 3f;

    // 현재 함정에 닿아있는 대상 + 다음 데미지까지 남은 시간
    Dictionary<Health, float> touching = new Dictionary<Health, float>();

    void Update()
    {
        var keys = new List<Health>(touching.Keys);
        foreach (var h in keys)
        {
            if (h == null) { touching.Remove(h); continue; }

            touching[h] -= Time.deltaTime;
            if (touching[h] <= 0f)
            {
                DealDamage(h);
                touching[h] = damageCooldown;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 히트박스(공격 판정)는 함정 피해 대상이 아님
        if (Hitbox.IsHitboxCollider(other)) return;
        Health health = other.GetComponent<Health>()
                     ?? other.GetComponentInParent<Health>();
        if (health == null) return;

        if (!touching.ContainsKey(health))
        {
            // 닿는 즉시 첫 데미지
            DealDamage(health);
            touching[health] = damageCooldown;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (Hitbox.IsHitboxCollider(other)) return;
        Health health = other.GetComponent<Health>()
                     ?? other.GetComponentInParent<Health>();
        if (health == null) return;

        touching.Remove(health);
    }

    void DealDamage(Health health)
    {
        if (health == null) return;
        int dir = health.transform.position.x > transform.position.x ? 1 : -1;
        health.TakeDamage(damage, dir, knockbackX, knockbackY);
    }
}