using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Level Data")]
    [Tooltip("若设置了周配置，则按天覆盖 currentLevelConfig")]
    public WeekLevelConfigSO weekLevelConfig;

    public LevelConfigSO currentLevelConfig;
    public Transform spawnPoint; // 顾客出生点

    [Tooltip("耐心耗尽等情况下顾客走向并消失的位置；全场景共配一处即可，不必每张桌子单独拖")]
    public Transform customerExitPoint;

    [Header("Debug")]
    public bool debugLog = true;

    private float levelTimer = 0f;
    private int currentWaveIndex = 0;
    private bool spawningPaused;

    // 缓存场景里所有的桌子
    private OrderResponse[] allTables;

    void Start()
    {
        // 游戏开始时，找到场景里所有的桌子
        allTables = FindObjectsOfType<OrderResponse>();

        if (currentLevelConfig == null && weekLevelConfig == null)
        {
            Debug.LogError("<color=#FF0000>[Spawner]</color> 未配置关卡数据 (LevelConfigSO / WeekLevelConfigSO)！");
        }
    }

    /// <summary>
    /// 由 DayCycleManager 在新的一天调用：切换当日 LevelConfigSO 并重置波次计时。
    /// </summary>
    public void ConfigureForDay(int dayIndex)
    {
        if (weekLevelConfig != null)
        {
            LevelConfigSO dayCfg = weekLevelConfig.GetDay(dayIndex);
            if (dayCfg != null)
                currentLevelConfig = dayCfg;
        }

        levelTimer = 0f;
        currentWaveIndex = 0;
        if (debugLog)
            Debug.Log($"[Spawner] ConfigureForDay index={dayIndex}, waves={(currentLevelConfig != null ? currentLevelConfig.waves.Count : 0)}");
    }

    /// <summary>
    /// 根据周配置或回退的 currentLevelConfig，计算某日计划生成的顾客总数（各波 groupSize 之和）。
    /// </summary>
    public int GetPlannedGuestCountForDay(int dayIndex)
    {
        LevelConfigSO cfg = null;
        if (weekLevelConfig != null)
            cfg = weekLevelConfig.GetDay(dayIndex);
        if (cfg == null)
            cfg = currentLevelConfig;
        return cfg != null ? cfg.GetTotalPlannedGuestCount() : 0;
    }

    public void SetSpawningPaused(bool paused) => spawningPaused = paused;

    void Update()
    {
        if (spawningPaused) return;
        if (currentLevelConfig == null || currentWaveIndex >= currentLevelConfig.waves.Count)
            return; // 关卡配置为空或所有波次已出完，停止运行

        // 计时器推进
        levelTimer += Time.deltaTime;

        // 检查是否到了下一波的出兵时间
        WaveData nextWave = currentLevelConfig.waves[currentWaveIndex];
        if (levelTimer >= nextWave.spawnTime)
        {
            // 尝试找一张合适的空桌子
            OrderResponse freeTable = FindAvailableTable(nextWave.groupSize);

            if (freeTable != null)
            {
                SpawnGroup(nextWave, freeTable);
                currentWaveIndex++; // 成功生成，游标移到下一波
            }
            else
            {
                // 如果没有空桌子，它会卡在这里等待，直到下一帧再找（相当于排队机制）
                // 你也可以在这里做个提示，比如 "餐厅已满，顾客正在等待..."
            }
        }
    }

    /// <summary>
    /// 寻找一张状态为空闲，且椅子数量足够坐下这队人的桌子
    /// </summary>
    private OrderResponse FindAvailableTable(int requiredSeats)
    {
        foreach (var table in allTables)
        {
            // 【修改点】：不仅要是 Empty，而且不能被 Reserved（预定）
            if (table.currentState == OrderResponse.TableState.Empty && !table.isReserved && table.chairs.Count >= requiredSeats)
            {
                return table;
            }
        }
        return null;
    }
    /// <summary>
    /// 生成顾客小队并分配桌子
    /// </summary>
    private void SpawnGroup(WaveData wave, OrderResponse targetTable)
    {
        if (wave.groupPrefab == null)
        {
            Debug.LogError("<color=#FF0000>[Spawner]</color> 这波的 groupPrefab 没有配置！");
            return;
        }

        // 【新增这一行】：经理一拍板，这桌立刻打上预定标记！别人抢不走了！
        targetTable.isReserved = true;

        // 1. 动态覆写这桌的点餐范围（把 SO 里的数据传给桌子）
        targetTable.minDishes = wave.minDishes;
        targetTable.maxDishes = wave.maxDishes;
        targetTable.currentCustomerCount = wave.groupSize;

        // 2. 生成小队
        CustomerGroup newGroup = Instantiate(wave.groupPrefab, spawnPoint.position, Quaternion.identity);
        
        // 3. 初始化（寻路会自动开始）
        newGroup.InitGroup(wave.groupSize, targetTable, spawnPoint, customerExitPoint);

        if (debugLog)
        {
            Debug.Log($"<color=#00FFFF>[Spawner]</color> 游戏时间 {levelTimer:F1}s: 生成了 {wave.groupSize} 人小队，分配到桌号 {targetTable.tableId}。点餐范围: {wave.minDishes}-{wave.maxDishes}。");
            if (customerExitPoint == null)
                Debug.LogWarning("[Spawner][PatienceLeave] customerExitPoint 未设置：耐心耗尽时顾客会原地销毁而不会走向消失点。");
        }
    }
}