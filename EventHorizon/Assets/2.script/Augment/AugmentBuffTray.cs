using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-11-21 추가] 체력바 오른쪽 끝에 맞춰, 지금 걸려 있는 '몇 초짜리' 증강 효과를 아이템 아이콘으로 보여 준다.
///  - 아이콘 오른쪽 아래에 남은 초 (작은 숫자)
///  - 재사용 대기(cooldown)는 아이콘을 어둡게
///  - 씬의 battle Canvas / AugmentBuffTray 오브젝트에 붙음 (가로 정렬 레이아웃 + 꺼 둔 Slot 견본은 에디터에 미리 만들어 둠)
///
/// 부르는 법: AugmentBuffTray.Show("다음 표적", 아이콘, 2f);  같은 이름이면 시간만 새로 고침
/// </summary>
public class AugmentBuffTray : MonoBehaviour
{
    public static AugmentBuffTray Instance { get; private set; }

    [Tooltip("꺼 둔 견본 칸 (Image + 자식 Time 글자)")]
    public RectTransform slotTemplate;
    [Tooltip("재사용 대기일 때 아이콘 색")]
    public Color cooldownTint = new Color(0.45f, 0.45f, 0.5f, 1f);

    class Entry { public RectTransform rt; public Image icon; public TMP_Text time; public float until; public bool cooldown; }
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    readonly List<string> dead = new List<string>();

    void Awake()
    {
        Instance = this;
        if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Show(string key, Sprite icon, float seconds, bool cooldown = false)
    {
        if (Instance == null || string.IsNullOrEmpty(key) || seconds <= 0f) return;
        Instance.Put(key, icon, seconds, cooldown);
    }

    public static void Clear()
    {
        if (Instance == null) return;
        foreach (var e in Instance.entries.Values) if (e.rt != null) Destroy(e.rt.gameObject);
        Instance.entries.Clear();
    }

    void Put(string key, Sprite icon, float seconds, bool cooldown)
    {
        if (slotTemplate == null) return;
        if (!entries.TryGetValue(key, out var e) || e.rt == null)
        {
            var rt = Instantiate(slotTemplate, slotTemplate.parent);
            rt.name = "Slot_" + key;
            rt.gameObject.SetActive(true);
            e = new Entry { rt = rt, icon = rt.GetComponent<Image>(), time = rt.GetComponentInChildren<TMP_Text>(true) };
            entries[key] = e;
        }
        e.until = Time.time + seconds;
        e.cooldown = cooldown;
        if (e.icon != null) { e.icon.sprite = icon; e.icon.color = cooldown ? cooldownTint : Color.white; e.icon.preserveAspect = true; }
        e.rt.SetAsLastSibling();
    }

    void Update()
    {
        dead.Clear();
        foreach (var kv in entries)
        {
            var e = kv.Value;
            if (e.rt == null) { dead.Add(kv.Key); continue; }
            float left = e.until - Time.time;
            if (left <= 0f) { Destroy(e.rt.gameObject); dead.Add(kv.Key); continue; }
            if (e.time != null) e.time.text = left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0");
        }
        foreach (var k in dead) entries.Remove(k);
    }
}
