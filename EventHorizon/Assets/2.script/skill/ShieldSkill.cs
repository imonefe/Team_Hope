using UnityEngine;

// [Faust 2026-11-08] 보호막 스킬: Skill 프리팹에 붙임. 스킬 키로 발동하면 플레이어 자식으로 쉴드를 만들고 보호막 수치를 줌
[RequireComponent(typeof(Skill))]
public class ShieldSkill : MonoBehaviour
{
    public GameObject shieldPrefab;           // FX_Shield_Future
    public int shieldAmount = 50;             // 보호막 수치
    public float duration = 0f;               // 0 = 깨질 때까지 유지
    public Vector3 offset = new Vector3(0f, 0.2f, 0f); // 플레이어 기준 위치

    [Header("효과음 [2026-11-09] — 보호막에 넘겨 줌")]
    public AudioClip upSfx;                               // 생성·다시 채움
    public AudioClip[] hitSfx = new AudioClip[0];         // 막아낼 때 (여러 개면 무작위)
    public AudioClip breakSfx;                            // 맞아서 깨질 때
    public AudioClip expireSfx;                           // 시간이 다 돼서 사라질 때

    void Awake()
    {
        var skill = GetComponent<Skill>();
        if (skill != null) skill.lifetime = 0f;
    }

    void Start()
    {
        if (!WasLaunched()) return;          // 씬에 놓아 둔 건 발동 안 함
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null && shieldPrefab != null)
        {
            var health = pc.GetComponent<Health>();
            var existing = pc.GetComponentInChildren<PlayerShield>();
            if (existing != null)
            {
                existing.SetSounds(upSfx, hitSfx, breakSfx, expireSfx);
                existing.Refill(shieldAmount, duration);
            }
            else
            {
                var go = Instantiate(shieldPrefab, pc.transform);
                go.transform.localPosition = offset;
                go.transform.localRotation = Quaternion.identity;
                var ps = go.GetComponent<PlayerShield>();
                if (ps == null) ps = go.AddComponent<PlayerShield>();
                ps.SetSounds(upSfx, hitSfx, breakSfx, expireSfx);
                ps.Init(health, shieldAmount, duration);
            }
        }
        Destroy(gameObject);
    }

    bool WasLaunched()
    {
        var skill = GetComponent<Skill>();
        var f = typeof(Skill).GetField("casterRef", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return skill != null && f != null && f.GetValue(skill) as Transform != null;
    }
}
