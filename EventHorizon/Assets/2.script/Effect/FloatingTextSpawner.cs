using UnityEngine;

/// <summary>
/// FloatingText를 어디서든 한 줄로 띄울 수 있게 해주는 헬퍼.
/// 씬에 하나(예: GameManager 하위) 배치하고 floatingTextPrefab만 연결해두면,
/// Hitbox / RunManager 등에서 FloatingTextSpawner.SpawnDamage(...) / SpawnText(...) 로 바로 호출 가능.
/// prefab이 연결 안 돼 있거나 씬에 이 오브젝트가 없으면 그냥 조용히 무시된다(팝업 없이 데미지/재화 로직은 정상 진행).
/// </summary>
public class FloatingTextSpawner : MonoBehaviour
{
    public static FloatingTextSpawner Instance;

    [Tooltip("FloatingText 컴포넌트가 붙어있는 프리팹")]
    public GameObject floatingTextPrefab;

    void Awake()
    {
        Instance = this;
    }

    public static void SpawnDamage(Vector3 pos, int amount, bool isCrit)
    {
        GameObject go = Spawn(pos);
        if (go == null) return;
        FloatingText ft = go.GetComponent<FloatingText>();
        if (ft != null) ft.ShowDamage(amount, isCrit);
    }

    public static void SpawnText(Vector3 pos, string text, Color color)
    {
        GameObject go = Spawn(pos);
        if (go == null) return;
        FloatingText ft = go.GetComponent<FloatingText>();
        if (ft != null) ft.Show(text, color);
    }

    static GameObject Spawn(Vector3 pos)
    {
        if (Instance == null || Instance.floatingTextPrefab == null) return null;
        return Instantiate(Instance.floatingTextPrefab, pos, Quaternion.identity);
    }
}
