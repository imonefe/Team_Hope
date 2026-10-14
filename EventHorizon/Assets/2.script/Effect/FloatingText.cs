using UnityEngine;
using TMPro;

/// <summary>
/// 데미지 숫자 / 재화 획득 등, 월드 공간에 잠깐 떴다가 위로 떠오르며 사라지는 텍스트.
/// 루트에 World Space Canvas를 두고 그 자식으로 TextMeshProUGUI 텍스트를 두는 구조
/// (프로젝트의 다른 UI들과 동일하게 TextMeshProUGUI를 재사용 — Canvas 하나짜리 미니 UI라고 보면 됨).
/// FloatingTextSpawner를 통해 스폰하는 걸 권장 (Show/ShowDamage는 스폰 직후 1회 호출).
/// </summary>
public class FloatingText : MonoBehaviour
{
    [Header("연출")]
    public float lifetime = 0.8f;
    public float riseDistance = 1.2f;
    public float randomXOffset = 0.3f;

    [Header("기본 색 (Show 계열에서 덮어씀)")]
    public Color defaultColor = Color.white;
    [Header("치명타 데미지 색/크기 배율")]
    public Color critColor = new Color(1f, 0.25f, 0.15f);
    public float critScaleMultiplier = 1.4f;

    [Tooltip("실제 텍스트가 표시될 TMP_Text (자식의 TextMeshProUGUI). 비워두면 자동으로 자식에서 찾음")]
    public TMP_Text label;

    Vector3 startPos;
    float timer;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        Vector3 offset = new Vector3(Random.Range(-randomXOffset, randomXOffset), 0f, 0f);
        transform.position += offset;
        startPos = transform.position;
        Destroy(gameObject, lifetime);
    }

    /// <summary>텍스트 내용과 색을 지정해서 표시. 스폰 직후 한 번만 호출.</summary>
    public void Show(string text, Color color)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.text = text;
        label.color = color;
    }

    /// <summary>데미지 숫자용 — 치명타면 색/크기를 다르게 표시.</summary>
    public void ShowDamage(int amount, bool isCrit)
    {
        Show(amount.ToString(), isCrit ? critColor : defaultColor);
        if (isCrit) transform.localScale *= critScaleMultiplier;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = lifetime > 0f ? Mathf.Clamp01(timer / lifetime) : 1f;

        // 처음엔 빠르게 떠오르다 서서히 멈추는 느낌 (ease-out)
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = startPos + Vector3.up * (eased * riseDistance);

        if (label != null)
        {
            Color c = label.color;
            c.a = 1f - t;
            label.color = c;
        }
    }
}
