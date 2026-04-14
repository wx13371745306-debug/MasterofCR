using UnityEngine;
using Mirror;

public class CarryableItem : NetworkBehaviour
{
    public enum ItemState
    {
        Free,
        Held,
        Placed
    }

    [Header("Refs")]
    public Rigidbody rb;
    public Collider[] itemColliders;
    public AttachPoseConfig poseConfig;
    public InteractableHighlight sensorHighlight;

    [Header("Category")]
    public ItemCategory categories = ItemCategory.None;

    [Header("Placement")]
    public ItemPlacePoint initialPlacePoint;

    [Header("Use")]
    public bool isUsable = true;
    public bool isPickable = true;
    public GameObject useHighlightObject;

    [Header("Physics")]
    [Tooltip("附着时是否将 Collider 切换为 Trigger（关闭则保留原始 Collider 状态）")]
    public bool disableCollidersOnAttach = true;

    [Header("IK 手部抓取点（可选）")]
    [Tooltip("左手抓取位置，留空则使用默认偏移")]
    public Transform leftHandGrip;
    [Tooltip("右手抓取位置，留空则使用默认偏移")]
    public Transform rightHandGrip;

    [Header("Debug")]
    public bool debugLog = false ;

    [HideInInspector] public PlayerAttributes lastHolderPlayer;

    public ItemPlacePoint CurrentPlacePoint => currentPlacePoint;
    public ItemState State => state;

