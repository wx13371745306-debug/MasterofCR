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
        ItemPlacePoint point = oldItem != null ? oldItem.CurrentPlacePoint : null;

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;
        Transform spawnParent = transform.parent;

        // 生成新结果物体
        GameObject newObj = Instantiate(resultPrefab, spawnPos, spawnRot, spawnParent);
        CarryableItem newItem = newObj.GetComponent<CarryableItem>();

        // 如果加工前是在台子上，让台子的放置点接收新物体
        if (point != null && newItem != null)
        {
            // 旧物体的信息清理已经在 TryAcceptItem 里处理或者即将销毁
            point.TryAcceptItem(newItem);
        }

        Destroy(gameObject); // 销毁自身的旧物体
    }
}