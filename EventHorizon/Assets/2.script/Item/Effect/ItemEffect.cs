using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 아이템 효과의 기본 틀
/// 새 효과 추가할 때 이 클래스 상속받아서 Apply() 구현
/// 아이템 정보 팝업에 보일 글자는 AppendDescription() 을 오버라이드해서 만든다.
/// </summary>
public abstract class ItemEffect : ScriptableObject
{
    [Header("팝업 표시 (선택)")]
    [Tooltip("비워두면 효과 수치로 자동 생성된 설명이 팝업에 표시됨. 직접 쓰고 싶을 때만 입력 (TMP 리치텍스트 사용 가능)")]
    [TextArea(1, 3)]
    public string descriptionOverride;

    public abstract void Apply(Stats stats, MonoBehaviour runner);

    /// <summary>팝업에 표시할 설명 항목들을 lines 에 추가한다. (직접 입력한 설명이 있으면 그걸 우선 사용)</summary>
    public void Describe(List<string> lines)
    {
        if (!string.IsNullOrWhiteSpace(descriptionOverride))
        {
            lines.Add(descriptionOverride.Trim());
            return;
        }
        AppendDescription(lines);
    }

    /// <summary>효과별 자동 설명. 한 항목 = 팝업 한 칸. 기본은 아무것도 표시하지 않음</summary>
    protected virtual void AppendDescription(List<string> lines) { }
}
