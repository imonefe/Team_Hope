using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-10-18 추가] 로비(영복의 은신처)에 들어오면 프롤로그 대화를 자동으로 시작한다.
///
/// - 런을 시작할 때마다 로비 방이 새로 만들어지므로, 런마다 한 번씩 재생된다.
/// - 플레이어가 생길 때까지 기다렸다가(최대 waitPlayerTimeout초) startDelay 뒤에 대화를 연다.
/// - 대사 내용은 npc(koke Canvas의 NPC)의 대사 세트 중 conditionKey 가 introKey 인 것.
/// - 조작 잠금·전투 UI 숨김·해제는 NPC 대화가 원래 하던 대로 처리한다.
/// </summary>
public class LobbyIntroDialogue : MonoBehaviour
{
    [Tooltip("대화를 띄울 NPC (로비의 koke Canvas)")]
    public NPC npc;
    [Tooltip("자동으로 재생할 대사 세트 이름 (NPC 대사 세트의 Condition Key)")]
    public string introKey = "intro";
    [Tooltip("플레이어가 나타난 뒤 대화를 열기까지 기다리는 시간(초) — 화면 전환이 끝날 여유")]
    public float startDelay = 0.8f;
    [Tooltip("플레이어가 이 시간 안에 나타나지 않으면 대화를 건너뜀(초)")]
    public float waitPlayerTimeout = 5f;

    IEnumerator Start()
    {
        if (npc == null) yield break;

        float t = 0f;
        while (PlayerController.Instance == null && t < waitPlayerTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        PlayerController player = PlayerController.Instance;
        if (player == null) yield break;

        for (float w = 0f; w < startDelay; w += Time.unscaledDeltaTime) yield return null;

        // 다른 대화가 이미 열려 있으면 끝날 때까지 기다림
        while (NPC.AnyTalking) yield return null;

        npc.SetCondition(introKey, player);
    }
}
