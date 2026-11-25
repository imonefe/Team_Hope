using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 증강 시험 방 (AugmentTestRoom 프리팹 맨 위 오브젝트에 붙음).
/// 개발 중 F8(AugmentOfferDirector)로 들어온다.
///
///  - 1 : 기획서 순서(일반 20 → 희귀 22 → 영웅 11 → 전설 6)대로 증강 하나를 준다.
///        다시 누르면 지금 시험하던 증강을 빼고(능력치·붙은 효과까지 되돌림) 다음 것을 준다.
///  - Shift + 1 : 지금 증강 강화 단계 올리기 (Lv1 → 2 → 3)
///  - Shift + 2 : 앞의 증강으로 돌아가기
///  - 허수아비: Dummy_Kill (죽음 판정 있음, 쓰러지면 잠시 뒤 다시 생김) · 그 양옆 Dummy_Side (범위 효과 확인용, 안 죽음)
///              · Dummy_Hit (안 죽음, 오래 때려 보는 용)
///  - 방을 나가면 시험 중이던 증강은 빠진다
/// </summary>
public class AugmentTestRoom : MonoBehaviour
{
    [Tooltip("기획서 순서대로")]
    public List<ItemData> augments = new List<ItemData>();
    [Tooltip("쓰러진 허수아비를 다시 만들 때 쓰는 프리팹 (TrainingDummy)")]
    public GameObject dummyPrefab;
    [Tooltip("죽는 허수아비 체력")]
    public int killableHp = 300;
    public float respawnDelay = 1.5f;
    [Header("[2026-11-21] 2키: 몬스터 소환 (근접 · 엘리트 · 원거리)")]
    public GameObject[] monsterPrefabs;
    [Tooltip("시험 방 기준 소환 위치 (몬스터 하나씩)")]
    public Vector2[] monsterSpawnOffsets = { new Vector2(14f, 1.5f), new Vector2(17f, 1.5f), new Vector2(20f, 1.5f) };
    public int maxMonsters = 9;
    readonly List<GameObject> monsters = new List<GameObject>();
    readonly List<ItemData> companions = new List<ItemData>();   // 함께 준 증강 (빼낼 때 같이 뺌)

    // 다른 증강이 있어야 보이는 증강 → 시험할 때 함께 주는 증강 (고유능력 이름 기준)
    static readonly Dictionary<string, string[]> Companions = new Dictionary<string, string[]>
    {
        { "연쇄 발화", new[] { "예열 코일" } },
        { "붕괴 탄두", new[] { "예열 코일" } },
        { "남은 불씨", new[] { "예열 코일" } },
        { "번제", new[] { "예열 코일" } },
        { "수집가의 불꽃", new[] { "예열 코일", "기억 갈고리" } },
        { "쐐기 각인", new[] { "균열 탐침" } },
    };

    int index = -1;
    ItemData current;
    Snapshot snap;
    readonly List<Component> addedComps = new List<Component>();
    // [2026-11-21 수정] 다시 세울 자리를 따로 기억 (예전엔 허수아비 자신의 위치를 들고 있어서, 쓰러지면 위치도 같이 사라져 다시 안 생겼음)
    Vector3 killPos; Vector3 killScale = Vector3.one; Transform dummiesRoot;
    bool hasKillPoint;
    GameObject killDummy;
    bool respawning;

    struct Snapshot
    {
        public bool valid;
        public float atk, def, spd, itemSpd, pause, cc, cm;
        public int maxHp, hp;
    }

    void Start()
    {
        dummiesRoot = transform.Find("Dummies");
        var kp = transform.Find("Dummies/Dummy_Kill");
        if (kp != null)
        {
            hasKillPoint = true;
            killPos = kp.position; killScale = kp.localScale;
            killDummy = kp.gameObject; MakeKillable(killDummy);
        }
        ScreenNotice.Show("사이버웨어 시험 방 — 1 다음 사이버웨어 · Shift+1 강화 · Shift+2 이전 · 2 몬스터 3마리 · 3 체력 40%/가득 · 4 방 정리 · Shift+4 방 입장");
    }

    void MakeKillable(GameObject go)
    {
        var td = go.GetComponent<TrainingDummy>();
        if (td != null) td.enabled = false;      // 체력을 다시 채우지 않게 → 진짜로 쓰러짐
        var st = go.GetComponent<Stats>();
        if (st != null) { st.maxHp = killableHp; st.currentHp = killableHp; }
    }

