using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>[2026-11-21 추가] 스테이지 한 칸의 종류</summary>
public enum StageKind { Tutorial = 0, Lobby = 1, Combat = 2, MidBoss = 3, Shop = 4, Boss = 5 }

/// <summary>
/// [2026-11-21 추가] 튜토리얼부터 마지막 스테이지까지의 순서표 (3. Data/MapData/StagePlan.asset).
///
/// - 일시정지 화면 '스테이지 설정'에 이 순서 그대로 나열되고, 켜 둔 칸만 차례로 밟는다.
/// - 켜고 끈 값은 PlayerPrefs 에 저장되고, '새 게임'을 시작할 때 한 번 읽어 이번 판 순서를 정한다
///   (게임 도중에 바꿔도 지금 판은 그대로, 다음 새 게임부터 적용).
/// - 튜토리얼 · 로비는 방 프리팹(room)으로 바로 들어가고, 나머지는 노드 지도에 한 줄로 이어진 노드가 된다.
/// - 켜 둔 칸 중 마지막 칸에서 적이 모두 없어지면(또는 마지막 칸 방이 아직 없으면) 게임 클리어.
/// </summary>
[CreateAssetMenu(fileName = "StagePlan", menuName = "Game/Map/StagePlan")]
public class StagePlan : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("저장 이름 (켜고 끈 값을 이 이름으로 저장 — 한 번 정하면 바꾸지 말 것)")]
        public string id;
        [Tooltip("묶음 이름 (튜토리얼 · 1S · 2S). 같은 묶음의 노드가 한 노드 지도에 나온다")]
        public string group;
        [Tooltip("설정 화면에 보이는 이름")]
        public string label;
        public StageKind kind;
        [Tooltip("튜토리얼 · 로비: 들어갈 방 프리팹")]
        public GameObject room;
        [Tooltip("노드(전투 · 중간 보스 · 상점 · 보스): 방 데이터 (비어 있거나 방이 없으면 그 칸은 '아직 없는 방')")]
        public MapRoomData roomData;
        [Tooltip("처음 켜져 있는지")]
        public bool defaultOn = true;

        public bool IsNode => kind != StageKind.Tutorial && kind != StageKind.Lobby;
    }

    public List<Entry> entries = new List<Entry>();
}

/// <summary>[2026-11-21 추가] 스테이지 켜고 끄기 저장 (PlayerPrefs)</summary>
public static class StageSettings
{
    const string Prefix = "StageOn.";

    public static bool IsOn(StagePlan.Entry e) => e != null && PlayerPrefs.GetInt(Prefix + e.id, e.defaultOn ? 1 : 0) == 1;

    public static void Set(StagePlan.Entry e, bool on)
    {
        if (e == null) return;
        PlayerPrefs.SetInt(Prefix + e.id, on ? 1 : 0);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// [2026-11-21 추가] 이번 판에 밟을 순서 (새 게임을 시작할 때 설정을 찍어 둔 것).
/// MapManager 가 진행을 맡고, 여기는 목록과 '지금 몇 번째인지'만 들고 있다.
/// </summary>
public static class StageFlow
{
    static readonly List<StagePlan.Entry> run = new List<StagePlan.Entry>();

    /// <summary>지금 들어가 있는 칸 (-1 = 아직 시작 전)</summary>
    public static int Current { get; private set; } = -1;
    public static bool Active { get; private set; }
    public static bool Cleared { get; set; }
    public static IReadOnlyList<StagePlan.Entry> Run => run;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { run.Clear(); Current = -1; Active = false; Cleared = false; }

    /// <summary>설정을 읽어 이번 판 순서를 만든다. 켜 둔 칸이 하나도 없으면 false (예전 방식으로 진행)</summary>
    public static bool Begin(StagePlan plan)
    {
        run.Clear(); Current = -1; Cleared = false;
        if (plan != null)
            foreach (var e in plan.entries)
                if (e != null && StageSettings.IsOn(e)) run.Add(e);
        Active = run.Count > 0;
        return Active;
    }

    public static StagePlan.Entry At(int i) => i >= 0 && i < run.Count ? run[i] : null;
    public static StagePlan.Entry CurrentEntry => At(Current);
    public static StagePlan.Entry PeekNext() => At(Current + 1);
    public static bool IsLast(int i) => i == run.Count - 1;
    public static bool CurrentIsLast => Active && Current >= 0 && IsLast(Current);
    public static void SetCurrent(int i) => Current = i;
}
