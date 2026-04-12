using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class CustomerGroup : NetworkBehaviour
{
    public static int ActiveGroupCount { get; private set; }

    [Header("Settings")]
    [Tooltip("单个顾客的预制体（必须挂有 CustomerAI 和 NavMeshAgent）")]
    public GameObject customerPrefab;

    [Header("Group Identity")]
    [Tooltip("这队有多少人（1-4）")]
    [Range(1, 4)]
    public int groupSize = 2;

    [Header("Order Settings")]
    [Tooltip("这桌最少点几个菜")]
    public int minDishes = 1;
    [Tooltip("这桌最多点几个菜")]
    public int maxDishes = 3;

    [Header("Patience - 等待点菜阶段")]
    [Tooltip("等待点菜阶段初始耐心")]
    public float basePatienceOrder = 100f;
    [Tooltip("等待点菜阶段每秒损失")]
    public float baseLossPerSecondOrder = 10f;

    [Header("Patience - 等待上菜阶段")]
    [Tooltip("等上菜阶段初始耐心")]
    public float basePatienceFood = 100f;
    [Tooltip("等上菜阶段每秒损失")]
    public float baseLossPerSecondFood = 5f;

    [Header("Patience - 上菜后耐心回复")]
    [Tooltip("任意菜品上桌后增加的耐心")]
    public float baseServePatienceBonus = 60f;
    [Tooltip("耐心值上限")]
    public float basePatienceCap = 100f;
    [Tooltip("低于此值时显示「不耐烦」图标（等上菜阶段）")]
    public float baseImpatientThreshold = 40f;

    [Header("Debug")]
    public bool debugLog = true;

    private int totalMembers;
    private int seatedMembers = 0;
    private OrderResponse assignedTable;
    private Transform customerExitPoint;

    private readonly List<CustomerAI> memberAis = new List<CustomerAI>();

    /// <summary>
    /// 由 Host 端 CustomerSpawner 调用。生成子顾客 AI 并通过 RPC 让 Guest 端执行相同的桌子绑定。
    /// </summary>
    public void InitGroup(OrderResponse table, Transform spawnPoint, Transform exitPoint = null)
    {
        totalMembers = groupSize;
        assignedTable = table;
        customerExitPoint = exitPoint;

        if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 成功组建 {groupSize} 人小队，目标桌号: {table.tableId}");

        int actualSpawnCount = Mathf.Min(groupSize, table.chairs.Count);

        for (int i = 0; i < actualSpawnCount; i++)
        {
            GameObject aiObj = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
            aiObj.name = $"Customer_{table.tableId}_{i + 1}";

            CustomerAI ai = aiObj.GetComponent<CustomerAI>();
            if (ai != null)
            {
                ai.syncTableId = table.tableId;
                ai.syncChairIndex = i;
            }

            if (NetworkServer.active)
                NetworkServer.Spawn(aiObj);

            if (ai != null)
            {
                memberAis.Add(ai);
                ai.MoveToChair(table.approachPoint, table.chairs[i], OnMemberSeated);
            }
        }

        table.ApplyGroupConfig(this);
        table.RegisterCustomerGroup(this);
        ActiveGroupCount++;

        if (actualSpawnCount > 0)
        {
            var dcm = DayCycleManager.Instance;
            if (dcm == null || dcm.Phase == DayCyclePhase.Business)
                DayStatsTracker.Instance?.RegisterFootfall(actualSpawnCount);
        }

        if (NetworkServer.active)
            RpcInitGroupOnClients(table.tableId);
    }

    /// <summary>Guest 端收到后，将本组绑定到对应桌子。各个 CustomerAI 通过 SyncVar 自行初始化椅子。</summary>
    [ClientRpc]
    void RpcInitGroupOnClients(int tableId)
    {
        if (NetworkServer.active) return;

        OrderResponse table = FindTableById(tableId);
        if (table == null)
        {
            if (debugLog) Debug.LogWarning($"[CustomerGroup] Guest 找不到 tableId={tableId} 的桌子");
            return;
        }

        assignedTable = table;
        totalMembers = groupSize;

        var spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner != null)
            customerExitPoint = spawner.customerExitPoint;

        table.ApplyGroupConfig(this);
        table.RegisterCustomerGroup(this);
        ActiveGroupCount++;

        var allAIs = FindObjectsByType<CustomerAI>(FindObjectsSortMode.None);
        foreach (var ai in allAIs)
        {
            if (ai != null && ai.syncTableId == tableId)
                memberAis.Add(ai);
        }

        if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> Guest 端绑定桌号 {tableId}，收集到 {memberAis.Count} 名 AI");
    }

    static OrderResponse FindTableById(int tableId)
    {
        var tables = Object.FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        foreach (var t in tables)
        {
            if (t.tableId == tableId) return t;
        }
        return null;
    }

    void OnDestroy()
    {
        ActiveGroupCount = Mathf.Max(0, ActiveGroupCount - 1);
    }

    public void BeginLeaveGroup()
    {
        memberAis.RemoveAll(a => a == null);
        int count = memberAis.Count;

        if (debugLog)
        {
            string exitName = customerExitPoint != null ? customerExitPoint.name : "NULL";
            Debug.Log($"[PatienceLeave][Group {name}] BeginLeaveGroup | AI数量={count} | 消失点={exitName}");
        }

        if (count == 0)
        {
            if (assignedTable != null) assignedTable.NotifyPatienceLeaveComplete();
            if (NetworkServer.active)
                NetworkServer.Destroy(gameObject);
            else
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
                if (NetworkServer.active)
                    NetworkServer.Destroy(gameObject);
                else
                    Destroy(gameObject);
            });
        }
    }

    private void OnMemberSeated()
    {
        seatedMembers++;
        if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 汇报: 1 名成员已落座 (进度: {seatedMembers}/{totalMembers})");

        if (seatedMembers >= totalMembers)
        {
            if (debugLog) Debug.Log($"<color=#FFA500>[CustomerGroup]</color> 汇报: 全组落座完毕！正式通知桌子 {assignedTable.tableId} 进入点单流程。");
            assignedTable.GroupSeated();
        }
    }
}
