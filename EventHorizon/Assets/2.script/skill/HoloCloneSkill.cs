using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [Faust 2026-11-09] 홀로그램 분신 순간이동 참격 스킬
// 1) 시전 좌표에 발도 준비 연출 → 2) 화면 안의 적 최대 N명을 랜덤 순서로, 분신이 순간이동하며 1명당 perTargetTime 동안 이펙트+데미지
//    (이동할 때마다 이전 위치→다음 위치 사이에 선이 남음) → 3) 시전 좌표로 돌아와 연출 → 4) 화면 가르기 + 화면 안 모든 적에게 데미지
// 플레이어는 스킬 동안 자유롭게 움직일 수 있음. Skill 프리팹(Skill 컴포넌트와 같은 오브젝트)에 붙여서 사용.
[RequireComponent(typeof(Skill))]
public class HoloCloneSkill : MonoBehaviour
{
    [Header("타이밍 (초)")]
    public float drawTime = 0.6f;           // 시전 좌표에서 발도 준비 (첫 순간이동까지)
    public float perTargetTime = 0.3f;      // 적 1명당 머무는 시간
    public float hitDelay = 0.05f;          // 적 위치에 이펙트가 나오고 데미지까지
    public float returnTime = 0.6f;         // 시전 좌표로 돌아와서 화면 가르기 전까지
    public float finalHitDelay = 0.1f;      // 화면 가르기 연출이 나오고 데미지까지
    public float endTime = 0.5f;            // 화면 가르기 후 스킬 정리까지

    [Header("대상")]
    public int maxTargets = 10;
    public float screenMargin = 0.3f;       // 화면 가장자리에서 이만큼 안쪽에 있는 적만 (월드 단위)
    public Camera viewCamera;               // 비우면 적을 그리는 카메라 중 화면이 가장 넓은 것 (이 씬에선 Camera_2D)

    [Header("데미지 (공격력·방어·치명타 계산은 기존 방식 그대로)")]
    public int hitDamage = 30;              // 순간이동 참격 1회
    public float hitKnockbackX = 2f, hitKnockbackY = 1f;
    public int finalDamage = 60;            // 화면 가르기
    public float finalKnockbackX = 4f, finalKnockbackY = 2f;
    [Tooltip("[2026-11-09] 켜면 화면 가르기에 맞은 일반 적은 체력과 상관없이 즉사 (보스는 아래 값만큼만)")]
    public bool finalExecute = true;
    public int executeDamage = 9999;        // 즉사 피해 (방어·상성을 거쳐도 남지 않을 만큼 크게)
    public int finalBossDamage = 60;        // 보스가 화면 가르기에 받는 피해
    public AudioClip hitSfx;
    public AudioClip criticalSfx;
    [Header("효과음 [2026-11-09] — 여러 개 넣으면 무작위")]
    [Tooltip("분신이 적에게 순간이동해 벨 때마다 (높낮이 다른 여러 개를 넣어 두면 매번 다르게 들림)")]
    public AudioClip[] chaseSfx = new AudioClip[0];
    [Range(0f, 1f)] public float chaseVolume = 1f;
    [Tooltip("마지막 화면 가르기")]
    public AudioClip finalSfx;

    [Header("분신 위치 (적 기준)")]
    public Vector2 cloneOffset = Vector2.zero;   // x 는 들어온 방향 기준 (+면 적을 지나친 쪽)

    [Header("아트 (선택 — 비워 두면 생략)")]
    public GameObject drawPrefab;                 // 시전 좌표: 발도 준비 스프라이트 애니메이션
    [Tooltip("적마다 이 중 하나를 랜덤으로 꺼내 씀 (분신 모션 + 참격 이펙트)")]
    public GameObject[] cloneSlashPrefabs = new GameObject[0];
    public GameObject returnPrefab;               // 시전 좌표로 돌아왔을 때 스프라이트 애니메이션
    public GameObject screenSlashPrefab;          // 화면 가르기 연출 (화면 중앙에 생성)
    public float effectLifetime = 1.5f;           // 생성한 이펙트 자동 삭제 (0 = 안 지움, 프리팹이 알아서)

