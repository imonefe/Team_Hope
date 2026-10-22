using UnityEngine;

/// <summary>
/// [2026-10-22] 참격 테스트 씬(SlashTest) 전용 조작 스크립트.
/// 게임의 검 1·2·3타 애니메이션(Sword01~03)에서 읽어 온 프레임을 그대로 재생해서
/// SwordSlashFX(검 참격)가 실제 게임과 같은 순간에 나오는지 확인하고, ScreenSlashEffect(화면 가르기)도 눌러 본다.
/// 프레임·시간은 에디터 도구(Tools > 참격 테스트 씬 만들기)가 채워 준다.
///
/// 조작: X = 검 공격(누를 때마다 1→2→3타 이어짐) / 1·2·3 = 그 타만 / G = 화면 가르기 / R = 자동 반복
/// </summary>
public class SlashTestDriver : MonoBehaviour
{
    [System.Serializable]
    public class Combo
    {
        public string name;
        public Sprite[] frames;
        public float[] times;      // 각 프레임이 시작되는 시간(초)
        public float length;       // 클립 길이(초)
    }

    [Header("대상")]
    public SpriteRenderer target;
    public ScreenSlashEffect screenSlash;

    [Header("검 1·2·3타 (에디터 도구가 채움)")]
    public Combo[] combos;
    [Tooltip("재생 속도 (게임의 검 공격 속도와 같게 1.2)")]
    public float speed = 1.2f;

    [Header("화면 가르기")]
    public float slashAngle = 28f;

    [Header("자동 반복")]
    public bool autoPlay = false;
    public float autoInterval = 0.6f;

    int current = -1;     // 재생 중인 타 (없으면 -1)
    float t;
    bool queued;          // 재생 중에 X 를 또 눌렀으면 다음 타로 이어짐
    bool chain;           // X 로 시작한 연속 공격인지 (1·2·3 키는 그 타만)
    float autoTimer;

    void Start()
    {
        ShowIdle();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (current < 0) Begin(0, true);
            else if (chain) queued = true;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1)) Begin(0, false);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Begin(1, false);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Begin(2, false);
        if (Input.GetKeyDown(KeyCode.G) && screenSlash != null && target != null)
            screenSlash.PlayAtWorld(slashAngle, target.bounds.center);
        if (Input.GetKeyDown(KeyCode.R)) { autoPlay = !autoPlay; autoTimer = 0f; }

        if (current >= 0) Step(dt);
        else if (autoPlay)
        {
            autoTimer += dt;
            if (autoTimer >= autoInterval) { autoTimer = 0f; Begin(0, true); }
        }
    }

    void Begin(int index, bool asChain)
    {
        if (combos == null || index < 0 || index >= combos.Length) return;
        current = index;
        t = 0f;
        queued = false;
        chain = asChain;
        ApplyFrame();
    }

    void Step(float dt)
    {
        t += dt * speed;
        Combo c = combos[current];
        if (t < c.length) { ApplyFrame(); return; }

        // 한 타가 끝남 → 이어서 칠지 결정 (자동 반복이면 3타까지 계속)
        bool next = chain && (queued || autoPlay) && current + 1 < combos.Length;
        if (next) Begin(current + 1, true);
        else { current = -1; ShowIdle(); }
    }

    void ApplyFrame()
    {
        Combo c = combos[current];
        if (target == null || c.frames == null || c.frames.Length == 0) return;
        int f = 0;
        for (int i = 0; i < c.times.Length; i++) if (t >= c.times[i]) f = i;
        f = Mathf.Clamp(f, 0, c.frames.Length - 1);
        if (target.sprite != c.frames[f]) target.sprite = c.frames[f];
    }

    void ShowIdle()
    {
        if (target != null && combos != null && combos.Length > 0 && combos[0].frames != null && combos[0].frames.Length > 0)
            target.sprite = combos[0].frames[0];
    }

    GUIStyle style;
    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(12, 12, 10, 10);
        }
        string state = current >= 0 ? combos[current].name : "대기";
        GUI.Box(new Rect(12, 12, 430, 170),
            "참격 테스트\n" +
            "X : 검 공격 (누를 때마다 1→2→3타)\n" +
            "1 / 2 / 3 : 그 타만 재생\n" +
            "G (또는 F) : 화면 가르기\n" +
            "R : 자동 반복 " + (autoPlay ? "[켜짐]" : "[꺼짐]") + "\n" +
            "지금 : " + state, style);
    }
}
