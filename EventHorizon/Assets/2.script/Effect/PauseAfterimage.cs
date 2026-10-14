using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 퍼즈(Q키) 중 주인공이 움직이면 그 자리에 "멈춘 잔상"을 남긴다.
///  - 주인공이 일정 거리를 움직일 때마다 지금 모습(애니메이션 그림·방향·크기)을 그대로 찍어 둔다 (가만히 있으면 안 생김)
///  - 잔상은 제자리에 얼어 있다가 2~3초(설정값) 뒤 금이 가며 조각나 사라진다 (최근 것은 진하게, 오래된 것은 옅게)
///  - 퍼즈가 끝나는 순간 남아 있던 잔상은 오래된 것부터 차례로 깨진다 (끝날 때 섬광·충격파와 같은 타이밍)
///
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 스스로 생성되고 씬이 바뀌어도 유지된다.
/// PauseManager.Instance.IsPaused 값만 지켜보므로 PauseManager · PlayerController 는 수정하지 않았다.
/// 개수·간격·깨지는 시간은 Resources/PauseGlitchSettings 의 "퍼즈 중 멈춘 잔상" 칸에서,
/// 색·윤곽선·조각 모양은 Resources/PauseAfterimage.mat 머티리얼에서 조절한다.
/// </summary>
public class PauseAfterimage : MonoBehaviour
{
    const string SettingsPath = "PauseGlitchSettings";
    const string MaterialPath = "PauseAfterimage";

    static PauseAfterimage instance;
    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    class Ghost
    {
        public SpriteRenderer renderer;
        public Vector3 basePosition;
        public Vector3 baseScale;
        public float seed;
        public float dieTime;              // 이 시각(실제 시간)이 되면 수명이 끝나 사라지기 시작
        public float fadeStart = -1f;      // 0 이상이면 서서히 사라지는 중 (최대 개수 초과)
        public float shatterStart = -1f;   // 0 이상이면 깨지는 중 (퍼즈 끝)
    }

    PauseGlitchSettings settings;
    Material ghostMaterial;
    MaterialPropertyBlock block;

    readonly List<Ghost> ghosts = new List<Ghost>();   // 오래된 것 → 최근 것 순서
    readonly Stack<Ghost> pool = new Stack<Ghost>();                 // 다시 쓰려고 꺼 둔 잔상

    SpriteRenderer target;     // 주인공 스프라이트
    bool wasPaused;
    float lastSpawnTime;
    Vector3 lastSpawnPosition;

    static float Now => Time.unscaledTime;   // 퍼즈·일시정지와 상관없이 실제 시간

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        PauseGlitchSettings s = Resources.Load<PauseGlitchSettings>(SettingsPath);
        if (s == null) return;   // 설정이 없으면 조용히 끔 (PauseGlitchEffect 쪽에서 이미 경고함)

        Material m = Resources.Load<Material>(MaterialPath);
        if (m == null)
        {
            Debug.LogWarning("[PauseAfterimage] Resources 폴더에 PauseAfterimage 머티리얼이 없어 잔상을 끕니다.");
            return;
        }

        GameObject go = new GameObject("[PauseAfterimage]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseAfterimage>();
        instance.settings = s;
        instance.ghostMaterial = m;
        instance.block = new MaterialPropertyBlock();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // 씬이 바뀌면 남아 있던 잔상은 바로 정리 (다른 씬에 떠 있지 않게)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAll();
        target = null;
        wasPaused = false;
    }

    void Update()
    {
        bool paused = settings.afterimageEnabled
                      && PauseManager.Instance != null
                      && PauseManager.Instance.IsPaused;

        if (paused && !wasPaused) BeginPause();
        if (!paused && wasPaused) EndPause();
        wasPaused = paused;

        if (paused) TrySpawn();
        UpdateGhosts();
    }

    // ── 퍼즈 시작: 주인공 찾고, 출발 자리에 첫 잔상 ──
    void BeginPause()
    {
        ClearAll();
        FindTarget();
        if (target == null) return;
        lastSpawnPosition = target.transform.position;
        lastSpawnTime = Now;
        Spawn();
    }

    // ── 퍼즈 끝: 오래된 것부터 차례로 깨지기 시작 ──
    void EndPause()
    {
        int count = ghosts.Count;
        for (int i = 0; i < count; i++)
        {
            Ghost g = ghosts[i];
            if (g.fadeStart >= 0f || g.shatterStart >= 0f) continue;   // 이미 사라지는 중인 것은 그대로
            if (settings.afterimageShatter)
            {
                float delay = count > 1 ? (float)i / (count - 1) * settings.afterimageShatterStagger : 0f;
                g.shatterStart = Now + delay;
            }
            else
            {
                g.fadeStart = Now;
            }
        }
    }

