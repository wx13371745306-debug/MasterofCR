using UnityEngine;

public class PlateTool : MonoBehaviour, IHoldUseTool
{
    [Header("Debug")]
    public bool debugLog = true;

    public bool TryUse(
        PlayerItemInteractor interactor,
        PlayerInteractionSensor sensor,
        CarryableItem selfItem
    )
    {
        if (interactor == null || sensor == null || selfItem == null)
            return false;

        FryPot pot = FindTargetPot(sensor);
        if (pot == null)
        {
            if (debugLog)
                Debug.Log("[PlateTool] Use ignored: no FryPot found from current target.");
            return false;
        }

        if (!pot.CanServe())
        {
            if (debugLog)
                Debug.Log("[PlateTool] Use ignored: pot cannot serve yet.");
            return false;
        }

        GameObject resultPrefab = pot.Serve();
        if (resultPrefab == null)
        {
            if (debugLog)
                Debug.LogWarning("[PlateTool] Use failed: pot returned null resultPrefab.");
            return false;
        }

        CarryableItem resultItemPrefab = resultPrefab.GetComponent<CarryableItem>();
        if (resultItemPrefab == null)
        {
            if (debugLog)
                Debug.LogError("[PlateTool] Use failed: resultPrefab has no CarryableItem.");
            return false;
        }

        Transform holdPoint = interactor.GetHoldPoint();
        if (holdPoint == null)
        {
            if (debugLog)
                Debug.LogError("[PlateTool] Use failed: interactor holdPoint is null.");
            return false;
        }

        // 先生成成品菜
        GameObject newDishObj = Object.Instantiate(resultPrefab);
        CarryableItem newDishItem = newDishObj.GetComponent<CarryableItem>();

        // 让新菜进入手中
        newDishItem.BeginHold(holdPoint);
        interactor.ReplaceHeldItem(newDishItem);

        if (debugLog)
            Debug.Log($"[PlateTool] Served dish: {newDishItem.name}");

        // 最后销毁旧盘子
        Object.Destroy(selfItem.gameObject);

        return true;
    }

    FryPot FindTargetPot(PlayerInteractionSensor sensor)
    {
        // 路线 B：手里拿着东西时，Sensor 主要锁的是 PlacePoint
        ItemPlacePoint point = sensor.GetCurrentPlacePoint();
        if (point != null)
        {
            FryPot potFromPoint = point.GetComponentInParent<FryPot>();
            if (potFromPoint != null)
                return potFromPoint;
        }

        // 兜底：如果以后 Sensor 也能锁 Item，就顺手兼容一下
        CarryableItem item = sensor.GetCurrentItem();
        if (item != null)
        {
            FryPot potFromItem = item.GetComponent<FryPot>();
            if (potFromItem != null)
                return potFromItem;

            potFromItem = item.GetComponentInParent<FryPot>();
            if (potFromItem != null)
                return potFromItem;
        }

        return null;
    }
}