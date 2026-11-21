using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가 · 수정] 적에게 붙는 증강 상태 — 과열 · 균열 스택, 처형 표시, 머리 위 상태 띠.
/// 증강 능력이 처음 상태를 붙일 때 적 오브젝트에 자동으로 붙는다 (AugmentRuntime.StatusOf).
///
///  - 과열 (상한 없음): 스택당 0.5초마다 공격력(헤네시) 2% 피해 = 초당 4%.
///      새로 쌓이지 않고 4초(과열 키워드 3개면 6초)가 지나면 절반이 사라지고, 다시 그만큼 기다리면 또 절반.
///      '과열 폭발' 계열은 5스택 이상(AugmentRuntime.OverheatBurst)을 조건으로 쓴다.
///  - 균열: 스택당 받는 피해 +5%. 지속 6초, 최대 3(균열 3개면 4, 약점 스캔이면 더)
///  - 상태 띠: 키워드 아이콘을 가로로 늘어놓고, 스택 수는 아이콘 오른쪽 아래 작은 숫자. 처형 대상이면 처형 아이콘(숫자 없음).
///    균열이 최대이거나 과열이 폭발 조건 이상이면 아이콘이 맥동. 위치는 AugmentFx.Offsets.enemyBadge 로 조절.
///  - 과열이 쌓이는 순간: 흰색으로 아주 짧게 반짝 (피격 동작 없음). 균열: 보라 번쩍 + 금 조각
/// </summary>
public class EnemyStatus : MonoBehaviour
{
    public int overheat, fracture;
    float overheatTimer, fractureTimer, tickTimer;
    public float lastProbe = -99f;          // 균열 탐침 재사용 대기
    public int hitCounter;                  // 잠재우기 (세 번째 맞힘)
    public bool lastHitWasExecute;          // 마지막으로 맞을 때 처형 대상이었는지
    public float explodeImmuneUntil;        // 과열 폭발 연쇄 방지
    public float shieldBreakReadyAt;        // 백자 열람: 방패 무효 재사용 (같은 적 8초)
    public bool weakPoint;                  // 약점 스캔: 균열이 사라지지 않고 최대 증가
    public int weakExtra;
    public int horizonStored;               // 사건의 지평선: 퍼즈 중 저장된 피해
    public bool chainArmed;                 // 연쇄 발화: 폭발 예고 중

    /// <summary>과열 스택이 바뀜 (상태, 이전, 지금)</summary>
    public static event Action<EnemyStatus, int, int> OverheatChanged;

    Health health;
    Stats stats;
    Collider2D col;
    Transform badge;
    readonly List<Slot> slots = new List<Slot>();
    bool badgeBuilt;
    float lastFlash;
    Transform hole; SpriteRenderer holeR;

    class Slot
    {
        public Transform root;
        public SpriteRenderer icon;
        public TextMeshPro count;
        public bool pulse;
    }

    public Health Health => health;
    public Stats Stats => stats;
    public bool IsBoss => GetComponent<BossController>() != null;
    public int FractureMaxHere => AugmentRuntime.FractureMax + (weakPoint ? weakExtra : 0);
    /// <summary>과열 폭발 조건 (5스택 이상)</summary>
    public bool IsHot => overheat >= AugmentRuntime.OverheatBurst;
    public bool IsMaxFracture => fracture > 0 && fracture >= FractureMaxHere;
    public float HpRatio => stats != null && stats.maxHp > 0 ? (float)stats.currentHp / stats.maxHp : 1f;

