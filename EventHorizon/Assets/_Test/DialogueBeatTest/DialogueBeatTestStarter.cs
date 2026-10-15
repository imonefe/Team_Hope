using UnityEngine;

/// <summary>
/// DialogueBeat 테스트 씬 전용. 씬이 시작되면 자동으로 대화를 시작하고,
/// 대화가 끝난 뒤 Enter 를 누르면 처음부터 다시 시작한다.
/// (실제 게임에서는 npcTrigger 가 이 역할을 하므로 게임 씬에는 넣지 않는다)
/// </summary>
public class DialogueBeatTestStarter : MonoBehaviour
{
    public NPC npc;
    [Tooltip("씬 시작 후 몇 초 뒤 자동으로 대화를 시작할지")]
    public float autoStartDelay = 0.5f;

    void Start()
    {
        Invoke(nameof(Begin), autoStartDelay);
    }

    void Update()
    {
        if (npc != null && !npc.IsTalking && Input.GetKeyDown(KeyCode.Return))
            Begin();
    }

    void Begin()
    {
        if (npc != null && !npc.IsTalking)
            npc.SetCondition(null, null);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(16, 12, 900, 30), "F / 마우스 클릭: 다음 대사      Enter: (대화가 끝난 뒤) 처음부터 다시");
    }
}
