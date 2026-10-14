using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모든 UI 버튼 클릭 효과음을 한 곳에서 처리하는 자동 생성 오브젝트.
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 Resources/UISoundSettings 를 읽어서 스스로 생성되고,
/// 씬이 바뀌어도 유지된다 (인트로/메인메뉴/게임 씬 전부 동작, AudioManager 가 없는 씬에서도 동작).
///
/// 동작: 마우스 왼쪽 버튼을 누른 UI 와 뗀 UI 가 같은 버튼이면(= 실제 클릭) 소리를 재생.
///  - 비활성(interactable 꺼짐) 버튼은 소리 안 남
///  - 일시정지 메뉴(Time.timeScale = 0) 중에도 정상 재생
///  - 볼륨 = UISoundSettings.volume × AudioManager.sfxVolume(있을 때)
///
/// 코드에서 직접 클릭 소리를 내고 싶으면: UIClickSound.PlayClick();
/// </summary>
[DefaultExecutionOrder(-1000)] // EventSystem 보다 먼저 Update — 클릭으로 버튼이 꺼지기 전에 판정
public class UIClickSound : MonoBehaviour
{
    const string SettingsResourcePath = "UISoundSettings";

    static UIClickSound instance;

    UISoundSettings settings;
    AudioSource source;

    GameObject pressedTarget;
    UIButtonSound pressedCustom; // 누르는 순간의 버튼별 설정 (클릭으로 버튼이 파괴돼도 쓰도록 미리 저장)
    bool hasPressedTarget;
    readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        UISoundSettings loaded = Resources.Load<UISoundSettings>(SettingsResourcePath);
        if (loaded == null)
        {
            Debug.LogWarning("[UIClickSound] Resources/UISoundSettings 가 없어서 UI 클릭 소리를 끕니다.");
            return;
        }

        GameObject go = new GameObject("[UIClickSound]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UIClickSound>();
        instance.settings = loaded;

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;          // 2D
        src.ignoreListenerPause = true; // AudioListener.pause 중에도 재생
        src.outputAudioMixerGroup = loaded.outputMixerGroup;
        instance.source = src;
    }

    void Update()
    {
        if (settings == null || !settings.playClickSound) { hasPressedTarget = false; return; }

        if (Input.GetMouseButtonDown(0))
        {
            pressedTarget = FindClickTarget();
            hasPressedTarget = pressedTarget != null;
            pressedCustom = hasPressedTarget ? pressedTarget.GetComponent<UIButtonSound>() : null;
        }

        if (Input.GetMouseButtonUp(0) && hasPressedTarget)
        {
            hasPressedTarget = false;

            // 누른 버튼이 그 사이 꺼지거나 파괴됐다면(=클릭으로 메뉴가 닫힘/씬 전환) 클릭된 것으로 본다
            if (pressedTarget == null || !pressedTarget.activeInHierarchy || FindClickTarget() == pressedTarget)
                PlayFor(pressedCustom);
        }
    }

    // 마우스 아래에서 '클릭을 받는' UI 오브젝트를 찾는다 (없거나 대상이 아니면 null)
    GameObject FindClickTarget()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return null;

        PointerEventData ped = new PointerEventData(es) { position = Input.mousePosition };
        hits.Clear();
        es.RaycastAll(ped, hits);
        if (hits.Count == 0) return null;

        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        if (handler == null) return null;

        Selectable sel = handler.GetComponent<Selectable>();
        if (sel != null) return sel.IsInteractable() ? handler : null;
        return settings.includeCustomClickables ? handler : null;
    }

    void PlayFor(UIButtonSound custom)
    {
        if (custom != null && custom.mute) return;

        AudioClip clip = custom != null && custom.clickClip != null ? custom.clickClip : settings.clickClip;
        float scale = custom != null ? custom.volumeScale : 1f;
        PlayClip(clip, scale);
    }

    void PlayClip(AudioClip clip, float volumeScale)
    {
        if (clip == null || source == null) return;

        float sfx = AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 1f;
        source.pitch = 1f + Random.Range(-settings.pitchVariation, settings.pitchVariation);
        source.PlayOneShot(clip, settings.volume * sfx * volumeScale);
    }

    /// <summary>코드에서 기본 클릭 소리를 직접 재생 (예: 키보드로 메뉴 선택했을 때)</summary>
    public static void PlayClick()
    {
        if (instance == null || instance.settings == null || !instance.settings.playClickSound) return;
        instance.PlayClip(instance.settings.clickClip, 1f);
    }
}
