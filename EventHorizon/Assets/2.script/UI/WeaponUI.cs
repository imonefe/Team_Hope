using UnityEngine;

public class WeaponUI : MonoBehaviour
{
    PlayerController player;

    public GameObject swordIcon;
    public GameObject hammerIcon;
    public GameObject gloveIcon;

    void Start()
    {
        CharacterManager.Instance.OnCharacterChanged += SetPlayer;
        SetPlayer(CharacterManager.Instance.CurrentCharacter);
    }

    void SetPlayer(GameObject obj)
    {
        // 기존 이벤트 해제
        if (player != null)
            player.OnWeaponChanged -= UpdateIcon;

        player = obj.GetComponent<PlayerController>();

        if (player != null)
        {
            // 무기 변경 이벤트 구독
            player.OnWeaponChanged += UpdateIcon;
            // 현재 무기로 즉시 갱신
            UpdateIcon(player.weapontype);
        }
    }

    void UpdateIcon(Weapontype type)
    {
        swordIcon.SetActive(false);
        hammerIcon.SetActive(false);
        gloveIcon.SetActive(false);

        switch (type)
        {
            case Weapontype.Sword: swordIcon.SetActive(true); break;
            case Weapontype.Hammer: hammerIcon.SetActive(true); break;
            case Weapontype.Glove: gloveIcon.SetActive(true); break;
        }
    }

    void OnDestroy()
    {
        if (player != null)
            player.OnWeaponChanged -= UpdateIcon;

        // [2026-09-29 추가] Start()에서 구독한 CharacterManager.OnCharacterChanged 해제 누락 수정.
        // (HpUI.cs와 동일한 문제 — 씬 재시작 후 파괴된 오브젝트에 이벤트가 걸려서 예외가 날 수 있었음)
        if (CharacterManager.Instance != null)
            CharacterManager.Instance.OnCharacterChanged -= SetPlayer;
    }
}