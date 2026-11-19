using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-19 신규] 애니메이션·이펙트가 아직 없는 중간보스를 시험하기 위한 공통 부모.
///
/// - 몸은 색 박스(body), 머리 위 글자(label)로 지금 하는 행동을 보여준다.
/// - 패턴에서 쓰는 공용 기능: 방 크기 재기, 경고 박스, 선·상자·원 판정, 임시 도형, 화면 가르기 재생.
/// - 체력·피격·사망·재화·퍼즈 정지는 부모(MidBossController → BossController)가 처리한다.
/// 진짜 그림이 오면 body 대신 스파인을 넣고, SetLabel 부분을 애니메이션 재생으로 바꾸면 된다.
/// </summary>
public abstract class MidBossTestBase : MidBossController
{
    [Header("[ 테스트 그림 ]")]
    [Tooltip("몸 (색 박스)")]
    public SpriteRenderer body;
    [Tooltip("머리 위 행동 글자")]
    public TMP_Text label;
    public Color bodyColor = new Color(0.55f, 0.62f, 0.75f, 1f);

    [Header("[ 공용 이펙트 프리팹 ]")]
    [Tooltip("공격 예고 박스 (FX_WarningZone)")]
    public GameObject warningPrefab;
    [Tooltip("임시 도형 — 1×1 칸 흰 사각형 (FX_TestShape). 크기·각도·색은 코드가 정함")]
    public GameObject shapePrefab;
    [Tooltip("임시 원 — 지름 1칸 흰 원 (FX_TestCircle)")]
    public GameObject circlePrefab;
    [Tooltip("화면 가르기 (FX_BossScreenSlash — 검사 처형 컷신과 같은 ScreenSlashEffect)")]
    public GameObject screenSlashPrefab;
    [Tooltip("방 벽·바닥 찾기용")]
    public LayerMask wallMask;

    protected readonly List<GameObject> live = new List<GameObject>();   // 패턴이 만든 것 (죽으면 정리)
    Health playerHealth;

    protected override void Awake()
    {
        animMode = AnimMode.Animator;   // 애니메이터 없음 → 부모의 재생 처리는 모두 건너뜀
        useHitReaction = false;
        base.Awake();
        if (wallMask.value == 0) wallMask = LayerMask.GetMask("ground");
        ResetBody();
        SetLabel("");
    }

    protected override void OnCooldownStart()
    {
        base.OnCooldownStart();
        ResetBody();
        SetLabel("");
    }

    protected override void OnTrueDeath()
    {
        StopAllCoroutines();
        CleanupLive();
        SetLabel("처치");
        if (body != null) body.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        base.OnTrueDeath();
    }

    protected void CleanupLive()
    {
        foreach (var g in live) if (g != null) Destroy(g);
        live.Clear();
    }

    // ───────────────────────── 그림 ─────────────────────────
    protected void SetLabel(string text) { SetLabel(text, Color.white); }
    protected void SetLabel(string text, Color color)
    {
        if (label == null) return;
        label.text = text;
        label.color = color;
        // 보스가 왼쪽을 볼 때(스케일 x 음수) 글자가 뒤집히지 않게
        Vector3 s = label.transform.localScale;
        s.x = Mathf.Abs(s.x) * (transform.localScale.x < 0f ? -1f : 1f);
        label.transform.localScale = s;
    }

    protected void SetBodyColor(Color c) { if (body != null) body.color = c; }
    protected void ResetBody() { SetBodyColor(bodyColor); }

    protected override void Update()
    {
        base.Update();
        // 방향을 바꾸면 글자도 다시 맞춤
        if (label != null && Mathf.Sign(label.transform.localScale.x) != Mathf.Sign(transform.localScale.x))
        {
            Vector3 s = label.transform.localScale; s.x = -s.x; label.transform.localScale = s;
        }
    }

    // ───────────────────────── 위치 ─────────────────────────
    protected void FacePlayer()
    {
        if (player == null) return;
        FlipTo(player.position.x >= transform.position.x ? 1 : -1);
    }

    protected Vector3 BodyCenter()
    {
        Collider2D c = GetComponent<Collider2D>();
        return c != null ? c.bounds.center : transform.position;
    }

    protected Vector3 PlayerCenter()
    {
        if (player == null) return transform.position;
        Collider2D c = player.GetComponent<Collider2D>();
        return c != null ? c.bounds.center : player.position;
    }

