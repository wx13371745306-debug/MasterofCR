using UnityEngine;

public class CarryableTool : MonoBehaviour
{
    public Rigidbody rb;
    public Collider col;
    public AttachPoseConfig poseConfig;
    public bool debugLog = true;

    // 记录当前放置的桌面
    public PlaceableSurface currentSurface;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        poseConfig = GetComponent<AttachPoseConfig>();
    }

    public void PickUp(Transform holdPoint)
    {
        if (debugLog) Debug.Log($"[Tool] PickUp: {name} -> {holdPoint.name}");

        // 如果原来放在桌子上，先清空那个位置
        if (currentSurface != null)
        {
            currentSurface.RemoveDish();
            currentSurface = null;
        }

        // 关掉高亮
        var highlight = GetComponent<DishHighlight>();
        if (highlight) highlight.SetHighlighted(false);

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
        
        if (poseConfig != null)
        {
            transform.localPosition = poseConfig.holdLocalPosition;
            transform.localRotation = Quaternion.Euler(poseConfig.holdLocalEulerAngles);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
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
        
        if (poseConfig != null)
        {
            transform.localPosition = poseConfig.equipLocalPosition;
            transform.localRotation = Quaternion.Euler(poseConfig.equipLocalEulerAngles);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    public void PlaceOnSurface(PlaceableSurface surface)
    {
        if (surface == null || surface.placePoint == null)
        {
            if (debugLog) Debug.Log("[Tool] PlaceOnSurface failed: surface or placePoint is null");
            return;
        }

        if (debugLog) Debug.Log($"[Tool] PlaceOnSurface: {name} -> {surface.name}");

        transform.SetParent(null, worldPositionStays: true);
        transform.position = surface.placePoint.position;
        // 注意：不使用 placePoint.rotation，或者由 poseConfig 决定

        if (col != null)
            col.enabled = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 挂到 placePoint 下，方便应用 localPosition
        transform.SetParent(surface.placePoint, true);

        if (poseConfig != null)
        {
            // 应用位置偏移
            transform.localPosition = poseConfig.placeLocalPosition;
            
            // 忽略旋转偏移，强制保持不旋转（或者保持默认朝向，这里设为 identity）
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        currentSurface = surface;
        currentSurface.MarkOccupied(true);
    }

    public void UnequipToGround()
    {
        if (debugLog) Debug.Log($"[Tool] UnequipToGround: {name}");

        Drop();
    }
}