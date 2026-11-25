using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 전투 알림 모음 — 증강 능력이 전투 사건을 듣고, 피해 계산에 끼어드는 곳.
/// 기존 스크립트(Hitbox · ProjectileBase · Health · PlayerController · PauseManager · Room · BossController ·
/// Enemy · CharacterSkills · Skill)가 한 줄씩 여기를 부른다. 듣는 쪽은 AugmentRuntime 하나.
/// </summary>
public static class CombatEvents
{
    public struct HitInfo
    {
        public Health target;
        public Stats targetStats;
        public int damage;        // 실제로 들어간 피해
        public bool isCrit;
        public Vector3 position;
        public bool fromAbility;  // 증강 능력이 낸 피해 (연쇄 방지용)
        public bool isProjectile;
        public SwingTag swing;    // 평타 판정이면 그 동작 정보 (아니면 null)
        public bool fromSkill;    // 스킬이 낸 판정
        public GameObject source; // 판정 오브젝트 (없을 수 있음)
    }

    // ── 사건 ──
    public static event Action<HitInfo> PlayerHit;              // 플레이어 공격이 적을 맞힘
    public static event Action<Health, Vector3> EnemyKilled;    // 적·보스 처치
    public static event Action<Vector3, float> DashStarted;     // 출발 위치, 방향
    public static event Action<Vector3> DashEnded;              // 끝난 위치
    public static event Action PauseStarted;
    public static event Action PauseEnded;
    public static event Action<Room> RoomCleared;
    public static event Action<int> PlayerDamaged;              // 받은 피해 (체력에 들어간 양)
    public static event Action<SwingTag> SwingStarted;          // 평타 판정이 막 생김 (크기를 바꿀 수 있음)
    public static event Action<Vector3> PlayerLanded;           // 착지
    public static event Action<Enemy> EnemyAttackStarted;       // 일반 적이 공격을 시작
    public static event Action<float> HealOrbTaken;             // [2026-11-25] 회복 구 획득 (먹기 전 체력 비율 0~1)

    /// <summary>지금 증강 능력이 피해를 내는 중인지 (그 피해로 다시 능력이 터지는 연쇄를 막음)</summary>
    public static bool AbilityDamageInProgress;
    /// <summary>Hitbox.DealDirectHit 로 때릴 때 판정 오브젝트 대신 넘길 것 (없으면 null)</summary>
    public static GameObject DirectHitSource;

    // ── 피해 계산 끼어들기 (AugmentRuntime 이 채움) ──
    public static Func<Health, int, bool, int> ModifyOutgoing;   // 플레이어가 주는 피해
    public static Func<int, int> ModifyIncoming;                 // 플레이어가 받는 피해
    public static Func<Health, int, int> ModifyEnemyIncoming;    // 적이 체력에서 잃을 피해 (사건의 지평선)
    public static Func<Health, float> CritBonus;                 // 대상에 대한 치명타 확률 추가
    public static Func<bool> ConsumeForcedCrit;                  // 다음 공격 치명타 확정 하나 쓰기
    public static Func<bool> FreeCastAvailable;                  // 재사용 대기 없이 스킬을 쓸 수 있는지 (확장 소켓)
    public static Func<int, bool, float> OnSkillCast;            // 스킬 시전 (칸, 공짜인지) → 이 시전의 피해 배율
    public static Func<Vector3, float, float> DashSpeedHook;     // 대시 속도 배율 (원거리 관측)

    public static float PauseGaugeMultiplier = 1f;
    public static float PauseDurationBonus = 0f;
    public static float PauseCritBonus = 0f;
    public static float AttackSpeedMultiplier = 1f;
    public static float PlayerProjectileScale = 1f;
    public static float DashExtraInvuln = 0f;
    /// <summary>증강 파편 드롭 확률 배율 (보급 우선권)</summary>
    public static float ShardDropMultiplier = 1f;
    /// <summary>[2026-11-25] 회복 구 회복량 배율 (과부하 사이버웨어가 올림, 기본 1)</summary>
    public static float HealOrbMultiplier = 1f;
    /// <summary>평타 판정 · 무기 궤적 크기 배율 (조준 고정)</summary>
    public static float SwingScale = 1f;

    // [2026-11-24] 조준 고정: 이번 평타 판정이 커지기 전 · 후의 월드 범위. 같은 프레임에 나오는 무기 궤적 이펙트가
    //  이 차이만큼만(앞으로 늘어난 길이 · 위아래로 늘어난 높이) 늘어나도록 PlayerController 가 읽어 감
    public static int SwingFitFrame = -1;
    public static Rect SwingFitFrom, SwingFitTo;
    public static int SwingFitFacing = 1;
    public static bool HasSwingFit => SwingFitFrame == Time.frameCount;
    public static void SetSwingFit(Rect from, Rect to, int facing)
    {
        SwingFitFrame = Time.frameCount; SwingFitFrom = from; SwingFitTo = to; SwingFitFacing = facing >= 0 ? 1 : -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PlayerHit = null; EnemyKilled = null; DashStarted = null; DashEnded = null; HealOrbTaken = null;
        PauseStarted = null; PauseEnded = null; RoomCleared = null; PlayerDamaged = null;
        SwingStarted = null; PlayerLanded = null; EnemyAttackStarted = null;
        ModifyOutgoing = null; ModifyIncoming = null; ModifyEnemyIncoming = null; CritBonus = null; ConsumeForcedCrit = null;
        FreeCastAvailable = null; OnSkillCast = null; DashSpeedHook = null;
        ResetModifiers();
        AbilityDamageInProgress = false;
        DirectHitSource = null;
    }

