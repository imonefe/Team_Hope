using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-11-19 신규] 화면 가르기를 여러 줄 동시에 긋는다 (ScreenSlashEffect 옆에 붙임).
///
/// ScreenSlash 쉐이더의 기존 선 1개 + 추가 선(최대 8개)을 같은 진행도로 함께 그린다.
/// 재생이 끝나거나 오브젝트가 꺼지면 추가 선 수를 0으로 되돌려,
/// 같은 재질을 쓰는 처형 컷신·홀로분신 화면 가르기에는 영향이 남지 않게 한다.
/// </summary>
[RequireComponent(typeof(ScreenSlashEffect))]
public class ScreenSlashMultiLine : MonoBehaviour
{
    public const int MaxExtra = 8;   // 쉐이더 배열 크기와 같아야 함

    static readonly int CountId = Shader.PropertyToID("_ExtraLineCount");
    static readonly int LinesId = Shader.PropertyToID("_ExtraLines");

    ScreenSlashEffect fx;
    Material mat;
    bool applied;

    /// <summary>주 선(mainPos, mainAngle) + 추가 선들(월드 위치·각도)을 동시에 긋는다</summary>
    public void Play(Vector3 mainPos, float mainAngle, IList<Vector3> extraPos, IList<float> extraAngle)
    {
        fx = GetComponent<ScreenSlashEffect>();
        mat = fx != null ? fx.slashMaterial : null;
        if (fx == null) return;

        if (mat != null)
        {
            Camera cam = fx.targetCamera != null ? fx.targetCamera : Camera.main;
            var arr = new Vector4[MaxExtra];
            int n = 0;
            for (int i = 0; extraPos != null && i < extraPos.Count && n < MaxExtra; i++)
            {
                Vector3 vp = cam != null ? cam.WorldToViewportPoint(extraPos[i]) : new Vector3(0.5f, 0.5f, 0f);
                arr[n++] = new Vector4(vp.x, vp.y, extraAngle[i], 0f);
            }
            mat.SetVectorArray(LinesId, arr);
            mat.SetFloat(CountId, n);
            applied = true;
        }

        fx.PlayAtWorld(mainAngle, mainPos);
        StartCoroutine(ClearWhenDone());
    }

    IEnumerator ClearWhenDone()
    {
        yield return null;
        while (fx != null && fx.IsPlaying) yield return null;
        Clear();
    }

    void OnDisable() { Clear(); }

    void Clear()
    {
        if (!applied || mat == null) return;
        mat.SetFloat(CountId, 0f);
        applied = false;
    }
}
