using UnityEngine;

/// <summary>
/// Sprite-Lit-Default(2D Lit) 계열 셰이더는 원래 SpriteRenderer 전용으로 설계돼서,
/// 내부적으로 unity_SpriteColor / unity_SpriteProps 라는 값을 SpriteRenderer가 자동으로 채워준다.
/// 이 셰이더를 MeshRenderer(진짜 3D 메쉬)에 그대로 쓰면 이 값들이 비어있어서
/// 색이 이상하게 나오거나(검정/투명) 렌더링이 깨질 수 있다 — 이걸 대신 채워주는 보정용 컴포넌트.
///
/// 참고: Unity가 향후 버전에서 MeshRenderer를 2D Lit 계열에 공식 지원하겠다고 밝힌 바 있어서,
/// 이 컴포넌트는 "정식 지원 전까지의 임시 보정"이라고 보면 된다.
/// </summary>
[RequireComponent(typeof(Renderer))]
[ExecuteAlways]
public class MeshSpriteLitPropertyFix : MonoBehaviour
{
    [Tooltip("스프라이트 틴트 색상 (기본값: 흰색 = 원래 색 그대로)")]
    public Color spriteColor = Color.white;

    MaterialPropertyBlock mpb;
    Renderer targetRenderer;

    void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Apply();
    }
#endif

    void Apply()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(mpb);

        // SpriteRenderer가 자동으로 채워주는 값들을 대신 채워준다.
        mpb.SetColor("unity_SpriteColor", spriteColor);
        mpb.SetColor("_Color", spriteColor);
        // unity_SpriteProps: (flipX, flipY, 기타) — 뒤집기 없음 기본값
        mpb.SetVector("unity_SpriteProps", new Vector4(1f, 0f, 0f, 0f));

        targetRenderer.SetPropertyBlock(mpb);
    }
}
