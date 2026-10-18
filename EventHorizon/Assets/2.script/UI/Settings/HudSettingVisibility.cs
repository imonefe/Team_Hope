using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 설정 창의 켬/끔에 따라 HUD 묶음을 보이거나 숨긴다.
///
/// - 숨길 묶음(예: 무기 정보 칸, 조작키 안내 줄)의 맨 위 오브젝트에 붙이고, 어느 설정을 따를지 고른다.
/// - 오브젝트를 끄지 않고 CanvasGroup 의 투명도만 0 으로 바꾸므로, 그 안의 스크립트(WeaponStripUI 등)는 계속 돈다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class HudSettingVisibility : MonoBehaviour
{
    public enum Target { WeaponInfo, KeyHints }

    [Tooltip("WeaponInfo = 설정의 '무기 정보 칸', KeyHints = 설정의 '조작키 안내 줄'")]
    public Target setting = Target.WeaponInfo;

    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;   // HUD 는 클릭을 받을 일이 없음
    }

    void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    void Apply()
    {
        bool visible = setting == Target.WeaponInfo ? GameSettings.ShowWeaponInfo : GameSettings.ShowKeyHints;
        if (group != null) group.alpha = visible ? 1f : 0f;
    }
}
