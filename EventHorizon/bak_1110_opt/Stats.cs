using UnityEngine;
using System;
using System.Collections.Generic;

public class Stats : MonoBehaviour
{
    [Header("기본 스탯")]
    public int maxHp = 100;
    public int currentHp;

    public float Atk = 10f;  // 공격력 / 퍼즈 오브젝트 투척거리
    public float Def = 5f;   // 방어력
    public float spd = 5f;   // 이동속도

    [System.NonSerialized] public float itemSpdBonus;   // 아이템 효과로 현재 누적된 spd 증가분 합계 (StatModifier/FadeEffect가 갱신). 수동으로 spd를 바꿔도 영향받지 않음

    // 장판(기름/물 등) 등 외부 요인이 이동속도에 곱하는 배율들 — 발생원(예: 장판 인스턴스) 별로 등록/해제.
    // 여러 장판에 동시에 걸쳐 있으면 가장 강한(가장 작은) 배율이 적용된다.
    readonly Dictionary<object, float> speedModifiers = new Dictionary<object, float>();

    /// <summary>source(보통 장판 컴포넌트 자신)를 키로 이동속도 배율을 등록/갱신한다.</summary>
    public void AddSpeedModifier(object source, float multiplier)
    {
        if (source == null) return;
        speedModifiers[source] = multiplier;
    }

    /// <summary>source가 등록한 이동속도 배율을 해제한다 (장판을 벗어나거나 장판이 사라질 때).</summary>
    public void RemoveSpeedModifier(object source)
    {
        if (source == null) return;
        speedModifiers.Remove(source);
    }

    /// <summary>현재 적용 중인 모든 이동속도 배율 중 가장 강한(가장 작은) 값. 아무것도 없으면 1(정상 속도).</summary>
    public float GetSpeedMultiplier()
    {
        float m = 1f;
        foreach (float v in speedModifiers.Values)
            if (v < m) m = v;
        return m;
    }

    /// <summary>Collider2D로부터 Stats를 찾는다 — 콜라이더가 자식 오브젝트에 있는 경우까지 대비해 부모도 확인.</summary>
    public static Stats Resolve(Collider2D col)
    {
        Stats s = col.GetComponent<Stats>();
        if (s == null) s = col.GetComponentInParent<Stats>();
        return s;
    }

    [Tooltip("아이템으로 늘어난 spd 증가분(itemSpdBonus)에 실제로 얼마나 반영할지 (1 = 100% 반영, 0.5 = 절반만). 기본 spd는 항상 100% 반영됨. UI 표시와 이동속도 계산 모두 GetEffectiveSpd()를 통해 이 비율이 적용됨.")]
    [Range(0f, 1f)]
    public float itemSpeedBonusRatio = 0.5f;
    public float Focus = 5f;   // 집중력 (퍼즈 지속시간 / 배치 오브젝트 수)

    [Header("치명타")]
    [Range(0f, 1f)]
    public float critChance = 0.1f;  // 치명타 확률 (0.1 = 10%)
    public float critMultiplier = 2f;    // 치명타 배율

    [Header("상성 (공격타입 → 스피드 → 방어 → 공격타입 순으로 우위)")]
    public NowAdvantage advantage;

    [Header("힐 이펙트 (선택 — 캐릭터를 따라다님)")]
    public GameObject healEffectPrefab;

    public event Action<int, int> OnHpChanged;

    void Awake()
    {
        currentHp = maxHp;
    }

    void Start()
    {
        NotifyHp();
    }

    public void TakeDamage(int damage)
    {
        currentHp = Mathf.Max(0, currentHp - damage);
        NotifyHp();
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        NotifyHp();
    }

    /// <summary>
    /// 회복 + 힐 이펙트 출력.
    /// 힐 오브젝트, 보조 힐 아이템 등 "즉시 먹는 회복"에만 사용.
    /// 자동 회복(HpRegen)이나 스탯 보너스에는 사용하지 말 것.
    /// </summary>
    public void HealWithEffect(int amount)
    {
        Heal(amount);

        // 힐 이펙트 (캐릭터 자식으로 붙여서 따라다니게)
        if (healEffectPrefab != null)
        {
            GameObject fx = Instantiate(healEffectPrefab, transform.position, Quaternion.identity);
            fx.transform.SetParent(transform);
        }
    }

    /// <summary>
    /// 최대 체력 증가 (currentHp 도 함께 증가)
    /// </summary>
    public void IncreaseMaxHp(int amount)
    {
        maxHp = Mathf.Max(1, maxHp + amount);
        currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        NotifyHp();
    }