    void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<Stats>();
        col = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
    }

    public void AddOverheat(int n)
    {
        if (n <= 0 || health == null || health.IsDead) return;
        if (AugmentRuntime.Instance != null) n = AugmentRuntime.Instance.OverheatGainFor(this, n);   // 붕괴 탄두: 균열 최대면 2배
        int before = overheat;
        overheat += n;
        overheatTimer = AugmentRuntime.OverheatDuration;
        if (Time.time - lastFlash >= 0.06f)
        {
            lastFlash = Time.time;
            var e = GetComponent<Enemy>();
            if (e != null) e.TintFlash(Color.white, 0.85f, 0.06f);   // 흰색 순간 반짝임만 (피격 동작 없음)
        }
        RefreshBadge();
        Raise(before, overheat);
    }

    void Raise(int before, int after)
    {
        if (OverheatChanged == null || before == after) return;
        try { OverheatChanged(this, before, after); } catch (Exception ex) { Debug.LogException(ex); }
    }

    public void AddFracture(int n)
    {
        if (n <= 0 || health == null || health.IsDead) return;
        int before = fracture;
        fracture = Mathf.Min(FractureMaxHere, fracture + n);
        fractureTimer = AugmentRuntime.FractureDuration;
        if (Time.time - lastFlash >= 0.08f)
        {
            lastFlash = Time.time;
            AugmentFx.BodyFlash(this, AugmentFx.KwColor(AugmentKeyword.Fracture), Mathf.Lerp(0.35f, 0.85f, fracture / (float)FractureMaxHere), 0.22f);
        }
        if (fracture > before) AugmentFx.Sparks(Top(), AugmentFx.KwColor(AugmentKeyword.Fracture), 2 + (fracture - before) * 2, 2.5f, 0.3f, 4f, 0.12f);   // 금 조각
        RefreshBadge();
    }

    public void SetFractureFull()
    {
        if (health == null || health.IsDead) return;
        fracture = FractureMaxHere;
        fractureTimer = AugmentRuntime.FractureDuration;
        AugmentFx.BodyFlash(this, AugmentFx.KwColor(AugmentKeyword.Fracture), 0.85f, 0.22f);
        RefreshBadge();
    }

    public void ClearFracture() { fracture = 0; RefreshBadge(); }

    public Bounds Body => col != null ? col.bounds : new Bounds(transform.position + Vector3.up, new Vector3(1f, 2f, 0f));
    public Vector3 Center => Body.center + (Vector3)AugmentFx.Offsets.enemyCenter;
    public Vector3 Top() { var b = Body; return new Vector3(b.center.x, b.max.y, transform.position.z - 0.5f); }

    void Update()
    {
        if (health == null || health.IsDead) { if (badge != null) badge.gameObject.SetActive(false); if (hole != null) Destroy(hole.gameObject); return; }
        UpdateHole();
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) { if (badge != null && badge.gameObject.activeSelf) { badge.position = BadgePos(); UpdateBadgeAnim(); } return; }   // 퍼즈 중엔 적의 시간도 멈춤

        bool changed = false;
        if (overheat > 0)
        {
            overheatTimer -= Time.deltaTime;
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = 0.5f;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(AugmentRuntime.PlayerAtk * 0.02f * overheat));
                FloatingTextSpawner.SpawnText(Center, dmg.ToString(), AugmentRuntime.OverheatColor);   // 피해 숫자
                health.TakeDotDamage(dmg);
                if (health.IsDead) return;
            }
            // 새로 쌓이지 않으면 절반씩 줄어듦 (상한 대신)
            if (overheatTimer <= 0f)
            {
                int b = overheat;
                overheat = overheat <= 1 ? 0 : overheat / 2;
                overheatTimer = AugmentRuntime.OverheatDuration;
                changed = true;
                Raise(b, overheat);
            }
        }
        if (fracture > 0 && !weakPoint)
        {
            fractureTimer -= Time.deltaTime;
            if (fractureTimer <= 0f) { fracture = 0; changed = true; }
        }
        bool exec = ShowExecute();
        if (exec != execShown) changed = true;
        if (changed) RefreshBadge();
        if (badge != null && badge.gameObject.activeSelf) { badge.position = BadgePos(); UpdateBadgeAnim(); }
    }

    bool execShown;
    bool ShowExecute() => AugmentRuntime.Instance != null && AugmentRuntime.Instance.ShowsExecuteMark(health);

    Vector3 BadgePos()
    {
        var b = Body;
        Vector2 o = AugmentFx.Offsets.enemyBadge;
        return new Vector3(b.center.x + o.x, b.max.y + o.y, transform.position.z - 0.5f);
    }

    // ── 상태 띠: 아이콘 가로 정렬 + 아이콘 오른쪽 아래 숫자 ──
    public void RefreshBadge()
    {
        execShown = ShowExecute();
        bool any = overheat > 0 || fracture > 0 || execShown;
        if (!any) { if (badge != null) badge.gameObject.SetActive(false); return; }
        if (!badgeBuilt) BuildBadge();
        badge.gameObject.SetActive(true);
        int used = 0;
        if (overheat > 0) SetSlot(used++, AugmentKeyword.Overheat, overheat.ToString(), IsHot);
        if (fracture > 0) SetSlot(used++, AugmentKeyword.Fracture, fracture.ToString(), IsMaxFracture);
        if (execShown) SetSlot(used++, AugmentKeyword.Execute, "", AugmentRuntime.Instance != null && AugmentRuntime.Instance.InstantKillReady(health));
        for (int i = used; i < slots.Count; i++) slots[i].root.gameObject.SetActive(false);
        const float gap = 0.5f;
        for (int i = 0; i < used; i++) slots[i].root.localPosition = new Vector3((i - (used - 1) * 0.5f) * gap, 0f, 0f);
        badge.position = BadgePos();
    }

    void SetSlot(int i, AugmentKeyword k, string count, bool pulse)
    {
        while (slots.Count <= i) slots.Add(MakeSlot(slots.Count));
        var s = slots[i];
        s.root.gameObject.SetActive(true);
        s.icon.sprite = AugmentRuntime.KeywordIcon(k);
        Color c = AugmentFx.KwColor(k);
        if (s.icon.sprite == null) { s.icon.sprite = AugmentFx.Diamond; s.icon.color = c; } else s.icon.color = Color.white;
        s.count.text = count;
        // 숫자는 아이콘 오른쪽 아래 모서리에서 바깥쪽으로 (아이콘과 겹치지 않게)
        var ext = s.icon.sprite != null ? s.icon.sprite.bounds.extents * 0.3f : new Vector3(0.15f, 0.15f, 0f);
        s.count.transform.localPosition = new Vector3(ext.x * 0.8f, -ext.y * 0.55f, -0.01f);
        s.count.color = Color.Lerp(c, Color.white, 0.35f);
        s.pulse = pulse;
    }

    void UpdateBadgeAnim()
    {
        float p = 1f + 0.18f * Mathf.Sin(Time.time * 10f);
        foreach (var s in slots)
            if (s.root.gameObject.activeSelf) s.icon.transform.localScale = Vector3.one * 0.3f * (s.pulse ? p : 1f);
    }

    void BuildBadge()
    {
        badgeBuilt = true;
        badge = new GameObject("AugStatus").transform;
        badge.SetParent(transform, true);
        badge.gameObject.AddComponent<KeepUprightBadge>();   // 적이 좌우로 뒤집혀도 표시는 뒤집히지 않게
    }

    Slot MakeSlot(int i)
    {
        var s = new Slot();
        s.root = new GameObject("Slot" + i).transform;
        s.root.SetParent(badge, false);
        s.icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        s.icon.transform.SetParent(s.root, false);
        s.icon.transform.localScale = Vector3.one * 0.3f;
        s.icon.sortingOrder = 5152;
        s.count = new GameObject("Count").AddComponent<TextMeshPro>();
        s.count.transform.SetParent(s.root, false);
        s.count.transform.localPosition = new Vector3(0.17f, -0.1f, -0.01f);   // 아이콘 오른쪽 아래 모서리 바깥 (SetSlot 에서 아이콘 크기에 맞춰 다시 잡음)
        if (AugmentRuntime.Font != null) s.count.font = AugmentRuntime.Font;
        s.count.fontSize = 1.5f;
        s.count.alignment = TextAlignmentOptions.TopLeft;
        s.count.rectTransform.pivot = new Vector2(0f, 1f);
        s.count.rectTransform.sizeDelta = new Vector2(0.5f, 0.25f);
        s.count.fontStyle = FontStyles.Bold;
        s.count.outlineWidth = 0.3f;
        s.count.outlineColor = Color.black;
        s.count.sortingOrder = 5153;
        return s;
    }

    // ── 사건의 지평선: 머리 위 검은 구멍 (저장된 피해만큼 커짐) ──
    void UpdateHole()
    {
        if (horizonStored <= 0) { if (hole != null) { Destroy(hole.gameObject); hole = null; } return; }
        if (hole == null)
        {
            holeR = new GameObject("FxHorizon").AddComponent<SpriteRenderer>();
            holeR.sprite = AugmentFx.Dot;
            if (AugmentFx.SpriteMat != null) holeR.sharedMaterial = AugmentFx.SpriteMat;
            holeR.sortingOrder = 5149;
            hole = holeR.transform;
            var ring = AugmentFx.Circle(hole, 0.3f, new Color(0.75f, 0.7f, 1f, 0.9f), 0.04f);
            ring.transform.SetParent(hole, true);
        }
        float ratio = stats != null && stats.maxHp > 0 ? Mathf.Clamp01(horizonStored / (float)stats.maxHp) : 0.3f;
        float size = Mathf.Lerp(0.35f, 1.4f, Mathf.Sqrt(ratio)) * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 12f));
        hole.position = BadgePos() + Vector3.up * (0.45f + size * 0.3f);
        hole.localScale = Vector3.one * size;
        holeR.color = new Color(0.02f, 0f, 0.06f, 0.92f);
        var ringC = hole.GetComponentInChildren<FxCircle>();
        if (ringC != null) ringC.radius = size * 0.42f;
    }
}

/// <summary>부모가 좌우 반전(scale.x &lt; 0)돼도 표시가 거울처럼 뒤집히지 않게</summary>
public class KeepUprightBadge : MonoBehaviour
{
    void LateUpdate()
    {
        var p = transform.parent;
        if (p == null) return;
        Vector3 ls = p.lossyScale;
        transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)) * Mathf.Sign(ls.x), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
        transform.rotation = Quaternion.identity;
    }
}
