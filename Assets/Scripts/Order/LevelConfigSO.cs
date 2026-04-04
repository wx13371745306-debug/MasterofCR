using System.Collections.Generic;
using UnityEngine;

// 这个标签让你可以直接在 Unity 里右键创建这个配置表
[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "Cooking/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("关卡波次设置")]
    [Tooltip("按时间顺序排列的顾客生成计划")]
    public List<WaveData> waves = new List<WaveData>();

    /// <summary>本关配置下计划生成的顾客总人数（各波 groupSize 之和）。</summary>
    public int GetTotalPlannedGuestCount()
    {
        if (waves == null) return 0;
        int n = 0;
        foreach (var w in waves)
        {
            if (w != null) n += w.groupSize;
        }
        return n;
    }
}

// 序列化此类，使其能在 Inspector 中显示
[System.Serializable]
public class WaveData
{
    [Header("时间与队伍")]
    [Tooltip("距离关卡开始多少秒后生成这一波")]
    public float spawnTime;
    
    [Tooltip("这波顾客的人数 (如 1-4)")]
    [Range(1, 4)]
    public int groupSize = 2;

    [Tooltip("使用的顾客小队预制体")]
    public CustomerGroup groupPrefab;

    [Header("点餐需求")]
    [Tooltip("这桌最少点几个菜")]
    public int minDishes = 1;
    [Tooltip("这桌最多点几个菜")]
    public int maxDishes = 3;
}