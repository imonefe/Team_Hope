using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-01] 노드 캔버스에서 고른 노드의 방 이름·설명을 보여 주는 글자 묶음.
/// 노드 캔버스 프리팹 안의 'RoomInfo' 오브젝트에 붙어 있고, 글자 오브젝트 두 개를 인스펙터에서 연결한다.
/// 글꼴·크기·색·외곽선·위치는 프리팹의 글자 오브젝트에서 직접 정한다. (코드는 내용만 바꿈)
/// MapManager 가 노드를 고를 때마다 Show 를 부른다. 고른 노드가 없으면 빈칸.
/// </summary>
public class MapRoomInfoUI : MonoBehaviour
{
    public TMP_Text nameText;   // 방 이름 (Battle, Boss …)
    public TMP_Text descText;   // 설명

    void Awake() => Show(null);

    public void Show(MapRoomData data)
    {
        if (nameText != null) nameText.text = data != null ? data.GetDisplayName() : "";
        if (descText != null) descText.text = data != null ? data.GetDescription() : "";
    }
}
