using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// [2026-10-18 추가] 인트로 화면 캐릭터 일러스트 교차.
///
/// 일러스트를 한 명씩 차례로 보여 준다. 앞 일러스트가 완전히 사라진 뒤에 다음 일러스트가 나온다 (겹침 없음).
///   등장 : 투명 → 보임,  왼쪽(-등장 이동 거리) → 제자리
///   유지 : 제자리 → 오른쪽으로 '유지 중 흐름' 만큼 천천히
///   퇴장 : 보임 → 투명,  오른쪽으로 '퇴장 이동 거리' 만큼 더
///   → '쉬는 시간' 만큼 빈 화면 → 다음 일러스트
///
/// - 일러스트는 목록(illustrations)에 계속 추가하면 된다. 그림마다 크기 배율과 위치 보정을 따로 줄 수 있다.
/// - 그림 크기 = 기본 높이 × 그림별 배율 (가로는 그림 비율대로 자동).
/// - 기준점은 그림의 발밑(아래 가운데). 이 오브젝트의 부모(IllustrationArea)가 서 있는 자리에 발이 닿는다.
/// - 에디터(플레이 전)에서는 '미리보기 번호'의 그림을 유지 상태로 띄워 두므로 크기·위치를 보면서 맞출 수 있다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class IntroIllustrationCycler : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("보여 줄 일러스트")]
        public Sprite sprite;
        [Tooltip("이 그림만의 크기 배율 (기본 높이에 곱함)")]
        public float scale = 1f;
        [Tooltip("이 그림만의 위치 보정 (px)")]
        public Vector2 offset;
    }

    [Header("일러스트 목록")]
    public List<Entry> illustrations = new List<Entry>();
    [Tooltip("켜면 무작위 순서 (같은 그림이 연달아 나오지 않음), 끄면 목록 순서대로")]
    public bool randomOrder = false;

    [Header("크기")]
    [Tooltip("배율 1일 때 그림 높이 (px, 화면 1920×1080 기준)")]
    public float baseHeight = 1150f;

    [Header("시간 (초)")]
    [Tooltip("화면이 열린 뒤 첫 일러스트가 나오기까지")]
    public float startDelay = 0.3f;
    public float fadeInTime = 1.2f;
    public float holdTime = 4.6f;
    public float fadeOutTime = 1.2f;
    [Tooltip("앞 일러스트가 완전히 사라진 뒤 다음 일러스트가 나오기까지 비워 두는 시간")]
    public float gapTime = 0.4f;

    [Header("이동 거리 (px, + = 오른쪽)")]
    [Tooltip("등장할 때 제자리보다 이만큼 왼쪽에서 출발")]
    public float moveInDistance = 140f;
    [Tooltip("유지하는 동안 오른쪽으로 흐르는 거리")]
    public float holdDrift = 50f;
    [Tooltip("퇴장하면서 오른쪽으로 더 가는 거리")]
    public float moveOutDistance = 140f;

    [Header("곡선")]
    [Tooltip("등장 곡선. OutCubic = 빠르게 들어와 끝에서 부드럽게 멈춤")]
    public Ease inEase = Ease.OutCubic;
    [Tooltip("퇴장 곡선. InCubic = 천천히 출발해 점점 빨라짐")]
    public Ease outEase = Ease.InCubic;

    [Header("에디터 미리보기")]
    [Tooltip("플레이 전 화면에 띄워 둘 일러스트 번호 (0부터). 크기·위치 맞출 때 사용")]
    public int previewIndex = 0;

    Image image;
    RectTransform rt;
    int lastIndex = -1;
    bool previewDirty = true;

    void Awake()
    {
        image = GetComponent<Image>();
        rt = transform as RectTransform;
    }

    void OnEnable()
    {
        if (image == null) Awake();
        if (!Application.isPlaying) { previewDirty = true; return; }

        image.raycastTarget = false;
        SetPose(0f, 0f);
        StopAllCoroutines();
        StartCoroutine(Run());
    }

    void OnValidate()
    {
        previewDirty = true;
        if (baseHeight < 1f) baseHeight = 1f;
        fadeInTime = Mathf.Max(0f, fadeInTime);
        holdTime = Mathf.Max(0f, holdTime);
        fadeOutTime = Mathf.Max(0f, fadeOutTime);
        gapTime = Mathf.Max(0f, gapTime);
    }

    // 에디터(플레이 전) 미리보기 — 값이 바뀌었을 때만 다시 그린다
    void Update()
    {
        if (Application.isPlaying || !previewDirty) return;
        previewDirty = false;
        if (image == null) Awake();

        int count = illustrations != null ? illustrations.Count : 0;
        if (count == 0 || image == null) return;
        Entry e = illustrations[Mathf.Clamp(previewIndex, 0, count - 1)];
        if (e == null || e.sprite == null) return;

        ApplyEntry(e);
        SetPose(1f, 0f);
    }

    IEnumerator Run()
    {
        if (startDelay > 0f) yield return Wait(startDelay);

        while (true)
        {
            Entry e = PickNext();
            if (e == null)
            {
                // 보여 줄 그림이 없으면 잠깐 쉬었다가 다시 확인 (목록을 플레이 중에 채워도 동작)
                SetPose(0f, 0f);
                yield return Wait(0.5f);
                continue;
            }

            ApplyEntry(e);

            // 등장
            for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
            {
                float k = DOVirtual.EasedValue(0f, 1f, t / fadeInTime, inEase);
                SetPose(k, Mathf.LerpUnclamped(-moveInDistance, 0f, k));
                yield return null;
            }

            // 유지
            for (float t = 0f; t < holdTime; t += Time.unscaledDeltaTime)
            {
                SetPose(1f, Mathf.Lerp(0f, holdDrift, t / holdTime));
                yield return null;
            }

            // 퇴장
            for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
            {
                float k = DOVirtual.EasedValue(0f, 1f, t / fadeOutTime, outEase);
                SetPose(1f - k, holdDrift + moveOutDistance * k);
                yield return null;
            }

            // 완전히 사라진 다음 쉬었다가 다음 일러스트
            SetPose(0f, holdDrift + moveOutDistance);
            if (gapTime > 0f) yield return Wait(gapTime);
        }
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    // 다음에 보여 줄 그림 고르기 (그림이 빈 칸은 건너뜀)
    Entry PickNext()
    {
        if (illustrations == null || illustrations.Count == 0) return null;

        List<int> valid = new List<int>();
        for (int i = 0; i < illustrations.Count; i++)
            if (illustrations[i] != null && illustrations[i].sprite != null) valid.Add(i);
        if (valid.Count == 0) return null;

        int next;
        if (randomOrder)
        {
            if (valid.Count > 1) valid.Remove(lastIndex);   // 같은 그림 연속 방지
            next = valid[Random.Range(0, valid.Count)];
        }
        else
        {
            next = valid[0];
            foreach (int i in valid)
                if (i > lastIndex) { next = i; break; }       // 지난 번호 다음 칸 (끝이면 처음으로)
        }

        lastIndex = next;
        return illustrations[next];
    }

    Vector2 currentOffset;

    // 그림 교체 + 크기 맞추기 (발밑 기준)
    void ApplyEntry(Entry e)
    {
        image.sprite = e.sprite;
        image.preserveAspect = true;

        Rect r = e.sprite.rect;
        float h = baseHeight * Mathf.Max(0.01f, e.scale);
        float w = r.height > 0f ? h * r.width / r.height : h;

        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(w, h);
        currentOffset = e.offset;
    }

    // alpha: 0~1,  x: 제자리 기준 가로 이동(px)
    void SetPose(float alpha, float x)
    {
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
        image.enabled = c.a > 0.001f && image.sprite != null;
        rt.anchoredPosition = currentOffset + new Vector2(x, 0f);
    }
}
