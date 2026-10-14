using UnityEngine;

public class Box : ProximityInteractable
{
    [Header("스폰 포인트")]
    public Transform spawnPoint;

    [Header("드랍 아이템 목록")]
    public GameObject[] itemPrefabs;

    [Header("스킬 + 무기 드랍 (선택 — 켜면 위 itemPrefabs 대신 이쪽을 사용)")]
    [Tooltip("켜면 DropItem() 시 itemPrefabs에서 하나 뽑는 대신, 아래 두 목록에서 각각 하나씩 뽑아 " +
             "스킬은 왼쪽으로, 무기는 오른쪽으로 동시에 튕겨져 나온다.")]
    public bool dropSkillAndWeapon = false;
    [Tooltip("SkillPickup 컴포넌트가 붙은 프리팹들 (왼쪽으로 드랍)")]
    public GameObject[] skillPickupPrefabs;
    [Tooltip("Weapon 컴포넌트가 붙은 프리팹들 (오른쪽으로 드랍)")]
    public GameObject[] weaponPickupPrefabs;

    [Header("아이템 튀어오르는 힘")]
    public float launchForceX = 2f;
    public float launchForceY = 6f;

    [Header("애니메이션")]
    public Animator animator;
    public string openTrigger = "Open";

    [Header("콜라이더")]
    public Collider2D interactCollider;

    bool isActive = false;
    bool isOpened = false;

    void Awake()
    {
        SetActive(false);
    }

    public void SetActive(bool value)
    {
        isActive = value;
        isOpened = false;
        if (interactCollider != null)
            interactCollider.enabled = isActive;

        // [2026-10-10 추가] 상자가 나타나는 순간 내용물을 미리 정해 확보한다
        // (같은 방에 상자가 여러 개 나와도 서로 다른 새 아이템이 들어가도록)
        if (value) PlanDrop();
    }

    // ── 내용물 미리 정하기 ─────────────────────────────────
    GameObject plannedItemPrefab;
    ItemData plannedItemData;

    void PlanDrop()
    {
        if (dropSkillAndWeapon || plannedItemPrefab != null) return;
        plannedItemPrefab = PickNewItemPrefab(itemPrefabs);
        if (plannedItemPrefab == null) return;
        plannedItemData = plannedItemPrefab.GetComponent<Item>().data;
        RunManager.Instance?.HoldItem(plannedItemData, this);
    }

    void ReleasePlan()
    {
        if (plannedItemData != null) RunManager.Instance?.ReleaseItem(plannedItemData, this);
        plannedItemPrefab = null;
        plannedItemData = null;
    }

    void OnDestroy()
    {
        ReleasePlan(); // 열지 않고 사라지면 확보 해제
    }

