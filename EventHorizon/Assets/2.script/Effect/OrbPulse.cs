using UnityEngine;

/// <summary>
/// [2026-11-21 추가] 원거리 적 에너지 구체(EnemyOrbProjectile)의 살아 있는 느낌.
///  - 중심(core)이 살짝 맥동, 바깥 빛(glow)이 천천히 숨쉬듯, 고리(ring)가 천천히 돈다.
///  - 퍼즈 중엔 같이 멈춤 (적 투사체가 퍼즈에 멈추는 것과 맞춤)
/// 모양 · 색은 프리팹 자식(Core · Glow · Ring)의 SpriteRenderer 에서 바꾼다.
/// </summary>
public class OrbPulse : MonoBehaviour
{
    public Transform core, glow, ring;
    [Tooltip("중심 맥동 세기 · 빠르기")]
    public float coreAmount = 0.12f, coreSpeed = 9f;
    [Tooltip("바깥 빛 숨쉬기 세기 · 빠르기")]
    public float glowAmount = 0.18f, glowSpeed = 4f;
    [Tooltip("고리 회전 (도/초)")]
    public float ringSpin = 120f;

    Vector3 coreBase, glowBase;
    float t, seed;

    void Awake()
    {
        if (core != null) coreBase = core.localScale;
        if (glow != null) glowBase = glow.localScale;
        seed = Random.value * 10f;
    }

    void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        t += Time.deltaTime;
        if (core != null) core.localScale = coreBase * (1f + coreAmount * Mathf.Sin((t + seed) * coreSpeed));
        if (glow != null) glow.localScale = glowBase * (1f + glowAmount * Mathf.Sin((t + seed) * glowSpeed));
        if (ring != null) ring.Rotate(0f, 0f, ringSpin * Time.deltaTime);
    }
}
