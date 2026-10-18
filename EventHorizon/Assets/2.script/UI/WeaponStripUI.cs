using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// [2026-10-18 추가] 체력 바 오른쪽 끝에 붙는 작은 무기 정보 칸 (시안 B안).
///
///   [무기 이름] [상성] 공격력 15
///   - 공격력 = 공격력(Atk) × 무기 배율 × 공격 버프 (PlayerController.CurrentAttackPower, 적 방어·치명타·상성 적용 전)
///   - 무기를 바꾸면 칸이 살짝 튕긴다.
/// - 값은 PlayerController 에서 읽기만 한다 (공격 코드는 건드리지 않음).
/// - 글자 길이에 맞춰 칸 위치를 자동으로 이어 붙이므로, 글꼴 크기를 바꿔도 겹치지 않는다.
/// - 설정 창의 '무기 정보 칸' 켬/끔은 같은 오브젝트의 HudSettingVisibility 가 처리한다.
///
/// [2026-10-18 수정] 콤보 칸, 망치 차지·건틀릿 속도 바, 무기를 바꿨을 때의 상성 설명 줄을 빼고 공격력만 보여 준다.
/// </summary>
public class WeaponStripUI : MonoBehaviour
{
    [Header("이름 · 상성")]
    public TMP_Text nameText;
    [Tooltip("상성 칸 바탕 (상성 색으로 물듦)")]
    public Image chipBackground;
    public TMP_Text chipText;
    [Tooltip("상성 칸 글자 좌우 여백 (px)")]
    public float chipPadding = 10f;

    [Header("공격력")]
    public TMP_Text attackText;
    [Tooltip("공격력 글자 모양. {0} 자리에 숫자가 들어감")]
    public string attackFormat = "공격력 {0}";

    [Header("간격")]
    [Tooltip("이름 · 상성 · 공격력 사이 간격 (px)")]
    public float spacing = 12f;

    [Header("무기 이름 (Weapontype 순서: 없음, 검, 망치, 건틀릿, 총)")]
    public string[] weaponNames = { "", "검", "망치", "건틀릿", "총" };

    [Header("상성 이름 · 색")]
    public string powerName = "공격형";
    public string speedName = "스피드형";
    public string defenseName = "방어형";
    public Color powerColor = new Color(0.937f, 0.478f, 0.384f, 1f);
    public Color speedColor = new Color(0.498f, 0.827f, 0.839f, 1f);
    public Color defenseColor = new Color(0.624f, 0.706f, 1f, 1f);
    public Color noneColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    PlayerController player;
    Weapontype shownType = (Weapontype)(-1);
    int shownAttack = int.MinValue;

    void Start()
    {
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharacterChanged += SetPlayer;
            SetPlayer(CharacterManager.Instance.CurrentCharacter);
        }
        else
        {
            SetPlayer(PlayerController.Instance != null ? PlayerController.Instance.gameObject : null);
        }
    }

    void OnDestroy()
    {
        if (player != null) player.OnWeaponChanged -= OnWeaponChanged;
        if (CharacterManager.Instance != null) CharacterManager.Instance.OnCharacterChanged -= SetPlayer;
    }

    void SetPlayer(GameObject obj)
    {
        if (player != null) player.OnWeaponChanged -= OnWeaponChanged;
        player = obj != null ? obj.GetComponent<PlayerController>() : null;
        if (player != null)
        {
            player.OnWeaponChanged += OnWeaponChanged;
            ApplyWeapon(player.weapontype);
        }
    }

    void OnWeaponChanged(Weapontype type)
    {
        bool changed = type != shownType;
        ApplyWeapon(type);
        if (!changed) return;

        // 바꾼 직후 칸을 살짝 튕긴다
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.5f).SetUpdate(true);
    }

    // 무기가 바뀌었을 때 글자·색·칸 배치
    void ApplyWeapon(Weapontype type)
    {
        shownType = type;
        NowAdvantage adv = player != null && player.EquippedWeapon != null ? player.EquippedWeapon.advantage : NowAdvantage.None;
        Color accent = AdvColor(adv);

        int idx = (int)type;
        if (nameText != null) nameText.text = idx >= 0 && idx < weaponNames.Length ? weaponNames[idx] : type.ToString();
        if (chipText != null) { chipText.text = AdvName(adv); chipText.color = accent; }
        if (chipBackground != null)
        {
            Color c = accent; c.a = chipBackground.color.a; chipBackground.color = c;
            chipBackground.gameObject.SetActive(adv != NowAdvantage.None);
        }

        shownAttack = int.MinValue;   // 다음 Update 에서 공격력 글자를 다시 씀
        UpdateAttack();
        Layout();
    }

    // 글자 길이에 맞춰 왼쪽부터 이어 붙임 (모든 칸은 기준점이 왼쪽 가운데)
    void Layout()
    {
        float x = 0f;
        if (nameText != null)
        {
            nameText.ForceMeshUpdate();
            float w = nameText.preferredWidth;
            Place(nameText.rectTransform, x, w);
            x += w + spacing;
        }
        if (chipBackground != null && chipBackground.gameObject.activeSelf && chipText != null)
        {
            chipText.ForceMeshUpdate();
            float w = chipText.preferredWidth + chipPadding * 2f;
            Place((RectTransform)chipBackground.transform, x, w);
            x += w + spacing;
        }
        if (attackText != null)
            attackText.rectTransform.anchoredPosition = new Vector2(x, attackText.rectTransform.anchoredPosition.y);
    }

    static void Place(RectTransform r, float x, float width)
    {
        r.anchoredPosition = new Vector2(x, r.anchoredPosition.y);
        r.sizeDelta = new Vector2(width, r.sizeDelta.y);
    }

    void Update()
    {
        UpdateAttack();
    }

    // 공격력은 아이템·버프로 바뀔 수 있으므로 매 프레임 확인하고, 바뀌었을 때만 글자를 고친다
    void UpdateAttack()
    {
        if (player == null || attackText == null) return;
        int atk = player.CurrentAttackPower;
        if (atk == shownAttack) return;
        shownAttack = atk;
        attackText.text = string.Format(attackFormat, atk);
    }

    string AdvName(NowAdvantage a)
    {
        switch (a)
        {
            case NowAdvantage.Power: return powerName;
            case NowAdvantage.Speed: return speedName;
            case NowAdvantage.Defense: return defenseName;
            default: return "";
        }
    }

    Color AdvColor(NowAdvantage a)
    {
        switch (a)
        {
            case NowAdvantage.Power: return powerColor;
            case NowAdvantage.Speed: return speedColor;
            case NowAdvantage.Defense: return defenseColor;
            default: return noneColor;
        }
    }
}
