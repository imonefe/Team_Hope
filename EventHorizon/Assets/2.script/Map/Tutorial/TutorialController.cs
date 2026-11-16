using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-16 추가] 튜토리얼 방 (기획서: 한 방 · 화면 글자 · 해 봐야 다음 구간이 열림 · 건너뛰기 없음).
///
/// 구간마다 통과 조건이 있고, 조건을 채우면 그 구간 끝의 막이(gate)가 꺼지고 다음 구간 안내가 뜬다.
/// 안내 글자의 {Jump} {Dash} 같은 표시는 설정된 키 이름으로 바뀐다 (키 설정을 바꿔도 맞게 나옴).
/// 오브젝트(막이·영역·허수아비·상자·글자)는 에디터 메뉴 "EventHorizon/튜토리얼 방 만들기"가 만들어 둔다.
/// </summary>
public class TutorialController : MonoBehaviour
{
    public enum Condition
    {
        Jump,          // 점프 한 번
        DoubleJump,    // 공중 점프 (점프 2회)
        Dash,          // 대시
        DoubleDash,    // 대시 2회 연속 (쿨타임 안에)
        Reach,         // reachZone 에 닿기
        DummyDamage,   // 허수아비에 피해 일정량
        WeaponSwap,    // 무기 교체 키
        WeaponSkill,   // 0번(무기 스킬) 칸 스킬 사용
        AnySkill,      // 아무 스킬 사용
        Pause,         // 퍼즈 사용
        BoxAndInventory, // 상자 열기 + 인벤토리 열기
    }

    [System.Serializable]
    public class Step
    {
        public string title;
        [TextArea(2, 3)] public string guide;
        public Condition condition;
        [Tooltip("이 구간을 통과하면 꺼지는 막이")]
        public GameObject gate;
        [Tooltip("구덩이에 빠졌을 때 되돌아오는 위치")]
        public Transform checkpoint;
        [Tooltip("Reach 조건용 영역")]
        public TutorialZone reachZone;
        [Tooltip("DummyDamage 조건용 허수아비")]
        public Stats dummy;
        public int damageNeeded = 50;
        [Tooltip("BoxAndInventory 조건용 상자 (구간이 시작될 때 열 수 있게 됨)")]
        public Box box;
        [Tooltip("구간이 시작될 때 퍼즈 게이지를 가득 채움 (게이지 모으기 생략)")]
        public bool fillPauseGauge;
    }

    public List<Step> steps = new List<Step>();

    [Header("화면 글자")]
    public TMP_Text titleText;
    public TMP_Text guideText;
    public string doneTitle = "튜토리얼 완료";
    [TextArea] public string doneGuide = "오른쪽 끝의 문으로 나가세요";

    int index = -1;
    int dummyDamage;
    bool boxOpened, inventoryAfterBox;
    Stats watchedDummy;
    PlayerController player;

    void Start()
    {
        KeyBindings.Changed += RefreshText;
        Begin(0);
    }

    void OnDestroy()
    {
        KeyBindings.Changed -= RefreshText;
        if (watchedDummy != null) watchedDummy.OnHpChanged -= OnDummyHp;
    }

    public Step Current => index >= 0 && index < steps.Count ? steps[index] : null;

    void Begin(int i)
    {
        index = i;
        dummyDamage = 0;
        boxOpened = inventoryAfterBox = false;
        if (watchedDummy != null) { watchedDummy.OnHpChanged -= OnDummyHp; watchedDummy = null; }

        Step s = Current;
        if (s != null)
        {
            if (s.reachZone != null) s.reachZone.reached = false;
            if (s.dummy != null) { watchedDummy = s.dummy; watchedDummy.OnHpChanged += OnDummyHp; }
            if (s.box != null) s.box.SetActive(true);
            if (s.fillPauseGauge && PauseManager.Instance != null) PauseManager.Instance.AddGauge(100f);
        }
        RefreshText();
    }

    void RefreshText()
    {
        Step s = Current;
        if (titleText != null) titleText.text = s != null ? $"{index + 1} / {steps.Count}  {s.title}" : doneTitle;
        if (guideText != null) guideText.text = Keys(s != null ? s.guide : doneGuide);
    }

    /// <summary>{Jump} 처럼 쓴 동작 이름을 지금 설정된 키 이름으로 바꾼다</summary>
    static string Keys(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (KeyAction a in System.Enum.GetValues(typeof(KeyAction)))
            text = text.Replace("{" + a + "}", "<b>" + KeyBindings.KeyName(a) + "</b>");
        return text;
    }

    void OnDummyHp(int current, int max) { dummyDamage += Mathf.Max(0, max - current); }

    void Update()
    {
        Step s = Current;
        if (s == null) return;
        if (player == null || !player.isActiveAndEnabled) player = FindPlayer();
        if (player == null) return;

        if (Done(s))
        {
            if (s.gate != null) s.gate.SetActive(false);
            FloatingTextSpawner.SpawnText(player.transform.position + Vector3.up * 2f, "통과!", Color.yellow);
            Begin(index + 1);
        }
    }

    bool Done(Step s)
    {
        switch (s.condition)
        {
            case Condition.Jump:        return player.JumpCount >= 1;
            case Condition.DoubleJump:  return player.JumpCount >= 2;
            case Condition.Dash:        return player.IsDashing;
            case Condition.DoubleDash:  return player.DeshCount >= 2;
            case Condition.Reach:       return s.reachZone != null && s.reachZone.reached;
            case Condition.DummyDamage: return dummyDamage >= s.damageNeeded;
            case Condition.WeaponSwap:  return KeyBindings.Down(KeyAction.WeaponSwap);
            case Condition.WeaponSkill: return KeyBindings.Down(KeyAction.Skill1) && HasSkill(0);
            case Condition.AnySkill:
                return (KeyBindings.Down(KeyAction.Skill1) && HasSkill(0))
                    || (KeyBindings.Down(KeyAction.Skill2) && HasSkill(1))
                    || (KeyBindings.Down(KeyAction.Skill3) && HasSkill(2));
            case Condition.Pause:       return PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            case Condition.BoxAndInventory:
                if (s.box != null && s.box.IsOpened) boxOpened = true;
                if (boxOpened && KeyBindings.Down(KeyAction.Inventory)) inventoryAfterBox = true;
                return inventoryAfterBox;
        }
        return false;
    }

    bool HasSkill(int slot)
    {
        CharacterSkills cs = player.GetComponent<CharacterSkills>();
        return cs != null && cs.GetSkill(slot) != null;
    }

    static PlayerController FindPlayer()
    {
        foreach (PlayerController pc in FindObjectsOfType<PlayerController>())
            if (pc.isActiveAndEnabled && pc.CompareTag("Player")) return pc;
        return null;
    }

    /// <summary>구덩이에 빠진 플레이어를 지금 구간 시작 위치로 (피해 없음)</summary>
    public void Respawn(GameObject who)
    {
        Step s = Current;
        Transform cp = s != null && s.checkpoint != null ? s.checkpoint : (steps.Count > 0 ? steps[steps.Count - 1].checkpoint : null);
        if (cp == null || who == null) return;
        who.transform.position = cp.position;
        Rigidbody2D rb = who.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
    }
}
