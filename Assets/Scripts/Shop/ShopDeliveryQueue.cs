using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 准备阶段：5 秒前的订单合并到批次；5 秒当下统一 spawn；之后下单立即 spawn。
/// </summary>
public class ShopDeliveryQueue : MonoBehaviour
{
    [SerializeField] private ShopDeliverySpawner deliverySpawner;

    readonly Dictionary<string, int> pendingBatch = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<string, int> dayZeroPending = new Dictionary<string, int>(StringComparer.Ordinal);
    bool batchDeliveredForCurrentPrep;

    public ShopDeliverySpawner DeliverySpawner
    {
        get => deliverySpawner;
        set => deliverySpawner = value;
    }

    /// <summary>是否有已下单但尚未交付的货物（Day0 存单 + 准备阶段批次）。</summary>
    public bool HasPendingOrders => dayZeroPending.Count > 0 || pendingBatch.Count > 0;

    public void OnNewPrepStarted()
    {
        pendingBatch.Clear();
        batchDeliveredForCurrentPrep = false;

        foreach (var kv in dayZeroPending)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value <= 0) continue;
            if (!pendingBatch.TryGetValue(kv.Key, out int q)) q = 0;
            pendingBatch[kv.Key] = q + kv.Value;
        }
        dayZeroPending.Clear();
    }

    /// <summary>Day0 阶段下单：仅记账，第一天进入准备阶段时并入 pendingBatch，按准备阶段 5 秒规则交货。</summary>
    public void EnqueueDayZeroCart(Dictionary<string, int> cart)
    {
        if (cart == null) return;
        foreach (var kv in cart)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value <= 0) continue;
            if (!dayZeroPending.TryGetValue(kv.Key, out int q)) q = 0;
            dayZeroPending[kv.Key] = q + kv.Value;
        }
    }

    /// <summary>
    /// 由 DayCycleManager 在准备阶段调用：当经过 shopDeliveryDelay 秒时触发一次批次交货。
    /// </summary>
    public void TryDeliverBatchIfDue(float prepElapsed, float shopDeliveryDelayFromPrepStart, ShopItemCatalog catalog)
    {
        if (batchDeliveredForCurrentPrep || catalog == null || deliverySpawner == null) return;
        if (prepElapsed + 1e-4f < shopDeliveryDelayFromPrepStart) return;

        batchDeliveredForCurrentPrep = true;
        if (pendingBatch.Count > 0)
        {
            deliverySpawner.SpawnPurchases(pendingBatch, catalog);
            pendingBatch.Clear();
        }
    }

    /// <summary>
    /// 商店下单：根据是否已过批次时间决定立即生成或并入批次。
    /// </summary>
    public void EnqueueOrSpawn(Dictionary<string, int> cart, ShopItemCatalog catalog, float prepElapsed, float shopDeliveryDelayFromPrepStart)
    {
        if (cart == null || catalog == null || deliverySpawner == null) return;

        if (prepElapsed + 1e-4f >= shopDeliveryDelayFromPrepStart || batchDeliveredForCurrentPrep)
        {
            deliverySpawner.SpawnPurchases(cart, catalog);
            return;
        }

        foreach (var kv in cart)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value <= 0) continue;
            if (!pendingBatch.TryGetValue(kv.Key, out int q)) q = 0;
            pendingBatch[kv.Key] = q + kv.Value;
        }
    }
}
