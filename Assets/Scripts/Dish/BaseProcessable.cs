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

        // 先生成新物体
        GameObject newObj = Instantiate(resultPrefab, spawnPos, spawnRot, spawnParent);
        CarryableItem newItem = newObj.GetComponent<CarryableItem>();

        // 如果旧物体原本在 PlacePoint 上，就让新物体重新正式放上去
        if (oldPlacePoint != null)
        {
            oldPlacePoint.ClearOccupant(oldItem);

            if (newItem != null)
            {
                bool placed = oldPlacePoint.ForcePlace(newItem);

                if (!placed && debugLog)
                    Debug.LogWarning($"[{GetType().Name}] ForcePlace failed for {newObj.name}");
            }
            else if (debugLog)
            {
                Debug.LogWarning($"[{GetType().Name}] Result prefab {newObj.name} has no CarryableItem.");
            }
        }

        Destroy(gameObject);
    }
}