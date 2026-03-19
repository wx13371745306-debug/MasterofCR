using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrderUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("挂载 OrderResponse 的桌子物体")]
    public OrderResponse tableOrder;
    [Tooltip("用来放图标的容器（挂了 LayoutGroup 的物体）")]
    public Transform iconContainer;
    [Tooltip("图标预制体（只包含一个 Image 组件即可）")]
    public GameObject iconPrefab;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        // 初始化显示
        RefreshOrderUI();
    }

    void LateUpdate()
    {
        // 每一帧只需要处理朝向
        if (alwaysFaceCamera && mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        // 简易做法：每帧检查订单数量变化来刷新（或者你可以去改造 OrderResponse 加个事件）
        // 为了性能，我们最好去 OrderResponse 加个事件。但 MVP 版本我们可以先在 Update 里简单判断一下数量
        // 这里为了稳健，建议直接每帧刷或者加个脏标记。
        // *更好的做法*：我们在 OrderResponse 里加个 OnOrderChanged 事件。
        // 这里暂时先用轮询检测变化，如果列表内容变了就刷。
        CheckOrderChange();
    }

    private List<FryRecipeDatabase.FryRecipe> lastCachedOrder = new List<FryRecipeDatabase.FryRecipe>();

    void CheckOrderChange()
    {
        if (tableOrder == null) return;
        
        var current = tableOrder.GetCurrentOrder();
        bool changed = false;

        if (current.Count != lastCachedOrder.Count)
        {
            changed = true;
        }
        else
        {
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] != lastCachedOrder[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            RefreshOrderUI();
        }
    }

    public void RefreshOrderUI()
    {
        if (tableOrder == null || iconContainer == null || iconPrefab == null) return;

        // 1. 记录最新状态
        var current = tableOrder.GetCurrentOrder();
        lastCachedOrder.Clear();
        lastCachedOrder.AddRange(current);

        // 2. 清空旧图标
        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }

        // 3. 生成新图标
        foreach (var recipe in current)
        {
            if (recipe == null) continue;

            if (recipe.dishIcon == null)
            {
                Debug.LogWarning($"[OrderUI] 配方 '{recipe.recipeName}' 的 dishIcon 为空！请检查 FryRecipeDatabase 配置。");
                continue;
            }

            GameObject iconObj = Instantiate(iconPrefab, iconContainer);
            Image img = iconObj.GetComponent<Image>();
            
            if (img != null)
            {
                img.sprite = recipe.dishIcon;
                Debug.Log($"[OrderUI] 成功显示图标：{recipe.recipeName} -> {recipe.dishIcon.name}");
            }
            else
            {
                Debug.LogError($"[OrderUI] IconPrefab 上找不到 Image 组件！请检查预制体。");
            }
        }
    }
}