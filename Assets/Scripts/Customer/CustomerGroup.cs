using UnityEngine;
using System.Collections.Generic;

public class CustomerGroup : MonoBehaviour
{
    public static int ActiveGroupCount { get; private set; }

    [Header("Settings")]
    [Tooltip("单个顾客的预制体（必须挂有 CustomerAI 和 NavMeshAgent）")]
    public GameObject customerPrefab;
    
    [Header("Debug")]
    public bool debugLog = true;

    private int totalMembers;
    private int seatedMembers = 0;
    private OrderResponse assignedTable;
    /// <summary>由生成器传入：离场目标（与桌子数量无关，集中管理）</summary>
    private Transform customerExitPoint;

    /// <summary>
    /// 生成时缓存的 AI。入座后 <see cref="CustomerAI"/> 会 SetParent 到椅子，不再是本物体子节点，
    /// 因此不能用 GetComponentsInChildren，必须用此列表离场。
    /// </summary>
    private readonly List<CustomerAI> memberAis = new List<CustomerAI>();

    /// <summary>
    /// 初始化并生成队伍
    /// </summary>
    /// <param name="size">这队有多少人</param>
    /// <param name="table">他们要去的桌子</param>
    /// <param name="spawnPoint">出生点</param>
    /// <param name="exitPoint">消失点（可为空，则离场时在原地销毁）</param>
    public void InitGroup(int size, OrderResponse table, Transform spawnPoint, Transform exitPoint = null)
    {
        totalMembers = size;
        assignedTable = table;
        customerExitPoint = exitPoint;

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
                memberAis.Add(ai);
                // 指挥 AI 走向对应的椅子，并把 OnMemberSeated 方法作为回调传给它
                ai.MoveToChair(table.approachPoint, table.chairs[i], OnMemberSeated);
            }
            else
            {
                Debug.LogError($"<color=#FF0000>[CustomerGroup 错误]</color> 预制体 {customerPrefab.name} 上缺少 CustomerAI 脚本！");
            }
        }

        table.RegisterCustomerGroup(this);
        ActiveGroupCount++;

        if (actualSpawnCount > 0)
        {
            var dcm = DayCycleManager.Instance;
            if (dcm == null || dcm.Phase == DayCyclePhase.Business)
                DayStatsTracker.Instance?.RegisterFootfall(actualSpawnCount);
        }
    }

    void OnDestroy()
    {
        ActiveGroupCount = Mathf.Max(0, ActiveGroupCount - 1);
    }

    /// <summary>
    /// 耐心归零等：全员走向 <see cref="customerExitPoint"/> 后销毁本组（消失点由 <see cref="CustomerSpawner"/> 在生成时注入）。
    /// </summary>
    public void BeginLeaveGroup()
    {
        memberAis.RemoveAll(a => a == null);
        int count = memberAis.Count;

        if (debugLog)
        {
            string exitName = customerExitPoint != null ? customerExitPoint.name : "NULL";
            Debug.Log($"[PatienceLeave][Group {name}] BeginLeaveGroup | AI数量={count}（缓存列表，入座后已不在子物体下）| 消失点={exitName}" +
                      (customerExitPoint == null ? "（为 NULL 时 AI 会原地销毁，请在 CustomerSpawner 上绑定 customerExitPoint）" : ""));
        }

        if (count == 0)
        {
            if (debugLog) Debug.LogWarning($"[PatienceLeave][Group {name}] 无有效 CustomerAI（列表为空或已销毁），直接销毁组并 NotifyPatienceLeaveComplete");
            if (assignedTable != null) assignedTable.NotifyPatienceLeaveComplete();
            Destroy(gameObject);
            return;
        }

        int pending = count;
        foreach (var ai in memberAis)
        {
            if (ai == null) continue;
            ai.BeginLeave(customerExitPoint, () =>
            {
                pending--;
                if (pending > 0) return;
                if (assignedTable != null) assignedTable.NotifyPatienceLeaveComplete();
                Destroy(gameObject);
            });
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