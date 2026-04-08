using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冰箱站点管理系统。
/// 支持存储容量受限的物体，开启冰箱内的保底不腐烂机制。
/// </summary>
public class FridgeStation : BaseStation
{
    [Header("Fridge Settings")]
    public int maxCapacity = 2;
    [Tooltip("放进去的物品将被移到这里所在的层级并下移（不可见地隐藏）")]
    public Transform storageVisualPivot; 
    
    [Header("Visual Effects")]
    [Tooltip("冰箱为空时隐藏，不为空时激活的视觉提示")]
    public GameObject notEmptyVisualObj;
    
    [Header("UI References")]
    [Tooltip("冰箱上的画布中包含 Layout Group 的节点（如 HorizontalLayoutGroup）")]
    public RectTransform uiLayoutPanel;
    [Tooltip("放入物品后在UI上生成的图标预制体（需带Image组件）")]
    public GameObject iconUIPrefab;

    private Stack<CarryableItem> storedItems = new Stack<CarryableItem>();
    private List<GameObject> activeIconUIs = new List<GameObject>();

    protected override void Awake()
    {
        base.Awake();
        UpdateVisuals();
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        // 冰箱的交互核心在于玩家直接拿着东西按 J 或是空手按 J。
        // 这里拦截 K 键交互，防止跟基类发生冲突。
        if (debugLog) Debug.Log($"<color=#00FFFF>[FridgeStation]</color> {name} 不支持 K 键操作，请使用 J 键存取。");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
    }

    public bool CanAcceptItem() => storedItems.Count < maxCapacity;

    /// <summary>
    /// 被 PlayerItemInteractor 在按下 J（手中有物品并且面对本冰箱时）调用
    /// </summary>
    public bool TryPutHeldItem(PlayerItemInteractor interactor, CarryableItem item)
    {
        if (storedItems.Count >= maxCapacity)
        {
            if (debugLog) Debug.Log($"<color=#00FFFF>[FridgeStation]</color> {name} 已满 ({maxCapacity})，无法再放入！");
            return false;
        }

        if (item == null) return false;

        // 放手
        interactor.ReplaceHeldItem(null);
        item.DropToGround(); 
        
        // 移动到冰箱体系内并使其隐形但苟活（确保它能监听到换日事件）
        item.transform.SetParent(storageVisualPivot != null ? storageVisualPivot : transform, false);
        item.transform.localPosition = new Vector3(0, -9999f, 0); 
        
        // 彻底关闭其物理判定以防穿模或意外拿取
        if (item.rb != null)
        {
            item.rb.isKinematic = true;
            item.rb.useGravity = false;
        }
        if (item.itemColliders != null)
        {
            foreach (var coll in item.itemColliders)
            {
                if(coll != null) coll.enabled = false;
            }
        }

        // 把腐烂组件设为冰箱内保护状态（保底腐烂度为 1 不会进入 Rotten）
        DecayableProp itemDecay = item.GetComponent<DecayableProp>();
        if (itemDecay != null)
        {
            itemDecay.isInFridge = true;
        }

        storedItems.Push(item);
        if (debugLog) Debug.Log($"<color=#00FFFF>[FridgeStation]</color> 放入了: {item.name}。当前容量 {storedItems.Count}/{maxCapacity}");

        UpdateVisuals();
        return true;
    }

    /// <summary>
    /// 被 PlayerItemInteractor 在按下 J（空手并且面对本冰箱时）调用
    /// </summary>
    public bool TryTakeItem(PlayerItemInteractor interactor)
    {
        if (storedItems.Count == 0)
        {
            if (debugLog) Debug.Log($"<color=#00FFFF>[FridgeStation]</color> {name} 是空的，无法拿取。");
            return false;
        }

        CarryableItem poppedItem = storedItems.Pop();

        // 移除冰箱保护
        DecayableProp itemDecay = poppedItem.GetComponent<DecayableProp>();
        if (itemDecay != null)
        {
            itemDecay.isInFridge = false;
        }

        // 恢复它的碰撞体等物理状态
        if (poppedItem.itemColliders != null)
        {
            foreach (var coll in poppedItem.itemColliders)
            {
                if (coll != null) coll.enabled = true;
            }
        }

        // 送到玩家手里
        poppedItem.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(poppedItem);

        if (debugLog) Debug.Log($"<color=#00FFFF>[FridgeStation]</color> 取出了: {poppedItem.name}。当前容量 {storedItems.Count}/{maxCapacity}");

        UpdateVisuals();
        return true;
    }

    private void UpdateVisuals()
    {
        if (notEmptyVisualObj != null)
        {
            notEmptyVisualObj.SetActive(storedItems.Count > 0);
        }

        // 刷新冰箱面板里存放列表的 Icon
        if (uiLayoutPanel != null && iconUIPrefab != null)
        {
            foreach (var obj in activeIconUIs)
            {
                if (obj != null) Destroy(obj);
            }
            activeIconUIs.Clear();

            // Stack 倒序遍历（这样越早放入的就排在越上面/左侧）
            CarryableItem[] itemsArray = storedItems.ToArray();
            for (int i = itemsArray.Length - 1; i >= 0; i--)
            {
                CarryableItem stItem = itemsArray[i];
                GameObject iconObj = Instantiate(iconUIPrefab, uiLayoutPanel);
                Image img = iconObj.GetComponent<Image>();
                DecayableProp decay = stItem.GetComponent<DecayableProp>();
                
                if (img != null && decay != null && decay.itemIcon != null)
                {
                    img.sprite = decay.itemIcon;
                }
                activeIconUIs.Add(iconObj);
            }
        }
    }
}
