using UnityEngine;

[System.Serializable]
public class EffectPiece
{
    [Header("이펙트 프리펩")]
    public GameObject prefab;

    [Header("위치 오프셋 (플레이어 기준, 좌우 자동 반전)")]
    public Vector3 posOffset = Vector3.zero;

    [Header("회전 (Z축, 도 단위)")]
    public float rotationZ = 0f;

    [Header("좌우 반전 (기본으로 뒤집기)")]
    public bool flipX = false;

    [Header("삭제 시간 (0=자동)")]
    public float lifetime = 0f;
}

/// <summary>
/// 효과음 목록. 1개만 넣으면 그 소리가, 여러 개 넣으면 그중 하나가 랜덤으로 재생된다.
/// 후보가 2개 이상이면 방금 나온 소리는 바로 다시 나오지 않는다.
/// </summary>
[System.Serializable]
public class RandomSfx
{
    [Tooltip("재생할 소리들. 여러 개 넣으면 그중 하나가 랜덤 재생")]
    public AudioClip[] clips;

    [System.NonSerialized] int lastIndex = -1;

    /// <summary>목록 중 하나를 랜덤으로 돌려준다. 비어 있으면 null.</summary>
    public AudioClip Pick()
    {
        if (clips == null || clips.Length == 0) return null;

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) count++;
        }
        if (count == 0) return null;

        // 직전 소리는 후보에서 뺀다 (후보가 2개 이상일 때만)
        bool skipLast = count >= 2 && lastIndex >= 0 && lastIndex < clips.Length && clips[lastIndex] != null;
        int pick = Random.Range(0, skipLast ? count - 1 : count);

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (skipLast && i == lastIndex) continue;
            if (pick == 0)
            {
                lastIndex = i;
                return clips[i];
            }
            pick--;
        }
        return null;
    }
}

[System.Serializable]
public class ComboEffect
{
    [Header("효과음 (여러 개 넣으면 그중 하나가 랜덤 재생)")]
    public RandomSfx sfx = new RandomSfx();

    public AudioClip PickSfx()
    {
        return sfx != null ? sfx.Pick() : null;
    }

    [Header("이펙트 조각들 (여러 개 등록 가능)")]
    public EffectPiece[] pieces;
}

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("기본")]
    public Weapontype weapontype;
    public NowAdvantage advantage;
    public AnimatorOverrideController animatorOverride;

    [Header("스탯 보너스 (장착 시 적용)")]
    public StatModifier statBonus;

    [Header("전투")]
    public GameObject hitboxPrefab;
    [Range(0.1f, 5f)]
    public float damageMultiplier = 1f;

    [Header("퍼즈 게이지")]
    public float pauseGaugePerHit = 2.5f; // 기본 2.5% (40타 = 100%)

    [Header("사운드 (여러 개 넣으면 그중 하나가 랜덤 재생)")]
    public RandomSfx swingSfx = new RandomSfx();
    public RandomSfx hitSfx = new RandomSfx();
    public RandomSfx criticalSfx = new RandomSfx();

    [Header("콤보별 이펙트·효과음 (1·2·3타)")]
    public ComboEffect[] comboEffects = new ComboEffect[3];

    [Header("건틀릿 전용 (공격속도 스택)")]
    [Tooltip("피격 성공 1회당 속도 증가량")]
    public float stackSpeedPerHit = 0.15f;
    [Tooltip("최대 공격 속도 배율")]
    public float maxStackSpeed = 3f;
    [Tooltip("이 시간(초) 동안 공격 안 하면 스택 초기화")]
    public float stackResetTime = 1.5f;

    [Header("총 전용")]
    public float gunRange = 20f; // 레이저 사거리
    public GameObject gunBeamPrefab;      // 빔 스프라이트 프리펩
    public float gunBeamDuration = 0.1f;  // 빔 표시 시간
    [Tooltip("빔 각도 랜덤 흔들림 (±도)")]
    public float gunBeamAngleJitter = 0f;
    [Tooltip("콤보별 빔 시작 위치 오프셋 (인덱스 = 콤보 타수-1, 좌우 자동 반전)")]
    public Vector2[] gunBeamMuzzleOffsets;

    [Header("망치 전용 (차지 강타)")]
    [Tooltip("이 시간(초) 이상 눌러야 차지가 '무장'됨. 그 전에 떼면 그냥 평타(기존 콤보)로 처리됨")]
    public float chargeTime = 0.3f;
    [Tooltip("무장된 후, 최대 위력까지 걸리는 추가 시간(초)")]
    public float maxChargeTime = 2f;
    [Tooltip("차지가 막 무장된 순간의 데미지 배율")]
    public float minChargeMultiplier = 1f;
    [Tooltip("완전히 차지했을 때(chargeTime + maxChargeTime 이상 홀드) 데미지 배율")]
    public float maxChargeMultiplier = 3f;
    [Tooltip("차지 강타 전용 히트박스. 비어있으면 기본 hitboxPrefab 사용")]
    public GameObject chargeHitboxPrefab;
    [Tooltip("차지 중 캐릭터에 붙는 이펙트 (무장되는 순간 생성, 발동/취소 시 제거)")]
    public GameObject chargeEffectPrefab;
    [Tooltip("차지가 무장되는 순간 재생할 사운드 (여러 개 넣으면 랜덤)")]
    public RandomSfx chargeSfx = new RandomSfx();
    [Tooltip("차지 강타 명중 시 넉백 (수평)")]
    public float chargeKnockbackX = 9f;
    [Tooltip("차지 강타 명중 시 넉백 (수직)")]
    public float chargeKnockbackY = 5f;

    // ───────── 랜덤 사운드 고르기 (PlayerController에서 호출) ─────────

    public AudioClip PickSwing()    { return swingSfx != null ? swingSfx.Pick() : null; }
    public AudioClip PickHit()      { return hitSfx != null ? hitSfx.Pick() : null; }
    public AudioClip PickCritical() { return criticalSfx != null ? criticalSfx.Pick() : null; }
    public AudioClip PickCharge()   { return chargeSfx != null ? chargeSfx.Pick() : null; }

    // 런타임 전용 스냅샷
    StatSnapshot appliedSnap;

    public void ApplyStats(Stats stats)
    {
        appliedSnap = statBonus.Apply(stats);
    }

    public void RemoveStats(Stats stats)
    {
        StatModifier.Remove(stats, appliedSnap);
    }
}