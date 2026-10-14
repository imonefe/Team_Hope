using UnityEngine;

/// <summary>
/// 게임 시작 시 필요한 UI 캔버스 프리팹들을 한 번씩 생성해두는 스크립트.
/// 예전의 GameMenuLoader + GameOverLoader 를 하나로 합친 것.
///
///  - 일시정지 메뉴(GameMenuCanvas): 생성 후 꺼두고, ESC를 누르면 켜준다.
///    켜지는 순간(OnEnable) GameMenuController가 스스로 패널을 띄우고 게임을 멈추며,
///    닫힐 때(Resume) 스스로 다시 꺼진다. 인벤토리가 열려 있으면 ESC는 인벤토리부터 닫는다.
///  - 게임오버 화면(GameOverCanvas): 생성만 해둔다. 실제로 켜는 건
///    CharacterManager.GameOver() → GameOverUI.Show() 가 담당한다.
///
/// 씬 안에 이미 같은 UI가 배치되어 있으면 새로 만들지 않고 그걸 그대로 쓴다.
/// 앞으로 이런 캔버스가 늘어나면 여기에 칸을 하나 추가하면 된다.
/// </summary>
public class UILoader : MonoBehaviour
{
    [Header("일시정지 메뉴")]
    [Tooltip("게임 시작 시 한 번 생성해둘 일시정지(설정) 메뉴 프리팹")]
    public GameObject gameMenuCanvasPrefab;
    [Tooltip("일시정지 메뉴를 여는 키")]
    public KeyCode menuKey = KeyCode.Escape;

    [Header("게임오버 화면")]
    [Tooltip("게임 시작 시 한 번 생성해둘 게임오버 캔버스 프리팹")]
    public GameObject gameOverCanvasPrefab;

    GameObject gameMenu;

    void Awake()
    {
        SetupGameMenu();
        SetupGameOver();
    }

    // ───────── 일시정지 메뉴 ─────────

    void SetupGameMenu()
    {
        // 씬 안에 이미 설정 메뉴가 있다면(수동으로 배치해둔 경우 등) 중복 생성하지 않고 그걸 그대로 씀
        GameMenuController existing = FindObjectOfType<GameMenuController>(true);
        if (existing != null)
        {
            gameMenu = existing.gameObject;
            return;
        }

        if (gameMenuCanvasPrefab == null)
        {
            Debug.LogWarning("[UILoader] gameMenuCanvasPrefab이 비어있습니다.");
            return;
        }

        gameMenu = Instantiate(gameMenuCanvasPrefab);

        // 프리팹의 루트 오브젝트는 기본적으로 활성 상태로 생성되기 때문에(그 순간 OnEnable도 같이
        // 실행되어 설정 패널이 잠깐 열림), 렌더링이 일어나기 전인 이 시점에 바로 꺼서
        // 평소 상태(비활성)로 되돌린다. 같은 프레임 안에서 처리되므로 화면 깜빡임은 없다.
        gameMenu.SetActive(false);
    }

    void Update()
    {
        if (gameMenu == null) return;
        if (gameMenu.activeSelf) return; // 이미 열려있으면 GameMenuController가 알아서 처리하므로 무시

        if (!Input.GetKeyDown(menuKey)) return;

        // [2026-10-10 추가] 게임오버 화면에서는 ESC 메뉴를 열지 않는다 (두 화면이 겹쳐 버튼이 함께 눌리고,
        // 메뉴를 닫으면 게임오버 뒤에서 게임이 다시 흘렀음)
        if (GamePause.Has(GamePause.GameOver)) return;

        // 인벤토리가 열려있는 상태에서 ESC를 누르면 설정 메뉴를 열지 않고 인벤토리부터 닫는다.
        if (Inventory.Instance != null && Inventory.Instance.IsOpen)
        {
            Inventory.Instance.Close();
            return;
        }

        gameMenu.SetActive(true);
    }

    // ───────── 게임오버 화면 ─────────

    void SetupGameOver()
    {
        // 씬 안에 이미 게임오버 UI가 있다면 중복 생성하지 않음
        GameOverUI existing = FindObjectOfType<GameOverUI>(true);
        if (existing != null) return;

        if (gameOverCanvasPrefab == null)
        {
            Debug.LogWarning("[UILoader] gameOverCanvasPrefab이 비어있습니다.");
            return;
        }

        // 프리팹 루트가 기본 활성 상태로 생성돼도 GameOverUI.Awake()가 스스로 SetActive(false)로 꺼둔다.
        Instantiate(gameOverCanvasPrefab);
    }
}
