using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// [Faust 2026-11-08] 검 스킬: 제자리 다단히트 → 마지막에 전방 돌진하며 경로 데미지
// Skill 프리팹(Skill 컴포넌트와 같은 오브젝트)에 붙여서 사용.
// CharacterSkills.Activate → Skill.Launch(시전자 위치·방향 세팅) 직후 이 컴포넌트가 스킬 전체를 진행한다.
[RequireComponent(typeof(Skill))]
public class SwordFlurrySkill : MonoBehaviour
{
    [Header("전체 시간 (초) — 끝나면 조작 복귀")]
    public float totalDuration = 3.3f;

    [Header("1) 다단히트 (청록 박스)")]
    public float startDelay = 0.1f;              // 키 누르고 첫 타까지
    public float flurryDuration = 2.4f;          // 다단히트 지속
    public float tickInterval = 0.1f;            // 타격 간격
    public int tickDamage = 5;                   // 1타 기본 데미지 (공격력·방어·치명타 계산은 기존 방식 그대로)
    public Vector2 flurryBoxOffset = new Vector2(2.5f, 0.6f); // 플레이어 기준 (x = 바라보는 방향)
    public Vector2 flurryBoxSize = new Vector2(7f, 4f);
    public float tickKnockbackX = 0f;            // 0이면 적이 밀려나지 않아 박스 안에 계속 맞음
    public float tickKnockbackY = 0f;

    [Header("2) 마무리 돌진 (핑크 박스 쪽으로 이동)")]
    public float dashWindup = 0.1f;              // 다단히트 끝나고 돌진 전 잠깐 멈춤
    public float dashDistance = 7f;
    public float dashDuration = 0.2f;
    public int dashDamage = 40;
    public float dashHitHeight = 2.5f;           // 돌진 경로 판정 높이
    public float dashHitYOffset = 0.2f;          // 판정 중심 높이 (플레이어 기준)
    public float dashHitPadding = 0.6f;          // 시작·끝 지점 앞뒤로 더 넓힐 거리
    public float dashKnockbackX = 6f;
    public float dashKnockbackY = 3f;
    public bool passThroughEnemies = true;       // 돌진 중 적 몸체와 충돌 안 함 (통과)
    public bool stopAtWalls = true;              // 벽(ground 레이어)에 닿으면 거기서 멈춤
    [Tooltip("[2026-11-11] 땅에서 쓰면 발판 끝에서 멈춘다 (플레이어 중심이 발판 끝에 걸친 위치). 공중에서 쓰면 적용 안 함")]
    public bool stopAtLedge = true;
    public float ledgeCheckDepth = 0.6f;          // 발 아래로 이만큼 안에 땅이 있어야 '발판 위'로 봄

    [Header("아트 (선택 — 비워 두면 생략)")]
    [Tooltip("다단히트 동안 플레이어에 붙어 재생, 끝나면 삭제. 여러 개 넣으면 전부 동시에 나옴")]
    public GameObject[] flurryEffectPrefabs = new GameObject[0];
    public GameObject dashEffectPrefab;          // 돌진 시작 지점에 생성
    public GameObject dashEndEffectPrefab;       // 돌진 끝 지점에 생성
    [Header("캐릭터 모션 — 클립을 직접 넣으면 스킬 동안 플레이어 Animator 위에 덮어서 재생 (컨트롤러는 안 건드림)")]
    public AnimationClip flurryClip;             // 연격 동안 재생
    public bool loopFlurryClip = true;           // 연격 클립 반복
    public AnimationClip dashClip;               // 돌진 이동이 끝난 뒤부터 스킬 끝까지, 마지막 프레임 유지
    [Header("(선택) Animator 트리거 방식 — 클립 칸을 쓰면 비워 두세요")]
    public string animTriggerFlurry = "";        // 플레이어 Animator 트리거 이름 (없으면 비움)
    public string animTriggerDash = "";
    public AudioClip hitSfx;
    public AudioClip criticalSfx;

    [Header("[2026-11-09] 효과음 — 여러 개 넣으면 무작위, 비우면 생략")]
    [Tooltip("연타 중 휘두르는 소리 (적이 없어도 남)")]
    public AudioClip[] flurrySwingSfx = new AudioClip[0];
    [Tooltip("몇 타마다 휘두르기 소리를 낼지 (1 = 매 타. 0.1초 간격이라 2 정도가 적당)")]
    public int swingEveryTicks = 2;
    [Range(0f, 1f)] public float swingVolume = 0.6f;
    [Tooltip("연타가 적에게 맞을 때 (비우면 위 hitSfx). 한 타에 여러 마리를 맞혀도 소리는 한 번만")]
    public AudioClip[] flurryHitSfx = new AudioClip[0];
    [Tooltip("강한 참격(돌진) 시작 소리")]
    public AudioClip[] dashStartSfx = new AudioClip[0];
    [Tooltip("강한 참격이 적에게 맞을 때 (비우면 hitSfx)")]
    public AudioClip[] dashHitSfx = new AudioClip[0];
    [Tooltip("강한 참격 치명타 (비우면 criticalSfx)")]
    public AudioClip[] dashCriticalSfx = new AudioClip[0];
    [Tooltip("음높이 흔들림 (0.05 = ±5%)")]
    public float pitchJitter = 0.05f;

