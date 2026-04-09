using UnityEngine;

public class FloorKillZone : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = true;

    private void OnTriggerEnter(Collider other)
    {
        BreakOnFloor breakable = other.GetComponentInParent<BreakOnFloor>();
        if (breakable != null)
        {
            if (debugLog)
                Debug.Log($"[FloorKillZone] Break triggered for {breakable.name}");

            breakable.Break();
        }
    }
}