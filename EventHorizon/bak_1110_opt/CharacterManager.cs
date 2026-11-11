using UnityEngine;
using System;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance;

    public GameObject characterA;
    public GameObject characterB;

    public GameObject CurrentCharacter { get; private set; }

    public event Action<GameObject> OnCharacterChanged;

    void Awake()
    {
        Instance = this;
        SetCharacter(characterA);
    }

    void Update()
    {
        // [2026-10-10] 창이 떠 있거나 대화 중이면 캐릭터 교체 입력을 받지 않음
        if (Time.timeScale == 0f || NPC.AnyTalking) return;
        if (Input.GetKeyDown(KeyCode.Tab))
            SwitchCharacter();
    }

    public void SwitchCharacter()
    {
        if (CurrentCharacter == characterA) SetCharacter(characterB);
        else SetCharacter(characterA);
    }

    void SetCharacter(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("CharacterManager: 대상 캐릭터가 null이에요.");
            return;
        }

        if (CurrentCharacter != null)
            CurrentCharacter.SetActive(false);

        CurrentCharacter = target;
        CurrentCharacter.SetActive(true);

        OnCharacterChanged?.Invoke(CurrentCharacter);
    }

    /// <summary>
    /// 캐릭터 사망 시 Health.cs 에서 호출
    /// 살아있는 다른 캐릭터로 전환 / 둘 다 죽으면 게임오버
    /// </summary>
    public void NotifyCharacterDead(GameObject deadCharacter)
    {
        // 살아있는 다른 캐릭터 찾기
        GameObject other = deadCharacter == characterA ? characterB : characterA;

        if (other != null && other.activeSelf)
        {
            Debug.Log($"[CharacterManager] {deadCharacter.name} 사망 → {other.name} 로 전환");
            CurrentCharacter = null; // 잠깐 null 로 해서 SetCharacter 의 비활성화 방지
            SetCharacter(other);
        }
        else
        {
            // 둘 다 사망 → 게임오버
            Debug.Log("[CharacterManager] 두 캐릭터 모두 사망 → 게임오버");
            GameOver();
        }
    }


    /// <summary>현재 캐릭터의 CharacterSkills (없으면 null). 스킬 UI 교차용.</summary>
    public CharacterSkills CurrentSkills
    {
        get { return CurrentCharacter != null ? CurrentCharacter.GetComponent<CharacterSkills>() : null; }
    }

    /// <summary>현재 캐릭터의 Stats (없으면 null). 스탯 교차용.</summary>
    public Stats CurrentStats
    {
        get { return CurrentCharacter != null ? CurrentCharacter.GetComponent<Stats>() : null; }
    }

    /// <summary>
    /// [2026-10-15 추가] 남은 캐릭터와 상관없이 바로 게임오버 화면을 띄운다.
    /// 보스 컷씬(CutsceneSequencer의 KillPlayer 스텝)처럼 연출로 플레이어를 처리할 때 사용.
    /// </summary>
    public void ForceGameOver()
    {
        GameOver();
    }

    void GameOver()
    {
        Debug.Log("[CharacterManager] 게임오버!");

        // 게임오버 UI가 씬에 준비돼 있으면 화면을 띄우고 결과를 보여준다 (다시하기는 그 화면의 버튼이 처리).
        // 혹시 UI가 아직 안 붙어있는 상황을 대비해, 없으면 예전처럼 즉시 재시작한다.
        if (GameOverUI.Instance != null)
            GameOverUI.Instance.Show(CurrentStats);
        else
            RestartRun();
    }

    /// <summary>
    /// 런 재시작 — 게임오버 화면의 "다시하기" 버튼에서 호출.
    /// 각 매니저의 런 상태를 초기화한 뒤 씬을 다시 불러온다.
    /// </summary>
    public void RestartRun()
    {
        // 실제 초기화는 RunManager 한 곳에서 담당한다 (판 데이터 + 맵 진행도 + 씬 재로드)
        RunManager.RestartRun();
    }
}
