using UnityEngine;

/// <summary>
/// 挂在 GlobalManagers 上。监听 DayCycleManager.OnDayAdvanced，
/// 在黑屏换日时将场景中所有成品菜替换为 FailedDish（模拟隔夜腐烂），
/// 同时将锅中已完成的菜也标记为 FailedDish。
/// </summary>
public class OvernightSpoilHandler : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("用于获取 failedDishRecipe.resultPrefab 的配方数据库")]
    [SerializeField] private FryRecipeDatabase recipeDatabase;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    void OnEnable()
    {
        if (DayCycleManager.Instance != null)
            DayCycleManager.Instance.OnDayAdvanced += HandleOvernight;
    }

    void OnDisable()
    {
        if (DayCycleManager.Instance != null)
            DayCycleManager.Instance.OnDayAdvanced -= HandleOvernight;
    }

    void HandleOvernight()
    {
        SpoilSceneDishes();
        SpoilFryPots();
    }

    /// <summary>将场景中所有放置/掉落的成品菜替换为 FailedDish 预制体。</summary>
    void SpoilSceneDishes()
    {
        if (recipeDatabase == null || recipeDatabase.failedDishRecipe == null) return;

        var failedRecipe = recipeDatabase.failedDishRecipe;
        GameObject failedPrefab = failedRecipe.resultPrefab;
        if (failedPrefab == null)
        {
            if (debugLog) Debug.LogWarning("[OvernightSpoil] failedDishRecipe.resultPrefab 为空，跳过场景替换。");
            return;
        }

        string failedName = failedRecipe.recipeName;
        var allDishes = FindObjectsByType<DishRecipeTag>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var dish in allDishes)
        {
            if (dish == null) continue;
            if (dish.recipeName == failedName) continue;

            var oldCarryable = dish.GetComponent<CarryableItem>();
            ItemPlacePoint placePoint = oldCarryable != null ? oldCarryable.CurrentPlacePoint : null;
            Transform spawnParent = null;
            Vector3 pos = dish.transform.position;
            Quaternion rot = dish.transform.rotation;

            if (placePoint != null)
            {
                spawnParent = placePoint.attachPoint != null ? placePoint.attachPoint : placePoint.transform;
                placePoint.ClearOccupant();
            }

            Destroy(dish.gameObject);

            GameObject newObj = Instantiate(failedPrefab, pos, rot);

            if (placePoint != null)
            {
                var newCarryable = newObj.GetComponent<CarryableItem>();
                if (newCarryable != null)
                    placePoint.TryAcceptItem(newCarryable);
            }

            count++;
        }

        if (debugLog && count > 0)
            Debug.Log($"[OvernightSpoil] 隔夜腐烂：替换了 {count} 道场景中的菜品为 FailedDish。");
    }

    /// <summary>将所有锅中已完成的成品菜标记为 FailedDish。</summary>
    void SpoilFryPots()
    {
        var allPots = FindObjectsByType<FryPot>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var pot in allPots)
        {
            if (pot == null || !pot.CanServe()) continue;
            pot.SpoilFinishedDish();
            count++;
        }

        if (debugLog && count > 0)
            Debug.Log($"[OvernightSpoil] 隔夜腐烂：{count} 口锅中的成品菜已变为 FailedDish。");
    }
}
