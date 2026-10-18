using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// [2026-10-18 추가] 설정 창.
///
/// 여는 방법: 어디서든 SettingsWindow.Open() 한 줄.
///   - 프리팹 "SettingsCanvas" 를 Resources 폴더에서 찾아 처음 한 번 만들어 두고, 그다음부터는 켜고 끄기만 한다.
///     (프리팹 위치: Assets/1.Prefab/UI/Canvas/설정/Resources/SettingsCanvas.prefab)
///   - 지금은 ESC 메뉴의 '설정' 버튼(GameMenuController.OpenSettings)과 인트로의 '설정' 버튼이 부른다.
/// 닫는 방법: ESC 또는 '닫기' 버튼.
///   - 닫힌 바로 그 프레임에 같은 ESC 로 뒤의 ESC 메뉴까지 닫히지 않도록 BlocksEscape 를 제공한다.
///
/// 값은 GameSettings 에 바로 적용·저장된다 (따로 '저장' 버튼 없음).
/// 창 안의 줄(슬라이더·스위치·고르기)은 각각 SettingSlider / SettingToggle / SettingStepper 이고,
/// 이 스크립트는 그것들을 GameSettings 와 연결만 한다.
/// </summary>
public class SettingsWindow : MonoBehaviour
{
    public const string ResourcePath = "SettingsCanvas";

    [Header("소리")]
    public SettingSlider masterVolume;
    public SettingSlider bgmVolume;
    public SettingSlider sfxVolume;

    [Header("화면")]
    public SettingToggle fullscreen;
    public SettingStepper resolution;
    public SettingSlider screenShake;

    [Header("표시")]
    public SettingToggle weaponInfo;
    public SettingToggle keyHints;
    public SettingToggle damageNumbers;

    [Header("버튼")]
    public Button resetButton;
    public Button closeButton;

    [Header("열기 연출")]
    [Tooltip("창 전체 (투명도 조절용). 비우면 자동으로 찾거나 추가")]
    public CanvasGroup group;
    [Tooltip("가운데 창 (열릴 때 살짝 커지며 나타남)")]
    public RectTransform panel;
    public float fadeTime = 0.15f;

    static SettingsWindow instance;
    static int closedFrame = -1;
    List<Vector2Int> resolutions = new List<Vector2Int>();

    /// <summary>설정 창이 열려 있는지</summary>
    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    /// <summary>
    /// ESC 를 다른 창(ESC 메뉴 등)이 처리하면 안 되는 상태인지.
    /// 열려 있거나, 이번 프레임에 ESC 로 막 닫혔으면 true.
    /// </summary>
    public static bool BlocksEscape => IsOpen || Time.frameCount == closedFrame;

    /// <summary>설정 창 열기 (없으면 Resources 에서 만들어 둠)</summary>
    public static void Open()
    {
        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[SettingsWindow] Resources 폴더에서 '" + ResourcePath + "' 프리팹을 찾지 못했습니다.");
                return;
            }
            GameObject go = Instantiate(prefab);
            go.name = prefab.name;
            instance = go.GetComponent<SettingsWindow>();
            if (instance == null)
            {
                Debug.LogWarning("[SettingsWindow] 프리팹에 SettingsWindow 가 없습니다.");
                Destroy(go);
                return;
            }
        }
        instance.Show();
    }

    /// <summary>설정 창 닫기</summary>
    public static void CloseWindow()
    {
        if (instance != null) instance.Close();
    }

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        Bind(masterVolume, GameSettings.SetMasterVolume);
        Bind(bgmVolume, GameSettings.SetBgmVolume);
        Bind(sfxVolume, GameSettings.SetSfxVolume);
        Bind(screenShake, GameSettings.SetShakeScale);
        Bind(fullscreen, GameSettings.SetFullscreen);
        Bind(weaponInfo, GameSettings.SetShowWeaponInfo);
        Bind(keyHints, GameSettings.SetShowKeyHints);
        Bind(damageNumbers, GameSettings.SetShowDamageNumbers);

        if (resolution != null)
            resolution.OnChanged = i =>
            {
                if (i >= 0 && i < resolutions.Count) GameSettings.SetResolution(resolutions[i].x, resolutions[i].y);
            };

        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // 씬에 직접 놓아 둔 경우에도 Open() 이 이 창을 쓰게 등록
        if (instance == null) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    static void Bind(SettingSlider s, System.Action<float> set) { if (s != null) s.OnChanged = set; }
    static void Bind(SettingToggle t, System.Action<bool> set) { if (t != null) t.OnChanged = set; }

    void Show()
    {
        gameObject.SetActive(true);
        RefreshAll();

        // 뒤의 메뉴 버튼이 키보드 선택 상태로 남아 같이 눌리지 않게
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // 게임이 멈춘 상태(ESC 메뉴)에서도 움직이도록 SetUpdate(true)
        group.DOKill();
        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.DOFade(1f, fadeTime).SetUpdate(true);
        if (panel != null)
        {
            panel.DOKill();
            panel.localScale = Vector3.one * 0.96f;
            panel.DOScale(1f, fadeTime).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        closedFrame = Time.frameCount;
        group.DOKill();
        if (panel != null) panel.DOKill();
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void ResetAll()
    {
        GameSettings.ResetToDefaults();
        RefreshAll();
    }

    // 지금 저장된 값으로 창 안의 줄을 모두 다시 맞춤 (알림 없이)
    void RefreshAll()
    {
        if (masterVolume != null) masterVolume.SetValue(GameSettings.MasterVolume, false);
        if (bgmVolume != null) bgmVolume.SetValue(GameSettings.BgmVolume, false);
        if (sfxVolume != null) sfxVolume.SetValue(GameSettings.SfxVolume, false);
        if (screenShake != null) screenShake.SetValue(GameSettings.ShakeScale, false);
        if (fullscreen != null) fullscreen.SetValue(GameSettings.Fullscreen, false);
        if (weaponInfo != null) weaponInfo.SetValue(GameSettings.ShowWeaponInfo, false);
        if (keyHints != null) keyHints.SetValue(GameSettings.ShowKeyHints, false);
        if (damageNumbers != null) damageNumbers.SetValue(GameSettings.ShowDamageNumbers, false);

        if (resolution != null)
        {
            resolutions = GameSettings.GetResolutions();
            List<string> labels = new List<string>();
            int current = 0;
            for (int i = 0; i < resolutions.Count; i++)
            {
                labels.Add(resolutions[i].x + " × " + resolutions[i].y);
                if (resolutions[i].x == GameSettings.ResolutionWidth && resolutions[i].y == GameSettings.ResolutionHeight)
                    current = i;
            }
            resolution.SetOptions(labels, current);
        }
    }
}
