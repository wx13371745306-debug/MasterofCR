using System;
using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    public static event Action<int> OnMoneyChanged;

    [SerializeField] private int currentMoney = 0;

    public int CurrentMoney => currentMoney;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        OnMoneyChanged?.Invoke(currentMoney);
    }

    public void AddMoney(int amount)
    {
        if (amount == 0) return;

        int next = currentMoney + amount;
        currentMoney = Mathf.Max(0, next);

        OnMoneyChanged?.Invoke(currentMoney);
    }

    /// <summary>
    /// 扣除金钱；余额不足时返回 false 且不修改当前金额。
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (currentMoney < amount) return false;

        currentMoney -= amount;
        OnMoneyChanged?.Invoke(currentMoney);
        return true;
    }
}
