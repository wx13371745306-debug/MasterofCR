using System.Collections.Generic;
using UnityEngine;

public class GlobalOrderUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("装载所有卡片的父节点（必须挂有 Horizontal Layout Group 组件）")]
    public Transform cardContainer;
    
    [Tooltip("单张卡片的预制体")]
    public GameObject orderCardPrefab;

    [Header("Debug")]
    public bool debugLog = true; // 开启调试日志

    private Dictionary<string, GameObject> activeCards = new Dictionary<string, GameObject>();

    public void SetOrdersVisible(bool visible)
    {
        if (cardContainer != null)
            cardContainer.gameObject.SetActive(visible);
    }

    void OnEnable()
    {
        GlobalOrderManager.OnOrderAdded += HandleOrderAdded;
        GlobalOrderManager.OnOrderRemoved += HandleOrderRemoved;
        
        if (debugLog) Debug.Log("<color=#00FFFF>[GlobalOrderUI]</color> 监听器已启动，正在等待新订单...");
    }

    void OnDisable()
    {
        GlobalOrderManager.OnOrderAdded -= HandleOrderAdded;
        GlobalOrderManager.OnOrderRemoved -= HandleOrderRemoved;
    }

    private void HandleOrderAdded(OrderInstance newOrder)
    {
        if (debugLog) Debug.Log($"<color=#00FFFF>[GlobalOrderUI]</color> 听到广播！准备为桌号 {newOrder.tableId} 生成 '{newOrder.recipe.recipeName}' 的卡片。");

        // 1. 检查引用是否丢失
        if (orderCardPrefab == null)
        {
            Debug.LogError("<color=#FF0000>[GlobalOrderUI 致命错误]</color> Order Card Prefab 槽位是空的！请在 Inspector 中赋值。");
            return;
        }
        if (cardContainer == null)
        {
            Debug.LogError("<color=#FF0000>[GlobalOrderUI 致命错误]</color> Card Container 槽位是空的！请在 Inspector 中赋值。");
            return;
        }

        // 2. 生成物体
        GameObject cardObj = Instantiate(orderCardPrefab, cardContainer);
        
        // 【防隐形保险】：强行把 UI 的缩放设为 1，防止生成时变成 0 导致看不见
        cardObj.transform.localScale = Vector3.one;

        // 3. 赋值数据
        OrderCardUI cardUI = cardObj.GetComponent<OrderCardUI>();
        if (cardUI != null)
        {
            cardUI.Setup(newOrder);
            if (debugLog) Debug.Log($"<color=#00FFFF>[GlobalOrderUI]</color> 卡片生成成功！ID: {newOrder.orderId}。当前总卡片数: {activeCards.Count + 1}");
        }
        else
        {
            Debug.LogError($"<color=#FF0000>[GlobalOrderUI 错误]</color> 生成的预制体 '{cardObj.name}' 身上没有挂载 OrderCardUI 脚本！");
        }

        activeCards.Add(newOrder.orderId, cardObj);
    }

    private void HandleOrderRemoved(OrderInstance removedOrder)
    {
        if (activeCards.TryGetValue(removedOrder.orderId, out GameObject cardObj))
        {
            Destroy(cardObj);
            activeCards.Remove(removedOrder.orderId);
            if (debugLog) Debug.Log($"<color=#00FFFF>[GlobalOrderUI]</color> 成功核销并销毁卡片！ID: {removedOrder.orderId}");
        }
    }
}