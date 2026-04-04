using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DishPlaceSystem : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = true;

    [Header("动画")]
    [Min(0f)] public float moveDuration = 0.12f;

    void Log(string msg) { if (debugLog) Debug.Log(msg); }
    void LogWarn(string msg) { if (debugLog) Debug.LogWarning(msg); }

    struct PlacedDish
    {
        public GameObject itemObj;
        public DishSize size;
    }

    private readonly List<PlacedDish> servedFood = new List<PlacedDish>();
    private readonly List<PlacedDish> servedDrink = new List<PlacedDish>();
    private readonly Dictionary<Transform, Coroutine> moveRoutines = new Dictionary<Transform, Coroutine>();

    public bool AcceptDish(CarryableItem dish, DishSize size)
    {
        if (dish == null) return false;

        DishPoint[] points = GetComponentsInChildren<DishPoint>(includeInactive: true);
        int drinkSlots = 0, foodSlots = 0;
        foreach (var p in points) { if (p.slotSize == DishSize.D) drinkSlots++; else foodSlots++; }

        int activeDrink = 0, activeFood = 0;
        foreach(var d in servedDrink) if (d.itemObj != null && d.itemObj.activeSelf) activeDrink++;
        foreach(var d in servedFood) if (d.itemObj != null && d.itemObj.activeSelf) activeFood++;

        bool isFull = (size == DishSize.D) ? (activeDrink >= drinkSlots) : (activeFood >= foodSlots);
        if (isFull) 
        {
            Log($"[DishPlaceSystem] 无法正常放入 {dish.name}，对应大类满载。");
            return false;
        }

        ForceAcceptDish(dish, size);
        return true;
    }

    public void ForceAcceptDish(CarryableItem dish, DishSize size)
    {
        if (dish == null) return;

        Log($"[DishPlaceSystem] 接收/强制放入菜品: {dish.name}, 尺寸: {size}");

        // 1. 禁用所有碰撞体
        if (dish.itemColliders != null)
        {
            foreach (var col in dish.itemColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        // 2. 销毁刚体
        Rigidbody rb = dish.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        // 3. 修改层级为 Default，脱离 Sensor 检测
        ChangeLayerRecursively(dish.gameObject, LayerMask.NameToLayer("Default"));

        // 保存对 GameObject 的引用
        GameObject dishObj = dish.gameObject;

        // 4. 失活 CarryableItem 组件而不是销毁它，因为 OrderResponse 还需要这个引用
        dish.enabled = false;

        dishObj.transform.SetParent(transform, true);

        if (size == DishSize.D)
        {
            servedDrink.Add(new PlacedDish { itemObj = dishObj, size = size });
            SortPlaced(servedDrink);
            Log($"[DishPlaceSystem] 饮品列表更新，当前饮品数量: {servedDrink.Count}");
        }
        else
        {
            servedFood.Add(new PlacedDish { itemObj = dishObj, size = size });
            SortPlaced(servedFood);
            Log($"[DishPlaceSystem] 餐食列表更新，当前餐食数量: {servedFood.Count}");
        }

        ReassignAll();
    }

    public void ClearAllDishes()
    {
        Log($"[DishPlaceSystem] 清理桌面：销毁所有已上菜品，共 {servedFood.Count + servedDrink.Count} 份。");
        for (int i = 0; i < servedFood.Count; i++)
        {
            if (servedFood[i].itemObj != null)
                Destroy(servedFood[i].itemObj);
        }
        for (int i = 0; i < servedDrink.Count; i++)
        {
            if (servedDrink[i].itemObj != null)
                Destroy(servedDrink[i].itemObj);
        }

        servedFood.Clear();
        servedDrink.Clear();

        foreach (var kv in moveRoutines)
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
        }
        moveRoutines.Clear();
    }

    public List<DishSize> GetSlotSizes()
    {
        var points = GetComponentsInChildren<DishPoint>(includeInactive: true);
        var result = new List<DishSize>(points.Length);
        foreach (var p in points)
        {
            if (p == null) continue;
            result.Add(p.slotSize);
        }
        return result;
    }

    private void ReassignAll()
    {
        Log($"[DishPlaceSystem] 开始重新分配餐点位置...");
        DishPoint[] allPoints = GetComponentsInChildren<DishPoint>(includeInactive: true);
        var foodPoints = new List<DishPoint>();
        var drinkPoints = new List<DishPoint>();

        foreach (var p in allPoints)
        {
            if (p == null) continue;
            if (p.slotSize == DishSize.D) drinkPoints.Add(p);
            else foodPoints.Add(p);
        }

        foodPoints.Sort((a, b) => CompareSizeDesc(a.slotSize, b.slotSize));
        drinkPoints.Sort((a, b) => CompareSizeDesc(a.slotSize, b.slotSize));

        for (int i = servedFood.Count - 1; i >= 0; i--)
        {
            if (servedFood[i].itemObj == null) servedFood.RemoveAt(i);
        }
        for (int i = servedDrink.Count - 1; i >= 0; i--)
        {
            if (servedDrink[i].itemObj == null) servedDrink.RemoveAt(i);
        }

        var activeFood = servedFood.FindAll(d => d.itemObj != null && d.itemObj.activeSelf);
        var activeDrink = servedDrink.FindAll(d => d.itemObj != null && d.itemObj.activeSelf);

        AssignListToPoints(activeFood, foodPoints, "餐食");
        AssignListToPoints(activeDrink, drinkPoints, "饮品");
    }

    private void AssignListToPoints(List<PlacedDish> dishes, List<DishPoint> points, string logTag)
    {
        int count = Mathf.Min(dishes.Count, points.Count);
        if (dishes.Count > points.Count)
        {
            LogWarn($"[DishPlaceSystem] 警告：{logTag} 数量 ({dishes.Count}) 超出可用位置 ({points.Count})！多出的菜品将堆叠。");
        }

        for (int i = 0; i < dishes.Count; i++)
        {
            var d = dishes[i];
            if (d.itemObj == null) continue;

            Transform targetPoint = (i < points.Count) ? points[i].transform : transform;

            Log($"[DishPlaceSystem] 分配 {logTag}: {d.itemObj.name} -> 点位 {targetPoint.name}");

            if (moveRoutines.TryGetValue(d.itemObj.transform, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            moveRoutines[d.itemObj.transform] = StartCoroutine(MoveRoutine(d.itemObj.transform, targetPoint.position, targetPoint.rotation));
        }
    }

    private IEnumerator MoveRoutine(Transform target, Vector3 toPos, Quaternion toRot)
    {
        if (target == null) yield break;

        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;

        float dur = Mathf.Max(0.0001f, moveDuration);
        float t = 0f;
        while (t < 1f)
        {
            if (target == null) yield break;
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);
            target.position = Vector3.Lerp(fromPos, toPos, k);
            target.rotation = Quaternion.Slerp(fromRot, toRot, k);
            yield return null;
        }

        if (target != null)
        {
            target.position = toPos;
            target.rotation = toRot;
        }
    }

    private void ChangeLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            ChangeLayerRecursively(child.gameObject, newLayer);
        }
    }

    private static void SortPlaced(List<PlacedDish> list)
    {
        list.Sort((a, b) => CompareSizeDesc(a.size, b.size));
    }

    private static int CompareSizeDesc(DishSize a, DishSize b)
    {
        return SizeRank(b).CompareTo(SizeRank(a));
    }

    private static int SizeRank(DishSize s)
    {
        switch (s)
        {
            case DishSize.L: return 3;
            case DishSize.M: return 2;
            case DishSize.S: return 1;
            case DishSize.D: return 0;
            default: return 0;
        }
    }
}
