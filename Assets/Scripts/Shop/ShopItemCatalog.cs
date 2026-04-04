using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ShopItemCatalog", menuName = "Shop/Shop Item Catalog")]
public class ShopItemCatalog : ScriptableObject
{
    [System.Serializable]
    public class ShopItemEntry
    {
        [Tooltip("唯一 ID，同一目录内不可重复。可对应任意预制体（食材、饮料、道具等），如 drink_cola、plate_stack")]
        public string itemId = "";

        public string displayName;
        public Sprite icon;
        public int unitPrice = 1;
        public GameObject worldPrefab;
        public bool unlocked = true;

        [HideInInspector]
        [SerializeField, FormerlySerializedAs("ingredientId")]
        private FryIngredientId legacyIngredientId;

        /// <summary>从旧版「仅枚举食材」数据迁移到 itemId 字符串。</summary>
        public void MigrateLegacyIdIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(itemId) && legacyIngredientId != FryIngredientId.None)
                itemId = legacyIngredientId.ToString();
        }
    }

    public List<ShopItemEntry> items = new List<ShopItemEntry>();

    void OnValidate()
    {
        if (items == null) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in items)
        {
            if (e == null) continue;
            e.MigrateLegacyIdIfNeeded();

            if (string.IsNullOrWhiteSpace(e.itemId)) continue;

            string key = e.itemId.Trim();
            if (!seen.Add(key))
                Debug.LogWarning($"[ShopItemCatalog] 重复的 itemId: \"{key}\"", this);
        }
    }

    public ShopItemEntry GetEntry(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        string id = itemId.Trim();
        if (items == null) return null;

        foreach (var e in items)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.itemId)) continue;
            if (string.Equals(e.itemId.Trim(), id, StringComparison.Ordinal))
                return e;
        }
        return null;
    }

    public bool TryGetEntry(string itemId, out ShopItemEntry entry)
    {
        entry = GetEntry(itemId);
        return entry != null;
    }
}