    [Header("[2026-11-09] 공중에서 쓰면")]
    [Tooltip("켜면 연타 중에도 중력을 받아 떨어짐 (가로로만 멈춤). 끄면 예전처럼 그 자리에 멈춰 떠 있음")]
    public bool keepGravity = true;

    [Header("디버그")]
    public bool drawDebugBoxes = true;           // 플레이 중 Scene 뷰에 판정 박스 표시

    PlayerController pc;
    Rigidbody2D rb;
    Stats stats;
    Collider2D body;
    int facing = 1;
    float savedGravity;
    bool locked, gravitySaved, collisionChanged;
    readonly List<Vector2Int> ignoredPairs = new List<Vector2Int>();
    readonly List<GameObject> flurryFx = new List<GameObject>();
    int enemyMask, wallMask;
    PlayableGraph animGraph;
    AnimationPlayableOutput animOut;
    AnimationClipPlayable animPlay;
    bool animLoop;

    void Awake()
    {
        // Skill.Launch가 lifetime으로 이 오브젝트를 먼저 지우지 않도록 (끝나면 직접 지움)
        var skill = GetComponent<Skill>();
        if (skill != null) skill.lifetime = 0f;
    }

    void Start()
    {
        // [Faust 2026-11-08] 안전장치: 스킬 키(CharacterSkills → Skill.Launch)로 생성된 경우에만 발동.
        // 씬에 미리 놓아 둔 오브젝트(아트 작업용)는 플레이해도 아무것도 하지 않는다.
        if (!WasLaunched()) return;
        pc = FindObjectOfType<PlayerController>();
        if (pc == null) { Destroy(gameObject); return; }
        rb = pc.GetComponent<Rigidbody2D>();
        stats = pc.GetComponent<Stats>();
        body = pc.GetComponent<Collider2D>();
        // Skill.Launch가 왼쪽이면 스킬 오브젝트 X 스케일을 음수로 뒤집어 둠 → 그걸로 방향 판단
        facing = transform.lossyScale.x < 0f ? -1 : 1;
        enemyMask = LayerMask.GetMask("Enemy", "BossBody");
        wallMask = LayerMask.GetMask("ground");
        StartCoroutine(Run());
    }

