using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 퍼즈 게이지 UI
/// Canvas 에 붙이기
///
/// 평소엔 gaugeSlider가 "충전된 게이지"(0~100)를 보여주다가, 퍼즈가 발동되는 순간부터는
/// 같은 슬라이더를 "남은 퍼즈 시간 비율"로 전환해서 보여준다. 그래서 발동하자마자
/// 확 비는 게 아니라, 퍼즈 지속시간에 비례해서 서서히 줄어든다.
///
/// [2026-10-20 수정] 가득 찼을 때 표시 (시안 B + D)
/// - 바 색: 평소 normalColor → 가득 차면 readyColor 로 colorFadeTime 동안 바뀜 (움직임 없음)
/// - 바 오른쪽 끝에 작은 마름모가 천천히 깜빡이고, 바 오른쪽 끝 아래에 [Q] 퍼즈 안내가 뜬다
/// - 마름모·안내는 Start 에서 코드로 만든다 (새 그림 없음: 마름모는 그림 없는 단색 칸을 45도 돌린 것,
///   키 상자는 조작키 안내 칸과 같은 keyBoxSprite)
/// - gaugeFill 이 비어 있으면 슬라이더의 채움 이미지(Fill Rect)를 자동으로 찾아 쓴다.
/// </summary>
public class PauseGaugeUI : MonoBehaviour
{
    [Header("게이지 바")]
    public Slider gaugeSlider;
    [Tooltip("게이지 채워지는 Image (비우면 gaugeSlider 의 Fill Rect 에서 자동으로 찾음)")]
    public Image gaugeFill;

    [Header("퍼즈 중 표시")]
    public GameObject pausePanel;    // 퍼즈 중에만 보이는 패널
    public Slider timeSlider;    // 남은 시간 바
    public Slider distSlider;    // 남은 거리 바

    [Header("색상")]
    public Color normalColor = new Color(0.62f, 0.6f, 0.58f);
    public Color readyColor = new Color(1.0f, 0.92f, 0.7f);
    public Color pausingColor = new Color(0.5f, 1.0f, 0.8f);
    [Tooltip("색이 바뀌는 데 걸리는 시간 (초)")]
    public float colorFadeTime = 0.2f;

    [Header("가득 찼을 때 안내 (2026-10-20)")]
    [Tooltip("키 상자 그림 (조작키 안내 칸과 같은 UIKeyBox)")]
    public Sprite keyBoxSprite;
    [Tooltip("안내 글꼴 (한글이 들어 있는 글꼴)")]
    public TMP_FontAsset labelFont;
    public string keyText = "Q";
    public string labelText = "퍼즈";
    public Color hintColor = new Color(0.95f, 0.92f, 0.85f, 1f);
    [Tooltip("마름모 색")]
    public Color markerColor = new Color(1f, 0.93f, 0.7f, 1f);
    [Tooltip("마름모 크기 (px)")]
    public float markerSize = 10f;
    [Tooltip("마름모가 1초에 몇 번 깜빡이는지")]
    public float markerBlinkPerSecond = 0.8f;
    [Range(0f, 1f)] public float markerMinAlpha = 0.35f;
    [Tooltip("안내 글자 위치 미세 조정 (보이는 바 오른쪽 아래 모서리 기준, hintGap 만큼 아래에서 추가로)")]
    public Vector2 hintOffset = Vector2.zero;

    Image marker;
    CanvasGroup hintGroup;
    RectTransform fillRT;
    readonly Vector3[] fillCorners = new Vector3[4];
    [Tooltip("[2026-10-21] [Q] 퍼즈 안내와 바 아래쪽 사이 간격 (px)")]
    public float hintGap = 4f;
    Color currentColor;
    bool colorInit;

