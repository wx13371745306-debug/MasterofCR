using System.Collections.Generic;
using UnityEngine;

public class FryPot : MonoBehaviour
{
    [Header("Refs")]
    public FryRecipeDatabase recipeDatabase;
    public GameObject failedDishPrefab;

    [Tooltip("专门用于放入原材料的锅内 PlacePoint。食材放上去后会被锅自动吸收。")]
    public ItemPlacePoint ingredientPlacePoint;

    [Header("Visual Settings")]
    [Tooltip("视觉模型生成的父节点（建议在锅底稍微向上偏移一点）")]
    public Transform visualContainer;
    [Tooltip("随机位置范围，防止食材完全重叠")]
    public float spawnRandomRange = 0.08f;

    [Header("State")]
    public float currentProgress;
    public float requiredProgress;
    public bool cookingFinished;
    public bool cookingFailed;

    [Header("Debug")]
    public bool debugLog = true;

    private readonly Dictionary<FryIngredientId, int> materials = new Dictionary<FryIngredientId, int>();
    private FryRecipeDatabase.FryRecipe finishedRecipe;

    // 追踪动态生成的物体
    private List<GameObject> spawnedIngredientVisuals = new List<GameObject>();
    private GameObject spawnedFinishedVisual;

    void OnEnable()
    {
        // 订阅事件：当有点东西放进食材点时，自动触发吸收
        if (ingredientPlacePoint != null)
            ingredientPlacePoint.OnItemPlacedEvent += TryConsumePlacedIngredient;
    }

    void OnDisable()
    {
        if (ingredientPlacePoint != null)
            ingredientPlacePoint.OnItemPlacedEvent -= TryConsumePlacedIngredient;
    }

    void TryConsumePlacedIngredient(CarryableItem item)
    {
        if (cookingFinished || item == null) return;

        FryableItem fry = item.GetComponent<FryableItem>();
        if (fry == null) return;

        if (fry.visualInPotPrefab != null && visualContainer != null)
        {
            GameObject v = Instantiate(fry.visualInPotPrefab, visualContainer);
            v.transform.localPosition = Random.insideUnitSphere * spawnRandomRange;
            v.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            spawnedIngredientVisuals.Add(v);
        }

        int addValue = materials.Count == 0 ? fry.baseRequired : fry.addedRequired;
        requiredProgress += addValue;

        if (!materials.ContainsKey(fry.ingredientId))
            materials[fry.ingredientId] = 0;
        materials[fry.ingredientId]++;

        // 清理点位占用并销毁物体
        ingredientPlacePoint.ClearOccupant();

        if (debugLog)
            Debug.Log($"[FryPot] Consumed ingredient: {item.name}, id={fry.ingredientId}, add={addValue}, progress={currentProgress:F1}/{requiredProgress:F1}");

        Destroy(item.gameObject);
    }

    public bool HasAnyIngredient()
    {
        return materials.Count > 0;
    }

    public bool CanReceiveProgress()
    {
        return !cookingFinished && requiredProgress > 0f;
    }

    public void AddProgress(float amount)
    {
        if (!CanReceiveProgress()) return;
        if (amount <= 0f) return;

        currentProgress += amount;

        if (currentProgress >= requiredProgress)
        {
            currentProgress = requiredProgress;
            ResolveRecipe();
        }
    }

    void ResolveRecipe()
    {
        if (cookingFinished) return;
        cookingFinished = true;

        if (recipeDatabase == null)
        {
            cookingFailed = true;
            if (debugLog) Debug.LogWarning("[FryPot] Resolve failed: recipeDatabase is null.");
            // 如果失败，暂时用 failedDishPrefab 顶替视觉表现
            SpawnFinishedVisual(failedDishPrefab);
            return;
        }

        var recipe = recipeDatabase.FindMatch(materials);

        if (recipe == null)
        {
            cookingFailed = true;
            if (debugLog) Debug.Log("[FryPot] Resolve result: FailedDish");
            // 如果配方不匹配，同样用 failedDishPrefab 顶替
            SpawnFinishedVisual(failedDishPrefab);
            return;
        }

        finishedRecipe = recipe;
        if (debugLog) Debug.Log($"[FryPot] Resolve result: {recipe.recipeName}");

        // 【核心修复】：使用你专门配置的锅内视觉模型
        SpawnFinishedVisual(finishedRecipe.finishedVisualPrefab);
    }

    void SpawnFinishedVisual(GameObject prefab)
    {
        // 1. 清理掉正在做菜过程中的原材料模型
        ClearIngredientVisuals();

        if (prefab != null && visualContainer != null)
        {
            // 2. 实例化专属的视觉模型
            spawnedFinishedVisual = Instantiate(prefab, visualContainer);
            spawnedFinishedVisual.transform.localPosition = Vector3.zero;
            spawnedFinishedVisual.transform.localRotation = Quaternion.identity;

            // 3. 防御性代码（可选）：
            // 既然你已经专门准备了 finishedVisualPrefab，正常情况下它应该只有 MeshRenderer。
            // 但为了防止有时候图省事直接拖了实体 Prefab 进来，这里还是保留剔除物理组件的安全网。
            Collider[] colliders = spawnedFinishedVisual.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            Rigidbody rb = spawnedFinishedVisual.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            CarryableItem itemComp = spawnedFinishedVisual.GetComponent<CarryableItem>();
            if (itemComp != null) itemComp.enabled = false;
        }
    }

    public bool CanServe()
    {
        return cookingFinished;
    }

    public GameObject Serve()
    {
        if (!cookingFinished)
        {
            if (debugLog) Debug.LogWarning("[FryPot] Serve failed: cooking not finished.");
            return null;
        }

        GameObject result = null;

        if (cookingFailed)
            result = failedDishPrefab;
        else if (finishedRecipe != null)
            result = finishedRecipe.resultPrefab;

        if (spawnedFinishedVisual != null)
            Destroy(spawnedFinishedVisual);

        if (result == null && debugLog)
            Debug.LogWarning("[FryPot] Serve returned null prefab.");

        ResetPot();
        return result;
    }

    void ResetPot()
    {
        materials.Clear();
        currentProgress = 0f;
        requiredProgress = 0f;
        cookingFinished = false;
        cookingFailed = false;
        finishedRecipe = null;

        ClearIngredientVisuals();
        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);
    }

    private void ClearIngredientVisuals()
    {
        foreach (var v in spawnedIngredientVisuals)
        {
            if (v != null) Destroy(v);
        }
        spawnedIngredientVisuals.Clear();
    }
}