using UnityEngine;

// 지금 SpriteRenderer가 보여주고 있는 스프라이트의 "원본 텍스처"를 기준으로,
// 그 텍스처에 맞는 노멀맵(혹은 스페큘러 등) 재질로 바꿔치기 해주는 컴포넌트.
// 애니메이터 스테이트 이름이 아니라 실제로 화면에 나오는 텍스처로 판단하기 때문에,
// 무기 교체로 같은 스테이트(Atk01 등)에서 다른 스프라이트 시트가 나와도 항상 맞는 재질이 붙는다.
// 새 노멀맵이 추가되면 textureMaterials 배열에 (원본 텍스처, 재질) 한 줄만 추가하면 된다.
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimNormalMapSwitcher : MonoBehaviour
{
    [System.Serializable]
    public struct TextureMaterial
    {
        public Texture2D diffuseTexture;
        public Material material;
    }

    public SpriteRenderer spriteRenderer;
    public Material defaultMaterial;
    public TextureMaterial[] textureMaterials;

    private Material currentMaterial;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        Texture2D currentTexture = spriteRenderer.sprite.texture;
        Material target = defaultMaterial;

        if (textureMaterials != null)
        {
            for (int i = 0; i < textureMaterials.Length; i++)
            {
                if (textureMaterials[i].material != null && textureMaterials[i].diffuseTexture == currentTexture)
                {
                    target = textureMaterials[i].material;
                    break;
                }
            }
        }

        if (target != null && target != currentMaterial)
        {
            spriteRenderer.sharedMaterial = target;
            currentMaterial = target;
        }
    }
}
