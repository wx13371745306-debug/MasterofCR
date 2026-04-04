using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeekLevelConfig", menuName = "Cooking/Week Level Config")]
public class WeekLevelConfigSO : ScriptableObject
{
    [Tooltip("索引 0 = 周一 … 6 = 周日")]
    public List<LevelConfigSO> days = new List<LevelConfigSO>(7);

    public LevelConfigSO GetDay(int dayIndex)
    {
        if (days == null || days.Count == 0) return null;
        int i = Mathf.Clamp(dayIndex, 0, days.Count - 1);
        return days[i];
    }
}