    // 목록에서 플레이어에게 새 아이템(안 얻었고, 바닥·다른 상자에 확보돼 있지 않은 것)만 골라 무작위로 하나
    static GameObject PickNewItemPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || RunManager.Instance == null) return null;
        var pool = new System.Collections.Generic.List<GameObject>();
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            Item it = p.GetComponent<Item>();
            if (it != null && RunManager.Instance.IsNewItem(it.data)) pool.Add(p);
        }
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    /// <summary>
    /// 이 상자 프리팹에서 지금 플레이어에게 새로운 것을 줄 수 있는지 (BoxSpawner가 상자 종류를 고를 때 사용).
    /// 일반 상자: 새 아이템이 하나라도 남아 있으면 true / 스킬·무기 상자: 줄 수 있는 스킬이나 무기가 있으면 true
    /// </summary>
    public static bool HasSomethingNew(GameObject boxPrefab)
    {
        if (boxPrefab == null) return false;
        Box b = boxPrefab.GetComponent<Box>();
        if (b == null) return false;

        if (b.dropSkillAndWeapon)
        {
            if (b.skillPickupPrefabs != null)
                foreach (var p in b.skillPickupPrefabs) if (p != null && CanDropSkill(p)) return true;
            if (b.weaponPickupPrefabs != null)
                foreach (var p in b.weaponPickupPrefabs) if (p != null && CanDropWeapon(p)) return true;
            return false;
        }
        return PickNewItemPrefab(b.itemPrefabs) != null;
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동. 여기서는 "지금 열 수 있는 상태인가"만 판단한다.
    protected override bool CanInteract() => isActive && !isOpened;

    protected override void Interact()
    {
        OpenBox();
    }

    void OpenBox()
    {
        Debug.Log("[Box] OpenBox 호출!");
        isOpened = true;
        if (animator != null)
        {
            Debug.Log($"[Box] SetTrigger: {openTrigger}");
            animator.SetTrigger(openTrigger);
        }
        else Debug.LogWarning("[Box] Animator 없음!");
    }

    [Header("아이템이 다 떨어졌을 때 대신 주는 재화")]
    public int fallbackCurrencyMin = 10;
    public int fallbackCurrencyMax = 20;


    // 무기: 지금 들고 있는 무기와 같은 무기는 제외
    static bool CanDropWeapon(GameObject prefab)
    {
        Weapon w = prefab.GetComponent<Weapon>();
        if (w == null || w.data == null) return true;
        PlayerController pc = PlayerController.Instance;
        return pc == null || pc.EquippedWeapon != w.data;
    }

    // 스킬: 이미 가진 스킬 제외 + 일반 스킬인데 1·2번 칸이 다 차 있으면 제외 (주워도 넣을 수 없음)
    static bool CanDropSkill(GameObject prefab)
    {
        SkillPickup sp = prefab.GetComponent<SkillPickup>();
        if (sp == null || sp.skillPrefab == null) return true;
        PlayerController pc = PlayerController.Instance;
        CharacterSkills skills = pc != null ? pc.GetComponent<CharacterSkills>() : null;
        if (skills == null) return true;

        bool normalSlotFree = false;
        for (int i = 0; i < 3; i++)
        {
            Skill owned = skills.GetSkill(i);
            if (owned == sp.skillPrefab) return false;        // 이미 가진 스킬
            if (i >= 1 && owned == null) normalSlotFree = true;
        }
        if (sp.skillPrefab.skillType == Skill.SkillType.Normal && !normalSlotFree) return false;
        return true;
    }

    // 애니메이션 이벤트에서 호출
    public void DropItem()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;

        if (dropSkillAndWeapon)
        {
            // [2026-10-10 수정] 플레이어가 지금 들고 있는 무기 / 이미 가진 스킬 / 넣을 칸이 없는 스킬은 후보에서 뺀다.
            // 남은 후보가 없으면 그 자리는 재화로 대신 준다.
            LaunchOne(skillPickupPrefabs, pos, -1, CanDropSkill);  // 스킬 — 왼쪽
            LaunchOne(weaponPickupPrefabs, pos, 1, CanDropWeapon); // 무기 — 오른쪽
            return;
        }

        // [2026-10-10 수정] 상자가 나타날 때 미리 정해둔 새 아이템을 드랍한다.
        // 그 사이 같은 아이템을 다른 곳에서 얻어버렸으면 지금 기준으로 새 아이템을 다시 고른다.
        GameObject prefab = plannedItemPrefab;
        if (prefab == null || (RunManager.Instance != null && RunManager.Instance.IsObtained(plannedItemData)))
        {
            ReleasePlan();
            prefab = PickNewItemPrefab(itemPrefabs);
        }

        if (prefab == null)
        {
            // 새 아이템이 하나도 남지 않았으면(전부 모음) 재화로 대신 지급
            ReleasePlan();
            if (RunManager.Instance != null)
                RunManager.Instance.DropRandomCurrency(pos, fallbackCurrencyMin, fallbackCurrencyMax);
            return;
        }

        GameObject item = Instantiate(prefab, pos, Quaternion.identity); // 생성된 아이템이 스스로 확보를 이어받음
        ReleasePlan();

        Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float randX = Random.Range(-launchForceX, launchForceX);
            rb.AddForce(new Vector2(randX, launchForceY), ForceMode2D.Impulse);
        }
    }

    // prefabs 중 조건(canPick)을 통과한 것 하나를 랜덤으로 뽑아 pos에 생성하고, dir 방향(-1=왼쪽/1=오른쪽)으로 튕겨낸다.
    // 통과한 게 없으면 재화로 대신 지급 (목록 자체가 비어 있으면 아무것도 안 함)
    void LaunchOne(GameObject[] prefabs, Vector3 pos, int dir, System.Predicate<GameObject> canPick)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        GameObject spawned = SpawnUtil.InstantiateRandom(prefabs, pos, canPick);
        if (spawned == null)
        {
            if (RunManager.Instance != null)
                RunManager.Instance.DropRandomCurrency(pos, fallbackCurrencyMin, fallbackCurrencyMax);
            return;
        }

        Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float x = Random.Range(launchForceX * 0.5f, launchForceX) * dir;
            rb.AddForce(new Vector2(x, launchForceY), ForceMode2D.Impulse);
        }
    }
}
