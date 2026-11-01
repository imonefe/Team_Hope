using UnityEngine;

/// <summary>
/// [2026-10-22] 스프라이트 여러 장을 프레임마다 정한 시간대로 한 번(또는 반복) 재생한다.
/// 애니메이터 없이 쓰는 간단한 프레임 애니메이션 — 보스2 컷신의 검사(knight) 발도 연출용.
/// CutsceneSequencer 의 '검사의 일격(SwordSlashKill)' 단계가 CurrentFrame 을 보고 베는 순간을 맞춘다.
///
/// 시간은 Time.deltaTime(게임 시간)을 쓴다 → 히트스톱·슬로모션 때 같이 느려진다.
/// </summary>
public class SpriteFrameSequence : MonoBehaviour
{
    [Tooltip("그림을 바꿀 대상 (비우면 이 오브젝트의 SpriteRenderer)")]
    public SpriteRenderer target;
    [Tooltip("재생할 그림들 (순서대로)")]
    public Sprite[] frames;
    [Tooltip("각 그림을 보여 주는 시간(초). frames 와 같은 순서. 모자라면 마지막 값을 씀")]
    public float[] durations;
    [Tooltip("켜지면(생성되면) 바로 재생")]
    public bool playOnEnable = true;
    [Tooltip("끝나면 처음부터 다시")]
    public bool loop = false;
    [Tooltip("재생 속도 배율")]
    public float speed = 1f;

    /// <summary>지금 보여 주는 그림 번호 (재생 전 -1)</summary>
    public int CurrentFrame { get; private set; } = -1;
    /// <summary>한 번 재생이 끝났는지 (반복이면 항상 false)</summary>
    public bool Finished { get; private set; }
    /// <summary>재생 시작 후 흐른 시간(초)</summary>
    public float Elapsed { get; private set; }

    /// <summary>그림이 바뀔 때마다 (새 번호)</summary>
    public event System.Action<int> FrameChanged;

    bool playing;
    float frameTime;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play()
    {
        playing = frames != null && frames.Length > 0;
        Finished = false;
        Elapsed = 0f;
        frameTime = 0f;
        CurrentFrame = -1;
        if (playing) SetFrame(0);
    }

    /// <summary>이 프레임까지의 시작 시간(초) — 베는 순간이 언제인지 미리 알고 싶을 때</summary>
    public float TimeUntilFrame(int index)
    {
        float t = 0f;
        for (int i = 0; i < index && frames != null && i < frames.Length; i++) t += Duration(i);
        return t / Mathf.Max(0.01f, speed);
    }

    void Update()
    {
        if (!playing) return;
        float dt = Time.deltaTime * speed;
        Elapsed += dt;
        frameTime += dt;

        while (playing && frameTime >= Duration(CurrentFrame))
        {
            frameTime -= Duration(CurrentFrame);
            int next = CurrentFrame + 1;
            if (next >= frames.Length)
            {
                if (loop) next = 0;
                else { playing = false; Finished = true; return; }   // 마지막 그림에서 멈춤
            }
            SetFrame(next);
        }
    }

    float Duration(int i)
    {
        if (durations == null || durations.Length == 0) return 0.1f;
        return Mathf.Max(0.001f, durations[Mathf.Clamp(i, 0, durations.Length - 1)]);
    }

    void SetFrame(int i)
    {
        CurrentFrame = i;
        if (target != null) target.sprite = frames[i];
        FrameChanged?.Invoke(i);
    }
}
