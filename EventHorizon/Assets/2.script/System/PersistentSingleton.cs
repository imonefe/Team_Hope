using UnityEngine;

/// <summary>
/// "이미 인스턴스가 있으면 나를 파괴, 아니면 내가 유일한 인스턴스로 등록 + 씬 전환에도 유지"
/// 하는 공통 싱글턴 로직. AudioManager / RunManager / PauseManager 등이 상속해서 사용.
///
/// 사용법: 각자의 Awake()에서 InitSingleton()을 호출하고,
/// 반환값이 false면(중복이라 파괴됨) 그 이후 초기화 코드를 건너뛰면 됨.
/// </summary>
public abstract class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    /// <summary>true = 이 인스턴스가 정상적으로 싱글턴이 됨 / false = 중복이라 파괴됨</summary>
    protected bool InitSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        Instance = this as T;
        // [2026-10-10 추가] DontDestroyOnLoad는 최상위 오브젝트에만 동작한다. 다른 오브젝트의 자식으로
        // 배치돼 있으면(예: GameManager 밑의 RunManager) 경고만 뜨고 씬과 함께 파괴됐으므로 먼저 최상위로 뺀다.
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        return true;
    }
}
