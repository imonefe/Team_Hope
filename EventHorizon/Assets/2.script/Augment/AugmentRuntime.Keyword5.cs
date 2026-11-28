using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// [2026-11-25] 키워드 5개 단계 특수 보상 (교수님 피드백 6 '증강을 얻었다는 체감') + 같은 기능을 쓰는 사이버웨어.
///  연출은 Editor/KeywordFxBuilder 가 기존 이펙트를 색만 바꿔 만든 프리팹(Resources/KeywordFx)만 쓴다.
///   잔상 5  : 점프 · 대시 연속 사용 횟수 +1
///   과부하 5: 체력 50% 이하 → 몸 · 공격 범위 1.3배 (오염된 외골격: 1.15/1.2/1.25배, 합쳐서 최대 1.4배)
///   연격 5  : 공격 속도 증가 (AugmentRuntime.Update 의 공격 속도 계산)
///   가열 블레이드: 검 3타 → 불타는 검기 (피해 + 과열) — 연출 KW2_Heat_Wave (불꽃 혀 · 불티 · 연기)
///   과열 5  : 과열 전염 — 과열 5개 이상인 적을 때리면 주변 3m 적에게 과열 2개
///   정지 5  : 제로포인트가 풀리는 순간 시간 충격파 (화면 안 적 피해 + 밀어냄)
///   강탈 5  : 일반 적을 처치하면 그 적의 홀로그램이 나와 그 적의 공격(돌진 포함)을 한 번 하고 사라짐
///   처형 5  : 체력 10% 이하인 일반 적을 약점 속성으로 공격 → 잠깐 멈춤 + 처형 참격 + 즉시 처형
///   균열 5  : 치명타 → 바닥을 타고 앞으로 금이 뻗어 나감 (피해 + 균열)
/// </summary>
public partial class AugmentRuntime
{
    // ══════════════ 연출 프리팹 (Resources/KeywordFx) ══════════════

    static readonly Dictionary<string, GameObject> kfxCache = new Dictionary<string, GameObject>();