    [Header("가득 찼을 때 빛 입자 터짐 (2026-10-21)")]
    [Tooltip("한 번에 터지는 빛 입자 수 (바 전체에 고르게 나눠서 터짐)")]
    public int burstCount = 64;
    [Tooltip("[2026-10-21] 게이지가 가득 찬 순간 한 번 재생할 효과음 (효과음 볼륨 설정을 따름, 비우면 소리 없음)")]
    public AudioClip readySfx;
    [Tooltip("입자 처음 속도 범위 (px/초)")]
    public Vector2 burstSpeed = new Vector2(120f, 320f);
    [Tooltip("입자 수명 범위 (초)")]
    public Vector2 burstLifetime = new Vector2(0.5f, 0.9f);
    [Tooltip("입자 크기 범위 (px)")]
    public Vector2 burstSize = new Vector2(6f, 14f);
    [Tooltip("입자 중 이 비율만큼은 바 오른쪽 끝 30% 구간에 몰아서 터짐 (0 = 바 전체에 고르게)")]
    [Range(0f, 1f)] public float burstRightBias = 0f;
    [Tooltip("입자 중 이 비율만큼은 위쪽 부채꼴로, 나머지는 사방으로")]
    [Range(0f, 1f)] public float burstUpRatio = 0.7f;
    [Tooltip("공기저항 (클수록 빨리 느려짐)")]
    public float burstDrag = 3f;
    public Color burstColorA = new Color(1f, 0.85f, 0.45f, 1f);
    public Color burstColorB = new Color(1f, 1f, 1f, 1f);
    [Tooltip("가득 찬 순간 바가 하얗게 번쩍이는 시간 (초)")]
    public float flashTime = 0.15f;
    [Tooltip("가득 찬 순간 마름모·[Q] 퍼즈가 커졌다 돌아오는 배율과 시간")]
    public float popScale = 1.6f;
    public float popTime = 0.25f;

