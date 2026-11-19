using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-19 신규] 참격(검사) 중간보스 — 테스트판 (애니메이션·이펙트 없음, 색 박스 + 머리 위 글자).
///
/// 패턴 1 발도술: 자세 → 발도 3번을 나눠서 하나씩. 매번 그 순간 플레이어 위치에 선이 나타나고(경고) 그 선을 벤다. 마지막은 강한 참격.
/// 패턴 2 검기:   플레이어를 바라보고, 그 순간 플레이어 위치로 직선 검기 (추격 없음).
/// 패턴 3 패리:   패리 자세 동안 맞으면 피해 0 → 플레이어를 지나는 선을 짧게 경고 → 화면 가르기 (닿으면 큰 피해).
/// 패턴 4 난무:   차징(무방비). 그동안 일정 피해를 넣으면 자세 붕괴 → 취약(받는 피해 증가).
///                실패하면 방을 날뛰며 참격선 8개를 경고로 배치 → 8줄이 동시에 화면을 가르며 타격 (ScreenSlashMultiLine).
/// 화면 가르기는 검사 처형 컷신과 같은 ScreenSlashEffect. 실제 판정은 같은 각도의 선.
/// </summary>
public class SwordsmanMidBoss : MidBossTestBase
{
    [Header("[ 패턴 1 — 발도술 (3번 나눠서 순서대로) ]")]
    public float iaiStanceTime = 0.6f;
    [Tooltip("발도 횟수 (마지막이 강한 발도)")]
    public int iaiDraws = 3;
    [Tooltip("발도마다: 플레이어 위치에 선이 나타나고 베기까지 (경고 시간)")]
    public float iaiDrawWarn = 0.45f;
    [Tooltip("마지막 강한 발도의 경고 시간")]
    public float iaiFinalWarn = 0.6f;
    [Tooltip("발도 사이 간격")]
    public float iaiDrawGap = 0.15f;
    public float iaiLineLength = 14f;
    public float iaiLineThickness = 1f;
    public float iaiFinalThickness = 1.5f;
    public float iaiDamage = 0.8f;
    public float iaiFinalDamage = 1.6f;

    [Header("[ 패턴 2 — 검기 ]")]
    public float waveWindup = 0.4f;
    public float waveSpeed = 13f;
    public Vector2 waveSize = new Vector2(0.6f, 2.4f);
    public float waveHitRadius = 0.9f;
    public float waveLife = 3f;
    public float waveDamage = 1f;

    [Header("[ 패턴 3 — 패리 ]")]
    public float parryTime = 1.6f;
    [Tooltip("반격 선 경고 시간 — 이 안에 선 밖으로 피해야 함")]
    public float counterWarnTime = 0.5f;
    public float counterThickness = 1.6f;
    public float counterDamage = 2.2f;

    [Header("[ 패턴 4 — 난무 ]")]
    public float rampageChargeTime = 3f;
    [Tooltip("차징 중 이 비율(최대 체력 기준) 이상 깎으면 자세 붕괴")]
    [Range(0f, 1f)] public float rampageBreakRatio = 0.12f;
    public float vulnerableTime = 3.5f;
    [Tooltip("취약 상태에서 받는 피해 배율")]
    public float vulnerableDamageMul = 1.5f;
    public int rampageLines = 8;
    public float rampageHopTime = 0.15f;
    [Tooltip("마지막 선을 배치한 뒤 동시 타격까지")]
    public float rampageWarnTime = 1f;
    public float rampageThickness = 1.2f;
    public float rampageDamage = 2f;

    [Header("[ 색 ]")]
    public Color warnColor = new Color(1f, 0.85f, 0.3f, 1f);
    public Color parryColor = new Color(0.35f, 0.6f, 1f, 1f);
    public Color chargeColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color vulnerableColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color slashColor = new Color(1f, 1f, 1f, 0.95f);

    const int P_IAI = 0, P_WAVE = 1, P_PARRY = 2, P_RAMPAGE = 3;

    bool parrying, parried;

    protected override int GetPatternCount() => 4;

