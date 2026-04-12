using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// 挂在玩家预制体上，管理两个饰品槽位。
/// 本地玩家按 O 键卸下所有饰品；掉线时自动卸下。
/// 网络同步通过 PlayerNetworkController 统一代理。
/// </summary>
public class PlayerAccessoryHolder : NetworkBehaviour
{
    [Header("装备挂点（在玩家 Root 下手动创建空子物体，拖入此处）")]
    public Transform mountPoint0;
    public Transform mountPoint1;

    [Header("卸下设置")]
    [Tooltip("卸下时饰品生成在玩家头顶多高")]
    public float dropHeightOffset = 1.5f;

    [Tooltip("两个饰品掉落时左右偏移量，避免重叠")]
    public float dropSideOffset = 0.3f;

    [Header("Debug")]
    public bool debugLog = true;

    private AccessoryItem slot0;
    private AccessoryItem slot1;
    private PlayerAttributes playerAttributes;
    private PlayerNetworkController networkController;

    public bool CanEquip() => slot0 == null || slot1 == null;
    public int EquippedCount => (slot0 != null ? 1 : 0) + (slot1 != null ? 1 : 0);

    void Awake()
    {
        playerAttributes = GetComponent<PlayerAttributes>();
        networkController = GetComponent<PlayerNetworkController>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.oKey.wasPressedThisFrame && EquippedCount > 0)
        {
            if (networkController != null && NetworkClient.active)
                networkController.CmdUnequipAllAccessories();
            else
                ExecuteUnequipAll();
        }
    }

    // ── 装备 ──────────────────────────────────────────

    /// <summary>将饰品装备到空槽位（由 AccessoryItem.BeginHold 调用）</summary>
    public void Equip(AccessoryItem item)
    {
        Transform mount;
        if (slot0 == null)
        {
            slot0 = item;
            mount = mountPoint0;
        }
        else if (slot1 == null)
        {
            slot1 = item;
            mount = mountPoint1;
        }
        else return;

        uint ownerNetId = netId;
        item.AttachToPlayer(mount, ownerNetId);
        item.OnEquipped(playerAttributes);

        if (debugLog)
            Debug.Log($"<color=#00FF00>[PlayerAccessoryHolder]</color> 装备了 {item.name}，当前槽位占用: {EquippedCount}/2");
    }

    /// <summary>远端镜像挂载：仅做视觉 SetParent，不触发属性加成（属性由本地端处理）。</summary>
    public void RemoteVisualEquip(AccessoryItem item)
    {
        Transform mount;
        if (slot0 == null)
        {
            slot0 = item;
            mount = mountPoint0;
        }
        else if (slot1 == null)
        {
            slot1 = item;
            mount = mountPoint1;
        }
        else return;

        // 与 AttachToPlayer 一致：在 SetParent 前记录缩放，再乘 equippedScale，避免父级缩放改变 localScale 后重复折算错误
        Vector3 scaleBeforeParent = item.transform.localScale;

        item.SetNetworkTransformSync(false);

        if (item.rb != null)
        {
            item.rb.isKinematic = true;
            item.rb.useGravity = false;
        }
        if (item.itemColliders != null)
        {
            foreach (var c in item.itemColliders)
                if (c != null) c.enabled = false;
        }

        item.transform.SetParent(mount, false);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = scaleBeforeParent * item.equippedScale;

        if (debugLog)
            Debug.Log($"<color=#00FF00>[PlayerAccessoryHolder]</color> 远端视觉挂载 {item.name} 到 {mount.name}");
    }

    // ── 卸下 ──────────────────────────────────────────

    /// <summary>实际执行卸下所有饰品的逻辑（所有端调用）</summary>
    public void ExecuteUnequipAll()
    {
        Vector3 headPos = transform.position + Vector3.up * dropHeightOffset;
        Vector3 right = transform.right;

        if (slot0 != null)
        {
            slot0.OnUnequipped(playerAttributes);
            slot0.DetachFromPlayer(headPos - right * dropSideOffset);
            slot0 = null;
        }
        if (slot1 != null)
        {
            slot1.OnUnequipped(playerAttributes);
            slot1.DetachFromPlayer(headPos + right * dropSideOffset);
            slot1 = null;
        }

        if (debugLog)
            Debug.Log($"<color=#FF4444>[PlayerAccessoryHolder]</color> 已卸下所有饰品");
    }

    // ── 掉线处理 ──────────────────────────────────────

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (EquippedCount > 0)
            ExecuteUnequipAll();
    }
}