    void Update()
    {
        if (killDummy == null && !respawning && hasKillPoint) StartCoroutine(Respawn());
        if (GamePause.IsPaused) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (shift) LevelUp();
            else Step(+1);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (shift) Step(-1);
            else SpawnMonsters();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleHp();
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (shift) AugmentRuntime.Instance?.TestRoomEntered();   // 방에 들어온 것처럼 (약점 스캔 등)
            else AugmentRuntime.Instance?.TestRoomCleared();         // 방을 정리한 것처럼 (떠올림 · 비상식량 등)
        }
    }

    IEnumerator Respawn()
    {
        respawning = true;
        yield return new WaitForSeconds(respawnDelay);
        if (dummyPrefab != null)
        {
            killDummy = Instantiate(dummyPrefab, killPos, Quaternion.identity, dummiesRoot != null ? dummiesRoot : transform);
            killDummy.transform.position = killPos;
            killDummy.transform.localScale = killScale;
            killDummy.name = "Dummy_Kill";
            MakeKillable(killDummy);
            // 다시 서는 연출 (글자 없음): 발밑 파동 + 위로 흩어지는 불티
            AugmentFx.Ring(killPos + Vector3.up * 0.1f, new Color(0.85f, 0.92f, 1f), 1.6f, 0.4f, 0.08f, 2, 0.4f);
            AugmentFx.Sparks(killPos + Vector3.up * 0.8f, new Color(0.85f, 0.92f, 1f), 10, 2.5f, 0.5f, -2f, 0.14f);
        }
        respawning = false;
    }

    // 2키: 근접 · 엘리트 · 원거리 몬스터를 한 마리씩 (적 투사체 · 적 공격 · 피격 증강 시험용)
    void SpawnMonsters()
    {
        monsters.RemoveAll(m => m == null);
        if (monsterPrefabs == null) return;
        for (int i = 0; i < monsterPrefabs.Length && monsters.Count < maxMonsters; i++)
        {
            if (monsterPrefabs[i] == null) continue;
            Vector2 o = monsterSpawnOffsets != null && i < monsterSpawnOffsets.Length ? monsterSpawnOffsets[i] : new Vector2(14f + i * 3f, 1.5f);
            Vector3 pos = transform.position + (Vector3)o;
            var m = Instantiate(monsterPrefabs[i], pos, Quaternion.identity, transform);
            monsters.Add(m);
            AugmentFx.Ring(pos, new Color(1f, 0.45f, 0.4f), 1.4f, 0.35f, 0.08f, 2, 0.4f);
        }
    }

    // 3키: 체력 40% ↔ 가득 (과부하 · 체력 조건 증강 시험용)
    void ToggleHp()
    {
        var st = PC != null ? PC.GetComponent<Stats>() : null;
        if (st == null) return;
        int low = Mathf.Max(1, Mathf.RoundToInt(st.maxHp * 0.4f));
        if (st.currentHp > low) st.TakeDamage(st.currentHp - low);
        else st.Heal(st.maxHp);
    }

    void Step(int dir)
    {
        if (augments.Count == 0) return;
        Remove();
        index = (index + dir + augments.Count) % augments.Count;
        Grant(augments[index]);
    }

    static PlayerController PC => PlayerController.Instance;

    void Grant(ItemData d)
    {
        if (d == null || PC == null) return;
        var st = PC.GetComponent<Stats>();
        snap = new Snapshot
        {
            valid = st != null,
            atk = st.Atk, def = st.Def, spd = st.spd, itemSpd = st.itemSpdBonus, pause = st.PauseDuration,
            cc = st.critChance, cm = st.critMultiplier, maxHp = st.maxHp, hp = st.currentHp
        };
        var before = new HashSet<Component>(PC.GetComponents<Component>());
        if (d.effects != null)
            foreach (var e in d.effects)
                if (e != null) e.Apply(st, PC);
        current = d;
        RunManager.Instance?.RegisterObtained(d);
        RunManager.Instance?.SetAugmentLevel(d, 1);
        Inventory.Instance?.AddPassive(d);
        // 다른 증강이 있어야 보이는 증강이면 그 증강도 함께
        companions.Clear();
        string extra = "";
        if (!string.IsNullOrEmpty(d.abilityName) && Companions.TryGetValue(d.abilityName, out var need))
            foreach (var n in need)
            {
                var c = augments.Find(a => a != null && a.abilityName == n);
                if (c == null || c == d) continue;
                if (c.effects != null) foreach (var e in c.effects) if (e != null) e.Apply(st, PC);
                RunManager.Instance?.RegisterObtained(c);
                RunManager.Instance?.SetAugmentLevel(c, 1);
                Inventory.Instance?.AddPassive(c);
                companions.Add(c);
                extra += $" + {c.itemName}";
            }
        addedComps.Clear();
        foreach (var c in PC.GetComponents<Component>()) if (!before.Contains(c)) addedComps.Add(c);
        ScreenNotice.Show($"[{index + 1}/{augments.Count}] {AugmentRules.TierName(d.tier)} · {d.itemName}{extra}");
    }

    void LevelUp()
    {
        if (current == null || RunManager.Instance == null || PC == null) return;
        int lv = Mathf.Max(1, RunManager.Instance.GetAugmentLevel(current));
        if (lv >= AugmentRules.MaxLevel) { ScreenNotice.Show($"{current.itemName} 이미 Lv{lv}"); return; }
        RunManager.Instance.SetAugmentLevel(current, lv + 1);
        if (current.effects != null)
            foreach (var e in current.effects)
                if (e is IAugmentLevelEffect le) le.OnAugmentLevelChanged(PC.GetComponent<Stats>(), PC, lv, lv + 1);
        AugmentRuntime.Instance?.Refresh();
        ScreenNotice.Show($"{current.itemName} Lv{lv + 1} ({AugmentRules.LevelValue(current, lv + 1)})");
    }

    void Remove()
    {
        if (current == null) return;
        Inventory.Instance?.RemovePassiveAt(-1, current);
        RunManager.Instance?.ForgetObtained(current);
        foreach (var c in companions) { Inventory.Instance?.RemovePassiveAt(-1, c); RunManager.Instance?.ForgetObtained(c); }
        companions.Clear();
        foreach (var c in addedComps) if (c != null) Destroy(c);
        addedComps.Clear();
        if (snap.valid && PC != null)
        {
            var st = PC.GetComponent<Stats>();
            st.Atk = snap.atk; st.Def = snap.def; st.spd = snap.spd; st.itemSpdBonus = snap.itemSpd; st.PauseDuration = snap.pause;
            st.critChance = snap.cc; st.critMultiplier = snap.cm; st.maxHp = snap.maxHp; st.currentHp = Mathf.Min(snap.hp, snap.maxHp);
        }
        current = null;
        snap.valid = false;
        AugmentRuntime.Instance?.Refresh();
    }

    void OnDestroy() => Remove();
}
