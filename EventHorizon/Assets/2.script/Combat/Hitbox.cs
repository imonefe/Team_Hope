using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 근접 판정용 히트박스. 타격 방식(hitMode)에 따라 두 가지로 동작한다:
///  - Single   : 대상 하나당 딱 한 번만 데미지 (기존 방식 그대로 — 검격 등 스쳐 지나가는 판정용)
///  - Periodic : 히트박스 안에 머무는 동안 tickInterval 초마다 계속 데미지 (불바닥·독구름 같은 지속 피해 장판용)
/// 데미지 계산·치명타·연출(쉐이크/사운드/이펙트)·퍼즈 게이지 로직은 두 모드가 완전히 공유한다.
/// </summary>
public class Hitbox : MonoBehaviour
{
    public enum HitMode
    {
        Single,   // 대상 하나당 한 번만 (재진입해도 다시 안 맞음)
        Periodic, // 안에 머무는 동안 tickInterval 간격으로 계속
    }

    [Header("타격 방식")]
    [Tooltip("Single = 대상당 한 번만 데미지 / Periodic = 안에 있는 동안 tickInterval마다 계속 데미지")]
    public HitMode hitMode = HitMode.Single;
    [Tooltip("Periodic일 때, 같은 대상을 다시 때리기까지의 간격(초). 진입 즉시 1회 타격 후 이 간격으로 반복된다.")]
    public float tickInterval = 1f;

    public int damage = 10;
    public float lifetime = 0.15f;
    public int direction;

    [Header("소유자 레이어 (이 레이어는 데미지 제외)")]
    public string ownerLayer = "Player";

    [Header("넉백")]
    public float knockbackX = 5f;
    public float knockbackY = 3f;

    [Header("치명타")]
    public Stats attackerStats;
    [Tooltip("치명타로 명중했을 때 재생할 이펙트 (비워두면 재생 안 함). 1회성 파티클이라 재생 후 자동으로 파괴된다.")]
    public GameObject criticalHitEffectPrefab;
    [Tooltip("criticalHitEffectPrefab을 재생한 뒤 자동 파괴까지 대기 시간(초)")]
    public float criticalHitEffectLifetime = 1f;

    [Header("피격 사운드")]
    public AudioClip hitSfx;
    public AudioClip criticalSfx;

    [Header("카메라 쉐이크")]
    [Tooltip("데미지 1당 흔들림 증가량")]
    public float shakePerDamage = 0.01f;
    [Tooltip("최소 흔들림")]
    public float shakeMin = 0.05f;
    [Tooltip("일반 최대 흔들림")]
    public float shakeMax = 0.3f;
    [Tooltip("치명타 최대 흔들림")]
    public float shakeMaxCrit = 0.5f;
    [Tooltip("일반 흔들림 지속시간")]
    public float shakeDuration = 0.15f;
    [Tooltip("치명타 흔들림 지속시간")]
    public float shakeDurationCrit = 0.25f;

    [Header("강한 타격 (차지 강타 등, 선택)")]
    [Tooltip("true면 위 shakeMax/shakeMaxCrit 대신 아래 Heavy 값을 사용")]
    public bool heavyImpact = false;
    public float shakeMaxHeavy = 0.6f;
    public float shakeDurationHeavy = 0.3f;

    HashSet<Health> hit = new HashSet<Health>();                         // Single 모드: 이미 맞은 대상
    Dictionary<Health, float> nextTickTime = new Dictionary<Health, float>(); // Periodic 모드: 대상별 다음 타격 가능 시각
    bool gaugeCharged = false; // 이 히트박스당 1번만 충전 (모드 무관 공통, 틱마다 충전되지 않도록)

    float age = 0f;

    // [2026-10-10 수정] 수명을 Destroy 예약 대신 직접 센다 — Q 퍼즈 중에는 적이 만든 판정의 수명이 흐르지 않고
    // 판정도 꺼진다 (예전엔 보스 차징 판정(3초)이 퍼즈 중에도 살아 있어 멈춘 보스 앞에서 맞았음)
    void Update()
    {
        if (IsFrozenByPause()) return;
        age += Time.deltaTime;
        if (age >= lifetime) Destroy(gameObject);
    }

    // 적이 만든 판정이고 Q 퍼즈 중인지 (플레이어 판정은 퍼즈 중에도 정상 작동)
    bool IsFrozenByPause()
    {
        return ownerLayer != "Player" && PauseManager.Instance != null && PauseManager.Instance.IsPaused;
    }

