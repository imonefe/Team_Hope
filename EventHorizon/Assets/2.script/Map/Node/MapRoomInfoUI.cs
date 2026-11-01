using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// [2026-11-01] 노드 캔버스에서 고른 노드의 방 이름·설명을 보여 주는 글자 묶음.
/// 노드 캔버스 프리팹 안의 'RoomInfo' 오브젝트에 붙어 있고, 글자 두 개와 CanvasGroup 을 인스펙터에서 연결한다.
/// 글꼴·크기·색·외곽선·위치는 프리팹에서 직접 정한다. (코드는 내용과 등장·퇴장 움직임만)
/// MapManager 가 노드를 고를 때마다 Show 를 부른다. 고른 노드가 없으면 사라진다.
///
/// 등장: 왼쪽에서 제자리로 밀려 들어오며 나타남 / 퇴장: 오른쪽으로 밀려 나가며 사라짐
/// (노드 캔버스가 열린 동안 게임 시간이 멈춰 있으므로 실제 시간 기준으로 움직인다)
/// </summary>
public class MapRoomInfoUI : MonoBehaviour
{
    public TMP_Text nameText;   // 방 이름 (Battle, Boss …)
    public TMP_Text descText;   // 설명
    public CanvasGroup group;   // 묶음 전체 투명도

    [Header("등장·퇴장 연출")]
    [Tooltip("나타날 때 왼쪽에서 얼마나 밀려 들어오는지 (픽셀)")]
    public float slideInFrom = 30f;
    [Tooltip("사라질 때 오른쪽으로 얼마나 밀려 나가는지 (픽셀)")]
    public float slideOutTo = 20f;
    public float fadeInTime = 0.25f;
    public float fadeOutTime = 0.12f;

    RectTransform rt;
    Vector2 home;               // 프리팹에서 놓아 둔 제자리
    Sequence seq;
    MapRoomData shown;

    void Awake()
    {
        rt = (RectTransform)transform;
        home = rt.anchoredPosition;
        SetText(null);
        if (group != null) group.alpha = 0f;
    }

    void OnDisable()
    {
        // 캔버스가 닫히면 연출을 끊고 제자리·투명 상태로 (다음에 열 때 깔끔하게 시작)
        seq?.Kill();
        if (rt != null) rt.anchoredPosition = home;
        if (group != null) group.alpha = 0f;
        shown = null;
    }

    void OnDestroy() => seq?.Kill();

    public void Show(MapRoomData data)
    {
        if (data == shown) return;
        bool wasShown = shown != null && group != null && group.alpha > 0.01f;
        shown = data;

        seq?.Kill();
        if (group == null) { SetText(data); return; }   // CanvasGroup 을 안 넣었으면 내용만 바꿈

        seq = DOTween.Sequence().SetUpdate(true);
        if (wasShown)
        {
            seq.Append(group.DOFade(0f, fadeOutTime));
            seq.Join(rt.DOAnchorPos(home + new Vector2(slideOutTo, 0f), fadeOutTime).SetEase(Ease.InQuad));
        }
        if (data != null)
        {
            seq.AppendCallback(() =>
            {
                SetText(data);
                rt.anchoredPosition = home - new Vector2(slideInFrom, 0f);
            });
            seq.Append(group.DOFade(1f, fadeInTime));
            seq.Join(rt.DOAnchorPos(home, fadeInTime).SetEase(Ease.OutCubic));
        }
        else
        {
            seq.AppendCallback(() => { SetText(null); rt.anchoredPosition = home; });
        }
    }

    void SetText(MapRoomData data)
    {
        if (nameText != null) nameText.text = data != null ? data.GetDisplayName() : "";
        if (descText != null) descText.text = data != null ? data.GetDescription() : "";
    }
}