    class Spark { public RectTransform rt; public Image img; public Vector2 vel; public float life, maxLife, size; public Color col; }
    readonly List<Spark> sparks = new List<Spark>();
    RectTransform burstRoot;
    static Sprite dotSprite;
    bool wasReady, readyInit;
    float flashTimer, popTimer;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);

        if (gaugeFill == null && gaugeSlider != null && gaugeSlider.fillRect != null)
            gaugeFill = gaugeSlider.fillRect.GetComponent<Image>();

        // [2026-10-21 수정] 마름모·안내는 늘림이 없는 mp(슬라이더) 밑에 붙이고, 위치만 매 프레임 '실제로 보이는 채움 이미지' 끝에 맞춘다.
        // (보이는 바 Fill Area 는 mp 기준 왼쪽 20·아래 11 로 옮겨져 있고 세로로 2.58배 늘려져 있어서,
        //  그 밑에 붙이면 마름모·글자가 같이 늘어나 찌그러졌다)
        if (gaugeSlider != null)
        {
            BuildReadyHint((RectTransform)gaugeSlider.transform);
            if (gaugeFill != null) fillRT = gaugeFill.rectTransform;
            BuildBurst((RectTransform)gaugeSlider.transform);
        }
        SetReadyHint(false, 0f);
    }

    // 바 오른쪽 끝의 마름모 + 그 아래 [Q] 퍼즈 안내를 만든다
    void BuildReadyHint(RectTransform bar)
    {
        // 마름모
        RectTransform m = NewRect("ReadyMarker", bar, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        m.anchoredPosition = new Vector2(markerSize * 0.75f, 0f);
        m.sizeDelta = new Vector2(markerSize, markerSize);
        m.localRotation = Quaternion.Euler(0f, 0f, 45f);
        marker = m.gameObject.AddComponent<Image>();
        marker.raycastTarget = false;

        // [Q] 퍼즈 (오른쪽 끝을 바 오른쪽 끝에 맞춤)
        RectTransform h = NewRect("ReadyHint", bar, new Vector2(1f, 0.5f), new Vector2(1f, 1f));
        h.anchoredPosition = hintOffset;
        h.sizeDelta = new Vector2(90f, 22f);
        hintGroup = h.gameObject.AddComponent<CanvasGroup>();
        hintGroup.blocksRaycasts = false;
        hintGroup.interactable = false;

        TMP_Text label = NewText("Label", h, labelText, 17f, TextAlignmentOptions.MidlineRight);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        label.ForceMeshUpdate();
        float labelW = label.preferredWidth;

        RectTransform k = NewRect("Key", h, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        k.sizeDelta = new Vector2(22f, 22f);
        k.anchoredPosition = new Vector2(-(labelW + 6f), 0f);
        if (keyBoxSprite != null)
        {
            Image box = k.gameObject.AddComponent<Image>();
            box.sprite = keyBoxSprite;
            box.type = Image.Type.Sliced;
            box.color = hintColor;
            box.raycastTarget = false;
        }
        TMP_Text kt = NewText("Text", k, keyText, 15f, TextAlignmentOptions.Center);
        RectTransform kr = kt.rectTransform;
        kr.anchorMin = Vector2.zero; kr.anchorMax = Vector2.one;
        kr.offsetMin = Vector2.zero; kr.offsetMax = Vector2.zero;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        return r;
    }

    TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        RectTransform r = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (labelFont != null) t.font = labelFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = hintColor;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    void Update()
    {
        if (PauseManager.Instance == null) return;

        PauseManager pm = PauseManager.Instance;
        bool isPaused = pm.IsPaused;
        float gauge = pm.GaugeValue;
        bool ready = !isPaused && gauge >= 100f;
        float udt = Time.unscaledDeltaTime;

        // [2026-10-21] 막 가득 찬 순간 한 번 → 빛 입자 터짐 + 바 번쩍임 + 표시 '톡' (시작할 때 이미 가득 차 있으면 안 터짐)
        if (!readyInit) { wasReady = ready; readyInit = true; }
        else if (ready && !wasReady) PlayReadyBurst();
        wasReady = ready;
        UpdateSparks(udt);

        // 게이지 바
        // 퍼즈 중엔 "남은 퍼즈 시간 비율"로, 평소엔 "충전된 게이지 비율"로 표시한다.
        // (GaugeValue는 퍼즈 발동 즉시 0으로 리셋되므로, 그대로 쓰면 발동하자마자 슬라이더가 확 비어버린다)
        if (gaugeSlider != null)
        {
            if (isPaused && pm.MaxTime > 0f)
                gaugeSlider.value = pm.RemainingTime / pm.MaxTime;
            else if (!isPaused)
                gaugeSlider.value = gauge / 100f;
        }

        // 바 색: 목표 색으로 colorFadeTime 동안 부드럽게 (움직임은 없음)
        if (gaugeFill != null)
        {
            Color target = isPaused ? pausingColor : ready ? readyColor : normalColor;
            if (!colorInit) { currentColor = target; colorInit = true; }
            float k = colorFadeTime > 0f ? Mathf.Clamp01(Time.unscaledDeltaTime / colorFadeTime) : 1f;
            currentColor = Color.Lerp(currentColor, target, k);
            Color shown = currentColor;
            if (flashTimer > 0f)
            {
                shown = Color.Lerp(currentColor, Color.white, flashTime > 0f ? flashTimer / flashTime : 0f);
                flashTimer -= udt;
            }
            gaugeFill.color = shown;
        }

        // 마름모 깜빡임 + [Q] 퍼즈 안내 (ESC 메뉴로 멈춰 있어도 움직이도록 실제 시간 기준)
        float blink = (Mathf.Sin(Time.unscaledTime * markerBlinkPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
        if (ready) PlaceReadyHint();
        SetReadyHint(ready, blink);

        // 퍼즈 패널
        if (pausePanel != null) pausePanel.SetActive(isPaused);

        if (!isPaused) return;

        // 남은 시간 / 거리 (별도 슬라이더가 연결돼 있을 때만 갱신)
        if (timeSlider != null)
        {
            timeSlider.maxValue = pm.MaxTime;
            timeSlider.value = pm.RemainingTime;
        }

        if (distSlider != null)
        {
            distSlider.maxValue = pm.MaxDist;
            distSlider.value = pm.RemainingDist;
        }
    }

    // [2026-10-21 추가] 보이는 채움 이미지의 오른쪽 끝(세로 가운데)에 마름모, 오른쪽 아래 모서리 밑에 [Q] 퍼즈
    void PlaceReadyHint()
    {
        if (fillRT == null || gaugeSlider == null) return;
        Transform bar = gaugeSlider.transform;
        fillRT.GetWorldCorners(fillCorners);          // 0 왼아래, 1 왼위, 2 오른위, 3 오른아래
        Vector3 tr = bar.InverseTransformPoint(fillCorners[2]);
        Vector3 br = bar.InverseTransformPoint(fillCorners[3]);
        float midY = (tr.y + br.y) * 0.5f;

        if (marker != null)
            marker.rectTransform.localPosition = new Vector3(br.x + markerSize * 0.75f, midY, 0f);
        if (hintGroup != null)
            ((RectTransform)hintGroup.transform).localPosition = new Vector3(br.x + hintOffset.x, br.y - hintGap + hintOffset.y, 0f);
    }

    void SetReadyHint(bool on, float blink)
    {
        // 가득 찬 순간 커졌다가 돌아오는 '톡'
        float s = 1f;
        if (popTimer > 0f)
        {
            float u = popTime > 0f ? popTimer / popTime : 0f;   // 1 → 0
            s = 1f + (popScale - 1f) * u * u;
            popTimer -= Time.unscaledDeltaTime;
        }
        if (marker != null) marker.rectTransform.localScale = new Vector3(s, s, 1f);
        if (hintGroup != null) hintGroup.transform.localScale = new Vector3(s, s, 1f);

        if (marker != null)
        {
            marker.enabled = on;
            Color c = markerColor;
            c.a = Mathf.Lerp(markerMinAlpha, 1f, blink);
            marker.color = c;
        }
        if (hintGroup != null) hintGroup.alpha = on ? 1f : 0f;
    }

    // ── [2026-10-21 추가] 가득 찼을 때 빛 입자 ─────────────────────────
    void BuildBurst(RectTransform bar)
    {
        burstRoot = NewRect("ReadyBurst", bar, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f));
        burstRoot.sizeDelta = Vector2.zero;
        burstRoot.SetAsLastSibling();   // 바·마름모·안내보다 위에 그림
        if (dotSprite == null) dotSprite = MakeDotSprite(32);
        for (int i = 0; i < Mathf.Max(0, burstCount); i++)
        {
            RectTransform r = NewRect("Spark", burstRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Image img = r.gameObject.AddComponent<Image>();
            img.sprite = dotSprite;
            img.raycastTarget = false;
            img.enabled = false;
            sparks.Add(new Spark { rt = r, img = img });
        }
    }

    // 가운데가 밝고 가장자리로 갈수록 흐려지는 동그란 점 (파일 없이 코드로 한 번만 만듦)
    static Sprite MakeDotSprite(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 가운데 ~ 1 가장자리
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    void PlayReadyBurst()
    {
        flashTimer = flashTime;
        popTimer = popTime;
        if (readySfx != null) AudioManager.PlaySFX(readySfx);
        if (fillRT == null || gaugeSlider == null || burstRoot == null) return;

        // 보이는 채움 이미지의 범위 (mp 기준 좌표)
        Transform bar = gaugeSlider.transform;
        fillRT.GetWorldCorners(fillCorners);
        Vector3 bl = bar.InverseTransformPoint(fillCorners[0]);
        Vector3 tr = bar.InverseTransformPoint(fillCorners[2]);
        float w = tr.x - bl.x;
        // burstRoot 는 mp 의 왼쪽 가운데가 기준점이므로 그 좌표로 옮김
        Vector3 rootPos = burstRoot.localPosition;

        // [2026-10-21 수정] 바 전체에 고르게: 바를 입자 수만큼 칸으로 나누고 칸마다 하나씩 (칸 안에서는 무작위)
        int n = sparks.Count;
        for (int i = 0; i < n; i++)
        {
            Spark s = sparks[i];
            float x = Random.value < burstRightBias
                ? Mathf.Lerp(tr.x - w * 0.3f, tr.x, Random.value)
                : Mathf.Lerp(bl.x, tr.x, (i + Random.value) / n);
            float y = Mathf.Lerp(bl.y, tr.y, Random.value);
            s.rt.localPosition = new Vector3(x - rootPos.x, y - rootPos.y, 0f);

            float ang = Random.value < burstUpRatio ? Random.Range(30f, 150f) : Random.Range(0f, 360f);
            float spd = Random.Range(burstSpeed.x, burstSpeed.y);
            s.vel = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * spd;
            s.life = 0f;
            s.maxLife = Random.Range(burstLifetime.x, burstLifetime.y);
            s.size = Random.Range(burstSize.x, burstSize.y);
            s.col = Color.Lerp(burstColorA, burstColorB, Random.value);
            s.rt.sizeDelta = new Vector2(s.size, s.size);
            s.img.color = s.col;
            s.img.enabled = true;
        }
    }

    void UpdateSparks(float dt)
    {
        float damp = Mathf.Exp(-burstDrag * dt);
        foreach (Spark s in sparks)
        {
            if (!s.img.enabled) continue;
            s.life += dt;
            float t = s.maxLife > 0f ? s.life / s.maxLife : 1f;
            if (t >= 1f) { s.img.enabled = false; continue; }
            s.vel *= damp;
            s.rt.localPosition += (Vector3)(s.vel * dt);
            float sz = s.size * (1f - 0.7f * t);
            s.rt.sizeDelta = new Vector2(sz, sz);
            Color c = s.col; c.a = s.col.a * (1f - t * t);
            s.img.color = c;
        }
    }
}
