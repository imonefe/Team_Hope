using UnityEngine;
using System.Collections;

/// <summary>
/// 스킬 프리팹의 "두뇌" (A방식).
/// 생성 위치·방향·수명을 담당하고, 애니메이션 이벤트로 프리팹(히트박스·투사체·이펙트 등)을
/// 원하는 프레임에 소환한다. 여러 개를 동시에 소환할 수도 있다.
/// 실제 판정·이동은 소환된 프리팹의 컴포넌트(Hitbox, 투사체 스크립트 등)가 처리한다.
///
/// 스킬 프리팹 만드는 법:
/// 1. 빈 오브젝트 생성 → 이 Skill.cs 붙이기
/// 2. skillType / cooldown / spawnOffset / lifetime / icon 설정
/// 3. Animator + 애니메이션 클립 추가
/// 4. spawnEntries 에 소환할 프리팹들을 등록 (히트박스·투사체·이펙트 무엇이든)
/// 5. 애니메이션 클립의 원하는 프레임에 이벤트를 찍어 함수 호출:
///    - SpawnAll()      : 모든 spawnEntries 를 동시에 소환
///    - SpawnGroup(int) : 특정 인덱스의 entry 만 소환 (프레임마다 다르게)
///
/// 소환물에는 시전자 방향이 전달된다:
///  - Hitbox 가 있으면 direction 세팅
///  - flipByFacing 이면 localScale.x 반전 (대부분의 2D 투사체·이펙트가 이 방향으로 이동)
///
/// 여러 발을 뿌리는 연출은 코드 수정 없이 SpawnEntry의 count/pattern으로 조합한다
/// (Fixed / RandomCircle / Fan / Line / Spiral). 새 연출이 필요하면 대부분 프리팹 값 조정으로 해결되고,
/// 패턴 종류 자체가 새로 필요할 때만 코드 수정이 필요하다.
/// </summary>
public class Skill : MonoBehaviour
{
    // 값(0/1)은 기존 프리팹 데이터(Weapon=1)와 호환되도록 고정.
    // CharacterSkills.AcquireSkill()이 이 값을 보고 슬롯을 자동 배정한다:
    // Weapon → 0번 슬롯 고정 / Normal → 1~2번 슬롯에 얻은 순서대로.
    public enum SkillType { Normal = 0, Weapon = 1 }

