using UnityEngine;

public class CarryableTool : MonoBehaviour
{
    public Rigidbody rb;
    public Collider col;
    public bool debugLog = true;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void PickUp(Transform holdPoint)
    {
        if (debugLog) Debug.Log($"[Tool] PickUp: {name} -> {holdPoint.name}");

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (col != null)
            col.enabled = false;

        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop()
    {
        if (debugLog) Debug.Log($"[Tool] Drop: {name}");

        transform.SetParent(null, true);

        if (col != null)
            col.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void Equip(Transform equipPoint)
    {
        if (debugLog) Debug.Log($"[Tool] Equip: {name} -> {equipPoint.name}");

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (col != null)
            col.enabled = false;

        transform.SetParent(equipPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void UnequipToGround()
    {
        if (debugLog) Debug.Log($"[Tool] UnequipToGround: {name}");

        Drop();
    }
}