    [Header("순간이동 선")]
    public Material lineMaterial;                 // 비우면 선 안 그림
    public float lineWidth = 0.06f;
    [ColorUsage(true, true)] public Color lineColor = new Color(0.6f, 0.4f, 2f, 1f);
    public float lineLifetime = 0.6f;             // 선이 남아 있다가 사라지는 시간
    public string lineSortingLayer = "근경2D";
    public int lineSortingOrder = 50;

    [Header("디버그")]
    public bool drawDebug = true;                 // Scene 뷰에 화면 범위·이동 경로 표시

    PlayerController pc;
    Stats stats;
    int enemyMask;

    void Awake()
    {
        var skill = GetComponent<Skill>();
        if (skill != null) skill.lifetime = 0f;   // 끝나면 직접 지움
    }

    void Start()
    {
        if (!WasLaunched()) return;               // 씬에 놓아 둔 건 발동 안 함
        pc = FindObjectOfType<PlayerController>();
        if (pc == null) { Destroy(gameObject); return; }
        stats = pc.GetComponent<Stats>();
        enemyMask = LayerMask.GetMask("Enemy", "BossBody");
        if (viewCamera == null) viewCamera = PickGameCamera();
        StartCoroutine(Run());
    }

    // [Faust 2026-11-09] Camera.main(Main Camera_3d, 원근 FOV 25)은 실제 화면보다 훨씬 좁게 잡혀서,
    // 적 레이어를 그리는 카메라 중 플레이어 깊이에서 화면이 가장 넓은 카메라를 씀
    Camera PickGameCamera()
    {
        Camera best = null; float bestArea = 0f;
        float z = pc.transform.position.z;
        foreach (var c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled || (c.cullingMask & enemyMask) == 0) continue;
            var keep = viewCamera; viewCamera = c;
            Vector3 a = ViewportOnPlane(Vector2.zero, z), b = ViewportOnPlane(Vector2.one, z);
            viewCamera = keep;
            float area = Mathf.Abs((b.x - a.x) * (b.y - a.y));
            if (area > bestArea) { bestArea = area; best = c; }
        }
        return best != null ? best : Camera.main;
    }

    // [2026-11-11] 시전 전 확인용: 화면 안에 적이 한 명이라도 있는지.
    // CharacterSkills가 프리팹에 대고 부르므로 이 컴포넌트의 값(pc, viewCamera 등)은 바꾸지 않고 지역 변수로만 계산한다.
    public bool HasEnemyOnScreen(Transform player)
    {
        if (player == null) return false;
        int mask = LayerMask.GetMask("Enemy", "BossBody");
        float z = player.position.z;
        Camera cam = viewCamera;
        if (cam == null)
        {
            float bestArea = 0f;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.isActiveAndEnabled || (c.cullingMask & mask) == 0) continue;
                Vector3 p0 = PlaneOf(c, Vector2.zero, z), p1 = PlaneOf(c, Vector2.one, z);
                float area = Mathf.Abs((p1.x - p0.x) * (p1.y - p0.y));
                if (area > bestArea) { bestArea = area; cam = c; }
            }
            if (cam == null) cam = Camera.main;
        }
        if (cam == null) return true;   // 카메라를 못 찾으면 막지 않음
        Vector3 a = PlaneOf(cam, Vector2.zero, z), b = PlaneOf(cam, Vector2.one, z);
        Vector2 min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)) + Vector2.one * screenMargin;
        Vector2 max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)) - Vector2.one * screenMargin;
        foreach (var col in Physics2D.OverlapAreaAll(min, max, mask))
        {
            if (col == null || Hitbox.IsHitboxCollider(col)) continue;
            Health h = col.GetComponent<Health>();
            if (h == null) h = col.GetComponentInParent<Health>();
            if (h != null && h.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    static Vector3 PlaneOf(Camera cam, Vector2 vp, float z)
    {
        Ray r = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        float t = Mathf.Abs(r.direction.z) < 1e-5f ? 0f : (z - r.origin.z) / r.direction.z;
        return r.origin + r.direction * t;
    }

    bool WasLaunched()
    {
        var skill = GetComponent<Skill>();
        var f = typeof(Skill).GetField("casterRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return skill != null && f != null && f.GetValue(skill) as Transform != null;
    }

    IEnumerator Run()
    {
        Vector3 origin = pc.transform.position;               // 시전 좌표 (플레이어가 움직여도 고정)
        int facing = pc.transform.lossyScale.x < 0f ? -1 : 1;

        // ── 1) 발도 준비 ──
        Spawn(drawPrefab, origin, facing);
        if (drawTime > 0f) yield return new WaitForSeconds(drawTime);

        // ── 2) 순간이동 참격 ──
        var targets = PickTargets();
        Debug.Log($"[HoloClone] 대상 {targets.Count}명 (카메라 {(viewCamera ? viewCamera.name : "없음")})");
        Vector3 prev = origin;
        foreach (var h in targets)
        {
            if (h == null || !h.gameObject.activeInHierarchy) continue;   // 그사이 죽었거나 사라짐
            Vector3 tp = h.transform.position;
            var col = h.GetComponent<Collider2D>();
            if (col != null) tp = col.bounds.center;
            int dir = tp.x >= prev.x ? 1 : -1;
            Vector3 clonePos = tp + new Vector3(cloneOffset.x * dir, cloneOffset.y, 0f);
            clonePos.z = origin.z;

            DrawLine(prev, clonePos);
            if (cloneSlashPrefabs != null && cloneSlashPrefabs.Length > 0)
                Spawn(cloneSlashPrefabs[Random.Range(0, cloneSlashPrefabs.Length)], clonePos, dir);
            SkillSfx.Play(SkillSfx.Pick(chaseSfx), chaseVolume);   // [2026-11-09] 추격 참격

            if (hitDelay > 0f) yield return new WaitForSeconds(hitDelay);
            if (h != null && h.gameObject.activeInHierarchy)
                Hitbox.DealDirectHit(h, hitDamage, stats, dir, hitKnockbackX, hitKnockbackY, tp, hitSfx, criticalSfx, "HoloClone", true);
            float rest = perTargetTime - hitDelay;
            if (rest > 0f) yield return new WaitForSeconds(rest);
            prev = clonePos;
        }

        // ── 3) 시전 좌표로 복귀 ──
        if (targets.Count > 0) DrawLine(prev, origin);
        Spawn(returnPrefab, origin, facing);
        if (returnTime > 0f) yield return new WaitForSeconds(returnTime);

        // ── 4) 화면 가르기 ──
        Vector3 center = ScreenCenterOnPlane(origin.z);
        Spawn(screenSlashPrefab, center, facing);
        SkillSfx.Play(finalSfx);                                  // [2026-11-09] 화면 가르기
        if (finalHitDelay > 0f) yield return new WaitForSeconds(finalHitDelay);
        var all = OnScreenEnemies();
        Debug.Log($"[HoloClone] 화면 가르기 {all.Count}명");
        bool firstFinal = true;   // [2026-11-09] 여러 마리를 맞혀도 타격음은 한 번만
        foreach (var h in all)
        {
            if (h == null) continue;
            int dir = h.transform.position.x >= origin.x ? 1 : -1;
            bool isBoss = h.GetComponentInParent<BossController>() != null;
            int dmg = isBoss ? finalBossDamage : (finalExecute ? executeDamage : finalDamage);
            if (Hitbox.DealDirectHit(h, dmg, stats, dir, finalKnockbackX, finalKnockbackY, h.transform.position,
                                     firstFinal ? hitSfx : null, firstFinal ? criticalSfx : null, "HoloCloneFinal", true))
                firstFinal = false;
        }

        if (endTime > 0f) yield return new WaitForSeconds(endTime);
        Destroy(gameObject);
    }

    // 화면 안 적 중 최대 maxTargets 명, 랜덤 순서
    List<Health> PickTargets()
    {
        var list = OnScreenEnemies();
        for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
        if (list.Count > maxTargets) list.RemoveRange(maxTargets, list.Count - maxTargets);
        return list;
    }

    List<Health> OnScreenEnemies()
    {
        var result = new List<Health>();
        if (!ScreenRect(pc.transform.position.z, out Vector2 min, out Vector2 max)) return result;
        min += Vector2.one * screenMargin; max -= Vector2.one * screenMargin;
        if (drawDebug)
        {
            Debug.DrawLine(new Vector3(min.x, min.y), new Vector3(max.x, min.y), Color.yellow, 1f);
            Debug.DrawLine(new Vector3(max.x, min.y), new Vector3(max.x, max.y), Color.yellow, 1f);
            Debug.DrawLine(new Vector3(max.x, max.y), new Vector3(min.x, max.y), Color.yellow, 1f);
            Debug.DrawLine(new Vector3(min.x, max.y), new Vector3(min.x, min.y), Color.yellow, 1f);
        }
        var seen = new HashSet<Health>();
        foreach (var col in Physics2D.OverlapAreaAll(min, max, enemyMask))
        {
            if (col == null || Hitbox.IsHitboxCollider(col)) continue;
            Health h = col.GetComponent<Health>();
            if (h == null) h = col.GetComponentInParent<Health>();
            if (h == null || !h.gameObject.activeInHierarchy || !seen.Add(h)) continue;
            result.Add(h);
        }
        return result;
    }

    // 카메라 화면 네 귀퉁이를 z 평면에 투영 (원근 카메라도 됨)
    bool ScreenRect(float z, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        if (viewCamera == null) return false;
        Vector3 a = ViewportOnPlane(new Vector2(0, 0), z), b = ViewportOnPlane(new Vector2(1, 1), z);
        min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return true;
    }

    Vector3 ViewportOnPlane(Vector2 vp, float z)
    {
        Ray r = viewCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        float t = Mathf.Abs(r.direction.z) < 1e-5f ? 0f : (z - r.origin.z) / r.direction.z;
        return r.origin + r.direction * t;
    }

    Vector3 ScreenCenterOnPlane(float z) => viewCamera != null ? ViewportOnPlane(new Vector2(0.5f, 0.5f), z) : pc.transform.position;

    void Spawn(GameObject prefab, Vector3 pos, int dir)
    {
        if (prefab == null) return;
        var fx = Instantiate(prefab, pos, prefab.transform.rotation);
        if (dir < 0) { var s = fx.transform.localScale; s.x = -s.x; fx.transform.localScale = s; }   // 왼쪽이면 좌우 반전
        if (effectLifetime > 0f) Destroy(fx, effectLifetime);
    }

    void DrawLine(Vector3 a, Vector3 b)
    {
        if (drawDebug) Debug.DrawLine(a, b, Color.magenta, 1f);
        if (lineMaterial == null) return;
        var go = new GameObject("HoloClone_Line");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a); lr.SetPosition(1, b);
        lr.sharedMaterial = lineMaterial;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 2;
        lr.startColor = lr.endColor = lineColor;
        lr.sortingLayerName = lineSortingLayer;
        lr.sortingOrder = lineSortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Destroy(go, lineLifetime + 0.2f);   // 스킬 오브젝트가 먼저 지워져도 선이 남지 않게
        StartCoroutine(FadeLine(lr));
    }

    IEnumerator FadeLine(LineRenderer lr)
    {
        float t = 0f;
        while (t < lineLifetime && lr != null)
        {
            float k = 1f - t / lineLifetime;
            Color c = lineColor; c.a *= k;
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = lineWidth * Mathf.Lerp(0.3f, 1f, k);
            t += Time.deltaTime;
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);
    }
}