    /// <summary>
    /// 최대 체력 감소 (currentHp 가 maxHp 초과 시 맞춤)
    /// </summary>
    public void DecreaseMaxHp(int amount)
    {
        maxHp = Mathf.Max(1, maxHp - amount);
        currentHp = Mathf.Min(currentHp, maxHp);
        NotifyHp();
    }

    void NotifyHp()
    {
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// 공통 피해 계산식 (기획서 기준)
    /// 유효방어 = 방어력 - (공격자 집중력/100), 기본피해 = 원본데미지 - 유효방어 (최소 1)
    /// requireBothStats = true 이면 attacker/target 중 하나라도 없을 때 방어 계산을 건너뛰고 원본데미지를 그대로 반환(최소 1)
    /// requireBothStats = false(기본) 이면 없는 쪽은 0으로 취급하고 계산 진행
    /// </summary>
    public static int CalculateEffectiveDamage(float rawDamage, Stats attacker, Stats target, bool requireBothStats = false)
    {
        if (requireBothStats && (attacker == null || target == null))
            return Mathf.Max(1, Mathf.RoundToInt(rawDamage));

        float targetDef = target != null ? target.Def : 0f;
        float attackerFocus = attacker != null ? attacker.Focus : 0f;
        float effectiveDef = Mathf.Max(0f, targetDef - (attackerFocus / 100f));
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage - effectiveDef));
    }

    /// <summary>
    /// 실제로 이동에 반영/표시되어야 하는 spd. 기본 spd는 100% 그대로, 아이템으로 늘어난 증가분(itemSpdBonus)만 itemSpeedBonusRatio 비율로 감쇠해서 반환.
    /// 이동속도 계산과 UI 표시 모두 반드시 이 값을 써야 함 (spd 원본을 직접 쓰면 UI/실제 이동속도가 서로 달라짐).
    /// </summary>
    public float GetEffectiveSpd()
    {
        return (spd - itemSpdBonus * (1f - itemSpeedBonusRatio)) * GetSpeedMultiplier();
    }

    /// <summary>
    /// 상성 배율. 공격자 타입이 대상 타입보다 우위면 1.2배, 열위면 0.8배, 동타입/무속성(None)이면 1배.
    /// 우위 순서: 공격타입(Power) → 스피드(Speed) → 방어(Defense) → 공격타입(Power)
    /// </summary>
    public static float GetAdvantageMultiplier(NowAdvantage attackerType, NowAdvantage defenderType)
    {
        if (attackerType == NowAdvantage.None || defenderType == NowAdvantage.None || attackerType == defenderType)
            return 1f;

        bool attackerWins = (attackerType == NowAdvantage.Power && defenderType == NowAdvantage.Speed)
                          || (attackerType == NowAdvantage.Speed && defenderType == NowAdvantage.Defense)
                          || (attackerType == NowAdvantage.Defense && defenderType == NowAdvantage.Power);

        return attackerWins ? 1.2f : 0.8f;
    }

    // [2026-09-29 추가] Hitbox.cs(근접)와 ProjectileBase.cs(투사체)가 "상성 배율 곱해서 최종
    // 데미지 계산 + 콘솔 로그 출력"을 토씨 하나 안 틀리고 거의 그대로 중복 구현하고 있었다.
    // 두 곳 다 이 메서드 하나로 대체. logTag는 로그 앞에 붙는 "[Hitbox]"/"[Projectile]" 같은 이름표.
    public static int ApplyAdvantageMultiplierWithLog(int baseDamage, Stats attackerStats, Stats targetStats, string logTag, string targetName)
    {
        float attributeMultiplier = GetAdvantageMultiplier(
            attackerStats != null ? attackerStats.advantage : NowAdvantage.None,
            targetStats != null ? targetStats.advantage : NowAdvantage.None);
        int finalDamage = Mathf.RoundToInt(baseDamage * attributeMultiplier);

        Debug.Log($"[{logTag}] {(attackerStats != null ? attackerStats.name : "Unknown")}" +
            $"({(attackerStats != null ? attackerStats.advantage : NowAdvantage.None)}) → " +
            $"{targetName}({(targetStats != null ? targetStats.advantage : NowAdvantage.None)}) | " +
            $"상성배율 x{attributeMultiplier} | 최종 데미지 {finalDamage}");

        return finalDamage;
    }
}