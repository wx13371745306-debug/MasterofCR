using UnityEngine;
using Mirror;

/// <summary>
/// 饰品基类。继承 CarryableItem，重写拾取逻辑实现"拿起即装备"。
/// 子类重写 OnEquipped / OnUnequipped 来实现具体的属性加成。
/// </summary>
public class AccessoryItem : CarryableItem
{
    [Header("饰品 - 通用配置")]
    [Tooltip("未装备时绕Y轴旋转速度")]
    public float idleRotateSpeed = 30f;

    [Tooltip("装备后模型缩放比例（相对于原始大小）")]
    public float equippedScale = 0.5f;

    // 装备者的 netId（0 = 未被装备）
    [SyncVar] private uint _equippedByNetId;
    public bool IsEquipped => _equippedByNetId != 0;
    public uint EquippedByNetId => _equippedByNetId;

    /// <summary>
    /// 标记：BeginHold 中自动装备成功后置 true，
    /// 供 RpcAcknowledgePickUp 判断是否需要设 heldItem。
    /// </summary>
    [HideInInspector] public bool wasAutoEquipped = false;

    private Vector3 originalScale;

    protected override void Awake()
    {
        base.Awake();
        categories = ItemCategory.Accessory;
        isPickable = true;
        originalScale = transform.localScale;
    }

    void Update()
    {
        if (!IsEquipped && State == ItemState.Free)
            transform.Rotate(Vector3.up, idleRotateSpeed * Time.deltaTime);
    }

    // ── 拾取拦截 ──────────────────────────────────────

    public override void BeginHold(Transform holdPoint)
    {
        if (holdPoint == null) return;

        var holder = holdPoint.GetComponentInParent<PlayerAccessoryHolder>();
        if (holder != null && holder.CanEquip())
        {
            holder.Equip(this);
            wasAutoEquipped = true;
            return; // 不调用 base.BeginHold，玩家保持空手
        }
        // 装备栏满 → 什么都不做（CanBePickedUp 应已提前拦截）
    }

    public override bool CanBePickedUp()
    {
        if (IsEquipped) return false;
        return base.CanBePickedUp();
    }

    public override bool CanBePickedUp(Transform holdPoint)
    {
        if (IsEquipped) return false;
        if (!base.CanBePickedUp()) return false;
        if (holdPoint == null) return true;
        var holder = holdPoint.GetComponentInParent<PlayerAccessoryHolder>();
        if (holder == null) return true;
        return holder.CanEquip();
    }

    // ── 装备 / 卸下（由 PlayerAccessoryHolder 调用）──────

    /// <summary>将饰品附着到玩家挂点上</summary>
    public void AttachToPlayer(Transform mountPoint, uint ownerNetId)
    {
        originalScale = transform.localScale;

        // 关闭物理
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        if (itemColliders != null)
        {
            foreach (var c in itemColliders)
                if (c != null) c.enabled = false;
        }

        transform.SetParent(mountPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = originalScale * equippedScale;

        _equippedByNetId = ownerNetId;
        SetNetworkTransformSync(false);

        if (debugLog)
            Debug.Log($"<color=#FFD700>[AccessoryItem]</color> {name} 已装备到 {mountPoint.name}，归属 netId={ownerNetId}");
    }

    /// <summary>从玩家身上卸下，掉落到指定位置</summary>
    public void DetachFromPlayer(Vector3 spawnPos)
    {
        _equippedByNetId = 0;

        transform.SetParent(null);
        transform.localScale = originalScale;
        transform.position = spawnPos;

        // 恢复物理
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (itemColliders != null)
        {
            foreach (var c in itemColliders)
                if (c != null) c.enabled = true;
        }

        if (debugLog)
            Debug.Log($"<color=#FFD700>[AccessoryItem]</color> {name} 已卸下，掉落到 {spawnPos}");
    }

    // ── 子类重写的效果钩子 ────────────────────────────

    /// <summary>装备时触发（子类在此添加属性加成）</summary>
    public virtual void OnEquipped(PlayerAttributes attrs) { }

    /// <summary>卸下时触发（子类在此移除属性加成）</summary>
    public virtual void OnUnequipped(PlayerAttributes attrs) { }
}