    private ItemPlacePoint currentPlacePoint;
    private ItemPlacePoint lastPlacePointBeforeHold;
    private ItemState state = ItemState.Free;
    private int originalLayer;
    private NetworkBehaviour cachedNetTransform;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        poseConfig = GetComponent<AttachPoseConfig>();
        sensorHighlight = GetComponent<InteractableHighlight>();
        itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);
    }

    protected virtual void Awake()
    {
        originalLayer = gameObject.layer;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (itemColliders == null || itemColliders.Length == 0)
            itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        else
        {
            foreach (var c in itemColliders)
            {
                if (c == null)
                {
                    itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);
                    break;
                }
            }
        }

        if (useHighlightObject != null)
            useHighlightObject.SetActive(false);

        SetSensorHighlight(false);

        foreach (var nb in GetComponents<NetworkBehaviour>())
        {
            if (nb != this && nb.GetType().Name.Contains("NetworkTransform"))
            {
                cachedNetTransform = nb;
                break;
            }
        }
    }

    protected virtual void Start()
    {
        if (state == ItemState.Free && initialPlacePoint != null)
            ForcePlaceAtStart(initialPlacePoint);
    }

    public bool HasAnyCategory(ItemCategory mask)
    {
        if (mask == ItemCategory.None)
            return false;

        return (categories & mask) != 0;
    }

    public bool HasBinProtectedCategory() => HasAnyCategory(ItemCategory.BinProtected);

    public virtual bool CanBePickedUp()
    {
        return isPickable && state != ItemState.Held;
    }

    /// <summary>
    /// 带抓取点上下文（例如饰品需校验 PlayerAccessoryHolder 空槽），默认与无参一致。
    /// </summary>
    public virtual bool CanBePickedUp(Transform holdPoint)
    {
        return CanBePickedUp();
    }

    public virtual void BeginHold(Transform holdPoint)
    {
        if (holdPoint == null) return;

        lastHolderPlayer = holdPoint.GetComponentInParent<PlayerAttributes>();
        lastPlacePointBeforeHold = currentPlacePoint;

        if (currentPlacePoint != null)
        {
            currentPlacePoint.ClearOccupant();
            currentPlacePoint = null;
        }

        SetNetTransformEnabled(false);
        SetAttachedPhysics(true);
        SetHeldLayer(true);

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

        state = ItemState.Held;
        if (debugLog) Debug.Log($"[CarryableItem] Begin hold: {name}");
    }

    // 放置动作交给目标点位主导
    public bool TryReleaseToPoint(ItemPlacePoint targetPoint)
    {
        if (targetPoint == null) return false;
        return targetPoint.TryAcceptItem(this);
    }

    // 仅供 ItemPlacePoint 内部调用的状态切换
    public void InternalSetPlacedState(ItemPlacePoint point)
    {
        currentPlacePoint = point;
        SetAttachedPhysics(true);
        SetHeldLayer(false);
        transform.SetParent(point.attachPoint, false);

        if (poseConfig != null)
        {
            transform.localPosition = poseConfig.placeLocalPosition;
            transform.localRotation = Quaternion.Euler(poseConfig.placeLocalEulerAngles);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        state = ItemState.Placed;
        SetNetTransformEnabled(true);
        if (debugLog) Debug.Log($"[CarryableItem] Placed internally: {name} at {point.name}");
    }

    // 仅供 ItemPlacePoint 调用的清除占用
    public void ClearPlaceState()
    {
        currentPlacePoint = null;
        state = ItemState.Free;
    }

    public virtual void DropToGround()
    {
        if (currentPlacePoint != null)
        {
            currentPlacePoint.ClearOccupant();
        }

        transform.SetParent(null);
        SetAttachedPhysics(false);
        SetHeldLayer(false);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (disableCollidersOnAttach && itemColliders != null)
        {
            foreach (var c in itemColliders)
            {
                if (c == null) continue;
                c.isTrigger = false;
            }
        }

        state = ItemState.Free;
        SetNetTransformEnabled(true);
        if (debugLog) Debug.Log($"[CarryableItem] Dropped to ground: {name}");
    }

    public void ForcePlaceAtStart(ItemPlacePoint point)
    {
        if (point != null)
        {
            lastPlacePointBeforeHold = point;
            point.TryAcceptItem(this);
        }
    }

    public bool TryUse(PlayerItemInteractor interactor, PlayerInteractionSensor sensor)
    {
        if (!isUsable)
        {
            if (debugLog) Debug.Log($"[CarryableItem] Use ignored: {name} is not usable.");
            return false;
        }

        MonoBehaviour[] all = GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IHoldUseTool tool)
            {
                bool used = tool.TryUse(interactor, sensor, this);
                if (used) return true;
            }
        }

        if (useHighlightObject != null)
        {
            bool next = !useHighlightObject.activeSelf;
            useHighlightObject.SetActive(next);
            if (debugLog) Debug.Log($"[CarryableItem] Use toggled highlight on {name}: {next}");
            return true;
        }

        if (debugLog) Debug.Log($"[CarryableItem] Use called on {name}, but no tool handled it.");
        return false;
    }

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null)
            sensorHighlight.SetHighlighted(on);
    }

    private void SetAttachedPhysics(bool attached)
    {
        if (rb != null)
        {
            if (attached)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (disableCollidersOnAttach && itemColliders != null)
        {
            foreach (var c in itemColliders)
            {
                if (c == null) continue;
                c.isTrigger = attached;
            }
        }
    }

    private void SetHeldLayer(bool held)
    {
        int targetLayer = held ? LayerMask.NameToLayer("HeldItem") : originalLayer;
        if (targetLayer == -1) return;
        if (debugLog)
            Debug.Log($"<color=#FFAA00>[CarryableItem]</color> {name} SetHeldLayer({held}) → Layer: {LayerMask.LayerToName(targetLayer)} | 调用来源:\n{System.Environment.StackTrace}");
        SetLayerRecursively(gameObject, targetLayer);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>服务端 ReplaceWithResult Spawn 成品后调用：纯客户端将占位镜像到与主机同一 ItemPlacePoint（无 NI 的槽位需对端显式 TryAccept）。</summary>
    [ClientRpc]
    public void RpcMirrorRegisterAtPlacePoint(Vector3 serverPlacePointWorldPos)
    {
        if (NetworkServer.active) return;

        ItemPlacePoint pp = ItemPlacePointNetUtil.FindItemPlacePointNearServerPosition(serverPlacePointWorldPos, ItemPlacePointNetUtil.ServerHintMatchRadius);
        if (pp == null)
            pp = ItemPlacePointNetUtil.FindNearestComponent<ItemPlacePoint>(transform.position, ItemPlacePointNetUtil.ReleasePlaceClientSearchRadius);

        if (pp == null)
        {
            if (debugLog)
                Debug.LogWarning($"[RpcMirrorRegisterAtPlacePoint] 无法解析 ItemPlacePoint serverPos={serverPlacePointWorldPos}");
            return;
        }

        // 与主机 ReplaceWithResult 一致：先 Clear 再 TryAccept。客户端槽位仍可能指向上一食材（Destroy 时序常晚于本 Rpc），否则 CanPlace 直接失败且物体无法挂到 attachPoint。
        if (pp.CurrentItem != null && pp.CurrentItem != this)
            pp.ClearOccupant(silent: true);

        SetNetworkTransformSync(false);
        if (!pp.TryAcceptItem(this))
        {
            // 放置失败时恢复 NT，避免关闭同步后卡在默认位姿 (0,0,0)、无父节点
            SetNetworkTransformSync(true);
            if (debugLog)
                Debug.LogWarning($"[ChopNetDiag][RpcMirrorRegisterAtPlacePoint] TryAcceptItem 失败 item={name} point={pp.name}");
        }
        else if (debugLog)
        {
            Debug.Log($"[ChopNetDiag] RpcMirrorRegisterAtPlacePoint OK item={name} → point={pp.name}");
        }
    }

    /// <summary>供外部（如 PlayerNetworkController）在联机 hold/release 时开关 NetworkTransform 同步。</summary>
    public void SetNetworkTransformSync(bool enabled) => SetNetTransformEnabled(enabled);

    private void SetNetTransformEnabled(bool enabled)
    {
        if (cachedNetTransform != null)
            cachedNetTransform.enabled = enabled;
    }
}