    static GameObject KFx(string name, Vector3 pos, float scale, float life, float angleZ = 0f, bool warn = true)
    {
        if (!kfxCache.TryGetValue(name, out var prefab) || prefab == null)
        {
            prefab = Resources.Load<GameObject>("KeywordFx/" + name);
            kfxCache[name] = prefab;
            if (prefab == null && warn) Debug.LogWarning($"[키워드 연출] Resources/KeywordFx/{name} 가 없음 — 메뉴 Tools/Event Horizon/키워드 연출 프리팹 다시 만들기");
        }
        if (prefab == null) return null;
        if (AugmentFx.Quiet) scale *= AugmentFx.QuietSize;   // [2026-11-26] 조용한 연출
        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, angleZ));
        go.transform.localScale = prefab.transform.localScale * scale;
        // 복사본은 스크립트를 뺐으므로 재생을 직접 시작 (원본에서 스크립트가 재생시키던 이펙트 대비)
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(true); ps.Play(true); }
        if (life > 0f) Destroy(go, life);
        return go;
    }

    // ══════════════ 매 프레임 ══════════════

    void UpdateKeyword5(bool paused)
    {
        if (pc == null) return;

        // 잔상 5: 점프 · 대시 연속 사용 +1
        int extra = Kw(AugmentKeyword.Afterimage) >= AugmentRules.Stage3 ? 1 : 0;
        pc.bonusJumps = extra;
        pc.bonusDashes = extra;

        UpdateBody(paused);
        AimScale *= bodyCur;   // 공격 판정 크기 (OnSwing 에서 평타 판정에 곱해짐)

    }

    // ══════════════ 과부하 5 · 오염된 외골격: 몸 커짐 ══════════════

    float bodyCur = 1f, bodyPulse;

    float BodyTarget()
    {
        if (HpRatioP() > 0.5f) return 1f;
        float add = 0f;
        if (Kw(AugmentKeyword.Overload) >= AugmentRules.Stage3) add += 0.30f;
        int lv = L("과부하 퓨즈");
        if (lv > 0) add += ByLevel(lv, 0.15f, 0.20f, 0.25f);
        return Mathf.Min(1.4f, 1f + add);
    }

    /// <summary>헤네시 전체를 키움 (PlayerController.visualScale — 그림 · 공격 판정 위치는 커지고, 몸 충돌체는 같은 크기로 유지)</summary>
    void UpdateBody(bool paused)
    {
        float target = BodyTarget();
        float before = bodyCur;
        bodyCur = Mathf.MoveTowards(bodyCur, target, Time.deltaTime * 1.5f);
        pc.visualScale = bodyCur;
        // [2026-11-26] 과부하 퓨즈만으로 커졌고 그 퓨즈가 일반 등급이면 조용하게 (커질 때 흔들림 · 번쩍임 없음, 맥동 없음 — 몸 크기 변화만)
        if (target > 1.01f && before <= 1.01f) Proc("과부하 퓨즈");   // [2026-11-28] 커지는 순간 = 발동
        bool quiet = IsCommon("과부하 퓨즈") && Kw(AugmentKeyword.Overload) < AugmentRules.Stage3;
        if (target > 1.01f && before <= 1.01f && !quiet) { AugmentFx.BodyFlash(pc, COverload, 0.8f, 0.35f); CameraFollow.Shake(0.12f, 0.15f); }
        // 커져 있는 동안 몸이 분홍으로 맥동 (따로 이펙트를 띄우지 않음)
        if (bodyCur > 1.02f && !paused)
        {
            bodyPulse -= Time.deltaTime;
            if (bodyPulse <= 0f) { bodyPulse = 0.7f; if (!quiet) AugmentFx.BodyFlash(pc, COverload, 0.35f, 0.3f); }
        }
    }

    // ══════════════ 가열 블레이드: 검 3타 불타는 검기 ══════════════

    float lastFinisher = -9f;

    void Keyword5OnSwing(SwingTag tag)
    {
        if (tag == null || pc == null || tag.weapon != Weapontype.Sword || tag.combo != 3) return;
        int lvDance = L("검무");
        if (lvDance <= 0 || Time.time - lastFinisher < 0.25f) return;   // 판정이 여러 개 생겨도 한 번만
        lastFinisher = Time.time; Proc("검무");
        // [2026-11-27] 불처럼 보이게: 불꽃 혀 · 불티 · 연기가 뒤로 남는 새 검기 (메뉴 EventHorizon/이펙트 v2 만들기). 없으면 예전 메쉬 검기
        string waveFx = Resources.Load<GameObject>("KeywordFx/KW2_Heat_Wave") != null ? "KW2_Heat_Wave" : "KW_Dance_SwordWave";
        StartCoroutine(SwordWave(PCenter, tag.facing, 6f, ByLevel(lvDance, 0.5f, 0.75f, 1.0f), (int)ByLevel(lvDance, 1, 2, 3), waveFx, OverheatColor));
    }

    IEnumerator SwordWave(Vector3 from, int dir, float length, float ratio, int heat, string fx, Color sparkColor)
    {
        const float speed = 22f;
        Vector3 to = from + new Vector3(dir * length, 0f, 0f);
        // [2026-11-27] v2 검기(KW2_)는 남은 불꽃 · 연기가 스스로 꺼지도록 1초 남김 (예전 것은 그대로 0.15초)
        bool v2 = fx.StartsWith("KW2_");
        var go = KFx(fx, from, 1.1f, length / speed + (v2 ? 1.0f : 0.15f), dir > 0 ? 0f : 180f);
        var hit = new HashSet<Health>();
        float t = 0f, time = length / speed;
        while (t <= time)
        {
            Vector3 head = Vector3.Lerp(from, to, Mathf.Clamp01(t / time));
            if (go != null) go.transform.position = head;
            foreach (var e in EnemiesInBox(head, new Vector2(1.2f, 1.6f)))
                if (hit.Add(e))
                {
                    AbilityHit(e, ratio, from, 3f, 1f);
                    if (heat > 0) StatusOf(e).AddOverheat(heat);
                    AugmentFx.Sparks(EC(e), sparkColor, 8, 4f, 0.3f, 0f, 0.14f);
                }
            t += Time.deltaTime;
            yield return null;
        }
        // 다 날아가면 새 불꽃만 멈춤 (이미 나온 불꽃 · 불티 · 연기는 제 수명대로 꺼짐)
        if (v2 && go != null) foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    // ══════════════ 과열 5: 과열 전염 ══════════════
    //  과열이 5개 이상인 적을 평타로 때리면 주변 3m 안의 다른 적에게 과열 2개가 불티로 튄다 (같은 적에게서는 0.5초에 한 번)

    readonly Dictionary<Health, float> spreadAt = new Dictionary<Health, float>();

    void OverheatSpread(Health h, EnemyStatus st)
    {
        if (st == null || !st.IsHot) return;
        if (spreadAt.TryGetValue(h, out float last) && Time.time - last < 0.5f) return;
        spreadAt[h] = Time.time;
        foreach (var e in EnemiesIn(EC(h), 3f))
        {
            if (e == h) continue;
            var target = e;
            AugmentFx.Fly(EC(h), e.transform, OverheatColor, 1, 0.3f, () =>
            {
                if (target != null && !target.IsDead) { StatusOf(target).AddOverheat(2); AugmentFx.Sparks(EC(target), OverheatColor, 6, 3f, 0.3f); }
            }, 0.2f, false);
        }
    }

    // 대시 연동 (지금은 쓰는 보상 없음 — 호출 자리만 남김)
    void Keyword5OnDashStart(Vector3 pos) { }
    void Keyword5OnDashEnd(Vector3 pos) { }

    // ══════════════ 정지 5: 시간 충격파 ══════════════

    void Keyword5OnPauseEnd()
    {
        if (Kw(AugmentKeyword.Stasis) < 5 || pc == null) return;
        try
        {
            var cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            KFx("KW_Stasis_Wave", PCenter, 2.5f, 2f);
            AugmentFx.Ring(PCenter, CStasis, 9f, 0.55f, 0.14f, 2);
            CameraFollow.Shake(0.25f, 0.2f);
            int n = 0;
            foreach (var e in EnemiesIn(PCenter, 30f))
            {
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                    if (vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) continue;
                }
                AbilityHit(e, 0.8f, PPos, 7f, 2f);   // 헤네시 반대쪽으로 밀어냄
                n++;
            }
#if UNITY_EDITOR
            Debug.Log($"[키워드 정지 5] 시간 충격파 — 맞은 적 {n}");
#endif
        }
        catch (System.Exception ex) { Debug.LogException(ex); }
    }

    // ══════════════ 강탈 5: 강탈한 적의 공격을 홀로그램으로 ══════════════
    //  일반 적을 처치하면 그 자리에 그 적의 홀로그램(고장난 비서와 같은 홀로그램 그림)이 나와 실제 적처럼
    //  그 적의 이동 동작 · 이동 속도로 가장 가까운 적을 쫓아가, 그 적의 공격 거리에 들어오면 공격을 한 번 하고 사라진다 — 돌진형이면 돌진까지.
    //  공격 동작의 애니메이션 이벤트 시점(StartAttackDash · SpawnHitbox · SpawnAttackEffect)에 맞춰 판정 · 투사체 · 이펙트를 낸다.
    //  동작이 끝나면 사라짐. 1초에 한 번 · 동시에 최대 3개.

    int holoAlive; float holoReady;
    const float HoloRatio = 0.6f;

    struct HoloSpec
    {
        public AnimationClip clip; public string spritePath; public Sprite sprite;
        public Vector3 scale; public int sortLayer, sortOrder;
        public Vector3 hitOffset; public Vector2 hitSize; public bool melee;
        public GameObject projectile, effect; public Vector3 effectOffset;
        public bool dash; public float dashSpeed, dashDistance, dashDelay;
        public float spawnScale;
        public AnimationClip moveClip; public float speed, range;   // [2026-11-25] 실제 적처럼 쫓아가기
        public float footOffset;   // [2026-11-25] 위치 → 몸 충돌체 바닥까지 거리 (바닥에 붙이기)
        public Vector2 bodySize;   // [2026-11-25] 몸 충돌체 크기 (사라질 때 파편 범위 — 그림은 위 여백이 커서 씀)
        // [2026-11-25] 원래 적의 그림 구조 그대로: 적 위치 → 애니메이터 오브젝트 오프셋, 애니메이터 → 그림까지 자식들의 지역 변환
        public Vector3 animOffset, animScale;
        public Vector3[] chainPos, chainScale; public Quaternion[] chainRot;
    }

    void Keyword5OnKill(Health h, Vector3 pos)
    {
        if (Kw(AugmentKeyword.Plunder) < 5 || h == null || IsBoss(h) || pc == null) return;
        if (Time.time < holoReady || holoAlive >= 3) return;
        var e = h.GetComponent<Enemy>();
        if (e == null || !TryReadHolo(e, out var spec)) return;
        var target = Nearest(EC(h), 10f, h);
        if (target == null) return;
        holoReady = Time.time + 1f;
        StartCoroutine(HoloAttack(spec, h.transform.position, target));
    }

    bool TryReadHolo(Enemy e, out HoloSpec spec)
    {
        spec = default;
        var an = e.GetComponentInChildren<Animator>();
        var sr = AugmentFx.PlayerSprite(e);   // 가장 큰 스프라이트 = 몸 그림
        if (an == null || an.runtimeAnimatorController == null || sr == null) return false;
        // 공격 동작: 'SpawnHitbox' 이벤트가 있는 클립 (이름에 attack 이 있으면 우선)
        AnimationClip best = null;
        foreach (var c in an.runtimeAnimatorController.animationClips)
        {
            if (c == null) continue;
            bool hasHit = false;
            foreach (var ev in c.events) if (ev.functionName == "SpawnHitbox") { hasHit = true; break; }
            if (!hasHit) continue;
            if (best == null || (c.name.ToLower().Contains("attack") && !best.name.ToLower().Contains("attack"))) best = c;
        }
        if (best == null) return false;
        spec.clip = best;
        spec.spritePath = RelPath(an.transform, sr.transform);
        float ef = Mathf.Sign(e.transform.lossyScale.x); if (ef == 0f) ef = 1f;
        Vector3 ao = an.transform.position - e.transform.position;
        spec.animOffset = new Vector3(ao.x * ef, ao.y, 0f);   // 오른쪽을 볼 때 기준
        Vector3 al = an.transform.lossyScale;
        spec.animScale = new Vector3(Mathf.Abs(al.x), Mathf.Abs(al.y), 1f);
        var chain = new List<Transform>();
        for (var x = sr.transform; x != null && x != an.transform; x = x.parent) chain.Insert(0, x);
        spec.chainPos = new Vector3[chain.Count]; spec.chainScale = new Vector3[chain.Count]; spec.chainRot = new Quaternion[chain.Count];
        for (int i = 0; i < chain.Count; i++)
        {
            spec.chainPos[i] = chain[i].localPosition; spec.chainRot[i] = chain[i].localRotation;
            var ls = chain[i].localScale; spec.chainScale[i] = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), ls.z);
        }
        spec.sprite = sr.sprite;
        spec.scale = new Vector3(Mathf.Abs(sr.transform.lossyScale.x), Mathf.Abs(sr.transform.lossyScale.y), 1f);
        spec.sortLayer = sr.sortingLayerID; spec.sortOrder = sr.sortingOrder;
        spec.spawnScale = e.spawnScale;
        float face = Mathf.Sign(e.transform.localScale.x); if (face == 0f) face = 1f;
        spec.melee = e.hitboxPrefab != null;
        if (e.hitboxPoint != null)
        {
            Vector3 d = e.hitboxPoint.position - e.transform.position;
            spec.hitOffset = new Vector3(Mathf.Abs(d.x), d.y, 0f);   // 앞쪽 거리 (방향은 홀로그램이 정함)
        }
        else spec.hitOffset = new Vector3(0.8f, 0.6f, 0f);
        spec.hitSize = new Vector2(1.6f, 1.4f);
        var box = e.hitboxPrefab != null ? e.hitboxPrefab.GetComponentInChildren<BoxCollider2D>() : null;
        if (box != null)
        {
            Vector3 ls = box.transform.lossyScale;
            spec.hitSize = new Vector2(Mathf.Abs(box.size.x * ls.x), Mathf.Abs(box.size.y * ls.y)) * Mathf.Max(0.1f, e.spawnScale);
        }
        spec.projectile = e.projectilePrefab;
        spec.effect = e.attackEffectPrefab;
        Transform ep = e.attackEffectPoint != null ? e.attackEffectPoint : e.hitboxPoint;
        if (ep != null) { Vector3 d = ep.position - e.transform.position; spec.effectOffset = new Vector3(Mathf.Abs(d.x), d.y, 0f); }
        spec.dash = e.attackDash; spec.dashSpeed = e.attackDashSpeed; spec.dashDistance = e.attackDashDistance; spec.dashDelay = e.attackDashDelay;
        spec.moveClip = FindMoveClip(an.runtimeAnimatorController, best);
        var body = e.GetComponent<Collider2D>();
        spec.footOffset = body != null ? e.transform.position.y - body.bounds.min.y : 0f;
        spec.bodySize = body != null ? (Vector2)body.bounds.size : new Vector2(1f, 1.6f);
        var st = e.GetComponent<Stats>();
        spec.speed = st != null && st.spd > 0f ? st.spd : 3f;
        // 공격 시작 거리: 그 적의 공격 거리 (원거리 적은 최소 5m)
        spec.range = Mathf.Max(0.8f, e.AttackRange);
        if (spec.projectile != null) spec.range = Mathf.Max(spec.range, 5f);
        return true;
    }

    /// <summary>[2026-11-25] 걷기 동작 찾기 — 적들은 공용 애니메이터(Enemy.controller)에 클립만 바꿔 끼운 오버라이드 컨트롤러를 씀.
    /// 공용 걷기 자리(EnemyBase_Walk)에 그 적이 끼운 클립을 꺼냄. 오버라이드가 아니면 이름으로 찾음 (walk · wark · run · move · chase)</summary>
    static AnimationClip FindMoveClip(RuntimeAnimatorController rac, AnimationClip attack)
    {
        if (rac is AnimatorOverrideController aoc)
        {
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(list);
            foreach (var kv in list)
                if (kv.Key != null && kv.Key.name.ToLower().Contains("walk"))
                    return kv.Value != null ? kv.Value : kv.Key;
        }
        foreach (var c in rac.animationClips)
        {
            if (c == null || c == attack) continue;
            string n = c.name.ToLower();
            if (n.Contains("atk") || n.Contains("attack")) continue;
            if (n.Contains("walk") || n.Contains("wark") || n.Contains("run") || n.Contains("move") || n.Contains("chase")) return c;
        }
        return null;
    }

    static string RelPath(Transform root, Transform t)
    {
        if (t == root) return "";
        var parts = new List<string>();
        for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
        return string.Join("/", parts);
    }

    IEnumerator HoloAttack(HoloSpec sp, Vector3 at, Health target)
    {
        holoAlive++;
        // 꼭두각시: 공격 클립을 직접 재생(SampleAnimation)해 그림만 바꾸는 숨은 오브젝트 → 그 그림을 홀로그램이 따라 그림
        var root = new GameObject("HoloPuppet");
        root.transform.position = at;
        Transform body = root.transform;
        if (!string.IsNullOrEmpty(sp.spritePath))
        {
            var names = sp.spritePath.Split('/');
            for (int i = 0; i < names.Length; i++)
            {
                var c = new GameObject(names[i]).transform; c.SetParent(body, false);
                if (sp.chainPos != null && i < sp.chainPos.Length) { c.localPosition = sp.chainPos[i]; c.localRotation = sp.chainRot[i]; c.localScale = sp.chainScale[i]; }
                body = c;
            }
        }
        var sr = body.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sp.sprite; sr.sortingLayerID = sp.sortLayer; sr.sortingOrder = sp.sortOrder; sr.enabled = false;

        float face = Mathf.Sign(target.transform.position.x - at.x); if (face == 0f) face = 1f;
        Vector3 pos = at;
        int groundMask = LayerMask.GetMask("ground", "platform");
        float fallV = 0f;
        // 바닥에 붙이기: 발 위 0.6 에서 아래로 광선 → 발을 바닥 면에. 0.6 이하 턱은 바로 올라서고, 바닥이 꺼지면 중력으로 떨어짐
        void Ground(bool snap)
        {
            float feet = pos.y - sp.footOffset;
            var hit = Physics2D.Raycast(new Vector2(pos.x, feet + 0.6f), Vector2.down, 30f, groundMask);
            if (hit.collider == null) return;
            float gy = hit.point.y;
            if (snap || feet <= gy + 0.02f) { pos.y = gy + sp.footOffset; fallV = 0f; return; }
            fallV += 25f * Time.deltaTime;
            feet = Mathf.Max(gy, feet - fallV * Time.deltaTime);
            pos.y = feet + sp.footOffset;
            if (feet <= gy) fallV = 0f;
        }
        void Pose()
        {
            // 애니메이터 오브젝트 자리 = 적 위치 + 오프셋(바라보는 쪽으로), 좌우는 맨 위 크기의 부호로 (원래 적과 같은 방식)
            root.transform.position = pos + new Vector3(sp.animOffset.x * face, sp.animOffset.y, 0f);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = new Vector3(sp.animScale.x * face, sp.animScale.y, 1f);
        }
        Ground(true);   // 공중에서 죽었어도 바닥에서 나타남
#if UNITY_EDITOR
        Debug.Log($"[강탈 홀로그램] 클립 {sp.clip.name} · 이동 {(sp.moveClip != null ? sp.moveClip.name : "없음")} · 발 오프셋 {sp.footOffset:F2} · 그림 경로 '{sp.spritePath}' · 나타난 높이 {pos.y:F2}");
#endif
        float len = Mathf.Max(0.2f, sp.clip.length);
        // [2026-11-25] 공격 클립 재생: 이벤트를 뺀 사본을 애니메이터 그래프로 직접 시간 지정해 재생 (SampleAnimation 은 실행 중 스프라이트를 안 바꿈)
        var clip = Instantiate(sp.clip);
        clip.events = new AnimationEvent[0];   // 원래 이벤트는 이쪽에서 시간에 맞춰 처리 (받는 스크립트가 없어 오류 나지 않게)
        var animator = root.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 숨은 그림이라도 계속 움직이게
        var graph = PlayableGraph.Create("HoloPuppet");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Holo", animator);
        var clipPlayable = AnimationClipPlayable.Create(graph, clip);
        AnimationClip moveCopy = null;
        AnimationClipPlayable movePlayable = default;
        if (sp.moveClip != null)
        {
            moveCopy = Instantiate(sp.moveClip); moveCopy.events = new AnimationEvent[0];
            movePlayable = AnimationClipPlayable.Create(graph, moveCopy);
        }
        graph.Play();
        void Sample(AnimationClipPlayable p, float time) { output.SetSourcePlayable(p); p.SetTime(time); graph.Evaluate(); }
        var ghost = AugmentFx.Ghost(sr, CPlunder, 99f, 0.85f, true);
        if (ghost != null) ghost.hold = true;

        // ── 1. 쫓아가기: 그 적의 이동 동작 · 이동 속도로 가장 가까운 적에게 다가감 (최대 3초) ──
        float chase = 0f, mt = 0f;
        while (root != null && chase < 3f)
        {
            if (target == null || target.IsDead) target = Nearest(pos, 12f);
            if (target == null) break;
            float dx = target.transform.position.x - pos.x;
            if (Mathf.Abs(dx) > 0.05f) face = Mathf.Sign(dx);
            if (Mathf.Abs(dx) <= sp.range) break;   // 공격 거리 안 → 공격
            if (!Paused)
            {
                float step = Mathf.Min(Mathf.Abs(dx) - sp.range * 0.8f, sp.speed * Time.deltaTime);
                pos.x += face * Mathf.Max(0f, step);
                chase += Time.deltaTime; mt += Time.deltaTime;
            }
            if (!Paused) Ground(false);
            if (moveCopy != null) Sample(movePlayable, Mathf.Repeat(mt, Mathf.Max(0.05f, moveCopy.length)));
            else Sample(clipPlayable, 0f);
            Pose();
            yield return null;
        }
        if (target == null || target.IsDead || root == null) { chase = 99f; }

        // ── 2. 공격 1회 ──
        Sample(clipPlayable, 0f);
        Pose();

        var events = new List<AnimationEvent>(sp.clip.events);
        events.Sort((a, b) => a.time.CompareTo(b.time));
        int next = 0;
        bool dashing = false, dashStarted = false; float dashLeft = 0f;
        float t = 0f;
        while (chase < 99f && t <= len && root != null)
        {
            if (!Paused) t += Time.deltaTime;
            Sample(clipPlayable, Mathf.Min(t, len));
            // 클립이 위치 · 크기를 움직여도 홀로그램 자리 · 방향은 이쪽에서 정함
            if (dashing)
            {
                float step = Mathf.Min(dashLeft, sp.dashSpeed * 1.2f * Time.deltaTime);
                pos.x += face * step; dashLeft -= step;
                if (dashLeft <= 0f) dashing = false;
            }
            if (!Paused) Ground(false);
            Pose();

            if (sp.dash && !dashStarted && t >= sp.dashDelay && !HasEvent(events, "StartAttackDash"))
            { dashStarted = dashing = true; dashLeft = sp.dashDistance * 1.2f; }

            while (next < events.Count && events[next].time <= t)
            {
                switch (events[next].functionName)
                {
                    case "StartAttackDash":
                        if (sp.dash && !dashStarted) { dashStarted = dashing = true; dashLeft = sp.dashDistance * 1.2f; }
                        break;
                    case "SpawnHitbox":
                        HoloHit(sp, pos, face, target);
                        break;
                    case "SpawnAttackEffect":
                        if (sp.effect != null)
                        {
                            var fx = Instantiate(sp.effect, pos + new Vector3(sp.effectOffset.x * face, sp.effectOffset.y, 0f), Quaternion.identity);
                            if (sp.spawnScale != 1f) fx.transform.localScale *= sp.spawnScale;
                            if (face < 0f) { var s = fx.transform.localScale; fx.transform.localScale = new Vector3(-s.x, s.y, s.z); }
                            AugmentFx.Tint(fx, CPlunder, 1.6f);
                            Destroy(fx, 2f);
                        }
                        break;
                }
                next++;
            }
            yield return null;
        }
        // 사라짐: 머리부터 발끝까지 홀로그램 파편으로 흩어짐 (그동안 그림은 마지막 자세 그대로)
        if (ghost != null)
        {
            float feet = pos.y - sp.footOffset;
            var bodyBox = new Bounds(new Vector3(pos.x, feet + sp.bodySize.y * 0.5f, pos.z), new Vector3(sp.bodySize.x, sp.bodySize.y, 0.1f));
            yield return HoloDissolve(ghost, 0.6f, bodyBox);
        }
        if (graph.IsValid()) graph.Destroy();
        Destroy(clip);
        if (moveCopy != null) Destroy(moveCopy);
        if (root != null) Destroy(root);
        holoAlive--;
    }

    static readonly int CutYId = Shader.PropertyToID("_CutY");

    /// <summary>[2026-11-25] 머리부터 파편으로: 위에서 아래로 내려가는 선 위쪽은 안 그림(홀로그램 셰이더 _CutY), 선 자리에서 홀로그램 색 세모 파편이 흩어짐</summary>
    IEnumerator HoloDissolve(FxGhost ghost, float time, Bounds b)
    {
        var r = ghost != null ? ghost.Renderer : null;
        if (r == null) yield break;
        ghost.hold = true;
        // b = 몸 충돌체 범위 (머리 꼭대기 ~ 발). 그림이 몸보다 조금 위로 나와 있을 수 있어 시작선은 머리 위 약간
        b.Expand(new Vector3(0f, 0.15f, 0f));
        var mpb = new MaterialPropertyBlock();
        float t = 0f, shardT = 0f;
        while (t < time && r != null)
        {
            if (!Paused) t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            float lineY = Mathf.Lerp(b.max.y, b.min.y, k);
            r.GetPropertyBlock(mpb); mpb.SetFloat(CutYId, lineY); r.SetPropertyBlock(mpb);
            shardT -= Time.deltaTime;
            if (shardT <= 0f && !Paused)
            {
                shardT = 0.02f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = new Vector3(Random.Range(b.min.x + b.size.x * 0.2f, b.max.x - b.size.x * 0.2f), lineY, b.center.z);
                    Vector2 v = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(0.3f, 1.6f));
                    AugmentFx.Shard(p, v, Color.Lerp(CPlunder, Color.white, Random.Range(0f, 0.4f)), Random.Range(0.4f, 0.7f), Random.Range(0.1f, 0.2f));
                }
            }
            yield return null;
        }
        if (ghost != null) Destroy(ghost.gameObject);
    }

    static bool HasEvent(List<AnimationEvent> list, string fn)
    {
        foreach (var e in list) if (e.functionName == fn) return true;
        return false;
    }

    void HoloHit(HoloSpec sp, Vector3 pos, float face, Health target)
    {
        Vector3 hp = pos + new Vector3(sp.hitOffset.x * face, sp.hitOffset.y, 0f);
        if (sp.projectile != null)
        {
            StartCoroutine(HoloShot(sp, hp, target));
            return;
        }
        if (!sp.melee) return;
        foreach (var e in EnemiesInBox(hp, sp.hitSize))
        {
            AbilityHit(e, HoloRatio, pos, 3f, 1f);
            AugmentFx.Sparks(EC(e), CPlunder, 6, 3.5f, 0.25f, 0f, 0.13f);
        }
    }

    /// <summary>그 적의 투사체를 그대로 날림 — 원래 스크립트는 끄고 직접 움직여 맞힘 (역류 탄도와 같은 방식)</summary>
    IEnumerator HoloShot(HoloSpec sp, Vector3 from, Health target)
    {
        var go = Instantiate(sp.projectile, from, Quaternion.identity);
        if (sp.spawnScale != 1f) go.transform.localScale *= sp.spawnScale;
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
        AugmentFx.Tint(go, CPlunder, 1.6f);
        float speed = 18f, life = 2.5f;
        while (life > 0f && go != null)
        {
            if (Paused) { yield return null; continue; }
            Vector3 to = target != null && !target.IsDead ? EC(target) : go.transform.position + go.transform.right;
            Vector3 d = to - go.transform.position;
            if (d.magnitude < 0.7f && target != null && !target.IsDead)
            {
                AbilityHit(target, HoloRatio, go.transform.position, 3f, 1f);
                AugmentFx.Sparks(EC(target), CPlunder, 8, 4f, 0.3f, 0f, 0.14f);
                break;
            }
            go.transform.position += d.normalized * speed * Time.deltaTime;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            life -= Time.deltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ══════════════ 처형 5 · 균열 5 (맞힐 때) ══════════════

    readonly HashSet<Health> executing = new HashSet<Health>();

    /// <summary>약점 속성으로 때렸는지 (참격 → 관통 → 타격 → 참격 상성에서 유리)</summary>
    bool IsWeaknessHit(Stats target) =>
        pstats != null && target != null && Stats.GetAdvantageMultiplier(pstats.advantage, target.advantage) > 1f;

    void Keyword5OnHit(CombatEvents.HitInfo hi)
    {
        var h = hi.target;
        if (h == null || h.IsDead) return;

        // 과열 5: 과열 전염
        if (Kw(AugmentKeyword.Overheat) >= AugmentRules.Stage3) OverheatSpread(h, StatusOf(h));

        // 처형 5: 체력 10% 이하인 일반 적을 약점 속성으로 공격 → 즉시 처형
        var ts = hi.targetStats != null ? hi.targetStats : h.GetComponent<Stats>();
        if (Kw(AugmentKeyword.Execute) >= AugmentRules.Stage3 && !IsBoss(h) && ts != null && ts.maxHp > 0
            && ts.currentHp > 0 && ts.currentHp <= ts.maxHp * 0.10f && IsWeaknessHit(ts) && executing.Add(h))
            StartCoroutine(ExecuteFinish(h));

        // 균열 5: 치명타 → 바닥을 타고 금
        if (Kw(AugmentKeyword.Fracture) >= AugmentRules.Stage3 && hi.isCrit && Time.time >= crackReady && pc != null)
        {
            crackReady = Time.time + 0.8f;
            StartCoroutine(CrackWave(PFeet, PFacing, 0.5f));
        }
    }

    IEnumerator ExecuteFinish(Health h)
    {
        Vector3 at = EC(h);
        KFx("KW_Execute_Slash", at, 1.4f, 0.8f, Random.Range(-20f, 20f) + (PFacing < 0 ? 180f : 0f));
        CameraFollow.Shake(0.2f, 0.12f);
        // 아주 짧은 멈춤 — 제로포인트 · 메뉴처럼 이미 시간이 바뀐 상태면 건드리지 않음
        const float stopScale = 0.05f;
        bool stopped = false;
        if (Mathf.Approximately(Time.timeScale, 1f) && !Paused) { Time.timeScale = stopScale; stopped = true; }
        yield return new WaitForSecondsRealtime(0.08f);
        if (stopped && Mathf.Approximately(Time.timeScale, stopScale)) Time.timeScale = 1f;
        if (h != null && !h.IsDead) AbilityHit(h, 99f, PPos, 4f, 2f);
        executing.Remove(h);
    }

    // ══════════════ 균열 5: 바닥을 타고 뻗는 금 ══════════════

    float crackReady;

    IEnumerator CrackWave(Vector3 feet, int dir, float ratio)
    {
        var hit = new HashSet<Health>();
        for (int i = 1; i <= 5; i++)
        {
            Vector3 p = feet + new Vector3(dir * 0.9f * i, 0.25f, 0f);
            if (KFx("KW2_Fracture_Node", p, 1f, 1.3f, warn: false) == null) KFx("KW_Fracture_Crack", p, 0.6f, 1f);   // [v2] 금이 지나는 자리마다 빛 기둥 · 파편 · 불티
            foreach (var e in EnemiesInBox(p + Vector3.up * 0.4f, new Vector2(1.0f, 1.4f)))
                if (hit.Add(e)) { AbilityHit(e, ratio, feet, 1f, 0.5f); StatusOf(e).AddFracture(1); }
            yield return new WaitForSeconds(0.04f);
        }
    }

    // ══════════════ 보급 단검용 검집: 푸른 피격 참격 자국 ══════════════

    /// <summary>제로포인트 중 맞힌 자리에 푸른 피격 참격(sword_hit 색 바꿈)을 남기고 잠깐 뒤 멈춰 둠 — 풀릴 때 OnPauseEnd 가 지움</summary>
    GameObject SheathMark(Vector3 pos, float angle)
    {
        var go = KFx("KW2_Sheath_Hit", pos, 1f, 0f, angle, warn: false) ?? KFx("KW_Sheath_Hit", pos, 0.8f, 0f, angle);   // [v2] 짧고 날카로운 칼자국 + 피격
        if (go != null) AugmentFx.Run(FreezeAfter(go, 0.07f));
        return go;
    }

    static IEnumerator FreezeAfter(GameObject go, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (go == null) yield break;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Pause(true);
        foreach (var an in go.GetComponentsInChildren<Animator>(true)) an.speed = 0f;
    }

    void ResetKeyword5State()
    {
        bodyCur = 1f;
        if (pc != null) pc.visualScale = 1f;
        holoAlive = 0; executing.Clear(); spreadAt.Clear();
    }
}
