using UnityEngine;
using TMPro;

/// <summary>
/// [2026-10-17 추가] 로비 무기 진열대. 무기를 미리 들어 보고 마음에 드는 걸로 출발할 수 있게 한다.
///
/// - weaponPrefab(무기 줍기 프리팹: Sword / Glove / Hammer)을 받침 위에 떠 있는 그림으로 보여 준다.
///   진열용 복사본은 충돌체를 모두 꺼서, 닿기만 해도 바뀌는 일반 무기 줍기와 달리 지나가도 안 바뀐다.
/// - 가까이 가서 F키 → 그 무기로 교체 (일반 무기 줍기와 같은 Weapon.ApplyTo 사용 — 스탯·공격 방식 모두 교체)
/// - 지금 들고 있는 무기의 진열대는 비어 보이고 "장착 중"으로 표시된다. 다른 무기로 바꾸면 다시 나타남.
/// - 로비에서 마지막으로 든 무기 그대로 런을 시작한다.
///
/// 프리팹 구성: 이 스크립트 + 줍기 범위(Is Trigger 켠 BoxCollider2D, 레이어 Pickup)
/// </summary>
public class WeaponStand : ProximityInteractable
{
    [Header("진열할 무기")]
    [Tooltip("무기 줍기 프리팹 (Weapon 컴포넌트가 붙은 것: 1.Prefab/item/EtcOBJ/Sword 등)")]
    public GameObject weaponPrefab;
    [Tooltip("진열대 위에 보일 무기 이름")]
    public string displayName = "무기";

    [Header("보이는 위치")]
    [Tooltip("진열대 기준 무기 그림 위치")]
    public Vector3 displayOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("무기 그림이 위아래로 흔들리는 폭")]
    public float bobHeight = 0.15f;
    [Tooltip("1초에 흔들리는 횟수")]
    public float bobSpeed = 0.8f;

    [Header("이름표")]
    public TMP_FontAsset labelFont;
    [Tooltip("진열대 기준 이름표 위치")]
    public Vector3 labelOffset = new Vector3(0f, 3.4f, 0f);
    public float labelSize = 6f;
    public Color labelColor = new Color(0.93f, 0.8f, 0.52f, 1f);
    public Color hintColor = new Color(0.94f, 0.9f, 0.81f, 1f);
    public int labelSortingOrder = 5000;

    Weapon displayWeapon;
    WeaponData data;
    GameObject display;
    TextMeshPro label;
    bool lastEquipped;
    bool labelDirty = true;

    void Start()
    {
        if (weaponPrefab == null) return;

        // 진열용 복사본 — 충돌체·물리를 꺼서 그림으로만 쓴다
        display = Instantiate(weaponPrefab, transform.position + displayOffset, Quaternion.identity, transform);
        display.name = weaponPrefab.name + "_Display";
        foreach (var col in display.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (var rb in display.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

        displayWeapon = display.GetComponent<Weapon>();
        data = displayWeapon != null ? displayWeapon.data : null;

        CreateLabel();
    }

    void CreateLabel()
    {
        var go = new GameObject("Label");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = labelOffset;

        label = go.AddComponent<TextMeshPro>();
        if (labelFont != null) label.font = labelFont;
        label.fontSize = labelSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.rectTransform.sizeDelta = new Vector2(8f, 3f);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = labelSortingOrder;
        labelDirty = true;
    }

    bool IsEquipped()
    {
        PlayerController pc = PlayerController.Instance;
        return pc != null && data != null && pc.EquippedWeapon == data;
    }

    protected override bool CanInteract() => displayWeapon != null && !IsEquipped();

    protected override void Interact()
    {
        if (playerObj == null || displayWeapon == null) return;
        PlayerController pc = playerObj.GetComponent<PlayerController>();
        Stats stats = playerObj.GetComponent<Stats>();
        if (pc == null || stats == null) return;

        displayWeapon.ApplyTo(stats, pc);   // 일반 무기 줍기와 같은 방식으로 교체
        labelDirty = true;
    }

    protected override void OnPlayerEnter(Collider2D other) { labelDirty = true; }
    protected override void OnPlayerExit(Collider2D other) { labelDirty = true; }

    protected override void Update()
    {
        base.Update();
        if (display == null) return;

        bool equipped = IsEquipped();
        if (equipped != lastEquipped) { lastEquipped = equipped; labelDirty = true; }

        // 들고 있는 무기는 진열대에서 비워 보이게
        if (display.activeSelf == equipped) display.SetActive(!equipped);
        if (!equipped)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
            display.transform.position = transform.position + displayOffset + new Vector3(0f, bob, 0f);
        }

        if (labelDirty && label != null)
        {
            labelDirty = false;
            string hint = equipped ? "장착 중" : (playerNearby ? "[F] 장착" : "");
            label.text = "<color=#" + ColorUtility.ToHtmlStringRGBA(labelColor) + ">" + displayName + "</color>"
                       + (hint.Length > 0 ? "\n<size=70%><color=#" + ColorUtility.ToHtmlStringRGBA(hintColor) + ">" + hint + "</color></size>" : "");
        }
    }
}
