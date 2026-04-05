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
    public bool debugLog = true;

    private CarryableItem currentItem;
    private ItemPlacePoint currentPlacePoint;
    private IInteractiveStation currentStation;
    private CarryableItem currentStackTarget;

    private SphereCollider sensorCollider;
    private readonly Collider[] overlapBuffer = new Collider[64];
    private int overlapCount;

    // Debug 用：记录本帧扫描到的各类去重列表
    private readonly List<CarryableItem> debugItems = new List<CarryableItem>();
    private readonly List<ItemPlacePoint> debugPlacePoints = new List<ItemPlacePoint>();
    private readonly List<IInteractiveStation> debugStations = new List<IInteractiveStation>();

    void Awake()
    {
        sensorCollider = GetComponent<SphereCollider>();
        if (sensorCollider != null)
            sensorCollider.enabled = false;
    }

    void Update()
    {
        if (playerRoot == null || interactor == null) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : playerRoot.position;

        // 每帧用 SphereCollider 的参数做一次 OverlapSphere 扫描
        float radius = 2f;
        if (sensorCollider != null)
        {
            Vector3 scale = sensorCollider.transform.lossyScale;
            radius = sensorCollider.radius * Mathf.Max(scale.x, scale.y, scale.z);
        }

        LayerMask combinedMask = itemMask | placePointMask | stationMask;
        overlapCount = Physics.OverlapSphereNonAlloc(originPos, radius, overlapBuffer, combinedMask);

        if (debugLog)
        {
            debugItems.Clear();
            debugPlacePoints.Clear();
            debugStations.Clear();
        }

        PickNearestItem(originPos);
        PickNearestPlacePoint(originPos);
        PickNearestStation(originPos);
        PickNearestStackTarget(originPos);
    }

    void PickNearestItem(Vector3 originPos)
    {
        CarryableItem best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            if ((1 << col.gameObject.layer & itemMask) == 0) continue;

            CarryableItem item = col.GetComponentInParent<CarryableItem>();
            if (item == null || !item.enabled || !item.gameObject.activeInHierarchy) continue;
            if (item.State == CarryableItem.ItemState.Held) continue;

            if (debugLog && !debugItems.Contains(item)) debugItems.Add(item);

            float dist = Vector3.Distance(originPos, item.transform.position);
            if (dist < bestDist - 0.001f) { bestDist = dist; best = item; }
        }
        currentItem = best;
    }

    void PickNearestPlacePoint(Vector3 originPos)
    {
        ItemPlacePoint best = null;
        float bestDist = float.MaxValue;
        CarryableItem heldItem = interactor.GetHeldItem();

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            if ((1 << col.gameObject.layer & placePointMask) == 0) continue;

            ItemPlacePoint point = col.GetComponentInParent<ItemPlacePoint>();
            if (point == null || !point.gameObject.activeInHierarchy) continue;

            if (heldItem != null)
            {
                CarryableItem ownerItem = point.GetComponentInParent<CarryableItem>();
                if (ownerItem == heldItem) continue;
            }

            if (debugLog && !debugPlacePoints.Contains(point)) debugPlacePoints.Add(point);

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
        IInteractiveStation best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            if ((1 << col.gameObject.layer & stationMask) == 0) continue;

            IInteractiveStation station = col.GetComponentInParent<IInteractiveStation>();
            if (station == null) continue;
            MonoBehaviour mb = station as MonoBehaviour;
            if (mb == null || !mb.gameObject.activeInHierarchy) continue;

            if (debugLog && !debugStations.Contains(station)) debugStations.Add(station);

            float dist = Vector3.Distance(originPos, mb.transform.position);
            if (dist < bestDist - 0.001f) { bestDist = dist; best = station; }
        }
        currentStation = best;
    }

    void PickNearestStackTarget(Vector3 originPos)
    {
        currentStackTarget = null;

        CarryableItem heldItem = interactor != null ? interactor.GetHeldItem() : null;
        if (heldItem == null) return;

        StackableProp heldStackable = heldItem.GetComponent<StackableProp>();
        if (heldStackable == null) return;

        CarryableItem best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            if ((1 << col.gameObject.layer & itemMask) == 0) continue;

            CarryableItem candidate = col.GetComponentInParent<CarryableItem>();
            if (candidate == null || candidate == heldItem) continue;
            if (!candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;

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

            StackableProp candidateStackable = candidate.GetComponent<StackableProp>();
            if (candidateStackable != null && candidateStackable.CanStackWith(heldItem))
            {
                if (candidate.State == CarryableItem.ItemState.Held) continue;

                float dist = Vector3.Distance(originPos, candidate.transform.position);
                if (dist < bestDist - 0.001f) { bestDist = dist; best = candidate; }
            }
        }

        currentStackTarget = best;
    }

    public CarryableItem GetCurrentItem() => currentItem;
    public ItemPlacePoint GetCurrentPlacePoint() => currentPlacePoint;
    public IInteractiveStation GetCurrentStation() => currentStation;
    public CarryableItem GetCurrentStackTarget() => currentStackTarget;

    public void RegisterItem(CarryableItem item) { }
    public void UnregisterItem(CarryableItem item) { }

    private void OnGUI()
    {
        if (!debugLog) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : (playerRoot != null ? playerRoot.position : Vector3.zero);

        GUILayout.BeginArea(new Rect(10, 10, 350, Screen.height - 20), GUI.skin.box);
        GUI.color = Color.white;

        GUILayout.Label($"<b>[Sensor Debug]</b> Origin: {originPos}", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Space(10);

        GUILayout.Label($"<b>--- PlacePoints ({debugPlacePoints.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.green } });
        foreach (var p in debugPlacePoints)
        {
            if (p == null) continue;
            float dist = Vector3.Distance(originPos, p.transform.position);
            string prefix = (p == currentPlacePoint) ? "<b><color=yellow>[BEST]</color></b> " : "";
            string status = p.CurrentItem != null ? $"[占用: {p.CurrentItem.name}]" : "[空闲]";
            GUILayout.Label($"{prefix}{p.name} | Dist: {dist:F2} | {status}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        GUILayout.Label($"<b>--- Stations ({debugStations.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.cyan } });
        foreach (var s in debugStations)
        {
            if (s == null) continue;
            MonoBehaviour mb = s as MonoBehaviour;
            float dist = Vector3.Distance(originPos, mb.transform.position);
            string prefix = (s == currentStation) ? "<b><color=yellow>[BEST]</color></b> " : "";
            bool canInteract = s.CanInteract(interactor);
            GUILayout.Label($"{prefix}{mb.name} | Dist: {dist:F2} | CanInteract: {canInteract}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        GUILayout.Label($"<b>--- Items ({debugItems.Count}) ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.white } });
        foreach (var item in debugItems)
        {
            if (item == null) continue;
            float dist = Vector3.Distance(originPos, item.transform.position);
            string prefix = (item == currentItem) ? "<b><color=yellow>[BEST]</color></b> " : "";
            GUILayout.Label($"{prefix}{item.name} | Dist: {dist:F2}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        GUILayout.Space(10);

        string stackTargetText = currentStackTarget != null ? currentStackTarget.name : "无";
        GUILayout.Label($"<b>--- Stack Target ---</b>", new GUIStyle(GUI.skin.label) { richText = true, normal = new GUIStyleState() { textColor = Color.magenta } });
        GUILayout.Label($"当前堆叠目标: <b>{stackTargetText}</b>", new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (!debugLog || !Application.isPlaying) return;

        Vector3 originPos = sensorOrigin != null ? sensorOrigin.position : (playerRoot != null ? playerRoot.position : transform.position);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(originPos, 0.05f);

        if (currentPlacePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(originPos, currentPlacePoint.transform.position);
            Gizmos.DrawWireSphere(currentPlacePoint.transform.position, 0.1f);
        }

        if (currentStation != null && currentStation is MonoBehaviour mb)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(originPos, mb.transform.position);
            Gizmos.DrawWireCube(mb.transform.position, Vector3.one * 0.2f);
        }
    }
}
