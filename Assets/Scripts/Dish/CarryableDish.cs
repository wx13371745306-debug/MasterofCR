using UnityEngine;

public class CarryableDish : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody rb;
    public Collider col;

    [Header("Debug")]
    public bool debugLog = true;

    public PlaceableSurface currentSurface;

    [Header("Break / Dirt")]
    public LayerMask floorKillMask;
    public GameObject dirtyStainPrefab;
    public GameObject breakEffectPrefab; // 可选，不要也行
    public bool canBreakOnFloor = true;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canBreakOnFloor) return;

        if (((1 << other.gameObject.layer) & floorKillMask) == 0)
            return;

        BreakOnFloor(transform.position);
    }

    public void PickUp(Transform holdPoint)
    {
        if (debugLog) Debug.Log($"[Dish] PickUp: {name} -> {holdPoint.name}");

        // 如果原来放在某个桌子上，先把那个位置清空
        if (currentSurface != null)
        {
            currentSurface.RemoveDish();
            currentSurface = null;
        }

        // 关掉高亮
        var highlight = GetComponent<DishHighlight>();
        if (highlight) highlight.SetHighlighted(false);

        // 拿在手里：关物理、关碰撞
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (col) col.enabled = false;

        // 跟随挂点
        transform.SetParent(holdPoint, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop()
    {
        if (debugLog) Debug.Log($"[Dish] Drop: {name}");

        transform.SetParent(null, worldPositionStays: true);

        if (col) col.enabled = true;

        if (rb)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    public void PlaceOnSurface(PlaceableSurface surface)
    {
        if (surface == null || surface.placePoint == null)
        {
            if (debugLog) Debug.Log("[Dish] PlaceOnSurface failed: surface or placePoint is null");
            return;
        }

        if (debugLog) Debug.Log($"[Dish] PlaceOnSurface: {name} -> {surface.name}");

        transform.SetParent(null, worldPositionStays: true);
        transform.position = surface.placePoint.position;
        transform.rotation = surface.placePoint.rotation;

        if (col) col.enabled = true;

        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        currentSurface = surface;
        currentSurface.MarkOccupied(true);
    }


    public void BreakOnFloor(Vector3 hitPoint)
    {
        if (!canBreakOnFloor) return;

        if (debugLog)
            Debug.Log($"[Dish] BreakOnFloor: {name} at {hitPoint}");

        // 如果原来放在桌子上，先清掉占位
        if (currentSurface != null)
        {
            currentSurface.RemoveDish();
            currentSurface = null;
        }

        // 生成污渍
        if (dirtyStainPrefab != null)
        {
            Vector3 stainPos = hitPoint + Vector3.up * 0.01f;
            Quaternion stainRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Instantiate(dirtyStainPrefab, stainPos, stainRot);
        }

        // 生成粒子特效
        if (breakEffectPrefab != null)
        {
            Vector3 fxPos = hitPoint + Vector3.up * 0.02f;
            Quaternion fxRot = Quaternion.identity;

            GameObject fx = Instantiate(breakEffectPrefab, fxPos, fxRot);

            ParticleSystem ps = fx.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                if (debugLog) Debug.Log("[Dish] Break effect played.");
            }
            else
            {
                Debug.LogWarning("[Dish] No ParticleSystem found in breakEffectPrefab.");
            }
        }

        Destroy(gameObject);
    }
}