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

    void Update()
    {
        if (playerRoot == null || interactor == null) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : playerRoot.position;

        PickNearestItem(originPos);
        PickNearestPlacePoint(originPos);
        PickNearestStation(originPos);
    }

    void PickNearestItem(Vector3 originPos)
    {
        itemCandidates.RemoveWhere(i => i == null || !i.gameObject.activeInHierarchy);

        CarryableItem best = null;
        float bestDist = float.MaxValue;
        foreach (var item in itemCandidates)
        {
            if (!item.CanBePickedUp()) continue; 

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

        // 【这里是抓幽灵的关键区域】
        ItemPlacePoint point = other.GetComponentInParent<ItemPlacePoint>();
        if (point != null) 
        {
            bool removed = placePointCandidates.Remove(point);
            if (debugLog) Debug.Log($"<color=#FF9900>[Sensor 离开]</color> 移除放置点: <b>{point.name}</b>。触发者: {other.name}。是否成功移除: {removed}。当前列表数量: {placePointCandidates.Count}");
        }
        else
        {
            // 如果一个碰撞体出去了，但它身上已经找不到 PlacePoint 了，这极有可能是父子层级被改变导致的泄漏！
            if (((1 << other.gameObject.layer) & placePointMask) != 0)
            {
                if (debugLog) Debug.LogWarning($"<color=#FF0000>[幽灵警告]</color> 属于 PlacePoint 层的碰撞体 '{other.name}' 离开了雷达，但现在从它身上 GetComponentInParent 已经找不到 ItemPlacePoint 了！它可能变成幽灵了！");
            }
        }

        IInteractiveStation station = other.GetComponentInParent<IInteractiveStation>();
        if (station != null) stationCandidates.Remove(station);
    }

    public CarryableItem GetCurrentItem() => currentItem;
    public ItemPlacePoint GetCurrentPlacePoint() => currentPlacePoint;
    public IInteractiveStation GetCurrentStation() => currentStation;
    
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