    // 플레이어가 서 있는 줄 기준 방의 왼쪽·오른쪽 벽, 바닥, 천장
    protected void GetRoomBounds(out float left, out float right, out float floor, out float ceil)
    {
        Vector3 from = player != null ? player.position : transform.position;
        Vector2 o = from + Vector3.up * 0.8f;
        RaycastHit2D h;
        h = Physics2D.Raycast(o, Vector2.left, 60f, wallMask); left = h.collider != null ? h.point.x : o.x - 12f;
        h = Physics2D.Raycast(o, Vector2.right, 60f, wallMask); right = h.collider != null ? h.point.x : o.x + 12f;
        h = Physics2D.Raycast(o, Vector2.down, 60f, wallMask); floor = h.collider != null ? h.point.y : from.y;
        h = Physics2D.Raycast(o, Vector2.up, 60f, wallMask); ceil = h.collider != null ? h.point.y : from.y + 12f;
    }

    // 순간이동 (사라짐 → 이동 → 나타남)
    protected IEnumerator Blink(Vector3 dest, float hide)
    {
        if (body != null) body.enabled = false;
        if (hide > 0f) yield return PausableWait(hide);
        rigid.velocity = Vector2.zero;
        rigid.position = dest;
        transform.position = dest;
        if (body != null) body.enabled = true;
    }

