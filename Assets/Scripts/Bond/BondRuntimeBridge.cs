using UnityEngine;

/// <summary>
/// 挂在 GlobalManagers 上，提供羁绊状态的全局入口。
/// 进入场景时自动从 MenuSO 刷新羁绊激活状态。
/// </summary>
public class BondRuntimeBridge : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = false;

    public static BondRuntimeBridge Instance { get; private set; }

    [Header("数据引用")]
    public BondActivationStateSO bondState;
    public MenuSO menuSO;

    public BondActivationStateSO State => bondState;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BondBridge] 重复实例被销毁: {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (debugLog) Debug.Log($"[BondBridge] Awake 初始化 | bondState={(bondState != null ? bondState.name : "NULL")} | menuSO={(menuSO != null ? menuSO.name : "NULL")}");
        Refresh();
    }

    /// <summary>根据当前 MenuSO 重新计算羁绊状态，并通知受影响的组件。</summary>
    public void Refresh()
    {
        if (bondState == null)
        {
            Debug.LogWarning("[BondBridge] bondState 未赋值，无法刷新羁绊。");
            return;
        }
        if (menuSO == null)
        {
            Debug.LogWarning("[BondBridge] menuSO 未赋值，羁绊将全部重置。");
        }
        else
        {
            if (debugLog) Debug.Log($"[BondBridge] Refresh 开始 | MenuSO 已选菜谱数: {menuSO.selectedRecipes.Count}");
        }

        bondState.RefreshFromMenu(menuSO);

        var activeBonds = bondState.GetActiveBonds();
        if (debugLog) Debug.Log($"[BondBridge] Refresh 完成，激活羁绊数: {activeBonds.Count}");
        foreach (var b in activeBonds)
            if (debugLog) Debug.Log($"[BondBridge]   已激活: '{b.displayName}' (tag={b.tag})");
    }
}