    public static void ResetModifiers()
    {
        PauseGaugeMultiplier = 1f; PauseDurationBonus = 0f; PauseCritBonus = 0f;
        AttackSpeedMultiplier = 1f; PlayerProjectileScale = 1f; DashExtraInvuln = 0f; ShardDropMultiplier = 1f; HealOrbMultiplier = 1f; SwingScale = 1f;
        SwingFitFrame = -1;
    }

    // ── 부르는 쪽 도우미 ──
    public static int Outgoing(Health target, int damage, bool isCrit)
        => ModifyOutgoing != null && target != null ? Mathf.Max(1, Safe(() => ModifyOutgoing(target, damage, isCrit), damage)) : damage;

    public static int Incoming(int damage) => ModifyIncoming != null ? Mathf.Max(0, Safe(() => ModifyIncoming(damage), damage)) : damage;

    public static int EnemyIncoming(Health h, int damage) => ModifyEnemyIncoming != null && h != null ? Mathf.Max(0, Safe(() => ModifyEnemyIncoming(h, damage), damage)) : damage;

    public static float ExtraCrit(Health target) => CritBonus != null && target != null ? Safe(() => CritBonus(target), 0f) : 0f;

    public static bool TakeForcedCrit() => ConsumeForcedCrit != null && Safe(() => ConsumeForcedCrit(), false);

    public static bool CanCastFree() => FreeCastAvailable != null && Safe(() => FreeCastAvailable(), false);

    public static float SkillCast(int slot, bool free) => OnSkillCast != null ? Mathf.Max(0f, Safe(() => OnSkillCast(slot, free), 1f)) : 1f;

    public static float DashSpeed(Vector3 pos, float dir) => DashSpeedHook != null ? Mathf.Max(0.1f, Safe(() => DashSpeedHook(pos, dir), 1f)) : 1f;

    // [2026-11-21] 증강 쪽 오류가 전투 코드(스킬 코루틴·사망 처리 등)를 멈추지 않게 — 오류는 콘솔에만 남김
    static void Safe(Action a) { try { a(); } catch (Exception e) { Debug.LogException(e); } }
    static T Safe<T>(Func<T> f, T fallback) { try { return f(); } catch (Exception e) { Debug.LogException(e); return fallback; } }

    public static void RaiseHit(Health target, int damage, bool isCrit, Vector3 pos, bool projectile = false, GameObject source = null)
    {
        if (PlayerHit == null || target == null) return;
        var st = target.GetComponent<Stats>() ?? target.GetComponentInParent<Stats>();
        SwingTag swing = source != null ? source.GetComponentInParent<SwingTag>() : null;
        bool skill = source != null && source.GetComponentInParent<SkillMark>() != null;
        var info = new HitInfo
        {
            target = target, targetStats = st, damage = damage, isCrit = isCrit, position = pos,
            fromAbility = AbilityDamageInProgress, isProjectile = projectile, swing = swing, fromSkill = skill, source = source
        };
        Safe(() => PlayerHit(info));
    }

    public static void RaiseKilled(Health h, Vector3 pos) { if (EnemyKilled != null) Safe(() => EnemyKilled(h, pos)); }
    public static void RaiseHealOrbTaken(float hpRatioBefore) { if (HealOrbTaken != null) Safe(() => HealOrbTaken(hpRatioBefore)); }
    public static void RaiseDashStart(Vector3 pos, float dir) { if (DashStarted != null) Safe(() => DashStarted(pos, dir)); }
    public static void RaiseDashEnd(Vector3 pos) { if (DashEnded != null) Safe(() => DashEnded(pos)); }
    public static void RaisePauseStart() { if (PauseStarted != null) Safe(() => PauseStarted()); }
    public static void RaisePauseEnd() { if (PauseEnded != null) Safe(() => PauseEnded()); }
    public static void RaiseRoomCleared(Room r) { if (RoomCleared != null) Safe(() => RoomCleared(r)); }
    public static void RaisePlayerDamaged(int dmg) { if (dmg > 0 && PlayerDamaged != null) Safe(() => PlayerDamaged(dmg)); }
    public static void RaiseSwing(SwingTag t) { if (t != null && SwingStarted != null) Safe(() => SwingStarted(t)); }
    public static void RaiseLanded(Vector3 pos) { if (PlayerLanded != null) Safe(() => PlayerLanded(pos)); }
    public static void RaiseEnemyAttack(Enemy e) { if (e != null && EnemyAttackStarted != null) Safe(() => EnemyAttackStarted(e)); }
}

/// <summary>[2026-11-21] 플레이어 평타 판정에 붙는 동작 정보 (PlayerController.ExecuteHitbox 가 붙임)</summary>
public class SwingTag : MonoBehaviour
{
    public Weapontype weapon;
    public int combo;            // 1~3 (망치 차지 강타는 3)
    public bool charged;         // 망치 차지 강타
    public float chargePower;    // 차지 정도 0~1
    public bool airborne;        // 공중에서 휘두름
    public int facing = 1;
    [NonSerialized] public float startTime;   // 판정이 생긴 시각 (이번 공격 중에 얻은 데이터 구분용)
    void Awake() { startTime = Time.time; }
    [NonSerialized] public bool handled;   // 증강이 이 판정에 한 번만 반응하도록
    [NonSerialized] public readonly HashSet<string> once = new HashSet<string>();
}

/// <summary>[2026-11-21] 스킬이 낸 판정·투사체 표시 (Skill.SpawnAt 이 붙임)</summary>
public class SkillMark : MonoBehaviour
{
    [NonSerialized] public bool reading;   // 백자 열람 권한 '열람' 시전
}
