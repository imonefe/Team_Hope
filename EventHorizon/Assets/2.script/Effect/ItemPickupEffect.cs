using System.Collections;
using UnityEngine;

/// <summary>
/// 아이템 획득 효과. Item.Pickup() 에서 ItemPickupEffect.Spawn(위치, 아이템데이터) 한 줄로 부른다.
///
/// 아이템 자리에서
///  1) 등급색 빛이 번쩍 (부드러운 빛 덩어리, 밝게 더해짐)
///  2) 고리가 퍼지며 사라짐
///  3) 빛 알갱이가 사방으로 튀었다가 사라짐
///  4) 아이템 아이콘이 톡 커졌다가 위로 떠오르며 사라짐
///  5) 머리 위에 아이템 이름 (등급색, 기존 FloatingTextSpawner 사용 — 씬에 없으면 생략)
///
/// 값 조절: Assets/Etc/Resources/ItemPickupEffect.prefab 의 인스펙터에서. (프리팹이 없으면 아래 기본값으로 동작)
/// 그림 파일 없이 코드로 그리므로, 별도 이미지가 필요 없다.
/// </summary>
public class ItemPickupEffect : MonoBehaviour
{
    [Header("등급 색")]
    public Color commonColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    public Color rareColor = new Color(0.35f, 0.65f, 1f, 1f);
    public Color epicColor = new Color(0.75f, 0.42f, 1f, 1f);
    public Color legendaryColor = new Color(1f, 0.78f, 0.3f, 1f);

    [Header("빛 번쩍임")]
    [Tooltip("빛 덩어리 크기 (월드 단위)")]
    public float flashSize = 2.4f;
    public float flashDuration = 0.35f;
    [Range(0f, 3f)] public float flashBrightness = 1.4f;

    [Header("퍼지는 고리")]
    public float ringStartSize = 0.4f;
    public float ringEndSize = 3.2f;
    public float ringDuration = 0.45f;

    [Header("빛 알갱이")]
    [Range(0, 80)] public int sparkCount = 20;
    public float sparkSpeedMin = 2.5f;
    public float sparkSpeedMax = 5.5f;
    public float sparkSizeMin = 0.08f;
    public float sparkSizeMax = 0.2f;
    public float sparkLifetime = 0.6f;
    [Tooltip("알갱이가 아래로 떨어지는 정도")]
    public float sparkGravity = 0.6f;

    [Header("아이콘")]
    public bool showIcon = true;
    [Tooltip("아이콘 높이 (월드 단위)")]
    public float iconSize = 0.9f;
    [Tooltip("위로 떠오르는 높이")]
    public float iconRise = 1.2f;
    public float iconDuration = 0.8f;

    [Header("이름")]
    public bool showName = true;
    public Vector3 nameOffset = new Vector3(0f, 1.6f, 0f);