    // Skill.Launch가 호출되면 Skill 안의 casterRef(시전자)가 채워진다 → 그걸로 판단 (Skill.cs는 수정하지 않음)
    bool WasLaunched()
    {
        var skill = GetComponent<Skill>();
        if (skill == null) return false;
        var f = typeof(Skill).GetField("casterRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null && f.GetValue(skill) as Transform != null;
    }

    IEnumerator Run()
    {
        float t0 = Time.time;
        pc.LockControl(this); locked = true;
        if (rb != null)
        {
            savedGravity = rb.gravityScale; gravitySaved = true;
            if (!keepGravity) rb.gravityScale = 0f;
            rb.velocity = keepGravity ? new Vector2(0f, rb.velocity.y) : Vector2.zero;
        }
        Trigger(animTriggerFlurry);
        PlayClip(flurryClip, loopFlurryClip);

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        // ── 1) 다단히트 ──
        if (flurryEffectPrefabs != null)
            foreach (var prefab in flurryEffectPrefabs)
            {
                if (prefab == null) continue;
                var fx = Instantiate(prefab, pc.transform.position, Quaternion.identity, pc.transform);
                // [Faust 2026-11-08] 플레이어 자식이라 플레이어 스케일(-1)로 이미 뒤집힘 → 여기서 또 뒤집으면 원래대로 돌아가 버림(왼쪽에서 안 뒤집히던 원인)
                if (Mathf.Sign(fx.transform.lossyScale.x) != facing) { var s = fx.transform.localScale; s.x = -s.x; fx.transform.localScale = s; }
                flurryFx.Add(fx);
            }
        float flurryEnd = Time.time + flurryDuration;
        int tick = 0;
        while (Time.time < flurryEnd && pc != null)
        {
            HoldStill();
            if (swingEveryTicks <= 1 || tick % swingEveryTicks == 0)
                SkillSfx.Play(SkillSfx.Pick(flurrySwingSfx), swingVolume, pitchJitter);   // [2026-11-09]
            tick++;
            Vector2 c = (Vector2)pc.transform.position + new Vector2(flurryBoxOffset.x * facing, flurryBoxOffset.y);
            HitBox(c, flurryBoxSize, tickDamage, tickKnockbackX, tickKnockbackY, null, false);
            if (drawDebugBoxes) DrawBox(c, flurryBoxSize, Color.cyan, tickInterval);
            yield return WaitHolding(tickInterval);
        }
        DestroyFlurryFx();

        if (dashWindup > 0f) yield return WaitHolding(dashWindup);

        // ── 2) 마무리 돌진 ──
        if (pc != null)
        {
            Trigger(animTriggerDash);
            Vector2 start = rb != null ? rb.position : (Vector2)pc.transform.position;
            float travel = dashDistance;
            if (stopAtWalls && body != null && rb != null)
            {
                var hits = new RaycastHit2D[4];
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = wallMask, useTriggers = false };
                int n = body.Cast(new Vector2(facing, 0f), filter, hits, dashDistance);
                for (int i = 0; i < n; i++) travel = Mathf.Min(travel, Mathf.Max(0f, hits[i].distance - 0.02f));
            }
            // [2026-11-11] 발판 끝에서 멈춤 — 시작할 때 땅을 딛고 있었으면, 앞으로 0.1씩 나가며 발 아래 땅이 끊기는 곳 직전까지만 이동
            if (stopAtLedge && body != null)
            {
                float footY = body.bounds.min.y + 0.1f;
                float down = ledgeCheckDepth + 0.1f;
                float offX = start.x - body.bounds.center.x;   // rb 위치와 몸 중심 차이 보정
                int footMask = wallMask | LayerMask.GetMask("platform");   // [2026-11-11] 발판(platform) 위에서도 끝에서 멈추게
                bool Ground(float cx) => Physics2D.Raycast(new Vector2(cx, footY), Vector2.down, down, footMask).collider != null;
                float cx0 = start.x - offX;
                if (Ground(cx0))
                {
                    const float step = 0.1f;
                    float ok = 0f;
                    for (float d = step; d <= travel + 0.0001f; d += step)
                    {
                        if (!Ground(cx0 + facing * d)) break;
                        ok = d;
                    }
                    travel = Mathf.Min(travel, ok);
                }
            }
            Vector2 end = start + new Vector2(facing * travel, 0f);
            if (dashEffectPrefab != null) SpawnFx(dashEffectPrefab, start);
            SkillSfx.Play(SkillSfx.Pick(dashStartSfx), 1f, pitchJitter);   // [2026-11-09] 강한 참격 시작
            if (passThroughEnemies) SetEnemyCollision(false);

            var dashHit = new HashSet<Health>();
            float t = 0f;
            while (t < dashDuration && pc != null)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                Vector2 p = Vector2.Lerp(start, end, Mathf.Clamp01(t / Mathf.Max(0.0001f, dashDuration)));
                if (rb != null) { rb.velocity = Vector2.zero; rb.MovePosition(p); } else pc.transform.position = p;
                // 시작점 ~ 현재 위치까지를 한 박스로 훑어서, 경로 위 적을 한 번씩 타격
                float minX = Mathf.Min(start.x, p.x) - dashHitPadding, maxX = Mathf.Max(start.x, p.x) + dashHitPadding;
                Vector2 c = new Vector2((minX + maxX) * 0.5f, start.y + dashHitYOffset);
                Vector2 size = new Vector2(maxX - minX, dashHitHeight);
                HitBox(c, size, dashDamage, dashKnockbackX, dashKnockbackY, dashHit, true);
                if (drawDebugBoxes) DrawBox(c, size, Color.magenta, 0.5f);
            }
            if (passThroughEnemies) SetEnemyCollision(true);
            if (dashEndEffectPrefab != null && pc != null) SpawnFx(dashEndEffectPrefab, pc.transform.position);
        }

        // 돌진 모션: 돌진 이동이 끝난 뒤부터 스킬 끝까지 (없으면 연격 모션을 끄고 기본 모션으로)
        if (dashClip != null) PlayClip(dashClip, false); else StopClip();