    void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        // 플레이어 히트박스 = 파랑 / 적 히트박스 = 빨강
        Gizmos.color = ownerLayer == "Player"
            ? new Color(0f, 0.5f, 1f, 0.4f)
            : new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        // 테두리
        Gizmos.color = ownerLayer == "Player"
            ? new Color(0f, 0.5f, 1f, 1f)
            : new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // Single은 이미 Enter 시점에 판정이 끝나므로 Stay에서는 아무것도 하지 않는다.
        // Periodic만 머무는 동안 계속 판정한다 (실제 타격 여부는 TryHit 안의 tick 간격 체크가 결정).
        if (hitMode != HitMode.Periodic) return;
        TryHit(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (hitMode != HitMode.Periodic) return;
        Health target = ResolveHealth(other);
        if (target != null) nextTickTime.Remove(target); // 벗어나면 타이머 리셋 — 재진입하면 즉시 다시 1회 타격
    }

    Health ResolveHealth(Collider2D other)
    {
        Health target = other.GetComponent<Health>();
        if (target == null)
            target = other.GetComponentInParent<Health>();
        return target;
    }

    // Single/Periodic 공통 진입점 — "지금 이 대상을 때려도 되는가"만 모드별로 판단하고, 실제 처리는 ApplyHit로 위임
    void TryHit(Collider2D other)
    {
        // 다른 히트박스는 맞는 대상이 아님 (판정 상자는 때리기만 하고 맞지 않는다).
        // 적에게 붙어 다니는 판정(hitboxFollowAttacker)이 부모의 Health로 이어져
        // 다른 적·플레이어 공격에 대신 맞던 문제 방지
        if (IsHitboxCollider(other)) return;
        if (IsFrozenByPause()) return;

        // 소유자 레이어 제외
        if (!string.IsNullOrEmpty(ownerLayer) &&
            other.gameObject.layer == LayerMask.NameToLayer(ownerLayer))
        {
            return;
        }

        Health target = ResolveHealth(other);
        if (target == null) return;

        // 자기 자신 제외 (레이어와 상관없이) — 플레이어가 플랫폼을 뚫고 내려가는 동안 레이어가
        // "dropping" 으로 잠깐 바뀌면 위의 레이어 검사를 통과해서 자기 공격에 맞던 문제 방지
        if (attackerStats != null)
        {
            Stats targetOwner = target.GetComponent<Stats>() ?? target.GetComponentInParent<Stats>();
            if (targetOwner == attackerStats) return;
        }
        if (ownerLayer == "Player" && target.CompareTag("Player")) return;
        // 같은 편 제외 — 적이 만든 판정은 다른 적·보스에게 피해를 주지 않는다
        if (ownerLayer == "Enemy" && IsEnemySide(target.gameObject)) return;

        if (hitMode == HitMode.Single)
        {
            if (hit.Contains(target)) return;
            hit.Add(target);
        }
        else // Periodic
        {
            if (nextTickTime.TryGetValue(target, out float readyTime) && Time.time < readyTime)
                return;
            nextTickTime[target] = Time.time + tickInterval;
        }

        ApplyHit(target, other);
    }