    // ───────────────────────── 임시 도형 ─────────────────────────
    /// <summary>회전한 사각형 (선·검기·레이저 그림). life 가 0보다 크면 hold 뒤 fade 동안 흐려지며 사라짐</summary>
    protected GameObject Shape(GameObject prefab, Vector3 center, Vector2 size, float angleDeg, Color color, int order, float hold, float fade)
    {
        if (prefab == null) return null;
        GameObject g = Instantiate(prefab, center, Quaternion.Euler(0f, 0f, angleDeg));
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = g.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) { sr.color = color; sr.sortingOrder = order; }
        live.Add(g);
        if (hold >= 0f) StartCoroutine(FadeOut(g, sr, hold, fade));
        return g;
    }

    IEnumerator FadeOut(GameObject g, SpriteRenderer sr, float hold, float fade)
    {
        yield return PausableWait(hold);
        Color c0 = sr != null ? sr.color : Color.white;
        float t = 0f;
        while (g != null && t < fade)
        {
            if (!IsWorldPaused) t += Time.deltaTime;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - t / Mathf.Max(0.01f, fade)));
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    /// <summary>회전한 경고 박스 (선 모양 예고)</summary>
    protected WarningZone WarnBox(Vector3 center, Vector2 size, float angleDeg, float duration)
    {
        if (warningPrefab == null) return null;
        WarningZone wz = WarningZone.Spawn(warningPrefab, center, size, duration);
        if (wz != null)
        {
            wz.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            live.Add(wz.gameObject);
        }
        return wz;
    }

    /// <summary>
    /// [2026-11-19] 보스·경고 박스를 실제로 그리는 카메라 (적 레이어를 그리는 카메라, 정사영 우선).
    /// Camera.main(Main Camera_3d)은 원근 카메라라 화면 위치가 2D 오브젝트와 어긋난다.
    /// </summary>
    protected static Camera GameCamera()
    {
        int enemyBit = 1 << LayerMask.NameToLayer("Enemy");
        Camera best = null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled || (c.cullingMask & enemyBit) == 0) continue;
            if (best == null || (c.orthographic && !best.orthographic)) best = c;
        }
        return best != null ? best : Camera.main;
    }

    /// <summary>검사 처형 컷신과 같은 화면 가르기 (각도는 화면 기준, 월드 각도와 같음)</summary>
    protected void PlayScreenSlash(Vector3 worldPos, float angleDeg)
    {
        if (screenSlashPrefab == null) return;
        GameObject g = Instantiate(screenSlashPrefab, worldPos, Quaternion.identity);
        ScreenSlashEffect fx = g.GetComponent<ScreenSlashEffect>();
        if (fx != null) fx.targetCamera = GameCamera();   // [2026-11-19] 2D 를 그리는 카메라 기준으로 화면 위치 계산
        if (fx != null)
        {
            fx.PlayAtWorld(angleDeg, worldPos);
            Destroy(g, fx.duration + 1f);
        }
        else Destroy(g, 3f);
    }

    /// <summary>[2026-11-19] 화면 가르기 여러 줄 동시 (주 선 + 추가 선 최대 8개). 프리팹에 ScreenSlashMultiLine 이 있어야 함</summary>
    protected void PlayScreenSlashMulti(IList<Vector3> pts, IList<float> angs)
    {
        if (screenSlashPrefab == null || pts == null || pts.Count == 0) return;
        GameObject g = Instantiate(screenSlashPrefab, pts[0], Quaternion.identity);
        ScreenSlashEffect fx = g.GetComponent<ScreenSlashEffect>();
        if (fx != null) fx.targetCamera = GameCamera();   // [2026-11-19] 2D 를 그리는 카메라 기준으로 화면 위치 계산
        ScreenSlashMultiLine multi = g.GetComponent<ScreenSlashMultiLine>();
        var ep = new List<Vector3>(); var ea = new List<float>();
        for (int i = 1; i < pts.Count; i++) { ep.Add(pts[i]); ea.Add(angs[i]); }
        if (multi != null) multi.Play(pts[0], angs[0], ep, ea);
        else if (fx != null) fx.PlayAtWorld(angs[0], pts[0]);
        Destroy(g, fx != null ? fx.duration + 1f : 3f);
    }

    // ───────────────────────── 판정 ─────────────────────────
    protected Health PlayerHealth()
    {
        if (player == null) return null;
        if (playerHealth == null || playerHealth.transform != player) playerHealth = player.GetComponent<Health>();
        return playerHealth;
    }

    Bounds PlayerBounds()
    {
        Collider2D col = player != null ? player.GetComponent<Collider2D>() : null;
        Bounds b = col != null ? col.bounds : new Bounds(player != null ? player.position : Vector3.zero, Vector3.one);
        b.center = new Vector3(b.center.x, b.center.y, 0f);
        return b;
    }

    protected bool Hit(float mul, int dir, float kbX, float kbY, Vector3 at, string tag)
    {
        Health h = PlayerHealth();
        if (h == null) return false;
        if (dir == 0) dir = player.position.x >= at.x ? 1 : -1;
        return Hitbox.DealDirectHit(h, Mathf.RoundToInt(stats.Atk * mul), stats, dir, kbX, kbY, at, null, null, tag, false);
    }

    /// <summary>점 p 를 지나고 angle 각도인 무한한 선(두께 thickness)에 플레이어가 겹치는지</summary>
    protected bool PlayerOnLine(Vector3 p, float angleDeg, float thickness)
    {
        if (player == null) return false;
        Bounds b = PlayerBounds();
        float a = angleDeg * Mathf.Deg2Rad;
        Vector2 n = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));                 // 선에 수직
        float d = Mathf.Abs(Vector2.Dot((Vector2)(b.center - p), n));
        float reach = Mathf.Abs(b.extents.x * n.x) + Mathf.Abs(b.extents.y * n.y);   // 플레이어 상자가 수직 방향으로 차지하는 반폭
        return d <= thickness * 0.5f + reach;
    }

    protected bool PlayerInBox(Vector3 center, Vector2 size)
    {
        if (player == null) return false;
        return new Bounds(center, new Vector3(size.x, size.y, 100f)).Intersects(PlayerBounds());
    }

    protected bool PlayerInCircle(Vector3 c, float r)
    {
        if (player == null) return false;
        Collider2D col = player.GetComponent<Collider2D>();
        Vector2 pc = col != null ? (Vector2)col.ClosestPoint(c) : (Vector2)player.position;
        return Vector2.Distance(pc, c) <= r;
    }

    // ───────────────────────── 날아가는 것 ─────────────────────────
    /// <summary>
    /// 원·사각형 투사체를 움직인다. homingTurn > 0 이면 초당 그 각도만큼 플레이어 쪽으로 꺾음.
    /// 플레이어에 닿거나 벽에 닿거나 life 가 지나면 사라짐. 한 번만 피해.
    /// </summary>
    protected IEnumerator MoveProjectile(GameObject g, Vector2 dir, float speed, float homingTurn, float life,
                                         float hitRadius, float mul, string tag, bool rotateToDir)
    {
        if (g == null) yield break;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        float t = 0f;
        while (g != null && t < life)
        {
            if (!IsWorldPaused)
            {
                float dt = Time.deltaTime;
                t += dt;
                if (homingTurn > 0f && player != null)
                {
                    Vector2 want = ((Vector2)PlayerCenter() - (Vector2)g.transform.position).normalized;
                    float ang = Vector2.SignedAngle(dir, want);
                    float step = Mathf.Clamp(ang, -homingTurn * dt, homingTurn * dt);
                    dir = (Quaternion.Euler(0f, 0f, step) * dir).normalized;
                }
                Vector3 next = g.transform.position + (Vector3)(dir * speed * dt);
                if (Physics2D.OverlapCircle(next, hitRadius * 0.5f, wallMask) != null) break;   // 벽
                g.transform.position = next;
                if (rotateToDir) g.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                if (PlayerInCircle(next, hitRadius))
                {
                    Hit(mul, dir.x < 0f ? -1 : 1, 5f, 2f, next, tag);
                    break;
                }
            }
            yield return null;
        }
        if (g != null) { live.Remove(g); Destroy(g); }
    }

    /// <summary>pausable 대기 중 매 프레임 조건 확인 (조건이 참이 되면 바로 끝남)</summary>
    protected IEnumerator WaitOrUntil(float duration, System.Func<bool> until)
    {
        float t = 0f;
        while (t < duration)
        {
            if (until != null && until()) yield break;
            if (!IsWorldPaused) t += Time.deltaTime;
            yield return null;
        }
    }
}
