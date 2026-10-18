using UnityEngine;

/// <summary>
/// 애니메이션 이벤트로 발소리를 재생한다. 등록한 소리 중 하나를 랜덤으로 고른다.
///
/// 사용법
///  1. 주인공(Animator가 붙은 오브젝트, 예: PlayerA)에 이 컴포넌트를 붙인다.
///  2. Clips 칸에 발소리 4~5개를 넣는다.
///  3. Run 애니메이션에서 발이 땅에 닿는 프레임에 애니메이션 이벤트를 만들고,
///     Function 칸에 PlayFootstep 을 입력한다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerFootstepSfx : MonoBehaviour
{
    [Header("발소리 (이 중 하나가 랜덤으로 재생)")]
    public AudioClip[] clips;

    [Header("소리 크기")]
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("매번 크기를 이만큼 랜덤으로 흔든다 (0이면 항상 같은 크기)")]
    [Range(0f, 0.5f)] public float volumeRandom = 0.1f;

    [Header("소리 높낮이")]
    [Range(0.5f, 2f)] public float pitch = 1f;
    [Tooltip("매번 높낮이를 이만큼 랜덤으로 흔든다 (같은 소리도 조금씩 다르게 들림)")]
    [Range(0f, 0.3f)] public float pitchRandom = 0.08f;

    [Header("옵션")]
    [Tooltip("켜면 방금 나온 소리가 바로 다시 나오지 않는다")]
    public bool avoidRepeat = true;
    [Tooltip("이 시간(초) 안에 또 호출되면 무시한다. 애니메이션이 바뀌는 순간 소리가 두 번 겹치는 것을 막는다")]
    public float minInterval = 0.08f;
    [Tooltip("재생에 쓸 AudioSource. 비워두면 발소리 전용 AudioSource를 자동으로 만든다.\n" +
             "공중에 뜨면 이 AudioSource를 통째로 멈추므로, 다른 소리와 같이 쓰는 AudioSource는 넣지 않는다")]
    public AudioSource source;

    [Header("공중 판정")]
    [Tooltip("켜면 점프 등으로 공중에 떠 있는 동안 발소리를 즉시 끊고, 새 발소리도 내지 않는다")]
    public bool stopWhenAirborne = true;
    [Tooltip("땅에 닿아 있는지 알려주는 Animator 파라미터 이름 (PlayerController가 매 프레임 갱신)")]
    public string groundedParam = "isGrounded";

    int lastIndex = -1;
    float lastTime = -999f;

    Animator animator;
    int groundedHash;
    bool hasGroundedParam;

    void Awake()
    {
        // 발소리 전용 스피커를 따로 둔다 (다른 효과음의 높낮이에 영향을 주지 않도록)
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // 2D 소리
        }

        // 땅 판정은 PlayerController가 Animator에 넣어주는 값을 그대로 읽는다
        animator = GetComponent<Animator>();
        groundedHash = Animator.StringToHash(groundedParam);
        hasGroundedParam = false;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == groundedHash && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasGroundedParam = true;
                    break;
                }
            }
        }
        if (stopWhenAirborne && !hasGroundedParam)
            Debug.LogWarning("[PlayerFootstepSfx] Animator에 '" + groundedParam + "' 파라미터가 없어 공중 판정을 쓸 수 없습니다.", this);
    }

    void Update()
    {
        // 공중에 뜬 순간 재생 중인 발소리를 바로 끊는다
        if (IsAirborne() && source != null && source.isPlaying)
            source.Stop();
    }

    bool IsAirborne()
    {
        if (!stopWhenAirborne || !hasGroundedParam || animator == null) return false;
        return !animator.GetBool(groundedHash);
    }

    // ───────── 애니메이션 이벤트에서 호출하는 함수 ─────────

    public void PlayFootstep()
    {
        if (clips == null || clips.Length == 0 || source == null) return;
        if (!source.isActiveAndEnabled) return;
        if (Time.time - lastTime < minInterval) return;
        if (IsAirborne()) return;   // 공중에서는 발소리를 내지 않는다

        int index = PickIndex();
        if (index < 0) return;

        lastIndex = index;
        lastTime = Time.time;

        source.pitch = pitch + Random.Range(-pitchRandom, pitchRandom);
        float vol = Mathf.Clamp01(volume + Random.Range(-volumeRandom, volumeRandom));
        source.PlayOneShot(clips[index], vol * GameSettings.SfxVolume);   // [2026-10-18] 설정 창의 효과음 크기 적용
    }

    // ───────── 랜덤 선택 ─────────

    // 비어 있는 칸은 건너뛰고, avoidRepeat가 켜져 있으면 직전 소리는 후보에서 뺀다
    int PickIndex()
    {
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (IsCandidate(i)) count++;
        }

        // 후보가 없으면(소리가 1개뿐인 경우 등) 직전 소리라도 다시 재생
        if (count == 0)
        {
            bool lastValid = lastIndex >= 0 && lastIndex < clips.Length && clips[lastIndex] != null;
            return lastValid ? lastIndex : -1;
        }

        int pick = Random.Range(0, count);
        for (int i = 0; i < clips.Length; i++)
        {
            if (!IsCandidate(i)) continue;
            if (pick == 0) return i;
            pick--;
        }
        return -1;
    }

    bool IsCandidate(int i)
    {
        if (clips[i] == null) return false;
        if (avoidRepeat && i == lastIndex) return false;
        return true;
    }
}
