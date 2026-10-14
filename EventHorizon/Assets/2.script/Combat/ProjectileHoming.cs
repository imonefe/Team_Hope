using UnityEngine;

/// <summary>
/// 유도 미사일
/// 발사 직후 위로 올라가다가 점차 플레이어 방향으로 유도
/// </summary>
public class ProjectileHoming : ProjectileBase
{
    [Header("설정")]
    public float moveSpeed = 6f;
    public float lifetime = 4f;

    [Header("유도 설정")]
    public float initialUpTime = 0.5f;  // 처음 위로 올라가는 시간
    public float homingDelay = 0.5f;  // 유도 시작까지 대기 시간
    public float turnSpeed = 3f;    // 플레이어 향해 꾺이는 속도

    [Header("이펙트")]
    public GameObject trailEffectPrefab;

    Transform player;
    Vector2 currentDir;
    float timer = 0f;

    protected override float FacingX => currentDir.x;

    protected override void Awake()
    {
        base.Awake();

        // 처음엔 위로
        currentDir = Vector2.up;

        // 트레일 이펙트
        if (trailEffectPrefab != null)
            Instantiate(trailEffectPrefab, transform.position, Quaternion.identity)
                .transform.SetParent(transform);

        // [2026-10-10 수정] 수명은 FixedUpdate에서 센다 (퍼즈로 멈춘 동안은 줄지 않음)

        // CharacterManager가 없는 씬(테스트 씬 등)에서도 예외 없이 동작하도록
        if (CharacterManager.Instance != null)
        {
            UpdatePlayerTarget(CharacterManager.Instance.CurrentCharacter);
            CharacterManager.Instance.OnCharacterChanged += UpdatePlayerTarget;
        }
        else
        {
            UpdatePlayerTarget(GameObject.FindGameObjectWithTag("Player"));
        }
    }

    void OnDestroy()
    {
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= UpdatePlayerTarget;
    }

    void UpdatePlayerTarget(GameObject obj)
    {
        if (obj != null) player = obj.transform;
    }

    void FixedUpdate()
    {
        if (PauseCheck()) return;

        timer += Time.fixedDeltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (timer < initialUpTime)
        {
            // 위로 올라가기
            currentDir = Vector2.up;
        }
        else if (timer >= homingDelay && player != null)
        {
            // 플레이어 방향으로 서서히 꾺기
            Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
            currentDir = Vector2.Lerp(currentDir, toPlayer, turnSpeed * Time.fixedDeltaTime).normalized;
        }

        rigid.velocity = currentDir * moveSpeed;

        // 방향에 따라 회전
        float angle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
