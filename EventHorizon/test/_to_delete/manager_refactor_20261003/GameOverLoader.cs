using UnityEngine;

/// <summary>
/// 게임 시작 시 GameOverCanvas 프리팹을 한 번 생성해두고(평소엔 비활성 상태) 대기시킨다.
/// GameMenuLoader와 같은 패턴 — 실제로 켜는 건 CharacterManager.GameOver() → GameOverUI.Show()가 담당하고,
/// 이 스크립트는 "씬에 인스턴스를 한 번 만들어 GameOverUI.Instance를 준비해두는 것"만 담당한다.
/// </summary>
public class GameOverLoader : MonoBehaviour
{
    [Tooltip("게임 시작 시 한 번 생성해둘 게임오버 캔버스 프리팹")]
    public GameObject gameOverCanvasPrefab;

    GameObject instance;

    void Awake()
    {
        // 씬 안에 이미 게임오버 UI가 있다면(수동으로 배치해둔 경우 등) 중복 생성하지 않고 그걸 그대로 씀
        GameOverUI existing = FindObjectOfType<GameOverUI>(true);
        if (existing != null)
        {
            instance = existing.gameObject;
            return;
        }

        if (gameOverCanvasPrefab == null)
        {
            Debug.LogWarning("[GameOverLoader] gameOverCanvasPrefab이 비어있습니다.");
            return;
        }

        instance = Instantiate(gameOverCanvasPrefab);
        // 프리팹 루트가 기본 활성 상태로 생성돼도 GameOverUI.Awake()가 스스로 SetActive(false)로 꺼둔다.
    }
}
