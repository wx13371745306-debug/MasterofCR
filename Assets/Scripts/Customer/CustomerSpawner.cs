using System.Collections.Generic;
using UnityEngine;
using Mirror;

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
        RefreshTableCache();

        if (currentLevelConfig == null && weekLevelConfig == null)
        {
            Debug.LogError("<color=#FF0000>[Spawner]</color> 未配置关卡数据 (LevelConfigSO / WeekLevelConfigSO)！");
        }
    }

    private void RefreshTableCache()
    {
        allTables = FindObjectsByType<OrderResponse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (debugLog)
            Debug.Log($"[Spawner] 刷新桌子缓存，找到 {allTables.Length} 张桌子");
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
        if (NetworkClient.active && !NetworkServer.active) return;

        if (spawningPaused) return;
        if (currentLevelConfig == null || currentWaveIndex >= currentLevelConfig.waves.Count)
            return;

        if (allTables == null || allTables.Length == 0)
            RefreshTableCache();

        levelTimer += Time.deltaTime;

        // 检查是否到了下一波的出兵时间
        WaveData nextWave = currentLevelConfig.waves[currentWaveIndex];
        if (levelTimer >= nextWave.spawnTime)
        {
            if (nextWave.groupPrefab == null)
            {
                Debug.LogError("<color=#FF0000>[Spawner]</color> 这波的 groupPrefab 没有配置！");
                currentWaveIndex++;
                return;
            }

            int requiredSeats = nextWave.groupPrefab.groupSize;

            // 尝试找一张合适的空桌子
            OrderResponse freeTable = FindAvailableTable(requiredSeats);

            if (freeTable != null)
            {
                SpawnGroup(nextWave, freeTable);
                currentWaveIndex++; // 成功生成，游标移到下一波
            }
            else
            {
                if (debugLog)
                    Debug.LogWarning($"<color=#FFA500>[Spawner]</color> 时间已到但找不到空桌！wave={currentWaveIndex}, 需要座位={requiredSeats}, 桌子总数={allTables?.Length ?? 0}");
            }
        }
    }

    /// <summary>
    /// 寻找一张状态为空闲，且椅子数量足够坐下这队人的桌子
    /// </summary>
    private OrderResponse FindAvailableTable(int requiredSeats)
    {
        if (allTables == null || allTables.Length == 0)
        {
            if (debugLog) Debug.LogWarning("<color=#FF0000>[Spawner]</color> allTables 为空！场景中没找到任何 OrderResponse 桌子。");
            return null;
        }

        foreach (var table in allTables)
        {
            if (table.currentState == OrderResponse.TableState.Empty && !table.isReserved && table.chairs.Count >= requiredSeats)
            {
                return table;
            }
        }

        if (debugLog)
        {
            foreach (var table in allTables)
            {
                Debug.Log($"<color=#FFA500>[Spawner 桌况]</color> 桌号={table.tableId} state={table.currentState} reserved={table.isReserved} chairs={table.chairs.Count} 需要={requiredSeats}");
            }
        }
        return null;
    }
    /// <summary>
    /// 生成顾客小队并分配桌子
    /// </summary>
    private void SpawnGroup(WaveData wave, OrderResponse targetTable)
    {
        targetTable.isReserved = true;

        CustomerGroup newGroup = Instantiate(wave.groupPrefab, spawnPoint.position, Quaternion.identity);

        if (NetworkServer.active)
            NetworkServer.Spawn(newGroup.gameObject);

        newGroup.InitGroup(targetTable, spawnPoint, customerExitPoint);

        if (debugLog)
        {
            Debug.Log($"<color=#00FFFF>[Spawner]</color> 游戏时间 {levelTimer:F1}s: 生成了 {wave.groupPrefab.groupSize} 人小队，分配到桌号 {targetTable.tableId}。" +
                $"点餐范围: {wave.groupPrefab.minDishes}-{wave.groupPrefab.maxDishes}。");
            if (customerExitPoint == null)
                Debug.LogWarning("[Spawner][PatienceLeave] customerExitPoint 未设置：耐心耗尽时顾客会原地销毁而不会走向消失点。");
        }
    }
}