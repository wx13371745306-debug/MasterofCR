using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制游戏内 HUD 上的可用羁绊显示。
/// 会在启用和接到菜选单被确认的消息后，自动获取羁绊信息并生成Icon。
/// </summary>
public class HUDActiveBondUI : MonoBehaviour
{
    public bool debugLog = false;

    [Header("UI References")]
    [Tooltip("将用于布局的容器")]
    public Transform iconContainer;
    [Tooltip("悬浮羁绊图标的预制体")]
    public GameObject iconPrefab;

    void OnEnable()
    {
        MenuSelectionUIController.OnMenuConfirmed += RefreshBondsDisplay;
        // 延时或者正常抓取一遍
        RefreshBondsDisplay();
    }

    void OnDisable()
    {
        MenuSelectionUIController.OnMenuConfirmed -= RefreshBondsDisplay;
    }

    public void RefreshBondsDisplay()
    {
        // 确保单例状态已经初始化，如果为空可能有先后顺序问题
        if (BondRuntimeBridge.Instance == null || BondRuntimeBridge.Instance.State == null) return;

        if (debugLog) Debug.Log("[HUDActiveBondUI] 执行了某操作: 获取并刷新 HUD 羁绊列表");

        if (iconContainer == null || iconPrefab == null) return;

        // 清理旧图标
        for (int i = iconContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(iconContainer.GetChild(i).gameObject);
        }

        List<BondActivationStateSO.BondEntry> activeBonds = BondRuntimeBridge.Instance.State.GetActiveBonds();
        foreach (var bond in activeBonds)
        {
            GameObject go = Instantiate(iconPrefab, iconContainer);
            go.transform.localScale = Vector3.one;
            HUDActiveBondIcon iconView = go.GetComponent<HUDActiveBondIcon>();
            if (iconView != null)
            {
                iconView.Setup(bond);
            }
        }
        
        if (debugLog) Debug.Log($"[HUDActiveBondUI] 刷新完成: 渲染了 {activeBonds.Count} 个羁绊图标");
    }
}
