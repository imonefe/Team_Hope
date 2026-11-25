using UnityEngine;

/// <summary>
/// [2026-11-25 추가] 회복 구 — 일반 적을 처치하면 dropChance 확률로 떨어지는 초록 구.
/// 플레이어가 먹으면 최대 체력의 healPercent 만큼 회복 (× CombatEvents.HealOrbMultiplier — 과부하 사이버웨어가 올림).
///
///  - 프리팹: Etc/Resources/HealOrb.prefab (Health.Die 에서 HealOrb.TryDrop 으로 부름)
///  - 튀어 올랐다가 바닥 · 발판(ground, platform 층)에 내려앉고 위아래로 살랑거림
///  - 체력이 가득이면 먹지 않음 (그대로 남음). lifetime 초 뒤 사라지며 마지막 blinkTime 초는 깜빡임
///  - 물리 몸체 없이 스스로 움직이고, 플레이어와의 거리로 획득을 판정한다 (충돌 층 설정 불필요)
/// </summary>
public class HealOrb : MonoBehaviour
{
    [Header("드랍")]
    [Range(0f, 1f)] public float dropChance = 0.25f;
    [Header("회복")]
    [Range(0f, 1f)] public float healPercent = 0.15f;
    public Color textColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Header("움직임")]
    public float popSpeed = 5f;          // 처음 튀어 오르는 속도
    public float sideSpeed = 1.5f;       // 옆으로 튀는 최대 속도
    public float gravity = 18f;
    public float hoverHeight = 0.3f;     // 바닥에서 떠 있는 높이
    public float bobAmount = 0.06f;
    public float bobSpeed = 3f;

    [Header("획득")]
    public float pickupDelay = 0.4f;     // 나온 직후 바로 먹히지 않게
    public float magnetRadius = 2f;
    public float magnetSpeed = 9f;
    public float pickupRadius = 0.5f;

    [Header("수명")]
    public float lifetime = 12f;
    public float blinkTime = 3f;

    const float TowardCamera = 1.5f;     // 다른 획득 연출과 같은 깊이

    Vector2 vel;
    bool landed;
    float groundY, age, bobPhase;
    SpriteRenderer[] renderers;
    Transform player;
    Stats playerStats;
    float findTimer;

    static HealOrb template;
    static bool templateLoaded;

    /// <summary>적 처치 자리에서 확률로 회복 구를 떨어뜨림</summary>
    public static void TryDrop(Vector3 pos, Transform roomParent)
    {
        if (!templateLoaded) { template = Resources.Load<HealOrb>("HealOrb"); templateLoaded = true; }
        if (template == null || Random.value >= template.dropChance) return;
        pos.z -= TowardCamera;
        Instantiate(template, pos, Quaternion.identity, roomParent);
    }

    void Start()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        vel = new Vector2(Random.Range(-sideSpeed, sideSpeed), popSpeed);
        bobPhase = Random.value * 6.28f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime) { Destroy(gameObject); return; }
        Blink();

        FindPlayer(dt);
        bool canTake = age >= pickupDelay && playerStats != null && playerStats.currentHp < playerStats.maxHp;
        Vector3 target = player != null ? PlayerCenter() : Vector3.zero;
        float dist = player != null ? Vector2.Distance(transform.position, target) : float.MaxValue;

        if (canTake && dist <= pickupRadius) { Take(); return; }

        if (canTake && dist <= magnetRadius)
        {
            // 플레이어에게 끌려감 (가까울수록 빠르게)
            float k = 1f - dist / magnetRadius;
            Vector3 p = Vector2.MoveTowards(transform.position, target, magnetSpeed * (0.4f + k) * dt);
            p.z = transform.position.z;
            transform.position = p;
            landed = false;
            vel = Vector2.zero;
            return;
        }

        if (landed)
        {
            Vector3 p = transform.position;
            p.y = groundY + hoverHeight + Mathf.Sin(age * bobSpeed + bobPhase) * bobAmount;
            transform.position = p;
            return;
        }

        // 떨어지는 중
        vel.y -= gravity * dt;
        Vector3 pos = transform.position;
        Vector2 step = vel * dt;
        if (vel.y < 0f)
        {
            // 아래로 갈 때만 바닥 · 발판 검사 (올라갈 때는 발판을 뚫고 지나감)
            float len = -step.y + hoverHeight;
            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.down, len, LayerMask.GetMask("ground", "platform"));
            if (hit.collider != null)
            {
                landed = true;
                groundY = hit.point.y;
                pos.x += step.x;
                pos.y = groundY + hoverHeight;
                transform.position = pos;
                return;
            }
        }
        pos.x += step.x;
        pos.y += step.y;
        transform.position = pos;
        if (age > 6f && !landed) Destroy(gameObject);   // 바닥을 못 찾고 떨어지면 정리
    }

    void Take()
    {
        float before = playerStats.maxHp > 0 ? (float)playerStats.currentHp / playerStats.maxHp : 1f;
        int amount = Mathf.Max(1, Mathf.RoundToInt(playerStats.maxHp * healPercent * Mathf.Max(0f, CombatEvents.HealOrbMultiplier)));
        playerStats.HealWithEffect(amount);
        CombatEvents.RaiseHealOrbTaken(before);   // 사이버웨어 (급조된 장기 등)
        FloatingTextSpawner.SpawnText(PlayerCenter() + new Vector3(0f, 0.8f, 0f), $"+{amount}", textColor);
        Destroy(gameObject);
    }

    void Blink()
    {
        if (renderers == null) return;
        bool show = true;
        float left = lifetime - age;
        if (left < blinkTime) show = Mathf.Repeat(age, left < 1f ? 0.12f : 0.25f) < (left < 1f ? 0.06f : 0.15f);
        foreach (var r in renderers) if (r != null) r.enabled = show;
    }

    void FindPlayer(float dt)
    {
        // 태그 배틀로 캐릭터가 바뀔 수 있어서 가끔 다시 찾는다
        findTimer -= dt;
        if (player != null && player.gameObject.activeInHierarchy && findTimer > 0f) return;
        findTimer = 0.5f;
        player = null; playerStats = null;
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            Stats s = go.GetComponent<Stats>();
            if (s == null || !go.activeInHierarchy) continue;
            player = go.transform; playerStats = s;
            break;
        }
    }

    Vector3 PlayerCenter()
    {
        Collider2D c = player.GetComponent<Collider2D>();
        Vector3 p = c != null ? c.bounds.center : player.position;
        p.z = transform.position.z;
        return p;
    }
}
