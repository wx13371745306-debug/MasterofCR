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

    [Header("糊菜倒计时")]
    [Tooltip("烹饪完成后多少秒菜会糊掉")]
    [SerializeField] private float burnTime = 10f;
    [Tooltip("前多少秒为安全期（进度条不闪烁）")]
    [SerializeField] private float burnSafeTime = 4f;
    private float burnElapsed;
    private bool isBurnCountdown;

    public bool IsBurnCountdown => isBurnCountdown;
    public float BurnRatio => burnTime > 0f ? Mathf.Clamp01(burnElapsed / burnTime) : 0f;
    public float BurnSafeRatio => burnTime > 0f ? Mathf.Clamp01(burnSafeTime / burnTime) : 0f;

    [Header("Debug")]
    public bool debugLog = true;

    [Header("UI")]
    public TableOrderProgressUI progressUI;

    private readonly Dictionary<string, int> materials = new Dictionary<string, int>(System.StringComparer.Ordinal);
    private FryRecipeDatabase.FryRecipe finishedRecipe;

    private List<GameObject> spawnedIngredientVisuals = new List<GameObject>();
    private GameObject spawnedFinishedVisual;

    void OnEnable()
    {
        // 订阅事件：当有物品放到锅的专属放置点时，自动触发吸收逻辑
        if (ingredientPlacePoint != null)
        {
            ingredientPlacePoint.OnItemPlacedEvent += OnIngredientPlaced;
        }
    }

    void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        if (ingredientPlacePoint != null)
        {
            ingredientPlacePoint.OnItemPlacedEvent -= OnIngredientPlaced;
        }
    }

    void Start()
    {
        // 游戏开始时，检查一下锅里是不是已经有东西了（初始化兜底）
        if (ingredientPlacePoint != null && ingredientPlacePoint.CurrentItem != null)
        {
            OnIngredientPlaced(ingredientPlacePoint.CurrentItem);
        }
    }

    void Update()
    {
        if (!isBurnCountdown) return;
        burnElapsed += Time.deltaTime;
        if (burnElapsed >= burnTime)
        {
            if (debugLog) Debug.Log("[FryPot] 糊菜倒计时结束，菜糊了！", this);
            isBurnCountdown = false;
            cookingFinished = true;
            SpoilFinishedDish();
        }
    }

    // 当有食材被放置进来时，由 ItemPlacePoint 的事件主动调用这个方法
    void OnIngredientPlaced(CarryableItem item)
    {
        if (item == null || cookingFinished || isBurnCountdown) return;

        FryIngredientTag tag = item.GetComponent<FryIngredientTag>();
        if (tag == null)
        {
            if (debugLog) Debug.LogWarning($"[FryPot] 物体 '{item.name}' 没有 FryIngredientTag，被忽略", item);
            return;
        }

        string id = tag.NormalizedIngredientId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning(
                $"[FryPot] 物体带有 FryIngredientTag 但 ingredientId 为空或仅空白，无法识别为食材: {item.name}",
                item);
            return;
        }

        FryableItem fry = item.GetComponent<FryableItem>();
        if (fry == null)
        {
            Debug.LogWarning(
                $"[FryPot] 物体带有 FryIngredientTag 但缺少 FryableItem，无法下锅: {item.name} (ingredientId={id})",
                item);
            return;
        }

        if (fry.visualInPotPrefab != null && visualContainer != null)
        {
            GameObject v = Instantiate(fry.visualInPotPrefab, visualContainer);
            v.transform.localPosition = Random.insideUnitSphere * spawnRandomRange;
            v.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            spawnedIngredientVisuals.Add(v);
        }

        int addValue = materials.Count == 0 ? fry.baseRequired : fry.addedRequired;
        requiredProgress += addValue;

        if (!materials.ContainsKey(id)) materials[id] = 0;
        materials[id]++;

        ingredientPlacePoint.ClearOccupant();
        if (debugLog)
        {
            Debug.Log($"[FryPot] 吸收食材: '{item.name}' → ingredientId='{id}'  " +
                      $"当前锅内: {DumpMaterials()}  " +
                      $"进度: {currentProgress}/{requiredProgress}", this);
        }
        Destroy(item.gameObject);
    }

    public bool HasAnyIngredient() => materials.Count > 0;
    public bool CanReceiveProgress() => !cookingFinished && !isBurnCountdown && requiredProgress > 0f;

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
        if (cookingFinished || isBurnCountdown) return;

        ClearIngredientVisuals();

        if (recipeDatabase == null)
        {
            cookingFinished = true;
            if (debugLog) Debug.LogWarning("[FryPot] Resolve failed: recipeDatabase is null.");
            return;
        }

        if (debugLog)
            Debug.Log($"[FryPot] === 开始匹配菜谱 ===  锅内材料({materials.Count}种): {DumpMaterials()}", this);

        finishedRecipe = recipeDatabase.FindMatch(materials);

        if (finishedRecipe == null)
        {
            cookingFinished = true;
            Debug.LogWarning("[FryPot] 匹配失败: 无匹配菜谱，且未配置兜底 FailedDish!", this);
            return;
        }

        bool isFailed = (finishedRecipe == recipeDatabase.failedDishRecipe);

        if (debugLog)
            Debug.Log($"[FryPot] 匹配结果: '{finishedRecipe.recipeName}' " +
                      (isFailed ? "(兜底/失败菜，直接完成)" : $"(正常菜谱，进入{burnTime}s糊菜倒计时)"), this);

        if (finishedRecipe.finishedVisualPrefab != null && visualContainer != null)
            spawnedFinishedVisual = Instantiate(finishedRecipe.finishedVisualPrefab, visualContainer);

        if (isFailed)
        {
            cookingFinished = true;
        }
        else
        {
            isBurnCountdown = true;
            burnElapsed = 0f;
        }
    }

    public bool CanDump() => HasAnyIngredient() || cookingFinished || isBurnCountdown;

    public void ForceClear()
    {
        if (debugLog) Debug.Log("[FryPot] ForceClear: 玩家清空了锅。");
        ResetPot();
    }

    /// <summary>将锅内成品强制替换为 FailedDish（糊菜 / 隔夜腐烂共用）。</summary>
    public void SpoilFinishedDish()
    {
        if (recipeDatabase == null) return;
        var failed = recipeDatabase.failedDishRecipe;
        if (failed == null || finishedRecipe == failed) return;

        if (debugLog) Debug.Log($"[FryPot] 菜品变质: '{finishedRecipe?.recipeName}' → '{failed.recipeName}'", this);
        finishedRecipe = failed;

        if (spawnedFinishedVisual != null) Destroy(spawnedFinishedVisual);
        if (failed.finishedVisualPrefab != null && visualContainer != null)
            spawnedFinishedVisual = Instantiate(failed.finishedVisualPrefab, visualContainer);
    }

    public bool CanServe() => cookingFinished || isBurnCountdown;

    public GameObject Serve()
    {
        if (!cookingFinished && !isBurnCountdown) return null;

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
        isBurnCountdown = false;
        burnElapsed = 0f;
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

    private string DumpMaterials()
    {
        if (materials.Count == 0) return "(空)";
        var sb = new System.Text.StringBuilder();
        foreach (var kv in materials)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"'{kv.Key}'×{kv.Value}");
        }
        return sb.ToString();
    }
}