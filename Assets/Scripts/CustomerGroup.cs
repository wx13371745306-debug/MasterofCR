using UnityEngine;
using System.Collections.Generic;

public class CustomerGroup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("单个顾客的预制体（必须挂有 CustomerAI 和 NavMeshAgent）")]
    public GameObject customerPrefab;
    
    [Header("Debug")]
    public bool debugLog = true;

    private int totalMembers;
    private int seatedMembers = 0;
    private OrderResponse assignedTable;

    /// <summary>
    /// 初始化并生成队伍
    /// </summary>
    /// <param name="size">这队有多少人</param>
    /// <param name="table">他们要去的桌子</param>
    /// <param name="spawnPoint">出生点</param>
    public void InitGroup(int size, OrderResponse table, Transform spawnPoint)
    {
        totalMembers = size;
        assignedTable = table;

        if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 成功组建 {size} 人小队，目标桌号: {table.tableId}");

        // 防止生成的数量超过桌子的椅子数量
        int actualSpawnCount = Mathf.Min(size, table.chairs.Count);

        for (int i = 0; i < actualSpawnCount; i++)
        {
            // 在出生点生成 AI，并把当前 GameObject 设为他们的父物体，方便管理
            GameObject aiObj = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity, transform);
            aiObj.name = $"Customer_{table.tableId}_{i+1}"; // 起个名字方便 Debug 观察
            
            CustomerAI ai = aiObj.GetComponent<CustomerAI>();
            if (ai != null)
            {
                // 指挥 AI 走向对应的椅子，并把 OnMemberSeated 方法作为回调传给它
                ai.MoveToChair(table.approachPoint, table.chairs[i], OnMemberSeated);
            }
            else
            {
                Debug.LogError($"<color=#FF0000>[CustomerGroup 错误]</color> 预制体 {customerPrefab.name} 上缺少 CustomerAI 脚本！");
            }
        }
    }

    /// <summary>
    /// 当有队伍成员坐下时，会触发这个方法
    /// </summary>
    private void OnMemberSeated()
    {
        seatedMembers++;
        if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 汇报: 1 名成员已落座 (进度: {seatedMembers}/{totalMembers})");

        // 当所有人都坐好后
        if (seatedMembers >= totalMembers)
        {
            if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 汇报: 全组落座完毕！正式通知桌子 {assignedTable.tableId} 进入点单流程。");
            
            // 触发桌子的状态机！
            assignedTable.GroupSeated();
        }
    }
}