using TMPro;
using UnityEngine;

/// <summary>
/// 绑定右上角计时：准备+营业阶段显示剩余 mm:ss；其余阶段显示 00:00。
/// </summary>
public class DayTimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private DayCycleManager dayCycle;

    void Update()
    {
        var d = dayCycle != null ? dayCycle : DayCycleManager.Instance;
        if (d == null || label == null) return;

        if (d.Phase == DayCyclePhase.Prep || d.Phase == DayCyclePhase.Business)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(d.RemainingClockSeconds));
            label.text = FormatMmSs(s);
        }
        else
            label.text = "00:00";
    }

    static string FormatMmSs(int totalSeconds)
    {
        int m = totalSeconds / 60;
        int s = totalSeconds % 60;
        return $"{m:00}:{s:00}";
    }
}
