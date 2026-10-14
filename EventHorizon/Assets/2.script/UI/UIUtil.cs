using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 공용 유틸. 여러 UI 스크립트에서 반복되는 "아이콘 스프라이트 세팅 + null이면 숨김" 패턴과
/// "패널 닫기(가능하면 애니메이션과 함께)" 패턴을 공유.
/// </summary>
public static class UIUtil
{
    /// <summary>스프라이트를 넣고, null이면 이미지를 자동으로 비활성화(sprite=null, enabled=false)</summary>
    public static void SetIcon(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.enabled = sprite != null;
    }

    /// <summary>
    /// panel에 UIPanelAnimator가 붙어있으면 퇴장 애니메이션(페이드아웃+스케일다운)을 재생한 뒤
    /// 실제로 SetActive(false)하고, 없으면 그냥 즉시 SetActive(false)한다.
    /// 이미 꺼져있으면 애니메이션 없이 바로 onComplete만 호출한다.
    /// 패널을 닫는 모든 UI 스크립트가 panel.SetActive(false) 대신 이걸 쓰면
    /// UIPanelAnimator가 붙어있는 패널은 자동으로 닫힐 때도 애니메이션이 재생된다
    /// (Inventory, ItemInfoPopup, GameMenuController 등).
    /// </summary>
    public static void HidePanel(GameObject panel, System.Action onComplete = null)
    {
        if (panel == null || !panel.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        UIPanelAnimator animator = panel.GetComponent<UIPanelAnimator>();
        if (animator != null)
        {
            animator.Hide(onComplete);
        }
        else
        {
            panel.SetActive(false);
            onComplete?.Invoke();
        }
    }
}
