using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 UI 버튼 클릭 효과음을 한 곳에서 처리하는 자동 생성 오브젝트.
/// 씬에 따로 배치할 필요 없음 — 게임이 시작되면 Resources/UISoundSettings 를 읽어서 스스로 생성되고,
/// 씬이 바뀌어도 유지된다 (인트로/메인메뉴/게임 씬 전부 동작, AudioManager 가 없는 씬에서도 동작).
///
/// 동작: 마우스 왼쪽 버튼을 누른 UI 와 뗀 UI 가 같은 버튼이면(= 실제 클릭) 소리를 재생.
///  - 버튼 종류(기본/확인/취소/노드 선택)에 따라 다른 소리 — 판별 규칙은 UISoundSettings 참고
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
    // 누르는 순간 미리 정해둔 소리/볼륨 (클릭으로 버튼이 꺼지거나 파괴돼도 그대로 쓰기 위해)
    AudioClip pressedClip;
    float pressedVolumeScale = 1f;
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
            hasPressedTarget = pressedTarget != null && ResolveSound(pressedTarget, out pressedClip, out pressedVolumeScale);
        }

        if (Input.GetMouseButtonUp(0) && hasPressedTarget)
        {
            hasPressedTarget = false;

            // 누른 버튼이 그 사이 꺼지거나 파괴됐다면(=클릭으로 메뉴가 닫힘/씬 전환) 클릭된 것으로 본다
            if (pressedTarget == null || !pressedTarget.activeInHierarchy || FindClickTarget() == pressedTarget)
                PlayClip(pressedClip, pressedVolumeScale);
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

    // 버튼에 맞는 소리를 정한다. false = 이 버튼은 소리 없음(음소거)
    bool ResolveSound(GameObject target, out AudioClip clip, out float volumeScale)
    {
        clip = null;
        volumeScale = 1f;

        UIButtonSound custom = target.GetComponent<UIButtonSound>();
        if (custom != null)
        {
            if (custom.mute) return false;
            volumeScale = custom.volumeScale;
            if (custom.clickClip != null) { clip = custom.clickClip; return true; }
        }

        UISoundType type = custom != null && custom.type != UISoundType.Auto ? custom.type : DetectType(target);
        clip = settings.GetClip(type);
        return clip != null;
    }

    // 자동 판별: 맵 노드 → OnClick 연결 함수 이름 → 버튼 글자 → 오브젝트 이름
    UISoundType DetectType(GameObject target)
    {
        if (target.GetComponent<MapButton>() != null) return UISoundType.MapNode;

        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                UISoundType t = MatchKeywords(button.onClick.GetPersistentMethodName(i));
                if (t != UISoundType.Default) return t;
            }
        }

        TMP_Text tmp = target.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            UISoundType t = MatchKeywords(tmp.text);
            if (t != UISoundType.Default) return t;
        }
        Text legacy = target.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            UISoundType t = MatchKeywords(legacy.text);
            if (t != UISoundType.Default) return t;
        }

        return MatchKeywords(target.name);
    }

    UISoundType MatchKeywords(string s)
    {
        if (string.IsNullOrEmpty(s)) return UISoundType.Default;
        s = s.ToLowerInvariant();
        // 취소 단어를 먼저 검사 — "QuitGame" 처럼 둘 다 걸릴 일은 거의 없지만, 닫기/종료가 확인으로 잘못 잡히는 쪽이 더 어색함
        if (ContainsAny(s, settings.cancelKeywords)) return UISoundType.Cancel;
        if (ContainsAny(s, settings.confirmKeywords)) return UISoundType.Confirm;
        return UISoundType.Default;
    }

    static bool ContainsAny(string s, string[] words)
    {
        if (words == null) return false;
        foreach (string w in words)
        {
            if (!string.IsNullOrEmpty(w) && s.Contains(w.ToLowerInvariant())) return true;
        }
        return false;
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
