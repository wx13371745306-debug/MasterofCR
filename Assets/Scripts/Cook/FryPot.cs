using System.Collections.Generic;
using UnityEngine;

public class FryPot : MonoBehaviour
{
    [Header("Refs")]
    public FryRecipeDatabase recipeDatabase;
    public GameObject failedDishPrefab;

    [Tooltip("专门用于放入原材料的锅内 PlacePoint。食材放上去后会被锅自动吸收。")]
    public ItemPlacePoint ingredientPlacePoint;

    [Header("State")]
    public float currentProgress;
    public float requiredProgress;

    public bool cookingFinished;
    public bool cookingFailed;

    [Header("Debug")]
    public bool debugLog = true;

    private readonly Dictionary<FryIngredientId, int> materials =
        new Dictionary<FryIngredientId, int>();

    private FryRecipeDatabase.FryRecipe finishedRecipe;

    void Update()
    {
        TryConsumePlacedIngredient();
    }

    void TryConsumePlacedIngredient()
    {
        if (ingredientPlacePoint == null) return;
        if (cookingFinished) return;

        CarryableItem item = ingredientPlacePoint.CurrentItem;
        if (item == null) return;

        FryableItem fry = item.GetComponent<FryableItem>();
        if (fry == null) return;

        int addValue = materials.Count == 0
            ? fry.baseRequired
            : fry.addedRequired;

        requiredProgress += addValue;

        if (!materials.ContainsKey(fry.ingredientId))
            materials[fry.ingredientId] = 0;

        materials[fry.ingredientId]++;

        // 先清掉点位占用，再销毁物体
        ingredientPlacePoint.ClearOccupant(item);

        if (debugLog)
        {
            Debug.Log(
                $"[FryPot] Consumed ingredient: {item.name}, " +
                $"id={fry.ingredientId}, add={addValue}, " +
                $"progress={currentProgress:F1}/{requiredProgress:F1}"
            );
        }

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

            if (debugLog)
                Debug.LogWarning("[FryPot] Resolve failed: recipeDatabase is null.");

            return;
        }

        var recipe = recipeDatabase.FindMatch(materials);

        if (recipe == null)
        {
            cookingFailed = true;

            if (debugLog)
                Debug.Log("[FryPot] Resolve result: FailedDish");

            return;
        }

        finishedRecipe = recipe;

        if (debugLog)
            Debug.Log($"[FryPot] Resolve result: {recipe.recipeName}");
    }

    public bool CanServe()
    {
        return cookingFinished;
    }

    public GameObject Serve()
    {
        if (!cookingFinished)
        {
            if (debugLog)
                Debug.LogWarning("[FryPot] Serve failed: cooking not finished.");
            return null;
        }

        GameObject result = null;

        if (cookingFailed)
        {
            result = failedDishPrefab;
        }
        else if (finishedRecipe != null)
        {
            result = finishedRecipe.resultPrefab;
        }

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

        if (debugLog)
            Debug.Log("[FryPot] Reset.");
    }
}