    protected override IEnumerator ExecutePattern(int index)
    {
        if (player == null) yield break;
        state = BossState.Attacking;
        rigid.velocity = new Vector2(0f, rigid.velocity.y);
        switch (index)
        {
            case P_IAI: yield return IaiPattern(); break;
            case P_WAVE: yield return WavePattern(); break;
            case P_PARRY: yield return ParryPattern(); break;
            default: yield return RampagePattern(); break;
        }
        ResetBody();
        SetLabel("");
    }

    // ───────────────────────── 패턴 1 — 발도술 ─────────────────────────
    // 자세 → 발도를 3번 나눠서 하나씩: 매번 그 순간 플레이어 위치에 선이 나타나고(경고) 그 선을 벤다. 마지막은 강한 참격 (굵고 세게)
    IEnumerator IaiPattern()
    {
        FacePlayer();
        SetLabel("발도술 자세", warnColor);
        yield return PausableWait(iaiStanceTime);

        int n = Mathf.Max(1, iaiDraws);
        for (int i = 0; i < n; i++)
        {
            bool final = i == n - 1;
            FacePlayer();
            Vector3 p = PlayerCenter();                                  // 지금 플레이어 위치
            float ang = Random.Range(-75f, 75f);
            float thick = final ? iaiFinalThickness : iaiLineThickness;
            float warn = final ? iaiFinalWarn : iaiDrawWarn;
            SetLabel(final ? "발도 — 강하게" : $"발도 {i + 1}/{n}", warnColor);
            WarnBox(p, new Vector2(iaiLineLength, thick), ang, warn);
            yield return PausableWait(warn);

            SetLabel(final ? "발도!" : $"발도 {i + 1}/{n}", Color.white);
            Shape(shapePrefab, p, new Vector2(iaiLineLength, thick * 0.4f), ang, slashColor, 4120, 0.1f, 0.2f);
            if (PlayerOnLine(p, ang, thick)) Hit(final ? iaiFinalDamage : iaiDamage, 0, final ? 7f : 4f, final ? 4f : 2f, PlayerCenter(), "Swordsman_Iai");
            if (!final) yield return PausableWait(iaiDrawGap);
        }
        yield return PausableWait(0.5f);
    }

    // ───────────────────────── 패턴 2 — 검기 ─────────────────────────
    IEnumerator WavePattern()
    {
        FacePlayer();
        SetLabel("검기", warnColor);
        yield return PausableWait(waveWindup);

        Vector3 from = BodyCenter() + new Vector3(0.8f * lockedDir, 0f, 0f);
        Vector2 dir = (Vector2)(PlayerCenter() - from);   // 지금 플레이어 위치로 (이후 추격 없음)
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        GameObject wave = Shape(shapePrefab, from, waveSize, ang, new Color(0.75f, 0.95f, 1f, 0.95f), 4110, -1f, 0f);
        SetLabel("");
        StartCoroutine(MoveProjectile(wave, dir, waveSpeed, 0f, waveLife, waveHitRadius, waveDamage, "Swordsman_Wave", true));
        yield return PausableWait(0.4f);
    }

    // ───────────────────────── 패턴 3 — 패리 ─────────────────────────
    IEnumerator ParryPattern()
    {
        FacePlayer();
        SetLabel("패리", parryColor);
        SetBodyColor(parryColor);
        System.Func<bool> prev = health.invulnerable;
        parrying = true; parried = false;
        health.invulnerable = () => parrying || (prev != null && prev());

        yield return WaitOrUntil(parryTime, () => parried);

        parrying = false;
        health.invulnerable = prev;
        if (!parried) { SetLabel(""); yield return PausableWait(0.3f); yield break; }

        // 반격: 보스 → 플레이어 방향의 선을 짧게 경고한 뒤 화면 가르기
        SetLabel("반격!", Color.white);
        Vector3 c = BodyCenter();
        Vector2 d = (Vector2)(PlayerCenter() - c);
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        WarnBox(c, new Vector2(80f, counterThickness), ang, counterWarnTime);
        yield return PausableWait(counterWarnTime);

        PlayScreenSlash(c, ang);
        Shape(shapePrefab, c, new Vector2(80f, counterThickness * 0.35f), ang, slashColor, 4120, 0.15f, 0.3f);
        if (PlayerOnLine(c, ang, counterThickness)) Hit(counterDamage, 0, 8f, 4f, PlayerCenter(), "Swordsman_Counter");
        yield return PausableWait(0.6f);
    }

