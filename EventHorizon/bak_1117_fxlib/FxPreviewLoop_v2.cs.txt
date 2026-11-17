// [2026-11-17 신규] 이펙트 모음 미리보기 전용 (FxLibrary_Preview 장면에서만 사용)
// 일정 간격으로 이펙트를 다시 띄운다. 재생 중 '색 번호'를 바꾸면 색이 바뀐다.
using UnityEngine;

public class FxPreviewLoop : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public string name;
        public GameObject[] variants;   // 색 순서대로
        public Vector3 position;
        public bool loop;               // 반복 이펙트: 한 번 띄워 두고 유지
        public bool move;               // 탄 궤적: 좌우로 움직여 꼬리를 보여 줌
    }

    [Tooltip("0 주황, 1 하늘, 2 보라, 3 흰")]
    public int colorIndex = 0;
    public string[] colorNames;
    [Tooltip("다시 띄우는 간격 (초)")]
    public float interval = 1.6f;
    [Tooltip("느리게 보기 (1 = 보통)")]
    [Range(0.1f, 1f)] public float timeScale = 1f;
    public Slot[] slots;
    [Tooltip("-1 = 전체 격자, 0 이상 = 그 번호 이펙트만 가운데서 크게 (번호는 Slots 순서)")]
    public int focus = -1;
    [Tooltip("크게 볼 때 카메라 크기 (작을수록 확대)")]
    public float focusSize = 2.6f;

    Camera cam; Vector3 camHome; float camHomeSize;

    GameObject[] live;
    int shownColor = -1;
    float timer;

    void Start()
    {
        cam = Camera.main;
        if (cam != null) { camHome = cam.transform.position; camHomeSize = cam.orthographicSize; }
        live = new GameObject[slots.Length];
        SpawnAll(true);
    }

    void Update()
    {
        Time.timeScale = timeScale;
        if (cam != null)
        {
            bool f = focus >= 0 && focus < slots.Length;
            var target = f ? new Vector3(slots[focus].position.x, slots[focus].position.y + .6f, camHome.z) : camHome;
            cam.transform.position = target;
            cam.orthographicSize = f ? focusSize : camHomeSize;
        }
        if (colorIndex != shownColor) { SpawnAll(true); return; }

        timer += Time.deltaTime;
        if (timer >= interval) SpawnAll(false);

        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].move || live[i] == null) continue;
            float t = Mathf.Clamp01(timer / (interval * 0.7f));
            live[i].transform.position = slots[i].position + new Vector3(Mathf.Lerp(-3f, 3f, t), 0, 0);
        }
    }

    void SpawnAll(bool all)
    {
        timer = 0f;
        int c = Mathf.Clamp(colorIndex, 0, 3);
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s.loop && !s.move && !all && live[i] != null) continue;
            if (live[i] != null) Destroy(live[i]);
            var prefab = (s.variants != null && c < s.variants.Length) ? s.variants[c] : null;
            if (prefab == null) continue;
            var pos = s.move ? s.position + new Vector3(-3f, 0, 0) : s.position;
            live[i] = Instantiate(prefab, pos, Quaternion.identity);
        }
        shownColor = colorIndex;
    }

    void OnDisable() { Time.timeScale = 1f; }
}