    void FindTarget()
    {
        GameObject player = CharacterManager.Instance != null ? CharacterManager.Instance.CurrentCharacter : null;
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.gameObject;
        }
        target = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
    }

    // ── 움직였을 때만 잔상 하나 ──
    void TrySpawn()
    {
        if (target == null) FindTarget();
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy || target.sprite == null) return;

        if (Now - lastSpawnTime < settings.afterimageMinInterval) return;
        Vector3 pos = target.transform.position;
        float spacing = Mathf.Max(0.01f, settings.afterimageSpacing);
        if ((pos - lastSpawnPosition).sqrMagnitude < spacing * spacing) return;

        Spawn();
        lastSpawnTime = Now;
        lastSpawnPosition = pos;
    }

    void Spawn()
    {
        Ghost g = pool.Count > 0 ? pool.Pop() : CreateGhost();
        Transform t = target.transform;
        SpriteRenderer r = g.renderer;

        // 지금 주인공 모습 그대로 복사 (좌우 뒤집기는 크기의 부호에 들어 있음)
        r.transform.SetPositionAndRotation(t.position, t.rotation);
        r.transform.localScale = t.lossyScale;
        r.sprite = target.sprite;
        r.flipX = target.flipX;
        r.flipY = target.flipY;
        r.sortingLayerID = target.sortingLayerID;
        r.sortingOrder = target.sortingOrder - 1;   // 주인공 바로 뒤
        r.gameObject.layer = target.gameObject.layer;

        g.basePosition = t.position;
        g.baseScale = t.lossyScale;
        g.seed = Random.Range(0f, 100f);
        float lifeMin = Mathf.Max(0f, settings.afterimageLifeMin);
        g.dieTime = Now + Random.Range(lifeMin, Mathf.Max(lifeMin, settings.afterimageLifeMax));
        g.fadeStart = -1f;
        g.shatterStart = -1f;
        SetDissolve(g, 0f);
        r.gameObject.SetActive(true);
        ghosts.Add(g);

        // 최대 개수를 넘으면 가장 오래된 (아직 남아 있는) 잔상부터 서서히 사라지게
        int alive = 0;
        for (int i = 0; i < ghosts.Count; i++) if (ghosts[i].fadeStart < 0f) alive++;
        for (int i = 0; i < ghosts.Count && alive > settings.afterimageMaxCount; i++)
        {
            if (ghosts[i].fadeStart >= 0f) continue;
            ghosts[i].fadeStart = Now;
            alive--;
        }
    }

    Ghost CreateGhost()
    {
        GameObject go = new GameObject("Afterimage");
        go.transform.SetParent(transform, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sharedMaterial = ghostMaterial;
        return new Ghost { renderer = r };
    }

    // ── 진하기·깨짐 갱신 ──
    void UpdateGhosts()
    {
        int count = ghosts.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];

            // 수명이 끝나면 혼자 깨지기(또는 서서히 사라지기) 시작
            if (g.fadeStart < 0f && g.shatterStart < 0f && Now >= g.dieTime)
            {
                if (settings.afterimageLifeShatter) g.shatterStart = Now;
                else g.fadeStart = Now;
            }

            // 최근 것은 진하게, 오래된 것은 옅게
            float rank = count > 1 ? (float)i / (count - 1) : 1f;
            float alpha = settings.afterimageAlpha * Mathf.Lerp(settings.afterimageOldestScale, 1f, rank);

            if (g.fadeStart >= 0f)
            {
                float k = settings.afterimageFadeTime > 0f ? (Now - g.fadeStart) / settings.afterimageFadeTime : 1f;
                if (k >= 1f) { Release(i); continue; }
                alpha *= 1f - k;
            }

            if (g.shatterStart >= 0f)
            {
                float k = settings.afterimageShatterTime > 0f ? (Now - g.shatterStart) / settings.afterimageShatterTime : 1f;
                if (k >= 1f) { Release(i); continue; }
                if (k > 0f)
                {
                    float eased = 1f - (1f - k) * (1f - k);
                    SetDissolve(g, eased);
                    // 살짝 부풀며 위로 흩어지는 느낌
                    g.renderer.transform.localScale = g.baseScale * (1f + 0.08f * eased);
                    g.renderer.transform.position = g.basePosition + Vector3.up * (0.12f * eased);
                }
            }

            g.renderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    void SetDissolve(Ghost g, float value)
    {
        g.renderer.GetPropertyBlock(block);
        // 속성 묶음을 쓰면 스프라이트 그림 연결이 사라지는 경우가 있어 지금 그림을 직접 다시 넣어 줌 (Enemy.HitFlash 와 같은 처리)
        if (g.renderer.sprite != null) block.SetTexture(MainTexId, g.renderer.sprite.texture);
        block.SetFloat(DissolveId, value);
        block.SetFloat(SeedId, g.seed);
        g.renderer.SetPropertyBlock(block);
    }

    void Release(int index)
    {
        Ghost g = ghosts[index];
        ghosts.RemoveAt(index);
        g.renderer.gameObject.SetActive(false);
        g.renderer.transform.localScale = Vector3.one;
        pool.Push(g);
    }

    void ClearAll()
    {
        for (int i = ghosts.Count - 1; i >= 0; i--) Release(i);
    }
}
