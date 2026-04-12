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

    public float CurrentProgress { get => currentProgress; set => currentProgress = value; }
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

        GameObject newObj = Instantiate(resultPrefab, spawnPos, spawnRot, spawnParent);
        CarryableItem newItem = newObj.GetComponent<CarryableItem>();

        if (newItem != null && oldItem != null)
            newItem.initialPlacePoint = oldPlacePoint;

        DecayableProp oldDecay = GetComponent<DecayableProp>();
        DecayableProp newDecay = newObj.GetComponent<DecayableProp>();

        if (newDecay != null)
        {
            if (oldDecay != null)
                oldDecay.CopyStateTo(newDecay);
            else
            {
                newDecay.ForceSetFreshness(1);
                if (debugLog)
                    Debug.Log($"[{GetType().Name}] 原食材无腐烂组件，成品强制新鲜度=1");
            }
        }

        if (oldPlacePoint != null)
        {
            oldPlacePoint.ClearOccupant();

            if (newItem != null)
            {
                newItem.SetNetworkTransformSync(false);
                bool placed = oldPlacePoint.TryAcceptItem(newItem);
                if (!placed && debugLog)
                    Debug.LogWarning($"[{GetType().Name}] TryAcceptItem failed for {newObj.name}");
            }
        }

        if (Mirror.NetworkServer.active)
            Mirror.NetworkServer.Spawn(newObj);

        if (Mirror.NetworkServer.active && newItem != null && oldPlacePoint != null)
            newItem.RpcMirrorRegisterAtPlacePoint(oldPlacePoint.transform.position);

        if (Mirror.NetworkServer.active)
            Mirror.NetworkServer.Destroy(gameObject);
        else
            Destroy(gameObject);
    }
}
