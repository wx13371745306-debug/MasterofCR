using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractionSensor : MonoBehaviour
{
    [Header("Masks")]
    public LayerMask itemMask;
    public LayerMask placePointMask;
    public LayerMask stationMask;

    [Header("Refs")]
    public Transform playerRoot;
    [Tooltip("用于计算距离的基准点（建议设置在玩家胸口或头部）。如果不填则默认使用 playerRoot")]
    public Transform sensorOrigin;
    public PlayerItemInteractor interactor;

    [Header("Debug")]
    public bool debugLog = true; // 开启后才显示 Debug 面板

    private readonly HashSet<CarryableItem> itemCandidates = new HashSet<CarryableItem>();
    private readonly HashSet<ItemPlacePoint> placePointCandidates = new HashSet<ItemPlacePoint>();
    private readonly HashSet<IInteractiveStation> stationCandidates = new HashSet<IInteractiveStation>();

    private CarryableItem currentItem;
    private ItemPlacePoint currentPlacePoint;
    private IInteractiveStation currentStation;
    private CarryableItem currentStackTarget; // 【新增】当前可堆叠的目标（持物状态下）

    void Update()
    {
        if (playerRoot == null || interactor == null) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : playerRoot.position;

        PickNearestItem(originPos);
        PickNearestPlacePoint(originPos);
        PickNearestStation(originPos);
        PickNearestStackTarget(originPos); // 【新增】搜索可堆叠的目标
    }

    void PickNearestItem(Vector3 originPos)
    {
        // enabled = false 的 CarryableItem 表示已被餐桌/系统吞噬，不应再被玩家感知
        itemCandidates.RemoveWhere(i => i == null || !i.enabled || !i.gameObject.activeInHierarchy);

        CarryableItem best = null;
        float bestDist = float.MaxValue;
        foreach (var item in itemCandidates)
        {
            // 只排除正在被别人拿着的物品，其余的交给 Interactor 判断（如 DirtyPlateStack 虽不可直接拾取但需要被检测到）
            if (item.State == CarryableItem.ItemState.Held) continue;

            float dist = Vector3.Distance(originPos, item.transform.position);
            if (dist < bestDist - 0.001f) { bestDist = dist; best = item; }
        }
        currentItem = best;
    }

    void PickNearestPlacePoint(Vector3 originPos)
    {
        placePointCandidates.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);

        ItemPlacePoint best = null;
        float bestDist = float.MaxValue;
        CarryableItem heldItem = interactor.GetHeldItem();

        foreach (var point in placePointCandidates)
        {
            if (heldItem != null)
            {
                CarryableItem ownerItem = point.GetComponentInParent<CarryableItem>();
                if (ownerItem == heldItem) continue;
            }

            float dist = Vector3.Distance(originPos, point.transform.position);

            float effectiveDist = dist;
            if (heldItem != null && point.CurrentItem == null)
            {
                effectiveDist -= 0.1f;
            }

            if (effectiveDist < bestDist - 0.001f)
            {
                bestDist = effectiveDist;
                best = point;
            }
        }
        currentPlacePoint = best;
    }

    void PickNearestStation(Vector3 originPos)
    {
        stationCandidates.RemoveWhere(s => s == null || !(s is MonoBehaviour mb) || !mb.gameObject.activeInHierarchy);

        IInteractiveStation best = null;
        float bestDist = float.MaxValue;
        foreach (var station in stationCandidates)
        {
            MonoBehaviour mb = station as MonoBehaviour;
            float dist = Vector3.Distance(originPos, mb.transform.position);
            if (dist < bestDist - 0.001f) { bestDist = dist; best = station; }
        }
        currentStation = best;
    }

    /// <summary>
    /// 【新增】当玩家手持物品时，在已有的 itemCandidates 池中搜索可堆叠的目标。
    /// 目标可以是：
    ///   1. 另一个拥有 StackableProp 且类别匹配的独立物品 → 合并成新堆
    ///   2. 一个已有的 DynamicItemStack 且还有空位 → 放入已有堆
    /// 手里没有东西时，或者手里的东西没有 StackableProp，stackTarget = null。
    /// </summary>
    void PickNearestStackTarget(Vector3 originPos)
    {
        currentStackTarget = null;
        
        CarryableItem heldItem = interactor != null ? interactor.GetHeldItem() : null;
        if (heldItem == null) return; // 空手时不搜索

        // 手持物品必须有 StackableProp 或者自己就是 Stack（不太可能但做防御）
        StackableProp heldStackable = heldItem.GetComponent<StackableProp>();
        if (heldStackable == null) return; // 手里的东西不可堆叠

        CarryableItem best = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in itemCandidates)
        {
            if (candidate == null || candidate == heldItem) continue;
            if (!candidate.gameObject.activeInHierarchy) continue;

            // 情况1：候选是一个已有的 DynamicItemStack
            DynamicItemStack existingStack = candidate as DynamicItemStack;
            if (existingStack != null)
            {
                if (existingStack.CanAccept(heldItem))
                {
                    float dist = Vector3.Distance(originPos, candidate.transform.position);
                    if (dist < bestDist - 0.001f) { bestDist = dist; best = candidate; }
                }
                continue;
            }

            // 情况2：候选是一个落单的、拥有 StackableProp 的同类物品
            StackableProp candidateStackable = candidate.GetComponent<StackableProp>();
            if (candidateStackable != null && candidateStackable.CanStackWith(heldItem))
            {
                // 候选不能正在被别人拿着
                if (candidate.State == CarryableItem.ItemState.Held) continue;

                float dist = Vector3.Distance(originPos, candidate.transform.position);
                if (dist < bestDist - 0.001f) { bestDist = dist; best = candidate; }
            }
        }

        currentStackTarget = best;
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & itemMask) != 0)
        {
            CarryableItem item = other.GetComponentInParent<CarryableItem>();
            if (item != null) itemCandidates.Add(item);
        }
        if (((1 << other.gameObject.layer) & placePointMask) != 0)
        {
            ItemPlacePoint point = other.GetComponentInParent<ItemPlacePoint>();
            if (point != null) 
            {
                placePointCandidates.Add(point);
                if (debugLog) Debug.Log($"<color=#00FF00>[Sensor 进入]</color> 发现放置点: <b>{point.name}</b>。触发它的碰撞体是: {other.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})。当前列表数量: {placePointCandidates.Count}");
            }
        }
        if (((1 << other.gameObject.layer) & stationMask) != 0)
        {
            IInteractiveStation station = other.GetComponentInParent<IInteractiveStation>();
            if (station != null) stationCandidates.Add(station);
        }
    }

    void OnTriggerExit(Collider other)
    {
        CarryableItem item = other.GetComponentInParent<CarryableItem>();
        if (item != null) itemCandidates.Remove(item);

        ItemPlacePoint point = other.GetComponentInParent<ItemPlacePoint>();
        if (point != null) 
        {
            bool removed = placePointCandidates.Remove(point);
            if (debugLog) Debug.Log($"<color=#FF9900>[Sensor 离开]</color> 移除放置点: <b>{point.name}</b>。触发者: {other.name}。是否成功移除: {removed}。当前列表数量: {placePointCandidates.Count}");
        }
        else if (((1 << other.gameObject.layer) & placePointMask) != 0)
        {
            // 碰撞体属于 PlacePoint 层但 GetComponentInParent 已经找不到了
            // 说明父子层级被改变（如被 DishPlaceSystem 吞噬后 reparent），按碰撞体实例做暴力清理
            placePointCandidates.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
            if (debugLog) Debug.Log($"<color=#FF9900>[Sensor 离开]</color> '{other.name}' 失去了 PlacePoint 父级，已对列表做一次清理。剩余: {placePointCandidates.Count}");
        }

        IInteractiveStation station = other.GetComponentInParent<IInteractiveStation>();
        if (station != null) stationCandidates.Remove(station);
    }

    public CarryableItem GetCurrentItem() => currentItem;
    public ItemPlacePoint GetCurrentPlacePoint() => currentPlacePoint;
    public IInteractiveStation GetCurrentStation() => currentStation;
    public CarryableItem GetCurrentStackTarget() => currentStackTarget; // 【新增】

    /// <summary>
    /// 【新增】让运行时动态生成的 CarryableItem（如 DynamicItemStack）手动注册到 Sensor 里。
    /// 解决在 Sensor 范围内原地创建时 OnTriggerEnter 不会触发的问题。
    /// </summary>
    public void RegisterItem(CarryableItem item)
    {
        if (item != null)
        {
            itemCandidates.Add(item);
            if (debugLog) Debug.Log($"<color=#00FFFF>[Sensor]</color> 手动注册了 Item: {item.name}");
        }
    }

    public void UnregisterItem(CarryableItem item)
    {
        if (item != null) itemCandidates.Remove(item);
    }
    
    // 省略 OnGUI 和 OnDrawGizmos (保留你原先的即可) ...
    // ...

    private void OnGUI()
    {
        if (!debugLog) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : (playerRoot != null ? playerRoot.position : Vector3.zero);

        // 在左上角绘制一个半透明黑底的 Debug 窗口
        GUILayout.BeginArea(new Rect(10, 10, 350, Screen.height - 20), GUI.skin.box);
        GUI.color = Color.white;

        GUILayout.Label($"<b>[Sensor Debug]</b> Origin: {originPos}", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Space(10);

        // 1. 打印 PlacePoint 状态
        GUILayout.Label($"<b>--- PlacePoints ({placePointCandidates.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.green } });
        foreach (var p in placePointCandidates)
        {
            if (p == null) continue;
            float dist = Vector3.Distance(originPos, p.transform.position);
            string prefix = (p == currentPlacePoint) ? "<b><color=yellow>[BEST]</color></b> " : "";
            string status = p.CurrentItem != null ? $"[占用: {p.CurrentItem.name}]" : "[空闲]";
            GUILayout.Label($"{prefix}{p.name} | Dist: {dist:F2} | {status}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        // 2. 打印 Station 状态
        GUILayout.Label($"<b>--- Stations ({stationCandidates.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.cyan } });
        foreach (var s in stationCandidates)
        {
            if (s == null) continue;
            MonoBehaviour mb = s as MonoBehaviour;
            float dist = Vector3.Distance(originPos, mb.transform.position);
            string prefix = (s == currentStation) ? "<b><color=yellow>[BEST]</color></b> " : "";
            bool canInteract = s.CanInteract(interactor);
            GUILayout.Label($"{prefix}{mb.name} | Dist: {dist:F2} | CanInteract: {canInteract}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        // 3. 打印 Item 状态
        GUILayout.Label($"<b>--- Items ({itemCandidates.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.white } });
        foreach (var i in itemCandidates)
        {
            if (i == null) continue;
            float dist = Vector3.Distance(originPos, i.transform.position);
            string prefix = (i == currentItem) ? "<b><color=yellow>[BEST]</color></b> " : "";
            GUILayout.Label($"{prefix}{i.name} | Dist: {dist:F2}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        // 4.【新增】打印 Stack Target 状态
        string stackTargetText = currentStackTarget != null ? currentStackTarget.name : "无";
        GUILayout.Label($"<b>--- Stack Target ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.magenta } });
        GUILayout.Label($"当前堆叠目标: <b>{stackTargetText}</b>", new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (!debugLog || !Application.isPlaying) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : (playerRoot != null ? playerRoot.position : transform.position);

        // 绘制基准点
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(originPos, 0.05f);

        // 连线到当前的 PlacePoint
        if (currentPlacePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(originPos, currentPlacePoint.transform.position);
            Gizmos.DrawWireSphere(currentPlacePoint.transform.position, 0.1f);
        }

        // 连线到当前的 Station
        if (currentStation != null && currentStation is MonoBehaviour mb)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(originPos, mb.transform.position);
            Gizmos.DrawWireCube(mb.transform.position, Vector3.one * 0.2f);
        }
    }
}