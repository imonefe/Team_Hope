using System.Collections;
using UnityEngine;

// [Faust 2026-11-09] 홀로 분신 마무리용 화면 가르기: 기존 ScreenSlashEffect 를 청록 + 검정으로 재생
// M_ScreenSlash 는 보스 컷신과 같이 쓰는 머티리얼이라, 재생하는 동안만 색을 바꾸고 끝나면(또는 꺼지면) 원래 색으로 되돌림.
[RequireComponent(typeof(ScreenSlashEffect))]
public class HoloScreenSlash : MonoBehaviour
{
    [Header("색 (재생 중에만 적용)")]
    [ColorUsage(true, true)] public Color lineColor = new Color(0f, 2.4f, 2.1f, 1f);   // 검기 라인·글로우·스트릭 (청록)
    [ColorUsage(true, true)] public Color gapColor = new Color(0f, 0f, 0f, 1f);       // 갈라진 틈 (검정)
    public Color flashColor = new Color(0.1f, 0.9f, 0.85f, 1f);                       // 흰 번쩍임 대신 청록 번쩍임

    [Header("각도")]
    public float angle = 28f;
    public float angleRandom = 12f;          // ± 랜덤

    static readonly int ID_Line = Shader.PropertyToID("_LineColor");
    static readonly int ID_Gap = Shader.PropertyToID("_GapColor");
    ScreenSlashEffect fx;
    Material mat;
    Color oldLine, oldGap;
    bool applied;

    void Start()
    {
        fx = GetComponent<ScreenSlashEffect>();
        mat = fx.slashMaterial;
        if (mat == null) return;
        oldLine = mat.GetColor(ID_Line);
        oldGap = mat.GetColor(ID_Gap);
        mat.SetColor(ID_Line, lineColor);
        mat.SetColor(ID_Gap, gapColor);
        applied = true;
        fx.flashWhite = flashColor;
        fx.angle = angle + Random.Range(-angleRandom, angleRandom);
        fx.Play();
        StartCoroutine(RestoreWhenDone());
    }

    IEnumerator RestoreWhenDone()
    {
        yield return null;
        while (fx != null && fx.IsPlaying) yield return null;
        Restore();
    }

    void OnDisable() { Restore(); }

    void Restore()
    {
        if (!applied || mat == null) return;
        mat.SetColor(ID_Line, oldLine);
        mat.SetColor(ID_Gap, oldGap);
        applied = false;
    }
}
