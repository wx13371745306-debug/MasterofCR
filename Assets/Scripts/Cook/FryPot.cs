using System.Collections.Generic;
using UnityEngine;

public class FryPot : MonoBehaviour
{
    [Header("Refs")]
    public FryRecipeDatabase recipeDatabase;
    // 删除：public GameObject failedDishPrefab; (已经移交给 Database 管理)

    [Tooltip("专门用于放入原材料的锅内 PlacePoint。食材放上去后会被锅自动吸收。")]
    public ItemPlacePoint ingredientPlacePoint;

    [Header("Visual Settings")]
    public Transform visualContainer;
    public float spawnRandomRange = 0.08f;

    [Header("State")]

    public float currentProgress;
    public float requiredProgress;
    public bool cookingFinished;
    // 删除：public bool cookingFailed; (不再需要这个状态标记)

    [Header("Debug")]
    public bool debugLog = true;

    private readonly Dictionary<FryIngredientId, int> materials = new Dictionary<FryIngredientId, int>();
    private FryRecipeDatabase.FryRecipe finishedRecipe;

    private List<GameObject> spawnedIngredientVisuals = new List<GameObject>();
    private GameObject spawnedFinishedVisual;

    void Update()
    {
        TryConsumePlacedIngredient();
    }

    void TryConsumePlacedIngredient()
    {
        // ... (保持之前的视觉生成和进度累加逻辑，这部分无需修改)
        if (ingredientPlacePoint == null || cookingFinished) return;

        CarryableItem item = ingredientPlacePoint.CurrentItem;
        if (item == null) return;

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

        if (!materials.ContainsKey(fry.ingredientId)) materials[fry.ingredientId] = 0;
        materials[fry.ingredientId]++;

        ingredientPlacePoint.ClearOccupant();
        if (debugLog) Debug.Log($"[FryPot] Consumed: {item.name}");
        Destroy(item.gameObject);
    }

    public bool HasAnyIngredient() => materials.Count > 0;
    public bool CanReceiveProgress() => !cookingFinished && requiredProgress > 0f;

    public void AddProgress(float amount)
    {
        if (!CanReceiveProgress() || amount <= 0f) return;
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

        ClearIngredientVisuals();

        if (recipeDatabase == null)
        {
            if (debugLog) Debug.LogWarning("[FryPot] Resolve failed: recipeDatabase is null.");
            return;
        }

        // --- 核心修改：此时 FindMatch 一定会返回一个配方（要么成功，要么兜底的 FailedDish）---
        finishedRecipe = recipeDatabase.FindMatch(materials);

        if (finishedRecipe == null)
        {
            // 只有当你的 Database 连 failedDishRecipe 都没有配置时才会进入这里
            if (debugLog) Debug.LogWarning("[FryPot] Resolve failed: No matched recipe and no fallback FailedDish config!");
            return;
        }

        // 统一处理视觉生成：无论是佛跳墙还是黑暗料理，都按配方的 visual 生成
        if (finishedRecipe.finishedVisualPrefab != null && visualContainer != null)
        {
            spawnedFinishedVisual = Instantiate(finishedRecipe.finishedVisualPrefab, visualContainer);
        }

        if (debugLog) Debug.Log($"[FryPot] Resolve result: {finishedRecipe.recipeName}");
    }

    public bool CanServe() => cookingFinished;

    public GameObject Serve()
    {
        if (!cookingFinished) return null;

        // --- 核心修改：不再需要 if (cookingFailed) 分支，一切以 finishedRecipe 为准 ---
        GameObject result = finishedRecipe != null ? finishedRecipe.resultPrefab : null;

        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);

        if (result == null && debugLog) Debug.LogWarning("[FryPot] Serve returned null prefab.");

        ResetPot();
        return result;
    }

    void ResetPot()
    {
        materials.Clear();
        currentProgress = 0f;
        requiredProgress = 0f;
        cookingFinished = false;
        // cookingFailed = false; (已删除)
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