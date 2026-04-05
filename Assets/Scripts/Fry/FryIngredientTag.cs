using UnityEngine;

/// <summary>
/// 下锅食材的逻辑 ID，须与 <see cref="FryRecipeDatabase"/> 中配方的 ingredientId 字符串一致。
/// </summary>
public class FryIngredientTag : MonoBehaviour
{
    [Tooltip("例如 TomatoChunk、Eggs；须与菜谱数据库中配置的字符串完全一致")]
    [SerializeField] string ingredientId;

    public string NormalizedIngredientId => NormalizeId(ingredientId);

    public static string NormalizeId(string raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
    }
}
