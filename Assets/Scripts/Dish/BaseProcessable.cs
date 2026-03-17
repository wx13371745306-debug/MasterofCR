using UnityEngine;

public abstract class BaseProcessable : MonoBehaviour, IProcessable
{
    [Header("Processing")]
    [Min(0.01f)] public float requiredProgress = 60f;
    [SerializeField] protected float currentProgress = 0f;

    [Header("Result")]
    public GameObject resultPrefab;

    [Header("Debug")]
    public bool debugLog = true;

    private bool hasCompleted = false;

    public abstract ProcessType SupportedProcessType { get; }

    public float CurrentProgress => currentProgress;
    public float RequiredProgress => requiredProgress;
    public float NormalizedProgress => Mathf.Clamp01(currentProgress / requiredProgress);
    public bool IsComplete => hasCompleted || currentProgress >= requiredProgress;

    public virtual bool CanProcess(ProcessType processType)
    {
        return !IsComplete && processType == SupportedProcessType;
    }

    public virtual void ApplyProgress(ProcessType processType, float amount, BaseStation sourceStation)
    {
        if (!CanProcess(processType)) return;
        if (amount <= 0f) return;

        currentProgress += amount;

        if (debugLog)
        {
            Debug.Log($"[{GetType().Name}] Progress on {name}: {currentProgress:F2}/{requiredProgress:F2}");
        }

        if (currentProgress >= requiredProgress)
        {
            currentProgress = requiredProgress;
            CompleteProcessing(sourceStation);
        }
    }

    protected virtual void CompleteProcessing(BaseStation sourceStation)
    {
        if (hasCompleted) return;
        hasCompleted = true;

        if (debugLog)
            Debug.Log($"[{GetType().Name}] Complete: {name}");

        ReplaceWithResult();
    }

    protected virtual void ReplaceWithResult()
    {
        if (resultPrefab == null)
        {
            if (debugLog)
                Debug.LogWarning($"[{GetType().Name}] {name} has no resultPrefab.");
            return;
        }

        CarryableItem oldItem = GetComponent<CarryableItem>();
        ItemPlacePoint oldPlacePoint = oldItem != null ? oldItem.CurrentPlacePoint : null;

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;
        Transform spawnParent = transform.parent;

        // 1. 先生成新物体
        GameObject newObj = Instantiate(resultPrefab, spawnPos, spawnRot, spawnParent);
        CarryableItem newItem = newObj.GetComponent<CarryableItem>();

        // 【核心修复】：让新生成的物体继承放置点记录
        if (newItem != null && oldItem != null)
        {
            // 继承原来的初始放置点
            newItem.initialPlacePoint = oldPlacePoint; 
        }

        // 2. 如果旧物体原本在 PlacePoint 上，就让新物体重新正式放上去
        if (oldPlacePoint != null)
        {
            // 【修正1】：使用新架构的无参清空方法
            oldPlacePoint.ClearOccupant(); 

            if (newItem != null)
            {
                // 【修正2】：使用新架构的 TryAcceptItem 替代原来的 ForcePlace
                bool placed = oldPlacePoint.TryAcceptItem(newItem); 

                if (!placed && debugLog)
                    Debug.LogWarning($"[{GetType().Name}] TryAcceptItem failed for {newObj.name}");
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[{GetType().Name}] Result prefab {newObj.name} has no CarryableItem.");
            }
        }

        // 3. 销毁旧物体
        Destroy(gameObject);
    }
}
