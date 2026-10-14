using UnityEngine;

public class Item : ProximityInteractable
{
    public ItemData data;

    void Start() { DestroyIfCannotSpawn(); }

    // [2026-10-10 추가] 바닥에 있는 동안은 이 아이템을 확보해 둔다 → 다른 상자가 같은 아이템을 또 뽑지 않음
    void Awake()
    {
        if (RunManager.Instance != null && data != null)
            RunManager.Instance.HoldItem(data, this);
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null && data != null)
            RunManager.Instance.ReleaseItem(data, this);
    }

    // 스폰 불가 조건(RunManager.CanSpawn == false)이면 파괴하고 true 반환
    bool DestroyIfCannotSpawn()
    {
        if (RunManager.Instance != null && !RunManager.Instance.CanSpawn(data))
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    // [2026-09-29 변경] Player 태그 감지 + F키 입력 처리(Update/OnTrigger)는 공용 베이스
    // ProximityInteractable로 이동.
    protected override void Interact()
    {
        Pickup();
    }

    void Pickup()
    {
        if (playerObj == null) return;

        if (DestroyIfCannotSpawn()) return;

        Stats stats = playerObj.GetComponent<Stats>();
        if (stats == null) return;

        if (data.effects != null)
            foreach (var effect in data.effects)
                if (effect != null)
                    effect.Apply(stats, playerObj.GetComponent<MonoBehaviour>());

        // 인벤토리 UI 목록에 반영 (기존엔 호출이 안 되어 있어서 인벤토리 창이 항상 비어있었음)
        Inventory.Instance?.AddPassive(data);

        RunManager.Instance?.RegisterObtained(data);

        // 주워서 즉시 파괴되면 OnTriggerExit2D가 안 불릴 수 있으므로, 팝업이 계속 떠있는 채로
        // 남아있지 않도록 여기서 직접 닫아준다.
        ItemInfoPopup.Instance?.Hide();

        // [2026-10-14 추가] 획득 효과 (등급색 빛·고리·알갱이 + 아이콘 떠오름 + 이름)
        ItemPickupEffect.Spawn(transform.position, data);

        Destroy(gameObject);
    }

    // 획득 범위 진입 — 플레이어 머리 위에 아이템 정보 팝업 표시
    protected override void OnPlayerEnter(Collider2D other)
    {
        ItemInfoPopup.Instance?.ShowNearItem(data, playerObj.transform);
    }

    protected override void OnPlayerExit(Collider2D other)
    {
        ItemInfoPopup.Instance?.Hide();
    }
}
