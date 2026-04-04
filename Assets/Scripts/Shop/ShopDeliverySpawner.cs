using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在锚点处按 3×3×3 格摆放购买的预制体；超过 27 件后沿 batchOffset 平移整块网格继续摆放。
/// </summary>
public class ShopDeliverySpawner : MonoBehaviour
{
    [Header("Placement")]
    public Transform anchor;

    [Tooltip("单格在锚点本地空间中的步长（X/Y/Z）")]
    public Vector3 cellSize = new Vector3(0.25f, 0.25f, 0.25f);

    [Tooltip("每满 27 格后，下一块 3×3×3 立方体在世界空间中的整体偏移")]
    public Vector3 batchOffset = new Vector3(0.75f, 0f, 0f);

    public void SpawnPurchases(Dictionary<string, int> cart, ShopItemCatalog catalog)
    {
        if (anchor == null)
        {
            Debug.LogWarning("[ShopDeliverySpawner] anchor 未设置。");
            return;
        }

        if (catalog == null || cart == null || cart.Count == 0) return;

        var prefabsInOrder = new List<GameObject>();
        var keys = new List<string>(cart.Keys);
        keys.Sort(System.StringComparer.Ordinal);

        foreach (var id in keys)
        {
            int qty = cart[id];
            if (qty <= 0) continue;

            ShopItemCatalog.ShopItemEntry entry = catalog.GetEntry(id);
            if (entry == null || entry.worldPrefab == null)
            {
                Debug.LogWarning($"[ShopDeliverySpawner] 未配置预制体: {id}");
                continue;
            }

            for (int i = 0; i < qty; i++)
                prefabsInOrder.Add(entry.worldPrefab);
        }

        for (int slotIndex = 0; slotIndex < prefabsInOrder.Count; slotIndex++)
        {
            int batch = slotIndex / 27;
            int localSlot = slotIndex % 27;
            int ix = localSlot % 3;
            int iy = (localSlot / 9) % 3;
            int iz = (localSlot / 3) % 3;

            Vector3 localPos = new Vector3(ix * cellSize.x, iy * cellSize.y, iz * cellSize.z);
            Vector3 worldPos = anchor.TransformPoint(localPos) + batch * batchOffset;

            GameObject prefab = prefabsInOrder[slotIndex];
            Instantiate(prefab, worldPos, anchor.rotation);
        }
    }
}
