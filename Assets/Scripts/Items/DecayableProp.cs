using UnityEngine;

/// <summary>
/// 控制物品的腐烂进程。
/// 通过监听 DayCycleManager.Instance.OnDayAdvanced 进行状态改变。
/// 如果物品处于冰箱中(isInFridge = true)，则会在不新鲜状态时锁定其烂度，不再进入腐烂状态。
/// </summary>
public class DecayableProp : MonoBehaviour
{
    public enum DecayState { Fresh, Stale, Rotten }

    [Header("Decay Settings")]
    [Tooltip("到达不新鲜所需的天数")]
    public int freshnessDuration = 3;
    [Tooltip("从不新鲜到达腐烂所需的天数")]
    public int rottennessDuration = 3;

    [Header("Visuals")]
    public GameObject freshObj;
    public GameObject staleObj;
    public GameObject rottenObj;
    
    [Tooltip("用于在冰箱等 UI 面板中显示的图标")]
    public Sprite itemIcon;

    [Header("Current State (Read Only)")]
    [SerializeField] private DecayState currentState = DecayState.Fresh;
    [SerializeField] private int currentFreshness;
    [SerializeField] private int currentRottenness;
    
    [Header("Flags")]
    [Tooltip("由冰箱系统自动切换该变量为True/False")]
    public bool isInFridge = false;

    public bool debugLog = false;

    public DecayState CurrentState => currentState;

    private void Awake()
    {
        currentFreshness = freshnessDuration;
        currentRottenness = rottennessDuration;
    }

    private void Start()
    {
        UpdateVisuals();
    }

    private void OnEnable()
    {
        if (DayCycleManager.Instance != null)
        {
            DayCycleManager.Instance.OnDayAdvanced += HandleDayAdvanced;
        }
    }

    private void OnDisable()
    {
        if (DayCycleManager.Instance != null)
        {
            DayCycleManager.Instance.OnDayAdvanced -= HandleDayAdvanced;
        }
    }

    private void HandleDayAdvanced()
    {
        if (currentState == DecayState.Fresh)
        {
            currentFreshness--;
            if (debugLog) Debug.Log($"[DecayableProp] {gameObject.name} 新鲜度-1，剩余: {currentFreshness}");

            if (currentFreshness <= 0)
            {
                currentState = DecayState.Stale;
                if (debugLog) Debug.Log($"[DecayableProp] {gameObject.name} 从 新鲜 变为 不新鲜");
            }
        }
        else if (currentState == DecayState.Stale)
        {
            // 如果在冰箱中，不新鲜状态的物品腐烂度减到 1 为止，不会变 0 从而保护不进入腐烂
            if (isInFridge && currentRottenness == 1)
            {
                if (debugLog) Debug.Log($"[DecayableProp] {gameObject.name} 受冰箱保护，腐烂度锁定为 {currentRottenness}");
            }
            else
            {
                currentRottenness--;
                if (debugLog) Debug.Log($"[DecayableProp] {gameObject.name} 腐烂度-1，剩余: {currentRottenness}");

                if (currentRottenness <= 0)
                {
                    currentState = DecayState.Rotten;
                    if (debugLog) Debug.Log($"[DecayableProp] {gameObject.name} 从 不新鲜 变为 腐烂");
                }
            }
        }

        UpdateVisuals();
    }

    /// <summary>
    /// 将本物体的腐烂进度完全复制给目标物体。通常用于供货箱生成物品时。
    /// </summary>
    public void CopyStateTo(DecayableProp other)
    {
        if (other == null) return;
        other.currentState = this.currentState;
        other.currentFreshness = this.currentFreshness;
        other.currentRottenness = this.currentRottenness;
        other.UpdateVisuals();
        
        if (debugLog) Debug.Log($"[DecayableProp] 将 {gameObject.name} (阶段:{currentState}) 的腐烂进度复制给 {other.gameObject.name}");
    }

    private void UpdateVisuals()
    {
        if (freshObj != null) freshObj.SetActive(currentState == DecayState.Fresh);
        if (staleObj != null) staleObj.SetActive(currentState == DecayState.Stale);
        if (rottenObj != null) rottenObj.SetActive(currentState == DecayState.Rotten);
    }
}