    [Header("소리 (선택)")]
    public AudioClip sfx;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("그리는 순서")]
    [Tooltip("플레이어(4000~5000)보다 위")]
    public int sortingOrder = 5100;
    [Tooltip("카메라 쪽으로 당기는 거리. 플레이어는 깊이를 기록하는 셰이더(URP Lit)를 써서, 같은 깊이면 효과가 뒤로 가려짐")]
    public float towardCamera = 1.5f;

    static Sprite softSquare, ringSprite;
    static Material additiveMat, unlitMat;

    /// <summary>획득 효과를 pos 에 띄운다. data 가 없으면 흰색, 아이콘·이름 없이.</summary>
    public static void Spawn(Vector3 pos, ItemData data)
    {
        ItemPickupEffect template = Resources.Load<ItemPickupEffect>("ItemPickupEffect");
        ItemPickupEffect fx = template != null
            ? Instantiate(template, pos, Quaternion.identity)
            : new GameObject("ItemPickupEffect").AddComponent<ItemPickupEffect>();
        pos.z -= fx.towardCamera;      // 2D 카메라는 +Z 를 보므로, z 를 줄이면 카메라 쪽(앞)
        fx.transform.position = pos;
        fx.Play(data);
    }

    void Play(ItemData data)
    {
        Color c = data != null ? TierColor(data.tier) : commonColor;
        EnsureResources();

        StartCoroutine(Flash(c));
        StartCoroutine(Ring(c));
        Sparks(c);
        if (showIcon && data != null && data.icon != null) StartCoroutine(Icon(data.icon));
        if (showName && data != null && !string.IsNullOrEmpty(data.itemName))
            FloatingTextSpawner.SpawnText(transform.position + nameOffset, data.itemName, c);
        if (sfx != null) AudioSource.PlayClipAtPoint(sfx, transform.position, sfxVolume);

        float life = Mathf.Max(flashDuration, ringDuration, sparkLifetime, showIcon ? iconDuration : 0f) + 0.2f;
        Destroy(gameObject, life);
    }

    Color TierColor(ItemTier t)
    {
        switch (t)
        {
            case ItemTier.Rare: return rareColor;
            case ItemTier.Epic: return epicColor;
            case ItemTier.Legendary: return legendaryColor;
            default: return commonColor;
        }
    }

    // ── 각 연출 ──

    IEnumerator Flash(Color c)
    {
        SpriteRenderer sr = MakeRenderer("Flash", softSquare, additiveMat, sortingOrder);
        float t = 0f;
        while (t < flashDuration)
        {
            float k = t / flashDuration;
            float size = flashSize * Mathf.Lerp(0.6f, 1.1f, EaseOut(k));
            sr.transform.localScale = new Vector3(size, size, 1f);
            sr.color = new Color(c.r, c.g, c.b, flashBrightness * (1f - k) * (1f - k));
            t += Time.deltaTime;
            yield return null;
        }
        sr.enabled = false;
    }

    IEnumerator Ring(Color c)
    {
        SpriteRenderer sr = MakeRenderer("Ring", ringSprite, unlitMat, sortingOrder + 1);
        float t = 0f;
        while (t < ringDuration)
        {
            float k = t / ringDuration;
            float size = Mathf.Lerp(ringStartSize, ringEndSize, EaseOut(k));
            sr.transform.localScale = new Vector3(size, size, 1f);
            sr.color = new Color(c.r, c.g, c.b, 1f - k);
            t += Time.deltaTime;
            yield return null;
        }
        sr.enabled = false;
    }

    void Sparks(Color c)
    {
        if (sparkCount <= 0) return;
        GameObject go = new GameObject("Sparks");
        go.layer = gameObject.layer;
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(sparkLifetime * 0.6f, sparkLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeedMin, sparkSpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sparkSizeMin, sparkSizeMax);
        main.startColor = c;
        main.gravityModifier = sparkGravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = sparkCount;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.rotation = Vector3.zero;                 // 화면과 같은 평면(XY)으로 퍼짐

        var vel = ps.limitVelocityOverLifetime;        // 튀어나간 뒤 빠르게 느려짐
        vel.enabled = true;
        vel.dampen = 0.15f;
        vel.limit = 0.5f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = additiveMat;
        r.sortingOrder = sortingOrder + 2;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        go.SetActive(true);
    }

    IEnumerator Icon(Sprite icon)
    {
        SpriteRenderer sr = MakeRenderer("Icon", icon, unlitMat, sortingOrder + 3);
        float h = Mathf.Max(0.0001f, icon.bounds.size.y);
        float baseScale = iconSize / h;
        Vector3 start = Vector3.zero;
        float t = 0f;
        while (t < iconDuration)
        {
            float k = t / iconDuration;
            // 0~25%: 톡 커짐(0.6 → 1.15 → 1), 이후 위로 떠오르며 사라짐
            float pop = k < 0.12f ? Mathf.Lerp(0.6f, 1.15f, k / 0.12f)
                      : k < 0.25f ? Mathf.Lerp(1.15f, 1f, (k - 0.12f) / 0.13f) : 1f;
            sr.transform.localScale = Vector3.one * baseScale * pop;
            sr.transform.localPosition = start + Vector3.up * iconRise * EaseOut(Mathf.InverseLerp(0.15f, 1f, k));
            float a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            sr.color = new Color(1f, 1f, 1f, a);
            t += Time.deltaTime;
            yield return null;
        }
        sr.enabled = false;
    }

    // ── 공용 ──

    SpriteRenderer MakeRenderer(string name, Sprite sprite, Material mat, int order)
    {
        GameObject go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        return sr;
    }

    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

    static void EnsureResources()
    {
        // 밝게 더해지는 부드러운 점 (빛줄기 먼지와 같은 머티리얼 — 모양은 셰이더가 그림)
        if (additiveMat == null) additiveMat = Resources.Load<Material>("LightDust");
        // 고리·아이콘: 빛을 받지 않는 기본 스프라이트 셰이더 (어두운 방에서도 밝게 보이도록)
        if (unlitMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) unlitMat = new Material(sh) { name = "ItemPickupFX Unlit", hideFlags = HideFlags.DontSave };
        }

        if (softSquare == null)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            softSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
            softSquare.hideFlags = HideFlags.DontSave;
        }

        if (ringSprite == null)
        {
            const int N = 128;
            Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.08f);   // 반지름 0.85 근처만 밝은 고리
                    a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect);
            ringSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
