using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-11-26] 스탯 카드 기본 세트 (일반 14장 + 대가 6장) — 제로포인트 지속시간은 사이버웨어에서만 조절하므로 카드에서 뺌 와 Resources/StatCardPool 을 만든다.
///  이미 있는 카드 에셋은 건드리지 않음 (직접 고친 수치 보존) — 없는 것만 새로 만들고 풀 목록에 넣는다.
///  메뉴 EventHorizon/스탯 카드 기본 세트 만들기 (프로젝트 폴더에 stat_cards.trigger 가 있으면 자동 1회)
/// </summary>
[InitializeOnLoad]
public static class StatCardSetBuilder
{
    const string TriggerFile = "stat_cards.trigger";
    const string Dir = "Assets/3. Data/StatCard";
    const string PoolDir = Dir + "/Resources";
    const string PoolPath = PoolDir + "/StatCardPool.asset";

    static StatCardSetBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(TriggerFile)) return;
            File.Delete(TriggerFile);
            Build();
        };
    }

    struct Def
    {
        public string name; public ItemTier tier; public StatModifier m;
        public Def(string n, ItemTier t, StatModifier mod) { name = n; tier = t; m = mod; }
    }

    static StatModifier M(float atkP = 0, float hpP = 0, float spdP = 0, float def = 0, float crit = 0, float critMul = 0, float pause = 0)
        => new StatModifier { atkPercent = atkP, hpPercent = hpP, spdPercent = spdP, def = def, critChance = crit, critMultiplier = critMul, pauseDuration = pause };

    // 수치 근거 (2026-11-26): 전투방 10칸 → 8칸이 차면 남은 방은 스탯 방 → 한 판에 스탯 카드 약 5장.
    // 지금 사이버웨어 8개가 주던 스탯 (공격력 약 +50% · 최대 체력 약 +38% · 치명타 약 +8%p) 중 절반 이상을 카드 5장이 채우도록.
    static readonly Def[] Set =
    {
        // ── 일반 ──
        new Def("날 선 감각",   ItemTier.Common, M(atkP: 0.10f)),
        new Def("강화 골격",    ItemTier.Common, M(hpP: 0.15f)),
        new Def("경량화",       ItemTier.Common, M(spdP: 0.05f)),
        new Def("장갑 보강",    ItemTier.Common, M(def: 2f)),
        new Def("조준 보정",    ItemTier.Common, M(crit: 0.04f)),
        // ── 희귀 ──
        new Def("전투 본능",    ItemTier.Rare, M(atkP: 0.15f)),
        new Def("생체 강화",    ItemTier.Rare, M(hpP: 0.22f)),
        new Def("반사 신경",    ItemTier.Rare, M(spdP: 0.08f)),
        new Def("급소 분석",    ItemTier.Rare, M(crit: 0.06f)),
        new Def("파괴 회로",    ItemTier.Rare, M(critMul: 0.25f)),
        new Def("균형 조정",    ItemTier.Rare, M(atkP: 0.08f, hpP: 0.10f)),
        // ── 영웅 ──
        new Def("살상 규약",    ItemTier.Epic, M(atkP: 0.22f)),
        new Def("철갑 육체",    ItemTier.Epic, M(hpP: 0.30f, def: 3f)),
        new Def("정밀 타격",    ItemTier.Epic, M(crit: 0.08f, critMul: 0.25f)),
        // ── 대가 (장점 ≈ 같은 등급 일반 카드의 1.8배, 단점 하나) ──
        new Def("광폭화",       ItemTier.Common, M(atkP: 0.18f, hpP: -0.10f)),
        new Def("얇은 장갑",    ItemTier.Common, M(spdP: 0.09f, def: -2f)),
        new Def("비대 근육",    ItemTier.Rare, M(hpP: 0.40f, spdP: -0.06f)),
        new Def("과열 회로",    ItemTier.Rare, M(atkP: 0.27f, def: -3f)),
        new Def("한계 돌파",    ItemTier.Epic, M(critMul: 0.45f, crit: -0.03f)),
        new Def("유리 대포",    ItemTier.Epic, M(atkP: 0.40f, hpP: -0.20f)),
    };

    [MenuItem("EventHorizon/스탯 카드 기본 세트 만들기")]
    public static void Build()
    {
        Directory.CreateDirectory(PoolDir);
        AssetDatabase.Refresh();

        var pool = AssetDatabase.LoadAssetAtPath<StatCardPool>(PoolPath);
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<StatCardPool>();
            AssetDatabase.CreateAsset(pool, PoolPath);
        }

        int made = 0;
        foreach (var d in Set)
        {
            string path = $"{Dir}/{d.name}.asset";
            var card = AssetDatabase.LoadAssetAtPath<StatCardData>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<StatCardData>();
                card.cardName = d.name;
                card.tier = d.tier;
                card.modifier = d.m;
                AssetDatabase.CreateAsset(card, path);
                made++;
            }
            if (!pool.cards.Contains(card)) pool.cards.Add(card);
        }
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();
        Debug.Log($"[스탯 카드] 새로 만듦 {made}장 · 풀 {pool.cards.Count}장 — {PoolPath}");
    }
}
