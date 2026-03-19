using UnityEngine;
using TMPro; // 如果你使用的是 TextMeshPro
// using UnityEngine.UI; // 如果你使用的是旧版普通 Text，请取消注释这行，并把下面的 TextMeshProUGUI 改成 Text

public class MoneyUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("拖入显示金钱的 Text 组件")]
    public TextMeshProUGUI moneyText; 
    
    [Header("Settings")]
    [Tooltip("金钱显示的前缀，比如 '$' 或 '金币: '")]
    public string prefix = "$ ";

    void OnEnable()
    {
        // 脚本激活时，订阅金钱变化事件
        MoneyManager.OnMoneyChanged += UpdateMoneyDisplay;
        
        // 尝试初始化显示当前的金钱（防止 UI 比 MoneyManager 晚加载导致一开始没文字）
        if (MoneyManager.Instance != null)
        {
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        // 脚本隐藏/销毁时，取消订阅，防止内存泄漏
        MoneyManager.OnMoneyChanged -= UpdateMoneyDisplay;
    }

    // 当金钱发生变化时，这个方法会被自动调用
    private void UpdateMoneyDisplay(int newAmount)
    {
        if (moneyText != null)
        {
            moneyText.text = prefix + newAmount.ToString();
        }
    }
}