        // ── 3) 남은 시간 동안 경직 후 복귀 ──
        float remain = totalDuration - (Time.time - t0);
        if (remain > 0f) yield return WaitHolding(remain);
        Finish();
        Destroy(gameObject);
    }

    void DestroyFlurryFx()
    {
        foreach (var fx in flurryFx) if (fx != null) Destroy(fx);
        flurryFx.Clear();
    }

    IEnumerator WaitHolding(float sec)
    {
        float end = Time.time + sec;
        while (Time.time < end) { HoldStill(); yield return null; }
    }

    void HoldStill()
    {
        if (rb != null) rb.velocity = keepGravity ? new Vector2(0f, rb.velocity.y) : Vector2.zero;   // [2026-11-09] 중력 유지 시 세로 속도는 둠
    }

    void HitBox(Vector2 center, Vector2 size, int damage, float kx, float ky, HashSet<Health> once, bool dash)
    {
        // [2026-11-09] 연타/강한 참격 소리를 따로, 여러 개 중 무작위. 한 번에 여러 마리를 맞혀도 소리는 첫 대상에서만
        AudioClip hitClip = (dash ? SkillSfx.Pick(dashHitSfx) : SkillSfx.Pick(flurryHitSfx)) ?? hitSfx;
        AudioClip critClip = (dash ? SkillSfx.Pick(dashCriticalSfx) : null) ?? criticalSfx;
        bool first = true;
        var cols = Physics2D.OverlapBoxAll(center, size, 0f, enemyMask);
        var thisTick = new HashSet<Health>();
        foreach (var col in cols)
        {
            if (col == null || Hitbox.IsHitboxCollider(col)) continue;
            Health h = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
            if (h == null || !thisTick.Add(h)) continue;
            if (once != null && !once.Add(h)) continue;
            if (Hitbox.DealDirectHit(h, damage, stats, facing, kx, ky, col.bounds.center,
                                     first ? hitClip : null, first ? critClip : null, "SwordFlurry", true))
                first = false;
        }
    }

    void SetEnemyCollision(bool enable)
    {
        if (pc == null) return;
        int pl = pc.gameObject.layer;
        if (!enable)
        {
            ignoredPairs.Clear();
            foreach (var name in new[] { "Enemy", "BossBody" })
            {
                int l = LayerMask.NameToLayer(name);
                if (l < 0 || Physics2D.GetIgnoreLayerCollision(pl, l)) continue; // 원래 꺼져 있던 건 건드리지 않음
                Physics2D.IgnoreLayerCollision(pl, l, true);
                ignoredPairs.Add(new Vector2Int(pl, l));
            }
            collisionChanged = true;
        }
        else
        {
            foreach (var p in ignoredPairs) Physics2D.IgnoreLayerCollision(p.x, p.y, false);
            ignoredPairs.Clear();
            collisionChanged = false;
        }
    }

    void SpawnFx(GameObject prefab, Vector3 pos)
    {
        var fx = Instantiate(prefab, pos, Quaternion.identity);
        if (facing < 0) { var s = fx.transform.localScale; s.x = -Mathf.Abs(s.x); fx.transform.localScale = s; }
    }

    void Trigger(string name)
    {
        if (string.IsNullOrEmpty(name) || pc == null) return;
        var an = pc.GetComponentInChildren<Animator>();
        if (an != null) an.SetTrigger(name);
    }

    void PlayClip(AnimationClip clip, bool loop)
    {
        if (clip == null || pc == null) return;
        var an = pc.GetComponentInChildren<Animator>();
        if (an == null) return;
        if (!animGraph.IsValid())
        {
            animGraph = PlayableGraph.Create("SwordFlurrySkillAnim");
            animGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            animOut = AnimationPlayableOutput.Create(animGraph, "SkillAnim", an);
        }
        if (animPlay.IsValid()) animPlay.Destroy();
        animPlay = AnimationClipPlayable.Create(animGraph, clip);
        animOut.SetSourcePlayable(animPlay);
        animLoop = loop;
        animGraph.Play();
    }

    void StopClip()
    {
        if (animGraph.IsValid()) animGraph.Destroy();   // 덮어쓴 재생을 걷어내면 원래 Animator 컨트롤러로 복귀
    }

    void Update()
    {
        // 반복 재생 (클립 자체의 Loop 설정과 상관없이)
        if (animLoop && animPlay.IsValid())
        {
            var clip = animPlay.GetAnimationClip();
            if (clip != null && clip.length > 0f && animPlay.GetTime() >= clip.length)
                animPlay.SetTime(animPlay.GetTime() % clip.length);
        }
    }

    void Finish()
    {
        StopClip();
        if (collisionChanged) SetEnemyCollision(true);
        DestroyFlurryFx();
        if (rb != null && gravitySaved) { rb.gravityScale = savedGravity; gravitySaved = false; }
        if (pc != null && locked) { pc.ResumeControl(this); locked = false; }
    }

    // 방 이동·사망 등으로 도중에 지워져도 조작 잠금·중력·충돌이 남지 않게
    void OnDestroy() { Finish(); }

    static void DrawBox(Vector2 c, Vector2 size, Color col, float dur)
    {
        Vector2 h = size * 0.5f;
        Vector3 a = new Vector3(c.x - h.x, c.y - h.y), b = new Vector3(c.x + h.x, c.y - h.y),
                d = new Vector3(c.x - h.x, c.y + h.y), e = new Vector3(c.x + h.x, c.y + h.y);
        Debug.DrawLine(a, b, col, dur); Debug.DrawLine(b, e, col, dur);
        Debug.DrawLine(e, d, col, dur); Debug.DrawLine(d, a, col, dur);
    }
}