    // 패리 중에 맞으면 반격 (피해는 invulnerable 로 0)
    public override void OnHit(int dir)
    {
        if (parrying) parried = true;
        base.OnHit(dir);
    }

    // ───────────────────────── 패턴 4 — 난무 ─────────────────────────
    IEnumerator RampagePattern()
    {
        FacePlayer();
        SetLabel("난무 차징 — 끊어라!", chargeColor);
        SetBodyColor(chargeColor);
        int hpStart = stats.currentHp;
        int need = Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * rampageBreakRatio));
        bool broken = false;
        float t = 0f;
        while (t < rampageChargeTime)
        {
            if (!IsWorldPaused) t += Time.deltaTime;
            int dealt = hpStart - stats.currentHp;
            if (dealt >= need) { broken = true; break; }
            if (label != null) label.text = $"난무 차징 — 끊어라! {Mathf.Min(dealt, need)}/{need}";
            yield return null;
        }

        if (broken)
        {
            // 자세 붕괴 → 취약 (받는 피해 증가)
            SetLabel("자세 붕괴 — 취약", Color.white);
            SetBodyColor(vulnerableColor);
            System.Func<int, int> prev = health.damageAbsorber;
            health.damageAbsorber = dmg => Mathf.RoundToInt((prev != null ? prev(dmg) : dmg) * vulnerableDamageMul);
            yield return PausableWait(vulnerableTime);
            health.damageAbsorber = prev;
            yield break;
        }

        // 실패 → 방을 날뛰며 참격선 8개 배치 (경고) → 동시 타격
        SetLabel("난무", chargeColor);
        GetRoomBounds(out float left, out float right, out float floor, out float ceil);
        Vector3 roomC = new Vector3((left + right) * 0.5f, (floor + ceil) * 0.5f, 0f);
        float diag = Vector2.Distance(new Vector2(left, floor), new Vector2(right, ceil)) + 4f;
        var pts = new List<Vector3>(); var angs = new List<float>();
        float warnTotal = rampageLines * rampageHopTime + rampageWarnTime;
        for (int i = 0; i < rampageLines; i++)
        {
            Vector3 p = new Vector3(Random.Range(left + 1.5f, right - 1.5f), Random.Range(floor + 1f, Mathf.Max(floor + 1.5f, ceil - 2f)), 0f);
            float a = Random.Range(-80f, 80f);
            pts.Add(p); angs.Add(a);
            yield return Blink(p - Vector3.up * 1.5f, 0f);    // 그 자리로 튀어 감 (몸 중심이 선 위)
            Shape(shapePrefab, p, new Vector2(2.2f, 0.15f), a, slashColor, 4120, 0.05f, 0.1f);   // 지나가며 긋는 짧은 칼선
            WarnBox(p, new Vector2(diag, rampageThickness), a, warnTotal - i * rampageHopTime);
            yield return PausableWait(rampageHopTime);
        }
        yield return Blink(new Vector3(roomC.x, floor + 0.1f, 0f), 0.1f);
        FacePlayer();
        SetLabel("난무 — 일제 참격", Color.white);
        yield return PausableWait(rampageWarnTime);

        PlayScreenSlashMulti(pts, angs);                  // 8줄이 동시에 화면을 가름
        bool hit = false;
        for (int i = 0; i < pts.Count; i++)
        {
            Shape(shapePrefab, pts[i], new Vector2(diag, rampageThickness * 0.35f), angs[i], slashColor, 4120, 0.15f, 0.3f);
            if (!hit && PlayerOnLine(pts[i], angs[i], rampageThickness)) hit = Hit(rampageDamage, 0, 8f, 4f, PlayerCenter(), "Swordsman_Rampage");
        }
        yield return PausableWait(0.8f);
    }

    protected override void OnTrueDeath()
    {
        parrying = false;
        base.OnTrueDeath();
    }
}