    /// <summary>
    /// [2026-10-10 추가] 히트박스 없이 바로 때리는 공격(총 레이저 등)용 공통 피해 처리.
    /// 피해 계산·치명타(퍼즈 중 보너스 포함)·상성·화면 흔들림·피격음·데미지 숫자를 근접 판정과 같은 규칙으로 처리한다.
    /// 퍼즈 게이지는 여기서 충전하지 않는다(부르는 쪽이 공격 1회당 한 번 충전). 실제로 맞혔으면 true.
    /// </summary>
    public static bool DealDirectHit(Health target, int rawDamage, Stats attacker, int direction,
                                     float knockbackX, float knockbackY, Vector3 hitPos,
                                     AudioClip hitSfx, AudioClip criticalSfx, string logTag, bool byPlayer)
    {
        if (target == null || !target.CanBeHit()) return false;

        Stats targetStats = target.GetComponent<Stats>() ?? target.GetComponentInParent<Stats>();
        int baseDamage = Stats.CalculateEffectiveDamage(rawDamage, attacker, targetStats);

        bool isCrit = false;
        int finalDamage = baseDamage;
        if (attacker != null)
        {
            float critChance = attacker.critChance;
            if (byPlayer && PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                critChance = Mathf.Max(critChance, PauseManager.Instance.pauseCritChance);
            isCrit = Random.value < critChance;
            if (isCrit) finalDamage = Mathf.RoundToInt(baseDamage * attacker.critMultiplier);
        }
        finalDamage = Stats.ApplyAdvantageMultiplierWithLog(finalDamage, attacker, targetStats, logTag, target.name);

        // 화면 흔들림 (근접 판정 기본값과 같은 기준)
        float magnitude = Mathf.Clamp(finalDamage * 0.01f, 0.05f, isCrit ? 0.5f : 0.3f);
        CameraFollow.Shake(magnitude, isCrit ? 0.25f : 0.15f);

        AudioClip clip = isCrit && criticalSfx != null ? criticalSfx : hitSfx;
        if (clip != null) AudioManager.PlaySFX(clip, hitPos);

        target.TakeDamage(finalDamage, direction, knockbackX, knockbackY);
        FloatingTextSpawner.SpawnDamage(hitPos, finalDamage, isCrit);
        return true;
    }

    /// <summary>콜라이더가 히트박스(공격 판정) 자체인지 — Hitbox/ProjectileBase/TrapTile 공용</summary>
    public static bool IsHitboxCollider(Collider2D col)
    {
        return col != null && col.GetComponent<Hitbox>() != null;
    }

    /// <summary>적 편(일반 적 레이어 Enemy, 보스 몸체 레이어 BossBody)인지</summary>
    public static bool IsEnemySide(GameObject go)
    {
        int layer = go.layer;
        return layer == LayerMask.NameToLayer("Enemy") || layer == LayerMask.NameToLayer("BossBody");
    }

    // 실제 피해 계산·연출·상태 반영. Single/Periodic 어느 쪽에서 왔든 동일하게 처리한다.
    void ApplyHit(Health target, Collider2D other)
    {
        // [2026-10-10 추가] 대시·피격 무적·이미 사망으로 피해가 안 들어가는 대상에는 연출(숫자·흔들림·소리·게이지)도 내지 않는다
        if (!target.CanBeHit()) return;

        // 피해 계산식 (기획서 기준) — 공통 공식은 Stats.CalculateEffectiveDamage() 참고
        // 유효방어 = 방어력 - (집중력/100)
        // 기본피해 = 공격력 - 유효방어
        // 실제피해 = 기본피해 × 속성배율 (현재 1.0)
        Stats targetStats = target.GetComponent<Stats>() ?? target.GetComponentInParent<Stats>();
        int baseDamage = Stats.CalculateEffectiveDamage(damage, attackerStats, targetStats);

        // 치명타 판정
        bool isCrit = false;
        int finalDamage = baseDamage;
        if (attackerStats != null)
        {
            float critChance = attackerStats.critChance;
            // 퍼즈 중 플레이어 공격은 치명타 확률을 최소 PauseManager.pauseCritChance (기본 20%) 로 올림
            if (ownerLayer == "Player" && PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                critChance = Mathf.Max(critChance, PauseManager.Instance.pauseCritChance);
            isCrit = Random.value < critChance;
            if (isCrit)
                finalDamage = Mathf.RoundToInt(baseDamage * attackerStats.critMultiplier);
        }

        // 속성 배율 (공격자 ↔ 대상 상성) — ProjectileBase.CalcDamage()와 동일 로직/로그라
        // Stats.ApplyAdvantageMultiplierWithLog()로 공유한다.
        finalDamage = Stats.ApplyAdvantageMultiplierWithLog(finalDamage, attackerStats, targetStats, "Hitbox", target.name);

        // 카메라 쉐이크
        float shakeCap = heavyImpact ? shakeMaxHeavy : (isCrit ? shakeMaxCrit : shakeMax);
        float shakeTime = heavyImpact ? shakeDurationHeavy : (isCrit ? shakeDurationCrit : shakeDuration);
        float magnitude = Mathf.Clamp(finalDamage * shakePerDamage, shakeMin, shakeCap);
        CameraFollow.Shake(magnitude, shakeTime);

        // 사운드
        AudioClip clip = isCrit && criticalSfx != null ? criticalSfx : hitSfx;
        if (clip != null)
            AudioManager.PlaySFX(clip, other.transform.position);

        // 치명타 이펙트 (1회성 파티클, 재생 후 자동 파괴)
        if (isCrit && criticalHitEffectPrefab != null)
        {
            GameObject fx = Instantiate(criticalHitEffectPrefab, other.transform.position, Quaternion.identity);
            Destroy(fx, criticalHitEffectLifetime);
        }

        // 퍼즈 게이지 충전 (히트박스 1개당 1번만 — Periodic이어도 틱마다 충전되지 않음)
        if (!gaugeCharged && ownerLayer == "Player" && attackerStats != null)
        {
            gaugeCharged = true;
            PlayerController pc = attackerStats.GetComponent<PlayerController>();
            float gauge = pc?.EquippedWeapon != null ? pc.EquippedWeapon.pauseGaugePerHit : 2.5f;
            PauseManager.Instance?.AddGauge(gauge);

            // 건틀릿 공격 속도 스택 (피격 성공 시)
            if (pc != null) pc.OnGauntletHit();
        }

        target.TakeDamage(finalDamage, direction, knockbackX, knockbackY);

        // 데미지 숫자 팝업 (플로팅 텍스트)
        FloatingTextSpawner.SpawnDamage(other.transform.position, finalDamage, isCrit);

    }
}