    // 여러 발을 어떤 모양으로 흩뿌릴지 — 새 연출이 필요할 때 여기 종류를 늘리면
    // 그 다음부터는 모든 스킬 프리팹이 코드 수정 없이 이 패턴을 골라 쓸 수 있다.
    public enum SpawnPattern
    {
        Fixed,        // offset 위치 그대로 (count가 2 이상이면 같은 자리에 겹쳐 나옴 — 보통 count=1)
        RandomCircle, // offset을 중심으로 patternRadius 안에서 매번 랜덤 위치
        Fan,          // offset 방향 기준 부채꼴(patternAngle 범위)로 count개를 균등 분배
        Line,         // offset을 중심으로 lineSpacing 간격으로 count개를 일렬로 배치
        Spiral,       // 회전하며(발마다 patternAngle씩) 반경이 patternRadius까지 늘어나는 나선형
    }

    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("소환할 프리팹 (히트박스 / 투사체 / 애니메이션 / 파티클 등 무엇이든)")]
        public GameObject prefab;
        [Tooltip("소환 위치 오프셋 (스킬 기준, 패턴의 중심점). 방향에 따라 X 자동 반전")]
        public Vector2 offset;

        [Header("소환 개수 / 패턴")]
        [Tooltip("이 프리팹을 몇 개 소환할지")]
        public int count = 1;
        [Tooltip("count가 2개 이상일 때 어떤 모양으로 흩뿌릴지")]
        public SpawnPattern pattern = SpawnPattern.Fixed;
        [Tooltip("RandomCircle: 랜덤 반경 / Fan,Spiral: 중심에서 퍼지는 거리")]
        public float patternRadius = 0f;
        [Tooltip("Fan: 전체 부채꼴 각도(도) / Spiral: 한 발마다 추가로 회전하는 각도(도)")]
        public float patternAngle = 60f;
        [Tooltip("Line 패턴에서 소환물 사이의 간격")]
        public float lineSpacing = 0.5f;
        [Tooltip("패턴으로 계산된 위치에 추가로 더하는 원형 랜덤 지터. 0이면 없음")]
        public float extraRandomRadius = 0f;
        [Tooltip("0이면 count개를 전부 한 프레임에 동시 소환. 0보다 크면 이 간격(초)으로 하나씩 순차 소환(연타 느낌)")]
        public float spawnInterval = 0f;

        [Header("공통 옵션")]
        [Tooltip("시전자 방향에 맞춰 좌우 반전")]
        public bool flipByFacing = true;
        [Tooltip("소환물의 부모를 스킬로 둘지 (false면 월드에 독립 생성 — 투사체 권장)")]
        public bool parentToSkill = false;
        [Tooltip("소환물 자동 소멸까지 시간(초). 0 이하면 자동 소멸 안 함")]
        public float lifetime = 0f;
    }

    [Header("분류")]
    [Tooltip("Weapon = 무기 스킬(획득 시 0번 슬롯 고정) / Normal = 일반 스킬(획득 순서대로 1~2번 슬롯)")]
    public SkillType skillType = SkillType.Weapon;
    public Sprite icon;
    public string skillName;
    [Tooltip("스킬 설명·효과 (인벤토리 UI 표시용)")]
    [TextArea] public string description;

    [Header("발동")]
    [Tooltip("쿨타임(초) — 스킬마다 다름")]
    public float cooldown = 3f;
    [Tooltip("스킬 본체 생성 위치 오프셋 (플레이어 기준, 방향 반영)")]
    public Vector2 spawnOffset = new Vector2(1f, 0f);
    [Tooltip("스킬 오브젝트 자동 소멸까지 시간(초). 0 이하면 자동 소멸 안 함")]
    public float lifetime = 1f;
    [Tooltip("생성 시 시전자 방향에 맞춰 좌우 반전")]
    public bool flipByFacing = true;
    [Tooltip("true면 시전자의 현재 높이(점프 중 등)와 무관하게 바로 아래 바닥에 위치를 맞춘다. " +
             "포탑처럼 항상 바닥에 붙어있어야 하는 스킬에서, 공중에서 시전해도 바닥에 스폰되게 하고 싶을 때 사용")]
    public bool snapToGround = false;
    [Tooltip("바닥 감지 최대 거리(시전자 위치 기준 아래로). 이 거리 안에 ground/platform 레이어가 없으면 스냅하지 않고 기존 방식(캐스터 기준 오프셋)으로 스폰")]
    public float groundCheckDistance = 20f;
    [Tooltip("true면 애니메이션 이벤트 없이 Launch() 시점에 spawnEntries를 즉시 전부 소환한다(SpawnAll()과 동일). " +
             "Animator/애니메이션 이벤트를 아직 안 만든 스킬이나, 프레임 타이밍이 필요 없는 간단한 스킬용")]
    public bool spawnOnLaunch = false;

    [Header("소환물 (애니메이션 이벤트로 소환)")]
    [Tooltip("소환할 프리팹 목록. SpawnAll()로 동시 소환, SpawnGroup(i)로 개별 소환")]
    public SpawnEntry[] spawnEntries;

    [Header("공격 강화 버프 (선택 — 프리팹 소환 대신 시전자의 평타를 일정 시간 강화)")]
    [Tooltip("true면 Launch() 시점에 spawnEntries와 별개로(또는 대신) 시전자의 평타를 buffDuration 동안 강화한다")]
    public bool isAttackBuff = false;
    [Tooltip("버프 지속 시간(초)")]
    public float buffDuration = 5f;
    [Tooltip("평타 데미지 배율 (1 = 변화 없음)")]
    public float buffDamageMultiplier = 1.5f;
    [Tooltip("공격 애니메이션 재생 속도 배율 (1 = 변화 없음)")]
    public float buffAnimSpeedMultiplier = 1.3f;
    [Tooltip("버프 지속 동안 시전자를 따라다니는 오라 이펙트 (선택, 비워두면 표시 안 함)")]
    public GameObject buffAuraPrefab;
    [Tooltip("버프 중 평타가 나갈 때마다 추가로 재생되는 이펙트 (선택, 비워두면 재생 안 함)")]
    public GameObject buffExtraHitEffectPrefab;

    int facing = 1; // 시전 방향 저장 (애니메이션 이벤트 소환 시 사용)
    Transform casterRef; // 시전자 저장 (소환물에 attackerStats 전달용 — 치명타/속성상성 계산에 필요)

    /// <summary>
    /// CharacterSkills 가 프리팹 생성 직후 호출.
    /// caster: 시전 캐릭터, castFacing: 바라보는 방향(1/-1)
    /// </summary>
    public void Launch(Transform caster, int castFacing)
    {
        facing = castFacing;
        casterRef = caster;

        // 위치: 시전자 기준 오프셋 (방향 반영)
        Vector3 off = spawnOffset;
        if (flipByFacing) off.x *= facing;
        Vector3 spawnPos = caster.position + off;

        // 바닥 스냅 — 시전자가 점프 중이어도 캐스터 발밑이 아니라 실제 바닥 표면(y)에 맞춘다.
        // FireOrb.cs의 착지 스냅과 같은 방식(레이캐스트로 실제 바닥 표면 좌표를 구해서 그대로 사용).
        if (snapToGround)
        {
            // 스폰될 X 좌표(좌우 반전 반영된 offset.x 포함) 기준으로 그 아래 바닥을 찾는다 —
            // 캐스터 바로 아래가 아니라 실제로 스킬이 놓일 자리 아래를 봐야 절벽 등에서도 정확함.
            Vector2 rayOrigin = new Vector2(spawnPos.x, caster.position.y);
            RaycastHit2D groundHit = Physics2D.Raycast(rayOrigin, Vector2.down,
                groundCheckDistance, LayerMask.GetMask("ground", "platform"));
            if (groundHit.collider != null)
                spawnPos.y = groundHit.point.y + off.y;
        }

        transform.position = spawnPos;

        // 스킬 본체 좌우 반전
        if (flipByFacing && facing < 0)
        {
            Vector3 s = transform.localScale;
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
        }

        // 스킬 본체 자동 소멸
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);

        // 공격 강화 버프 (spawnEntries와 별개로, 캐스트 즉시 발동)
        if (isAttackBuff)
        {
            PlayerController pc = caster.GetComponent<PlayerController>();
            if (pc != null)
                pc.ApplyAttackBuff(buffDamageMultiplier, buffAnimSpeedMultiplier, buffDuration, buffAuraPrefab, buffExtraHitEffectPrefab);
        }

        // 애니메이션 이벤트가 없는 스킬용 — 즉시 전부 소환
        if (spawnOnLaunch)
            SpawnAll();
    }

    // ── 애니메이션 이벤트: 모든 소환물 동시 소환 ──
    public void SpawnAll()
    {
        if (spawnEntries == null) return;
        foreach (var e in spawnEntries)
            SpawnOne(e);
    }

    // ── 애니메이션 이벤트: 특정 인덱스만 소환 (프레임마다 다르게) ──
    public void SpawnGroup(int index)
    {
        if (spawnEntries == null || index < 0 || index >= spawnEntries.Length) return;
        SpawnOne(spawnEntries[index]);
    }

    // 하위 호환: 기존 애니메이션 이벤트 이름(SpawnHitbox)으로도 전체 소환
    public void SpawnHitbox() { SpawnAll(); }

    // 실제 소환 처리 — count/pattern에 따라 한 entry에서 여러 개를 뿌릴 수 있다
    void SpawnOne(SpawnEntry e)
    {
        if (e == null || e.prefab == null) return;

        int n = Mathf.Max(1, e.count);

        // spawnInterval이 있으면 한 프레임에 몰아서 내지 않고 하나씩 시간차를 두고 순차 소환한다.
        if (e.spawnInterval > 0f && n > 1)
        {
            StartCoroutine(SpawnSequential(e, n));
            return;
        }

        for (int i = 0; i < n; i++)
        {
            Vector3 off = CalcPatternOffset(e, i, n);
            if (e.flipByFacing) off.x *= facing;
            SpawnAt(e, transform.position + off);
        }
    }

    // 순차 소환 — 캐스터/스킬 위치가 소환 도중 바뀌어도(스킬 오브젝트 자체는 안 움직이지만) 그 시점 기준으로 계산
    IEnumerator SpawnSequential(SpawnEntry e, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 off = CalcPatternOffset(e, i, n);
            if (e.flipByFacing) off.x *= facing;
            SpawnAt(e, transform.position + off);
            yield return new WaitForSeconds(e.spawnInterval);
        }
    }

    // 패턴 종류에 따라 offset(스킬 기준, 좌우 반전 적용 전)을 계산
    Vector3 CalcPatternOffset(SpawnEntry e, int index, int count)
    {
        Vector3 off = e.offset;

        switch (e.pattern)
        {
            case SpawnPattern.RandomCircle:
                {
                    Vector2 c = Random.insideUnitCircle * e.patternRadius;
                    off.x += c.x;
                    off.y += c.y;
                    break;
                }
            case SpawnPattern.Fan:
                {
                    // patternAngle 범위 안에 count개를 균등 분배 (count=1이면 정중앙)
                    float t = count > 1 ? (float)index / (count - 1) : 0.5f;
                    float angle = (t - 0.5f) * e.patternAngle;
                    Vector2 dir = RotateVector(Vector2.right, angle) * e.patternRadius;
                    off.x += dir.x;
                    off.y += dir.y;
                    break;
                }
            case SpawnPattern.Line:
                {
                    // 중앙 기준 좌우(상하)로 lineSpacing 간격으로 나란히
                    float t = count > 1 ? index - (count - 1) * 0.5f : 0f;
                    off.y += t * e.lineSpacing;
                    break;
                }
            case SpawnPattern.Spiral:
                {
                    float angle = index * e.patternAngle;
                    float r = count > 1 ? e.patternRadius * (index + 1) / count : e.patternRadius;
                    Vector2 dir = RotateVector(Vector2.right, angle) * r;
                    off.x += dir.x;
                    off.y += dir.y;
                    break;
                }
            case SpawnPattern.Fixed:
            default:
                break;
        }

        // 패턴 결과 위에 추가로 살짝 흔드는 지터 (모든 패턴에 공통 적용 가능)
        if (e.extraRandomRadius > 0f)
        {
            Vector2 jitter = Random.insideUnitCircle * e.extraRandomRadius;
            off.x += jitter.x;
            off.y += jitter.y;
        }

        return off;
    }

    // 2D 벡터를 degrees만큼 회전 (반시계 방향 양수)
    Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // 최종 위치(pos)에 실제 Instantiate + 방향/시전자 스탯 전달
    void SpawnAt(SpawnEntry e, Vector3 pos)
    {
        GameObject go = Instantiate(e.prefab, pos, Quaternion.identity,
                                    e.parentToSkill ? transform : null);

        // 직선 투사체(ProjectileStraight)가 있는지 먼저 확인 —
        // 있으면 아래에서 Init()의 회전이 방향 표현을 전담하므로, 좌우 반전(스케일)은 건너뛴다.
        // 회전 + 스케일 반전을 같이 하면 왼쪽으로 갈 때 두 반전이 겹쳐서 스프라이트가
        // 위아래로 뒤집혀 보이는 문제가 생기기 때문.
        ProjectileStraight[] projectiles = go.GetComponentsInChildren<ProjectileStraight>(true);
        bool isProjectile = projectiles.Length > 0;

        // [2026-10-10 추가] 스킬 본체 자식으로 붙는 경우, 본체가 이미 좌우 반전돼 있으면 자식은 다시 뒤집지 않는다
        // (부모 반전 + 자식 반전이 겹쳐 화면에서는 원래 방향으로 돌아가 그림이 반대를 향하던 문제)
        bool parentFlipped = e.parentToSkill && transform.lossyScale.x < 0f;

        // 좌우 반전 (히트박스·이펙트가 이 방향으로 표시)
        if (e.flipByFacing && facing < 0 && !isProjectile && !parentFlipped)
        {
            Vector3 s = go.transform.localScale;
            s.x = -Mathf.Abs(s.x);
            go.transform.localScale = s;
        }

        // 시전자의 Stats — 치명타/방어 관통/속성상성 계산에 필요해서 소환물에 그대로 전달한다.
        // (이걸 안 넘겨주면 attackerStats가 비어서 치명타·속성보너스가 전부 빠진 채로 데미지가 들어간다)
        Stats casterStats = casterRef != null ? casterRef.GetComponent<Stats>() : null;

        // 히트박스가 있으면 방향 + 시전자 스탯 전달
        // + 스킬 자체 위력(프리팹에 미리 세팅된 damage)에 시전자의 평타 공격력(Atk)을 더해서
        //   "내 평타 공격력 + 스킬 위력"이 최종 데미지가 되도록 한다.
        Hitbox[] hitboxes = go.GetComponentsInChildren<Hitbox>(true);
        foreach (var hb in hitboxes)
        {
            hb.direction = facing;
            hb.attackerStats = casterStats;
            if (casterStats != null)
                hb.damage += Mathf.RoundToInt(casterStats.Atk);
        }

        // 투사체(ProjectileStraight/ProjectileHoming 공통)에도 시전자 스탯 전달 + 공격력 가산
        ProjectileBase[] allProjectiles = go.GetComponentsInChildren<ProjectileBase>(true);
        foreach (var pb in allProjectiles)
        {
            pb.attackerStats = casterStats;
            if (casterStats != null)
                pb.damage += Mathf.RoundToInt(casterStats.Atk);
        }

        // 직선 투사체(ProjectileStraight) 전용 — Init() 안에서 이동 방향(=회전)과 수명 타이머
        // (자동 소멸)가 같이 설정된다. 이걸 안 불러주면 방향이 (0,0)인 채로 그 자리에 멈춰있고
        // 소멸도 안 됨. (참고: ProjectileHoming은 Init()이 아예 없음 — Awake()에서 스스로 수명
        // 타이머를 걸고 FixedUpdate()에서 스스로 방향을 계산해서, 이 호출이 필요 없다.)
        foreach (var proj in projectiles)
        {
            proj.Init(new Vector2(facing, 0));
            // 부모(스킬 본체)가 이미 반전돼 있으면 투사체 자신의 반전은 되돌린다 (이중 반전 방지)
            if (parentFlipped)
            {
                Vector3 ps = proj.transform.localScale;
                ps.x = Mathf.Abs(ps.x);
                proj.transform.localScale = ps;
            }
        }

        // 자동 소멸
        if (e.lifetime > 0f)
            Destroy(go, e.lifetime);
    }
}
