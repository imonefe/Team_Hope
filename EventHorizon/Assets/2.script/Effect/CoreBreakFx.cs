using System.Collections;
using UnityEngine;

/// <summary>
/// [2026-11-24 추가] 보스방 코어가 부서질 때 연출 (FX_CoreBreak_Slash · Pierce · Strike 프리팹 맨 위에 붙음).
/// 입자(번쩍임 · 연기 · 파편 · 글자)는 프리팹 안 Particle System 이 스스로 재생하고, 이 스크립트는
///  - 속성 상징(깃펜)을 살짝 키우며 symbolFadeTime 초에 걸쳐 사라지게 하고 (그 모양 자리에서 글자 입자가 흩어져 나옴)
///  - 화면을 약하게 흔들고
///  - lifetime 초 뒤 연출 오브젝트를 지운다.
/// 프리팹은 Editor/CoreBreakFxBuilder 가 만든다.
/// </summary>
public class CoreBreakFx : MonoBehaviour
{
    [Tooltip("속성 상징 (깃펜) 그림")]
    public SpriteRenderer symbol;
    [Tooltip("상징이 사라지는 시간 (초)")]
    public float symbolFadeTime = 0.5f;
    [Tooltip("사라지는 동안 커지는 배율")]
    public float symbolGrow = 1.15f;
    [Tooltip("연출 전체 길이 — 이 시간 뒤 오브젝트를 지움 (초)")]
    public float lifetime = 1.6f;
    [Header("화면 흔들림")]
    public float shakeMagnitude = 0.15f;
    public float shakeTime = 0.2f;

    void Start()
    {
        if (shakeMagnitude > 0f) CameraFollow.Shake(shakeMagnitude, shakeTime);
        if (symbol != null) StartCoroutine(FadeSymbol());
        Destroy(gameObject, lifetime);
    }

    IEnumerator FadeSymbol()
    {
        Vector3 s0 = symbol.transform.localScale;
        Color c0 = symbol.color;
        float t = 0f;
        while (t < symbolFadeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / symbolFadeTime);
            symbol.transform.localScale = s0 * Mathf.Lerp(1f, symbolGrow, k);
            symbol.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k * k));
            yield return null;
        }
        symbol.enabled = false;
    }
}
