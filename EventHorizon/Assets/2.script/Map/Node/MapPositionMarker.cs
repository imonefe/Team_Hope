using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-15 추가] 노드 캔버스에서 "내가 지금 있는 노드" 위에 떠 있는 금색 마름모 핀.
///
/// - MapManager 가 노드 캔버스를 만들 때 캔버스 맨 위(마지막 자식)에 함께 만든다. 씬이나 프리팹에 배치할 필요 없음.
/// - 지금 있는 노드 위에서 위아래로 살짝 흔들린다.
/// - 다른 노드를 고르면 그 노드까지 살짝 뛰어오르듯 미끄러져 간다 (MoveTo).
/// - 노드맵이 열려 있는 동안 게임 시간이 멈추므로 모든 움직임은 실제 시간 기준.
/// - 마우스 클릭을 막지 않는다 (아래 노드를 그대로 누를 수 있음).
/// </summary>
public class MapPositionMarker : MonoBehaviour
{
    RectTransform rt;
    MapButton target;        // 지금 핀이 가리키는 노드
    float height;            // 노드 가운데 → 핀 아래 꼭짓점 높이
    float bobAmount;         // 흔들림 폭
    float bobSpeed;          // 1초에 흔들리는 횟수

    // 옮겨 가는 중
    bool moving;
    Vector3 moveFrom;
    float moveElapsed, moveDuration, moveHop;

    /// <summary>핀을 만든다. 부모는 노드 버튼들과 같은 부모(캔버스 루트)여야 한다.</summary>
    public static MapPositionMarker Create(Transform parent, Sprite sprite, Vector2 size,
                                           float height, float bobAmount, float bobSpeed)
    {
        var go = new GameObject("PositionMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;

        var marker = go.AddComponent<MapPositionMarker>();
        marker.rt = (RectTransform)go.transform;
        marker.rt.SetParent(parent, false);
        marker.rt.anchorMin = marker.rt.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rt.pivot = new Vector2(0.5f, 0f);   // 아래 꼭짓점이 기준점 → 노드를 가리킴
        marker.rt.sizeDelta = size;
        marker.rt.SetAsLastSibling();               // 노드·연결선보다 위에 그림

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        marker.height = height;
        marker.bobAmount = bobAmount;
        marker.bobSpeed = bobSpeed;
        return marker;
    }

    public bool IsMoving => moving;

    /// <summary>핀을 노드 위에 바로 놓는다. null 이면 숨긴다.</summary>
    public void SnapTo(MapButton button)
    {
        moving = false;
        target = button;
        gameObject.SetActive(button != null);
        if (button == null) return;
        rt.SetAsLastSibling();
        Apply(BasePosition(button));
    }

    /// <summary>지금 노드에서 button 까지 duration 초 동안 옮겨 간다. 다 옮겨 가면 끝나는 코루틴.</summary>
    public IEnumerator MoveTo(MapButton button, float duration, float hop)
    {
        if (button == null) yield break;
        if (target == null || duration <= 0f || !gameObject.activeInHierarchy)
        {
            SnapTo(button);
            yield break;
        }

        moveFrom = BasePosition(target);
        target = button;
        moveElapsed = 0f;
        moveDuration = duration;
        moveHop = hop;
        moving = true;

        // 이동은 LateUpdate 가 한다. 혹시 캔버스가 꺼져 멈추더라도 영원히 기다리지 않도록 실제 시간으로 한 번 더 확인
        float waited = 0f;
        while (moving && waited < duration + 0.5f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        moving = false;
    }

    // 노드 가운데 위치 (핀의 부모 좌표 기준)
    Vector3 BasePosition(MapButton button)
    {
        return rt.parent.InverseTransformPoint(button.transform.position);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 매 프레임 노드 위치를 다시 읽는다 (화면 크기 변화·등장 애니메이션에도 따라붙게)
        Vector3 to = BasePosition(target);
        Vector3 pos = to;
        if (moving)
        {
            moveElapsed += Time.unscaledDeltaTime;
            float t = moveDuration > 0f ? Mathf.Clamp01(moveElapsed / moveDuration) : 1f;
            float eased = t * t * (3f - 2f * t);                 // 천천히 출발 → 천천히 도착
            pos = Vector3.LerpUnclamped(moveFrom, to, eased);
            pos.y += Mathf.Sin(t * Mathf.PI) * moveHop;          // 가운데쯤 살짝 뛰어오름
            if (t >= 1f) moving = false;
        }
        Apply(pos);
    }

    void Apply(Vector3 basePosition)
    {
        // 0 ~ bobAmount 사이로만 흔들림 → 핀이 노드 안으로 파고들지 않음
        float bob = (Mathf.Sin(Time.unscaledTime * bobSpeed * Mathf.PI * 2f) + 1f) * 0.5f * bobAmount;
        rt.localPosition = basePosition + new Vector3(0f, height + bob, 0f);
    }
}
